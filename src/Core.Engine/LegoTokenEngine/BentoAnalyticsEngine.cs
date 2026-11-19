using System.Globalization;
using MinePerform.Core.SchemaStudio;

namespace MinePerform.Core.LegoTokenEngine;

/// <summary>
/// Computes the CHART & SUMMARY Bento-Grid analytics snapshot across any period
/// (Tahunan, Semester, Triwulan, Bulanan, Custom) using parameterized SQLite queries
/// and the unlimited Lego-Token IKU portfolio.
/// </summary>
public sealed class BentoAnalyticsEngine
{
    private static readonly string[] MonthLabels =
        ["Jan", "Feb", "Mar", "Apr", "Mei", "Jun", "Jul", "Agu", "Sep", "Okt", "Nov", "Des"];

    private readonly DynamicSqliteStudio _studio;
    private readonly LegoTokenCompiler _compiler;

    public BentoAnalyticsEngine(DynamicSqliteStudio studio, LegoTokenCompiler compiler)
    {
        _studio = studio;
        _compiler = compiler;
    }

    public BentoSummarySnapshot BuildSnapshot(string workspaceId, PeriodFilter periodFilter, string primaryTableSlug = "input")
    {
        var ikus = _compiler.ListIkuDefinitions(workspaceId);
        var ikuResults = new List<IkuEvaluationResult>(ikus.Count);
        double weightedSum = 0.0;
        double totalWeight = 0.0;

        foreach (var iku in ikus)
        {
            var eval = _compiler.EvaluateIku(iku, periodFilter);
            ikuResults.Add(eval);
            weightedSum += eval.AchievementPercent * iku.Weight;
            totalWeight += iku.Weight;
        }

        double nkp = totalWeight > 0 ? Math.Round(weightedSum / totalWeight, 2) : 0.0;

        var table = _studio.GetTableBySlug(workspaceId, primaryTableSlug);
        if (table is null)
        {
            return new BentoSummarySnapshot(nkp, 0, 0, 0, 0, 0, 0m, 0m, ResolvePremiCategory(nkp), Math.Min(100.0, nkp), ikuResults, [], [], []);
        }

        var cols = _studio.GetColumns(table.Id).Select(c => c.ColumnKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var (startIso, endIso) = periodFilter.ResolveDateRange();
        string phys = IdentifierGuard.Quote(table.PhysicalTableName);
        string dateFilter = !string.IsNullOrEmpty(table.DateColumnKey) && cols.Contains(table.DateColumnKey)
            ? $" WHERE substr({IdentifierGuard.Quote(table.DateColumnKey)}, 1, 10) >= @start AND substr({IdentifierGuard.Quote(table.DateColumnKey)}, 1, 10) <= @end"
            : string.Empty;

        int totalDocs = 0, fcl = 0, lcl = 0, findings = 0, notul = 0;
        decimal totalPdri = 0m, totalKb = 0m;

        using (var cmd = _studio.Connection.CreateCommand())
        {
            string fclExpr = cols.Contains("fcl_lcl") ? "SUM(CASE WHEN UPPER(TRIM(\"fcl_lcl\")) = 'FCL' THEN 1 ELSE 0 END)" : "0";
            string lclExpr = cols.Contains("fcl_lcl") ? "SUM(CASE WHEN UPPER(TRIM(\"fcl_lcl\")) = 'LCL' THEN 1 ELSE 0 END)" : "0";
            string findExpr = cols.Contains("temuan") ? "SUM(CASE WHEN \"temuan\" = 1 OR UPPER(CAST(\"temuan\" AS TEXT)) IN ('TRUE','YA','1') THEN 1 ELSE 0 END)" : "0";
            string notulExpr = cols.Contains("notul") ? "SUM(CASE WHEN UPPER(TRIM(CAST(\"notul\" AS TEXT))) IN ('YA','TRUE','1','NOTUL') THEN 1 ELSE 0 END)" : "0";
            string pdriExpr = cols.Contains("total_pdri") ? "COALESCE(SUM(CAST(\"total_pdri\" AS REAL)), 0.0)" : "0.0";
            string kbExpr = cols.Contains("total_kurang_bayar") ? "COALESCE(SUM(CAST(\"total_kurang_bayar\" AS REAL)), 0.0)" : "0.0";

            cmd.CommandText = $"""
                SELECT COUNT(*), COALESCE({fclExpr}, 0), COALESCE({lclExpr}, 0),
                       COALESCE({findExpr}, 0), COALESCE({notulExpr}, 0),
                       {pdriExpr}, {kbExpr}
                FROM {phys}{dateFilter}
                """;
            if (!string.IsNullOrEmpty(dateFilter))
            {
                cmd.Parameters.AddWithValue("@start", startIso);
                cmd.Parameters.AddWithValue("@end", endIso);
            }

            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                totalDocs = r.GetInt32(0);
                fcl = r.GetInt32(1);
                lcl = r.GetInt32(2);
                findings = r.GetInt32(3);
                notul = r.GetInt32(4);
                totalPdri = Convert.ToDecimal(r.GetDouble(5), CultureInfo.InvariantCulture);
                totalKb = Convert.ToDecimal(r.GetDouble(6), CultureInfo.InvariantCulture);
            }
        }

        var tpsDist = cols.Contains("tps")
            ? QueryTopCategories(phys, "tps", dateFilter, startIso, endIso, 10)
            : [];

        var delayReasons = cols.Contains("alasan_mulai_1_jam")
            ? QueryTopCategories(phys, "alasan_mulai_1_jam", dateFilter, startIso, endIso, 8)
            : [];

        var monthlyTrend = (!string.IsNullOrEmpty(table.DateColumnKey) && cols.Contains(table.DateColumnKey))
            ? QueryMonthlyTrend(phys, table.DateColumnKey, cols, periodFilter.Year)
            : [];

        string premiCat = ResolvePremiCategory(nkp);
        double premiRunRate = Math.Round(Math.Clamp(nkp, 0.0, 100.0), 1);

        return new BentoSummarySnapshot(
            nkp,
            totalDocs,
            fcl,
            lcl,
            findings,
            notul,
            Math.Round(totalPdri, 2),
            Math.Round(totalKb, 2),
            premiCat,
            premiRunRate,
            ikuResults,
            tpsDist,
            delayReasons,
            monthlyTrend);
    }

