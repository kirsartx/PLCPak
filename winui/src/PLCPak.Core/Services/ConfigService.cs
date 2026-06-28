using System.Text.Json;
using System.Text.Json.Serialization;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AppPaths _paths;

    public ConfigService(AppPaths paths) => _paths = paths;

    public static CompressConfig GetDefaultConfig() => new CompressConfig().Clone();

    public CompressConfig Load()
    {
        if (!File.Exists(_paths.ConfigPath))
            return GetDefaultConfig();

        try
        {
            var json = File.ReadAllText(_paths.ConfigPath);
            var config = JsonSerializer.Deserialize<CompressConfig>(json, JsonOptions);
            return MergeDefaults(config);
        }
        catch
        {
            return GetDefaultConfig();
        }
    }

    public void Save(CompressConfig config)
    {
        var directory = Path.GetDirectoryName(_paths.ConfigPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_paths.ConfigPath, json);
    }

    public void SaveRecentProjects(IEnumerable<string> projects)
    {
        var config = Load();
        config.RecentProjects = projects
            .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
        Save(config);
    }

    private static CompressConfig MergeDefaults(CompressConfig? config)
    {
        var defaults = GetDefaultConfig();
        if (config is null)
            return defaults;

        defaults.VolumeSizeMB = config.VolumeSizeMB;
        defaults.AdScanTimeoutSec = config.AdScanTimeoutSec;
        defaults.AdConfirmThreshold = config.AdConfirmThreshold;
        defaults.UseRecycleBin = config.UseRecycleBin;
        defaults.PreviewBeforeClean = config.PreviewBeforeClean;
        defaults.ParallelHashWorkers = config.ParallelHashWorkers;
        defaults.UseScanCache = config.UseScanCache;
        defaults.HashMaxFileMB = config.HashMaxFileMB;
        defaults.CompressThreads = config.CompressThreads;
        defaults.TempDir = config.TempDir ?? string.Empty;
        defaults.SkipStoreCompress = config.SkipStoreCompress;
        defaults.ManifestStaleDays = config.ManifestStaleDays;
        defaults.Whitelist = config.Whitelist?.Count > 0 ? [..config.Whitelist] : [];
        defaults.RecentProjects = config.RecentProjects?.Count > 0 ? [..config.RecentProjects] : [];
        if (config.HashSkipExtensions?.Count > 0)
            defaults.HashSkipExtensions = [..config.HashSkipExtensions];
        if (config.AdLikeExtensions?.Count > 0)
            defaults.AdLikeExtensions = [..config.AdLikeExtensions];
        return defaults;
    }
}