using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class ManifestService
{
    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "README.txt", "示例说明.txt", "ad-manifest.json", ".keep", ".DS_Store",
        "Remove-AdFiles.ps1", "AdSampleExtension.ps1", "文件压缩工具.ps1",
        "PLCPak.ps1", "PLCPak_v1.7.4.ps1", "更新广告清单.ps1"
    };

    private static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ps1", ".bat", ".cmd", ".vbs"
    };

    private readonly AppPaths _paths;

    public ManifestService(AppPaths paths) => _paths = paths;

    public AdManifest Read()
    {
        var manifestPath = _paths.AdManifestPath;
        if (!File.Exists(manifestPath))
        {
            return new AdManifest
            {
                Version = 1,
                Description = "广告样本哈希清单",
                FileCount = 0,
                Folders = [],
                Files = []
            };
        }

        var manifest = JsonHelper.ReadFile<AdManifest>(manifestPath) ?? new AdManifest();
        manifest.Folders ??= [];
        manifest.Files ??= [];
        return manifest;
    }

    public void Save(AdManifest manifest)
    {
        var fileList = manifest.Files
            .OrderByDescending(f => f.Size)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var folderList = manifest.Folders
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var output = new AdManifest
        {
            Version = manifest.Version,
            Description = manifest.Description,
            GeneratedFrom = string.IsNullOrWhiteSpace(manifest.GeneratedFrom) ? "手动更新" : manifest.GeneratedFrom,
            FileCount = fileList.Count,
            Folders = folderList,
            Files = fileList,
            LastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        Directory.CreateDirectory(_paths.AdSamplesRoot);
        JsonHelper.WriteFile(_paths.AdManifestPath, output, utf8Bom: true);
    }

    public bool TestManifestStale(int staleDays)
    {
        if (!File.Exists(_paths.AdManifestPath))
            return false;

        try
        {
            var manifest = Read();
            if (string.IsNullOrWhiteSpace(manifest.LastUpdated))
                return false;

            if (DateTime.TryParse(manifest.LastUpdated, out var dt))
                return (DateTime.Now - dt).TotalDays > staleDays;
        }
        catch
        {
            // ignore
        }

        return false;
    }

    public ImportSampleStats ImportPaths(
        IEnumerable<string> paths,
        bool autoPrune = false,
        int thresholdKb = 50,
        Action<string>? onLog = null)
    {
        var stats = new ImportSampleStats();
        var manifest = Read();
        var existingByKey = BuildExistingIndex(manifest);
        var folderSet = BuildFolderSet(manifest);
        var threshold = thresholdKb * 1024L;
        var adRoot = _paths.ResolveAdSamplesRoot();
        var imported = new List<string>();

        foreach (var inputPath in paths)
        {
            if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
            {
                stats.Errors.Add($"路径不存在: {inputPath}");
                continue;
            }

            if (Directory.Exists(inputPath))
            {
                AddFolderName(folderSet, Path.GetFileName(inputPath));
                var destDir = Path.Combine(adRoot, Path.GetFileName(inputPath));
                Directory.CreateDirectory(destDir);

                foreach (var src in Directory.EnumerateFiles(inputPath, "*", SearchOption.AllDirectories))
                {
                    var srcInfo = new FileInfo(src);
                    if (ShouldSkip(srcInfo.Name, srcInfo.Extension))
                        continue;

                    var rel = Path.GetRelativePath(inputPath, src);
                    var dest = Path.Combine(destDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(src, dest, overwrite: true);
                    imported.Add(dest);
                    stats.Copied++;
                }
            }
            else
            {
                var leaf = Path.GetFileName(inputPath);
                if (ShouldSkip(leaf, Path.GetExtension(leaf)))
                    continue;

                var dest = Path.Combine(adRoot, leaf);
                File.Copy(inputPath, dest, overwrite: true);
                imported.Add(dest);
                stats.Copied++;
            }
        }

        foreach (var filePath in imported)
        {
            var file = new FileInfo(filePath);
            var result = RegisterSampleFile(file, manifest, existingByKey, folderSet, adRoot);

            if (result.Duplicate)
            {
                stats.Skipped++;
                continue;
            }

            stats.Added += result.Added;
            stats.Updated += result.Updated;

            if (result.Added > 0)
                onLog?.Invoke($"[样本+] {file.Name}");

            if (autoPrune && file.Length > threshold)
            {
                try
                {
                    File.Delete(file.FullName);
                    stats.Pruned++;
                    onLog?.Invoke($"[样本-] 已精简: {file.Name}");
                }
                catch
                {
                    // ignore
                }
            }
        }

        manifest.Folders = folderSet.Keys.ToList();
        Save(manifest);
        return stats;
    }

    public ImportSampleStats SyncFromFolder(
        bool autoPrune = false,
        int thresholdKb = 50,
        Action<string>? onLog = null)
    {
        var adRoot = _paths.ResolveAdSamplesRoot();
        var manifest = Read();
        var existingByKey = BuildExistingIndex(manifest);
        var folderSet = BuildFolderSet(manifest);
        var stats = new ImportSampleStats();
        var threshold = thresholdKb * 1024L;
        var pruneList = new List<FileInfo>();

        foreach (var dir in Directory.EnumerateDirectories(adRoot, "*", SearchOption.AllDirectories))
            AddFolderName(folderSet, Path.GetFileName(dir));

        foreach (var filePath in Directory.EnumerateFiles(adRoot, "*", SearchOption.AllDirectories))
        {
            var file = new FileInfo(filePath);
            if (ShouldSkip(file.Name, file.Extension))
                continue;

            var result = RegisterSampleFile(file, manifest, existingByKey, folderSet, adRoot);
            if (result.Duplicate)
            {
                stats.Skipped++;
                continue;
            }

            stats.Added += result.Added;
            stats.Updated += result.Updated;

            if (result.Added > 0)
                onLog?.Invoke($"[样本+] {file.Name}");

            if (file.Length > threshold)
                pruneList.Add(file);
        }

        manifest.Folders = folderSet.Keys.ToList();
        Save(manifest);

        if (autoPrune)
        {
            foreach (var file in pruneList)
            {
                try
                {
                    File.Delete(file.FullName);
                    stats.Pruned++;
                }
                catch
                {
                    // ignore
                }
            }
        }

        return stats;
    }

    private sealed class RegisterResult
    {
        public int Added { get; init; }
        public int Updated { get; init; }
        public bool Duplicate { get; init; }
    }

    private RegisterResult RegisterSampleFile(
        FileInfo file,
        AdManifest manifest,
        Dictionary<string, AdManifestFileEntry> existingByKey,
        Dictionary<string, bool> folderSet,
        string adRoot)
    {
        var sha = AdSampleRepository.ComputeSha256(file.FullName);
        if (sha is null)
            return new RegisterResult();

        sha = sha.ToLowerInvariant();
        var key = $"{file.Name.ToLowerInvariant()}|{sha}";
        var rel = Path.GetRelativePath(adRoot, file.FullName);
        var samplePath = rel.Contains(Path.DirectorySeparatorChar) || rel.Contains('/')
            ? Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? string.Empty
            : string.Empty;

        var parentDir = file.Directory;
        while (parentDir is not null && parentDir.FullName.Length >= adRoot.Length)
        {
            AddFolderName(folderSet, parentDir.Name);
            if (string.Equals(parentDir.FullName, adRoot, StringComparison.OrdinalIgnoreCase))
                break;
            parentDir = parentDir.Parent;
        }

        if (existingByKey.TryGetValue(key, out var old))
        {
            if (!string.Equals(old.SamplePath, samplePath, StringComparison.Ordinal) || old.Size != (int)file.Length)
            {
                old.SamplePath = samplePath;
                old.Size = (int)file.Length;
                return new RegisterResult { Updated = 1 };
            }

            return new RegisterResult { Duplicate = true };
        }

        var entry = new AdManifestFileEntry
        {
            Name = file.Name,
            Sha256 = sha,
            Size = (int)file.Length,
            SamplePath = samplePath
        };

        manifest.Files.Add(entry);
        existingByKey[key] = entry;
        return new RegisterResult { Added = 1 };
    }

    private static Dictionary<string, AdManifestFileEntry> BuildExistingIndex(AdManifest manifest)
    {
        var map = new Dictionary<string, AdManifestFileEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Files)
        {
            var key = $"{entry.Name.ToLowerInvariant()}|{entry.Sha256.ToLowerInvariant()}";
            map[key] = entry;
        }
        return map;
    }

    private static Dictionary<string, bool> BuildFolderSet(AdManifest manifest)
    {
        var set = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in manifest.Folders)
            AddFolderName(set, folder);
        return set;
    }

    private static void AddFolderName(Dictionary<string, bool> folderSet, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        folderSet.TryAdd(name.ToLowerInvariant(), true);
    }

    private static bool ShouldSkip(string name, string extension) =>
        SkipNames.Contains(name) || SkipExtensions.Contains(extension.ToLowerInvariant());
}