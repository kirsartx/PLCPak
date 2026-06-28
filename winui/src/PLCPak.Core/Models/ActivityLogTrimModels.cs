namespace PLCPak.Core.Models;

public sealed class ActivityLogTrimPreviewResult
{
    public int TotalCount { get; set; }
    public int WouldRemoveCount { get; set; }
    public int WouldRemainCount { get; set; }
    public int KeepDays { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class ActivityLogTrimResult
{
    public int RemovedCount { get; set; }
    public int RemainingCount { get; set; }
    public int KeepDays { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}