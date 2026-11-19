# UI_UX_DESIGN_SYSTEM.md — Spesifikasi Visual Modern, Proporsional & Anti-Tabrakan (PC Avalonia & Android Compose)

Dokumen ini adalah **Konstitusi Visual (Single Source of Truth)** untuk seluruh antarmuka **MinePerform** agar AI maupun pengembang **TIDAK PERNAH** menebak-nebak ukuran tombol, jarak margin, warna, atau tata letak secara acak. Seluruh layar wajib mengikuti standar desain **"Sleek Linear + Airtable + Windows 11 Fluent 2"** dengan aturan proporsi matematis yang ketat, dioptimalkan untuk tabel kerja nyata berisi **56+ kolom yang terbagi dalam 4 Grup Header** (`INFO DASAR`, `DATA PEMERIKSAAN`, `PUTUSAN PFPD`, `LINK DATA`).

---

## 1. EKOSISTEM FIGMA GRATIS DARI GITHUB & UI LIBRARIES (0 RUPIAH)

Agar desain tidak dibuat secara buta, gunakan kombinasi alat gratis dari GitHub yang sudah terdaftar otomatis di [`.mcp.json`](./.mcp.json):

### A. Server MCP Figma Gratis (Cukup Akun Figma Free Tier + Personal Access Token)
1. **`figma-context-mcp` / Framelink (`npx -y figma-developer-mcp --stdio` — GitHub: `GLips/Figma-Context-MCP`):** Membaca *Auto-Layout*, jarak *padding/gap*, ukuran *font*, dan variabel warna dari file Figma komunitas gratis mana pun yang disalin (*Duplicate*) ke akun Figma gratis Anda, lalu menerjemahkannya langsung menjadi kode Avalonia XAML & Jetpack Compose yang proporsional.
2. **`talk-to-figma-mcp` (`npx -y cursor-talk-to-figma-mcp` — GitHub: `sonnylazuardi/cursor-talk-to-figma-mcp`):** Menghubungkan agen dua arah ke aplikasi Figma gratis via *WebSocket* agar wireframe halaman aplikasi dapat digambar langsung di kanvas Figma gratis Anda.
3. **`penpot-mcp` (GitHub: `penpot/penpot` + `penpot-mcp`):** Alternatif Figma yang 100% *Open-Source* & Gratis tanpa batasan fitur berbayar.

### B. Komponen UI Kit Gratis di Figma Community
- **Microsoft WinUI 3 / Fluent 2 Design Kit (Official Figma Community):** Standar proporsi aplikasi Windows modern.
- **ByteDance Semi Design System (Official Figma Community):** Memiliki pasangan library langsung di Avalonia bernama **`Semi.Avalonia`** sehingga tampilan di Figma dan hasil aplikasi `.exe` 100% identik.
- **Material 3 Design Kit (Official Google Figma Community):** Standar proporsi untuk aplikasi Android Jetpack Compose.

### C. Library Tema Sleek Siap Pakai (Native AOT & Compose)
- **PC Windows (Avalonia v11+ Native AOT):**
  - **`Semi.Avalonia` + `Semi.Avalonia.DataGrid`** (`irihi-technology/Semi.Avalonia`): Tema enterprise super modern bergaya SaaS (Notion/Airtable/Linear), mendukung *Dark Mode* & *Light Mode* instan, *Badge/Tag* token yang rapi, dan 100% mendukung **`PublishAot=true`**.
  - **`SukiUI`** (`kikipoulet/SukiUI`): Alternatif tema Avalonia dengan efek *Glassmorphism/Mica* dan animasi 120 FPS.
  - **`Lucide.Avalonia` / `Material.Icons.Avalonia`**: Ikon vektor SVG tajam di semua DPI monitor.
  - **`LiveChartsCore.SkiaSharpView.Avalonia`**: Grafik interaktif akselerasi GPU Skia.
