namespace PLCPak.Core.Models;

public sealed class FilteredJobsCsvExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public int EntryCount { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}