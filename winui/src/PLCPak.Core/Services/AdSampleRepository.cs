using System.Security.Cryptography;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class AdSampleRepository
{
    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "README.txt", "示例说明.txt", "ad-manifest.json", ".keep", ".DS_Store"
    };

    private static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ps1", ".bat", ".cmd", ".vbs"
    };

    private readonly AppPaths _paths;

    public AdSampleRepository(AppPaths paths) => _paths = paths;

    public AdSamples? LoadEnhanced()
    {
        var root = _paths.ResolveAdSamplesRoot();
        if (!Directory.Exists(root))
            return null;

        var samples = new AdSamples();

        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(dir);
            if (!string.IsNullOrEmpty(name))
                samples.FolderNames.TryAdd(name.ToLowerInvariant(), true);
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            if (ShouldSkip(info.Name, info.Extension))
                continue;

            var name = info.Name.ToLowerInvariant();
            if (!samples.FileNames.TryGetValue(name, out var paths))
            {
                paths = [];
                samples.FileNames[name] = paths;
            }
            paths.Add(file);

            var hash = ComputeSha256(file);
            if (hash is not null)
            {
                if (!samples.FileHashes.TryGetValue(hash, out var hashPaths))
                {
                    hashPaths = [];
                    samples.FileHashes[hash] = hashPaths;
                }
                hashPaths.Add(file);
            }
        }

        var manifestPath = Path.Combine(root, "ad-manifest.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                var manifest = JsonHelper.ReadFile<AdManifest>(manifestPath);
                if (manifest is not null)
                {
                    foreach (var folder in manifest.Folders)
                    {
                        if (!string.IsNullOrWhiteSpace(folder))
                            samples.FolderNames.TryAdd(folder.ToLowerInvariant(), true);
                    }

                    foreach (var entry in manifest.Files)
                    {
                        var name = entry.Name.ToLowerInvariant();
                        if (!samples.FileNames.ContainsKey(name))
                            samples.FileNames[name] = ["manifest"];

                        var sha = entry.Sha256.ToLowerInvariant();
                        if (!samples.FileHashes.ContainsKey(sha))
                            samples.FileHashes[sha] = ["manifest"];

                        samples.HashOnly[sha] = name;
                    }
                }
            }
            catch
            {
                // ignore manifest parse errors
            }
        }

        if (samples.FileNames.Count == 0 && samples.FolderNames.Count == 0)
            return null;

        return samples;
    }

    public static bool ShouldSkip(string name, string extension)
    {
        if (SkipNames.Contains(name))
            return true;

        return SkipExtensions.Contains(extension.ToLowerInvariant());
    }

    public static string? ComputeSha256(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    public static Dictionary<string, string?> ComputeSha256Parallel(
        IEnumerable<FileInfo> files,
        int workers,
        CancellationToken cancellationToken = default)
    {
        var fileList = files.ToList();
        var map = new System.Collections.Concurrent.ConcurrentDictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase);

        if (fileList.Count == 0)
            return new Dictionary<string, string?>(map, StringComparer.OrdinalIgnoreCase);

        if (fileList.Count <= 2 || workers <= 1)
        {
            foreach (var file in fileList)
                map[file.FullName] = ComputeSha256(file.FullName);
            return new Dictionary<string, string?>(map, StringComparer.OrdinalIgnoreCase);
        }

        var maxWorkers = Math.Min(workers, 8);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxWorkers,
            CancellationToken = cancellationToken
        };

        Parallel.ForEach(fileList, parallelOptions, file =>
        {
            map[file.FullName] = ComputeSha256(file.FullName);
        });

        return new Dictionary<string, string?>(map, StringComparer.OrdinalIgnoreCase);
    }
}