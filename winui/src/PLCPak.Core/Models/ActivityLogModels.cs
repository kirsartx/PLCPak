namespace PLCPak.Core.Models;

public sealed class ActivityLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ActivityLogQueryOptions
{
    public string? Category { get; set; }
    public string? Query { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; }
    public int? SinceDays { get; set; }
    public DateTime? Since { get; set; }
    public DateTime? Until { get; set; }
}

public sealed class ActivityLogPageResult
{
    public List<ActivityLogEntry> Entries { get; set; } = [];
    public int TotalMatched { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; }
    public bool HasMore { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class ActivityLogExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public int EntryCount { get; set; }
    public string? CategoryFilter { get; set; }
}