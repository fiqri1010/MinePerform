# CLAUDE.md — Konstitusi Rekayasa Kode MinePerform (Prinsip Andrej Karpathy)

> **Single Source of Truth untuk Arsitektur, Kualitas Kode, Performa Native AOT, dan Verifikasi Mandiri.**
> Setiap sesi kerja pada repositori **MinePerform** wajib mematuhi dokumen ini bersama [`UI_UX_DESIGN_SYSTEM.md`](./UI_UX_DESIGN_SYSTEM.md).

---

## 1. Identitas & Misi Sistem

**MinePerform** adalah ekosistem lintas-platform (**PC Windows Native AOT** + **Android Native Kotlin**) yang terdiri dari:
1. **Adaptive KPI/IKU Portfolio (`1a-N`, `1b-N`, `2a-N`, tak terbatas)** dengan **Lego-Token Formula Engine**.
2. **No-Code Layman SQLite Table Studio** (1–5+ tabel bebas per tahun, 4 Super-Header, 3 Frozen Columns, Computed Columns, Visual Table Linker, Selective Year-to-Year Blueprint Cloner).
3. **Adaptive Customs Document Compiler** (menggantikan spreadsheet manual `Data IP 2026.xlsx` — 9 sheet, serta mengkompilasi PDF **PIB BC 2.0, Lembar Lanjutan, LHP, B/L** + OCR ke *Master-Detail Row Drawer*).
4. **External Connectors & Autonomous OneDrive 5 GB+ Organizer** (`SIAP-NEO` / `SIAP-DESKTOP`, `BOT-MONITOR-PERIKSA`, humanized macro, serta *Zero-Copy* `MoveFileExW` pemindah foto 5 GB+ via `No. IP` ke `Nama Folder Onedrive`).
5. **Immortal Cloud Sync** (**Google OAuth 2.0** + **Firebase Firestore Spark Free Tier** + auto-backup `.sqlite` ke **OneDrive**).

---

## 2. Lima Prinsip Rekayasa Andrej Karpathy (Wajib Dipatuhi)

### Prinsip 1 — *Concrete Data Structures First* (Struktur Data Nyata Sebelum Abstraksi)
- Sebelum menulis *service*, *ViewModel*, atau UI, definisikan terlebih dahulu struktur data konkret (skema tabel kontrol SQLite `sys_*`, kontrak JSON *Lego-Token AST*, kontrak `SyncDeltaBatch`, dan struktur *Master-Detail* `sys_row_details`) dengan tipe eksplisit yang 100% kompatibel antara **C# Native AOT (`System.Text.Json` Source Generator)** dan **Kotlin (`kotlinx.serialization`)**.
- Hindari *over-abstraction* dan *enterprise boilerplate* berlapis tanpa manfaat nyata. Pilih *record*, *readonly struct*, `ReadOnlySpan<char>`, dan fungsi murni yang mudah diaudit dan diuji.
- Selalu perlakukan **`No. IP` sebagai `string`/`TEXT`** (JANGAN pernah `int`), karena nomor nyata memuat angka nol di depan (`000765`, `001081`) sekaligus kode non-PIB (`ATA-003/IMPOR/2026`).

### Prinsip 2 — *Zero Hardcoded Schemas or Formulas* (0% Skema & Rumus Kaku)
- **DILARANG KERAS** memaku rumus IKU (`1a-N`, `1b-N`, `2a-N`, dst.) atau skema kolom `INPUT` sebagai properti statis di kode C#/Kotlin.
- Seluruh 56 kolom `INPUT` (4 Super-Header) serta 8 sheet pendamping (`Jam Presensi`, `Calc Pack`, `Data Master`, `Cek IKU`, `REVIEW`, `PIVOT`, `Catatan`, `DASHBOARD`) dimuat sebagai **Dynamic Seed Template (JSON Blueprint)** ke dalam tabel metadata SQLite (`sys_tables`, `sys_column_groups`, `sys_columns`, `sys_table_relations`).
- Seluruh rumus IKU, tabel konversi (`📊`), filter (`🏷️`), dan sub-rumus (`🧩`) dikompilasi secara dinamis dari **Lego-Token JSON AST** menjadi **Parameterized SQLite Query**.