- **Android (Kotlin Jetpack Compose):**
  - **`androidx.compose.material3`** (Material 3 Expressive) + **`FlowRow`** + **`com.patrykandpatrick.vico:compose-m3`**.

---

## 2. ATURAN PROPORSI MATEMATIS & ANTI-TABRAKAN (8-POINT SPATIAL GRID)

**LARANGAN KERAS:** Dilarang menulis angka `Margin`, `Padding`, `Spacing`, `ItemSpacing`, `ColumnSpacing`, `RowSpacing`, atau `CornerRadius` ganjil/acak (seperti `Margin="3,7,11,2"`). Semua ukuran wajib memakai kelipatan **4px / 8px** (dengan `6px` khusus jarak chip *Lego-Token* dan `1px`/`2px` khusus garis *border*/fokus):

| Token Spasi | Nilai Piksel | Penggunaan Wajib |
| :--- | :--- | :--- |
| `Space.XXS` | `4px` | Jarak antara ikon dan teks di dalam satu keping *Lego-Token* (`[ 🏷️ FCL/LCL = FCL ]`). |
| `Space.XS` | `6px` | Jarak antar keping *Lego-Token* di dalam `WrapPanel` (`ItemSpacing="6"`) / `FlowRow`. |
| `Space.SM` | `8px` | Padding dalam tombol kecil, jarak antar chip filter grup kolom, jarak baris form rapat. |
| `Space.MD` | `12px` | Padding dalam Kartu IKU (`1a-N`, `1b-N`, `2a-N`) dan jarak antar grup tombol di Toolbar. |
| `Space.LG` | `16px` | Padding utama panel, jarak antar kartu di Bento Grid Dashboard (`ColumnSpacing="16"`). |
| `Space.XL` | `24px` | Margin luar halaman utama (*Page Container Padding*). |
| `Space.2XL` | `32px` | Pemisah antar bagian besar (*Major Section Gap*). |

### Skala Tipografi & Elevasi (WCAG 2.2 AA Contrast Discipline)
- **Font Family PC:** `Inter` / `Segoe UI Variable` (UI) + `JetBrains Mono` / `Cascadia Mono` (Kode IKU, `No. IP`, HS Code, nominal `Rp`, dan ekspresi token).
- **Font Family Android:** `Roboto Flex` / `Inter` + `JetBrains Mono` (tabular numerals `tnum` untuk angka KPI & rupiah agar kolom angka selalu rata vertikal).
- **Skala Ukuran:** `11px` (Status bar & micro-badge), `12px` (Sel tabel & chip token), `13px` (Body standar & input), `14px` (Judul kartu), `18px` (Judul halaman), `24px–32px` (Angka KPI utama Bento Grid).
- **Border Radius:** `4px` (Badge/Input kompak), `6px` (Lego-Token chip & tombol kecil), `8px` (Kartu IKU & panel), `12px` (Kartu Bento Grid & Dialog).

### Hukum Anti-Tabrakan & Anti-Terhimpit (*Zero-Collision Layout Laws*)
1. **Dilarang Memakai `Canvas` atau `Width` Kaku untuk Teks & Token:** Semua deretan token, filter grup kolom, dan badge status wajib berada di dalam **`WrapPanel`** (Avalonia) atau **`FlowRow`** (Jetpack Compose).
2. **Penanganan Tabel Lebar 56+ Kolom (*Super-Header Banding + Frozen Columns + Group Jump*):**
   - Kolom identitas kiri (`No.`, `Status`, `No. IP`) **WAJIB di-freeze (`FrozenColumnCount="3"`)** agar tidak hilang saat pengguna menggulir ke kolom `DATA PEMERIKSAAN` atau `PUTUSAN PFPD`.
   - Sediakan **Pill Toggle Grup Kolom** di atas tabel (`[Semua]` `[🔵 Info Dasar (30)]` `[🟠 Data Pemeriksaan (14)]` `[🟣 Putusan PFPD (11)]` `[🟢 Link OneDrive (1)]`) untuk menyembunyikan/menampilkan grup kolom atau melompat langsung tanpa pusing menggeser *scrollbar* panjang.
