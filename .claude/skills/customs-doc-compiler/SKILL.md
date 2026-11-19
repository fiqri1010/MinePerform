---
name: customs-doc-compiler
description: Automatically activates on Excel/CSV importers (Data IP 2026.xlsx 9 sheets), PDF parsers (UglyToad.PdfPig), OCR pipelines (Windows.Media.Ocr / ML Kit), Smart Messy Paste, and multi-page Indonesian customs document compilation (PIB BC 2.0, Lembar Lanjutan, LHP, B/L) into Master-Detail Row Drawer and Auto-Merge records.
---

# Adaptive Customs Document Compiler (`customs-doc-compiler`)

## 1. Three Supported Data Input Modes
1. **Mode 1 — Manual & Smart Messy Paste:**
   - Parse tab-separated (TSV), Excel clipboard, WhatsApp/chat tables, or messy pasted text directly into target columns via header fuzzy-matching and automatic sanitizer pipeline.
2. **Mode 2 — Semi-Auto Multi-Document Compiler:**
   - **Excel/CSV (`Data IP 2026.xlsx` — 9 Sheets):** Stream using `MiniExcel` (zero DOM memory bloat) and inspect via `markitdown-mcp` during development. Support all 9 sheet templates (`INPUT`, `Jam Presensi`, `Calc Pack`, `Data Master`, `Cek IKU`, `REVIEW`, `PIVOT`, `Catatan`, `DASHBOARD`) dynamically.
   - **PDF Customs Documents (`UglyToad.PdfPig`):** Page-by-page streaming extraction for **PIB BC 2.0 (Single Item & Multi-Item *Lembar Lanjutan*)**, **LHP (Laporan Hasil Pemeriksaan)**, and **B/L (Bill of Lading)**.
   - **Image OCR:** Offline native OCR using `Windows.Media.Ocr` (`Microsoft.Windows.SDK.Contracts`) on PC Windows and `com.google.mlkit:text-recognition` on Android.
3. **Mode 3 — Full-Auto Ingestion:**
   - Triggered automatically when `BOT-MONITOR-PERIKSA` downloads new PIB/LHP/B/L files or `SIAP-NEO` pushes inspection timestamps; merges fields into the matching `No. IP` row.

## 2. Multi-Page PIB BC 2.0 & *Lembar Lanjutan* -> Master-Detail Row Drawer
Customs declarations vary from 1 goods item on page 1 to dozens of items across *Lembar Lanjutan* pages:
- **Master Record (Main Table `INPUT`):**
  - Extract Header fields: `No. IP` (6-digit like `814253` or non-PIB like `ATA-003/IMPOR/2026`), `Tanggal IP`, `TPS`, `Jenis Impor`, `Importir`, `Pemilik Barang`, `PPJK`, `FCL/LCL`, container counts (`JML CONT 40F?`, `JML CONT 20F`), `JML PK`, `JUMLAH JENIS`, summary `JENIS BARANG`, `CAMPURAN?`, `HS CODE 1 JENIS` (when single item), `NILAI PABEAN`, and fiscal duties (`BM`, `BMT`, `BM KITE`, `CUKAI`, `PPN`, `PPnBM`, `PPh`, computed `TOTAL PDRI`).
- **Detail Records (`sys_row_details` -> Master-Detail Row Drawer):**
  - **`goods_item`:** Every line item from Page 1 + all *Lembar Lanjutan* pages (`item_no`, `hs_code`, `description`, `quantity`, `package_unit`, `net_weight_kg`, `gross_weight_kg`, `cif_value`).
  - **`container`:** Container numbers, sizes (`20F`/`40F`), seal numbers, and `FCL`/`LCL` flags.
  - **`document`:** Attached supporting documents (Invoice, Packing List, B/L, Certificate of Origin, LHP, SPBL) with local/OneDrive relative paths.

## 3. Auto-Merge Reconciliation (`PIB + LHP + B/L + SIAP-NEO`)
- **Primary Join Key:** Normalized `No. IP` (preserve leading zeros like `000765` and `001081` as `TEXT` — NEVER parse `No. IP` as integer!) paired with `Tanggal IP` / Year Workspace.
- **Source Priority & Non-Destructive Merge:**
  - `PIB BC 2.0`: Authoritative for `INFO DASAR` & *Lembar Lanjutan* items.
  - `B/L`: Enriches container numbers, shipper/consignee, vessel, gross weight, and package counts.
  - `LHP`: Populates `Kesimpulan LHP`, `TEMUAN?`, `Kendala Pemeriksaan`, `KET LAIN`, and `Selesai LHP`.
  - `SIAP-NEO`: Populates `Waktu PKB`, `Waktu Mulai Periksa`, `Selesai Periksa`, `Selesai LHP`, `BARCON`, `Pengurus`, `No. HP`.
  - Never silently overwrite user-edited manual cells without recording an audit trail or surfacing a merge conflict badge.

## 4. `Data IP 2026.xlsx` Smart Importer Rules
When importing `INPUT` (212+ rows, 56 columns across 4 super-headers):
1. Detect multi-row header bands (Row 1: Super-Header group names; Row 2: 56 column headers) automatically and map them to `sys_column_groups` + `sys_columns`.
2. Run **Excel Currency Sanitizer**: `" $2,880,771,505.92 "` -> `2880771505.92m`; `" $- "` -> `0m`.
3. Run **Data Hygiene Radar**:
   - Out-of-range dates (e.g., `05/11/2515`) -> preserve raw text in anomaly log, flag `⚠️`, do not crash.
   - Multi-line cells (`\n` inside `JENIS BARANG`) -> normalize display, strip `\n` in computed `Nama Folder Onedrive`, flag `⚠️`.
   - Blank numeric/inspection fields (`JML JENIS PERIKSA`) -> store `NULL`, flag soft `⚠️` if row status is `SPPB`.
4. Stream in batches of 100 rows inside a single SQLite WAL transaction so importing 212+ (or 10,000+) rows completes in `< 150 ms` with `< 2 MB` transient allocation.
