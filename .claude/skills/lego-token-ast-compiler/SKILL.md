---
name: lego-token-ast-compiler
description: Automatically activates when working on IKU portfolio cards (1a-N, 1b-N, 2a-N, unlimited IKU), Lego-Token formula builder canvas, AST-to-SQLite transpiler, conversion tables, sub-tokens, period filtering, or Chart & Summary Bento-Grid analytics. Strictly forbids hardcoded KPI/IKU formulas.
---

# Lego-Token AST Compiler & Unlimited IKU Engine (`lego-token-ast-compiler`)

## 1. Ironclad Rule: ZERO HARDCODED FORMULAS
**NEVER** write C# or Kotlin business logic that hardcodes how `1a-N`, `1b-N`, `2a-N`, or any future IKU is calculated (e.g., `if (code == "1a-N") return ...` is strictly forbidden).
Every KPI/IKU formula MUST be represented as a serializable **Lego-Token JSON AST**, editable visually via **Drag & Drop (120 FPS)** and **Click & Pin at Caret**, and shared identically between PC (C# Native AOT) and Android (Kotlin Compose).

## 2. Unlimited Kemenkeu IKU Portfolio Model
- **Kemenkeu Code Convention:** `{Seq}{Sub}-{Cascade}` (e.g., `1a-N`, `1b-N`, `2a-N`, `3a-CP`, `4a-CNP`), where:
  - `-N` = *Non-Cascading*
  - `-CP` = *Cascading Peta*
  - `-CNP` = *Cascading Non-Peta*
- **Unlimited Count:** Users can define 3, 5, or N IKUs per Year Workspace, each with:
  - `code`, `title`, `target_value`, `unit` (`%`, `Index`, `Dok`, `Jam`, `Rp`)
  - `consolidation_mode`: `LastKnownValue`, `Average`, `Sum`, `WeightedAverage`
  - `polarization`: `Maximize`, `Minimize`, `Stabilize`
  - `formula_ast_json`: Root Lego-Token AST expression
  - `audit_filter_ast_json`: Optional inclusion/exclusion predicate for Row Audit Preview (`✅ 198 Dihitung | ❌ 14 Dikecualikan`).

## 3. Lego-Token Type System (6 Semantic Chip Categories)
Every token is a strongly typed, AOT-serializable discriminated record (tagged `"kind"` in JSON):
1. **`ColumnRef` (`📦`, `#3B82F6`):** Reference to any dynamic table & column (`tableSlug`, `columnKey`, e.g., `INPUT.No. IP`, `INPUT.TOTAL PDRI`, `Jam Presensi.Hadir`). Supports cross-table references via `sys_table_relations`.
2. **`TimeOp` (`⏱️`, `#F59E0B`):** Duration & timestamp math: `SELISIH_MENIT(a, b)`, `SELISIH_JAM(a, b)`, `HARI_KERJA(a, b)`, `DALAM_BATAS_SLA(durasi, batas)`, respecting optional working-hours calendar from `Jam Presensi` / `Calc Pack`.
3. **`Aggregate` (`∑`, `#8B5CF6`):** `COUNT`, `COUNT_DISTINCT`, `SUM`, `AVG`, `MIN`, `MAX`, `PERCENTILE`, `PERCENT_WHERE(predicate)` with optional attached `FilterPin` scope.
4. **`FilterPin` (`🏷️`, `#10B981`):** Composable predicate chip (`FCL/LCL = FCL`, `TEMUAN? = TRUE`, `TK PERIKSA IN (10%, 30%)`, `NOTUL? = YA`, `Status = SPPB`) attachable to aggregates or sub-queries.
5. **`LogicAndConversion` (`🔀` / `📊`, `#EC4899`):**
   - Conditional block: `JIKA(cond) MAKA(thenExpr) LAINNYA(elseExpr)`
   - Conversion lookup table (`📊 TABEL_KONVERSI`): Ordered range-to-score table stored as JSON (e.g., `% Temuan` -> index score `0..5` or `0..120%`), completely user-editable.
6. **`SubTokenRef` (`🧩`, `#06B6D4`):** Reusable named formula fragment saved by the user (e.g., `#Komponen_Ketepatan_Waktu`, `#Batas_SLA_Kontainer`, `#Rasio_Temuan_LHP`) — expanded recursively with cycle detection before compilation.

Plus primitive tokens: `LiteralNumber`, `LiteralText`, `LiteralBool`, `BinaryOp` (`+`, `-`, `*`, `/`, `=`, `!=`, `<`, `<=`, `>`, `>=`, `AND`, `OR`), `GroupParen` (`(`, `)`).

## 4. Compilation Pipeline: Lego-Token AST -> Parameterized SQLite SQL
1. **Expand Sub-Tokens (`🧩`):** Inline referenced `SubTokenRef` nodes; reject circular references with a friendly Indonesian validation message.
2. **Validate & Type-Check:** Verify referenced tables/columns exist in the active Year Workspace metadata (`sys_tables` / `sys_columns`); produce exact token index errors for inline red underline in the canvas.
3. **Inject Active Period Filter:** Automatically scope table scans to the selected period (`Tahunan`, `Semester`, `Triwulan`, `Bulanan`, or `Custom [Start..End]`) on the table's designated date column (e.g., `Tanggal IP`).
4. **Transpile to Safe SQLite SQL:** Emit parameterized SQLite queries (`CASE WHEN`, `julianday(...) * 1440.0` for minute diffs, `COALESCE`, `NULLIF(denom, 0)` divide-by-zero protection) — NEVER interpolate raw user strings into SQL.
5. **Row-Level Audit Trail:** Return both the scalar KPI result AND row-level classification (`includedRowIds`, `excludedRowIds`, exclusion reasons) so the bottom panel displays `✅ N Dihitung | ❌ M Dikecualikan` transparently.

## 5. `CHART & SUMMARY` Bento-Grid Analytics Integration
Feed the same Period Filter and SQLite engine into the Bento-Grid analytics cards:
- **NKP (Nilai Kinerja Pegawai)** weighted rollup across active IKUs (`1a-N`, `1b-N`, `2a-N`, ...).
- **Document & Findings Metrics:** Total IP, `FCL` vs `LCL` split, `TEMUAN?` count & ratio.
- **Fiscal / PFPD Metrics:** `NOTUL?` count, `TOTAL PDRI`, `Total Kurang Bayar` (summed cleanly from sanitized decimals).
- **Operational Bottlenecks:** `TPS` distribution (`NCT1`, `JICT`, `TER3`, `GSRA`, `MAL0`, ...) and `Alasan Mulai >1 JAM` Pareto breakdown.
- **Premi / Incentive Run-Rate Projection:** Dynamic user-configurable tier formula (using the same Lego-Token / conversion table engine) to project end-of-period category (`OPTIMAL`, etc.).
