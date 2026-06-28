namespace PLCPak.Core.Models;

public sealed class AdCleanupResult
{
    public int TotalScanned { get; set; }
    public int TotalMatched { get; set; }
    public int TotalRemoved { get; set; }
    public List<string> RemovedFiles { get; } = [];
    public List<string> MatchedFiles { get; } = [];
    public List<string> Errors { get; } = [];
}