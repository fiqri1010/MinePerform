using System.Globalization;
using MinePerform.Core.LegoTokenEngine;

namespace MinePerform.Core.SchemaStudio;

/// <summary>
/// Data-driven 2026 Seed Blueprint calibrated to real operational files:
///  - DATA_INPUT.xlsx (56 columns across 4 Super-Headers, 3 Frozen Columns, Computed Columns)
///  - Companion tables from Data IP 2026.xlsx (Jam Presensi, Calc Pack, Data Master, Catatan)
///  - Manual_IKU_2026 (1a-N, 1b-N, 1c-N) expressed 100% as dynamic Lego-Token ASTs (ZERO hardcoded formulas).
/// </summary>
public static class BlueprintSeed2026
{
    public static WorkspaceModel SeedWorkspace2026(DynamicSqliteStudio studio, LegoTokenCompiler compiler, int year = 2026)
    {
        var ws = studio.CreateWorkspace(year, $"Workspace {year}", setActive: true);

        // 1. Primary Table: INPUT (56 columns, 4 Super-Headers, 3 Frozen Columns)
        var inputTable = CreateInputTable(studio, ws.Id);

        // 2. Companion Tables from Data IP 2026.xlsx
        var presensiTable = CreateJamPresensiTable(studio, ws.Id);
        var calcPackTable = CreateCalcPackTable(studio, ws.Id);
        var masterTable = CreateDataMasterTable(studio, ws.Id);
        _ = CreateCatatanTable(studio, ws.Id);

        // 3. Visual Table Linker relations
        studio.AddTableRelation(ws.Id, inputTable.Id, "tps", masterTable.Id, "kode_tps", "ManyToOne", "INPUT.TPS -> Data Master.Kode TPS");
        studio.AddTableRelation(ws.Id, inputTable.Id, "tanggal_ip", presensiTable.Id, "tanggal", "ManyToOne", "INPUT.Tanggal IP -> Jam Presensi.Tanggal");
        studio.AddTableRelation(ws.Id, inputTable.Id, "tk_periksa", calcPackTable.Id, "tk_periksa", "ManyToOne", "INPUT.TK PERIKSA -> Calc Pack.TK PERIKSA");

        // 4. Calibrated 2026 IKU Portfolio (100% Lego-Token AST + Reusable Sub-Tokens)
        SeedDefaultIkuPortfolio(compiler, ws.Id);

        return ws;
    }

