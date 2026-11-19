using System.Diagnostics;
using System.Globalization;
using MiniExcelLibs;
using MinePerform.Core.SchemaStudio;

namespace MinePerform.Core.FileAndDocAutomation;

/// <summary>
/// Streaming Excel/CSV/Smart-Messy-Paste importer powered by MiniExcel (zero DOM memory bloat).
/// Automatically handles Data IP 2026.xlsx 2-row Super-Header layout, embedded newlines in headers ("FCL/\nLCL"),
/// currency anomalies (" $2,880,771,505.92 ", " $- "), date typos ("05/11/2515"), and batches inside SQLite WAL.
/// </summary>
public sealed class DataIpExcelImporter
{
    private readonly DynamicSqliteStudio _studio;

    public DataIpExcelImporter(DynamicSqliteStudio studio)
    {
        _studio = studio;
    }

    /// <summary>
    /// Streams rows from an Excel (.xlsx) sheet (default "INPUT") into a dynamic table in WAL batches.
    /// </summary>
    public ImportReport ImportExcelSheet(
        string workspaceId,
        string xlsxPath,
        string tableSlug = "input",
        string? sheetName = null)
    {
        var sw = Stopwatch.StartNew();
        var ws = _studio.GetWorkspaceById(workspaceId)
            ?? throw new InvalidOperationException($"Workspace '{workspaceId}' not found.");
        var table = _studio.GetTableBySlug(workspaceId, tableSlug)
            ?? throw new InvalidOperationException($"Table '{tableSlug}' not found in workspace.");
        var columns = _studio.GetColumns(table.Id);

        var colLookup = BuildColumnLookup(columns);

        // Stream rows lazily with MiniExcel (useHeaderRow: false so we can detect 2-row Super-Headers cleanly)
        string targetSheet = sheetName ?? MiniExcel.GetSheetNames(xlsxPath).FirstOrDefault() ?? "INPUT";

        int headerRowIndex = -1;
        var excelColToKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        int imported = 0;
        int skipped = 0;
        int anomaliesTotal = 0;
        var sampleAnomalies = new List<DataAnomalyRecord>(20);

        using var tx = _studio.Connection.BeginTransaction();
        int rowIndex = 0;

        foreach (var rawRowObj in MiniExcel.Query(xlsxPath, useHeaderRow: false, sheetName: targetSheet))
        {
            rowIndex++;
            if (rawRowObj is not IDictionary<string, object?> rowDict)
            {
                continue;
            }

            // Detect the actual header row (contains "No. IP" or at least 5 matched column headers)
            if (headerRowIndex < 0)
            {
                var candidateMap = MatchHeaderRow(rowDict, colLookup);
                if (candidateMap.Count >= 4)
                {
                    headerRowIndex = rowIndex;
                    excelColToKey = candidateMap;
                }
                continue;
            }

            // Extract mapped cell values
            var cells = new Dictionary<string, string?>(excelColToKey.Count, StringComparer.Ordinal);
            bool hasMeaningfulData = false;

            foreach (var (excelKey, colKey) in excelColToKey)
            {
                if (!rowDict.TryGetValue(excelKey, out object? rawObj) || rawObj is null)
                {
                    continue;
                }

                string strVal = FormatExcelObject(rawObj);
                if (!string.IsNullOrWhiteSpace(strVal))
                {
                    cells[colKey] = strVal;
                    if (colKey is "no_ip" or "importir" or "tanggal" or "kode_tps")
                    {
                        hasMeaningfulData = true;
                    }
                }
            }

            if (!hasMeaningfulData)
            {
                skipped++;
                continue;
            }

            var (_, rowAnomalies) = _studio.InsertRow(table, columns, cells, ws.Year, tx);
            imported++;
            anomaliesTotal += rowAnomalies.Count;
            if (sampleAnomalies.Count < 20 && rowAnomalies.Count > 0)
            {
                sampleAnomalies.AddRange(rowAnomalies.Take(20 - sampleAnomalies.Count));
            }
        }

        tx.Commit();
        sw.Stop();

        return new ImportReport(tableSlug, imported, skipped, anomaliesTotal, sw.ElapsedMilliseconds, sampleAnomalies);
    }

