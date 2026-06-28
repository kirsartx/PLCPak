namespace PLCPak.Core.Models;

public sealed class ActivityLogBatchSummaryItem
{
    public string Category { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class ActivityLogBatchSummaryResult
{
    public int TotalCount { get; set; }
    public int? SinceDays { get; set; }
    public List<ActivityLogBatchSummaryItem> Items { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}