    private static TableDefinition CreateInputTable(DynamicSqliteStudio studio, string workspaceId)
    {
        var gInfo = new ColumnGroupDefinition("g_info", "", "INFO DASAR", "#3B82F6", "#1E3A8A40", 1);
        var gPeriksa = new ColumnGroupDefinition("g_periksa", "", "DATA PEMERIKSAAN", "#F59E0B", "#78350F40", 2);
        var gPfpd = new ColumnGroupDefinition("g_pfpd", "", "PUTUSAN PFPD", "#8B5CF6", "#4C1D9540", 3);
        var gLink = new ColumnGroupDefinition("g_link", "", "LINK DATA", "#10B981", "#064E3B40", 4);

        var cols = new List<ColumnDefinition>
        {
            // ===== GROUP 1: INFO DASAR (30 Columns; first 3 Frozen) =====
            Col(gInfo.Id, "no", "No.", ColumnDataType.Integer, 1, frozen: true, width: 64),
            Col(gInfo.Id, "status", "Status", ColumnDataType.Text, 2, frozen: true, width: 88),
            Col(gInfo.Id, "no_ip", "No. IP", ColumnDataType.Text, 3, frozen: true, width: 120),
            Col(gInfo.Id, "tanggal_ip", "Tanggal IP", ColumnDataType.Date, 4, width: 112),
            Col(gInfo.Id, "tps", "TPS", ColumnDataType.Text, 5, width: 88),
            Col(gInfo.Id, "jenis_impor", "Jenis Impor", ColumnDataType.Text, 6, width: 160),
            Col(gInfo.Id, "importir", "Importir", ColumnDataType.Text, 7, width: 220),
            Col(gInfo.Id, "pemilik_barang", "Pemilik Barang", ColumnDataType.Text, 8, width: 200),
            Col(gInfo.Id, "ppjk", "PPJK", ColumnDataType.Text, 9, width: 180),
            Col(gInfo.Id, "fcl_lcl", "FCL/LCL", ColumnDataType.Text, 10, width: 80),
            Col(gInfo.Id, "jml_cont_40f", "JML CONT 40F≥", ColumnDataType.Integer, 11, width: 96),
            Col(gInfo.Id, "jml_cont_20f", "JML CONT 20F", ColumnDataType.Integer, 12, width: 96),
            Col(gInfo.Id, "cont_40f_periksa", "CONT 40F≥ PERIKSA", ColumnDataType.Integer, 13, width: 108),
            Col(gInfo.Id, "cont_20f_periksa", "CONT 20F PERIKSA", ColumnDataType.Integer, 14, width: 108),
            Col(gInfo.Id, "jml_pk", "JML PK", ColumnDataType.Decimal, 15, width: 96),
            Col(gInfo.Id, "jml_pk_periksa", "JML PK PERIKSA", ColumnDataType.Decimal, 16, computed: true, expr: "TK_PERIKSA_RATIO(jml_pk, tk_periksa)", width: 108),
            Col(gInfo.Id, "jumlah_jenis", "JUMLAH JENIS", ColumnDataType.Integer, 17, width: 96),
            Col(gInfo.Id, "jml_jenis_periksa", "JML JENIS PERIKSA", ColumnDataType.Integer, 18, width: 108),
            Col(gInfo.Id, "jenis_barang", "JENIS BARANG", ColumnDataType.Text, 19, width: 240),
            Col(gInfo.Id, "campuran", "CAMPURAN?", ColumnDataType.Text, 20, width: 96),
            Col(gInfo.Id, "hs_code_1_jenis", "HS CODE 1 JENIS", ColumnDataType.Text, 21, width: 120),
            Col(gInfo.Id, "nilai_pabean", "NILAI PABEAN", ColumnDataType.Currency, 22, currency: "Rp", width: 152),
            Col(gInfo.Id, "bm", "BM", ColumnDataType.Currency, 23, currency: "Rp", width: 136),
            Col(gInfo.Id, "bmt", "BMT", ColumnDataType.Currency, 24, currency: "Rp", width: 120),
            Col(gInfo.Id, "bm_kite", "BM KITE", ColumnDataType.Currency, 25, currency: "Rp", width: 120),
            Col(gInfo.Id, "cukai", "CUKAI", ColumnDataType.Currency, 26, currency: "Rp", width: 120),
            Col(gInfo.Id, "ppn", "PPN", ColumnDataType.Currency, 27, currency: "Rp", width: 136),
            Col(gInfo.Id, "ppnbm", "PPnBM", ColumnDataType.Currency, 28, currency: "Rp", width: 120),
            Col(gInfo.Id, "pph", "PPh", ColumnDataType.Currency, 29, currency: "Rp", width: 136),
            Col(gInfo.Id, "total_pdri", "TOTAL PDRI", ColumnDataType.Currency, 30, computed: true, expr: "SUM(bm, bmt, bm_kite, cukai, ppn, ppnbm, pph)", currency: "Rp", width: 152),

            // ===== GROUP 2: DATA PEMERIKSAAN (14 Columns) =====
            Col(gPeriksa.Id, "tk_periksa", "TK PERIKSA", ColumnDataType.Text, 31, width: 104),
            Col(gPeriksa.Id, "waktu_pkb", "Waktu PKB", ColumnDataType.DateTime, 32, width: 152),
            Col(gPeriksa.Id, "waktu_mulai_periksa", "Waktu Mulai Periksa", ColumnDataType.DateTime, 33, width: 152),
            Col(gPeriksa.Id, "selesai_periksa", "Selesai Periksa", ColumnDataType.DateTime, 34, width: 152),
            Col(gPeriksa.Id, "selesai_lhp", "Selesai LHP", ColumnDataType.DateTime, 35, width: 152),
            Col(gPeriksa.Id, "alasan_mulai_1_jam", "Alasan Mulai >1 JAM", ColumnDataType.Text, 36, width: 180),
            Col(gPeriksa.Id, "kendala_pemeriksaan", "Kendala Pemeriksaan", ColumnDataType.Text, 37, width: 200),
            Col(gPeriksa.Id, "konfirmasi", "Konfirmasi?", ColumnDataType.Text, 38, width: 104),
            Col(gPeriksa.Id, "barcon", "BARCON", ColumnDataType.Text, 39, width: 104),
            Col(gPeriksa.Id, "kesimpulan_lhp", "Kesimpulan LHP", ColumnDataType.Text, 40, width: 180),
            Col(gPeriksa.Id, "ket_lain", "KET LAIN", ColumnDataType.Text, 41, width: 180),
            Col(gPeriksa.Id, "temuan", "TEMUAN?", ColumnDataType.Boolean, 42, width: 96),
            Col(gPeriksa.Id, "pengurus", "Pengurus", ColumnDataType.Text, 43, width: 160),
            Col(gPeriksa.Id, "no_hp", "No. HP", ColumnDataType.Text, 44, width: 128),

            // ===== GROUP 3: PUTUSAN PFPD (11 Columns) =====
            Col(gPfpd.Id, "spbl", "SPBL", ColumnDataType.Text, 45, width: 112),
            Col(gPfpd.Id, "notul", "NOTUL?", ColumnDataType.Text, 46, width: 96),
            Col(gPfpd.Id, "bm_kb", "BM-", ColumnDataType.Currency, 47, currency: "Rp", width: 136),
            Col(gPfpd.Id, "denda_adm_kb", "DENDA ADM-", ColumnDataType.Currency, 48, currency: "Rp", width: 136),
            Col(gPfpd.Id, "bmt_kb", "BMT-", ColumnDataType.Currency, 49, currency: "Rp", width: 120),
            Col(gPfpd.Id, "bm_kite_kb", "BM KITE-", ColumnDataType.Currency, 50, currency: "Rp", width: 120),
            Col(gPfpd.Id, "cukai_kb", "CUKAI-", ColumnDataType.Currency, 51, currency: "Rp", width: 120),
            Col(gPfpd.Id, "ppn_kb", "PPN-", ColumnDataType.Currency, 52, currency: "Rp", width: 136),
            Col(gPfpd.Id, "ppnbm_kb", "PPnBM-", ColumnDataType.Currency, 53, currency: "Rp", width: 120),
            Col(gPfpd.Id, "pph_kb", "PPh-", ColumnDataType.Currency, 54, currency: "Rp", width: 136),
            Col(gPfpd.Id, "total_kurang_bayar", "Total Kurang Bayar", ColumnDataType.Currency, 55, computed: true, expr: "SUM(bm_kb, denda_adm_kb, bmt_kb, bm_kite_kb, cukai_kb, ppn_kb, ppnbm_kb, pph_kb)", currency: "Rp", width: 160),

            // ===== GROUP 4: LINK DATA (1 Computed Pattern Column) =====
            Col(gLink.Id, "nama_folder_onedrive", "Nama Folder Onedrive", ColumnDataType.Computed, 56, computed: true, expr: "TEMPLATE({no}. {no_ip:000000}_{tanggal_ip:dd.MM.yyyy}_{importir}_{tps}_{jenis_barang})", width: 320)
        };

        return studio.CreateTable(
            workspaceId,
            "INPUT",
            [gInfo, gPeriksa, gPfpd, gLink],
            cols,
            slugOverride: "input",
            icon: "file-spreadsheet",
            isPrimary: true,
            dateColumnKey: "tanggal_ip",
            sortOrder: 1);
    }

