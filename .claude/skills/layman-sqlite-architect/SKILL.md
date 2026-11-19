---
name: layman-sqlite-architect
description: Automatically activates when designing, migrating, or modifying SQLite databases, dynamic table schemas, Column Groups, Computed Columns, Visual Table Linker, Smart Sanitizer, Anomaly Radar, or Selective Year-to-Year Blueprint Cloner. Ensures 100% No-Code dynamic SQLite storage with zero hardcoded domain schemas.
---

# No-Code Layman SQLite Architect (`layman-sqlite-architect`)

## 1. Core Design Principle: Metadata-Driven Dynamic SQLite (Zero Hardcoded Tables)
Users MUST be able to create, rename, reorder, group, link, and delete **1–5+ free-form tables per Year Workspace** (e.g., `INPUT`, `Jam Presensi`, `Calc Pack`, `Data Master`, `Catatan`) without ever writing SQL.

### Concrete System Metadata Schema (Control Plane)
Store schema definitions in dedicated metadata tables while backing each user table with a real, indexed SQLite physical table (`usr_y{year}_{table_slug}`) for sub-millisecond SQLite query performance:
1. `sys_workspaces (id, year, display_name, created_at_utc, is_active)`
2. `sys_tables (id, workspace_id, slug, display_name, physical_table_name, sort_order, icon, is_primary)`
3. `sys_column_groups (id, table_id, name, color_hex, chip_bg_hex, sort_order, is_collapsed_default)`
4. `sys_columns (id, table_id, group_id, column_key, display_name, data_type, is_frozen, is_computed, computed_ast_json, currency_code, format_mask, validation_rule_json, sort_order, width_px)`
5. `sys_table_relations (id, workspace_id, source_table_id, source_column_key, target_table_id, target_column_key, relation_type, display_label)` — powers **Visual Table Linker** (`🔗 Hubungkan Antar Tabel`).
6. `sys_row_details (id, table_id, parent_row_id, detail_kind, payload_json, sort_order)` — backs the **Master-Detail Row Drawer** (PIB *Lembar Lanjutan* goods items, containers, attached documents).
7. `sys_anomalies (id, table_id, row_id, column_key, severity, rule_code, raw_value, message, resolved)` — powers **Data Hygiene Radar** (`⚠️`).

### SQLite Engine Pragmas (Always Applied on Connection Open)
```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA foreign_keys = ON;
PRAGMA temp_store = MEMORY;
PRAGMA cache_size = -4000; -- ~4 MB cap to protect < 25 MB RAM budget
PRAGMA busy_timeout = 5000;
```

