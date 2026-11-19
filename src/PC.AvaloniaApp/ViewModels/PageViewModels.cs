using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinePerform.Core.CloudSync;
using MinePerform.Core.FileAndDocAutomation;
using MinePerform.Core.LegoTokenEngine;
using MinePerform.Core.SchemaStudio;

namespace MinePerform.PC.ViewModels;

/// <summary>Projected row item for the 56-column DATA DataGrid (100% AOT compiled bindings — zero reflection).</summary>
public sealed partial class DataRowViewItem : ObservableObject
{
    public long RowId { get; init; }
    public string No { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string NoIp { get; init; } = string.Empty;
    public string TanggalIp { get; init; } = string.Empty;
    public string Tps { get; init; } = string.Empty;
    public string Importir { get; init; } = string.Empty;
    public string FclLcl { get; init; } = string.Empty;
    public string JumlahJenis { get; init; } = string.Empty;
    public string JenisBarang { get; init; } = string.Empty;
    public string TotalPdri { get; init; } = string.Empty;
    public string TkPeriksa { get; init; } = string.Empty;
    public string WaktuPkb { get; init; } = string.Empty;
    public string WaktuMulai { get; init; } = string.Empty;
    public string SelesaiPeriksa { get; init; } = string.Empty;
    public string SelesaiLhp { get; init; } = string.Empty;
    public string Temuan { get; init; } = string.Empty;
    public string Notul { get; init; } = string.Empty;
    public string TotalKurangBayar { get; init; } = string.Empty;
    public string NamaFolderOnedrive { get; init; } = string.Empty;
    public bool HasAnomaly { get; init; }
    public string AnomalyTooltip { get; init; } = string.Empty;
    public Dictionary<string, string?> AllCells { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>Column-Group Quick-Jump &amp; Visibility Pill (INFO DASAR, DATA PEMERIKSAAN, PUTUSAN PFPD, LINK DATA).</summary>
public sealed partial class ColumnGroupPillItem : ObservableObject
{
    public string GroupId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int ColumnCount { get; init; }
    public string ColorHex { get; init; } = "#3B82F6";
    public string ChipBgHex { get; init; } = "#1E3A8A40";

    [ObservableProperty]
    private bool _isVisible = true;

    public string PillLabel => $"{Name} ({ColumnCount})";
}

/// <summary>Visual chip for the Lego-Token Palette &amp; Formula Canvas (WrapPanel — Zero Collision).</summary>
public sealed record LegoTokenChipItem(
    string Id,
    string Category,
    string Icon,
    string Label,
    string BorderHex,
    string BackgroundHex,
    string TokenPayload);

/// <summary>IKU portfolio card item (1a-N, 1b-N, 1c-N, or unlimited user-defined IKUs).</summary>
public sealed partial class IkuCardItem : ObservableObject
{
    public IkuDefinition Definition { get; init; } = null!;
    public string Code => Definition.Code;
    public string Title => Definition.Title;
    public string TargetLabel => $"Target: {Definition.TargetValue} {Definition.Unit} | {Definition.ConsolidationMode}";

    [ObservableProperty]
    private double _scalarValue;

    [ObservableProperty]
    private double _achievementPercent;

    [ObservableProperty]
    private int _includedCount;

    [ObservableProperty]
    private int _excludedCount;

    [ObservableProperty]
    private string _generatedSql = string.Empty;

    public string ScoreBadge => Definition.Unit == "%"
        ? $"{ScalarValue:0.0}%"
        : $"{ScalarValue:0.00}";

    public string StatusColorHex => AchievementPercent >= 100.0 ? "#10B981" : (AchievementPercent >= 80.0 ? "#F59E0B" : "#EF4444");
}

/// <summary>Master-Detail Row Drawer line item (PIB Lembar Lanjutan / Container / Document).</summary>
public sealed record RowDetailDisplayItem(string Badge, string Title, string Subtitle);

/// <summary>Simple key-value pair for the contextual right Inspector panel.</summary>
public sealed record InspectorFieldItem(string Key, string Value, string GroupColorHex);

/// <summary>Bar chart row for TPS distribution &amp; Alasan Mulai &gt; 1 JAM Pareto in Bento-Grid.</summary>
public sealed record BarChartRowItem(string Label, int Count, double WidthRatio);

// =================================================================================================
// PAGE 1: DATA (No-Code SQLite Studio, 4 Super-Headers, 3 Frozen Columns & Master-Detail Drawer)
// =================================================================================================
public sealed partial class DataTablePageViewModel : ObservableObject
{
    private readonly DynamicSqliteStudio _studio;
    private readonly DataIpExcelImporter _importer;

    public ObservableCollection<TableDefinition> Tables { get; } = [];
    public ObservableCollection<ColumnGroupPillItem> ColumnGroupPills { get; } = [];
    public ObservableCollection<DataRowViewItem> Rows { get; } = [];
    public ObservableCollection<RowDetailDisplayItem> SelectedRowDetails { get; } = [];

    [ObservableProperty]
    private TableDefinition? _selectedTable;

    [ObservableProperty]
    private DataRowViewItem? _selectedRow;

    [ObservableProperty]
    private bool _showInfoDasar = true;

    [ObservableProperty]
    private bool _showDataPemeriksaan = true;

    [ObservableProperty]
    private bool _showPutusanPfpd = true;

    [ObservableProperty]
    private bool _showLinkData = true;

    [ObservableProperty]
    private string _statusMessage = "Siap — 56 Kolom dalam 4 Super-Header (3 Kolom Beku)";

    [ObservableProperty]
    private int _totalRowCount;

    [ObservableProperty]
    private int _anomalyCount;

    private string _workspaceId = string.Empty;
    private int _workspaceYear = 2026;

    public DataTablePageViewModel(DynamicSqliteStudio studio, DataIpExcelImporter importer)
    {
        _studio = studio;
        _importer = importer;
    }

    public void LoadWorkspace(WorkspaceModel ws)
    {
        _workspaceId = ws.Id;
        _workspaceYear = ws.Year;

        Tables.Clear();
        foreach (var t in _studio.ListTables(ws.Id))
        {
            Tables.Add(t);
        }

        SelectedTable = Tables.FirstOrDefault(t => t.IsPrimary) ?? Tables.FirstOrDefault();
    }

    partial void OnSelectedTableChanged(TableDefinition? value)
    {
        if (value is not null)
        {
            RefreshTableData(value);
        }
    }

    partial void OnSelectedRowChanged(DataRowViewItem? value)
    {
        SelectedRowDetails.Clear();
        if (value is null || SelectedTable is null) return;

        var details = _studio.GetRowDetails(SelectedTable.Id, value.RowId);
        foreach (var d in details)
        {
            SelectedRowDetails.Add(new RowDetailDisplayItem(d.DetailKind.ToUpperInvariant(), d.DetailKind, d.PayloadJson));
        }

        if (SelectedRowDetails.Count == 0)
        {
            SelectedRowDetails.Add(new RowDetailDisplayItem("DOKUMEN", $"IP {value.NoIp} ({value.TanggalIp})", $"Importir: {value.Importir} • TPS: {value.Tps} • Jalur: {value.FclLcl}"));
            SelectedRowDetails.Add(new RowDetailDisplayItem("PEMERIKSAAN", $"TK: {value.TkPeriksa} • PKB: {value.WaktuPkb}", $"Mulai: {value.WaktuMulai} -> Selesai: {value.SelesaiPeriksa}"));
            SelectedRowDetails.Add(new RowDetailDisplayItem("PUTUSAN", $"Temuan: {value.Temuan} • Notul: {value.Notul}", $"Total PDRI: {value.TotalPdri} • Kurang Bayar: {value.TotalKurangBayar}"));
            if (!string.IsNullOrWhiteSpace(value.NamaFolderOnedrive))
            {
                SelectedRowDetails.Add(new RowDetailDisplayItem("ONEDRIVE", value.NamaFolderOnedrive, "Subfolder target: /Berkas/ & /Foto/"));
            }
        }
    }

    public void RefreshTableData(TableDefinition table, string? searchFilter = null)
    {
        var groups = _studio.GetColumnGroups(table.Id);
        var cols = _studio.GetColumns(table.Id);
        var anomalies = _studio.GetAnomalies(table.Id, unresolvedOnly: true);
        var anomByRow = anomalies.GroupBy(a => a.RowId).ToDictionary(g => g.Key, g => g.ToList());

        ColumnGroupPills.Clear();
        foreach (var g in groups)
        {
            int count = cols.Count(c => c.GroupId == g.Id);
            ColumnGroupPills.Add(new ColumnGroupPillItem
            {
                GroupId = g.Id,
                Name = g.Name,
                ColumnCount = count,
                ColorHex = g.ColorHex,
                ChipBgHex = g.ChipBgHex,
                IsVisible = true
            });
        }

        var dbRows = _studio.QueryRows(table, cols, limit: 500);
        Rows.Clear();

        foreach (var r in dbRows)
        {
            if (!string.IsNullOrWhiteSpace(searchFilter))
            {
                bool match = r.Cells.Values.Any(v => v is not null && v.Contains(searchFilter, StringComparison.OrdinalIgnoreCase));
                if (!match) continue;
            }

            anomByRow.TryGetValue(r.RowId, out var rowAnoms);
            string anomTip = rowAnoms is { Count: > 0 }
                ? string.Join(" | ", rowAnoms.Select(a => $"⚠️ {a.Message}"))
                : string.Empty;

            Rows.Add(new DataRowViewItem
            {
                RowId = r.RowId,
                No = GetCell(r.Cells, "no"),
                Status = GetCell(r.Cells, "status"),
                NoIp = GetCell(r.Cells, "no_ip"),
                TanggalIp = GetCell(r.Cells, "tanggal_ip"),
                Tps = GetCell(r.Cells, "tps"),
                Importir = GetCell(r.Cells, "importir"),
                FclLcl = GetCell(r.Cells, "fcl_lcl"),
                JumlahJenis = GetCell(r.Cells, "jumlah_jenis"),
                JenisBarang = GetCell(r.Cells, "jenis_barang"),
                TotalPdri = FormatCurrency(GetCell(r.Cells, "total_pdri")),
                TkPeriksa = GetCell(r.Cells, "tk_periksa"),
                WaktuPkb = GetCell(r.Cells, "waktu_pkb"),
                WaktuMulai = GetCell(r.Cells, "waktu_mulai_periksa"),
                SelesaiPeriksa = GetCell(r.Cells, "selesai_periksa"),
                SelesaiLhp = GetCell(r.Cells, "selesai_lhp"),
                Temuan = GetCell(r.Cells, "temuan") == "1" ? "YA" : "TIDAK",
                Notul = GetCell(r.Cells, "notul"),
                TotalKurangBayar = FormatCurrency(GetCell(r.Cells, "total_kurang_bayar")),
                NamaFolderOnedrive = GetCell(r.Cells, "nama_folder_onedrive"),
                HasAnomaly = rowAnoms is { Count: > 0 },
                AnomalyTooltip = anomTip,
                AllCells = r.Cells
            });
        }

        TotalRowCount = (int)_studio.CountRows(table);
        AnomalyCount = anomalies.Count;
        StatusMessage = $"Tabel {table.DisplayName}: {TotalRowCount} baris | {cols.Count} kolom | {AnomalyCount} anomali terdeteksi";
        SelectedRow = Rows.FirstOrDefault();
    }

    [RelayCommand]
    private void ToggleGroup(string groupName)
    {
        switch (groupName.ToUpperInvariant())
        {
            case "ALL":
                ShowInfoDasar = ShowDataPemeriksaan = ShowPutusanPfpd = ShowLinkData = true;
                break;
            case "INFO DASAR":
                ShowInfoDasar = !ShowInfoDasar;
                break;
            case "DATA PEMERIKSAAN":
                ShowDataPemeriksaan = !ShowDataPemeriksaan;
                break;
            case "PUTUSAN PFPD":
                ShowPutusanPfpd = !ShowPutusanPfpd;
                break;
            case "LINK DATA":
                ShowLinkData = !ShowLinkData;
                break;
        }
    }

    [RelayCommand]
    private void AddQuickSampleRow()
    {
        if (SelectedTable is null) return;
        var cols = _studio.GetColumns(SelectedTable.Id);
        int nextNo = TotalRowCount + 1;
        _studio.InsertRow(SelectedTable, cols, new Dictionary<string, string?>
        {
            ["no"] = nextNo.ToString(CultureInfo.InvariantCulture),
            ["status"] = "SPPB",
            ["no_ip"] = (814250 + nextNo).ToString("D6", CultureInfo.InvariantCulture),
            ["tanggal_ip"] = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["tps"] = "NCT1",
            ["importir"] = "PT. CONTOH IMPOR BARU",
            ["fcl_lcl"] = "FCL",
            ["jumlah_jenis"] = "1",
            ["jml_jenis_periksa"] = "1",
            ["jenis_barang"] = "SAMPLE BARANG PEMERIKSAAN",
            ["tk_periksa"] = "30%",
            ["jml_pk"] = "100",
            ["bm"] = "25000000",
            ["ppn"] = "11000000",
            ["temuan"] = "FALSE",
            ["notul"] = "TIDAK"
        }, _workspaceYear);

        RefreshTableData(SelectedTable);
    }

    public ImportReport ImportExcelFile(string filePath)
    {
        string slug = SelectedTable?.Slug ?? "input";
        var report = _importer.ImportExcelSheet(_workspaceId, filePath, slug);
        if (SelectedTable is not null) RefreshTableData(SelectedTable);
        StatusMessage = $"Impor selesai: {report.RowsImported} baris dalam {report.DurationMs} ms ({report.AnomaliesDetected} anomali)";
        return report;
    }

    private static string GetCell(Dictionary<string, string?> cells, string key) =>
        cells.TryGetValue(key, out string? v) && !string.IsNullOrWhiteSpace(v) ? v : string.Empty;

    private static string FormatCurrency(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Rp 0";
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d)
            ? $"Rp {d:N0}"
            : raw;
    }
}

// =================================================================================================
// PAGE 2: IKU & FORMULA (Unlimited IKU Portfolio + Modular 120 FPS Lego-Token WrapPanel Canvas)
// =================================================================================================
public sealed partial class IkuFormulaPageViewModel : ObservableObject
{
    private readonly LegoTokenCompiler _compiler;
    private string _workspaceId = string.Empty;
    private int _workspaceYear = 2026;

    public ObservableCollection<IkuCardItem> IkuCards { get; } = [];
    public ObservableCollection<LegoTokenChipItem> PaletteTokens { get; } = [];
    public ObservableCollection<LegoTokenChipItem> CanvasTokens { get; } = [];

    [ObservableProperty]
    private IkuCardItem? _selectedIku;

    [ObservableProperty]
    private string _auditSummaryText = "Pilih IKU untuk melihat pratinjau audit baris data";

    [ObservableProperty]
    private string _testResultText = "⚡ Uji Hitung: —";

    public IkuFormulaPageViewModel(LegoTokenCompiler compiler)
    {
        _compiler = compiler;
        SeedTokenPalette();
    }

    private void SeedTokenPalette()
    {
        PaletteTokens.Clear();
        PaletteTokens.Add(new("p1", "Column", "📦", "INPUT.No. IP", "#3B82F6", "#1E3A8A40", "COL:input.no_ip"));
        PaletteTokens.Add(new("p2", "Column", "📦", "INPUT.JUMLAH JENIS", "#3B82F6", "#1E3A8A40", "COL:input.jumlah_jenis"));
        PaletteTokens.Add(new("p3", "Column", "📦", "INPUT.CONT 40F≥ PERIKSA", "#3B82F6", "#1E3A8A40", "COL:input.cont_40f_periksa"));
        PaletteTokens.Add(new("p4", "TimeOp", "⏱️", "SELISIH_MENIT(Mulai, Selesai)", "#F59E0B", "#78350F40", "TIME:SELISIH_MENIT"));
        PaletteTokens.Add(new("p5", "Aggregate", "∑", "PERCENT_WHERE(...)", "#8B5CF6", "#4C1D9540", "AGG:PERCENT_WHERE"));
        PaletteTokens.Add(new("p6", "Aggregate", "∑", "COUNT / SUM / AVG", "#8B5CF6", "#4C1D9540", "AGG:COUNT"));
        PaletteTokens.Add(new("p7", "FilterPin", "🏷️", "FCL/LCL = FCL", "#10B981", "#064E3B40", "PIN:fcl_lcl=FCL"));
        PaletteTokens.Add(new("p8", "FilterPin", "🏷️", "TEMUAN? = TRUE", "#10B981", "#064E3B40", "PIN:temuan=1"));
        PaletteTokens.Add(new("p9", "Logic", "🔀", "JIKA - MAKA - LAINNYA", "#EC4899", "#83184340", "IF_ELSE"));
        PaletteTokens.Add(new("p10", "Conversion", "📊", "TABEL_KONVERSI", "#EC4899", "#83184340", "CONV_TABLE"));
        PaletteTokens.Add(new("p11", "SubToken", "🧩", "#Komponen_Ketepatan_Waktu", "#06B6D4", "#164E6340", "SUB:#Komponen_Ketepatan_Waktu"));
        PaletteTokens.Add(new("p12", "SubToken", "🧩", "#Rasio_Temuan_LHP", "#06B6D4", "#164E6340", "SUB:#Rasio_Temuan_LHP"));
        PaletteTokens.Add(new("p13", "SubToken", "🧩", "#Batas_SLA_Kontainer", "#06B6D4", "#164E6340", "SUB:#Batas_SLA_Kontainer"));
    }

    public void LoadWorkspace(WorkspaceModel ws)
    {
        _workspaceId = ws.Id;
        _workspaceYear = ws.Year;
        RefreshIkus();
    }

    public void RefreshIkus()
    {
        IkuCards.Clear();
        var period = new PeriodFilter(PeriodKind.Tahunan, _workspaceYear);

        foreach (var def in _compiler.ListIkuDefinitions(_workspaceId))
        {
            var eval = _compiler.EvaluateIku(def, period);
            IkuCards.Add(new IkuCardItem
            {
                Definition = def,
                ScalarValue = eval.ScalarValue,
                AchievementPercent = eval.AchievementPercent,
                IncludedCount = eval.IncludedCount,
                ExcludedCount = eval.ExcludedCount,
                GeneratedSql = eval.GeneratedSql
            });
        }

        SelectedIku = IkuCards.FirstOrDefault();
    }

    partial void OnSelectedIkuChanged(IkuCardItem? value)
    {
        CanvasTokens.Clear();
        if (value is null) return;

        FlattenAstToVisualChips(value.Definition.FormulaAst, CanvasTokens);
        AuditSummaryText = $"✅ {value.IncludedCount} Dihitung | ❌ {value.ExcludedCount} Dikecualikan";
        TestResultText = $"⚡ Uji Hitung: {value.ScoreBadge} (Capaian {value.AchievementPercent:0.0}%)";
    }

    [RelayCommand]
    private void PinTokenToCanvas(LegoTokenChipItem? chip)
    {
        if (chip is null) return;
        CanvasTokens.Add(chip with { Id = Guid.NewGuid().ToString("N")[..8] });
    }

    [RelayCommand]
    private void RemoveCanvasToken(LegoTokenChipItem? chip)
    {
        if (chip is not null) CanvasTokens.Remove(chip);
    }

    [RelayCommand]
    private void ClearCanvas() => CanvasTokens.Clear();

    [RelayCommand]
    private void TestEvaluateSelectedIku()
    {
        if (SelectedIku is null) return;
        var eval = _compiler.EvaluateIku(SelectedIku.Definition, new PeriodFilter(PeriodKind.Tahunan, _workspaceYear));
        SelectedIku.ScalarValue = eval.ScalarValue;
        SelectedIku.AchievementPercent = eval.AchievementPercent;
        SelectedIku.IncludedCount = eval.IncludedCount;
        SelectedIku.ExcludedCount = eval.ExcludedCount;
        AuditSummaryText = $"✅ {eval.IncludedCount} Dihitung | ❌ {eval.ExcludedCount} Dikecualikan";
        TestResultText = $"⚡ Uji Hitung: {SelectedIku.ScoreBadge} (Capaian {eval.AchievementPercent:0.0}%)";
    }

    [RelayCommand]
    private void AddCustomIku()
    {
        int nextIdx = IkuCards.Count + 1;
        string code = $"{nextIdx}a-N";
        var newDef = new IkuDefinition(
            $"iku_{Guid.NewGuid().ToString("N")[..10]}",
            _workspaceId,
            code,
            $"IKU Kustom #{nextIdx} (Edit di Kanvas Lego-Token)",
            100.0,
            "Dok",
            ConsolidationMode.Sum,
            PolarizationMode.Maximize,
            new AggregateToken("COUNT", "input"),
            PrimaryTableSlug: "input",
            Weight: 1.0,
            SortOrder: nextIdx);
        _compiler.SaveIkuDefinition(newDef);
        RefreshIkus();
        SelectedIku = IkuCards.LastOrDefault();
    }

    private static void FlattenAstToVisualChips(LegoTokenNode node, ObservableCollection<LegoTokenChipItem> chips)
    {
        switch (node)
        {
            case BinaryOpToken bin:
                chips.Add(new(Guid.NewGuid().ToString("N")[..6], "Paren", "", "(", "#475569", "#1E293B", "("));
                FlattenAstToVisualChips(bin.Left, chips);
                chips.Add(new(Guid.NewGuid().ToString("N")[..6], "Op", "", bin.Op, "#64748B", "#334155", bin.Op));
                FlattenAstToVisualChips(bin.Right, chips);
                chips.Add(new(Guid.NewGuid().ToString("N")[..6], "Paren", "", ")", "#475569", "#1E293B", ")"));
                break;
            case SubTokenRefToken st:
                chips.Add(new(Guid.NewGuid().ToString("N")[..6], "SubToken", "🧩", st.TokenName, "#06B6D4", "#164E6340", st.TokenName));
                break;
            case ConversionTableToken ct:
                chips.Add(new(Guid.NewGuid().ToString("N")[..6], "Conversion", "📊", $"{ct.Name}(...)", "#EC4899", "#83184340", ct.Name));
                break;
            case ColumnRefToken col:
                chips.Add(new(Guid.NewGuid().ToString("N")[..6], "Column", "📦", $"{col.TableSlug}.{col.ColumnKey}", "#3B82F6", "#1E3A8A40", col.ColumnKey));
                break;
            case AggregateToken agg:
                chips.Add(new(Guid.NewGuid().ToString("N")[..6], "Aggregate", "∑", $"{agg.Func}({agg.TableSlug})", "#8B5CF6", "#4C1D9540", agg.Func));
                break;
            case TimeOpToken tm:
                chips.Add(new(Guid.NewGuid().ToString("N")[..6], "TimeOp", "⏱️", tm.Operation, "#F59E0B", "#78350F40", tm.Operation));
                break;
            case FilterPinToken fp:
                chips.Add(new(Guid.NewGuid().ToString("N")[..6], "FilterPin", "🏷️", $"{fp.Column.ColumnKey} {fp.Op} {fp.Value}", "#10B981", "#064E3B40", fp.Value));
                break;
            case LiteralNumberToken num:
                chips.Add(new(Guid.NewGuid().ToString("N")[..6], "Num", "", num.Value.ToString(CultureInfo.InvariantCulture), "#64748B", "#1E293B", num.Value.ToString(CultureInfo.InvariantCulture)));
                break;
            default:
                chips.Add(new(Guid.NewGuid().ToString("N")[..6], "Node", "🔹", node.GetType().Name, "#64748B", "#1E293B", ""));
                break;
        }
    }
}

// =================================================================================================
// PAGE 3: CHART & SUMMARY (Bento-Grid Analytics, Period Filter, TPS/Delay Pareto & Premi Run-Rate)
// =================================================================================================
public sealed partial class ChartSummaryPageViewModel : ObservableObject
{
    private readonly BentoAnalyticsEngine _analytics;
    private string _workspaceId = string.Empty;
    private int _workspaceYear = 2026;

    public ObservableCollection<BarChartRowItem> TpsDistribution { get; } = [];
    public ObservableCollection<BarChartRowItem> DelayReasons { get; } = [];
    public ObservableCollection<MonthlyTrendPoint> MonthlyTrend { get; } = [];

    [ObservableProperty]
    private PeriodKind _selectedPeriodKind = PeriodKind.Tahunan;

    [ObservableProperty]
    private string _nkpText = "0.00%";

    [ObservableProperty]
    private string _totalDocsText = "0 IP (0 Temuan)";

    [ObservableProperty]
    private string _fclLclText = "FCL: 0 | LCL: 0";

    [ObservableProperty]
    private string _notulText = "Notul: 0 Dok";

    [ObservableProperty]
    private string _kurangBayarText = "KB: Rp 0";

    [ObservableProperty]
    private string _premiCategoryText = "Kategori: —";

    [ObservableProperty]
    private string _premiRunRateText = "Run-Rate: 0%";

    public ChartSummaryPageViewModel(BentoAnalyticsEngine analytics)
    {
        _analytics = analytics;
    }

    public void LoadWorkspace(WorkspaceModel ws)
    {
        _workspaceId = ws.Id;
        _workspaceYear = ws.Year;
        RefreshSnapshot();
    }

    [RelayCommand]
    private void SelectPeriod(string kindName)
    {
        if (Enum.TryParse(kindName, ignoreCase: true, out PeriodKind kind))
        {
            SelectedPeriodKind = kind;
            RefreshSnapshot();
        }
    }

    public void RefreshSnapshot()
    {
        if (string.IsNullOrEmpty(_workspaceId)) return;
        var snap = _analytics.BuildSnapshot(_workspaceId, new PeriodFilter(SelectedPeriodKind, _workspaceYear));

        NkpText = $"{snap.NkpPercent:0.00}%";
        TotalDocsText = $"{snap.TotalDocuments} IP ({snap.FindingsCount} Temuan)";
        FclLclText = $"FCL: {snap.FclCount} | LCL: {snap.LclCount}";
        NotulText = $"Notul: {snap.NotulCount} Dok";
        KurangBayarText = $"KB: Rp {snap.TotalKurangBayar:N0}";
        PremiCategoryText = $"Kategori: {snap.PremiCategory}";
        PremiRunRateText = $"Run-Rate: {snap.PremiRunRatePercent:0.0}%";

        TpsDistribution.Clear();
        int maxTps = Math.Max(1, snap.TpsDistribution.FirstOrDefault()?.Count ?? 1);
        foreach (var item in snap.TpsDistribution)
        {
            TpsDistribution.Add(new BarChartRowItem(item.Label, item.Count, (double)item.Count / maxTps));
        }

        DelayReasons.Clear();
        int maxDelay = Math.Max(1, snap.DelayReasons.FirstOrDefault()?.Count ?? 1);
        foreach (var item in snap.DelayReasons)
        {
            DelayReasons.Add(new BarChartRowItem(item.Label, item.Count, (double)item.Count / maxDelay));
        }

        MonthlyTrend.Clear();
        foreach (var pt in snap.MonthlyTrend)
        {
            MonthlyTrend.Add(pt);
        }
    }
}

// =================================================================================================
// PAGE 4: KONEKTOR (SIAP-NEO & BOT-MONITOR-PERIKSA) & ONEDRIVE 5GB+ ZERO-COPY PHOTO SORTER
// =================================================================================================
public sealed partial class ConnectorsOneDrivePageViewModel : ObservableObject
{
    private readonly DynamicSqliteStudio _studio;
    private readonly OneDriveBatchSorter _sorter;
    private string _workspaceId = string.Empty;
    private int _workspaceYear = 2026;

    [ObservableProperty]
    private string _siapNeoStatus = "🟢 Siap (IPC / Local DB / Humanized Macro)";

    [ObservableProperty]
    private string _botMonitorStatus = "🟢 Memantau Folder Unduhan";

    [ObservableProperty]
    private ConnectorMode _botMode = ConnectorMode.Otonom;

    [ObservableProperty]
    private string _sourcePhotosDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "FotoPeriksa");

    [ObservableProperty]
    private string _oneDriveYearDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Data_Periksa_2026");

    [ObservableProperty]
    private string _folderPatternPreview = "{No.}. {No. IP}_{Tanggal IP (dd.MM.yyyy)}_{Importir}_{TPS}_{JENIS BARANG}";

    [ObservableProperty]
    private double _sortProgressPercent;

    [ObservableProperty]
    private string _sortStatusLog = "Siap memindahkan foto 5 GB+ via Zero-Copy MoveFileExW";

    [ObservableProperty]
    private string? _lastBatchId;

    public ConnectorsOneDrivePageViewModel(DynamicSqliteStudio studio, OneDriveBatchSorter sorter)
    {
        _studio = studio;
        _sorter = sorter;
    }

    public void LoadWorkspace(WorkspaceModel ws)
    {
        _workspaceId = ws.Id;
        _workspaceYear = ws.Year;
    }

    [RelayCommand]
    private async Task RunPhotoSortAsync(bool dryRun)
    {
        if (!Directory.Exists(SourcePhotosDir))
        {
            SortStatusLog = $"⚠️ Folder sumber tidak ditemukan: {SourcePhotosDir}";
            return;
        }

        SortProgressPercent = 0;
        SortStatusLog = dryRun ? "Menjalankan Dry-Run simulasi pencocokan No. IP..." : "Memindahkan foto via Zero-Copy MoveFileExW...";

        var progress = new Progress<BatchSortProgress>(p =>
        {
            SortProgressPercent = p.TotalFilesDiscovered > 0
                ? (double)p.FilesProcessed / p.TotalFilesDiscovered * 100.0
                : 100.0;
            SortStatusLog = $"[{p.FilesProcessed}/{p.TotalFilesDiscovered}] No. IP {p.CurrentNoIp} — {p.CurrentFileName}";
        });

        var result = await _sorter.SortAndMoveAsync(
            _workspaceId,
            SourcePhotosDir,
            OneDriveYearDir,
            dryRun: dryRun,
            progress: progress);

        LastBatchId = result.DryRun ? null : result.BatchId;
        SortProgressPercent = 100;
        SortStatusLog = result.DryRun
            ? $"Dry-Run: {result.MatchedFolders} folder cocok, {result.UnmatchedFolders} tidak cocok, {result.FilesMoved} berkas siap dipindah."
            : $"Selesai (Zero-Copy): {result.FilesMoved} berkas dipindah ({result.TotalBytesMoved / (1024.0 * 1024.0):0.0} MB), {result.UnmatchedFolders} tidak cocok.";
    }

    [RelayCommand]
    private void UndoLastBatch()
    {
        if (string.IsNullOrEmpty(LastBatchId))
        {
            SortStatusLog = "Tidak ada batch pemindahan yang bisa dibatalkan.";
            return;
        }
        int restored = _sorter.UndoBatch(LastBatchId);
        SortStatusLog = $"Undo selesai: {restored} berkas dikembalikan ke lokasi semula.";
        LastBatchId = null;
    }

    [RelayCommand]
    private void RunOneDriveSqliteBackup()
    {
        string backupDir = Path.Combine(OneDriveYearDir, "_Backups_SQLite");
        var res = OneDriveSqliteBackupService.CreateCompressedBackup(_studio, backupDir, _workspaceYear);
        SortStatusLog = $"Backup SQLite selesai: {Path.GetFileName(res.BackupFilePath)} ({res.CompressedBytes / 1024.0:0.0} KB)";
    }
}