    private static TableDefinition CreateJamPresensiTable(DynamicSqliteStudio studio, string workspaceId)
    {
        var g = new ColumnGroupDefinition("g_pres", "", "ABSENSI CEISA", "#06B6D4", "#164E6340", 1);
        var cols = new List<ColumnDefinition>
        {
            Col(g.Id, "tanggal", "Tanggal", ColumnDataType.Date, 1, frozen: true, width: 112),
            Col(g.Id, "shift", "Shift", ColumnDataType.Text, 2, width: 88),
            Col(g.Id, "cluster", "Cluster", ColumnDataType.Text, 3, width: 88),
            Col(g.Id, "jam_absen_ceisa_impor", "Jam Absen CEISA Impor", ColumnDataType.Text, 4, width: 140),
            Col(g.Id, "jam_absen_ceisa_40", "Jam Absen CEISA 4.0", ColumnDataType.Text, 5, width: 140),
            Col(g.Id, "status_patuh_impor", "Status Patuh Impor", ColumnDataType.Text, 6, width: 136),
            Col(g.Id, "status_patuh_40", "Status Patuh 4.0", ColumnDataType.Text, 7, width: 136),
            Col(g.Id, "indeks_absen_impor", "Indeks Absen Impor", ColumnDataType.Decimal, 8, width: 120),
            Col(g.Id, "indeks_absen_40", "Indeks Absen 4.0", ColumnDataType.Decimal, 9, width: 120),
            Col(g.Id, "indeks_produktivitas", "Indeks Produktivitas", ColumnDataType.Decimal, 10, width: 128),
            Col(g.Id, "keterangan", "Keterangan", ColumnDataType.Text, 11, width: 200)
        };
        return studio.CreateTable(workspaceId, "Jam Presensi", [g], cols, slugOverride: "jam_presensi", icon: "clock", dateColumnKey: "tanggal", sortOrder: 2);
    }

