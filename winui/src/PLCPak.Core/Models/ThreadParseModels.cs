namespace PLCPak.Core.Models;

public sealed class ThreadParseResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleSource { get; set; } = string.Empty;
    public string BaiduLink { get; set; } = string.Empty;
    public string BaiduPassword { get; set; } = string.Empty;
    public string QuarkLink { get; set; } = string.Empty;
    public string QuarkPassword { get; set; } = string.Empty;
    public string ArchivePassword { get; set; } = string.Empty;
    public string DownloadHint { get; set; } = string.Empty;
    public List<string> AllLinks { get; set; } = [];
    public List<string> AttachmentLinks { get; set; } = [];
}

public sealed class JobMergeResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string TargetJobId { get; set; } = string.Empty;
    public string SourceJobId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string> MergedFields { get; set; } = [];
}

public sealed class PublishQueueEntry
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string PublishSummary { get; set; } = string.Empty;
    public bool HasCopy { get; set; }
    public bool BaiduReady { get; set; }
    public bool QuarkReady { get; set; }
    public bool TelegramReady { get; set; }
    public int ReadyChannelCount { get; set; }
}

public sealed class PublishQueueSnapshot
{
    public int Count { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public List<PublishQueueEntry> Entries { get; set; } = [];
}