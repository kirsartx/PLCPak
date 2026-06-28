namespace PLCPak.Core.Models;

public sealed class QueueCsvExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public int EntryCount { get; set; }
    public string QueueType { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class DuplicateReportExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public int GroupCount { get; set; }
    public int DuplicateJobCount { get; set; }
}