    private List<CategoryCountItem> QueryTopCategories(
        string phys,
        string colKey,
        string dateFilter,
        string startIso,
        string endIso,
        int limit)
    {
        var list = new List<CategoryCountItem>();
        string qCol = IdentifierGuard.Quote(colKey);
        string nonEmptyClause = $"{qCol} IS NOT NULL AND TRIM({qCol}) != '' AND TRIM({qCol}) != '-'";
        string where = string.IsNullOrEmpty(dateFilter)
            ? $" WHERE {nonEmptyClause}"
            : $"{dateFilter} AND {nonEmptyClause}";

        using var cmd = _studio.Connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT TRIM({qCol}) AS grp, COUNT(*) AS cnt
            FROM {phys}{where}
            GROUP BY TRIM({qCol})
            ORDER BY cnt DESC
            LIMIT @lim
            """;
        if (!string.IsNullOrEmpty(dateFilter))
        {
            cmd.Parameters.AddWithValue("@start", startIso);
            cmd.Parameters.AddWithValue("@end", endIso);
        }
        cmd.Parameters.AddWithValue("@lim", limit);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new CategoryCountItem(r.GetString(0), r.GetInt32(1)));
        }
        return list;
    }

    private List<MonthlyTrendPoint> QueryMonthlyTrend(
        string phys,
        string dateColKey,
        HashSet<string> cols,
        int year)
    {
        string qDate = IdentifierGuard.Quote(dateColKey);
        string findExpr = cols.Contains("temuan") ? "SUM(CASE WHEN \"temuan\" = 1 OR UPPER(CAST(\"temuan\" AS TEXT)) IN ('TRUE','YA','1') THEN 1 ELSE 0 END)" : "0";
        string kbExpr = cols.Contains("total_kurang_bayar") ? "COALESCE(SUM(CAST(\"total_kurang_bayar\" AS REAL)), 0.0)" : "0.0";

        var byMonth = new Dictionary<int, (int Docs, int Findings, decimal Kb)>();
        using var cmd = _studio.Connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT CAST(substr({qDate}, 6, 2) AS INTEGER) AS m,
                   COUNT(*),
                   COALESCE({findExpr}, 0),
                   {kbExpr}
            FROM {phys}
            WHERE substr({qDate}, 1, 4) = @yr
            GROUP BY m
            ORDER BY m
            """;
        cmd.Parameters.AddWithValue("@yr", year.ToString("D4", CultureInfo.InvariantCulture));

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (r.IsDBNull(0)) continue;
            int m = r.GetInt32(0);
            if (m is >= 1 and <= 12)
            {
                byMonth[m] = (r.GetInt32(1), r.GetInt32(2), Convert.ToDecimal(r.GetDouble(3), CultureInfo.InvariantCulture));
            }
        }

        var points = new List<MonthlyTrendPoint>(12);
        for (int m = 1; m <= 12; m++)
        {
            byMonth.TryGetValue(m, out var val);
            points.Add(new MonthlyTrendPoint(m, MonthLabels[m - 1], val.Docs, val.Findings, Math.Round(val.Kb, 2)));
        }
        return points;
    }

    private static string ResolvePremiCategory(double nkpPercent) => nkpPercent switch
    {
        >= 100.0 => "OPTIMAL",
        >= 85.0 => "BAIK",
        >= 70.0 => "CUKUP",
        _ => "PERLU PERHATIAN"
    };
}
