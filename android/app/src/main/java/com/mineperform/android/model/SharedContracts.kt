package com.mineperform.android.model

import kotlinx.serialization.ExperimentalSerializationApi
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonClassDiscriminator

/**
 * Shared JSON serializer configured identically to C# Core.Engine's AppJsonContext:
 * - Zero reflection (compile-time Kotlinx Serialization)
 * - Polymorphic "kind" discriminator for LegoTokenNode AST
 * - snake_case property names matching Firebase Firestore & SQLite JSON blobs
 */
@OptIn(ExperimentalSerializationApi::class)
val MinePerformJson: Json = Json {
    ignoreUnknownKeys = true
    encodeDefaults = true
    classDiscriminator = "kind"
}

@OptIn(ExperimentalSerializationApi::class)
@Serializable
@JsonClassDiscriminator("kind")
sealed class LegoTokenNode {

    @Serializable
    @SerialName("literal_number")
    data class LiteralNumber(
        @SerialName("value") val value: Double
    ) : LegoTokenNode()

    @Serializable
    @SerialName("literal_text")
    data class LiteralText(
        @SerialName("value") val value: String
    ) : LegoTokenNode()

    @Serializable
    @SerialName("column_ref")
    data class ColumnRef(
        @SerialName("table_slug") val tableSlug: String,
        @SerialName("column_key") val columnKey: String
    ) : LegoTokenNode()

    @Serializable
    @SerialName("binary_op")
    data class BinaryOp(
        @SerialName("op") val op: String,
        @SerialName("left") val left: LegoTokenNode,
        @SerialName("right") val right: LegoTokenNode
    ) : LegoTokenNode()

    @Serializable
    @SerialName("aggregate")
    data class Aggregate(
        @SerialName("func") val func: String,
        @SerialName("table_slug") val tableSlug: String,
        @SerialName("column_key") val columnKey: String? = null
    ) : LegoTokenNode()

    @Serializable
    @SerialName("time_op")
    data class TimeOp(
        @SerialName("operation") val operation: String,
        @SerialName("start_col") val startCol: ColumnRef,
        @SerialName("end_col") val endCol: ColumnRef
    ) : LegoTokenNode()

    @Serializable
    @SerialName("filter_pin")
    data class FilterPin(
        @SerialName("column") val column: ColumnRef,
        @SerialName("op") val op: String,
        @SerialName("value") val value: String
    ) : LegoTokenNode()

    @Serializable
    @SerialName("sub_token_ref")
    data class SubTokenRef(
        @SerialName("token_name") val tokenName: String
    ) : LegoTokenNode()

    @Serializable
    @SerialName("conversion_table")
    data class ConversionTable(
        @SerialName("name") val name: String,
        @SerialName("input") val input: LegoTokenNode
    ) : LegoTokenNode()
}

@Serializable
data class IkuDefinitionDto(
    @SerialName("id") val id: String,
    @SerialName("workspace_id") val workspaceId: String,
    @SerialName("code") val code: String,
    @SerialName("title") val title: String,
    @SerialName("target_value") val targetValue: Double,
    @SerialName("unit") val unit: String,
    @SerialName("consolidation_mode") val consolidationMode: String,
    @SerialName("polarization_mode") val polarizationMode: String,
    @SerialName("formula_ast") val formulaAst: LegoTokenNode,
    @SerialName("scalar_value") val scalarValue: Double = 0.0,
    @SerialName("achievement_percent") val achievementPercent: Double = 0.0
)

@Serializable
data class HlcTimestamp(
    @SerialName("physical_unix_ms") val physicalUnixMs: Long,
    @SerialName("logical_counter") val logicalCounter: Int,
    @SerialName("device_id") val deviceId: String
) : Comparable<HlcTimestamp> {
    override fun compareTo(other: HlcTimestamp): Int {
        val msCmp = physicalUnixMs.compareTo(other.physicalUnixMs)
        if (msCmp != 0) return msCmp
        val cntCmp = logicalCounter.compareTo(other.logicalCounter)
        if (cntCmp != 0) return cntCmp
        return deviceId.compareTo(other.deviceId)
    }
}

@Serializable
data class SyncDeltaEntry(
    @SerialName("delta_id") val deltaId: String,
    @SerialName("workspace_id") val workspaceId: String,
    @SerialName("table_slug") val tableSlug: String,
    @SerialName("row_uuid") val rowUuid: String,
    @SerialName("operation") val operation: String,
    @SerialName("changed_cells") val changedCells: Map<String, String?>,
    @SerialName("hlc") val hlc: HlcTimestamp
)

@Serializable
data class DeltaBatchDocument(
    @SerialName("batch_id") val batchId: String,
    @SerialName("workspace_id") val workspaceId: String,
    @SerialName("device_id") val deviceId: String,
    @SerialName("created_at_utc") val createdAtUtc: String,
    @SerialName("entry_count") val entryCount: Int,
    @SerialName("gzip_base64_payload") val gzipBase64Payload: String
)

data class VisualTokenChip(
    val id: String,
    val category: String,
    val icon: String,
    val label: String,
    val borderHex: Long,
    val bgHex: Long
)

data class InspectionCardItem(
    val rowId: Long,
    val no: String,
    val status: String,
    val noIp: String,
    val tanggalIp: String,
    val tps: String,
    val importir: String,
    val fclLcl: String,
    val jenisBarang: String,
    val temuan: Boolean,
    val notul: String,
    val kurangBayarFormatted: String,
    val oneDriveFolderName: String,
    val allFields: Map<String, String>
)