3. **Perlindungan Teks Panjang & Multi-Baris (*Text Overflow & Newline Guard*):**
   - Semua sel tabel (seperti `JENIS BARANG`, `Kendala Pemeriksaan`, dan `Nama Folder Onedrive`) wajib memiliki `TextTrimming="CharacterEllipsis"` (Compose: `maxLines = 1, overflow = TextOverflow.Ellipsis`), tinggi baris seragam (`36px`), mengganti `\n` tampilan menjadi ` ↵ `, dan menampilkan teks lengkap via `ToolTip.Tip`.
4. **Penanda Anomali Data (*Inline Soft Warning Badge*):**
   - Jika terdeteksi keanehan data (seperti tanggal typo `05/11/2515` atau nama barang mengandung *line break* `\n` atau `JML JENIS PERIKSA` kosong), tampilkan ikon peringatan kuning halus `⚠️` di sudut sel tanpa merusak lebar kolom maupun tinggi baris `36px`.
5. **Responsivitas Tanpa Kliping:**
   - Sidebar navigasi (`220px`) otomatis collapse menjadi ikon (`56px`) ketika lebar jendela `< 1100px`.
   - Inspector kanan (`320px`) dapat ditutup/buka otomatis (*overlay drawer* pada layar sempit, *docked panel* pada layar lebar `>= 1366px`).

---

## 3. PALET WARNA SEMANTIK (GRUP KOLOM & KEPINGAN LEGO-TOKEN)

| Kategori Elemen | Warna Utama (Hex) | Warna Latar Chip (Alpha ~15–25%) | Contoh Penggunaan di Aplikasi |
| :--- | :--- | :--- | :--- |
| **Surface / Background** | `#0F172A` (Dark) / `#F8FAFC` (Light) | `#1E293B` (Card Dark) / `#FFFFFF` (Card Light) | Latar utama & kartu *Bento Grid* dengan border `1px` (`#334155` Dark / `#E2E8F0` Light). |
| **Text Primary / Secondary** | `#F8FAFC` / `#94A3B8` (Dark) — `#0F172A` / `#475569` (Light) | — | Memastikan kontras minimal `4.5:1` (WCAG 2.2 AA). |
| **Grup `INFO DASAR` & Token Kolom (`📦`)** | `#3B82F6` (Royal Blue) | `#1E3A8A40` (Dark) / `#DBEAFE` (Light) | Super-Header `INFO DASAR` (30 kolom) & token kolom: `[ 📦 No. IP ]`, `[ 📦 JUMLAH JENIS ]`, `[ 📦 TOTAL PDRI ]`. |
| **Grup `DATA PEMERIKSAAN` & Token Waktu (`⏱️`)** | `#F59E0B` (Amber Gold) | `#78350F40` (Dark) / `#FEF3C7` (Light) | Super-Header `DATA PEMERIKSAAN` (14 kolom) & operasi durasi: `[ ⏱️ SELISIH_MENIT(Waktu Mulai, Selesai) ]`. |
| **Grup `PUTUSAN PFPD` & Token Agregasi (`∑`)** | `#8B5CF6` (Vivid Purple) | `#4C1D9540` (Dark) / `#EDE9FE` (Light) | Super-Header `PUTUSAN PFPD` (11 kolom: `NOTUL?`, `Total Kurang Bayar`) & fungsi `SUM(...)`, `COUNT(...)`. |
| **Grup `LINK DATA` & Pin Kriteria (`🏷️`)** | `#10B981` (Emerald Green) | `#064E3B40` (Dark) / `#D1FAE5` (Light) | Super-Header `LINK DATA` (`Nama Folder Onedrive`) & filter: `[ 🏷️ FCL/LCL = FCL ]`, `[ 🏷️ TEMUAN? = TRUE ]`. |
| **Token Logika & Konversi (`🔀` / `📊`)** | `#EC4899` (Rose Pink) | `#83184340` (Dark) / `#FCE7F3` (Light) | Blok `JIKA - MAKA - LAINNYA` & `[ 📊 TABEL_KONVERSI ]`. |
| **Sub-Token Rakitan (`🧩`)** | `#06B6D4` (Cyan Teal) | `#164E6340` (Dark) / `#CFFAFE` (Light) | Rumus yang disimpan jadi 1 chip: `[ 🧩 #Batas_SLA_Kontainer ]`. |
| **Status Semantic (Success / Warning / Danger)** | `#10B981` / `#F59E0B` / `#EF4444` | Alpha 15% | Status capaian IKU, badge anomali data `⚠️`, error validasi token. |

