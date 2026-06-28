namespace PLCPak.Core.Models;

public sealed class ActivityLogBatchBundleExportResult
{
    public string JsonExportPath { get; set; } = string.Empty;
    public string CsvExportPath { get; set; } = string.Empty;
    public string BundleStamp { get; set; } = string.Empty;
    public int? SinceDays { get; set; }
    public ActivityLogBatchSummaryResult Stats { get; set; } = new();
    public string SummaryText { get; set; } = string.Empty;
}