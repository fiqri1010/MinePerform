# MinePerform

**Adaptive KPI/IKU Portfolio (`1a-N`, `1b-N`, `2a-N`), No-Code SQLite Table Studio, Lego-Token Formula Engine, External Connectors (`SIAP-NEO`/`SIAP-DESKTOP` & `BOT-MONITOR-PERIKSA`), & Autonomous OneDrive 5 GB+ Work-File/Photo Organizer.**

MinePerform adalah ekosistem aplikasi lintas-platform (**PC Windows Native AOT** dan **Android Native Kotlin**) yang mengotomatisasi dan menggantikan alur kerja spreadsheet manual **`Data IP 2026.xlsx`** (9 sheet), mengkompilasi dokumen kepabeanan multi-halaman (**PIB BC 2.0, Lembar Lanjutan, LHP, B/L** + OCR), merakit rumus IKU secara modular dengan kepingan **Lego-Token** (*Zero Hardcoded Formulas*), menyinkronkan data lintas perangkat secara gratis & abadi (**Google OAuth 2.0 + Firebase Firestore Spark + OneDrive**), serta mengelompokkan berkas kerja dan foto pemeriksaan berukuran **5 GB+** secara *Zero-Copy* dengan target konsumsi memori **15–25 MB RAM** dan **0% tabrakan UI**.

---

## Ringkasan Kapabilitas Utama

| Modul | Fokus Utama | Teknologi Inti |
| :--- | :--- | :--- |
| **1. `Core.SchemaStudio`** | No-Code Dynamic SQLite Studio (1–5+ tabel/tahun), 4 Super-Headers & 3 Frozen Columns (56 kolom `INPUT`), Computed Columns (`TOTAL PDRI`, `Total Kurang Bayar`, `JML PK PERIKSA`, `Nama Folder Onedrive`), Visual Table Linker, Smart Sanitizer (`$-` $\rightarrow$ `0`, Windows Path Safety Guard, Anomaly Radar `⚠️`), Selective Year Cloner (`2026 -> 2027`). | `Microsoft.Data.Sqlite`, `SQLitePCLRaw`, `Dapper.AOT`, `MiniExcel` |
| **2. `Core.LegoTokenEngine`** | Portofolio IKU Kemenkeu tak terbatas (`1a-N`, `1b-N`, `2a-N`, `-CP`, `-CNP`), Kanvas Lego-Token lepas-pasang 120 FPS (Drag & Drop + Click & Pin at Caret), transpiler AST $\rightarrow$ Parameterized SQLite SQL, Audit Baris (`✅ Dihitung / ❌ Dikecualikan`), Bento-Grid `CHART & SUMMARY` lintas periode & proyeksi premi. | `System.Text.Json` Source Gen, `ZLinq`, `LiveChartsCore.SkiaSharpView.Avalonia` / `Vico` |
| **3. `Core.FileAndDocAutomation`** | 3 Mode Input (Manual/Smart Messy Paste, Semi-Auto XLSX/CSV/PDF/OCR, Full-Auto Humanized Macro *Minimum-Jerk Bezier* + *Gaussian Jitter*), Parser PIB BC 2.0 + *Lembar Lanjutan* ke *Master-Detail Row Drawer*, Auto-Merge PIB/LHP/BL/`SIAP-NEO`, Konektor `SIAP-NEO`/`SIAP-DESKTOP` & `BOT-MONITOR-PERIKSA`, serta pemindah foto **5 GB+** via `No. IP` ke `Nama Folder Onedrive/{Berkas,Foto}` (*Zero-Copy* `MoveFileExW` + `Channel<T>`). | `UglyToad.PdfPig`, `MiniExcel`, `Windows.Media.Ocr`, `ML Kit`, Win32 `[LibraryImport]` |
| **4. `Core.CloudSync`** | Login Google OAuth 2.0 (PKCE PC & Credential Manager Android), sinkronisasi *Schema Blueprint + Compressed Delta Log* via Firebase Firestore REST API (Spark Free Tier — tidak pernah tidur), serta *online auto-backup* `.sqlite.gz` ke OneDrive. | `HttpClient` + Source Gen JSON, Firebase Firestore REST v1, `firebase-firestore-ktx`, DPAPI |

---

## Target Performa & Desain

