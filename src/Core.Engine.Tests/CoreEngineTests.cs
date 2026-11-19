using System.Text.Json;
using MinePerform.Core.CloudSync;
using MinePerform.Core.FileAndDocAutomation;
using MinePerform.Core.LegoTokenEngine;
using MinePerform.Core.SchemaStudio;
using MinePerform.Core.Serialization;
using Xunit;

namespace MinePerform.Core.Tests;

public sealed class CoreEngineTests
{
    private static string CreateTempDbPath() =>
        Path.Combine(Path.GetTempPath(), $"mineperform_test_{Guid.NewGuid():N}.sqlite");

    private static void SafeDeleteFile(string path)
    {
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            string p = path + suffix;
            if (File.Exists(p))
            {
                try { File.Delete(p); } catch { /* ignore */ }
            }
        }
    }

    // ==================== MODUL 1: SCHEMA STUDIO, SANITIZER, ANOMALY RADAR & YEAR CLONER ====================

    [Fact]
    public void SmartSanitizer_HandlesRealWorldExcelAnomalies()
    {
        // 1. Excel locale currency anomalies
        Assert.True(SmartSanitizer.TrySanitizeCurrencyOrDecimal(" $2,880,771,505.92 ", out decimal big));
        Assert.Equal(2880771505.92m, big);

        Assert.True(SmartSanitizer.TrySanitizeCurrencyOrDecimal(" $- ", out decimal zeroDash));
        Assert.Equal(0m, zeroDash);

        Assert.True(SmartSanitizer.TrySanitizeCurrencyOrDecimal("Rp 144.039.000", out decimal idr));
        Assert.Equal(144039000m, idr);

        // 2. No. IP preserves leading zeros and non-PIB slash codes
        Assert.Equal("000765", SmartSanitizer.NormalizeNoIp("765"));
        Assert.Equal("001081", SmartSanitizer.NormalizeNoIp("001081"));
        Assert.Equal("ATA-003/IMPOR/2026", SmartSanitizer.NormalizeNoIp("ATA-003/IMPOR/2026"));

        // 3. Windows NTFS Path Safety Guard
        string sanitizedFolder = SmartSanitizer.SanitizeWindowsFolderName(
            "57. ATA-003/IMPOR/2026_12.02.2026_PT. ABC:TEST_MAL0_BARANG\nBARIS DUA*?");
        Assert.DoesNotContain('/', sanitizedFolder);
        Assert.DoesNotContain('\n', sanitizedFolder);
        Assert.DoesNotContain(':', sanitizedFolder);
        Assert.DoesNotContain('*', sanitizedFolder);
        Assert.Contains("ATA-003-IMPOR-2026", sanitizedFolder);

        // 4. Date Anomaly Radar catches 05/11/2515 without crashing
        var (iso, anom) = SmartSanitizer.SanitizeDate("05/11/2515", 2026, "tbl_1", 177, "tanggal_ip", false);
        Assert.Equal("2515-11-05", iso);
        Assert.NotNull(anom);
        Assert.Equal("DATE_YEAR_TYPO", anom!.RuleCode);
    }

    [Fact]
    public void BlueprintSeed2026_Creates56ColumnInput_With4SuperHeaders_3FrozenCols_AndComputedColumns()
    {
        string dbPath = CreateTempDbPath();
        try
        {
            using var studio = new DynamicSqliteStudio(dbPath);
            var compiler = new LegoTokenCompiler(studio);
            var ws = BlueprintSeed2026.SeedWorkspace2026(studio, compiler, 2026);

            var tables = studio.ListTables(ws.Id);
            Assert.Equal(5, tables.Count);

            var inputTable = studio.GetTableBySlug(ws.Id, "input")!;
            var groups = studio.GetColumnGroups(inputTable.Id);
            var cols = studio.GetColumns(inputTable.Id);

            Assert.Equal(4, groups.Count);
            Assert.Equal(["INFO DASAR", "DATA PEMERIKSAAN", "PUTUSAN PFPD", "LINK DATA"], groups.Select(g => g.Name).ToArray());
            Assert.Equal(56, cols.Count);
            Assert.Equal(3, cols.Count(c => c.IsFrozen));

            // Insert row with messy currency, slash No. IP, newline in JENIS BARANG, and date typo
            var (rowId, anomalies) = studio.InsertRow(inputTable, cols, new Dictionary<string, string?>
            {
                ["no"] = "57",
                ["status"] = "SPPB",
                ["no_ip"] = "ATA-003/IMPOR/2026",
                ["tanggal_ip"] = "05/11/2515",
                ["tps"] = "MAL0",
                ["importir"] = "PT. BERDIKARI",
                ["fcl_lcl"] = "FCL",
                ["jml_pk"] = "100",
                ["tk_periksa"] = "30%",
                ["jenis_barang"] = "FROZEN\nBEEF",
                ["bm"] = " $100,000.00 ",
                ["ppn"] = " $44,039.00 ",
                ["pph"] = " $- ",
                ["bm_kb"] = "Rp 5.000.000",
                ["denda_adm_kb"] = "Rp 1.000.000"
            }, 2026);

            var saved = studio.QueryRowById(inputTable, cols, rowId)!;
            Assert.Equal("144039", saved.Cells["total_pdri"]);
            Assert.Equal("6000000", saved.Cells["total_kurang_bayar"]);
            Assert.Equal("30", saved.Cells["jml_pk_periksa"]);
            Assert.Equal("57. ATA-003-IMPOR-2026_05.11.2515_PT. BERDIKARI_MAL0_FROZEN BEEF", saved.Cells["nama_folder_onedrive"]);

            // Anomalies captured cleanly: DATE_YEAR_TYPO, MULTILINE_NORMALIZED, MISSING_INSPECTION_COUNT
            Assert.Contains(anomalies, a => a.RuleCode == "DATE_YEAR_TYPO");
            Assert.Contains(anomalies, a => a.RuleCode == "MULTILINE_NORMALIZED");
            Assert.Contains(anomalies, a => a.RuleCode == "MISSING_INSPECTION_COUNT");
        }
        finally
        {
            SafeDeleteFile(dbPath);
        }
    }

    [Fact]
    public void YearBlueprintCloner_Clones2026To2027_Selectively_WithoutCopyingOperationalRows()
    {
        string dbPath = CreateTempDbPath();
        try
        {
            using var studio = new DynamicSqliteStudio(dbPath);
            var compiler = new LegoTokenCompiler(studio);
            var ws2026 = BlueprintSeed2026.SeedWorkspace2026(studio, compiler, 2026);

            // Seed 1 operational row in INPUT and 1 master row in Data Master
            var input2026 = studio.GetTableBySlug(ws2026.Id, "input")!;
            studio.InsertRow(input2026, studio.GetColumns(input2026.Id), new Dictionary<string, string?>
            {
                ["no"] = "1",
                ["no_ip"] = "814253",
                ["importir"] = "BERDIKARI"
            }, 2026);

            var master2026 = studio.GetTableBySlug(ws2026.Id, "data_master")!;
            studio.InsertRow(master2026, studio.GetColumns(master2026.Id), new Dictionary<string, string?>
            {
                ["kategori"] = "TPS",
                ["kode_tps"] = "MAL0",
                ["nama_label"] = "Multi Terminal Indonesia",
                ["aktif"] = "TRUE"
            }, 2026);

            var cloner = new YearBlueprintCloner(studio, compiler);
            var cloneRes = cloner.CloneWorkspace(ws2026.Id, 2027, new YearCloneOptions(
                CloneTablesAndColumns: true,
                CloneRelations: true,
                CloneIkuAndLegoTokens: true,
                CloneConnectorConfigs: true,
                CloneOneDrivePattern: true,
                CloneMasterLookupRows: true));

            Assert.Equal(2027, cloneRes.TargetYear);
            Assert.Equal(5, cloneRes.TablesCloned);
            Assert.Equal(3, cloneRes.RelationsCloned);
            Assert.Equal(3, cloneRes.IkusCloned);
            Assert.Equal(3, cloneRes.SubTokensCloned);
            Assert.Equal(1, cloneRes.MasterRowsCloned);

            // Operational INPUT table in 2027 MUST have 0 rows
            var input2027 = studio.GetTableBySlug(cloneRes.TargetWorkspaceId, "input")!;
            Assert.Equal(0, studio.CountRows(input2027));
        }
        finally
        {
            SafeDeleteFile(dbPath);
        }
    }

    // ==================== MODUL 2: LEGO-TOKEN AST COMPILER & BENTO ANALYTICS ====================

    [Fact]
    public void LegoTokenCompiler_EvaluatesDefaultIkus_Custom4thIku_AndDetectsCycles()
    {
        string dbPath = CreateTempDbPath();
        try
        {
            using var studio = new DynamicSqliteStudio(dbPath);
            var compiler = new LegoTokenCompiler(studio);
            var ws = BlueprintSeed2026.SeedWorkspace2026(studio, compiler, 2026);

            var inputTable = studio.GetTableBySlug(ws.Id, "input")!;
            var inputCols = studio.GetColumns(inputTable.Id);

            // Insert 4 FCL 1-jenis rows (3 within SLA, 1 finding) and 1 LCL row (excluded from 1b-N)
            var sampleRows = new[]
            {
                new Dictionary<string, string?>
                {
                    ["no"] = "1", ["status"] = "SPPB", ["no_ip"] = "814253", ["tanggal_ip"] = "2026-01-10",
                    ["tps"] = "NCT1", ["fcl_lcl"] = "FCL", ["cont_40f_periksa"] = "1", ["cont_20f_periksa"] = "0",
                    ["jumlah_jenis"] = "1", ["jml_jenis_periksa"] = "1",
                    ["waktu_pkb"] = "2026-01-10 08:00:00", ["waktu_mulai_periksa"] = "2026-01-10 08:30:00",
                    ["selesai_periksa"] = "2026-01-10 10:30:00", ["temuan"] = "FALSE", ["notul"] = "TIDAK",
                    ["bm"] = "100000000", ["ppn"] = "44000000"
                },
                new Dictionary<string, string?>
                {
                    ["no"] = "2", ["status"] = "SPPB", ["no_ip"] = "814254", ["tanggal_ip"] = "2026-01-12",
                    ["tps"] = "JICT", ["fcl_lcl"] = "FCL", ["cont_40f_periksa"] = "1", ["cont_20f_periksa"] = "0",
                    ["jumlah_jenis"] = "1", ["jml_jenis_periksa"] = "1",
                    ["waktu_pkb"] = "2026-01-12 08:00:00", ["waktu_mulai_periksa"] = "2026-01-12 08:40:00",
                    ["selesai_periksa"] = "2026-01-12 10:00:00", ["temuan"] = "TRUE", ["notul"] = "YA",
                    ["bm"] = "50000000", ["bm_kb"] = "12000000"
                },
                new Dictionary<string, string?>
                {
                    ["no"] = "3", ["status"] = "SPPB", ["no_ip"] = "814255", ["tanggal_ip"] = "2026-02-05",
                    ["tps"] = "NCT1", ["fcl_lcl"] = "FCL", ["cont_40f_periksa"] = "1", ["cont_20f_periksa"] = "0",
                    ["jumlah_jenis"] = "1", ["jml_jenis_periksa"] = "1",
                    ["waktu_pkb"] = "2026-02-05 09:00:00", ["waktu_mulai_periksa"] = "2026-02-05 09:20:00",
                    ["selesai_periksa"] = "2026-02-05 11:00:00", ["temuan"] = "FALSE", ["notul"] = "TIDAK",
                    ["alasan_mulai_1_jam"] = "Konfirmasi Pengurus"
                },
                new Dictionary<string, string?>
                {
                    ["no"] = "4", ["status"] = "SPPB", ["no_ip"] = "814256", ["tanggal_ip"] = "2026-02-08",
                    ["tps"] = "TER3", ["fcl_lcl"] = "LCL", ["cont_40f_periksa"] = "0", ["cont_20f_periksa"] = "0",
                    ["jumlah_jenis"] = "3", ["jml_jenis_periksa"] = "3",
                    ["waktu_pkb"] = "2026-02-08 08:00:00", ["waktu_mulai_periksa"] = "2026-02-08 08:30:00",
                    ["selesai_periksa"] = "2026-02-08 10:00:00", ["temuan"] = "FALSE", ["notul"] = "TIDAK"
                }
            };

            foreach (var r in sampleRows) studio.InsertRow(inputTable, inputCols, r, 2026);

            // Seed Jam Presensi row for 1c-N
            var presTable = studio.GetTableBySlug(ws.Id, "jam_presensi")!;
            studio.InsertRow(presTable, studio.GetColumns(presTable.Id), new Dictionary<string, string?>
            {
                ["tanggal"] = "2026-01-10",
                ["indeks_absen_impor"] = "4",
                ["indeks_absen_40"] = "4",
                ["indeks_produktivitas"] = "4"
            }, 2026);

            // Add unlimited 4th custom IKU dynamically ("2a-CP")
            compiler.SaveIkuDefinition(new IkuDefinition(
                "iku_custom_4",
                ws.Id,
                "2a-CP",
                "Total Dokumen FCL Diselesaikan",
                2.0,
                "Dok",
                ConsolidationMode.Sum,
                PolarizationMode.Maximize,
                new AggregateToken("COUNT", "input", Filters: [new FilterPinToken(new ColumnRefToken("input", "fcl_lcl"), "=", "FCL")]),
                PrimaryTableSlug: "input",
                Weight: 0.5,
                SortOrder: 4));

            var ikus = compiler.ListIkuDefinitions(ws.Id);
            Assert.Equal(4, ikus.Count);

            var period = new PeriodFilter(PeriodKind.Tahunan, 2026);

            // 1a-N: 100% on-time * 0.75 + 100 (25% findings -> >6% tier = 100) * 0.25 = 100%
            var eval1a = compiler.EvaluateIku(ikus.Single(i => i.Code == "1a-N"), period);
            Assert.Equal(100.0, eval1a.ScalarValue, 2);

            // 1b-N: 3 FCL 1-jenis included, 1 LCL excluded -> 100% on-time (index 5) & <=60m start (index 5) = 5.0
            var eval1b = compiler.EvaluateIku(ikus.Single(i => i.Code == "1b-N"), period);
            Assert.Equal(5.0, eval1b.ScalarValue, 2);
            Assert.Equal(3, eval1b.IncludedCount);
            Assert.Equal(1, eval1b.ExcludedCount);

            // 1c-N: ((4+4)/2)/4 * 70 + (4/4)*30 = 100%
            var eval1c = compiler.EvaluateIku(ikus.Single(i => i.Code == "1c-N"), period);
            Assert.Equal(100.0, eval1c.ScalarValue, 2);

            // Custom 4th IKU 2a-CP: 3 FCL docs
            var evalCustom = compiler.EvaluateIku(ikus.Single(i => i.Code == "2a-CP"), period);
            Assert.Equal(3.0, evalCustom.ScalarValue, 2);

            // Bento-Grid Analytics check
            var analytics = new BentoAnalyticsEngine(studio, compiler);
            var snap = analytics.BuildSnapshot(ws.Id, period);
            Assert.Equal(4, snap.TotalDocuments);
            Assert.Equal(3, snap.FclCount);
            Assert.Equal(1, snap.LclCount);
            Assert.Equal(1, snap.FindingsCount);
            Assert.Equal(1, snap.NotulCount);
            Assert.Equal(194000000m, snap.TotalPdri);
            Assert.Equal(12000000m, snap.TotalKurangBayar);
            Assert.Equal("OPTIMAL", snap.PremiCategory);
            Assert.Contains(snap.TpsDistribution, t => t.Label == "NCT1" && t.Count == 2);

            // Cycle detection in Sub-Tokens
            compiler.SaveSubToken(new SubTokenDefinition("cyc_a", ws.Id, "#CycleA", "", new SubTokenRefToken("#CycleB")));
            compiler.SaveSubToken(new SubTokenDefinition("cyc_b", ws.Id, "#CycleB", "", new SubTokenRefToken("#CycleA")));
            Assert.Throws<InvalidOperationException>(() =>
                compiler.CompileToSql(ws.Id, new SubTokenRefToken("#CycleA")));
        }
        finally
        {
            SafeDeleteFile(dbPath);
        }
    }

    // ==================== MODUL 3: REAL DATA_INPUT.XLSX IMPORT, PDF COMPILER, MACRO & ONEDRIVE SORTER ====================

    [Fact]
    public void DataIpExcelImporter_ImportsRealDataInputXlsx_AndSmartMessyPaste()
    {
        string dbPath = CreateTempDbPath();
        try
        {
            using var studio = new DynamicSqliteStudio(dbPath);
            var compiler = new LegoTokenCompiler(studio);
            var ws = BlueprintSeed2026.SeedWorkspace2026(studio, compiler, 2026);
            var importer = new DataIpExcelImporter(studio);

            // Test Smart Messy Paste first (always runs in any CI environment)
            string messyTsv = "No.\tStatus\tNo. IP\tTanggal IP\tTPS\tImportir\tBM\tPPN\tJENIS BARANG\n" +
                              "1\tSPPB\t000765\t02/01/2026\tMAL0\tPT. ABDI MANDIRI\t $1,500,000.00 \t $- \tFRESH LONGAN\n";
            var pasteReport = importer.ImportSmartMessyPaste(ws.Id, messyTsv);
            Assert.Equal(1, pasteReport.RowsImported);

            // If real DATA_INPUT.xlsx is mounted in workspace, verify streaming import of all 297 real rows!
            const string realXlsx = "/home/antigravity/DATA_INPUT.xlsx";
            if (File.Exists(realXlsx))
            {
                var realReport = importer.ImportExcelSheet(ws.Id, realXlsx, "input", "INPUT");
                Assert.True(realReport.RowsImported >= 290, $"Expected >=290 real rows, got {realReport.RowsImported}");

                var inputTable = studio.GetTableBySlug(ws.Id, "input")!;
                var totalInDb = studio.CountRows(inputTable);
                Assert.Equal(1 + realReport.RowsImported, totalInDb);
            }
        }
        finally
        {
            SafeDeleteFile(dbPath);
        }
    }

    [Fact]
    public void CustomsPdfCompiler_AutoMergesPibLembarLanjutan_AndLhp_IntoMasterDetailDrawer()
    {
        string dbPath = CreateTempDbPath();
        try
        {
            using var studio = new DynamicSqliteStudio(dbPath);
            var compiler = new LegoTokenCompiler(studio);
            var ws = BlueprintSeed2026.SeedWorkspace2026(studio, compiler, 2026);

            var pdfCompiler = new CustomsPdfCompiler();
            string pibText = """
                PEMBERITAHUAN IMPOR BARANG (BC 2.0) - LEMBAR LANJUTAN
                NO. IP: 000765
                TANGGAL IP: 02/01/2026
                IMPORTIR: PT. ABDI MANDIRI
                TPS: MAL0
                CONTAINER: MSKU1234567 (40FT)
                1. HS 0810.90.10 - FRESH LONGAN MERK 777 TIPE GOLD | 378 BASKET | 4,347.00 KG
                2. HS 0810.90.10 - FRESH LONGAN MERK 777 TIPE RED | 696 BASKET | 8,004.00 KG
                """;
            var compiledPib = pdfCompiler.CompileExtractedText(pibText, "pib_000765.pdf", pageCount: 2);
            Assert.Equal(CustomsDocKind.PibBc20, compiledPib.DocKind);
            Assert.Equal("000765", compiledPib.NoIp);
            Assert.Equal(2, compiledPib.GoodsItems.Count);
            Assert.Single(compiledPib.Containers);

            long rowId = pdfCompiler.AutoMergeIntoStudio(studio, ws.Id, compiledPib);

            // Auto-Merge LHP onto the exact same No. IP row
            string lhpText = """
                LAPORAN HASIL PEMERIKSAAN (LHP)
                NO. IP: 000765
                KESIMPULAN LHP: SESUAI PEMBERITAHUAN
                TEMUAN: TIDAK
                """;
            var compiledLhp = pdfCompiler.CompileExtractedText(lhpText, "lhp_000765.pdf");
            long mergedRowId = pdfCompiler.AutoMergeIntoStudio(studio, ws.Id, compiledLhp);
            Assert.Equal(rowId, mergedRowId);

            var inputTable = studio.GetTableBySlug(ws.Id, "input")!;
            var details = studio.GetRowDetails(inputTable.Id, rowId);
            Assert.Equal(2, details.Count(d => d.DetailKind == "goods_item"));
            Assert.Single(details, d => d.DetailKind == "container");

            // SIAP-NEO delta merge
            var siap = new SiapNeoConnector(new ConnectorProfileConfig("siap_neo", ConnectorMode.Otonom, @"\\.\pipe\siap-neo-bridge", true));
            int updated = siap.ApplyDeltas(studio, ws.Id, [
                new SiapNeoInspectionDelta("765", "2026-01-03 08:21:00", "2026-01-03 08:45:00", "2026-01-03 11:30:00", "2026-01-03 12:20:00", "OK", "Budi", "08123456789")
            ]);
            Assert.Equal(1, updated);

            // Humanized Macro trajectory check
            var path = HumanizedMacroEngine.GenerateMinimumJerkBezierPath(100, 100, 600, 400, 350, steps: 20, seed: 42);
            Assert.Equal(21, path.Count);
            Assert.Equal(100.0, path[0].X, 4);
            Assert.Equal(600.0, path[^1].X, 4);
            double jitter = HumanizedMacroEngine.SampleGaussianJitterMs(180, 45, 40, 600, new Random(42));
            Assert.InRange(jitter, 40.0, 600.0);
        }
        finally
        {
            SafeDeleteFile(dbPath);
        }
    }

    [Fact]
    public async Task OneDriveBatchSorter_MatchesNoIp_CreatesBerkasAndFoto_MovesAndUndoesCleanly()
    {
        string dbPath = CreateTempDbPath();
        string tempRoot = Path.Combine(Path.GetTempPath(), $"mp_sorter_{Guid.NewGuid():N}");
        string srcRoot = Path.Combine(tempRoot, "SourcePhotos");
        string odRoot = Path.Combine(tempRoot, "OneDrive_2026");

        try
        {
            using var studio = new DynamicSqliteStudio(dbPath);
            var compiler = new LegoTokenCompiler(studio);
            var ws = BlueprintSeed2026.SeedWorkspace2026(studio, compiler, 2026);
            var inputTable = studio.GetTableBySlug(ws.Id, "input")!;
            var cols = studio.GetColumns(inputTable.Id);

            studio.InsertRow(inputTable, cols, new Dictionary<string, string?>
            {
                ["no"] = "1",
                ["no_ip"] = "814253",
                ["tanggal_ip"] = "2025-12-26",
                ["importir"] = "BERDIKARI",
                ["tps"] = "MAL0",
                ["jenis_barang"] = "FROZEN BONELESS BEEF"
            }, 2026);

            // Prepare source folders: matched "814253" (1 photo + 1 pdf) and unmatched "999999"
            string matchedDir = Path.Combine(srcRoot, "814253");
            string unmatchedDir = Path.Combine(srcRoot, "999999");
            Directory.CreateDirectory(matchedDir);
            Directory.CreateDirectory(unmatchedDir);

            string photoFile = Path.Combine(matchedDir, "IMG_001.jpg");
            string pdfFile = Path.Combine(matchedDir, "PIB_814253.pdf");
            await File.WriteAllBytesAsync(photoFile, new byte[1024]);
            await File.WriteAllBytesAsync(pdfFile, new byte[512]);
            await File.WriteAllBytesAsync(Path.Combine(unmatchedDir, "orphan.jpg"), new byte[128]);

            var sorter = new OneDriveBatchSorter(studio);

            // 1. Dry-Run first
            var dryRes = await sorter.SortAndMoveAsync(ws.Id, srcRoot, odRoot, dryRun: true);
            Assert.Equal(1, dryRes.MatchedFolders);
            Assert.Equal(1, dryRes.UnmatchedFolders);
            Assert.Equal(2, dryRes.FilesMoved);
            Assert.True(File.Exists(photoFile));

            // 2. Real Zero-Copy Move
            var liveRes = await sorter.SortAndMoveAsync(ws.Id, srcRoot, odRoot, dryRun: false);
            Assert.Equal(2, liveRes.FilesMoved);
            Assert.False(File.Exists(photoFile));
            Assert.False(File.Exists(pdfFile));

            string expectedFolder = Path.Combine(odRoot, "1. 814253_26.12.2025_BERDIKARI_MAL0_FROZEN BONELESS BEEF");
            Assert.True(File.Exists(Path.Combine(expectedFolder, "Foto", "IMG_001.jpg")));
            Assert.True(File.Exists(Path.Combine(expectedFolder, "Berkas", "PIB_814253.pdf")));

            // 3. Undo Batch restores original files
            int reverted = sorter.UndoBatch(liveRes.BatchId);
            Assert.Equal(2, reverted);
            Assert.True(File.Exists(photoFile));
            Assert.True(File.Exists(pdfFile));
        }
        finally
        {
            SafeDeleteFile(dbPath);
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    // ==================== MODUL 4: GOOGLE AUTH PKCE, FIREBASE DELTA LOG & ONEDRIVE BACKUP ====================

    [Fact]
    public void CloudSync_Pkce_DeltaCompression_HlcLww_AndOneDriveSqliteBackup_WorkEndToEnd()
    {
        string dbPath = CreateTempDbPath();
        string bakDir = Path.Combine(Path.GetTempPath(), $"mp_bak_{Guid.NewGuid():N}");

        try
        {
            // 1. PKCE S256
            var pkce = GoogleAuthPkceService.GeneratePkcePair();
            Assert.True(pkce.CodeVerifier.Length >= 43);
            Assert.True(pkce.CodeChallenge.Length >= 43);
            string authUrl = GoogleAuthPkceService.BuildGoogleAuthorizationUrl("client-123", "http://127.0.0.1:51234/cb", pkce, "state-xyz");
            Assert.Contains("code_challenge_method=S256", authUrl);

            // 2. GZip+Base64 Delta Batch roundtrip via AOT AppJsonContext
            var hlcPc = new HybridLogicalClock("pc_win");
            var hlcAnd = new HybridLogicalClock("android_pixel");
            string ts1 = hlcPc.NextTimestamp();
            string ts2 = hlcAnd.NextTimestamp();

            var batch = new SyncDeltaBatch(
                "delta_001",
                2026,
                "pc_win",
                DateTime.UtcNow.ToString("O"),
                [
                    new SyncDeltaMutation("UPSERT_ROW", "input", 1, ts1, "pc_win", new Dictionary<string, string?>
                    {
                        ["no_ip"] = "814253",
                        ["temuan"] = "1"
                    })
                ]);

            string compressed = FirebaseRestSyncEngine.CompressDeltaBatch(batch);
            var restored = FirebaseRestSyncEngine.DecompressDeltaBatch(compressed);
            Assert.Equal("delta_001", restored.BatchId);
            Assert.Single(restored.Mutations);
            Assert.Equal("814253", restored.Mutations[0].Cells["no_ip"]);

            // 3. Field-level HLC LWW merge
            using var studio = new DynamicSqliteStudio(dbPath);
            var compiler = new LegoTokenCompiler(studio);
            var ws = BlueprintSeed2026.SeedWorkspace2026(studio, compiler, 2026);
            var syncEngine = new FirebaseRestSyncEngine(studio, compiler);

            var blueprint = syncEngine.ExportBlueprint(ws.Id);
            string bpJson = JsonSerializer.Serialize(blueprint, AppJsonContext.Default.SchemaBlueprintSnapshot);
            Assert.Contains("\"schema_version\":1", bpJson);

            var merged = syncEngine.MergeFieldLevelLww(
                new Dictionary<string, (string?, string)>
                {
                    ["waktu_pkb"] = ("2026-01-03 08:21:00", "9999999999999-0001-pc"),
                    ["temuan"] = ("0", "1000000000000-0001-pc")
                },
                new Dictionary<string, (string?, string)>
                {
                    ["temuan"] = ("1", "2000000000000-0001-android"),
                    ["ket_lain"] = ("Foto lapangan lengkap", ts2)
                });

            Assert.Equal("2026-01-03 08:21:00", merged["waktu_pkb"]);
            Assert.Equal("1", merged["temuan"]);
            Assert.Equal("Foto lapangan lengkap", merged["ket_lain"]);

            // 4. Online Crash-Safe SQLite Backup to OneDrive (.sqlite.gz)
            var bak = OneDriveSqliteBackupService.CreateCompressedBackup(studio, bakDir, 2026, maxSnapshotsToKeep: 14);
            Assert.True(File.Exists(bak.BackupFilePath));
            Assert.True(bak.CompressedBytes > 0);
        }
        finally
        {
            SafeDeleteFile(dbPath);
            if (Directory.Exists(bakDir)) Directory.Delete(bakDir, recursive: true);
        }
    }
}
