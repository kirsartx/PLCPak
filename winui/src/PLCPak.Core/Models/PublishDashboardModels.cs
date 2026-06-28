namespace PLCPak.Core.Models;

public enum JobListFilter
{
    All,
    PendingPublish,
    Published,
    Processed,
    Failed,
    Active,
    Archived
}

public sealed class PublishStats
{
    public int Total { get; init; }
    public int Published { get; init; }
    public int PendingPublish { get; init; }
    public int Processed { get; init; }
    public int Failed { get; init; }
    public int Active { get; init; }
    public int Archived { get; init; }

    public string SummaryText
        => $"共 {Total} | 已发布 {Published} | 待发布 {PendingPublish} | 已压缩 {Processed} | 进行中 {Active} | 失败 {Failed} | 归档 {Archived}";
}

public sealed class PublishHistoryEntry
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PublishSummary { get; set; } = string.Empty;
    public string BaiduLink { get; set; } = string.Empty;
    public string QuarkLink { get; set; } = string.Empty;
    public string TelegramLink { get; set; } = string.Empty;
    public DateTime? BaiduPublishedAt { get; set; }
    public DateTime? QuarkPublishedAt { get; set; }
    public DateTime? TelegramPublishedAt { get; set; }
    public DateTime? CopyGeneratedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class BatchCopyResult
{
    public int Success { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Messages { get; set; } = [];
}

public sealed class PublishHistoryExport
{
    public string Version { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; } = DateTime.Now;
    public int Count { get; set; }
    public List<PublishHistoryEntry> Entries { get; set; } = [];
    public string? ExportPath { get; set; }
}

public sealed class RecentActivityEntry
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string? LastLogLine { get; set; }
}

public sealed class JobMissingInboxEntry
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ExpectedInboxPath { get; set; } = string.Empty;
}

public sealed class WorkspaceHealthReport
{
    public string SummaryText { get; set; } = string.Empty;
    public List<string> OrphanInboxDirs { get; set; } = [];
    public List<string> OrphanExtractDirs { get; set; } = [];
    public List<string> OrphanOutputDirs { get; set; } = [];
    public List<JobMissingInboxEntry> JobsWithoutInbox { get; set; } = [];
    public long TotalBytes { get; set; }
    public long InboxBytes { get; set; }
    public long ExtractBytes { get; set; }
    public long OutputBytes { get; set; }
    public long PublishedBytes { get; set; }
    public long JobsBytes { get; set; }
}

public enum JobListSort
{
    Updated,
    Title
}

public sealed class JobHealthIssue
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "warning";
}

public sealed class JobHealthReport
{
    public int TotalJobs { get; set; }
    public int IssueCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<JobHealthIssue> Issues { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class JobImportResult
{
    public bool Created { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public PublishJob Job { get; set; } = null!;
}

public sealed class PublishLinksSnapshot
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BaiduLink { get; set; } = string.Empty;
    public string BaiduPassword { get; set; } = string.Empty;
    public string QuarkLink { get; set; } = string.Empty;
    public string QuarkPassword { get; set; } = string.Empty;
    public string TelegramLink { get; set; } = string.Empty;
    public string FormattedText { get; set; } = string.Empty;
}