- **PC Windows (`PC.AvaloniaApp`):** `.NET 8/9+` dengan `PublishAot=true` (Native AOT) + **Avalonia UI v11+** (`Semi.Avalonia` + `Semi.Avalonia.DataGrid` + `Lucide.Avalonia`), RAM **15–25 MB** saat aktif/idle, *cold startup* `< 500 ms`, **Zero Dynamic Reflection**.
- **Android (`android/`):** Murni **Kotlin** + **Jetpack Compose** (Material 3 Expressive, `FlowRow`, `LazyColumn`, `Vico`, `ML Kit`, `Credential Manager`), berbagi skema SQLite, JSON Lego-Token AST, dan Firebase Sync yang 100% kompatibel dengan PC.
- **Konstitusi Kode & Visual:**
  - [`CLAUDE.md`](./CLAUDE.md) — Konstitusi rekayasa kode berbasis prinsip Andrej Karpathy & 10 batasan mutlak.
  - [`UI_UX_DESIGN_SYSTEM.md`](./UI_UX_DESIGN_SYSTEM.md) — Konstitusi visual (8-Point Spatial Grid, Zero-Collision Laws, 4 Super-Header & 3 Frozen Columns, palet warna semantik, wireframe 5 layar, dan panduan MCP Figma Gratis).
  - [`docs/ARCHITECTURE_CONCEPT.md`](./docs/ARCHITECTURE_CONCEPT.md) — Anatomi terkalibrasi 56 kolom `Data IP 2026.xlsx`, 5 anomali data nyata, dan arsitektur Hybrid Cloud.

---

## Ekosistem Always-Active MCP, Skills & Lifecycle Hooks

Seluruh perkakas pengembangan telah dikonfigurasi agar aktif otomatis tanpa perlu dipanggil manual:

### 1. Always-Active MCP Servers ([`.mcp.json`](./.mcp.json))
- **Free Figma & Penpot (0 Rupiah):** `figma-context-mcp` (`GLips/Figma-Context-MCP`), `talk-to-figma-mcp` (`sonnylazuardi/cursor-talk-to-figma-mcp`), `penpot-mcp`.
- **Official MCP:** `sqlite`, `filesystem`, `git`, `github`, `sequential-thinking`, `memory`.
- **Community MCP:** `context7`, `markitdown` (pembaca otomatis `.xlsx`/`.docx`/`.pdf`), `roslyn-mcp`, `firebase-mcp`, `windows-mcp`, `adb-mcp`.

### 2. Seven Auto-Invoked Agent Skills (`.claude/skills/*/SKILL.md`)
1. [`sleek-ui-figma-architect`](./.claude/skills/sleek-ui-figma-architect/SKILL.md) — `.axaml`, `@Composable`, Figma/Penpot MCP, 8pt Grid, 4 Super-Headers, 0% tabrakan UI.
2. [`aot-memory-guardian`](./.claude/skills/aot-memory-guardian/SKILL.md) — Native AOT, `< 25 MB` RAM, `x:DataType`, Source Generators, Zero Reflection.
3. [`layman-sqlite-architect`](./.claude/skills/layman-sqlite-architect/SKILL.md) — Dynamic SQLite Studio, Column Groups, Computed Columns, Linker, Smart Sanitizer, Year Cloner.
4. [`lego-token-ast-compiler`](./.claude/skills/lego-token-ast-compiler/SKILL.md) — Unlimited IKU (`1a-N`, `1b-N`, `2a-N`), Zero Hardcoded Formulas, AST $\rightarrow$ SQLite SQL.
5. [`customs-doc-compiler`](./.claude/skills/customs-doc-compiler/SKILL.md) — `Data IP 2026.xlsx`, PIB 1 Barang & *Lembar Lanjutan* Master-Detail Drawer, LHP, B/L, OCR, Auto-Merge.
6. [`siap-bot-onedrive-bridge`](./.claude/skills/siap-bot-onedrive-bridge/SKILL.md) — `SIAP-NEO`/`SIAP-DESKTOP`, `BOT-MONITOR-PERIKSA`, Humanized Macro, Zero-Copy `MoveFileExW` 5 GB+ Sorter.
7. [`immortal-cloud-sync`](./.claude/skills/immortal-cloud-sync/SKILL.md) — Google OAuth 2.0, Firebase Firestore Spark REST Sync, OneDrive `.sqlite` backup, Android sync.

