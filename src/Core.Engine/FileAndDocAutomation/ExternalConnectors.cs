using System.Text.Json.Serialization;

namespace MinePerform.Core.FileAndDocAutomation;

[JsonConverter(typeof(JsonStringEnumConverter<ConnectorMode>))]
public enum ConnectorMode
{
    Manual,
    SemiAuto,
    Otonom
}

/// <summary>Telemetry record pushed by SIAP-NEO / SIAP-DESKTOP for a specific No. IP.</summary>
public sealed record SiapNeoInspectionDelta(
    [property: JsonPropertyName("no_ip")] string NoIp,
    [property: JsonPropertyName("waktu_pkb")] string? WaktuPkb,
    [property: JsonPropertyName("waktu_mulai_periksa")] string? WaktuMulaiPeriksa,
    [property: JsonPropertyName("selesai_periksa")] string? SelesaiPeriksa,
    [property: JsonPropertyName("selesai_lhp")] string? SelesaiLhp,
    [property: JsonPropertyName("barcon")] string? Barcon,
    [property: JsonPropertyName("pengurus")] string? Pengurus,
    [property: JsonPropertyName("no_hp")] string? NoHp);

/// <summary>Configuration for SIAP-NEO / SIAP-DESKTOP and BOT-MONITOR-PERIKSA connectors.</summary>
public sealed record ConnectorProfileConfig(
    [property: JsonPropertyName("connector_kind")] string ConnectorKind,
    [property: JsonPropertyName("mode")] ConnectorMode Mode,
    [property: JsonPropertyName("endpoint_or_path")] string EndpointOrPath,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("human_delay_mean_ms")] double HumanDelayMeanMs = 180.0,
    [property: JsonPropertyName("human_delay_stddev_ms")] double HumanDelayStdDevMs = 45.0);

/// <summary>Unified async external connector abstraction.</summary>
public interface IExternalConnector
{
    string Name { get; }
    ConnectorMode Mode { get; }
    Task<bool> ProbeHealthAsync(CancellationToken ct = default);
}

/// <summary>2D trajectory waypoint emitted by HumanizedMacroEngine.</summary>
public readonly record struct MacroWaypoint(double X, double Y, double ElapsedMs);

/// <summary>
/// Humanized OS-Level Macro trajectory & timing generator:
///  - Minimum-Jerk 5th-order polynomial: s(t) = 10t^3 - 15t^4 + 6t^5
///  - Cubic Bezier spatial curve with subtle lateral control offset
///  - Box-Muller Gaussian jitter for realistic inter-action delays.
/// </summary>
public static class HumanizedMacroEngine
{
    /// <summary>Evaluates the 5th-order Minimum-Jerk easing curve s(tau) in [0, 1].</summary>
    public static double MinimumJerk(double tau)
    {
        double t = Math.Clamp(tau, 0.0, 1.0);
        double t3 = t * t * t;
        double t4 = t3 * t;
        double t5 = t4 * t;
        return (10.0 * t3) - (15.0 * t4) + (6.0 * t5);
    }

    /// <summary>Generates a smooth Minimum-Jerk Cubic Bezier pointer path from (x0, y0) to (x1, y1).</summary>
    public static List<MacroWaypoint> GenerateMinimumJerkBezierPath(
        double x0,
        double y0,
        double x1,
        double y1,
        double durationMs,
        int steps = 24,
        int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : Random.Shared;
        double dx = x1 - x0;
        double dy = y1 - y0;
        double dist = double.Hypot(dx, dy);
        double lateral = Math.Min(60.0, dist * 0.15) * (rng.NextDouble() * 2.0 - 1.0);

        // Perpendicular unit vector for subtle organic arc
        double nx = dist > 1e-6 ? -dy / dist : 0.0;
        double ny = dist > 1e-6 ? dx / dist : 0.0;

        double cx1 = x0 + dx * 0.30 + nx * lateral;
        double cy1 = y0 + dy * 0.30 + ny * lateral;
        double cx2 = x0 + dx * 0.72 + nx * (lateral * 0.5);
        double cy2 = y0 + dy * 0.72 + ny * (lateral * 0.5);

        int n = Math.Max(2, steps);
        var path = new List<MacroWaypoint>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            double tau = (double)i / n;
            double u = MinimumJerk(tau);
            double inv = 1.0 - u;

            double x = (inv * inv * inv * x0)
                     + (3.0 * inv * inv * u * cx1)
                     + (3.0 * inv * u * u * cx2)
                     + (u * u * u * x1);
            double y = (inv * inv * inv * y0)
                     + (3.0 * inv * inv * u * cy1)
                     + (3.0 * inv * u * u * cy2)
                     + (u * u * u * y1);

            path.Add(new MacroWaypoint(x, y, tau * durationMs));
        }