---

## 4. WIREFRAME & TATA LETAK TIAP HALAMAN (BLUEPRINT VISUAL WAJIB)

### A. Kerangka Utama Aplikasi (*Global App Shell*)
```text
+--------------------------------------------------------------------------------------------------+
| [≡] WORKSPACE TAHUN: [ 2026 v ] [+ Salin Tahun]  |  🔍 Cari No. IP/Importir (Ctrl+K) | 🟢 Google |
+------------------+------------------------------------------------------------+------------------+
| NAVIGASI UTAMA   |  AREA KERJA UTAMA (MAIN STAGE - DINAMIS SESUAI MENU)       | PANEL INSPEKTOR  |
| (Lebar: 220px    |  (Lebar: * Mengikuti Layar, MinWidth: 640px)               | KONTEKSTUAL      |
|  Bisa Collapse   |                                                            | (Lebar: 320px    |
|  jadi 56px Ikon) |  [ Tab / Konten Halaman Aktif Ditampilkan di Sini ]        |  Muncul Otomatis |
|                  |                                                            |  Saat Klik Baris |
| 📊 Ringkasan     |                                                            |  atau Token IKU) |
| 🗂️ Data Tabel    |                                                            |                  |
| 🧩 IKU & Formula |                                                            |                  |
| 🤖 Konektor &    |                                                            |                  |
|    OneDrive      |                                                            |                  |
+------------------+------------------------------------------------------------+------------------+
| STATUS BAR (28px): ⚡ RAM: 18.4 MB (Native AOT) | 🔌 SIAP-NEO: Aktif | 📁 OneDrive: Siap | Sync OK |
+--------------------------------------------------------------------------------------------------+
```

### B. Halaman 1: `DATA` (No-Code SQLite Studio, 4 Super-Headers & Master-Detail Laci PIB)
Mendukung 1–5+ tabel bebas per tahun (`INPUT`, `Jam Presensi`, `Calc Pack`, `Data Master`, `Catatan`) dengan **3 kolom beku di kiri**, **pill lompat grup kolom**, dan **Laci Rincian Lembar Lanjutan**:
```text
+--------------------------------------------------------------------------------------------------+
| TABEL TAHUN 2026: [ 📑 INPUT (212) ] [ 🕒 Jam Presensi ] [ 📦 Calc Pack ] [ ⚙️ Master ] [ + ]    |
+--------------------------------------------------------------------------------------------------+
| [+ Baris] [📋 Tempel Cepat] [📄 Unggah PIB/LHP/BL/XLSX] [🤖 Tarik SIAP] | [🔗 Hubungkan] [+ Kolom]|
| GRUP KOLOM: [🔵 Info Dasar (30)] [🟠 Pemeriksaan (14)] [🟣 Putusan PFPD (11)] [🟢 OneDrive (1)]  |
+----+-------+--------+-------------------------------+-----------------------------+--------------+
|    FROZEN (BEKU)    | 🔵 INFO DASAR                 | 🟠 DATA PEMERIKSAAN         | 🟣 PFPD / 🟢 |
+----+-------+--------+-----------+-----+------+------+-------------+---------------+------+-------+
| No | Status| No. IP | Importir  | TPS | FCL  | Jns  | Waktu PKB   | Selesai LHP   | NOTUL| 📁 OD |
+----+-------+--------+-----------+-----+------+------+-------------+---------------+------+-------+
| ▶1 | SPPB  | 814253 | BERDIKARI | MAL0| FCL  |  1   | 02/01 08:58 | 02/01 17:02   | TIDAK| 📂Buka|
+----+-------+--------+-----------+-----+------+------+-------------+---------------+------+-------+
| ▼5 | SPPB  | 000765 | ABDI MAND.| MAL0| FCL  |  4   | 03/01 08:21 | 03/01 12:20   | TIDAK| 📂Buka|
|    +---------------------------------------------------------------------------------------------+
|    | ↳ 📂 LACI RINCIAN DOKUMEN (Dari Lembar Lanjutan PIB, LHP, B/L & Folder OneDrive)            |
|    |   [📦 4 Uraian Barang] [🚛 1 Kontainer] [📎 8 Dokumen] [📁 Folder: 5. 000765_02.01.2026_...]|
|    |   1. HS 0810.90.10 - FRESH LONGAN MERK 777 TIPE GOLD  | 378 BASKET | 4,347.00 KG            |
|    |   2. HS 0810.90.10 - FRESH LONGAN MERK 777 TIPE RED   | 696 BASKET | 8,004.00 KG            |
+----+-------+--------+-----------+-----+------+------+-------------+---------------+------+-------+
```

