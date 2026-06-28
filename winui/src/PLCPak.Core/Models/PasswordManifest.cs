namespace PLCPak.Core.Models;

public sealed class PasswordManifest
{
    public int Version { get; set; } = 1;
    public string Description { get; set; } = "解压密码匹配清单";
    public List<PasswordManifestEntry> Entries { get; set; } = [];
    public string? LastUpdated { get; set; }
}

public sealed class PasswordManifestEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public List<string> Sites { get; set; } = [];
    public List<string> UrlPatterns { get; set; } = [];
    public List<string> FolderNames { get; set; } = [];
    public List<string> FileNames { get; set; } = [];
    public List<string> ArchivePatterns { get; set; } = [];
    public int Priority { get; set; }
}

public sealed class PasswordMatchHit
{
    public string EntryId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public int Priority { get; init; }
}

public sealed class PasswordMatchResult
{
    public List<PasswordMatchHit> Hits { get; init; } = [];
    public string? BestPassword { get; init; }
    public string? BestReason { get; init; }

    public IEnumerable<string> OrderedPasswords =>
        Hits
            .OrderByDescending(h => h.Priority)
            .Select(h => h.Password)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.Ordinal);
}