    private static TableDefinition CreateCalcPackTable(DynamicSqliteStudio studio, string workspaceId)
    {
        var g = new ColumnGroupDefinition("g_calc", "", "PARAMETER KALKULASI", "#8B5CF6", "#4C1D9540", 1);
        var cols = new List<ColumnDefinition>
        {
            Col(g.Id, "tk_periksa", "TK PERIKSA", ColumnDataType.Text, 1, frozen: true, width: 120),
            Col(g.Id, "rasio_sampling", "Rasio Sampling", ColumnDataType.Decimal, 2, width: 120),
            Col(g.Id, "sla_1_jenis_menit", "SLA 1 Jenis (Menit)", ColumnDataType.Integer, 3, width: 140),
            Col(g.Id, "sla_2_5_jenis_menit", "SLA 2-5 Jenis (Menit)", ColumnDataType.Integer, 4, width: 140),
            Col(g.Id, "sla_gt_5_jenis_menit", "SLA >5 Jenis (Menit)", ColumnDataType.Integer, 5, width: 140),
            Col(g.Id, "bonus_per_kontainer_menit", "Bonus/Kontainer (Menit)", ColumnDataType.Integer, 6, width: 152)
        };
        return studio.CreateTable(workspaceId, "Calc Pack", [g], cols, slugOverride: "calc_pack", icon: "calculator", sortOrder: 3);
    }

    private static TableDefinition CreateDataMasterTable(DynamicSqliteStudio studio, string workspaceId)
    {
        var g = new ColumnGroupDefinition("g_mst", "", "REFERENSI MASTER", "#3B82F6", "#1E3A8A40", 1);
        var cols = new List<ColumnDefinition>
        {
            Col(g.Id, "kategori", "Kategori", ColumnDataType.Text, 1, frozen: true, width: 120),
            Col(g.Id, "kode_tps", "Kode / Kunci", ColumnDataType.Text, 2, width: 120),
            Col(g.Id, "nama_label", "Nama / Label", ColumnDataType.Text, 3, width: 220),
            Col(g.Id, "aktif", "Aktif?", ColumnDataType.Boolean, 4, width: 80)
        };
        return studio.CreateTable(workspaceId, "Data Master", [g], cols, slugOverride: "data_master", icon: "database", sortOrder: 4);
    }

    private static TableDefinition CreateCatatanTable(DynamicSqliteStudio studio, string workspaceId)
    {
        var g = new ColumnGroupDefinition("g_cat", "", "CATATAN KERJA", "#10B981", "#064E3B40", 1);
        var cols = new List<ColumnDefinition>
        {
            Col(g.Id, "tanggal", "Tanggal", ColumnDataType.Date, 1, frozen: true, width: 112),
            Col(g.Id, "judul", "Judul", ColumnDataType.Text, 2, width: 200),
            Col(g.Id, "isi_catatan", "Isi Catatan", ColumnDataType.Text, 3, width: 320),
            Col(g.Id, "tag", "Tag", ColumnDataType.Text, 4, width: 120)
        };
        return studio.CreateTable(workspaceId, "Catatan", [g], cols, slugOverride: "catatan", icon: "notebook", dateColumnKey: "tanggal", sortOrder: 5);
    }

