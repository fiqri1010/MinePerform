package com.mineperform.android.data

import android.content.Context
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper
import android.util.Base64
import com.mineperform.android.model.DeltaBatchDocument
import com.mineperform.android.model.HlcTimestamp
import com.mineperform.android.model.IkuDefinitionDto
import com.mineperform.android.model.InspectionCardItem
import com.mineperform.android.model.LegoTokenNode
import com.mineperform.android.model.MinePerformJson
import com.mineperform.android.model.SyncDeltaEntry
import kotlinx.serialization.builtins.ListSerializer
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.time.Instant
import java.util.UUID
import java.util.zip.GZIPInputStream
import java.util.zip.GZIPOutputStream

/**
 * Offline-First SQLite & Immortal Hybrid Cloud Sync Repository for Android.
 * Shares the exact same `sys_*` metadata schema, GZip+Base64 Delta Log format,
 * and HLC Last-Write-Wins (LWW) conflict resolution with PC Core.Engine.
 */
class LocalSqliteAndSyncRepository(context: Context) :
    SQLiteOpenHelper(context, DB_NAME, null, DB_VERSION) {

    private val deviceId: String = "android-${UUID.randomUUID().toString().take(8)}"

    override fun onConfigure(db: SQLiteDatabase) {
        super.onConfigure(db)
        db.enableWriteAheadLogging()
        db.setForeignKeyConstraintsEnabled(true)
    }

    override fun onCreate(db: SQLiteDatabase) {
        db.execSQL(
            """
            CREATE TABLE IF NOT EXISTS sys_workspaces (
                id TEXT PRIMARY KEY,
                year INTEGER NOT NULL UNIQUE,
                display_name TEXT NOT NULL,
                created_at_utc TEXT NOT NULL
            )
            """.trimIndent()
        )

        db.execSQL(
            """
            CREATE TABLE IF NOT EXISTS sys_iku_definitions (
                id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL,
                code TEXT NOT NULL,
                title TEXT NOT NULL,
                target_value REAL NOT NULL,
                unit TEXT NOT NULL,
                consolidation_mode TEXT NOT NULL,
                polarization_mode TEXT NOT NULL,
                formula_ast_json TEXT NOT NULL,
                sort_order INTEGER NOT NULL DEFAULT 0
            )
            """.trimIndent()
        )

        db.execSQL(
            """
            CREATE TABLE IF NOT EXISTS sys_sync_deltas (
                delta_id TEXT PRIMARY KEY,
                workspace_id TEXT NOT NULL,
                table_slug TEXT NOT NULL,
                row_uuid TEXT NOT NULL,
                operation TEXT NOT NULL,
                changed_cells_json TEXT NOT NULL,
                hlc_ms INTEGER NOT NULL,
                hlc_counter INTEGER NOT NULL,
                hlc_device TEXT NOT NULL,
                synced INTEGER NOT NULL DEFAULT 0
            )
            """.trimIndent()
        )

        db.execSQL(
            """
            CREATE TABLE IF NOT EXISTS tbl_2026_input (
                _rowid_uuid TEXT PRIMARY KEY,
                no TEXT,
                status TEXT,
                no_ip TEXT,
                tanggal_ip TEXT,
                tps TEXT,
                importir TEXT,
                fcl_lcl TEXT,
                jenis_barang TEXT,
                temuan INTEGER DEFAULT 0,
                notul TEXT,
                total_kurang_bayar REAL DEFAULT 0,
                nama_folder_onedrive TEXT
            )
            """.trimIndent()
        )

        seedDefaultWorkspaceIfEmpty(db)
    }

    override fun onUpgrade(db: SQLiteDatabase, oldVersion: Int, newVersion: Int) {
        // Forward-compatible non-destructive migrations
    }

    private fun seedDefaultWorkspaceIfEmpty(db: SQLiteDatabase) {
        val nowIso = Instant.now().toString()
        db.execSQL(
            "INSERT OR IGNORE INTO sys_workspaces (id, year, display_name, created_at_utc) VALUES (?, ?, ?, ?)",
            arrayOf("ws_2026", 2026, "Tahun 2026", nowIso)
        )

        val sampleAst = MinePerformJson.encodeToString(
            LegoTokenNode.serializer(),
            LegoTokenNode.Aggregate("PERCENT_WHERE", "input", "no_ip")
        )
        db.execSQL(
            """
            INSERT OR IGNORE INTO sys_iku_definitions
            (id, workspace_id, code, title, target_value, unit, consolidation_mode, polarization_mode, formula_ast_json, sort_order)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """.trimIndent(),
            arrayOf(
                "iku_default_1",
                "ws_2026",
                "IKU-01",
                "Indikator Kinerja Pemeriksaan (Sinkronisasi AST dari PC)",
                82.0,
                "%",
                "LastKnownValue",
                "Maximize",
                sampleAst,
                1
            )
        )
    }

    fun loadInspectionCards(searchQuery: String = ""): List<InspectionCardItem> {
        val db = readableDatabase
        val results = mutableListOf<InspectionCardItem>()
        val like = "%${searchQuery.trim()}%"
        val cursor = if (searchQuery.isBlank()) {
            db.rawQuery("SELECT rowid, * FROM tbl_2026_input ORDER BY rowid DESC LIMIT 200", null)
        } else {
            db.rawQuery(
                """
                SELECT rowid, * FROM tbl_2026_input
                WHERE no_ip LIKE ? OR importir LIKE ? OR tps LIKE ? OR jenis_barang LIKE ?
                ORDER BY rowid DESC LIMIT 200
                """.trimIndent(),
                arrayOf(like, like, like, like)
            )
        }

        cursor.use { c ->
            while (c.moveToNext()) {
                val noIp = c.getString(c.getColumnIndexOrThrow("no_ip")).orEmpty()
                val importir = c.getString(c.getColumnIndexOrThrow("importir")).orEmpty()
                val tps = c.getString(c.getColumnIndexOrThrow("tps")).orEmpty()
                val fclLcl = c.getString(c.getColumnIndexOrThrow("fcl_lcl")).orEmpty()
                val jenis = c.getString(c.getColumnIndexOrThrow("jenis_barang")).orEmpty()
                val status = c.getString(c.getColumnIndexOrThrow("status")).orEmpty()
                val tanggalIp = c.getString(c.getColumnIndexOrThrow("tanggal_ip")).orEmpty()
                val no = c.getString(c.getColumnIndexOrThrow("no")).orEmpty()
                val temuan = c.getInt(c.getColumnIndexOrThrow("temuan")) == 1
                val notul = c.getString(c.getColumnIndexOrThrow("notul")).orEmpty()
                val kb = c.getDouble(c.getColumnIndexOrThrow("total_kurang_bayar"))
                val folder = c.getString(c.getColumnIndexOrThrow("nama_folder_onedrive")).orEmpty()

                results.add(
                    InspectionCardItem(
                        rowId = c.getLong(0),
                        no = no,
                        status = status,
                        noIp = noIp,
                        tanggalIp = tanggalIp,
                        tps = tps,
                        importir = importir,
                        fclLcl = fclLcl,
                        jenisBarang = jenis,
                        temuan = temuan,
                        notul = notul,
                        kurangBayarFormatted = "Rp %,.0f".format(kb),
                        oneDriveFolderName = folder,
                        allFields = linkedMapOf(
                            "No." to no,
                            "Status" to status,
                            "No. IP" to noIp,
                            "Tanggal IP" to tanggalIp,
                            "TPS" to tps,
                            "Importir" to importir,
                            "FCL/LCL" to fclLcl,
                            "Jenis Barang" to jenis,
                            "Temuan" to if (temuan) "YA" else "TIDAK",
                            "Notul" to notul,
                            "Nama Folder OneDrive" to folder
                        )
                    )
                )
            }
        }
        return results
    }

    fun loadIkuDefinitions(workspaceId: String = "ws_2026"): List<IkuDefinitionDto> {
        val db = readableDatabase
        val list = mutableListOf<IkuDefinitionDto>()
        val cursor = db.rawQuery(
            "SELECT * FROM sys_iku_definitions WHERE workspace_id = ? ORDER BY sort_order ASC",
            arrayOf(workspaceId)
        )
        cursor.use { c ->
            while (c.moveToNext()) {
                val astJson = c.getString(c.getColumnIndexOrThrow("formula_ast_json"))
                val ast = runCatching {
                    MinePerformJson.decodeFromString(LegoTokenNode.serializer(), astJson)
                }.getOrDefault(LegoTokenNode.Aggregate("COUNT", "input"))

                list.add(
                    IkuDefinitionDto(
                        id = c.getString(c.getColumnIndexOrThrow("id")),
                        workspaceId = workspaceId,
                        code = c.getString(c.getColumnIndexOrThrow("code")),
                        title = c.getString(c.getColumnIndexOrThrow("title")),
                        targetValue = c.getDouble(c.getColumnIndexOrThrow("target_value")),
                        unit = c.getString(c.getColumnIndexOrThrow("unit")),
                        consolidationMode = c.getString(c.getColumnIndexOrThrow("consolidation_mode")),
                        polarizationMode = c.getString(c.getColumnIndexOrThrow("polarization_mode")),
                        formulaAst = ast
                    )
                )
            }
        }
        return list
    }

    fun compressDeltaBatch(workspaceId: String, entries: List<SyncDeltaEntry>): DeltaBatchDocument {
        val json = MinePerformJson.encodeToString(ListSerializer(SyncDeltaEntry.serializer()), entries)
        val out = ByteArrayOutputStream()
        GZIPOutputStream(out).use { gz -> gz.write(json.toByteArray(Charsets.UTF_8)) }
        val encoded = Base64.encodeToString(out.toByteArray(), Base64.NO_WRAP)
        return DeltaBatchDocument(
            batchId = "batch_${UUID.randomUUID().toString().take(12)}",
            workspaceId = workspaceId,
            deviceId = deviceId,
            createdAtUtc = Instant.now().toString(),
            entryCount = entries.size,
            gzipBase64Payload = encoded
        )
    }

    fun decompressDeltaBatch(doc: DeltaBatchDocument): List<SyncDeltaEntry> {
        val compressed = Base64.decode(doc.gzipBase64Payload, Base64.DEFAULT)
        val json = GZIPInputStream(ByteArrayInputStream(compressed)).bufferedReader(Charsets.UTF_8).readText()
        return MinePerformJson.decodeFromString(ListSerializer(SyncDeltaEntry.serializer()), json)
    }

    /**
     * Hybrid Logical Clock (HLC) Last-Write-Wins conflict resolution — 100% deterministic with PC.
     */
    fun resolveLww(local: SyncDeltaEntry, remote: SyncDeltaEntry): SyncDeltaEntry =
        if (remote.hlc > local.hlc) remote else local

    /**
     * Offline On-Device ML Kit OCR post-processor: extracts 6-digit No. IP and ISO Container numbers.
     */
    fun extractCustomsIdsFromOcrText(rawOcrText: String): Pair<String?, List<String>> {
        val noIpMatch = Regex("""\b(\d{6})\b""").find(rawOcrText)?.groupValues?.get(1)
        val containers = Regex("""\b([A-Z]{4}\d{7})\b""")
            .findAll(rawOcrText.uppercase())
            .map { it.groupValues[1] }
            .distinct()
            .toList()
        return Pair(noIpMatch, containers)
    }

    fun currentHlc(logicalCounter: Int = 0): HlcTimestamp =
        HlcTimestamp(System.currentTimeMillis(), logicalCounter, deviceId)

    companion object {
        private const val DB_NAME = "mineperform_android.sqlite"
        private const val DB_VERSION = 1
    }
}
