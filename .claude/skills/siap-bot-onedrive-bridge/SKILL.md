---
name: siap-bot-onedrive-bridge
description: Automatically activates on external connectors (SIAP-NEO, SIAP-DESKTOP, BOT-MONITOR-PERIKSA), OS-level humanized macros (Minimum-Jerk Bezier + Gaussian Jitter), OneDrive auto-folder generation, and 5GB+ batch photo sorting using Zero-Copy MoveFileExW and multi-threaded Channel<T>.
---

# External Connectors & OneDrive 5 GB+ Bridge (`siap-bot-onedrive-bridge`)

## 1. Dual External Connectors (Unified Abstraction Layer)
All connectors implement a clean async `IExternalConnector` interface returning typed delta records for Auto-Merge against `No. IP`:

### A. `SIAP-NEO` / `SIAP-DESKTOP` Connector
- **Transport Modes (Priority Order):**
  1. **IPC / Local Named Pipe (`\\.\pipe\siap-neo-bridge`)** or read-only local SQLite bridge (zero UI flakiness, lowest latency).
  2. **OS-Level Humanized Macro Fallback** (when direct IPC is unavailable): Uses Win32 `[LibraryImport("user32.dll")]` (`SendInput`, UI Automation) driven by:
     - **Minimum-Jerk Trajectory (5th-order polynomial)** with **Cubic Bezier** curve control points for natural pointer travel: $s(\tau) = 10\tau^3 - 15\tau^4 + 6\tau^5$.
     - **Box-Muller Gaussian Jitter** for inter-keystroke and click dwell timing (configurable safety bounds & immediate `ESC`/kill-switch abort).
- **Fields Supplied:** `Waktu PKB`, `Waktu Mulai Periksa`, `Selesai Periksa`, `Selesai LHP`, `BARCON`, `Pengurus`, `No. HP`, and live connection status telemetry for the `28px` status bar.

### B. `BOT-MONITOR-PERIKSA` Connector
- **Modes:** `Otonom` (auto-ingest & organize), `Semi-Auto` (stage & preview diff before commit), `Manual` (on-demand scan).
- **Pipeline:** Watches the configured download directory via `FileSystemWatcher` + debounced file-lock readiness check (`FileShare.None` probe) -> classifies incoming PIB, *Lembar Lanjutan*, LHP, and B/L files -> triggers `customs-doc-compiler` -> routes original files to `OneDrive/[Tahun]/[Nama Folder Onedrive]/Berkas/`.

## 2. OneDrive Auto-Folder Generator & Path Safety Guard
- **Pattern (Configurable via Computed Column / Template):**
  `{No.}. {No. IP}_{Tanggal IP (dd.MM.yyyy)}_{Importir}_{TPS}_{JENIS BARANG}`
  *Example:* `1. 814253_26.12.2025_BERDIKARI_MAL0_FROZEN BONELESS BEEF`
- **Mandatory Windows NTFS Sanitizer:**
  - Replace `/` and `\` with `-` (handles non-PIB codes like `ATA-003/IMPOR/2026` -> `ATA-003-IMPOR-2026`).
  - Strip `\r`, `\n`, `\t` (handles multi-line `JENIS BARANG` anomalies).
  - Strip forbidden NTFS characters `: * ? " < > |` and control characters (`< 0x20`).
  - Guard reserved Windows device names (`CON`, `PRN`, `AUX`, `NUL`, `COM1`..`LPT9`).
  - Trim trailing `.` and spaces; clamp folder name length safely (e.g., `<= 160` chars) and prepend `\\?\` extended-length path prefix when invoking Win32 APIs if total path approaches 240 chars.
- **Standard Subfolders:** Automatically ensures both `Berkas/` and `Foto/` exist inside each target folder.

## 3. High-Throughput 5 GB+ Batch Photo Sorter (Zero RAM Spike)
When sorting 5 GB+ of field inspection photos whose source directories are named by document number (e.g., `814253`, `001081`, `ATA-003-IMPOR-2026`):
1. **Document-Number Matcher:** Normalize source folder names and match against `INPUT.No. IP` (exact match, zero-padded 6-digit match, and sanitized `/` <-> `-` match). Unmatched folders are queued cleanly in an "Unmatched Review" list — NEVER deleted or moved blindly.
2. **Zero-Copy Win32 `MoveFileExW` (`[LibraryImport]`):**
   ```csharp
   [LibraryImport("kernel32.dll", EntryPoint = "MoveFileExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
   [return: MarshalAs(UnmanagedType.Bool)]
   internal static partial bool MoveFileEx(string lpExistingFileName, string lpNewFileName, uint dwFlags);
   ```
   - Same-volume moves execute in **O(1) instant NTFS metadata rename** (0 bytes copied, 0 RAM allocated).
   - Cross-volume moves use `MOVEFILE_COPY_ALLOWED | MOVEFILE_WRITE_THROUGH` or streaming `File.Copy` with a pooled `ArrayPool<byte>` 128 KB buffer + SHA-256/size verification before unlinking source.
3. **Bounded Multi-Threaded `Channel<T>` Pipeline:**
   - Producer enumerates files via `Directory.EnumerateFiles` (`EnumerationOptions`, lazy streaming).
   - Bounded `Channel.CreateBounded<FileMoveJob>(new BoundedChannelOptions(64) { SingleWriter = true })` feeds `2–4` worker tasks (`IProgress<BatchSortProgress>` throttled every 100 ms to UI thread so Avalonia stays 120 FPS and RAM stays `< 25 MB`).
   - Support full **CancellationToken**, **Dry-Run Preview**, **Collision Policy** (`SkipIfSameSize`, `RenameIncrement`, `Overwrite`), and an **Undo Transaction Journal** (`sys_file_move_journal`).