    private static void SeedDefaultIkuPortfolio(LegoTokenCompiler compiler, string workspaceId)
    {
        // Reusable Sub-Token 1: #Batas_SLA_Kontainer (180/240/300 min + 15 min per extra container)
        var totalContPeriksa = new BinaryOpToken("+",
            new ColumnRefToken("input", "cont_40f_periksa"),
            new ColumnRefToken("input", "cont_20f_periksa"));

        var extraContBonus = new IfElseToken(
            new BinaryOpToken(">", totalContPeriksa, new LiteralNumberToken(1)),
            new BinaryOpToken("*", new BinaryOpToken("-", totalContPeriksa, new LiteralNumberToken(1)), new LiteralNumberToken(15)),
            new LiteralNumberToken(0));

        var baseSla = new IfElseToken(
            new BinaryOpToken("<=", new ColumnRefToken("input", "jumlah_jenis"), new LiteralNumberToken(1)),
            new LiteralNumberToken(180),
            new IfElseToken(
                new BinaryOpToken("<=", new ColumnRefToken("input", "jumlah_jenis"), new LiteralNumberToken(5)),
                new LiteralNumberToken(240),
                new LiteralNumberToken(300)));

        var slaAst = new BinaryOpToken("+", baseSla, extraContBonus);
        compiler.SaveSubToken(new SubTokenDefinition(
            $"sub_{Guid.NewGuid().ToString("N")[..10]}",
            workspaceId,
            "#Batas_SLA_Kontainer",
            "Standar waktu pemeriksaan (180/240/300 menit) + 15 menit per peti kemas tambahan.",
            slaAst));

        // Reusable Sub-Token 2: #Komponen_Ketepatan_Waktu (% dokumen FCL sesuai SLA waktu periksa)
        var durasiPeriksa = new TimeOpToken(
            "SELISIH_MENIT",
            new ColumnRefToken("input", "waktu_mulai_periksa"),
            new ColumnRefToken("input", "selesai_periksa"));

        var komponenKetepatanAst = new AggregateToken(
            "PERCENT_WHERE",
            "input",
            Filters:
            [
                new FilterPinToken(new ColumnRefToken("input", "waktu_mulai_periksa"), "IS_NOT_NULL", ""),
                new FilterPinToken(new ColumnRefToken("input", "selesai_periksa"), "IS_NOT_NULL", "")
            ],
            NumeratorCondition: new BinaryOpToken("<=", durasiPeriksa, new SubTokenRefToken("#Batas_SLA_Kontainer")));

        compiler.SaveSubToken(new SubTokenDefinition(
            $"sub_{Guid.NewGuid().ToString("N")[..10]}",
            workspaceId,
            "#Komponen_Ketepatan_Waktu",
            "Persentase dokumen yang selesai periksa dalam batas SLA.",
            komponenKetepatanAst));

        // Reusable Sub-Token 3: #Rasio_Temuan_LHP (% dokumen dengan TEMUAN? = TRUE)
        var rasioTemuanAst = new AggregateToken(
            "PERCENT_WHERE",
            "input",
            NumeratorCondition: new FilterPinToken(new ColumnRefToken("input", "temuan"), "=", "1"));

        compiler.SaveSubToken(new SubTokenDefinition(
            $"sub_{Guid.NewGuid().ToString("N")[..10]}",
            workspaceId,
            "#Rasio_Temuan_LHP",
            "Persentase pemeriksaan fisik yang menghasilkan temuan LHP.",
            rasioTemuanAst));

        // ===== IKU 1a-N: Efektivitas Pemeriksaan Fisik (Target 82%) =====
        // Formula Akhir = (#Komponen_Ketepatan_Waktu * 0.75) + (Konversi_Temuan(#Rasio_Temuan_LHP) * 0.25)
        var konversiTemuan = new ConversionTableToken(
            "Konversi_Temuan_1a",
            new SubTokenRefToken("#Rasio_Temuan_LHP"),
            [
                new ConversionTier(null, 0.0, 0.0, "0%"),
                new ConversionTier(0.0001, 1.50, 20.0, "0.1% - 1.50%"),
                new ConversionTier(1.5001, 3.00, 40.0, "1.51% - 3.00%"),
                new ConversionTier(3.0001, 4.50, 60.0, "3.01% - 4.50%"),
                new ConversionTier(4.5001, 6.00, 80.0, "4.51% - 6.00%"),
                new ConversionTier(6.0001, null, 100.0, "> 6.00%")
            ],
            DefaultScore: 0.0);

        var iku1aFormula = new BinaryOpToken("+",
            new BinaryOpToken("*", new SubTokenRefToken("#Komponen_Ketepatan_Waktu"), new LiteralNumberToken(0.75)),
            new BinaryOpToken("*", konversiTemuan, new LiteralNumberToken(0.25)));

        compiler.SaveIkuDefinition(new IkuDefinition(
            $"iku_{Guid.NewGuid().ToString("N")[..10]}",
            workspaceId,
            "1a-N",
            "Efektivitas Pemeriksaan Fisik Barang Impor",
            82.0,
            "%",
            ConsolidationMode.LastKnownValue,
            PolarizationMode.Maximize,
            iku1aFormula,
            AuditFilterAst: new FilterPinToken(new ColumnRefToken("input", "status"), "IS_NOT_NULL", ""),
            PrimaryTableSlug: "input",
            Weight: 1.0,
            SortOrder: 1));

        // ===== IKU 1b-N: Indeks Efektivitas Waktu Pemeriksaan Fisik (Target 4.15) =====
        // Inclusion filter from Manual IKU 2026: FCL & JUMLAH JENIS = 1 (excludes LCL & FCL > 1 jenis)
        var fcl1JenisFilter = new BinaryOpToken("AND",
            new FilterPinToken(new ColumnRefToken("input", "fcl_lcl"), "=", "FCL"),
            new FilterPinToken(new ColumnRefToken("input", "jumlah_jenis"), "=", "1"));

        var pctSesuaiStandar = new AggregateToken(
            "PERCENT_WHERE",
            "input",
            Filters:
            [
                fcl1JenisFilter,
                new FilterPinToken(new ColumnRefToken("input", "waktu_mulai_periksa"), "IS_NOT_NULL", ""),
                new FilterPinToken(new ColumnRefToken("input", "selesai_periksa"), "IS_NOT_NULL", "")
            ],
            NumeratorCondition: new BinaryOpToken("<=", durasiPeriksa, new SubTokenRefToken("#Batas_SLA_Kontainer")));

        var indeksStandarWaktu = new ConversionTableToken(
            "Indeks_Standar_Waktu_1b",
            pctSesuaiStandar,
            [
                new ConversionTier(80.0, null, 5.0, "Sangat Tepat Waktu (80-100%)"),
                new ConversionTier(60.0, 79.9999, 4.0, "Tepat Waktu (60-79.99%)"),
                new ConversionTier(40.0, 59.9999, 3.0, "Kurang Tepat Waktu (40-59.99%)"),
                new ConversionTier(20.0, 39.9999, 2.0, "Tidak Tepat Waktu (20-39.99%)"),
                new ConversionTier(null, 19.9999, 1.0, "Sangat Tidak Tepat Waktu (<20%)")
            ],
            DefaultScore: 1.0);

        var avgMulaiMenit = new AggregateToken(
            "AVG",
            "input",
            TargetExpr: new TimeOpToken("SELISIH_MENIT", new ColumnRefToken("input", "waktu_pkb"), new ColumnRefToken("input", "waktu_mulai_periksa")),
            Filters:
            [
                fcl1JenisFilter,
                new FilterPinToken(new ColumnRefToken("input", "waktu_pkb"), "IS_NOT_NULL", ""),
                new FilterPinToken(new ColumnRefToken("input", "waktu_mulai_periksa"), "IS_NOT_NULL", "")
            ]);

        var indeksMulaiPeriksa = new ConversionTableToken(
            "Indeks_Mulai_Periksa_1b",
            avgMulaiMenit,
            [
                new ConversionTier(null, 60.0, 5.0, "<= 1 Jam"),
                new ConversionTier(60.0001, 180.0, 4.0, "1 - 3 Jam"),
                new ConversionTier(180.0001, 240.0, 3.0, ">3 - 4 Jam"),
                new ConversionTier(240.0001, 300.0, 2.0, ">4 - 5 Jam"),
                new ConversionTier(300.0001, null, 1.0, "> 5 Jam")
            ],
            DefaultScore: 5.0);

        var iku1bFormula = new BinaryOpToken("+",
            new BinaryOpToken("*", indeksStandarWaktu, new LiteralNumberToken(0.70)),
            new BinaryOpToken("*", indeksMulaiPeriksa, new LiteralNumberToken(0.30)));

        compiler.SaveIkuDefinition(new IkuDefinition(
            $"iku_{Guid.NewGuid().ToString("N")[..10]}",
            workspaceId,
            "1b-N",
            "Indeks Efektivitas Waktu Pemeriksaan Fisik",
            4.15,
            "Indeks",
            ConsolidationMode.Average,
            PolarizationMode.Maximize,
            iku1bFormula,
            AuditFilterAst: fcl1JenisFilter,
            PrimaryTableSlug: "input",
            Weight: 1.0,
            SortOrder: 2));

        // ===== IKU 1c-N: Persentase Efektifitas Ketepatan Waktu Absen CEISA & Produktivitas Pemeriksa (Target 82%) =====
        // Formula: (((AVG(indeks_absen_impor) + AVG(indeks_absen_40)) / 2) / 4 * 70) + ((AVG(indeks_produktivitas) / 4) * 30)
        var avgAbsenImpor = new AggregateToken("AVG", "jam_presensi", new ColumnRefToken("jam_presensi", "indeks_absen_impor"));
        var avgAbsen40 = new AggregateToken("AVG", "jam_presensi", new ColumnRefToken("jam_presensi", "indeks_absen_40"));
        var avgProd = new AggregateToken("AVG", "jam_presensi", new ColumnRefToken("jam_presensi", "indeks_produktivitas"));

        var komponenAbsen = new BinaryOpToken("*",
            new BinaryOpToken("/",
                new BinaryOpToken("/", new BinaryOpToken("+", avgAbsenImpor, avgAbsen40), new LiteralNumberToken(2.0)),
                new LiteralNumberToken(4.0)),
            new LiteralNumberToken(70.0));

        var komponenProd = new BinaryOpToken("*",
            new BinaryOpToken("/", avgProd, new LiteralNumberToken(4.0)),
            new LiteralNumberToken(30.0));

        var iku1cFormula = new BinaryOpToken("+", komponenAbsen, komponenProd);

        compiler.SaveIkuDefinition(new IkuDefinition(
            $"iku_{Guid.NewGuid().ToString("N")[..10]}",
            workspaceId,
            "1c-N",
            "Persentase Efektifitas Ketepatan Waktu Absen CEISA dan Produktivitas Pemeriksa",
            82.0,
            "%",
            ConsolidationMode.Average,
            PolarizationMode.Maximize,
            iku1cFormula,
            AuditFilterAst: new FilterPinToken(new ColumnRefToken("jam_presensi", "tanggal"), "IS_NOT_NULL", ""),
            PrimaryTableSlug: "jam_presensi",
            Weight: 1.0,
            SortOrder: 3));
    }

