namespace PLCPak.Core.Models;

public sealed class ScanCache
{
    public int Version { get; set; } = 1;
    public List<ScanCacheEntry> Entries { get; set; } = [];
}

public sealed class ScanCacheEntry
{
    public string Path { get; set; } = string.Empty;
    public long Mtime { get; set; }
    public int Scanned { get; set; }
    public int Matched { get; set; }
    public string Time { get; set; } = string.Empty;
}