### Prinsip 3 — *Non-Blocking Streaming File I/O* (Anggaran RAM `< 25 MB` & Startup `< 500 ms`)
- Target konsumsi RAM aplikasi PC Windows Native AOT: **15 MB – 25 MB** saat idle/aktif, *cold startup* `< 500 ms`.
- **DILARANG** memuat seluruh workbook Excel, PDF multi-halaman, atau berkas foto **5 GB+** ke dalam RAM sekaligus:
  - Excel: wajib streaming baris per baris via **`MiniExcel`**.
  - PDF: wajib baca halaman per halaman via **`UglyToad.PdfPig`** lalu langsung `Dispose()`.
  - Foto 5 GB+: wajib membaca metadata path saja dan memindahkan berkas via **Win32 Zero-Copy `MoveFileExW`** (`[LibraryImport]`) menggunakan *bounded* `Channel<T>` (`capacity: 64`) dan `IProgress<T>` yang di-*throttle* agar UI tetap 120 FPS.
- **Zero Dynamic Reflection:** Wajib `PublishAot=true`, `AvaloniaUseCompiledBindingsByDefault=true`, `x:DataType` pada setiap View & `DataTemplate`, `System.Text.Json` Source Generators (`AppJsonContext`), `Dapper.AOT`, `ZLinq`, `CommunityToolkit.Mvvm`, dan `[LibraryImport]` (bukan `[DllImport]`).

### Prinsip 4 — *Defensive Data Sanitization* (Tahan Anomali Data Kerja Nyata)
Data operasional lapangan selalu kotor. Pipeline impor dan penyimpanan WAJIB menangani 5 anomali nyata tanpa pernah *crash*:
1. **Format Mata Uang Excel Kacau:** `" $2,880,771,505.92 "` $\rightarrow$ `2880771505.92m`; `" $- "` $\rightarrow$ `0m` (`decimal` murni), ditampilkan rapi sesuai format kolom (default `Rp`).
2. **Karakter Ilegal Nama Folder Windows (*Path Safety Guard*):** Mengganti `/` dan `\` dengan `-` (misal `ATA-003/IMPOR/2026` $\rightarrow$ `ATA-003-IMPOR-2026`), menghapus `\r`, `\n`, `\t` (misal baris baru di `JENIS BARANG`), membersihkan `: * ? " < > |`, dan menjaga batas panjang path Windows pada kolom otomatis `Nama Folder Onedrive`.
3. **Typo Tanggal & Nilai Kosong (*Data Hygiene Radar*):** Mendeteksi anomali seperti tahun typo `05/11/2515` (baris 177) atau `JML JENIS PERIKSA` kosong (baris 111 & 180) dengan menandai badge kuning halus `⚠️` di `sys_anomalies` tanpa menggagalkan impor.
4. **Sanitasi SQLite Identifier:** Semua nama tabel/kolom dinamis wajib tervalidasi melalui slug allowlist (`^[a-z][a-z0-9_]{1,62}$`) dan semua nilai wajib melalui parameter terikat (`@p0`, `@p1`, ...).
5. **Kerahasiaan Data Pabean & Kredensial:** Jangan pernah menaruh data operasional nyata (`Data IP 2026.xlsx`, PDF PIB/LHP/B/L, `.sqlite`), kunci Firebase, atau token OAuth ke dalam Git.

### Prinsip 5 — *Always-On Autonomous Verification* (Verifikasi Mandiri Tanpa Disuruh)
- Setiap perubahan kode wajib diverifikasi secara otomatis melalui `.claude/settings.json` hooks (`UserPromptSubmit`, `PostToolUse`, `Stop`) dan unit/integration tests:
  - **Audit AOT & Memori:** 0 warning `IL2xxx`/`IL3xxx`, 0 reflection dinamis, RAM `< 25 MB`.
  - **Audit Visual & UI:** 0 angka spasi di luar 8-Point Grid (`4, 6, 8, 12, 16, 24, 32px`), 0 `<Canvas>` untuk teks/token, 3 Frozen Columns & 4 Super-Header untuk tabel 56 kolom, 0% tabrakan UI.
  - **Audit Data:** Uji impor bersih 212+ baris `INPUT` (beserta anomali `$-`, `/`, `\n`, `05/11/2515`), kompilasi AST Lego-Token, dan simulasi pemindahan folder foto via `No. IP`.

---

## 3. Sepuluh Batasan Arsitektur Mutlak (Ringkasan Cepat)

