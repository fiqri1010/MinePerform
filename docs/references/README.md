# Reference Operational Files (Local Only — Gitignored)

Letakkan berkas referensi kerja nyata di folder ini agar dapat dipindai otomatis oleh MCP `markitdown` / `MiniExcel` pada saat pengembangan maupun pengujian lokal:

- `Data IP 2026.xlsx` (9 sheet: `INPUT`, `Jam Presensi`, `Calc Pack`, `Data Master`, `Cek IKU`, `REVIEW`, `PIVOT`, `Catatan`, `DASHBOARD`)
- `Manual IKU 2026.docx` (pedoman perhitungan IKU `1a-N`, `1b-N`, `2a-N`, dst.)
- Sampel PDF pabean: PIB BC 2.0 (1 barang & *Lembar Lanjutan*), LHP, dan B/L.

> **Keamanan Data:** Seluruh file `*.xlsx`, `*.xls`, `*.csv`, `*.docx`, `*.pdf`, `*.jpg`, `*.png` di folder ini otomatis diabaikan oleh `.gitignore` agar data operasional kepabeanan dan identitas importir tidak pernah ter-commit ke repositori Git.
