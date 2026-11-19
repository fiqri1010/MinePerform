---
name: sleek-ui-figma-architect
description: Automatically activates on any .axaml, Avalonia UI, Jetpack Compose (@Composable), Figma MCP (figma-context-mcp, talk-to-figma-mcp), or Penpot MCP task. Enforces UI_UX_DESIGN_SYSTEM.md, Semi.Avalonia theming, 8-Point Spatial Grid (4/6/8/12/16/24/32px), 4 Super-Header column bands + 3 Frozen Columns for 56-column wide tables, WrapPanel/FlowRow Lego-Token layouts, and 0% UI collision.
---

# Sleek UI & Figma Architect (`sleek-ui-figma-architect`)

## 1. Mandatory Source of Truth
Before writing or modifying **any** `.axaml` or `@Composable` file, consult `UI_UX_DESIGN_SYSTEM.md` at the repository root. Never invent ad-hoc spacing, colors, or fixed pixel layouts.

## 2. Design-to-Code Workflow (Free MCP Tools — 0 Rupiah)
1. **Figma Read (`figma-context-mcp`):** When a Figma Community URL/node (Fluent 2, Semi Design, or Material 3) is provided, extract exact Auto-Layout padding, gap, typography scale, corner radius, and color tokens before writing XAML/Compose.
2. **Figma Wireframe Write (`talk-to-figma-mcp`):** Push page wireframes to the user's free Figma canvas via WebSocket when designing new views or iterating on layout structure.
3. **Open-Source Alternative (`penpot-mcp`):** Use Penpot MCP identically when working from Penpot design files.
4. **Theme Mapping:**
   - **PC Windows (Avalonia v11+ Native AOT):** `Semi.Avalonia`, `Semi.Avalonia.DataGrid`, `Lucide.Avalonia`, `Avalonia.Controls.ItemsRepeater`, `LiveChartsCore.SkiaSharpView.Avalonia`.
   - **Android (Kotlin Jetpack Compose):** `androidx.compose.material3` (Material 3 Expressive), `androidx.compose.foundation.layout.FlowRow`, `com.patrykandpatrick.vico:compose-m3`.

## 3. 8-Point Spatial Grid (Zero Off-Grid Values)
Every `Margin`, `Padding`, `Spacing`, `ItemSpacing`, `ColumnSpacing`, `RowSpacing`, and `CornerRadius` MUST use these tokens:
- `4px` (`Space.XXS`): Icon-to-label gap inside a single Lego-Token chip.
- `6px` (`Space.XS`): Gap between Lego-Token chips inside `WrapPanel` / `FlowRow`.
- `8px` (`Space.SM`): Compact button padding, column-group filter pill gap, dense form row gap.
- `12px` (`Space.MD`): IKU card (`1a-N`, `1b-N`, `2a-N`) internal padding, toolbar button group gap.
- `16px` (`Space.LG`): Primary panel padding, Bento Grid dashboard card gap (`ColumnSpacing="16"`).
- `24px` (`Space.XL`): Outer page container margin.
- `32px` (`Space.2XL`): Major section breaks.
- `1px` / `2px`: Strictly reserved for hairline borders (`#334155` dark / `#E2E8F0` light) and focus rings.

## 4. Zero-Collision Layout Laws
1. **No `<Canvas>` or Rigid Text Widths:** Never position text, badges, filter pills, or Lego-Tokens inside `<Canvas>` or fixed-width horizontal rows that clip. Always use `WrapPanel` (Avalonia) or `FlowRow` (Compose) with `ItemSpacing="6"`.
2. **Wide 56+ Column Table Architecture (`DATA` Page — `INPUT` Blueprint):**
   - **Frozen Columns:** Pin the left 3 identity columns (`No.`, `Status`, `No. IP`) with `FrozenColumnCount="3"`.
   - **4 Semantic Super-Header Bands:**
     - `INFO DASAR` (30 cols) — `#3B82F6` (Royal Blue) / chip bg `#1E3A8A40`
     - `DATA PEMERIKSAAN` (14 cols) — `#F59E0B` (Amber Gold) / chip bg `#78350F40`
     - `PUTUSAN PFPD` (11 cols) — `#8B5CF6` (Vivid Purple) / chip bg `#4C1D9540`
     - `LINK DATA` (1 col: `Nama Folder Onedrive`) — `#10B981` (Emerald Green) / chip bg `#064E3B40`
   - **Column-Group Quick-Jump & Toggle Pills:** Always render interactive group pills (`[Semua]` `[🔵 Info Dasar (30)]` `[🟠 Data Pemeriksaan (14)]` `[🟣 Putusan PFPD (11)]` `[🟢 Link OneDrive (1)]`) above the grid so operators never fight long horizontal scrollbars.
   - **Master-Detail Row Drawer:** Expandable inline row detail for multi-item PIB *Lembar Lanjutan*, LHP, B/L attachments, and OneDrive folder preview.
3. **Text Overflow & Multi-Line Protection:**
   - Every DataGrid cell (`JENIS BARANG`, `Kendala Pemeriksaan`, `Nama Folder Onedrive`, etc.) MUST enforce uniform row height (`36px`), `TextTrimming="CharacterEllipsis"` (Compose: `maxLines = 1, overflow = TextOverflow.Ellipsis`), sanitize display newlines (`\n` -> ` ↵ `), and expose the complete untruncated string in `ToolTip.Tip`.
4. **Data Hygiene Radar Badge:**
   - Render anomalies (e.g., year typo `05/11/2515`, missing `JML JENIS PERIKSA`, embedded `\n`) as a subtle corner warning icon (`⚠️`) with diagnostic tooltip — never expand row height or shift column widths.

## 5. Semantic Palette & Global Shell Proportions
- **App Shell:** Collapsible left nav rail (`220px` expanded / `56px` icon-only), dynamic main stage (`*`, `MinWidth="640"`), contextual right inspector (`320px`, auto-opens on row/token selection), bottom telemetry status bar (`28px`: RAM MB, SIAP-NEO state, OneDrive state, Firebase Sync state).
- **Lego-Token Colors:**
  - Column (`📦`): `#3B82F6` / `#1E3A8A40`
  - Time Op (`⏱️`): `#F59E0B` / `#78350F40`
  - Aggregation (`∑`): `#8B5CF6` / `#4C1D9540`
  - Filter Pin (`🏷️`): `#10B981` / `#064E3B40`
  - Logic & Conversion (`🔀` / `📊`): `#EC4899` / `#83184340`
  - Saved Sub-Token (`🧩`): `#06B6D4` / `#164E6340`

## 6. Verification Checklist Before Completing UI Edits
- [ ] Every `.axaml` UserControl, Window, and `DataTemplate` declares explicit `x:DataType`.
- [ ] Zero odd spacing numbers (`3, 5, 7, 9, 11...`) in XAML or Compose.
- [ ] All chip/token containers use `WrapPanel` or `FlowRow`.
- [ ] Both Light (`#F8FAFC` / `#FFFFFF`) and Dark (`#0F172A` / `#1E293B`) themes maintain WCAG AA contrast.
- [ ] Wide tables verify `FrozenColumnCount="3"`, 4 Super-Header bands, `36px` row height, and `CharacterEllipsis` + tooltip.
