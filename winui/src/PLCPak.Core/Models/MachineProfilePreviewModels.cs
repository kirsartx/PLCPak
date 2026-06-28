namespace PLCPak.Core.Models;

public sealed class MachineProfilePreviewResult
{
    public bool Valid { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Version { get; set; }
    public DateTime? ExportedAt { get; set; }
    public bool HasStudioConfig { get; set; }
    public bool HasUiPreferences { get; set; }
    public bool HasShortcutProfile { get; set; }
    public int ShortcutOverrideCount { get; set; }
    public int ShortcutDisabledCount { get; set; }
    public string? HtmlReportTheme { get; set; }
    public bool NightlyExportActivityLogBatchStatsAll { get; set; }
    public int ActivityLogKeepDays { get; set; }
    public string OperationsCenterSummary { get; set; } = string.Empty;
    public int ShortcutConflictCount { get; set; }
    public string ShortcutConflictMessage { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
}