        return path;
    }

    /// <summary>Samples a bounded Box-Muller Gaussian delay (ms) to prevent robotic fixed-interval timing.</summary>
    public static double SampleGaussianJitterMs(
        double meanMs = 180.0,
        double stdDevMs = 45.0,
        double minMs = 40.0,
        double maxMs = 600.0,
        Random? rng = null)
    {
        rng ??= Random.Shared;
        double u1 = Math.Max(1e-12, 1.0 - rng.NextDouble());
        double u2 = 1.0 - rng.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return Math.Clamp(meanMs + (z * stdDevMs), minMs, maxMs);
    }
}

/// <summary>
/// SIAP-NEO / SIAP-DESKTOP connector merging inspection timestamps & BARCON status into DynamicSqliteStudio.
/// </summary>
public sealed class SiapNeoConnector : IExternalConnector
{
    private readonly ConnectorProfileConfig _config;

    public SiapNeoConnector(ConnectorProfileConfig config)
    {
        _config = config;
    }

    public string Name => "SIAP-NEO / SIAP-DESKTOP";
    public ConnectorMode Mode => _config.Mode;

    public Task<bool> ProbeHealthAsync(CancellationToken ct = default)
    {
        bool ok = _config.Enabled && !string.IsNullOrWhiteSpace(_config.EndpointOrPath);
        return Task.FromResult(ok);
    }

    /// <summary>Applies a batch of SIAP-NEO inspection deltas into the INPUT table matched by normalized No. IP.</summary>
    public int ApplyDeltas(
        SchemaStudio.DynamicSqliteStudio studio,
        string workspaceId,
        IEnumerable<SiapNeoInspectionDelta> deltas,
        string tableSlug = "input")
    {
        var ws = studio.GetWorkspaceById(workspaceId)
            ?? throw new InvalidOperationException($"Workspace '{workspaceId}' not found.");
        var table = studio.GetTableBySlug(workspaceId, tableSlug)
            ?? throw new InvalidOperationException($"Table '{tableSlug}' not found.");
        var columns = studio.GetColumns(table.Id);

        int updatedCount = 0;
        foreach (var d in deltas)
        {
            string normIp = SchemaStudio.SmartSanitizer.NormalizeNoIp(d.NoIp);
            var row = studio.FindRowByColumnValue(table, columns, "no_ip", normIp);
            if (row is null) continue;

            var patch = new Dictionary<string, string?>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(d.WaktuPkb)) patch["waktu_pkb"] = d.WaktuPkb;
            if (!string.IsNullOrWhiteSpace(d.WaktuMulaiPeriksa)) patch["waktu_mulai_periksa"] = d.WaktuMulaiPeriksa;
            if (!string.IsNullOrWhiteSpace(d.SelesaiPeriksa)) patch["selesai_periksa"] = d.SelesaiPeriksa;
            if (!string.IsNullOrWhiteSpace(d.SelesaiLhp)) patch["selesai_lhp"] = d.SelesaiLhp;
            if (!string.IsNullOrWhiteSpace(d.Barcon)) patch["barcon"] = d.Barcon;
            if (!string.IsNullOrWhiteSpace(d.Pengurus)) patch["pengurus"] = d.Pengurus;
            if (!string.IsNullOrWhiteSpace(d.NoHp)) patch["no_hp"] = d.NoHp;

            if (patch.Count > 0)
            {
                studio.UpdateRowCells(table, columns, row.RowId, patch, ws.Year);
                updatedCount++;
            }
        }
        return updatedCount;
    }
}

/// <summary>
/// BOT-MONITOR-PERIKSA connector watching a download directory for newly fetched PIB/LHP/B/L files.
/// </summary>
public sealed class BotMonitorPeriksaConnector : IExternalConnector
{
    private readonly ConnectorProfileConfig _config;

    public BotMonitorPeriksaConnector(ConnectorProfileConfig config)
    {
        _config = config;
    }

    public string Name => "BOT-MONITOR-PERIKSA";
    public ConnectorMode Mode => _config.Mode;

    public Task<bool> ProbeHealthAsync(CancellationToken ct = default)
    {
        bool ok = _config.Enabled && Directory.Exists(_config.EndpointOrPath);
        return Task.FromResult(ok);
    }

    /// <summary>Scans the watched download directory for ready PDF documents (lock-free probe).</summary>
    public List<string> ScanReadyDocuments()
    {
        if (!Directory.Exists(_config.EndpointOrPath)) return [];

        var ready = new List<string>();
        foreach (string file in Directory.EnumerateFiles(_config.EndpointOrPath, "*.pdf", SearchOption.TopDirectoryOnly))
        {
            if (IsFileReady(file))
            {
                ready.Add(file);
            }
        }
        return ready;
    }

    private static bool IsFileReady(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return fs.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
