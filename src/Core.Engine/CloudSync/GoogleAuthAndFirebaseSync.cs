using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using MinePerform.Core.LegoTokenEngine;
using MinePerform.Core.SchemaStudio;
using MinePerform.Core.Serialization;

namespace MinePerform.Core.CloudSync;

/// <summary>
/// Monotonic Hybrid Logical Clock (HLC) for conflict-free Last-Write-Wins (LWW) field merges across PC &amp; Android.
/// Format: "{unixMs:D13}-{counter:D4}-{deviceId}" (lexicographically sortable).
/// </summary>
public sealed class HybridLogicalClock
{
    private readonly string _deviceId;
    private long _lastPhysicalMs;
    private int _counter;

    public HybridLogicalClock(string deviceId)
    {
        _deviceId = string.IsNullOrWhiteSpace(deviceId) ? "pc" : deviceId;
    }

    public string NextTimestamp()
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowMs > _lastPhysicalMs)
        {
            _lastPhysicalMs = nowMs;
            _counter = 0;
        }
        else
        {
            _counter++;
        }

        return $"{_lastPhysicalMs:D13}-{_counter:D4}-{_deviceId}";
    }

    public static int Compare(string? a, string? b) =>
        string.CompareOrdinal(a ?? string.Empty, b ?? string.Empty);
}