## 2. Dynamic Seed Template: Calibrated `Data IP 2026` Blueprint (Non-Hardcoded)
The 56-column `INPUT` table is loaded as a **JSON/Template Blueprint** (never hardcoded C# properties):
- **Group 1 — `INFO DASAR` (`#3B82F6`, 30 cols):** `No.` *(Frozen)*, `Status` *(Frozen)*, `No. IP` *(Frozen)*, `Tanggal IP`, `TPS`, `Jenis Impor`, `Importir`, `Pemilik Barang`, `PPJK`, `FCL/LCL`, `JML CONT 40F?`, `JML CONT 20F`, `CONT 40F? PERIKSA`, `CONT 20F PERIKSA`, `JML PK`, `JML PK PERIKSA` *(Computed)*, `JUMLAH JENIS`, `JML JENIS PERIKSA`, `JENIS BARANG`, `CAMPURAN?`, `HS CODE 1 JENIS`, `NILAI PABEAN`, `BM`, `BMT`, `BM KITE`, `CUKAI`, `PPN`, `PPnBM`, `PPh`, `TOTAL PDRI` *(Computed)*.
- **Group 2 — `DATA PEMERIKSAAN` (`#F59E0B`, 14 cols):** `TK PERIKSA`, `Waktu PKB`, `Waktu Mulai Periksa`, `Selesai Periksa`, `Selesai LHP`, `Alasan Mulai >1 JAM`, `Kendala Pemeriksaan`, `Konfirmasi?`, `BARCON`, `Kesimpulan LHP`, `KET LAIN`, `TEMUAN?`, `Pengurus`, `No. HP`.
- **Group 3 — `PUTUSAN PFPD` (`#8B5CF6`, 11 cols):** `SPBL`, `NOTUL?`, `BM-`, `DENDA ADM-`, `BMT-`, `BM KITE-`, `CUKAI-`, `PPN-`, `PPnBM-`, `PPh-`, `Total Kurang Bayar` *(Computed)*.
- **Group 4 — `LINK DATA` (`#10B981`, 1 col):** `Nama Folder Onedrive` *(Computed token pattern)*.

Companion template tables from `Data IP 2026.xlsx`: `Jam Presensi`, `Calc Pack`, `Data Master`, `Catatan`.

## 3. Computed Columns (Dynamic Evaluation)
Computed columns are defined via metadata AST/template expressions (never hardcoded column names in C#):
- **`TOTAL PDRI`:** `SUM(BM, BMT, BM KITE, CUKAI, PPN, PPnBM, PPh)`
- **`Total Kurang Bayar`:** `SUM(BM-, DENDA ADM-, BMT-, BM KITE-, CUKAI-, PPN-, PPnBM-, PPh-)`
- **`JML PK PERIKSA`:** Dynamic expression based on `TK PERIKSA` (`10%` / `30%` / `MENDALAM`) × `JML PK` (with configurable ceiling/minimum rule).
- **`Nama Folder Onedrive`:** Pattern `{No.}. {No. IP}_{Tanggal IP (dd.MM.yyyy)}_{Importir}_{TPS}_{JENIS BARANG}` passed through **Path Safety Guard**.

## 4. Smart Sanitizer & Data Hygiene Radar (5 Real-World Defenses)
1. **Excel Currency Sanitizer:** Strip leading/trailing whitespace, `$`, `Rp`, `,` (thousands), and convert `" $- "` or `"-"` to `0m` (`decimal`). Store canonical numeric `REAL`/`TEXT` exact decimal representation in SQLite; render using column's configured currency display (`Rp`).
2. **Windows Path Safety Guard:** Before outputting `Nama Folder Onedrive`, replace `/` and `\` with `-`, strip `\r`, `\n`, `\t`, remove illegal NTFS characters (`: * ? " < > |`), collapse repeated spaces, trim trailing dots/spaces, and cap segment length to avoid `MAX_PATH` issues.
3. **Date Anomaly Radar:** Flag out-of-range years (e.g., `year < workspaceYear - 2 || year > workspaceYear + 1`, catching real typos like `05/11/2515` at row 177) into `sys_anomalies` WITHOUT aborting import.
4. **Completeness & Format Radar:** Flag empty required inspection fields (e.g., blank `JML JENIS PERIKSA` on completed rows) and embedded `\n` in single-line text fields (`JENIS BARANG`).
5. **SQL Injection Defense:** Every dynamic table/column identifier is mapped through a strict allowlisted slug regex `^[a-z][a-z0-9_]{1,62}$` and quoted with `"identifier"`; all cell values MUST use parameterized `@p0, @p1, ...` bindings.

## 5. Selective Year-to-Year Blueprint Cloner (`2026 -> 2027`)
Provide a transactional wizard supporting independent checkboxes:
- `[x]` Tables, Column Groups, Frozen flags, Computed Column definitions (structure only — zero operational rows copied).
- `[x]` Visual Table Linker relations (`sys_table_relations`).
- `[x]` IKU Portfolio cards (`1a-N`, `1b-N`, `2a-N`, ...) and Lego-Token AST trees + Sub-Tokens.
- `[x]` External connector mappings (`SIAP-NEO` / `BOT-MONITOR-PERIKSA`) and Macro profiles.
- `[x]` OneDrive folder naming pattern and subfolder layout (`Berkas/`, `Foto/`).
- `[ ]` Optional: Reference lookup rows from `Data Master` (TPS codes, Jenis Impor master list).