| # | Batasan Mutlak | Penegakan Teknis |
| :---: | :--- | :--- |
| 1 | **Auto-Discovery Referensi Kerja** | Pindai `Data IP 2026.xlsx` (9 sheet), `Manual IKU 2026.docx`, dan sampel PDF di root / `docs/references/` menggunakan MCP `markitdown` / `MiniExcel`. Jika belum ada, gunakan cetak biru terkalibrasi 56 kolom sebagai seed dinamis. |
| 2 | **Performa Ekstrem PC & Android** | PC: C# .NET 8/9+ Native AOT + Avalonia v11+ (RAM `15–25 MB`, startup `< 500 ms`). Android: Murni Kotlin + Jetpack Compose berbagi skema SQLite, AST JSON, & Firebase Sync yang identik. |
| 3 | **Desain Sleek & Anti-Tabrakan** | `Semi.Avalonia` + `Semi.Avalonia.DataGrid` + `Lucide.Avalonia`, 8pt Grid, 4 Super-Headers berwarna, `FrozenColumnCount="3"`, Pill Toggle Grup Kolom, `CharacterEllipsis`, `WrapPanel`/`FlowRow`. |
| 4 | **Workspace Tahunan & Unlimited IKU** | Kode IKU Kemenkeu (`1a-N`, `1b-N`, `2a-N`, `-CP`, `-CNP`), jumlah tak terbatas, 4 Halaman Terpadu per Tahun (`DATA`, `IKU & FORMULA`, `CHART & SUMMARY`, `KONEKTOR & ONEDRIVE`). |
| 5 | **Selective Year Cloner** | Wizard centang selektif (`2026 -> 2027`) menyalin struktur tabel/grup/relasi/IKU/AST/konektor/pola OneDrive tanpa menyalin baris data transaksi tahun lama. |
| 6 | **No-Code SQLite Table Studio** | 1–5+ tabel bebas per tahun di SQLite lokal tanpa menulis SQL, Computed Columns, Visual Table Linker, Smart Sanitizer, dan Anomaly Radar. |
| 7 | **Zero Hardcoded Formulas** | 100% Modular Lego-Token Engine (`📦`, `⏱️`, `∑`, `🏷️`, `🔀`/`📊`, `🧩`), Drag & Drop 120 FPS + Click & Pin at Caret, transpiler AST -> SQLite SQL. |
| 8 | **Kompilasi Dokumen Pabean & 3 Mode Input** | Manual/Smart Messy Paste; Semi-Auto (XLSX/CSV, PDF PIB 1 Barang vs Lembar Lanjutan -> Master-Detail Drawer, LHP, B/L, OCR); Full-Auto (Humanized Macro: Minimum-Jerk Bezier + Gaussian Jitter). |
| 9 | **Konektor Ganda & OneDrive 5 GB+ Sorter** | `SIAP-NEO`/`SIAP-DESKTOP` + `BOT-MONITOR-PERIKSA` + auto-matching folder foto 5 GB+ via `No. IP` ke `Nama Folder Onedrive/{Berkas,Foto}` via Zero-Copy `MoveFileExW` + `Channel<T>`. |
| 10 | **Immortal Hybrid Cloud Sync** | Google OAuth 2.0 + Firebase Firestore REST API (Spark Free Tier, tidak pernah tidur) memakai *Schema Blueprint + Compressed Delta Log* + auto-backup `.sqlite` utuh ke OneDrive. |

---

## 4. Cetak Biru Struktur Data `INPUT` Terkalibrasi (56 Kolom — 4 Super-Header)

