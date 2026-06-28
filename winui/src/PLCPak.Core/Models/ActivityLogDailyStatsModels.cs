namespace PLCPak.Core.Models;

public sealed class ActivityLogDailyCountItem
{
    public DateTime Date { get; set; }
    public string DateLabel { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class ActivityLogDailyStatsResult
{
    public int Days { get; set; }
    public int TotalCount { get; set; }
    public List<ActivityLogDailyCountItem> Items { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class ActivityLogDailyStatsCsvExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public ActivityLogDailyStatsResult Stats { get; set; } = new();
    public string SummaryText { get; set; } = string.Empty;
}