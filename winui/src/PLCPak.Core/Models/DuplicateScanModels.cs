namespace PLCPak.Core.Models;

public sealed class DuplicateScanGroup
{
    public string Reason { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public List<JobDuplicateMatch> Jobs { get; set; } = [];
}

public sealed class DuplicateScanResult
{
    public int GroupCount { get; set; }
    public int DuplicateJobCount { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public List<DuplicateScanGroup> Groups { get; set; } = [];
}