namespace PLCPak.Core.Models;

public sealed class ActivityLogCsvExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public int EntryCount { get; set; }
    public string? CategoryFilter { get; set; }
    public string? QueryFilter { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}