### 3. Always-On Lifecycle Hooks ([`.claude/settings.json`](./.claude/settings.json))
- **`UserPromptSubmit`** ([`load-constitution.mjs`](./.claude/hooks/load-constitution.mjs)) — Memuat otomatis aturan `CLAUDE.md` & `UI_UX_DESIGN_SYSTEM.md` serta mendeteksi file referensi di `docs/references/`.
- **`PostToolUse` (`Edit|Write`)** ([`audit-post-edit.mjs`](./.claude/hooks/audit-post-edit.mjs)) — Mengaudit kepatuhan Native AOT (`x:DataType`, Zero Reflection, `[LibraryImport]`), 8-Point Spatial Grid, Zero-Collision UI, dan larangan hardcode rumus IKU.
- **`Stop`** ([`verify-stop.mjs`](./.claude/hooks/verify-stop.mjs)) — Memvalidasi integritas konfigurasi JSON serta memastikan proyek `.NET` bersih dari compile error dan warning AOT/trimming (`IL2xxx`/`IL3xxx`).

---

## Prasyarat & Persiapan Lingkungan

1. **Runtime & SDK:**
   - PC Windows: [.NET 8/9 SDK](https://dotnet.microsoft.com/) (dengan workload Desktop/Native AOT C++ Build Tools pada Windows).
   - Android: JDK 17+ & Android SDK (Gradle Kotlin DSL).
   - MCP Tooling: Node.js 20+ (`npx`), Python `uv` (`uvx`), dan opsional `dotnet tool install -g dotnet-roslyn-mcp`.
2. **Variabel Lingkungan:**
   Salin `.env.example` menjadi `.env` (otomatis di-gitignore) dan isi token yang diperlukan (`FIGMA_API_KEY`, `GITHUB_PERSONAL_ACCESS_TOKEN`, kredensial Firebase Spark & Google OAuth).
3. **Berkas Referensi Lokal (Opsional, Gitignored):**
   Letakkan `Data IP 2026.xlsx`, `Manual IKU 2026.docx`, dan sampel PDF (PIB/LHP/B/L) di [`docs/references/`](./docs/references/README.md) untuk verifikasi impor nyata tanpa risiko ter-commit ke Git.

---

## Rencana Eksekusi Bertahap (Roadmap)

- [x] **Tahap 1 — Auto-Discovery Referensi, Ekosistem Claude, Konstitusi & `README.md`:** Pemindaian dokumen referensi, `.mcp.json`, `.claude/settings.json` + 3 Lifecycle Hooks, 7 Auto-Active Skills, `CLAUDE.md`, `UI_UX_DESIGN_SYSTEM.md`, `docs/ARCHITECTURE_CONCEPT.md`, dan `README.md`.
- [ ] **Tahap 2 — Mesin Inti (`src/Core.Engine`):** No-Code Dynamic SQLite Engine, Smart `Data IP` Importer & Sanitizer, Selective Year Cloner, Lego-Token AST Transpiler, Adaptive PIB/LHP/BL Compiler, Unified Connectors (`SIAP-NEO` & `BOT-MONITOR-PERIKSA`), OneDrive 5 GB+ Zero-Copy Sorter, dan Google Auth + Firebase Sync + unit tests.
- [ ] **Tahap 3 — Aplikasi PC Windows Native AOT (`src/PC.AvaloniaApp`):** Implementasi 4 halaman kerja tahunan dengan `Semi.Avalonia`, 4 Super-Headers & 3 Frozen Columns di Halaman `DATA`, Kanvas Lego-Token 120 FPS, Bento-Grid LiveCharts2, dan Console Konektor & OneDrive.
- [ ] **Tahap 4 — Aplikasi Android Native (`android/`):** Aplikasi Kotlin + Jetpack Compose (Material 3 Expressive) dengan `FlowRow`, `Vico`, `ML Kit` OCR, Credential Manager, dan Firestore Sync.
- [ ] **Tahap 5 — Verifikasi Visual, Memori & Audit Data Otomatis:** Audit kompilasi Native AOT (`< 25 MB` RAM, 0 warning `IL2xxx`/`IL3xxx`), impor bersih 212+ baris `INPUT`, validasi 8pt Grid, dan 0% tabrakan UI.
