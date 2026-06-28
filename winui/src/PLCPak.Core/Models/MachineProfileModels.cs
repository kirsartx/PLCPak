namespace PLCPak.Core.Models;

public sealed class MachineProfileBundle
{
    public string Version { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; } = DateTime.Now;
    public string? StudioConfigJson { get; set; }
    public string? UiPreferencesJson { get; set; }
    public string? ShortcutProfileJson { get; set; }
    public string? OperationsCenterSummary { get; set; }
}

public sealed class MachineProfileExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public bool IncludesStudioConfig { get; set; }
    public bool IncludesUiPreferences { get; set; }
    public bool IncludesShortcutProfile { get; set; }
    public int ShortcutOverrideCount { get; set; }
    public int ShortcutDisabledCount { get; set; }
    public string HtmlReportTheme { get; set; } = "light";
    public bool NightlyExportActivityLogBatchStatsAll { get; set; }
    public int ActivityLogKeepDays { get; set; }
    public string OperationsCenterSummary { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class MachineProfileImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool StudioConfigImported { get; set; }
    public bool UiPreferencesImported { get; set; }
    public bool ShortcutProfileImported { get; set; }
    public bool Merged { get; set; }
}