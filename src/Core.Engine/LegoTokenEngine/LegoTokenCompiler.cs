using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MinePerform.Core.SchemaStudio;
using MinePerform.Core.Serialization;

namespace MinePerform.Core.LegoTokenEngine;

/// <summary>
/// Compiles arbitrary Lego-Token AST trees into parameterized SQLite SQL queries.
/// Zero hardcoded KPI/IKU formulas — supports unlimited IKUs, recursive sub-tokens with cycle detection,
/// period date scoping, conversion tables, and transparent row-level audit trails.
/// </summary>
public sealed class LegoTokenCompiler
{
    private readonly DynamicSqliteStudio _studio;

    public LegoTokenCompiler(DynamicSqliteStudio studio)
    {
        _studio = studio;
    }

    // ==================== PERSISTENCE (IKU & SUB-TOKENS) ====================

    public void SaveIkuDefinition(IkuDefinition iku)
    {
        string astJson = JsonSerializer.Serialize(iku.FormulaAst, AppJsonContext.Default.LegoTokenNode);
        string? auditJson = iku.AuditFilterAst is null
            ? null
            : JsonSerializer.Serialize(iku.AuditFilterAst, AppJsonContext.Default.LegoTokenNode);

        using var cmd = _studio.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sys_iku_definitions (
                id, workspace_id, code, title, target_value, unit,
                consolidation_mode, polarization, formula_ast_json, audit_filter_ast_json,
                primary_table_slug, weight, sort_order
            )
            VALUES (
                @id, @ws, @code, @title, @target, @unit,
                @cons, @pol, @ast, @audit, @tbl, @weight, @ord
            )
            ON CONFLICT(workspace_id, code) DO UPDATE SET
                title = excluded.title,
                target_value = excluded.target_value,
                unit = excluded.unit,
                consolidation_mode = excluded.consolidation_mode,
                polarization = excluded.polarization,
                formula_ast_json = excluded.formula_ast_json,
                audit_filter_ast_json = excluded.audit_filter_ast_json,
                primary_table_slug = excluded.primary_table_slug,
                weight = excluded.weight,
                sort_order = excluded.sort_order
            """;
        cmd.Parameters.AddWithValue("@id", iku.Id);
        cmd.Parameters.AddWithValue("@ws", iku.WorkspaceId);
        cmd.Parameters.AddWithValue("@code", iku.Code);
        cmd.Parameters.AddWithValue("@title", iku.Title);
        cmd.Parameters.AddWithValue("@target", iku.TargetValue);
        cmd.Parameters.AddWithValue("@unit", iku.Unit);
        cmd.Parameters.AddWithValue("@cons", iku.ConsolidationMode.ToString());
        cmd.Parameters.AddWithValue("@pol", iku.Polarization.ToString());
        cmd.Parameters.AddWithValue("@ast", astJson);
        cmd.Parameters.AddWithValue("@audit", (object?)auditJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tbl", iku.PrimaryTableSlug);
        cmd.Parameters.AddWithValue("@weight", iku.Weight);
        cmd.Parameters.AddWithValue("@ord", iku.SortOrder);
        cmd.ExecuteNonQuery();
    }

    public List<IkuDefinition> ListIkuDefinitions(string workspaceId)
    {
        var list = new List<IkuDefinition>();
        using var cmd = _studio.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, workspace_id, code, title, target_value, unit,
                   consolidation_mode, polarization, formula_ast_json, audit_filter_ast_json,
                   primary_table_slug, weight, sort_order
            FROM sys_iku_definitions
            WHERE workspace_id = @ws
            ORDER BY sort_order, code
            """;
        cmd.Parameters.AddWithValue("@ws", workspaceId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Enum.TryParse(reader.GetString(6), true, out ConsolidationMode cons);
            Enum.TryParse(reader.GetString(7), true, out PolarizationMode pol);
            var formulaAst = JsonSerializer.Deserialize(reader.GetString(8), AppJsonContext.Default.LegoTokenNode)
                ?? new LiteralNumberToken(0);
            var auditAst = reader.IsDBNull(9)
                ? null
                : JsonSerializer.Deserialize(reader.GetString(9), AppJsonContext.Default.LegoTokenNode);

            list.Add(new IkuDefinition(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetDouble(4),
                reader.GetString(5),
                cons,
                pol,
                formulaAst,
                auditAst,
                reader.GetString(10),
                reader.GetDouble(11),
                reader.GetInt32(12)));
        }
        return list;
    }