### C. Halaman 2: `IKU & FORMULA` (Portofolio `1a-N`, `1b-N`, `2a-N` & Kanvas Lego-Token)
```text
+---------------------------+----------------------------------------------------------------------+
| DAFTAR IKU TAHUN 2026     | KANVAS PERAKIT RUMUS LEGO-TOKEN: [ IKU 1a-N ]                        |
| [+ Tambah IKU] [📥 Salin] | Target: 82% | Konsolidasi: Last Known Value | Polarisasi: Maximize   |
+---------------------------+----------------------------------------------------------------------+
| 🟢 1a-N          [95.4%]  | 1. PALET TOKEN (Klik Sematkan di Kursor atau Drag & Drop ke Bawah):  |
| Efektivitas Periksa Fisik | [📦 INPUT.No. IP] [📦 JUMLAH JENIS] [📦 CONT 40F PERIKSA]            |
| Target: 82% | Non-Cascade | [⏱️ SELISIH_MENIT] [∑ COUNT] [∑ SUM] [🔀 JIKA-MAKA] [📊 KONVERSI]    |
+---------------------------+----------------------------------------------------------------------+
| 🟢 1b-N           [4.92]  | 2. KOTAK FORMULA UTAMA (WrapPanel - Otomatis Turun Baris & Rapi):    |
| Indeks Waktu Periksa Fisik| +------------------------------------------------------------------+ |
| Target: 4.15 | Non-Cascade| | ( ( [🧩 #Komponen_Ketepatan_Waktu]  *  0.75 )                    | |
+---------------------------+ |   + ( [📊 Konversi_Temuan( [🧩 #Rasio_Temuan_LHP] )] * 0.25 ) ) | |
| 🟢 2a-N          [98.0%]  | |                                              | <-- Kursor Aktif  | |
| Absen CEISA & Produktiv.  | +------------------------------------------------------------------+ |
| Target: 82% | Non-Cascade | [💾 Simpan Jadi Sub-Token Baru] [🧹 Bersihkan] [⚡ Uji Hitung: 95.4%]|
+---------------------------+----------------------------------------------------------------------+
| ➕ Tambah IKU Ke-4 / Ke-5 | 3. PRATINJAU AUDIT BARIS DATA: ✅ 198 Dihitung | ❌ 14 Dikecualikan  |
+---------------------------+----------------------------------------------------------------------+
```

