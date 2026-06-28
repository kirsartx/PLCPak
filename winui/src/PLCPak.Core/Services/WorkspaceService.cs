using System.Text.RegularExpressions;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class WorkspaceService
{
    private static readonly string[] ArchiveExtensions =
    [
        ".7z", ".zip", ".rar", ".tar", ".zst", ".tar.zst", ".001"
    ];

    private readonly AppPaths _paths;
    private readonly StudioConfigService _studioConfig;

    public WorkspaceService(AppPaths paths, StudioConfigService studioConfig)
    {
        _paths = paths;
        _studioConfig = studioConfig;
    }

    public string GetWorkspaceRoot()
    {
        var config = _studioConfig.Load();
        if (!string.IsNullOrWhiteSpace(config.WorkspaceRoot))
            return Path.GetFullPath(config.WorkspaceRoot);

        return Path.GetFullPath(Path.Combine(_paths.PackageRoot, "..", "workspace"));
    }

    public string JobsDirectory => Path.Combine(GetWorkspaceRoot(), "jobs");
    public string InboxDirectory => Path.Combine(GetWorkspaceRoot(), "inbox");
    public string ExtractDirectory => Path.Combine(GetWorkspaceRoot(), "extract");
    public string OutputDirectory => Path.Combine(GetWorkspaceRoot(), "output");
    public string PublishedDirectory => Path.Combine(GetWorkspaceRoot(), "published");

    public void EnsureLayout()
    {
        foreach (var dir in new[] { JobsDirectory, InboxDirectory, ExtractDirectory, OutputDirectory, PublishedDirectory })
            Directory.CreateDirectory(dir);
    }

    public static string Slugify(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return DateTime.Now.ToString("yyyyMMdd_HHmmss");

        var slug = Regex.Replace(title.Trim(), @"\s+", "_");
        slug = Regex.Replace(slug, @"[<>:""/\\|?*]", "");
        slug = slug.Trim('_', '.');
        return string.IsNullOrWhiteSpace(slug) ? DateTime.Now.ToString("yyyyMMdd_HHmmss") : slug;
    }

    public JobPaths BuildJobPaths(string slug)
    {
        EnsureLayout();
        return new JobPaths
        {
            Slug = slug,
            Inbox = Path.Combine(InboxDirectory, slug),
            Extract = Path.Combine(ExtractDirectory, slug),
            Staging = Path.Combine(ExtractDirectory, slug),
            Output = Path.Combine(OutputDirectory, slug)
        };
    }

    public List<string> FindArchives(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(IsArchiveFile)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return DeduplicateSplitVolumes(files);
    }

    public static string? PickPrimaryArchive(IEnumerable<string> archives)
    {
        return archives
            .Select(path => new FileInfo(path))
            .Where(info => info.Exists)
            .OrderByDescending(info => info.Length)
            .Select(info => info.FullName)
            .FirstOrDefault();
    }

    public static bool IsArchiveFile(string path)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var ext = Path.GetExtension(name);
        if (ArchiveExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return true;

        if (name.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase))
            return true;

        return Regex.IsMatch(name, @"\.7z\.\d+$", RegexOptions.IgnoreCase)
            || Regex.IsMatch(name, @"\.z\d+$", RegexOptions.IgnoreCase);
    }

    private static List<string> DeduplicateSplitVolumes(IEnumerable<string> files)
    {
        var result = new List<string>();
        var seenBases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var baseName = GetArchiveBaseName(file);
            if (seenBases.Add(baseName))
                result.Add(file);
        }

        return result;
    }

    private static string GetArchiveBaseName(string path)
    {
        var name = Path.GetFileName(path);
        if (Regex.IsMatch(name, @"\.7z\.\d+$", RegexOptions.IgnoreCase))
            return Regex.Replace(name, @"\.\d+$", "", RegexOptions.IgnoreCase);
        if (Regex.IsMatch(name, @"\.z\d+$", RegexOptions.IgnoreCase))
            return Regex.Replace(name, @"\.z\d+$", ".zip", RegexOptions.IgnoreCase);
        return name;
    }
}