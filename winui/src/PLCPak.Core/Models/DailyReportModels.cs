namespace PLCPak.Core.Models;

public sealed class DailyReportEntry
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string PublishSummary { get; set; } = string.Empty;
    public string NextActionLabel { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public sealed class DailyReportSnapshot
{
    public DateTime Date { get; set; } = DateTime.Today;
    public int TotalJobs { get; set; }
    public PublishStats Stats { get; set; } = new();
    public int PendingPublishCount { get; set; }
    public int FailedCount { get; set; }
    public List<DailyReportEntry> Entries { get; set; } = [];
    public string? CsvPath { get; set; }
    public string? JsonPath { get; set; }
}