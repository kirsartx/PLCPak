using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class PasswordManifestService
{
    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "README.txt", "示例说明.txt", "password-manifest.json", ".keep", ".DS_Store"
    };

    private readonly AppPaths _paths;

    public PasswordManifestService(AppPaths paths) => _paths = paths;

    public PasswordManifest Read()
    {
        var path = _paths.PasswordManifestPath;
        if (!File.Exists(path))
            return CreateDefault();

        var manifest = JsonHelper.ReadFile<PasswordManifest>(path) ?? CreateDefault();
        manifest.Entries ??= [];
        return manifest;
    }

    public void Save(PasswordManifest manifest)
    {
        Directory.CreateDirectory(_paths.PasswordSamplesDirectory);
        var output = new PasswordManifest
        {
            Version = manifest.Version,
            Description = manifest.Description,
            Entries = manifest.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Password))
                .OrderByDescending(e => e.Priority)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            LastUpdated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        JsonHelper.WriteFile(_paths.PasswordManifestPath, output, utf8Bom: true);
    }

    public ImportSampleStats SyncFromFolder(Action<string>? onLog = null)
    {
        var root = _paths.PasswordSamplesDirectory;
        Directory.CreateDirectory(root);
        var manifest = Read();
        var byId = manifest.Entries.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
        var stats = new ImportSampleStats();

        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
        {
            var folderName = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(folderName))
                continue;

            var password = ReadPasswordFromFolder(dir) ?? folderName;
            var id = SlugifyId(folderName);
            if (UpsertEntry(manifest, byId, id, folderName, password, folderNames: [folderName]))
            {
                stats.Added++;
                onLog?.Invoke($"[密码+] {folderName} -> {password}");
            }
            else
            {
                stats.Updated++;
            }
        }

        foreach (var file in Directory.EnumerateFiles(root, "*.txt", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            if (ShouldSkip(name))
                continue;

            var password = ReadFirstMeaningfulLine(file);
            if (string.IsNullOrWhiteSpace(password))
                continue;

            var id = SlugifyId(Path.GetFileNameWithoutExtension(file));
            if (UpsertEntry(manifest, byId, id, Path.GetFileNameWithoutExtension(file), password, fileNames: [name]))
            {
                stats.Added++;
                onLog?.Invoke($"[密码+] {name} -> {password}");
            }
            else
            {
                stats.Updated++;
            }
        }

        Save(manifest);
        return stats;
    }

    public ImportSampleStats ImportPaths(IEnumerable<string> paths, Action<string>? onLog = null)
    {
        var root = _paths.PasswordSamplesDirectory;
        Directory.CreateDirectory(root);
        var stats = new ImportSampleStats();

        foreach (var input in paths)
        {
            if (!File.Exists(input) && !Directory.Exists(input))
            {
                stats.Errors.Add($"路径不存在: {input}");
                continue;
            }

            if (Directory.Exists(input))
            {
                var dest = Path.Combine(root, Path.GetFileName(input));
                CopyDirectory(input, dest);
                stats.Copied++;
            }
            else
            {
                var dest = Path.Combine(root, Path.GetFileName(input));
                File.Copy(input, dest, overwrite: true);
                stats.Copied++;
            }
        }

        var sync = SyncFromFolder(onLog);
        stats.Added += sync.Added;
        stats.Updated += sync.Updated;
        stats.Skipped += sync.Skipped;
        stats.Errors.AddRange(sync.Errors);
        return stats;
    }

    private static bool UpsertEntry(
        PasswordManifest manifest,
        Dictionary<string, PasswordManifestEntry> byId,
        string id,
        string name,
        string password,
        List<string>? folderNames = null,
        List<string>? fileNames = null)
    {
        if (byId.TryGetValue(id, out var existing))
        {
            var changed = false;
            if (!string.Equals(existing.Password, password, StringComparison.Ordinal))
            {
                existing.Password = password;
                changed = true;
            }

            if (folderNames is not null)
                changed |= MergeList(existing.FolderNames, folderNames);

            if (fileNames is not null)
                changed |= MergeList(existing.FileNames, fileNames);

            return !changed;
        }

        var entry = new PasswordManifestEntry
        {
            Id = id,
            Name = name,
            Password = password,
            FolderNames = folderNames ?? [],
            FileNames = fileNames ?? [],
            Priority = 50
        };

        manifest.Entries.Add(entry);
        byId[id] = entry;
        return true;
    }

    private static bool MergeList(List<string> target, IEnumerable<string> values)
    {
        var changed = false;
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (!target.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(value);
                changed = true;
            }
        }

        return changed;
    }

    private static string? ReadPasswordFromFolder(string directory)
    {
        foreach (var fileName in new[] { "password.txt", "解压密码.txt", "密码.txt" })
        {
            var file = Path.Combine(directory, fileName);
            if (!File.Exists(file))
                continue;

            var line = ReadFirstMeaningfulLine(file);
            if (!string.IsNullOrWhiteSpace(line))
                return line;
        }

        return null;
    }

    private static string? ReadFirstMeaningfulLine(string file)
    {
        foreach (var line in File.ReadLines(file))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;
            return trimmed;
        }

        return null;
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);

        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(dir.Replace(source, destination));

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = file.Replace(source, destination);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static bool ShouldSkip(string name) => SkipNames.Contains(name);

    private static string SlugifyId(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        foreach (var c in Path.GetInvalidFileNameChars())
            slug = slug.Replace(c, '_');
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N") : slug;
    }

    private static PasswordManifest CreateDefault() => new()
    {
        Version = 1,
        Description = "解压密码匹配清单",
        Entries =
        [
            new PasswordManifestEntry
            {
                Id = "laowang-default",
                Name = "老王论坛自行打包",
                Password = "上老王论坛当老王",
                Sites = ["老王论坛"],
                UrlPatterns = ["laowang.vip", "laowang"],
                FolderNames = ["上老王论坛当老王"],
                Priority = 100
            }
        ]
    };
}