    private static ColumnDefinition Col(
        string groupId,
        string key,
        string display,
        ColumnDataType type,
        int order,
        bool frozen = false,
        bool computed = false,
        string? expr = null,
        string? currency = null,
        int width = 140) =>
        new("", "", groupId, key, display, type, frozen, computed, expr, currency, null, order, width);

    public static void SeedSampleInspectionRows(DynamicSqliteStudio studio, TableDefinition inputTable, int year = 2026)
    {
        var cols = studio.GetColumns(inputTable.Id);
        var samples = new (string no, string ip, string tgl, string tps, string imp, string fcl, string jmlJenis, string brg, string tk, string pkb, string mulai, string slsP, string slsL, string temuan, string notul, string bm, string ppn, string kb)[]
        {
            ("1", "814217", $"{year}-01-05", "NPCT1", "PT. INDOFOOD SUKSES MAKMUR TBK", "FCL", "1", "WHEAT FLOUR ADDITIVE", "10%", $"{year}-01-05 08:30", $"{year}-01-05 09:15", $"{year}-01-05 11:20", $"{year}-01-05 12:00", "0", "TIDAK", "45000000", "18500000", "0"),
            ("2", "814225", $"{year}-01-08", "KOJA", "PT. SAMSUNG ELECTRONICS INDONESIA", "FCL", "1", "LCD PANEL SPARE PARTS", "30%", $"{year}-01-08 09:00", $"{year}-01-08 09:45", $"{year}-01-08 12:10", $"{year}-01-08 13:00", "0", "TIDAK", "85000000", "35000000", "0"),
            ("3", "814239", $"{year}-01-12", "NCT1", "PT. TOYOTA MOTOR MANUFACTURING", "FCL", "3", "AUTOMOTIVE CKD STAMPING PARTS", "30%", $"{year}-01-12 10:15", $"{year}-01-12 11:00", $"{year}-01-12 14:30", $"{year}-01-12 15:15", "1", "YA", "120000000", "48000000", "14500000"),
            ("4", "814251", $"{year}-01-18", "JICT", "PT. UNILEVER INDONESIA TBK", "LCL", "1", "SURFACTANT RAW CHEMICAL", "10%", $"{year}-01-18 08:45", $"{year}-01-18 09:20", $"{year}-01-18 10:50", $"{year}-01-18 11:30", "0", "TIDAK", "32000000", "14000000", "0"),
            ("5", "814266", $"{year}-01-22", "GRAHA", "PT. LG ELECTRONICS INDONESIA", "FCL", "1", "COMPRESSOR REFRIGERANT UNIT", "30%", $"{year}-01-22 13:00", $"{year}-01-22 13:40", $"{year}-01-22 15:50", $"{year}-01-22 16:30", "0", "TIDAK", "62000000", "27000000", "0"),
            ("6", "814278", $"{year}-01-28", "TPSL", "PT. MAYORA INDAH TBK", "FCL", "2", "CHOCOLATE PASTE & PACKAGING", "10%", $"{year}-01-28 09:30", $"{year}-01-28 10:10", $"{year}-01-28 12:00", $"{year}-01-28 12:45", "0", "TIDAK", "28000000", "12000000", "0"),
            ("7", "814290", $"{year}-02-03", "NPCT1", "PT. ASTRA DAIHATSU MOTOR", "FCL", "1", "AUTOMOTIVE FASTENERS & BOLTS", "10%", $"{year}-02-03 08:15", $"{year}-02-03 09:00", $"{year}-02-03 10:45", $"{year}-02-03 11:30", "0", "TIDAK", "38000000", "16000000", "0"),
            ("8", "814304", $"{year}-02-07", "KOJA", "PT. KALBE FARMA TBK", "FCL", "1", "PHARMACEUTICAL RAW EXCIPIENT", "30%", $"{year}-02-07 10:00", $"{year}-02-07 10:40", $"{year}-02-07 13:00", $"{year}-02-07 13:45", "0", "TIDAK", "74000000", "31000000", "0")
        };

        foreach (var s in samples)
        {
            studio.InsertRow(inputTable, cols, new Dictionary<string, string?>
            {
                ["no"] = s.no,
                ["status"] = "SPPB",
                ["no_ip"] = s.ip,
                ["tanggal_ip"] = s.tgl,
                ["tps"] = s.tps,
                ["importir"] = s.imp,
                ["fcl_lcl"] = s.fcl,
                ["jumlah_jenis"] = s.jmlJenis,
                ["jml_jenis_periksa"] = s.jmlJenis,
                ["jenis_barang"] = s.brg,
                ["tk_periksa"] = s.tk,
                ["waktu_pkb"] = s.pkb,
                ["waktu_mulai_periksa"] = s.mulai,
                ["selesai_periksa"] = s.slsP,
                ["selesai_lhp"] = s.slsL,
                ["temuan"] = s.temuan,
                ["notul"] = s.notul,
                ["bm"] = s.bm,
                ["ppn"] = s.ppn,
                ["total_kurang_bayar"] = s.kb,
                ["nama_folder_onedrive"] = $"{s.no}. {s.ip}_{DateTime.Parse(s.tgl, CultureInfo.InvariantCulture).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}_{s.imp}_{s.tps}_{s.brg}"
            }, year);
        }
    }

    public static void SeedSamplePresensiRows(DynamicSqliteStudio studio, TableDefinition presensiTable, int year = 2026)
    {
        var cols = studio.GetColumns(presensiTable.Id);
        var dates = new[] { $"{year}-01-05", $"{year}-01-08", $"{year}-01-12", $"{year}-01-18", $"{year}-01-22" };
        foreach (var d in dates)
        {
            studio.InsertRow(presensiTable, cols, new Dictionary<string, string?>
            {
                ["tanggal"] = d,
                ["shift"] = "PAGI",
                ["cluster"] = "A",
                ["jam_absen_ceisa_impor"] = "07:25",
                ["jam_absen_ceisa_40"] = "07:28",
                ["status_patuh_impor"] = "PATUH",
                ["status_patuh_40"] = "PATUH",
                ["indeks_absen_impor"] = "4.0",
                ["indeks_absen_40"] = "4.0",
                ["indeks_produktivitas"] = "4.0",
                ["keterangan"] = "Tepat Waktu"
            }, year);
        }
    }
}