    public void SaveSubToken(SubTokenDefinition subToken)
    {
        string astJson = JsonSerializer.Serialize(subToken.Ast, AppJsonContext.Default.LegoTokenNode);
        using var cmd = _studio.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sys_sub_tokens (id, workspace_id, token_name, description, ast_json)
            VALUES (@id, @ws, @name, @desc, @ast)
            ON CONFLICT(workspace_id, token_name) DO UPDATE SET
                description = excluded.description,
                ast_json = excluded.ast_json
            """;
        cmd.Parameters.AddWithValue("@id", subToken.Id);
        cmd.Parameters.AddWithValue("@ws", subToken.WorkspaceId);
        cmd.Parameters.AddWithValue("@name", subToken.TokenName);
        cmd.Parameters.AddWithValue("@desc", subToken.Description);
        cmd.Parameters.AddWithValue("@ast", astJson);
        cmd.ExecuteNonQuery();
    }

    public Dictionary<string, SubTokenDefinition> GetSubTokensMap(string workspaceId)
    {
        var map = new Dictionary<string, SubTokenDefinition>(StringComparer.OrdinalIgnoreCase);
        using var cmd = _studio.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, workspace_id, token_name, description, ast_json FROM sys_sub_tokens WHERE workspace_id = @ws";
        cmd.Parameters.AddWithValue("@ws", workspaceId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var ast = JsonSerializer.Deserialize(reader.GetString(4), AppJsonContext.Default.LegoTokenNode)
                ?? new LiteralNumberToken(0);
            var def = new SubTokenDefinition(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), ast);
            map[def.TokenName] = def;
        }
        return map;
    }

    // ==================== COMPILATION & EXECUTION ====================

    public (string Sql, List<(string Name, object Value)> Parameters) CompileToSql(
        string workspaceId,
        LegoTokenNode rootAst,
        PeriodFilter? periodFilter = null)
    {
        var subTokens = GetSubTokensMap(workspaceId);
        var expanded = ExpandSubTokens(rootAst, subTokens, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var tables = _studio.ListTables(workspaceId)
            .ToDictionary(t => t.Slug, StringComparer.OrdinalIgnoreCase);
        var columnsByTable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tables.Values)
        {
            columnsByTable[t.Slug] = _studio.GetColumns(t.Id)
                .Select(c => c.ColumnKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var ctx = new CompileContext(tables, columnsByTable, periodFilter);
        string exprSql = EmitExpr(expanded, ctx);
        return ($"SELECT COALESCE(({exprSql}), 0.0);", ctx.Parameters);
    }

    public IkuEvaluationResult EvaluateIku(IkuDefinition iku, PeriodFilter? periodFilter = null)
    {
        var (sql, parameters) = CompileToSql(iku.WorkspaceId, iku.FormulaAst, periodFilter);

        double scalar = 0.0;
        using (var cmd = _studio.Connection.CreateCommand())
        {
            cmd.CommandText = sql;
            foreach (var (name, val) in parameters)
            {
                cmd.Parameters.AddWithValue(name, val);
            }
            object? res = cmd.ExecuteScalar();
            if (res is not null && res != DBNull.Value)
            {
                scalar = Convert.ToDouble(res, CultureInfo.InvariantCulture);
            }
        }

        // Compute row-level audit trail (✅ Included | ❌ Excluded)
        var (includedIds, excludedIds) = ComputeRowAudit(iku, periodFilter);

        double achievement = iku.TargetValue <= 0
            ? 0.0
            : iku.Polarization switch
            {
                PolarizationMode.Minimize => scalar <= 0 ? 120.0 : Math.Min(120.0, (iku.TargetValue / scalar) * 100.0),
                _ => Math.Min(120.0, (scalar / iku.TargetValue) * 100.0)
            };

        return new IkuEvaluationResult(
            iku.Code,
            Math.Round(scalar, 4),
            iku.TargetValue,
            Math.Round(achievement, 2),
            iku.Unit,
            includedIds.Count,
            excludedIds.Count,
            includedIds,
            excludedIds,
            sql);
    }

    private (List<long> Included, List<long> Excluded) ComputeRowAudit(IkuDefinition iku, PeriodFilter? periodFilter)
    {
        var table = _studio.GetTableBySlug(iku.WorkspaceId, iku.PrimaryTableSlug);
        if (table is null)
        {
            return ([], []);
        }

        var allRows = new List<long>();
        var periodParams = new List<(string Name, object Value)>();
        string periodWhere = BuildPeriodWhereClause(table, periodFilter, periodParams, "ap");

        using (var allCmd = _studio.Connection.CreateCommand())
        {
            allCmd.CommandText = $"SELECT \"_row_id\" FROM {IdentifierGuard.Quote(table.PhysicalTableName)}" +
                                 (string.IsNullOrEmpty(periodWhere) ? "" : $" WHERE {periodWhere}");
            foreach (var (n, v) in periodParams) allCmd.Parameters.AddWithValue(n, v);
            using var r = allCmd.ExecuteReader();
            while (r.Read()) allRows.Add(r.GetInt64(0));
        }

        if (iku.AuditFilterAst is null)
        {
            return (allRows, []);
        }

        var subTokens = GetSubTokensMap(iku.WorkspaceId);
        var expandedFilter = ExpandSubTokens(iku.AuditFilterAst, subTokens, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var tables = _studio.ListTables(iku.WorkspaceId).ToDictionary(t => t.Slug, StringComparer.OrdinalIgnoreCase);
        var colsByTable = tables.Values.ToDictionary(
            t => t.Slug,
            t => _studio.GetColumns(t.Id).Select(c => c.ColumnKey).ToHashSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        var ctx = new CompileContext(tables, colsByTable, periodFilter);
        string filterSql = EmitExpr(expandedFilter, ctx);

        var includedSet = new HashSet<long>();
        using (var incCmd = _studio.Connection.CreateCommand())
        {
            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture, $"SELECT \"_row_id\" FROM {IdentifierGuard.Quote(table.PhysicalTableName)} WHERE ({filterSql})");
            if (!string.IsNullOrEmpty(periodWhere))
            {
                sb.Append(CultureInfo.InvariantCulture, $" AND ({periodWhere})");
            }
            incCmd.CommandText = sb.ToString();
            foreach (var (n, v) in ctx.Parameters) incCmd.Parameters.AddWithValue(n, v);
            foreach (var (n, v) in periodParams) incCmd.Parameters.AddWithValue(n, v);

            using var r = incCmd.ExecuteReader();
            while (r.Read()) includedSet.Add(r.GetInt64(0));
        }

        var included = new List<long>(includedSet.Count);
        var excluded = new List<long>(Math.Max(0, allRows.Count - includedSet.Count));
        foreach (long id in allRows)
        {
            if (includedSet.Contains(id)) included.Add(id);
            else excluded.Add(id);
        }

        return (included, excluded);
    }

    // ==================== SUB-TOKEN EXPANSION (CYCLE DETECTION) ====================

    private static LegoTokenNode ExpandSubTokens(
        LegoTokenNode node,
        IReadOnlyDictionary<string, SubTokenDefinition> subTokens,
        HashSet<string> activeStack)
    {
        return node switch
        {
            SubTokenRefToken refTok => ExpandSingleSubToken(refTok, subTokens, activeStack),
            BinaryOpToken bin => bin with
            {
                Left = ExpandSubTokens(bin.Left, subTokens, activeStack),
                Right = ExpandSubTokens(bin.Right, subTokens, activeStack)
            },
            TimeOpToken t => t with
            {
                StartExpr = ExpandSubTokens(t.StartExpr, subTokens, activeStack),
                EndExpr = ExpandSubTokens(t.EndExpr, subTokens, activeStack)
            },
            AggregateToken agg => agg with
            {
                TargetExpr = agg.TargetExpr is null ? null : ExpandSubTokens(agg.TargetExpr, subTokens, activeStack),
                Filters = agg.Filters?.Select(f => ExpandSubTokens(f, subTokens, activeStack)).ToList(),
                NumeratorCondition = agg.NumeratorCondition is null ? null : ExpandSubTokens(agg.NumeratorCondition, subTokens, activeStack)
            },
            IfElseToken ie => ie with
            {
                Condition = ExpandSubTokens(ie.Condition, subTokens, activeStack),
                ThenExpr = ExpandSubTokens(ie.ThenExpr, subTokens, activeStack),
                ElseExpr = ExpandSubTokens(ie.ElseExpr, subTokens, activeStack)
            },
            ConversionTableToken ct => ct with
            {
                InputExpr = ExpandSubTokens(ct.InputExpr, subTokens, activeStack)
            },
            GroupParenToken gp => gp with
            {
                Inner = ExpandSubTokens(gp.Inner, subTokens, activeStack)
            },
            _ => node
        };
    }

    private static LegoTokenNode ExpandSingleSubToken(
        SubTokenRefToken refTok,
        IReadOnlyDictionary<string, SubTokenDefinition> subTokens,
        HashSet<string> activeStack)
    {
        if (!activeStack.Add(refTok.TokenName))
        {
            throw new InvalidOperationException(
                $"Deteksi referensi melingkar (circular reference) pada Sub-Token '{refTok.TokenName}'.");
        }

        if (!subTokens.TryGetValue(refTok.TokenName, out var def))
        {
            throw new InvalidOperationException(
                $"Sub-Token '{refTok.TokenName}' tidak ditemukan pada workspace aktif.");
        }

        try
        {
            return ExpandSubTokens(def.Ast, subTokens, activeStack);
        }
        finally
        {
            activeStack.Remove(refTok.TokenName);
        }
    }

    // ==================== SQLITE SQL CODEGEN ====================

    private static string EmitExpr(LegoTokenNode node, CompileContext ctx)
    {
        return node switch
        {
            LiteralNumberToken num => num.Value.ToString("R", CultureInfo.InvariantCulture),
            LiteralTextToken txt => ctx.AddParam(txt.Value),
            LiteralBoolToken b => b.Value ? "1" : "0",
            GroupParenToken gp => $"({EmitExpr(gp.Inner, ctx)})",
            ColumnRefToken col => EmitColumnRef(col, ctx),
            BinaryOpToken bin => EmitBinaryOp(bin, ctx),
            TimeOpToken timeOp => EmitTimeOp(timeOp, ctx),
            FilterPinToken pin => EmitFilterPin(pin, ctx),
            AggregateToken agg => EmitAggregate(agg, ctx),
            IfElseToken ie => $"(CASE WHEN {EmitExpr(ie.Condition, ctx)} THEN {EmitExpr(ie.ThenExpr, ctx)} ELSE {EmitExpr(ie.ElseExpr, ctx)} END)",
            ConversionTableToken ct => EmitConversionTable(ct, ctx),
            _ => throw new NotSupportedException($"Unsupported Lego-Token node type: {node.GetType().Name}")
        };
    }

    private static string EmitColumnRef(ColumnRefToken col, CompileContext ctx)
    {
        if (!ctx.Tables.ContainsKey(col.TableSlug))
        {
            throw new InvalidOperationException($"Tabel '{col.TableSlug}' tidak ditemukan di workspace.");
        }
        if (!ctx.ColumnsByTable[col.TableSlug].Contains(col.ColumnKey))
        {
            throw new InvalidOperationException($"Kolom '{col.ColumnKey}' tidak ditemukan pada tabel '{col.TableSlug}'.");
        }
        return IdentifierGuard.Quote(col.ColumnKey);
    }

    private static string EmitBinaryOp(BinaryOpToken bin, CompileContext ctx)
    {
        string op = bin.Op.Trim().ToUpperInvariant();
        string left = EmitExpr(bin.Left, ctx);
        string right = EmitExpr(bin.Right, ctx);

        return op switch
        {
            "+" or "-" or "*" or "=" or "!=" or "<" or "<=" or ">" or ">=" or "AND" or "OR" => $"({left} {op} {right})",
            "/" => $"(CAST({left} AS REAL) / NULLIF(CAST({right} AS REAL), 0.0))",
            _ => throw new InvalidOperationException($"Operator tidak diizinkan: '{bin.Op}'")
        };
    }

    private static string EmitTimeOp(TimeOpToken timeOp, CompileContext ctx)
    {
        string start = EmitExpr(timeOp.StartExpr, ctx);
        string end = EmitExpr(timeOp.EndExpr, ctx);
        string op = timeOp.Operation.Trim().ToUpperInvariant();

        return op switch
        {
            "SELISIH_MENIT" => $"((julianday({end}) - julianday({start})) * 1440.0)",
            "SELISIH_JAM" => $"((julianday({end}) - julianday({start})) * 24.0)",
            "HARI_KERJA" => $"MAX(0.0, julianday({end}) - julianday({start}))",
            _ => throw new InvalidOperationException($"Operasi waktu tidak dikenal: '{timeOp.Operation}'")
        };
    }

    private static string EmitFilterPin(FilterPinToken pin, CompileContext ctx)
    {
        string colSql = EmitColumnRef(pin.Column, ctx);
        string op = pin.Op.Trim().ToUpperInvariant();

        if (op == "IN" && pin.Values is { Count: > 0 })
        {
            var paramNames = pin.Values.Select(v => ctx.AddParam(v));
            return $"({colSql} IN ({string.Join(", ", paramNames)}))";
        }

        if (op == "IS_NOT_NULL")
        {
            return $"({colSql} IS NOT NULL AND TRIM(CAST({colSql} AS TEXT)) != '')";
        }

        if (op == "IS_NULL")
        {
            return $"({colSql} IS NULL OR TRIM(CAST({colSql} AS TEXT)) = '')";
        }

        if (op is "=" or "!=" or "<" or "<=" or ">" or ">=")
        {
            object boundVal = double.TryParse(pin.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double dVal)
                && pin.Column.ColumnKey != "no_ip"
                ? dVal
                : pin.Value;
            string pName = ctx.AddParam(boundVal);
            return $"({colSql} {op} {pName})";
        }

        throw new InvalidOperationException($"Filter operator tidak didukung: '{pin.Op}'");
    }

    private static string EmitAggregate(AggregateToken agg, CompileContext ctx)
    {
        if (!ctx.Tables.TryGetValue(agg.TableSlug, out var table))
        {
            throw new InvalidOperationException($"Tabel '{agg.TableSlug}' tidak ditemukan untuk agregasi '{agg.Func}'.");
        }

        string func = agg.Func.Trim().ToUpperInvariant();
        string phys = IdentifierGuard.Quote(table.PhysicalTableName);

        var whereParts = new List<string>();
        if (agg.Filters is not null)
        {
            foreach (var f in agg.Filters)
            {
                whereParts.Add($"({EmitExpr(f, ctx)})");
            }
        }

        string periodClause = BuildPeriodWhereClause(table, ctx.PeriodFilter, ctx.Parameters, $"pf{ctx.NextSuffix()}");
        if (!string.IsNullOrEmpty(periodClause))
        {
            whereParts.Add($"({periodClause})");
        }

        string whereSql = whereParts.Count > 0 ? $" WHERE {string.Join(" AND ", whereParts)}" : string.Empty;

        if (func == "PERCENT_WHERE")
        {
            if (agg.NumeratorCondition is null)
            {
                throw new InvalidOperationException("Aggregate PERCENT_WHERE memerlukan numerator_condition.");
            }
            string numCond = EmitExpr(agg.NumeratorCondition, ctx);
            return $"(SELECT COALESCE((SUM(CASE WHEN ({numCond}) THEN 1.0 ELSE 0.0 END) * 100.0) / NULLIF(COUNT(*), 0.0), 0.0) FROM {phys}{whereSql})";
        }

        string targetSql = agg.TargetExpr is null ? "*" : EmitExpr(agg.TargetExpr, ctx);
        string selectExpr = func switch
        {
            "COUNT" => $"CAST(COUNT({targetSql}) AS REAL)",
            "COUNT_DISTINCT" => $"CAST(COUNT(DISTINCT {targetSql}) AS REAL)",
            "SUM" => $"COALESCE(SUM(CAST({targetSql} AS REAL)), 0.0)",
            "AVG" => $"COALESCE(AVG(CAST({targetSql} AS REAL)), 0.0)",
            "MIN" => $"COALESCE(MIN(CAST({targetSql} AS REAL)), 0.0)",
            "MAX" => $"COALESCE(MAX(CAST({targetSql} AS REAL)), 0.0)",
            _ => throw new InvalidOperationException($"Fungsi agregasi tidak didukung: '{agg.Func}'")
        };

        return $"(SELECT {selectExpr} FROM {phys}{whereSql})";
    }

    private static string EmitConversionTable(ConversionTableToken ct, CompileContext ctx)
    {
        string inputSql = EmitExpr(ct.InputExpr, ctx);
        var sb = new StringBuilder("(CASE");

        foreach (var tier in ct.Tiers)
        {
            var conds = new List<string>();
            if (tier.MinInclusive.HasValue)
            {
                conds.Add($"(({inputSql}) >= {tier.MinInclusive.Value.ToString("R", CultureInfo.InvariantCulture)})");
            }
            if (tier.MaxInclusive.HasValue)
            {
                conds.Add($"(({inputSql}) <= {tier.MaxInclusive.Value.ToString("R", CultureInfo.InvariantCulture)})");
            }

            if (conds.Count > 0)
            {
                sb.Append(" WHEN ")
                  .Append(string.Join(" AND ", conds))
                  .Append(" THEN ")
                  .Append(tier.Score.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        sb.Append(" ELSE ")
          .Append(ct.DefaultScore.ToString("R", CultureInfo.InvariantCulture))
          .Append(" END)");
        return sb.ToString();
    }

    private static string BuildPeriodWhereClause(
        TableDefinition table,
        PeriodFilter? periodFilter,
        List<(string Name, object Value)> parameters,
        string prefix)
    {
        if (periodFilter is null || string.IsNullOrEmpty(table.DateColumnKey))
        {
            return string.Empty;
        }

        var (startIso, endIso) = periodFilter.ResolveDateRange();
        string pStart = $"@{prefix}_start";
        string pEnd = $"@{prefix}_end";
        parameters.Add((pStart, startIso));
        parameters.Add((pEnd, endIso));

        string col = IdentifierGuard.Quote(table.DateColumnKey);
        return $"substr({col}, 1, 10) >= {pStart} AND substr({col}, 1, 10) <= {pEnd}";
    }

    private sealed class CompileContext(
        Dictionary<string, TableDefinition> tables,
        Dictionary<string, HashSet<string>> columnsByTable,
        PeriodFilter? periodFilter)
    {
        private int _paramSeq;
        private int _suffixSeq;

        public Dictionary<string, TableDefinition> Tables { get; } = tables;
        public Dictionary<string, HashSet<string>> ColumnsByTable { get; } = columnsByTable;
        public PeriodFilter? PeriodFilter { get; } = periodFilter;
        public List<(string Name, object Value)> Parameters { get; } = [];

        public string AddParam(object value)
        {
            string name = $"@lp{_paramSeq++}";
            Parameters.Add((name, value));
            return name;
        }

        public int NextSuffix() => _suffixSeq++;
    }
}
