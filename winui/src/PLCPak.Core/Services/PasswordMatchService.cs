using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class PasswordMatchService
{
    private static readonly string[] PasswordHintFileNames =
    [
        "解压密码.txt", "密码.txt", "password.txt", "解压说明.txt"
    ];

    private readonly PasswordManifestService _manifest;
    private readonly StudioConfigService _studioConfig;
    private readonly SevenZipService _sevenZip;

    public PasswordMatchService(
        PasswordManifestService manifest,
        StudioConfigService studioConfig,
        SevenZipService sevenZip)
    {
        _manifest = manifest;
        _studioConfig = studioConfig;
        _sevenZip = sevenZip;
    }

    public PasswordMatchResult MatchForJob(PublishJob job, IEnumerable<string>? archives = null)
    {
        var manifest = _manifest.Read();
        var hits = new List<PasswordMatchHit>();
        var archiveList = archives?.Where(File.Exists).ToList() ?? [];

        MatchBySite(manifest, job, hits);
        MatchByUrl(manifest, job, hits);
        MatchInboxHintFiles(job, hits);
        MatchArchives(manifest, job, archiveList, hits);

        foreach (var pwd in _studioConfig.Load().ExtractPasswords)
            AddHit(hits, "studio-config", "studio-config", pwd, "studio-config.json", 10);

        var ordered = hits
            .OrderByDescending(h => h.Priority)
            .ThenBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var best = ordered.FirstOrDefault();
        return new PasswordMatchResult
        {
            Hits = ordered,
            BestPassword = best?.Password,
            BestReason = best is null ? null : $"{best.Name} ({best.Reason})"
        };
    }

    private static void MatchBySite(PasswordManifest manifest, PublishJob job, List<PasswordMatchHit> hits)
    {
        if (string.IsNullOrWhiteSpace(job.Source.Site))
            return;

        foreach (var entry in manifest.Entries)
        {
            if (entry.Sites.Any(site => site.Equals(job.Source.Site, StringComparison.OrdinalIgnoreCase)))
            {
                AddHit(hits, entry, $"来源论坛: {job.Source.Site}");
            }
        }
    }

    private static void MatchByUrl(PasswordManifest manifest, PublishJob job, List<PasswordMatchHit> hits)
    {
        if (string.IsNullOrWhiteSpace(job.Source.ThreadUrl))
            return;

        var url = job.Source.ThreadUrl;
        foreach (var entry in manifest.Entries)
        {
            foreach (var pattern in entry.UrlPatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    continue;

                if (url.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    AddHit(hits, entry, $"帖子链接含: {pattern}");
                    break;
                }
            }
        }
    }

    private static void MatchInboxHintFiles(PublishJob job, List<PasswordMatchHit> hits)
    {
        if (!Directory.Exists(job.Paths.Inbox))
            return;

        foreach (var file in Directory.EnumerateFiles(job.Paths.Inbox, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (!PasswordHintFileNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                continue;

            var password = ReadPasswordLine(file);
            if (string.IsNullOrWhiteSpace(password))
                continue;

            AddHit(hits, "inbox-file", name, password, $"inbox 文件: {name}", 80);
        }
    }

    private void MatchArchives(
        PasswordManifest manifest,
        PublishJob job,
        List<string> archives,
        List<PasswordMatchHit> hits)
    {
        if (archives.Count == 0)
            return;

        var seedPasswords = hits
            .Select(h => h.Password)
            .Concat(manifest.Entries.Select(e => e.Password))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var archive in archives)
        {
            var archiveName = Path.GetFileName(archive);
            foreach (var entry in manifest.Entries)
            {
                if (entry.ArchivePatterns.Any(p => LikeMatch(archiveName, p)))
                    AddHit(hits, entry, $"压缩包名: {archiveName}");
            }

            var listing = ListArchivePaths(archive, seedPasswords);
            foreach (var path in listing)
            {
                var leaf = GetArchiveLeafName(path);
                foreach (var entry in manifest.Entries)
                {
                    if (entry.FolderNames.Any(f => f.Equals(leaf, StringComparison.OrdinalIgnoreCase)
                        || path.Contains(f, StringComparison.OrdinalIgnoreCase)))
                    {
                        AddHit(hits, entry, $"压缩包内文件夹: {leaf}");
                    }

                    if (entry.FileNames.Any(f => f.Equals(leaf, StringComparison.OrdinalIgnoreCase)))
                    {
                        AddHit(hits, entry, $"压缩包内文件: {leaf}");
                    }
                }
            }
        }
    }

    private List<string> ListArchivePaths(string archivePath, IReadOnlyList<string> passwordCandidates)
    {
        var sevenZip = _sevenZip.FindSevenZip();
        if (sevenZip is null)
            return [];

        var passwords = new List<string> { string.Empty };
        foreach (var pwd in passwordCandidates)
        {
            if (!string.IsNullOrWhiteSpace(pwd) && !passwords.Contains(pwd))
                passwords.Add(pwd);
        }

        foreach (var password in passwords)
        {
            var args = new List<string> { "l", "-slt", archivePath };
            args.Add(string.IsNullOrEmpty(password) ? "-p-" : $"-p{password}");
            var result = _sevenZip.RunProcess(sevenZip, args, CancellationToken.None, extractMode: true);
            if (result.ExitCode != 0)
                continue;

            var paths = ParseArchiveListing(result.Output);
            if (paths.Count > 0)
                return paths;
        }

        return [];
    }

    private static List<string> ParseArchiveListing(string output)
    {
        var paths = new List<string>();
        foreach (var raw in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.StartsWith("Path = ", StringComparison.OrdinalIgnoreCase))
            {
                var path = line["Path = ".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(path))
                    paths.Add(path.Replace('\\', '/'));
                continue;
            }

            if (line.StartsWith('-') && line.Length > 30)
            {
                var name = line[53..].Trim();
                if (!string.IsNullOrWhiteSpace(name))
                    paths.Add(name.Replace('\\', '/'));
            }
        }

        return paths;
    }

    private static string GetArchiveLeafName(string path)
    {
        var normalized = path.TrimEnd('/', '\\');
        var idx = normalized.LastIndexOfAny(['/', '\\']);
        return idx < 0 ? normalized : normalized[(idx + 1)..];
    }

    private static void AddHit(List<PasswordMatchHit> hits, PasswordManifestEntry entry, string reason)
        => AddHit(hits, entry.Id, entry.Name, entry.Password, reason, entry.Priority);

    private static void AddHit(
        List<PasswordMatchHit> hits,
        string id,
        string name,
        string password,
        string reason,
        int priority)
    {
        if (string.IsNullOrWhiteSpace(password))
            return;

        if (hits.Any(h => h.Password.Equals(password, StringComparison.Ordinal)
            && h.Reason.Equals(reason, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        hits.Add(new PasswordMatchHit
        {
            EntryId = id,
            Name = name,
            Password = password,
            Reason = reason,
            Priority = priority
        });
    }

    private static string? ReadPasswordLine(string file)
    {
        try
        {
            foreach (var line in File.ReadLines(file))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    continue;

                var idx = trimmed.IndexOf(':');
                if (idx >= 0 && idx < trimmed.Length - 1)
                    return trimmed[(idx + 1)..].Trim();

                return trimmed;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static bool LikeMatch(string input, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        if (!pattern.Contains('*') && !pattern.Contains('?'))
            return input.Contains(pattern, StringComparison.OrdinalIgnoreCase);

        return System.Text.RegularExpressions.Regex.IsMatch(
            input,
            "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}