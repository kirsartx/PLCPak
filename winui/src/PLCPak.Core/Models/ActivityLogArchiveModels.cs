namespace PLCPak.Core.Models;

public sealed class ActivityLogArchiveResult
{
    public int ArchivedCount { get; set; }
    public int RemainingCount { get; set; }
    public int KeepDays { get; set; }
    public string? ArchivePath { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}