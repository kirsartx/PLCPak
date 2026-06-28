namespace PLCPak.Core.Models;

public sealed class AdSamples
{
    public Dictionary<string, List<string>> FileNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> FileHashes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> FolderNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> HashOnly { get; } = new(StringComparer.OrdinalIgnoreCase);
}