### D. Halaman 3: `CHART & SUMMARY` (Bento-Grid Analitik, PDRI/Notul & Proyeksi Premi)
```text
+--------------------------------------------------------------------------------------------------+
| FILTER PERIODE: (•) Tahunan  ( ) Semester  ( ) Triwulan  ( ) Bulanan  ( ) Custom: [Jan] s/d [Mei]|
+------------------------+------------------------+------------------------+-----------------------+
| 🏆 NILAI KINERJA (NKP) | 📈 TOTAL DOK & TEMUAN  | 💰 TOTAL PDRI & NOTUL  | ⚡ PROYEKSI PREMI     |
|      114.85%           |   212 IP (5 Temuan)    |   Notul: 118 Dok       |    Kategori: OPTIMAL  |
|  Status: Di Atas Target|   FCL: 199 | LCL: 13   |   KB: Rp 3,84 Miliar   |    Run-Rate: 100%     |
+------------------------+------------------------+------------------------+-----------------------+
| GRAFIK TREN REALISASI VS TARGET (LiveCharts2)   | SEBARAN TPS & ALASAN MULAI > 1 JAM             |
|  100% |        o---o---o---o---o                | NCT1 (72) ███████████████ | Konfirmasi Pengurus|
|   82% |-------- target -------------------------| JICT (41) █████████       | Ada Kegiatan Lain  |
|       +---------------------------------------- | TER3 (28) ██████          | Jam Istirahat      |
|        Jan  Feb  Mar  Apr  Mei  Jun  Jul  Agu   | GSRA (26) █████           | Cuaca              |
+-------------------------------------------------+------------------------------------------------+
```

### E. Halaman 4: `KONEKTOR (SIAP-NEO & BOT) & ONEDRIVE SORTER (5GB+ FOTO)`
```text
+-------------------------------------------------+------------------------------------------------+
| 🤖 JEMBATAN KONEKTOR OTOMASI                    | 📁 PENGELOMPOK BERKAS & FOTO ONEDRIVE (5GB+)   |
+-------------------------------------------------+------------------------------------------------+
| 1. SIAP-NEO / SIAP-DESKTOP                      | Folder Induk OneDrive:                         |
|    Status: 🟢 Terhubung (IPC / Local DB / Macro)| [ C:\Users\...\OneDrive\Data_Periksa_2026  📂] |
|    [🔄 Tarik Waktu PKB, Mulai, Selesai & LHP]   | Pola Nama Folder Otomatis (Dari Kolom INPUT):  |
|                                                 | `{No}. {No.IP}_{Tgl.IP}_{Importir}_{TPS}_{Brg}`|
| 2. BOT-MONITOR-PERIKSA                          | Subfolder Otomatis: `/Berkas/` & `/Foto/`      |
|    Status: 🟢 Memantau Folder Unduhan           |                                                |
|    Mode: (•) Otonom  ( ) Semi-Auto  ( ) Manual  | [🚀 Pindahkan & Cocokkan Foto 5GB+ via No. IP] |
|    [⚙️ Atur Jeda Manusia & Aturan Macro]        | Progres: [████████████████░░] 85% (Zero-Copy)  |
+-------------------------------------------------+------------------------------------------------+
```

---

## 5. ADAPTASI ANDROID NATIVE (KOTLIN + JETPACK COMPOSE MATERIAL 3)

- **Navigasi Bawah (*NavigationBar*):** 4 tab identik (`Ringkasan`, `Data`, `IKU`, `Sync/Foto`).
- **Tabel 56 Kolom di Layar Sempit:** Mode *Compact Inspection Card* dengan pill grup warna (`🔵 Info Dasar`, `🟠 Pemeriksaan`, `🟣 Putusan PFPD`, `🟢 OneDrive`) + *Bottom Sheet Master-Detail* untuk *Lembar Lanjutan* PIB dan input cepat lapangan (`TEMUAN?`, `Kendala Pemeriksaan`, scan OCR kamera ML Kit).
- **Kanvas Lego-Token Mobile:** Menggunakan `FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalArrangement = Arrangement.spacedBy(6.dp))` + *Click & Pin at Caret* agar mudah disentuh dengan jempol (`minTouchTarget = 48.dp`).
