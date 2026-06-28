using Microsoft.VisualBasic.FileIO;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class AdCleanupService
{
    private readonly AdSampleRepository _samples;
    private readonly ScanCacheService _scanCache;

    public AdCleanupService(AdSampleRepository samples, ScanCacheService scanCache)
    {
        _samples = samples;
        _scanCache = scanCache;
    }

    public AdCleanupResult Invoke(
        string targetPath,
        CompressConfig config,
        bool previewOnly = false,
        CancellationToken cancellationToken = default) =>
        Scan(targetPath, config, previewOnly, cancellationToken);

    public AdCleanupResult Scan(
        string targetPath,
        CompressConfig config,
        bool previewOnly = false,
        CancellationToken cancellationToken = default)
    {
        var stats = new AdCleanupResult();
        var adSamples = _samples.LoadEnhanced();

        if (adSamples is null)
            return stats;

        if (config.UseScanCache && previewOnly && _scanCache.IsValid(targetPath, config))
        {
            var cached = _scanCache.GetEntry(targetPath);
            if (cached is not null)
            {
                stats.TotalScanned = cached.Scanned;
                stats.TotalMatched = cached.Matched;
                if (cached.Matched > 0)
                    stats.MatchedFiles.Add("(缓存) 上次扫描匹配项");
                return stats;
            }
        }

        ScanFolders(targetPath, adSamples, config, previewOnly, stats, cancellationToken);
        ScanFiles(targetPath, adSamples, config, previewOnly, stats, cancellationToken);

        if (!previewOnly && stats.TotalRemoved > 0)
            RemoveEmptyDirectories(targetPath, config);

        if (previewOnly && config.UseScanCache)
            _scanCache.Update(targetPath, stats);

        return stats;
    }

    private static void ScanFolders(
        string targetPath,
        AdSamples adSamples,
        CompressConfig config,
        bool previewOnly,
        AdCleanupResult stats,
        CancellationToken cancellationToken)
    {
        if (adSamples.FolderNames.Count == 0)
            return;

        try
        {
            var allDirs = Directory.EnumerateDirectories(targetPath, "*", System.IO.SearchOption.AllDirectories)
                .Select(path => new DirectoryInfo(path))
                .Where(d => (d.Attributes & FileAttributes.ReparsePoint) == 0)
                .OrderByDescending(d => d.FullName.Length)
                .ToList();

            foreach (var dir in allDirs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rel = GetRelativePath(targetPath, dir.FullName);
                if (IsWhitelisted(rel, config.Whitelist))
                    continue;

                if (!adSamples.FolderNames.ContainsKey(dir.Name.ToLowerInvariant()))
                    continue;

                stats.MatchedFiles.Add($"[文件夹] {rel}");
                stats.TotalMatched++;

                if (previewOnly)
                    continue;

                try
                {
                    var mode = RemoveItem(dir.FullName, isDirectory: true, config.UseRecycleBin);
                    stats.RemovedFiles.Add($"[文件夹] {rel} ({mode})");
                    stats.TotalRemoved++;
                }
                catch (Exception ex)
                {
                    stats.Errors.Add($"无法删除文件夹: {dir.FullName} - {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            stats.Errors.Add($"扫描文件夹失败: {ex.Message}");
        }
    }

    private static void ScanFiles(
        string targetPath,
        AdSamples adSamples,
        CompressConfig config,
        bool previewOnly,
        AdCleanupResult stats,
        CancellationToken cancellationToken)
    {
        try
        {
            var allFiles = Directory.EnumerateFiles(targetPath, "*", System.IO.SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .Where(f => (f.Attributes & FileAttributes.ReparsePoint) == 0)
                .ToList();

            stats.TotalScanned = allFiles.Count;

            var candidates = allFiles
                .Where(f => adSamples.FileNames.ContainsKey(f.Name.ToLowerInvariant()))
                .ToList();

            var workers = config.ParallelHashWorkers > 0 ? config.ParallelHashWorkers : 4;
            var hashMap = AdSampleRepository.ComputeSha256Parallel(candidates, workers, cancellationToken);

            foreach (var file in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rel = GetRelativePath(targetPath, file.FullName);
                if (IsWhitelisted(rel, config.Whitelist))
                    continue;

                if (!IsAdFile(file, adSamples, config, hashMap))
                    continue;

                stats.MatchedFiles.Add(rel);
                stats.TotalMatched++;

                if (previewOnly)
                    continue;

                try
                {
                    var mode = RemoveItem(file.FullName, isDirectory: false, config.UseRecycleBin);
                    stats.RemovedFiles.Add($"{rel} ({mode})");
                    stats.TotalRemoved++;
                }
                catch (Exception ex)
                {
                    stats.Errors.Add($"无法删除: {file.FullName} - {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            stats.Errors.Add($"扫描文件失败: {ex.Message}");
        }
    }

    private static bool IsAdFile(
        FileInfo file,
        AdSamples adSamples,
        CompressConfig config,
        IReadOnlyDictionary<string, string?> hashMap)
    {
        var name = file.Name.ToLowerInvariant();
        if (!adSamples.FileNames.ContainsKey(name))
            return false;

        var ext = file.Extension.ToLowerInvariant();
        if (config.HashSkipExtensions.Contains(ext))
            return true;

        var hash = hashMap.TryGetValue(file.FullName, out var mapped)
            ? mapped
            : AdSampleRepository.ComputeSha256(file.FullName);

        return hash is not null && adSamples.FileHashes.ContainsKey(hash);
    }

    private static void RemoveEmptyDirectories(string targetPath, CompressConfig config)
    {
        var emptyDirs = Directory.EnumerateDirectories(targetPath, "*", System.IO.SearchOption.AllDirectories)
            .Select(path => new DirectoryInfo(path))
            .Where(d => (d.Attributes & FileAttributes.ReparsePoint) == 0)
            .Where(d => !Directory.EnumerateFileSystemEntries(d.FullName).Any())
            .OrderByDescending(d => d.FullName.Length)
            .ToList();

        foreach (var dir in emptyDirs)
        {
            try
            {
                RemoveItem(dir.FullName, isDirectory: true, config.UseRecycleBin);
            }
            catch
            {
                // ignore
            }
        }
    }

    public static string RemoveItem(string path, bool isDirectory, bool useRecycleBin)
    {
        if (isDirectory)
        {
            if (useRecycleBin)
            {
                try
                {
                    FileSystem.DeleteDirectory(
                        path,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                    return "recycled";
                }
                catch
                {
                    // fall through
                }
            }

            Directory.Delete(path, recursive: true);
            return "deleted";
        }

        if (useRecycleBin)
        {
            try
            {
                FileSystem.DeleteFile(
                    path,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin);
                return "recycled";
            }
            catch
            {
                // fall through
            }
        }

        File.Delete(path);
        return "deleted";
    }

    public static bool IsWhitelisted(string relativePath, IEnumerable<string> whitelist)
    {
        var patterns = whitelist as string[] ?? whitelist.ToArray();
        if (patterns.Length == 0)
            return false;

        var rel = relativePath.Replace('/', '\\').ToLowerInvariant();
        foreach (var w in patterns)
        {
            if (string.IsNullOrWhiteSpace(w))
                continue;

            var pat = w.Replace('/', '\\').ToLowerInvariant().Trim();
            if (rel.Equals(pat, StringComparison.OrdinalIgnoreCase) || LikeMatch(rel, pat))
                return true;
        }

        return false;
    }

    private static bool LikeMatch(string input, string pattern)
    {
        if (!pattern.Contains('*') && !pattern.Contains('?'))
            return false;

        return System.Text.RegularExpressions.Regex.IsMatch(
            input,
            "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string GetRelativePath(string root, string fullPath)
    {
        var rel = Path.GetRelativePath(root, fullPath);
        return rel.Replace('\\', '/');
    }
}