    /// <summary>
    /// Mode 1: Smart Messy Paste (TSV / CSV / Excel clipboard / chat table) with fuzzy header matching.
    /// </summary>
    public ImportReport ImportSmartMessyPaste(
        string workspaceId,
        string pastedText,
        string tableSlug = "input")
    {
        var sw = Stopwatch.StartNew();
        var ws = _studio.GetWorkspaceById(workspaceId)
            ?? throw new InvalidOperationException($"Workspace '{workspaceId}' not found.");
        var table = _studio.GetTableBySlug(workspaceId, tableSlug)
            ?? throw new InvalidOperationException($"Table '{tableSlug}' not found.");
        var columns = _studio.GetColumns(table.Id);
        var colLookup = BuildColumnLookup(columns);

        string[] lines = pastedText.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            return new ImportReport(tableSlug, 0, lines.Length, 0, sw.ElapsedMilliseconds, []);
        }

        char delimiter = lines[0].Contains('\t') ? '\t' : (lines[0].Contains(';') ? ';' : ',');
        string[] rawHeaders = SplitDelimitedLine(lines[0], delimiter);

        var idxToColKey = new Dictionary<int, string>();
        for (int i = 0; i < rawHeaders.Length; i++)
        {
            string norm = NormalizeHeaderName(rawHeaders[i]);
            if (colLookup.TryGetValue(norm, out string? key))
            {
                idxToColKey[i] = key;
            }
        }

        int imported = 0, skipped = 0, anomaliesTotal = 0;
        var sampleAnomalies = new List<DataAnomalyRecord>(20);

        using var tx = _studio.Connection.BeginTransaction();
        for (int r = 1; r < lines.Length; r++)
        {
            string[] parts = SplitDelimitedLine(lines[r], delimiter);
            var cells = new Dictionary<string, string?>(StringComparer.Ordinal);
            bool hasData = false;

            foreach (var (idx, colKey) in idxToColKey)
            {
                if (idx < parts.Length && !string.IsNullOrWhiteSpace(parts[idx]))
                {
                    cells[colKey] = parts[idx];
                    hasData = true;
                }
            }

            if (!hasData)
            {
                skipped++;
                continue;
            }

            var (_, rowAnom) = _studio.InsertRow(table, columns, cells, ws.Year, tx);
            imported++;
            anomaliesTotal += rowAnom.Count;
            if (sampleAnomalies.Count < 20 && rowAnom.Count > 0)
            {
                sampleAnomalies.AddRange(rowAnom.Take(20 - sampleAnomalies.Count));
            }
        }

        tx.Commit();
        sw.Stop();
        return new ImportReport(tableSlug, imported, skipped, anomaliesTotal, sw.ElapsedMilliseconds, sampleAnomalies);
    }

    private static Dictionary<string, string> BuildColumnLookup(IReadOnlyList<ColumnDefinition> columns)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columns)
        {
            map[NormalizeHeaderName(col.DisplayName)] = col.ColumnKey;
            map[NormalizeHeaderName(col.ColumnKey)] = col.ColumnKey;
        }
        // Real-world Excel header aliases (e.g. "JML CONT 40F?" vs "JML CONT 40F≥")
        map["jmlcont40f"] = "jml_cont_40f";
        map["cont40fperiksa"] = "cont_40f_periksa";
        map["fcllcl"] = "fcl_lcl";
        map["alasanmulai1jam"] = "alasan_mulai_1_jam";
        return map;
    }

    private static Dictionary<string, string> MatchHeaderRow(
        IDictionary<string, object?> rowDict,
        Dictionary<string, string> colLookup)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (excelCol, rawVal) in rowDict)
        {
            if (rawVal is null) continue;
            string header = Convert.ToString(rawVal, CultureInfo.InvariantCulture) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(header)) continue;

            // Preserve distinction between "BM" and "BM-" (PUTUSAN PFPD)
            string trimmed = header.Trim();
            if (trimmed.EndsWith('-') && colLookup.TryGetValue(NormalizeHeaderName(trimmed + "kb"), out string? kbKey))
            {
                map[excelCol] = kbKey;
                continue;
            }

            string norm = NormalizeHeaderName(trimmed);
            if (colLookup.TryGetValue(norm, out string? colKey))
            {
                map[excelCol] = colKey;
            }
        }
        return map;
    }

    private static string NormalizeHeaderName(string raw)
    {
        var chars = new List<char>(raw.Length);
        foreach (char c in raw.Trim())
        {
            if (char.IsLetterOrDigit(c))
            {
                chars.Add(char.ToLowerInvariant(c));
            }
            else if (c == '-' && raw.TrimEnd().EndsWith('-'))
            {
                chars.Add('k');
                chars.Add('b');
            }
        }
        return new string(chars.ToArray());
    }

    private static string FormatExcelObject(object obj) => obj switch
    {
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero
            ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable fmt => fmt.ToString(null, CultureInfo.InvariantCulture),
        _ => obj.ToString() ?? string.Empty
    };

    private static string[] SplitDelimitedLine(string line, char delimiter)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}
