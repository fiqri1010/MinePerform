using System.Text.Json.Serialization;

namespace MinePerform.Core.LegoTokenEngine;

[JsonConverter(typeof(JsonStringEnumConverter<ConsolidationMode>))]
public enum ConsolidationMode
{
    LastKnownValue,
    Average,
    Sum,
    WeightedAverage
}

[JsonConverter(typeof(JsonStringEnumConverter<PolarizationMode>))]
public enum PolarizationMode
{
    Maximize,
    Minimize,
    Stabilize
}

[JsonConverter(typeof(JsonStringEnumConverter<PeriodKind>))]
public enum PeriodKind
{
    Tahunan,
    Semester,
    Triwulan,
    Bulanan,
    Custom
}

/// <summary>Period filter shared across IKU evaluation and Chart & Summary Bento-Grid.</summary>
public sealed record PeriodFilter(
    [property: JsonPropertyName("kind")] PeriodKind Kind,
    [property: JsonPropertyName("year")] int Year,
    [property: JsonPropertyName("index")] int Index = 1,
    [property: JsonPropertyName("custom_start_iso")] string? CustomStartIso = null,
    [property: JsonPropertyName("custom_end_iso")] string? CustomEndIso = null)
{
    public (string StartIso, string EndIso) ResolveDateRange() => Kind switch
    {
        PeriodKind.Tahunan => ($"{Year:D4}-01-01", $"{Year:D4}-12-31"),
        PeriodKind.Semester => Index == 1
            ? ($"{Year:D4}-01-01", $"{Year:D4}-06-30")
            : ($"{Year:D4}-07-01", $"{Year:D4}-12-31"),
        PeriodKind.Triwulan => Index switch
        {
            1 => ($"{Year:D4}-01-01", $"{Year:D4}-03-31"),
            2 => ($"{Year:D4}-04-01", $"{Year:D4}-06-30"),
            3 => ($"{Year:D4}-07-01", $"{Year:D4}-09-30"),
            _ => ($"{Year:D4}-10-01", $"{Year:D4}-12-31")
        },
        PeriodKind.Bulanan => (
            $"{Year:D4}-{Math.Clamp(Index, 1, 12):D2}-01",
            $"{Year:D4}-{Math.Clamp(Index, 1, 12):D2}-{DateTime.DaysInMonth(Year, Math.Clamp(Index, 1, 12)):D2}"
        ),
        PeriodKind.Custom => (
            CustomStartIso ?? $"{Year:D4}-01-01",
            CustomEndIso ?? $"{Year:D4}-12-31"
        ),
        _ => ($"{Year:D4}-01-01", $"{Year:D4}-12-31")
    };
}

/// <summary>
/// Polymorphic AOT-safe Lego-Token AST node.
/// Shared identically between PC (C# System.Text.Json Source Gen) and Android (Kotlin kotlinx.serialization).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LiteralNumberToken), "literal_number")]
[JsonDerivedType(typeof(LiteralTextToken), "literal_text")]
[JsonDerivedType(typeof(LiteralBoolToken), "literal_bool")]
[JsonDerivedType(typeof(ColumnRefToken), "column_ref")]
[JsonDerivedType(typeof(BinaryOpToken), "binary_op")]
[JsonDerivedType(typeof(TimeOpToken), "time_op")]
[JsonDerivedType(typeof(FilterPinToken), "filter_pin")]
[JsonDerivedType(typeof(AggregateToken), "aggregate")]
[JsonDerivedType(typeof(IfElseToken), "if_else")]
[JsonDerivedType(typeof(ConversionTableToken), "conversion_table")]
[JsonDerivedType(typeof(SubTokenRefToken), "sub_token_ref")]
[JsonDerivedType(typeof(GroupParenToken), "group_paren")]
public abstract record LegoTokenNode;

public sealed record LiteralNumberToken(
    [property: JsonPropertyName("value")] double Value) : LegoTokenNode;

