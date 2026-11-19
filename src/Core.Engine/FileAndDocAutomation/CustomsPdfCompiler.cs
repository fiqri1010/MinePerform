using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using MinePerform.Core.SchemaStudio;
using MinePerform.Core.Serialization;

namespace MinePerform.Core.FileAndDocAutomation;

[JsonConverter(typeof(JsonStringEnumConverter<CustomsDocKind>))]
public enum CustomsDocKind
{
    PibBc20,
    Lhp,
    BillOfLading,
    Unknown
}

/// <summary>Single goods item extracted from PIB Page 1 or multi-page Lembar Lanjutan.</summary>
public sealed record PibGoodsItem(
    [property: JsonPropertyName("item_no")] int ItemNo,
    [property: JsonPropertyName("hs_code")] string HsCode,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("quantity")] decimal Quantity,
    [property: JsonPropertyName("package_unit")] string PackageUnit,
    [property: JsonPropertyName("net_weight_kg")] decimal NetWeightKg,
    [property: JsonPropertyName("cif_value")] decimal CifValue);

/// <summary>Container detail extracted from PIB or B/L.</summary>
public sealed record ContainerDetail(
    [property: JsonPropertyName("container_no")] string ContainerNo,
    [property: JsonPropertyName("size_feet")] string SizeFeet,
    [property: JsonPropertyName("fcl_lcl")] string FclLcl,
    [property: JsonPropertyName("seal_no")] string? SealNo = null);

/// <summary>Compiled structured output from a customs PDF or OCR image.</summary>
public sealed record CompiledCustomsDocument(
    [property: JsonPropertyName("doc_kind")] CustomsDocKind DocKind,
    [property: JsonPropertyName("source_path")] string SourcePath,
    [property: JsonPropertyName("no_ip")] string NoIp,
    [property: JsonPropertyName("header_fields")] Dictionary<string, string?> HeaderFields,
    [property: JsonPropertyName("goods_items")] List<PibGoodsItem> GoodsItems,
    [property: JsonPropertyName("containers")] List<ContainerDetail> Containers,
    [property: JsonPropertyName("page_count")] int PageCount);

/// <summary>Platform OCR abstraction (Windows.Media.Ocr on PC, ML Kit on Android).</summary>
public interface IOcrProvider
{
    Task<string> ExtractTextAsync(string imagePath, CancellationToken ct = default);
}

