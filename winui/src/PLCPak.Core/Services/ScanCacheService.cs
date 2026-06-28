using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class ScanCacheService
{
    private readonly AppPaths _paths;

    public ScanCacheService(AppPaths paths) => _paths = paths;

    public bool IsValid(string targetPath, CompressConfig config)
    {
        if (!config.UseScanCache)
            return false;

        if (!File.Exists(_paths.ScanCachePath))
            return false;

        try
        {
            var cache = JsonHelper.ReadFile<ScanCache>(_paths.ScanCachePath);
            var entry = cache?.Entries.FirstOrDefault(e =>
                string.Equals(e.Path, targetPath, StringComparison.OrdinalIgnoreCase));

            if (entry is null || !Directory.Exists(targetPath))
                return false;

            var dir = new DirectoryInfo(targetPath);
            return entry.Mtime == dir.LastWriteTimeUtc.Ticks;
        }
        catch
        {
            return false;
        }
    }

    public ScanCacheEntry? GetEntry(string targetPath)
    {
        if (!File.Exists(_paths.ScanCachePath))
            return null;

        try
        {
            var cache = JsonHelper.ReadFile<ScanCache>(_paths.ScanCachePath);
            return cache?.Entries.FirstOrDefault(e =>
                string.Equals(e.Path, targetPath, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    public void Update(string targetPath, AdCleanupResult stats)
    {
        var entries = new List<ScanCacheEntry>();

        if (File.Exists(_paths.ScanCachePath))
        {
            try
            {
                var old = JsonHelper.ReadFile<ScanCache>(_paths.ScanCachePath);
                if (old?.Entries is not null)
                {
                    entries.AddRange(old.Entries.Where(e =>
                        !string.Equals(e.Path, targetPath, StringComparison.OrdinalIgnoreCase)));
                }
            }
            catch
            {
                // ignore
            }
        }

        var dir = new DirectoryInfo(targetPath);
        entries.Add(new ScanCacheEntry
        {
            Path = targetPath,
            Mtime = dir.LastWriteTimeUtc.Ticks,
            Scanned = stats.TotalScanned,
            Matched = stats.TotalMatched,
            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });

        JsonHelper.WriteFile(_paths.ScanCachePath, new ScanCache
        {
            Version = 1,
            Entries = entries
        });
    }
}