public sealed record LiteralTextToken(
    [property: JsonPropertyName("value")] string Value) : LegoTokenNode;

public sealed record LiteralBoolToken(
    [property: JsonPropertyName("value")] bool Value) : LegoTokenNode;

/// <summary>📦 Column reference token (#3B82F6).</summary>
public sealed record ColumnRefToken(
    [property: JsonPropertyName("table_slug")] string TableSlug,
    [property: JsonPropertyName("column_key")] string ColumnKey) : LegoTokenNode;

/// <summary>Binary math or comparison operator (+, -, *, /, =, !=, &lt;, &lt;=, &gt;, &gt;=, AND, OR).</summary>
public sealed record BinaryOpToken(
    [property: JsonPropertyName("op")] string Op,
    [property: JsonPropertyName("left")] LegoTokenNode Left,
    [property: JsonPropertyName("right")] LegoTokenNode Right) : LegoTokenNode;

/// <summary>⏱️ Time duration operation token (#F59E0B): SELISIH_MENIT, SELISIH_JAM, HARI_KERJA.</summary>
public sealed record TimeOpToken(
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("start_expr")] LegoTokenNode StartExpr,
    [property: JsonPropertyName("end_expr")] LegoTokenNode EndExpr) : LegoTokenNode;

/// <summary>🏷️ Filter criteria pin (#10B981), e.g. FCL/LCL = FCL, TEMUAN? = TRUE.</summary>
public sealed record FilterPinToken(
    [property: JsonPropertyName("column")] ColumnRefToken Column,
    [property: JsonPropertyName("op")] string Op,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("values")] List<string>? Values = null) : LegoTokenNode;

/// <summary>∑ Aggregation token (#8B5CF6): COUNT, COUNT_DISTINCT, SUM, AVG, MIN, MAX, PERCENT_WHERE.</summary>
public sealed record AggregateToken(
    [property: JsonPropertyName("func")] string Func,
    [property: JsonPropertyName("table_slug")] string TableSlug,
    [property: JsonPropertyName("target_expr")] LegoTokenNode? TargetExpr = null,
    [property: JsonPropertyName("filters")] List<LegoTokenNode>? Filters = null,
    [property: JsonPropertyName("numerator_condition")] LegoTokenNode? NumeratorCondition = null) : LegoTokenNode;

/// <summary>🔀 Conditional logic token (#EC4899): JIKA(cond) MAKA(then) LAINNYA(else).</summary>
public sealed record IfElseToken(
    [property: JsonPropertyName("condition")] LegoTokenNode Condition,
    [property: JsonPropertyName("then_expr")] LegoTokenNode ThenExpr,
    [property: JsonPropertyName("else_expr")] LegoTokenNode ElseExpr) : LegoTokenNode;

/// <summary>Single tier row in a 📊 TABEL_KONVERSI lookup table.</summary>
public sealed record ConversionTier(
    [property: JsonPropertyName("min_inclusive")] double? MinInclusive,
    [property: JsonPropertyName("max_inclusive")] double? MaxInclusive,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("label")] string? Label = null);

/// <summary>📊 Conversion table token (#EC4899): maps an input expression to a converted score/index.</summary>
public sealed record ConversionTableToken(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("input_expr")] LegoTokenNode InputExpr,
    [property: JsonPropertyName("tiers")] List<ConversionTier> Tiers,
    [property: JsonPropertyName("default_score")] double DefaultScore = 0.0) : LegoTokenNode;

/// <summary>🧩 Reusable saved sub-formula chip (#06B6D4), e.g. #Komponen_Ketepatan_Waktu.</summary>
public sealed record SubTokenRefToken(
    [property: JsonPropertyName("token_name")] string TokenName) : LegoTokenNode;

/// <summary>Parenthesized grouping token for visual readability on the WrapPanel canvas.</summary>
public sealed record GroupParenToken(
    [property: JsonPropertyName("inner")] LegoTokenNode Inner) : LegoTokenNode;

