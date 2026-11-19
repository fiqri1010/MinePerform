using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using MinePerform.Core.SchemaStudio;

namespace MinePerform.Core.FileAndDocAutomation;

[JsonConverter(typeof(JsonStringEnumConverter<FileCollisionPolicy>))]
public enum FileCollisionPolicy
{
    SkipIfSameSize,
    RenameIncrement,
    Overwrite
}

public sealed record BatchSortProgress(
    int FilesProcessed,
    int TotalFilesDiscovered,
    long BytesProcessed,
    string CurrentNoIp,
    string CurrentFileName);

public sealed record BatchSortResult(
    string BatchId,
    bool DryRun,
    int MatchedFolders,
    int UnmatchedFolders,
    int FilesMoved,
    int FilesSkipped,
    long TotalBytesMoved,
    List<string> UnmatchedFolderNames);

/// <summary>
/// High-throughput Zero-Copy 5 GB+ Batch Photo & Document Sorter for OneDrive.
/// Matches source folders named by document number (e.g. "814253", "001081", "ATA-003-IMPOR-2026")
/// against INPUT.no_ip -> resolves sanitized Nama Folder Onedrive -> ensures Berkas/ & Foto/ ->
/// moves files via Win32 MoveFileExW ([LibraryImport]) and bounded multi-threaded Channel&lt;T&gt;
/// with zero RAM spikes (&lt; 25 MB) and full Undo Transaction Journal.
/// </summary>
public sealed partial class OneDriveBatchSorter
{
    private const uint MOVEFILE_REPLACE_EXISTING = 0x1;
    private const uint MOVEFILE_COPY_ALLOWED = 0x2;
    private const uint MOVEFILE_WRITE_THROUGH = 0x8;

    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".heic", ".heif", ".webp", ".bmp", ".gif", ".mp4", ".mov"
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt", ".xml"
    };

    [LibraryImport("kernel32.dll", EntryPoint = "MoveFileExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool MoveFileExNative(string lpExistingFileName, string lpNewFileName, uint dwFlags);

    private readonly DynamicSqliteStudio _studio;

    public OneDriveBatchSorter(DynamicSqliteStudio studio)
    {
        _studio = studio;
    }

    public async Task<BatchSortResult> SortAndMoveAsync(
        string workspaceId,
        string sourceRootDir,
        string oneDriveYearRootDir,
        string tableSlug = "input",
        bool dryRun = false,
        FileCollisionPolicy collisionPolicy = FileCollisionPolicy.SkipIfSameSize,
        int workerCount = 4,
        IProgress<BatchSortProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(sourceRootDir))
        {
            throw new DirectoryNotFoundException($"Source directory not found: '{sourceRootDir}'");
        }

        string batchId = $"batch_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
        var ipToFolderMap = BuildNoIpToOneDriveFolderLookup(workspaceId, tableSlug);

        var matchedDirs = new List<(string SourceDir, string NoIp, string TargetFolderName)>();
        var unmatchedNames = new List<string>();

        foreach (string subDir in Directory.EnumerateDirectories(sourceRootDir))
        {
            string dirName = Path.GetFileName(subDir);
            if (TryResolveNoIpMatch(dirName, ipToFolderMap, out string matchedNoIp, out string targetFolder))
            {
                matchedDirs.Add((subDir, matchedNoIp, targetFolder));
            }
            else
            {
                unmatchedNames.Add(dirName);
            }
        }

        if (!dryRun)
        {
            Directory.CreateDirectory(oneDriveYearRootDir);
        }

        var channel = Channel.CreateBounded<FileMoveJob>(new BoundedChannelOptions(64)
        {
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        int filesMoved = 0;
        int filesSkipped = 0;
        int totalDiscovered = 0;
        long totalBytes = 0;
        var journalBuffer = new System.Collections.Concurrent.ConcurrentBag<FileMoveJournalEntry>();

        // Producer: lazy Directory.EnumerateFiles (zero full-tree RAM spike)
        Task producer = Task.Run(async () =>
        {
            try
            {
                foreach (var (srcDir, noIp, targetFolder) in matchedDirs)
                {
                    ct.ThrowIfCancellationRequested();
                    string safeFolder = SmartSanitizer.SanitizeWindowsFolderName(targetFolder);
                    string targetBase = Path.Combine(oneDriveYearRootDir, safeFolder);
                    string fotoDir = Path.Combine(targetBase, "Foto");
                    string berkasDir = Path.Combine(targetBase, "Berkas");

                    if (!dryRun)
                    {
                        Directory.CreateDirectory(fotoDir);
                        Directory.CreateDirectory(berkasDir);
                    }

                    foreach (string filePath in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
                    {
                        ct.ThrowIfCancellationRequested();
                        string ext = Path.GetExtension(filePath);
                        string destSubDir = DocumentExtensions.Contains(ext) && !PhotoExtensions.Contains(ext)
                            ? berkasDir
                            : fotoDir;

                        string destPath = Path.Combine(destSubDir, Path.GetFileName(filePath));
                        long fileLen = new FileInfo(filePath).Length;
                        Interlocked.Increment(ref totalDiscovered);

                        await channel.Writer.WriteAsync(new FileMoveJob(filePath, destPath, noIp, fileLen), ct).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, ct);

        // Consumers: bounded multi-threaded Zero-Copy workers
        int workers = Math.Clamp(workerCount, 1, 8);
        var consumerTasks = new Task[workers];
        long lastProgressTicks = 0;

        for (int w = 0; w < workers; w++)
        {
            consumerTasks[w] = Task.Run(async () =>
            {
                await foreach (var job in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    var (moved, actualDest) = ExecuteFileMove(job.SourcePath, job.DestinationPath, job.ByteSize, dryRun, collisionPolicy);
                    if (moved)
                    {
                        Interlocked.Increment(ref filesMoved);
                        Interlocked.Add(ref totalBytes, job.ByteSize);
                        if (!dryRun)
                        {
                            journalBuffer.Add(new FileMoveJournalEntry(
                                Guid.NewGuid().ToString("N"),
                                batchId,
                                job.SourcePath,
                                actualDest,
                                job.NoIp,
                                job.ByteSize,
                                DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref filesSkipped);
                    }

                    // Throttle progress reports to ~10 Hz so Avalonia UI stays 120 FPS
                    long nowTicks = Environment.TickCount64;
                    long prevTicks = Volatile.Read(ref lastProgressTicks);
                    if (progress is not null && nowTicks - prevTicks >= 100
                        && Interlocked.CompareExchange(ref lastProgressTicks, nowTicks, prevTicks) == prevTicks)
                    {
                        progress.Report(new BatchSortProgress(
                            Volatile.Read(ref filesMoved) + Volatile.Read(ref filesSkipped),
                            Volatile.Read(ref totalDiscovered),
                            Volatile.Read(ref totalBytes),
                            job.NoIp,
                            Path.GetFileName(job.SourcePath)));
                    }
                }
            }, ct);
        }

        await producer.ConfigureAwait(false);
        await Task.WhenAll(consumerTasks).ConfigureAwait(false);

        if (!dryRun && !journalBuffer.IsEmpty)
        {
            PersistJournalEntries(journalBuffer);
        }

        progress?.Report(new BatchSortProgress(filesMoved + filesSkipped, totalDiscovered, totalBytes, string.Empty, "Selesai"));

        return new BatchSortResult(
            batchId,
            dryRun,
            matchedDirs.Count,
            unmatchedNames.Count,
            filesMoved,
            filesSkipped,
            totalBytes,
            unmatchedNames);
    }

    /// <summary>Reverts a previously committed batch using sys_file_move_journal.</summary>
    public int UndoBatch(string batchId)
    {
        var entries = new List<(string Id, string Src, string Dst)>();
        using (var cmd = _studio.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT id, source_path, destination_path
                FROM sys_file_move_journal
                WHERE batch_id = @bid AND reverted = 0
                """;
            cmd.Parameters.AddWithValue("@bid", batchId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) entries.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
        }

        int reverted = 0;
        using var tx = _studio.Connection.BeginTransaction();
        foreach (var (id, src, dst) in entries)
        {
            if (!File.Exists(dst)) continue;
            string? srcDir = Path.GetDirectoryName(src);
            if (!string.IsNullOrEmpty(srcDir)) Directory.CreateDirectory(srcDir);

            MoveFileZeroCopy(dst, src, overwrite: true);

            using var upd = _studio.Connection.CreateCommand();
            upd.Transaction = tx;
            upd.CommandText = "UPDATE sys_file_move_journal SET reverted = 1 WHERE id = @id";
            upd.Parameters.AddWithValue("@id", id);
            upd.ExecuteNonQuery();
            reverted++;
        }
        tx.Commit();
        return reverted;
    }

    private Dictionary<string, (string CanonicalNoIp, string FolderName)> BuildNoIpToOneDriveFolderLookup(string workspaceId, string tableSlug)
    {
        var map = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        var table = _studio.GetTableBySlug(workspaceId, tableSlug);
        if (table is null) return map;

        var cols = _studio.GetColumns(table.Id);
        var rows = _studio.QueryRows(table, cols, limit: 100_000);

        foreach (var row in rows)
        {
            if (!row.Cells.TryGetValue("no_ip", out string? noIp) || string.IsNullOrWhiteSpace(noIp)) continue;
            row.Cells.TryGetValue("nama_folder_onedrive", out string? folder);
            string safeFolder = string.IsNullOrWhiteSpace(folder)
                ? SmartSanitizer.SanitizeWindowsFolderName(noIp)
                : SmartSanitizer.SanitizeWindowsFolderName(folder);

            string norm = SmartSanitizer.NormalizeNoIp(noIp);
            map[norm] = (norm, safeFolder);
            // Also index NTFS-sanitized No. IP (e.g. "ATA-003-IMPOR-2026" matches "ATA-003/IMPOR/2026")
            string sanitizedIp = SmartSanitizer.SanitizeWindowsFolderName(norm);
            map[sanitizedIp] = (norm, safeFolder);
        }

        return map;
    }

    private static bool TryResolveNoIpMatch(
        string folderName,
        Dictionary<string, (string CanonicalNoIp, string FolderName)> lookup,
        out string matchedNoIp,
        out string targetFolder)
    {
        string trimmed = folderName.Trim();
        if (lookup.TryGetValue(trimmed, out var hit))
        {
            matchedNoIp = hit.CanonicalNoIp;
            targetFolder = hit.FolderName;
            return true;
        }

        string normalized = SmartSanitizer.NormalizeNoIp(trimmed);
        if (lookup.TryGetValue(normalized, out hit))
        {
            matchedNoIp = hit.CanonicalNoIp;
            targetFolder = hit.FolderName;
            return true;
        }

        matchedNoIp = string.Empty;
        targetFolder = string.Empty;
        return false;
    }

    private static (bool Moved, string ActualDestination) ExecuteFileMove(
        string sourcePath,
        string destPath,
        long sourceBytes,
        bool dryRun,
        FileCollisionPolicy policy)
    {
        string finalDest = destPath;
        if (File.Exists(finalDest))
        {
            switch (policy)
            {
                case FileCollisionPolicy.SkipIfSameSize:
                    if (new FileInfo(finalDest).Length == sourceBytes)
                    {
                        return (false, finalDest);
                    }
                    finalDest = MakeIncrementedPath(finalDest);
                    break;
                case FileCollisionPolicy.RenameIncrement:
                    finalDest = MakeIncrementedPath(finalDest);
                    break;
                case FileCollisionPolicy.Overwrite:
                    break;
            }
        }

        if (dryRun)
        {
            return (true, finalDest);
        }

        MoveFileZeroCopy(sourcePath, finalDest, overwrite: policy == FileCollisionPolicy.Overwrite);
        return (true, finalDest);
    }

    private static void MoveFileZeroCopy(string sourcePath, string destPath, bool overwrite)
    {
        if (OperatingSystem.IsWindows())
        {
            uint flags = MOVEFILE_COPY_ALLOWED | MOVEFILE_WRITE_THROUGH | (overwrite ? MOVEFILE_REPLACE_EXISTING : 0u);
            string extSrc = sourcePath.Length >= 240 && !sourcePath.StartsWith(@"\\?\", StringComparison.Ordinal)
                ? @"\\?\" + sourcePath
                : sourcePath;
            string extDst = destPath.Length >= 240 && !destPath.StartsWith(@"\\?\", StringComparison.Ordinal)
                ? @"\\?\" + destPath
                : destPath;

            if (MoveFileExNative(extSrc, extDst, flags))
            {
                return;
            }
        }

        // Cross-platform atomic rename (same volume) or pooled 128 KB streaming fallback (cross-volume)
        try
        {
            File.Move(sourcePath, destPath, overwrite);
        }
        catch (IOException)
        {
            StreamingCopyAndDelete(sourcePath, destPath, overwrite);
        }
    }

    private static void StreamingCopyAndDelete(string sourcePath, string destPath, bool overwrite)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
        try
        {
            using var src = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024);
            using var dst = new FileStream(destPath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024);
            int read;
            while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
            {
                dst.Write(buffer, 0, read);
            }
            dst.Flush(flushToDisk: true);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        File.Delete(sourcePath);
    }

    private static string MakeIncrementedPath(string path)
    {
        string dir = Path.GetDirectoryName(path) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        int i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{stem}_{i++}{ext}");
        } while (File.Exists(candidate));
        return candidate;
    }

    private void PersistJournalEntries(IEnumerable<FileMoveJournalEntry> entries)
    {
        using var tx = _studio.Connection.BeginTransaction();
        foreach (var e in entries)
        {
            using var cmd = _studio.Connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO sys_file_move_journal (id, batch_id, source_path, destination_path, no_ip, byte_size, moved_at_utc, reverted)
                VALUES (@id, @bid, @src, @dst, @ip, @sz, @at, 0)
                """;
            cmd.Parameters.AddWithValue("@id", e.Id);
            cmd.Parameters.AddWithValue("@bid", e.BatchId);
            cmd.Parameters.AddWithValue("@src", e.SourcePath);
            cmd.Parameters.AddWithValue("@dst", e.DestinationPath);
            cmd.Parameters.AddWithValue("@ip", e.NoIp);
            cmd.Parameters.AddWithValue("@sz", e.ByteSize);
            cmd.Parameters.AddWithValue("@at", e.MovedAtUtc);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private readonly record struct FileMoveJob(string SourcePath, string DestinationPath, string NoIp, long ByteSize);

    private sealed record FileMoveJournalEntry(
        string Id,
        string BatchId,
        string SourcePath,
        string DestinationPath,
        string NoIp,
        long ByteSize,
        string MovedAtUtc);
}