Disimpan sebagai **Dynamic Seed Template** (BUKAN class C# statis):

1. **`INFO DASAR` (`#3B82F6` — 30 Kolom):**
   `No.` *(Frozen)*, `Status` *(Frozen)*, `No. IP` *(Frozen)*, `Tanggal IP`, `TPS`, `Jenis Impor`, `Importir`, `Pemilik Barang`, `PPJK`, `FCL/LCL`, `JML CONT 40F?`, `JML CONT 20F`, `CONT 40F? PERIKSA`, `CONT 20F PERIKSA`, `JML PK`, `JML PK PERIKSA` *(Computed)*, `JUMLAH JENIS`, `JML JENIS PERIKSA`, `JENIS BARANG`, `CAMPURAN?`, `HS CODE 1 JENIS`, `NILAI PABEAN`, `BM`, `BMT`, `BM KITE`, `CUKAI`, `PPN`, `PPnBM`, `PPh`, `TOTAL PDRI` *(Computed)*.
2. **`DATA PEMERIKSAAN` (`#F59E0B` — 14 Kolom):**
   `TK PERIKSA` (`10%`, `30%`, `MENDALAM`), `Waktu PKB`, `Waktu Mulai Periksa`, `Selesai Periksa`, `Selesai LHP`, `Alasan Mulai >1 JAM`, `Kendala Pemeriksaan`, `Konfirmasi?`, `BARCON`, `Kesimpulan LHP`, `KET LAIN`, `TEMUAN?`, `Pengurus`, `No. HP`.
3. **`PUTUSAN PFPD` (`#8B5CF6` — 11 Kolom):**
   `SPBL`, `NOTUL?`, `BM-`, `DENDA ADM-`, `BMT-`, `BM KITE-`, `CUKAI-`, `PPN-`, `PPnBM-`, `PPh-`, `Total Kurang Bayar` *(Computed)*.
4. **`LINK DATA` (`#10B981` — 1 Kolom Otomatis):**
   `Nama Folder Onedrive` *(Computed + Path Safety Guard)*:
   `{No.}. {No. IP}_{Tanggal IP (dd.MM.yyyy)}_{Importir}_{TPS}_{JENIS BARANG}`

Sheet pendamping referensi `Data IP 2026.xlsx` (total 9 sheet): `INPUT`, `Jam Presensi`, `Calc Pack`, `Data Master`, `Cek IKU`, `REVIEW`, `PIVOT`, `Catatan`, `DASHBOARD`.

---

## 5. Ekosistem Auto-Active Skills & MCP

Seluruh 15 MCP Server di [`.mcp.json`](./.mcp.json) dan 7 Auto-Active Skills di `.claude/skills/` aktif otomatis sesuai konteks:
- `sleek-ui-figma-architect` — `.axaml`, `@Composable`, Figma/Penpot MCP, 8pt Grid, 0% tabrakan UI.
- `aot-memory-guardian` — `.cs`, `.csproj`, `.axaml`, RAM `< 25 MB`, Native AOT, Zero Reflection.
- `layman-sqlite-architect` — Dynamic SQLite Studio, Column Groups, Computed Columns, Linker, Sanitizer, Cloner.
- `lego-token-ast-compiler` — Portofolio IKU tak terbatas, AST Lego-Token -> SQLite SQL, Chart & Summary.
- `customs-doc-compiler` — `Data IP 2026.xlsx`, PDF PIB + Lembar Lanjutan, LHP, B/L, OCR, Auto-Merge.
- `siap-bot-onedrive-bridge` — `SIAP-NEO`/`SIAP-DESKTOP`, `BOT-MONITOR-PERIKSA`, Macro, OneDrive 5 GB+ Sorter.
- `immortal-cloud-sync` — Google OAuth 2.0, Firebase Firestore REST Sync, OneDrive `.sqlite` backup, Android sync.

---

## 6. Peta Struktur Solusi Target (Tahap 2 – Tahap 5)

```text
MinePerform/
├── src/
│   ├── Core.Engine/                     # .NET 8/9 AOT-compatible class library (IsAotCompatible=true)
│   │   ├── SchemaStudio/                # Modul 1: Dynamic SQLite Studio, Sanitizer, Anomaly Radar, Year Cloner
│   │   ├── LegoTokenEngine/             # Modul 2: Unlimited IKU, Lego-Token AST -> SQLite Transpiler, Analytics
│   │   ├── FileAndDocAutomation/        # Modul 3: MiniExcel, PdfPig PIB/LHP/BL, OCR, SIAP-NEO/BOT, MoveFileExW 5GB+
│   │   └── CloudSync/                   # Modul 4: Google OAuth PKCE, Firestore REST, Delta Log, OneDrive Backup
│   ├── PC.AvaloniaApp/                  # .NET Native AOT + Avalonia v11 + Semi.Avalonia + LiveCharts2
│   └── Core.Engine.Tests/               # Unit & integration tests (212+ row sanitizer, AST compiler, zero-copy mock)
├── android/                             # Pure Kotlin + Jetpack Compose (Material 3 Expressive, Vico, ML Kit, Firestore)
└── docs/                                # Arsitektur terkalibrasi & folder references (gitignored untuk data nyata)
```