/// <summary>
/// Streaming page-by-page PDF & OCR customs document compiler (PIB BC 2.0 + Lembar Lanjutan, LHP, B/L)
/// with non-destructive Auto-Merge reconciliation into DynamicSqliteStudio & Master-Detail Row Drawer.
/// </summary>
public sealed partial class CustomsPdfCompiler
{
    [GeneratedRegex(@"(?:NO\.?\s*IP|NOMOR\s*PENDAFTARAN|NO\.?\s*PIB)\s*[:\-]?\s*([A-Z0-9\-\/]{4,24})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoIpRegex();

    [GeneratedRegex(@"(?:TANGGAL\s*IP|TGL\.?\s*PENDAFTARAN|TGL\.?\s*PIB)\s*[:\-]?\s*(\d{1,2}[\-\/\.]\d{1,2}[\-\/\.]\d{4}|\d{4}-\d{2}-\d{2})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TanggalIpRegex();

    [GeneratedRegex(@"(?:IMPORTIR|NAMA\s*IMPORTIR)\s*[:\-]?\s*([^\r\n]{3,80})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImportirRegex();

    [GeneratedRegex(@"(?:TPS|TEMPAT\s*PENIMBUNAN)\s*[:\-]?\s*([A-Z0-9]{3,10})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TpsRegex();

    [GeneratedRegex(@"\b([A-Z]{4}\d{7})\b\s*(?:\(?(20|40|45)\s*(?:FT|F|FEET)?\)?)?", RegexOptions.CultureInvariant)]
    private static partial Regex ContainerRegex();

    [GeneratedRegex(@"(?:^|\n)\s*(\d{1,3})\.\s*(?:HS\s*)?(\d{4}\.\d{2}\.\d{2}|\d{8})\s*[-–:]\s*([^\r\n|]+?)(?:\s*\|\s*([\d,\.]+)\s*([A-Z]+))?(?:\s*\|\s*([\d,\.]+)\s*KG)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GoodsItemRegex();

    [GeneratedRegex(@"(?:KESIMPULAN\s*LHP|KESIMPULAN)\s*[:\-]?\s*([^\r\n]{3,120})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KesimpulanLhpRegex();

    [GeneratedRegex(@"(?:TEMUAN|HASIL\s*TEMUAN)\s*[:\-]?\s*(ADA|YA|TRUE|TIDAK|NIHIL|FALSE)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TemuanRegex();

    /// <summary>Streams a PDF page-by-page using UglyToad.PdfPig and disposes immediately to keep RAM &lt; 25 MB.</summary>
    public CompiledCustomsDocument CompilePdfFile(string pdfPath)
    {
        var sb = new StringBuilder(4096);
        int pageCount;

        using (var doc = PdfDocument.Open(pdfPath))
        {
            pageCount = doc.NumberOfPages;
            for (int p = 1; p <= pageCount; p++)
            {
                var page = doc.GetPage(p);
                sb.AppendLine(page.Text);
            }
        }

        return CompileExtractedText(sb.ToString(), pdfPath, pageCount);
    }

    /// <summary>Compiles raw extracted text (from PDF, OCR, or unit test fixture) into structured customs fields + Master-Detail items.</summary>
    public CompiledCustomsDocument CompileExtractedText(string text, string sourcePath = "memory", int pageCount = 1)
    {
        string upper = text.ToUpperInvariant();
        CustomsDocKind kind = upper.Contains("LAPORAN HASIL PEMERIKSAAN") || upper.Contains("KESIMPULAN LHP")
            ? CustomsDocKind.Lhp
            : upper.Contains("BILL OF LADING") || upper.Contains("B/L NO")
                ? CustomsDocKind.BillOfLading
                : upper.Contains("BC 2.0") || upper.Contains("PEMBERITAHUAN IMPOR BARANG") || upper.Contains("LEMBAR LANJUTAN")
                    ? CustomsDocKind.PibBc20
                    : CustomsDocKind.Unknown;

        var headers = new Dictionary<string, string?>(StringComparer.Ordinal);

        string noIp = string.Empty;
        var ipMatch = NoIpRegex().Match(text);
        if (ipMatch.Success)
        {
            noIp = SmartSanitizer.NormalizeNoIp(ipMatch.Groups[1].Value);
            headers["no_ip"] = noIp;
        }

        var tglMatch = TanggalIpRegex().Match(text);
        if (tglMatch.Success)
        {
            headers["tanggal_ip"] = tglMatch.Groups[1].Value.Trim();
        }

        var impMatch = ImportirRegex().Match(text);
        if (impMatch.Success)
        {
            headers["importir"] = impMatch.Groups[1].Value.Trim();
        }

        var tpsMatch = TpsRegex().Match(text);
        if (tpsMatch.Success)
        {
            headers["tps"] = tpsMatch.Groups[1].Value.Trim().ToUpperInvariant();
        }

        // Extract containers (deduplicated)
        var containers = new List<ContainerDetail>();
        var seenContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match cm in ContainerRegex().Matches(text))
        {
            string contNo = cm.Groups[1].Value.ToUpperInvariant();
            if (!seenContainers.Add(contNo)) continue;
            string size = cm.Groups[2].Success ? cm.Groups[2].Value : "40";
            containers.Add(new ContainerDetail(contNo, $"{size}F", "FCL"));
        }

        if (containers.Count > 0)
        {
            headers["fcl_lcl"] = "FCL";
            headers["jml_cont_40f"] = containers.Count(c => c.SizeFeet is "40F" or "45F").ToString(CultureInfo.InvariantCulture);
            headers["jml_cont_20f"] = containers.Count(c => c.SizeFeet == "20F").ToString(CultureInfo.InvariantCulture);
        }

        // Extract multi-page Lembar Lanjutan goods items
        var goods = new List<PibGoodsItem>();
        foreach (Match gm in GoodsItemRegex().Matches(text))
        {
            int itemNo = int.TryParse(gm.Groups[1].Value, out int n) ? n : goods.Count + 1;
            string hs = gm.Groups[2].Value.Trim();
            string desc = gm.Groups[3].Value.Trim();
            decimal qty = gm.Groups[4].Success && SmartSanitizer.TrySanitizeCurrencyOrDecimal(gm.Groups[4].Value, out decimal q) ? q : 0m;
            string unit = gm.Groups[5].Success ? gm.Groups[5].Value.Trim().ToUpperInvariant() : "PK";
            decimal kg = gm.Groups[6].Success && SmartSanitizer.TrySanitizeCurrencyOrDecimal(gm.Groups[6].Value, out decimal w) ? w : 0m;
            goods.Add(new PibGoodsItem(itemNo, hs, desc, qty, unit, kg, 0m));
        }

        if (goods.Count > 0)
        {
            headers["jumlah_jenis"] = goods.Count.ToString(CultureInfo.InvariantCulture);
            headers["jenis_barang"] = goods[0].Description;
            headers["campuran"] = goods.Count > 1 ? "YA" : "TIDAK";
            if (goods.Count == 1)
            {
                headers["hs_code_1_jenis"] = goods[0].HsCode;
            }
        }

        if (kind == CustomsDocKind.Lhp)
        {
            var kes = KesimpulanLhpRegex().Match(text);
            if (kes.Success) headers["kesimpulan_lhp"] = kes.Groups[1].Value.Trim();

            var tem = TemuanRegex().Match(text);
            if (tem.Success)
            {
                string tv = tem.Groups[1].Value.ToUpperInvariant();
                headers["temuan"] = tv is "ADA" or "YA" or "TRUE" ? "1" : "0";
            }
        }

        return new CompiledCustomsDocument(kind, sourcePath, noIp, headers, goods, containers, pageCount);
    }

    /// <summary>
    /// Non-destructive Auto-Merge into DynamicSqliteStudio: matches existing row by normalized No. IP
    /// (inserting if new) and populates Master-Detail Row Drawer items (goods_item, container, document).
    /// </summary>
    public long AutoMergeIntoStudio(
        DynamicSqliteStudio studio,
        string workspaceId,
        CompiledCustomsDocument doc,
        string tableSlug = "input")
    {
        var ws = studio.GetWorkspaceById(workspaceId)
            ?? throw new InvalidOperationException($"Workspace '{workspaceId}' not found.");
        var table = studio.GetTableBySlug(workspaceId, tableSlug)
            ?? throw new InvalidOperationException($"Table '{tableSlug}' not found.");
        var columns = studio.GetColumns(table.Id);

        long rowId;
        var existing = string.IsNullOrEmpty(doc.NoIp)
            ? null
            : studio.FindRowByColumnValue(table, columns, "no_ip", doc.NoIp);

        if (existing is null)
        {
            (rowId, _) = studio.InsertRow(table, columns, doc.HeaderFields, ws.Year);
        }
        else
        {
            // Non-destructive patch: fill empty fields, or allow LHP authoritative fields to update
            var patch = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var (k, v) in doc.HeaderFields)
            {
                if (string.IsNullOrWhiteSpace(v)) continue;
                existing.Cells.TryGetValue(k, out string? cur);
                bool isLhpOverride = doc.DocKind == CustomsDocKind.Lhp && k is "kesimpulan_lhp" or "temuan" or "kendala_pemeriksaan" or "selesai_lhp";
                if (string.IsNullOrWhiteSpace(cur) || isLhpOverride)
                {
                    patch[k] = v;
                }
            }
            if (patch.Count > 0)
            {
                studio.UpdateRowCells(table, columns, existing.RowId, patch, ws.Year);
            }
            rowId = existing.RowId;
        }

        // Persist Master-Detail Row Drawer records
        int sort = 0;
        foreach (var item in doc.GoodsItems)
        {
            string json = JsonSerializer.Serialize(item, AppJsonContext.Default.PibGoodsItem);
            studio.AddRowDetail(table.Id, rowId, "goods_item", json, sort++);
        }

        foreach (var cont in doc.Containers)
        {
            string json = JsonSerializer.Serialize(cont, AppJsonContext.Default.ContainerDetail);
            studio.AddRowDetail(table.Id, rowId, "container", json, sort++);
        }

        return rowId;
    }
}
