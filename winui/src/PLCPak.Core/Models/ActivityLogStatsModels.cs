namespace PLCPak.Core.Models;

public sealed class ActivityLogCategoryStat
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTime LatestTimestamp { get; set; }
}

public sealed class ActivityLogStatsResult
{
    public List<ActivityLogCategoryStat> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int? SinceDays { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class ActivityLogStatsExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public ActivityLogStatsResult Stats { get; set; } = new();
    public int? SinceDays { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class ActivityLogStatsCsvExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public ActivityLogStatsResult Stats { get; set; } = new();
    public int? SinceDays { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class ActivityLogStatsHtmlExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public ActivityLogStatsResult Stats { get; set; } = new();
    public int? SinceDays { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class ActivityLogStatBarItem
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public double BarPercent { get; set; }
}