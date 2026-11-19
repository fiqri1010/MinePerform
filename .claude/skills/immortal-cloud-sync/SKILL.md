---
name: immortal-cloud-sync
description: Automatically activates on Google OAuth 2.0 authentication (PC Windows PKCE & Android Credential Manager), Firebase Firestore REST API sync (Spark Free Tier — never sleeps), Schema Blueprint + Compressed Delta Log replication, OneDrive full .sqlite auto-backup, and cross-platform Android Kotlin compatibility.
---

# Immortal Hybrid Cloud Sync (`immortal-cloud-sync`)

## 1. Architecture Rationale: Forever-Free & Zero-Pause
- **Why NOT Supabase Free Tier?** Supabase pauses projects after 7 days of inactivity.
- **MinePerform Hybrid Cloud (100% Free & Immortal):**
  1. **Google OAuth 2.0 Identity:**
     - **PC Windows (Native AOT):** RFC 8252 Loopback HTTP redirect + PKCE (`S256`) exchanging Google ID token -> Firebase Auth REST API (`identitytoolkit.googleapis.com/v1/accounts:signInWithIdp`). Persist refresh token securely via Windows DPAPI (`ProtectedData` / Win32 `CryptProtectData` via `[LibraryImport]`).
     - **Android (Kotlin):** `androidx.credentials:credentials` (Credential Manager) + Google ID Option -> Firebase Auth.
  2. **Firebase Firestore (Spark Free Tier — Never Sleeps):**
     - PC communicates via lightweight **Firestore REST API v1** + `HttpClient` + `AppJsonContext` (Zero gRPC/reflection bloat, keeps Native AOT RAM `< 25 MB`!).
     - Android uses `com.google.firebase:firebase-firestore-ktx` reading/writing the exact same Firestore document contract.
  3. **OneDrive Full `.sqlite` Auto-Backup:**
     - Periodically creates a crash-safe SQLite snapshot via `SqliteConnection.BackupDatabase(...)` (online backup API — safe during WAL writes), compresses with Brotli/GZip (`mineperform_{year}_{yyyyMMdd_HHmm}.sqlite.gz`), and copies into the user's local OneDrive sync folder (or Microsoft Graph API) with rolling retention (keep last 14 snapshots).

## 2. Bandwidth-Optimal Sync Protocol: Schema Blueprint + Compressed Delta Log
Never push entire 56-column SQLite tables to Firestore on every edit (protects Spark Free Tier daily read/write/storage quotas):
- **Firestore Collection Layout (`/users/{uid}/workspaces/{year}/...`):**
  - `blueprint/current`: Compact JSON snapshot of `sys_tables`, `sys_column_groups`, `sys_columns`, `sys_table_relations`, IKU portfolio (`1a-N`, `1b-N`, `2a-N`, ...), and Lego-Token AST trees + Sub-Tokens.
  - `deltas/{batchId}`: GZip + Base64 compressed batch of row mutations (`UPSERT_ROW`, `DELETE_ROW`, `UPSERT_DETAIL`, `RESOLVE_ANOMALY`) with Lamport/HLC timestamps (`hlc_ts`, `device_id`, `seq`).
  - `checkpoints/{deviceId}`: Last applied `hlc_ts` and schema version hash.
- **Conflict Resolution:** Field-level **Last-Write-Wins (LWW)** per `(table_slug, row_id, column_key)` using monotonic Hybrid Logical Clock (`hlc_ts`), with explicit conflict log preservation so field data from Android (e.g., on-site inspection notes & `TEMUAN?`) merges seamlessly with PC updates (`SIAP-NEO` timestamps & `PUTUSAN PFPD`).

## 3. Offline-First & Cross-Platform Parity (PC C# <-> Android Kotlin)
1. **Local SQLite is Always Source of Truth:** Both PC and Android operate 100% offline with instant UI responsiveness; mutations append to a local `sys_sync_outbox` table and flush opportunistically when online.
2. **Canonical JSON Contract:**
   - All shared models (`SchemaBlueprint`, `LegoTokenNode`, `IkuDefinition`, `SyncDeltaBatch`, `RowDetailPayload`) use identical `snake_case` JSON field names and UTC ISO-8601 timestamps (`yyyy-MM-ddTHH:mm:ss.fffZ`) across C# (`System.Text.Json` Source Generator) and Kotlin (`kotlinx.serialization`).
3. **Security & Privacy Rules:**
   - Firestore Security Rules MUST enforce strict user isolation: `allow read, write: if request.auth != null && request.auth.uid == userId;`.
   - Never log OAuth tokens, refresh tokens, or raw taxpayer/importer PII in diagnostic logs.
   - Never commit `serviceAccountKey.json`, `google-services.json`, or `.env` files to Git.
