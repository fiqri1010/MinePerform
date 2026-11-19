using System.Text.Json.Serialization;
using MinePerform.Core.LegoTokenEngine;
using MinePerform.Core.SchemaStudio;

namespace MinePerform.Core.CloudSync;

/// <summary>Canonical Schema Blueprint synchronized between PC Windows and Android via Firebase Firestore.</summary>
public sealed record SchemaBlueprintSnapshot(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("workspace")] WorkspaceModel Workspace,
    [property: JsonPropertyName("tables")] List<TableDefinition> Tables,
    [property: JsonPropertyName("column_groups")] List<ColumnGroupDefinition> ColumnGroups,
    [property: JsonPropertyName("columns")] List<ColumnDefinition> Columns,
    [property: JsonPropertyName("relations")] List<TableRelationDefinition> Relations,
    [property: JsonPropertyName("ikus")] List<IkuDefinition> Ikus,
    [property: JsonPropertyName("sub_tokens")] List<SubTokenDefinition> SubTokens,
    [property: JsonPropertyName("updated_at_utc")] string UpdatedAtUtc);

/// <summary>Single row-level mutation inside a compressed Delta Log batch.</summary>
public sealed record SyncDeltaMutation(
    [property: JsonPropertyName("op_kind")] string OpKind,
    [property: JsonPropertyName("table_slug")] string TableSlug,
    [property: JsonPropertyName("row_id")] long RowId,
    [property: JsonPropertyName("hlc_ts")] string HlcTs,
    [property: JsonPropertyName("device_id")] string DeviceId,
    [property: JsonPropertyName("cells")] Dictionary<string, string?> Cells);

/// <summary>Compressed delta batch exchanged via Firestore /users/{uid}/workspaces/{year}/deltas/{batchId}.</summary>
public sealed record SyncDeltaBatch(
    [property: JsonPropertyName("batch_id")] string BatchId,
    [property: JsonPropertyName("workspace_year")] int WorkspaceYear,
    [property: JsonPropertyName("device_id")] string DeviceId,
    [property: JsonPropertyName("created_at_utc")] string CreatedAtUtc,
    [property: JsonPropertyName("mutations")] List<SyncDeltaMutation> Mutations);

/// <summary>OAuth 2.0 PKCE challenge/verifier pair for PC Desktop Google Sign-In.</summary>
public sealed record PkceChallengePair(
    [property: JsonPropertyName("code_verifier")] string CodeVerifier,
    [property: JsonPropertyName("code_challenge")] string CodeChallenge,
    [property: JsonPropertyName("method")] string Method = "S256");

/// <summary>Authenticated Google + Firebase session.</summary>
public sealed record GoogleAuthSession(
    [property: JsonPropertyName("uid")] string Uid,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("display_name")] string DisplayName,
    [property: JsonPropertyName("id_token")] string IdToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_at_utc")] string ExpiresAtUtc);

/// <summary>Result of online crash-safe SQLite backup to OneDrive.</summary>
public sealed record OneDriveBackupResult(
    [property: JsonPropertyName("backup_file_path")] string BackupFilePath,
    [property: JsonPropertyName("compressed_bytes")] long CompressedBytes,
    [property: JsonPropertyName("pruned_old_backups")] int PrunedOldBackups,
    [property: JsonPropertyName("created_at_utc")] string CreatedAtUtc);

/// <summary>Firebase Identity Toolkit signInWithIdp request payload (AOT-safe).</summary>
public sealed record FirebaseIdpSignInRequest(
    [property: JsonPropertyName("postBody")] string PostBody,
    [property: JsonPropertyName("requestUri")] string RequestUri,
    [property: JsonPropertyName("returnIdpCredential")] bool ReturnIdpCredential,
    [property: JsonPropertyName("returnSecureToken")] bool ReturnSecureToken);

/// <summary>Firebase Identity Toolkit signInWithIdp response payload (AOT-safe).</summary>
public sealed record FirebaseIdpSignInResponse(
    [property: JsonPropertyName("localId")] string? LocalId,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("idToken")] string? IdToken,
    [property: JsonPropertyName("refreshToken")] string? RefreshToken,
    [property: JsonPropertyName("expiresIn")] string? ExpiresIn);
