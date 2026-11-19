using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MinePerform.Core.FileAndDocAutomation;
using MinePerform.Core.LegoTokenEngine;
using MinePerform.Core.SchemaStudio;

namespace MinePerform.PC.ViewModels;

/// <summary>
/// Global App Shell ViewModel:
///  - Multi-Year Workspace Switcher + Selective Year Cloner (2026 -> 2027)
///  - Collapsible Navigation Rail (220px <-> 56px)
///  - Global Ctrl+K Search
///  - Contextual Right Inspector (320px)
///  - Live 28px Status Bar telemetry (RAM MB, SIAP-NEO, OneDrive, Sync)
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly DynamicSqliteStudio _studio;
    private readonly LegoTokenCompiler _compiler;
    private readonly BentoAnalyticsEngine _analytics;
    private readonly DataIpExcelImporter _importer;
    private readonly OneDriveBatchSorter _sorter;
    private readonly YearBlueprintCloner _cloner;
    private readonly DispatcherTimer _telemetryTimer;

    public DataTablePageViewModel DataPage { get; }
    public IkuFormulaPageViewModel IkuPage { get; }
    public ChartSummaryPageViewModel ChartPage { get; }
    public ConnectorsOneDrivePageViewModel ConnectorsPage { get; }

    public ObservableCollection<WorkspaceModel> Workspaces { get; } = [];
    public ObservableCollection<InspectorFieldItem> InspectorFields { get; } = [];

    [ObservableProperty]
    private WorkspaceModel? _activeWorkspace;

    [ObservableProperty]
    private int _selectedNavIndex; // Default to Ringkasan (Bento-Grid Dashboard)

    [ObservableProperty]
    private bool _isNavExpanded = true;

    [ObservableProperty]
    private bool _isInspectorOpen;

    [ObservableProperty]
    private string _globalSearchQuery = string.Empty;

    [ObservableProperty]
    private string _ramTelemetryText = "⚡ RAM: 18.0 MB (Native AOT)";

    [ObservableProperty]
    private string _googleAccountStatus = "🟢 Google Sync";

    [ObservableProperty]
    private string _bannerNotification = string.Empty;

    public double NavRailWidth => IsNavExpanded ? 220 : 56;
    public bool IsChartPageActive => SelectedNavIndex == 0;
    public bool IsDataPageActive => SelectedNavIndex == 1;
    public bool IsIkuPageActive => SelectedNavIndex == 2;
    public bool IsConnectorsPageActive => SelectedNavIndex == 3;

    public MainWindowViewModel()
    {
        string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MinePerform",
            "mineperform.sqlite");

        _studio = new DynamicSqliteStudio(dbPath);
        _compiler = new LegoTokenCompiler(_studio);
        _analytics = new BentoAnalyticsEngine(_studio, _compiler);
        _importer = new DataIpExcelImporter(_studio);
        _sorter = new OneDriveBatchSorter(_studio);
        _cloner = new YearBlueprintCloner(_studio, _compiler);

        DataPage = new DataTablePageViewModel(_studio, _importer);
        IkuPage = new IkuFormulaPageViewModel(_compiler);
        ChartPage = new ChartSummaryPageViewModel(_analytics);
        ConnectorsPage = new ConnectorsOneDrivePageViewModel(_studio, _sorter);

        DataPage.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DataTablePageViewModel.SelectedRow))
            {
                PopulateInspectorFromRow(DataPage.SelectedRow);
            }
        };

        // Ensure at least Workspace 2026 exists
        var existing = _studio.ListWorkspaces();
        if (existing.Count == 0)
        {
            BlueprintSeed2026.SeedWorkspace2026(_studio, _compiler, 2026);
            existing = _studio.ListWorkspaces();
        }

        foreach (var ws in existing) Workspaces.Add(ws);
        ActiveWorkspace = Workspaces.FirstOrDefault(w => w.IsActive) ?? Workspaces.First();

        // If the table currently has 0 rows (e.g. freshly seeded), seed realistic operational rows!
        var primaryTable = _studio.GetTableBySlug(ActiveWorkspace.Id, "input");
        if (primaryTable != null && _studio.CountRows(primaryTable) == 0)
        {
            BlueprintSeed2026.SeedSampleInspectionRows(_studio, primaryTable, ActiveWorkspace.Year);
            var presensiTable = _studio.GetTableBySlug(ActiveWorkspace.Id, "jam_presensi");
            if (presensiTable != null && _studio.CountRows(presensiTable) == 0)
            {
                BlueprintSeed2026.SeedSamplePresensiRows(_studio, presensiTable, ActiveWorkspace.Year);
            }
            DataPage.LoadWorkspace(ActiveWorkspace);
            ChartPage.LoadWorkspace(ActiveWorkspace);
        }

        UpdateRamTelemetry();
        _telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _telemetryTimer.Tick += (_, _) => UpdateRamTelemetry();
        _telemetryTimer.Start();
    }

    partial void OnIsNavExpandedChanged(bool value) => OnPropertyChanged(nameof(NavRailWidth));

    partial void OnSelectedNavIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsChartPageActive));
        OnPropertyChanged(nameof(IsDataPageActive));
        OnPropertyChanged(nameof(IsIkuPageActive));
        OnPropertyChanged(nameof(IsConnectorsPageActive));

        if (value == 0) ChartPage.RefreshSnapshot();
        else if (value == 2) IkuPage.RefreshIkus();
    }

    partial void OnActiveWorkspaceChanged(WorkspaceModel? value)
    {
        if (value is null) return;
        DataPage.LoadWorkspace(value);
        IkuPage.LoadWorkspace(value);
        ChartPage.LoadWorkspace(value);
        ConnectorsPage.LoadWorkspace(value);
    }

    partial void OnGlobalSearchQueryChanged(string value)
    {
        if (DataPage.SelectedTable is not null)
        {
            DataPage.RefreshTableData(DataPage.SelectedTable, value);
        }
    }

    [RelayCommand]
    private void ToggleNavRail() => IsNavExpanded = !IsNavExpanded;

    [RelayCommand]
    private void SelectNav(string indexStr)
    {
        if (int.TryParse(indexStr, out int idx))
        {
            SelectedNavIndex = idx;
        }
    }

    [RelayCommand]
    private void CloseInspector() => IsInspectorOpen = false;

    [RelayCommand]
    private void CloneToNextYear()
    {
        if (ActiveWorkspace is null) return;
        int nextYear = Workspaces.Max(w => w.Year) + 1;
        var res = _cloner.CloneWorkspace(ActiveWorkspace.Id, nextYear, new YearCloneOptions());
        var newWs = _studio.GetWorkspaceById(res.TargetWorkspaceId);
        if (newWs is not null)
        {
            Workspaces.Insert(0, newWs);
            ActiveWorkspace = newWs;
            BannerNotification = $"Kerangka {ActiveWorkspace.Year} disalin: {res.TablesCloned} tabel, {res.ColumnsCloned} kolom, {res.IkusCloned} IKU (0 baris lama).";
        }
    }

    private void PopulateInspectorFromRow(DataRowViewItem? row)
    {
        InspectorFields.Clear();
        if (row is null)
        {
            IsInspectorOpen = false;
            return;
        }

        InspectorFields.Add(new("No. IP", row.NoIp, "#3B82F6"));
        InspectorFields.Add(new("Importir", row.Importir, "#3B82F6"));
        InspectorFields.Add(new("TPS / FCL-LCL", $"{row.Tps} ({row.FclLcl})", "#3B82F6"));
        InspectorFields.Add(new("TOTAL PDRI", row.TotalPdri, "#3B82F6"));
        InspectorFields.Add(new("Waktu PKB", row.WaktuPkb, "#F59E0B"));
        InspectorFields.Add(new("Selesai LHP", row.SelesaiLhp, "#F59E0B"));
        InspectorFields.Add(new("TEMUAN?", row.Temuan, "#F59E0B"));
        InspectorFields.Add(new("NOTUL? / KB", $"{row.Notul} ({row.TotalKurangBayar})", "#8B5CF6"));
        InspectorFields.Add(new("Folder OneDrive", row.NamaFolderOnedrive, "#10B981"));

        if (row.HasAnomaly)
        {
            InspectorFields.Add(new("⚠️ Radar Anomali", row.AnomalyTooltip, "#F59E0B"));
        }

        IsInspectorOpen = true;
    }

    private void UpdateRamTelemetry()
    {
        double mb = Environment.WorkingSet / (1024.0 * 1024.0);
        RamTelemetryText = $"⚡ RAM: {mb:0.0} MB (Native AOT)";
    }

    public void Dispose()
    {
        _telemetryTimer.Stop();
        _studio.Dispose();
    }
}
