namespace PLCPak.Core.Models;

public sealed class JobBackupSnapshot
{
    public string Version { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; } = DateTime.Now;
    public int Count { get; set; }
    public List<PublishJob> Jobs { get; set; } = [];
}

public sealed class JobBackupImportResult
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Updated { get; set; }
    public List<string> Messages { get; set; } = [];
}

public enum JobBackupImportMode
{
    Merge,
    SkipExisting
}