/// <summary>Unlimited Kemenkeu IKU portfolio definition (e.g. 1a-N, 1b-N, 1c-N, 2a-N, 3a-CP, ...).</summary>
public sealed record IkuDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workspace_id")] string WorkspaceId,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("target_value")] double TargetValue,
    [property: JsonPropertyName("unit")] string Unit,
    [property: JsonPropertyName("consolidation_mode")] ConsolidationMode ConsolidationMode,
    [property: JsonPropertyName("polarization")] PolarizationMode Polarization,
    [property: JsonPropertyName("formula_ast")] LegoTokenNode FormulaAst,
    [property: JsonPropertyName("audit_filter_ast")] LegoTokenNode? AuditFilterAst = null,
    [property: JsonPropertyName("primary_table_slug")] string PrimaryTableSlug = "input",
    [property: JsonPropertyName("weight")] double Weight = 1.0,
    [property: JsonPropertyName("sort_order")] int SortOrder = 0);

/// <summary>Saved reusable Lego-Token fragment stored in sys_sub_tokens.</summary>
public sealed record SubTokenDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("workspace_id")] string WorkspaceId,
    [property: JsonPropertyName("token_name")] string TokenName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("ast")] LegoTokenNode Ast);

/// <summary>Result of compiling & executing an IKU Lego-Token AST against SQLite.</summary>
public sealed record IkuEvaluationResult(
    [property: JsonPropertyName("iku_code")] string IkuCode,
    [property: JsonPropertyName("scalar_value")] double ScalarValue,
    [property: JsonPropertyName("target_value")] double TargetValue,
    [property: JsonPropertyName("achievement_percent")] double AchievementPercent,
    [property: JsonPropertyName("unit")] string Unit,
    [property: JsonPropertyName("included_count")] int IncludedCount,
    [property: JsonPropertyName("excluded_count")] int ExcludedCount,
    [property: JsonPropertyName("included_row_ids")] List<long> IncludedRowIds,
    [property: JsonPropertyName("excluded_row_ids")] List<long> ExcludedRowIds,
    [property: JsonPropertyName("generated_sql")] string GeneratedSql);

/// <summary>Category count item for Bento-Grid charts (TPS distribution, Alasan Mulai >1 JAM).</summary>
public sealed record CategoryCountItem(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("count")] int Count);

/// <summary>Monthly series point for LiveCharts2 / Vico trend lines.</summary>
public sealed record MonthlyTrendPoint(
    [property: JsonPropertyName("month")] int Month,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("document_count")] int DocumentCount,
    [property: JsonPropertyName("findings_count")] int FindingsCount,
    [property: JsonPropertyName("kurang_bayar_total")] decimal KurangBayarTotal);

/// <summary>Complete Bento-Grid snapshot powering the CHART & SUMMARY page.</summary>
public sealed record BentoSummarySnapshot(
    [property: JsonPropertyName("nkp_percent")] double NkpPercent,
    [property: JsonPropertyName("total_documents")] int TotalDocuments,
    [property: JsonPropertyName("fcl_count")] int FclCount,
    [property: JsonPropertyName("lcl_count")] int LclCount,
    [property: JsonPropertyName("findings_count")] int FindingsCount,
    [property: JsonPropertyName("notul_count")] int NotulCount,
    [property: JsonPropertyName("total_pdri")] decimal TotalPdri,
    [property: JsonPropertyName("total_kurang_bayar")] decimal TotalKurangBayar,
    [property: JsonPropertyName("premi_category")] string PremiCategory,
    [property: JsonPropertyName("premi_run_rate_percent")] double PremiRunRatePercent,
    [property: JsonPropertyName("iku_results")] List<IkuEvaluationResult> IkuResults,
    [property: JsonPropertyName("tps_distribution")] List<CategoryCountItem> TpsDistribution,
    [property: JsonPropertyName("delay_reasons")] List<CategoryCountItem> DelayReasons,
    [property: JsonPropertyName("monthly_trend")] List<MonthlyTrendPoint> MonthlyTrend);
