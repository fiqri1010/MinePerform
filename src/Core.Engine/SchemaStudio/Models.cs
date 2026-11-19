using System.Text.Json.Serialization;

namespace MinePerform.Core.SchemaStudio;

/// <summary>Supported logical column data types for dynamic No-Code SQLite tables.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ColumnDataType>))]
public enum ColumnDataType
{
    Text,
    Integer,
    Decimal,
    Currency,
    Date,
    DateTime,
    Boolean,
    Computed
}

/// <summary>Severity level for Data Hygiene Radar anomalies.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnomalySeverity>))]
public enum AnomalySeverity
{
    Info,
    Warning,
    Error
}

/// <summary>Yearly workspace container (e.g., 2026, 2027).</summary>
public sealed record WorkspaceModel(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("year")] int Year,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("created_at_utc")] string CreatedAtUtc,
    [property: JsonPropertyName("is_active")] bool IsActive);

/// <summary>Logical table metadata backed by a real indexed SQLite table (usr_y{year}_{slug}).</summary>
public sealed record TableDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workspace_id")] string WorkspaceId,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("physical_table_name")] string PhysicalTableName,
    [property: JsonPropertyName("sort_order")] int SortOrder,
    [property: JsonPropertyName("icon")] string Icon,
    [property: JsonPropertyName("is_primary")] bool IsPrimary,
    [property: JsonPropertyName("date_column_key")] string? DateColumnKey = null);

/// <summary>Semantic Super-Header band (e.g. INFO DASAR, DATA PEMERIKSAAN, PUTUSAN PFPD, LINK DATA).</summary>
public sealed record ColumnGroupDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("table_id")] string TableId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("color_hex")] string ColorHex,
    [property: JsonPropertyName("chip_bg_hex")] string ChipBgHex,
    [property: JsonPropertyName("sort_order")] int SortOrder,
    [property: JsonPropertyName("is_collapsed_default")] bool IsCollapsedDefault = false);

/// <summary>Dynamic column metadata including frozen state, currency formatting, and computed formula.</summary>
public sealed record ColumnDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("table_id")] string TableId,
    [property: JsonPropertyName("group_id")] string? GroupId,
    [property: JsonPropertyName("column_key")] string ColumnKey,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("data_type")] ColumnDataType DataType,
    [property: JsonPropertyName("is_frozen")] bool IsFrozen,
    [property: JsonPropertyName("is_computed")] bool IsComputed,
    [property: JsonPropertyName("computed_expr")] string? ComputedExpr,
    [property: JsonPropertyName("currency_code")] string? CurrencyCode,
    [property: JsonPropertyName("format_mask")] string? FormatMask,
    [property: JsonPropertyName("sort_order")] int SortOrder,
    [property: JsonPropertyName("width_px")] int WidthPx = 140);

/// <summary>Visual Table Linker relationship between two tables in the same workspace.</summary>
public sealed record TableRelationDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workspace_id")] string WorkspaceId,
    [property: JsonPropertyName("source_table_id")] string SourceTableId,
    [property: JsonPropertyName("source_column_key")] string SourceColumnKey,
    [property: JsonPropertyName("target_table_id")] string TargetTableId,
    [property: JsonPropertyName("target_column_key")] string TargetColumnKey,
    [property: JsonPropertyName("relation_type")] string RelationType,
    [property: JsonPropertyName("display_label")] string DisplayLabel);

/// <summary>Master-Detail Row Drawer payload (e.g., PIB Lembar Lanjutan goods_item, container, document).</summary>
public sealed record RowDetailRecord(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("table_id")] string TableId,
    [property: JsonPropertyName("parent_row_id")] long ParentRowId,
    [property: JsonPropertyName("detail_kind")] string DetailKind,
    [property: JsonPropertyName("payload_json")] string PayloadJson,
    [property: JsonPropertyName("sort_order")] int SortOrder);

/// <summary>Data Hygiene Radar anomaly record attached to a specific cell.</summary>
public sealed record DataAnomalyRecord(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("table_id")] string TableId,
    [property: JsonPropertyName("row_id")] long RowId,
    [property: JsonPropertyName("column_key")] string ColumnKey,
    [property: JsonPropertyName("severity")] AnomalySeverity Severity,
    [property: JsonPropertyName("rule_code")] string RuleCode,
    [property: JsonPropertyName("raw_value")] string RawValue,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("resolved")] bool Resolved = false);

/// <summary>A single row in any dynamic user table (keyed by column_key).</summary>
public sealed record DynamicRow(
    [property: JsonPropertyName("row_id")] long RowId,
    [property: JsonPropertyName("cells")] Dictionary<string, string?> Cells,
    [property: JsonPropertyName("updated_at_utc")] string UpdatedAtUtc);

/// <summary>Selective checkbox options for cloning a workspace blueprint across years (e.g. 2026 -> 2027).</summary>
public sealed record YearCloneOptions(
    [property: JsonPropertyName("clone_tables_and_columns")] bool CloneTablesAndColumns = true,
    [property: JsonPropertyName("clone_relations")] bool CloneRelations = true,
    [property: JsonPropertyName("clone_iku_and_lego_tokens")] bool CloneIkuAndLegoTokens = true,
    [property: JsonPropertyName("clone_connector_configs")] bool CloneConnectorConfigs = true,
    [property: JsonPropertyName("clone_onedrive_pattern")] bool CloneOneDrivePattern = true,
    [property: JsonPropertyName("clone_master_lookup_rows")] bool CloneMasterLookupRows = false);

/// <summary>Result summary of a selective year-to-year blueprint clone.</summary>
public sealed record YearCloneResult(
    [property: JsonPropertyName("target_workspace_id")] string TargetWorkspaceId,
    [property: JsonPropertyName("target_year")] int TargetYear,
    [property: JsonPropertyName("tables_cloned")] int TablesCloned,
    [property: JsonPropertyName("columns_cloned")] int ColumnsCloned,
    [property: JsonPropertyName("relations_cloned")] int RelationsCloned,
    [property: JsonPropertyName("ikus_cloned")] int IkusCloned,
    [property: JsonPropertyName("sub_tokens_cloned")] int SubTokensCloned,
    [property: JsonPropertyName("connector_configs_cloned")] int ConnectorConfigsCloned,
    [property: JsonPropertyName("master_rows_cloned")] int MasterRowsCloned);

/// <summary>Summary returned by Excel/CSV/Messy-Paste import pipelines.</summary>
public sealed record ImportReport(
    [property: JsonPropertyName("table_slug")] string TableSlug,
    [property: JsonPropertyName("rows_imported")] int RowsImported,
    [property: JsonPropertyName("rows_skipped")] int RowsSkipped,
    [property: JsonPropertyName("anomalies_detected")] int AnomaliesDetected,
    [property: JsonPropertyName("duration_ms")] long DurationMs,
    [property: JsonPropertyName("sample_anomalies")] List<DataAnomalyRecord> SampleAnomalies);
