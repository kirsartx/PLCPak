namespace PLCPak.Core.Models;

public sealed class AllBackupBundle
{
    public string Version { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; } = DateTime.Now;
    public string? StudioConfigJson { get; set; }
    public string? UiPreferencesJson { get; set; }
    public List<PublishJob> Jobs { get; set; } = [];
}

public sealed class AllBackupExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public int JobCount { get; set; }
    public bool IncludesStudioConfig { get; set; }
}

public sealed class AllBackupImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ImportedJobs { get; set; }
    public int SkippedJobs { get; set; }
    public int ReplacedJobs { get; set; }
    public bool StudioConfigImported { get; set; }
    public bool Merged { get; set; }
}