/// <summary>
/// RFC 8252 Google OAuth 2.0 PKCE helper &amp; Firebase Auth REST exchange (Zero gRPC / Native AOT safe).
/// </summary>
public static class GoogleAuthPkceService
{
    public static PkceChallengePair GeneratePkcePair()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        string verifier = Base64UrlEncode(bytes);
        byte[] hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        string challenge = Base64UrlEncode(hash);
        return new PkceChallengePair(verifier, challenge, "S256");
    }

    public static string BuildGoogleAuthorizationUrl(
        string clientId,
        string redirectUri,
        PkceChallengePair pkce,
        string state)
    {
        string scope = Uri.EscapeDataString("openid email profile");
        return "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&response_type=code"
            + $"&scope={scope}"
            + $"&code_challenge={Uri.EscapeDataString(pkce.CodeChallenge)}"
            + "&code_challenge_method=S256"
            + $"&state={Uri.EscapeDataString(state)}"
            + "&access_type=offline&prompt=consent";
    }

    public static async Task<GoogleAuthSession> ExchangeGoogleIdTokenWithFirebaseAsync(
        HttpClient httpClient,
        string firebaseWebApiKey,
        string googleIdToken,
        CancellationToken ct = default)
    {
        string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={Uri.EscapeDataString(firebaseWebApiKey)}";
        var reqBody = new FirebaseIdpSignInRequest(
            $"id_token={Uri.EscapeDataString(googleIdToken)}&providerId=google.com",
            "http://localhost",
            true,
            true);

        string json = JsonSerializer.Serialize(reqBody, AppJsonContext.Default.FirebaseIdpSignInRequest);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await httpClient.PostAsync(url, content, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        string respJson = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var parsed = JsonSerializer.Deserialize(respJson, AppJsonContext.Default.FirebaseIdpSignInResponse)
            ?? throw new InvalidOperationException("Invalid Firebase signInWithIdp response.");

        int expiresSec = int.TryParse(parsed.ExpiresIn, out int s) ? s : 3600;
        string expiresAt = DateTime.UtcNow.AddSeconds(expiresSec).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

        return new GoogleAuthSession(
            parsed.LocalId ?? string.Empty,
            parsed.Email ?? string.Empty,
            parsed.DisplayName ?? string.Empty,
            parsed.IdToken ?? string.Empty,
            parsed.RefreshToken ?? string.Empty,
            expiresAt);
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// Immortal Hybrid Cloud Sync engine (Firebase Firestore Spark Free Tier REST API v1).
/// Uses Schema Blueprint + GZip/Base64 Compressed Delta Log + field-level HLC LWW merge.
/// </summary>
public sealed class FirebaseRestSyncEngine
{
    private readonly DynamicSqliteStudio _studio;
    private readonly LegoTokenCompiler _compiler;

    public FirebaseRestSyncEngine(DynamicSqliteStudio studio, LegoTokenCompiler compiler)
    {
        _studio = studio;
        _compiler = compiler;
    }

    public SchemaBlueprintSnapshot ExportBlueprint(string workspaceId)
    {
        var ws = _studio.GetWorkspaceById(workspaceId)
            ?? throw new InvalidOperationException($"Workspace '{workspaceId}' not found.");
        var tables = _studio.ListTables(ws.Id);
        var groups = new List<ColumnGroupDefinition>();
        var columns = new List<ColumnDefinition>();
        foreach (var t in tables)
        {
            groups.AddRange(_studio.GetColumnGroups(t.Id));
            columns.AddRange(_studio.GetColumns(t.Id));
        }

        return new SchemaBlueprintSnapshot(
            SchemaVersion: 1,
            Workspace: ws,
            Tables: tables,
            ColumnGroups: groups,
            Columns: columns,
            Relations: _studio.ListTableRelations(ws.Id),
            Ikus: _compiler.ListIkuDefinitions(ws.Id),
            SubTokens: _compiler.GetSubTokensMap(ws.Id).Values.ToList(),
            UpdatedAtUtc: DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
    }

    /// <summary>Compresses a SyncDeltaBatch with GZip + Base64 to minimize Firebase Spark bandwidth &amp; storage.</summary>
    public static string CompressDeltaBatch(SyncDeltaBatch batch)
    {
        byte[] utf8 = JsonSerializer.SerializeToUtf8Bytes(batch, AppJsonContext.Default.SyncDeltaBatch);
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            gz.Write(utf8, 0, utf8.Length);
        }
        return Convert.ToBase64String(ms.ToArray());
    }

    /// <summary>Decompresses a GZip + Base64 SyncDeltaBatch received from Firestore.</summary>
    public static SyncDeltaBatch DecompressDeltaBatch(string base64Gzip)
    {
        byte[] compressed = Convert.FromBase64String(base64Gzip);
        using var input = new MemoryStream(compressed);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gz.CopyTo(output);
        return JsonSerializer.Deserialize(output.ToArray(), AppJsonContext.Default.SyncDeltaBatch)
            ?? throw new InvalidOperationException("Failed to deserialize compressed SyncDeltaBatch.");
    }

    /// <summary>
    /// Applies a remote SyncDeltaBatch using field-level Hybrid Logical Clock (LWW) conflict resolution.
    /// </summary>
    public Dictionary<string, string?> MergeFieldLevelLww(
        IReadOnlyDictionary<string, (string? Value, string HlcTs)> localFields,
        IReadOnlyDictionary<string, (string? Value, string HlcTs)> remoteFields)
    {
        var merged = new Dictionary<string, string?>(StringComparer.Ordinal);
        var allKeys = new HashSet<string>(localFields.Keys, StringComparer.Ordinal);
        allKeys.UnionWith(remoteFields.Keys);

        foreach (string key in allKeys)
        {
            bool hasLocal = localFields.TryGetValue(key, out var loc);
            bool hasRemote = remoteFields.TryGetValue(key, out var rem);

            if (hasLocal && hasRemote)
            {
                merged[key] = HybridLogicalClock.Compare(rem.HlcTs, loc.HlcTs) >= 0 ? rem.Value : loc.Value;
            }
            else if (hasRemote)
            {
                merged[key] = rem.Value;
            }
            else if (hasLocal)
            {
                merged[key] = loc.Value;
            }
        }

        return merged;
    }

    /// <summary>Pushes a compressed delta document to Firebase Firestore REST API v1.</summary>
    public static async Task PushDeltaBatchToFirestoreAsync(
        HttpClient httpClient,
        string firebaseProjectId,
        GoogleAuthSession session,
        SyncDeltaBatch batch,
        CancellationToken ct = default)
    {
        string compressed = CompressDeltaBatch(batch);
        string docUrl = $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(firebaseProjectId)}"
                      + $"/databases/(default)/documents/users/{Uri.EscapeDataString(session.Uid)}"
                      + $"/workspaces/{batch.WorkspaceYear}/deltas/{Uri.EscapeDataString(batch.BatchId)}";

        // Minimal Firestore REST document JSON (no reflection)
        string body = $$"""
            {
              "fields": {
                "batch_id": { "stringValue": "{{EscapeJson(batch.BatchId)}}" },
                "device_id": { "stringValue": "{{EscapeJson(batch.DeviceId)}}" },
                "created_at_utc": { "stringValue": "{{EscapeJson(batch.CreatedAtUtc)}}" },
                "payload_gzip_b64": { "stringValue": "{{compressed}}" }
              }
            }
            """;

        using var req = new HttpRequestMessage(HttpMethod.Patch, docUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.IdToken);

        using var resp = await httpClient.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}

/// <summary>
/// Online crash-safe SQLite backup to OneDrive (.sqlite.gz) using SQLite Online Backup API
/// with automatic rolling retention (keeps latest 14 snapshots).
/// </summary>
public static class OneDriveSqliteBackupService
{
    public static OneDriveBackupResult CreateCompressedBackup(
        DynamicSqliteStudio studio,
        string oneDriveBackupDir,
        int workspaceYear,
        int maxSnapshotsToKeep = 14)
    {
        Directory.CreateDirectory(oneDriveBackupDir);
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string tempRawPath = Path.Combine(Path.GetTempPath(), $"mineperform_bak_{stamp}_{Guid.NewGuid().ToString("N")[..6]}.sqlite");
        string finalGzPath = Path.Combine(oneDriveBackupDir, $"mineperform_{workspaceYear}_{stamp}.sqlite.gz");

        try
        {
            // Crash-safe online backup (safe even while WAL transactions are active)
            using (var destConn = new SqliteConnection($"Data Source={tempRawPath}"))
            {
                destConn.Open();
                studio.Connection.BackupDatabase(destConn);
            }

            // Stream-compress to OneDrive directory
            using (var src = new FileStream(tempRawPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
            using (var dst = new FileStream(finalGzPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
            using (var gz = new GZipStream(dst, CompressionLevel.Optimal))
            {
                src.CopyTo(gz, 65536);
            }
        }
        finally
        {
            if (File.Exists(tempRawPath))
            {
                try { File.Delete(tempRawPath); } catch { /* ignore temp cleanup failure */ }
            }
        }

        // Enforce rolling retention
        int pruned = 0;
        var existingBackups = Directory
            .EnumerateFiles(oneDriveBackupDir, $"mineperform_{workspaceYear}_*.sqlite.gz")
            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = maxSnapshotsToKeep; i < existingBackups.Count; i++)
        {
            try
            {
                File.Delete(existingBackups[i]);
                pruned++;
            }
            catch
            {
                // Best effort pruning
            }
        }

        long compressedSize = new FileInfo(finalGzPath).Length;
        return new OneDriveBackupResult(
            finalGzPath,
            compressedSize,
            pruned,
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
    }
}
