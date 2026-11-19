---
name: aot-memory-guardian
description: Automatically activates on any .cs, .csproj, or .axaml file edit and .NET build/publish tasks. Enforces .NET 8/9+ Native AOT (PublishAot=true), RAM budget of 15-25 MB active/idle, cold startup under 500 ms, Zero Dynamic Reflection, compiled Avalonia x:DataType bindings, System.Text.Json Source Generators, Dapper.AOT, and [LibraryImport] P/Invoke.
---

# Native AOT & Memory Guardian (`aot-memory-guardian`)

## 1. Hard Performance & Footprint Targets
- **Working Set RAM (PC Windows):** `15 MB – 25 MB` active/idle (hard ceiling `< 25 MB` steady-state; streaming file operations must NOT cause GC spikes).
- **Cold Startup Time:** `< 500 ms` from process launch to first interactive frame.
- **Native AOT Cleanliness:** Zero `IL2xxx` (Trimming) and Zero `IL3xxx` (AOT) compiler warnings on `dotnet publish -r win-x64 -c Release`.

## 2. Mandatory `.csproj` Baseline for Native AOT
Every executable/desktop `.csproj` (`PC.AvaloniaApp`) MUST configure:
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>false</InvariantGlobalization> <!-- Required for id-ID / en-US currency & date formatting -->
  <StripSymbols>true</StripSymbols>
  <OptimizationPreference>Speed</OptimizationPreference>
  <IlcOptimizationPreference>Size</IlcOptimizationPreference>
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  <EnableAotAnalyzer>true</EnableAotAnalyzer>
  <EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
  <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  <Nullable>enable</Nullable>
  <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
</PropertyGroup>
```
Library projects (`Core.Engine`, etc.) MUST set `<IsAotCompatible>true</IsAotCompatible>` and `<IsTrimmable>true</IsTrimmable>`.

## 3. Zero Dynamic Reflection Rules
| Forbidden Pattern (Breaks AOT / Bloats RAM) | Mandatory Native AOT Replacement |
| :--- | :--- |
| `JsonSerializer.Serialize(obj)` (reflection) | `[JsonSerializable(typeof(...))] partial class AppJsonContext : JsonSerializerContext` + `AppJsonContext.Default.MyType` |
| Avalonia `{Binding Prop}` without `x:DataType` | Explicit `x:DataType="vm:MyViewModel"` on every View and `DataTemplate` |
| `[DllImport("kernel32.dll")]` | `[LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]` |
| `Activator.CreateInstance` / `Type.GetType` | Explicit switch expression factory or compile-time generic registry |
| `dynamic`, `System.Reflection.Emit`, `Expression.Compile()` | Discriminated union / AST interpreter or parameterized SQL transpiler |
| Classic reflection-based `Dapper` / `EF Core` | `Microsoft.Data.Sqlite` + `Dapper.AOT` (`[DapperAot]`) + direct `SqliteDataReader` for dynamic user tables |
| Heavy LINQ allocations in hot paths (`ToList()` chains) | `ZLinq` (zero-allocation value enumerables), `ReadOnlySpan<char>`, `CommunityToolkit.HighPerformance` (`ArrayPool<T>`, `MemoryOwner<T>`) |

## 4. Memory Discipline (< 25 MB RAM)
1. **Virtualization Mandatory:** All tables (56+ columns, 212–50,000+ rows) MUST use virtualized `DataGrid` / `ItemsRepeater` with `UniformGridLayout` or `StackLayout`. Never materialize all cells as visual controls simultaneously.
2. **Streaming I/O Only:**
   - Excel (`Data IP 2026.xlsx`): Stream rows via `MiniExcel.Query(...)` — NEVER load entire workbooks into DOM memory (`EPPlus` / `ClosedXML` are forbidden).
   - PDF (PIB/LHP/B/L): Stream page-by-page via `UglyToad.PdfPig` and dispose `PdfDocument` immediately after extraction.
   - Photos (5 GB+ OneDrive Sorter): NEVER read photo bytes into managed `byte[]` or `Bitmap` when sorting! Only read directory/file metadata and move via Win32 `MoveFileExW` (`MOVEFILE_COPY_ALLOWED | MOVEFILE_WRITE_THROUGH`) or bounded `System.Threading.Channels.Channel<T>` (`BoundedChannelOptions(64)`).
3. **MVVM Source Generation:** Use `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`) to eliminate runtime command reflection and boilerplate closures.
4. **Live RAM Telemetry:** Expose `Environment.WorkingSet` (or Win32 `GetProcessMemoryInfo` private working set) in the `28px` status bar on a low-frequency 5-second timer so regressions are immediately visible.

## 5. Verification Checklist
- [ ] Every new DTO serialized to JSON is registered on `AppJsonContext` (`[JsonSerializable]`).
- [ ] Every `.axaml` file compiles with `AvaloniaUseCompiledBindingsByDefault=true`.
- [ ] No `DllImport`, `Activator.CreateInstance`, `dynamic`, or reflection `GetProperty` calls exist.
- [ ] Large batch loops use `Span<T>`, `ZLinq`, or `IAsyncEnumerable<T>` with bounded channels.
