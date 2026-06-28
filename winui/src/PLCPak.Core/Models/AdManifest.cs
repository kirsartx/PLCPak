namespace PLCPak.Core.Models;

public sealed class AdManifest
{
    public int Version { get; set; } = 1;
    public string Description { get; set; } = "广告样本哈希清单";
    public string? GeneratedFrom { get; set; }
    public int FileCount { get; set; }
    public List<string> Folders { get; set; } = [];
    public List<AdManifestFileEntry> Files { get; set; } = [];
    public string? LastUpdated { get; set; }
}

public sealed class AdManifestFileEntry
{
    public string Name { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public int Size { get; set; }
    public string SamplePath { get; set; } = string.Empty;
}