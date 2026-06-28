namespace PLCPak.Core.Models;

public enum JobStatus
{
    Draft,
    InboxReady,
    Extracting,
    Extracted,
    Processing,
    Processed,
    Published,
    Failed,
    Archived
}

public enum JobPlatform
{
    PC,
    AZ,
    Both
}

public sealed class JobSource
{
    public string Type { get; set; } = "forum";
    public string Site { get; set; } = "老王论坛";
    public string ThreadUrl { get; set; } = string.Empty;
    public string DownloadHint { get; set; } = string.Empty;
    public string ArchivePassword { get; set; } = string.Empty;
    public List<string> MatchedPasswords { get; set; } = [];
}

public sealed class JobPaths
{
    public string Slug { get; set; } = string.Empty;
    public string Inbox { get; set; } = string.Empty;
    public string Extract { get; set; } = string.Empty;
    public string Staging { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
}

public sealed class JobArtifacts
{
    public List<string> InboxArchives { get; set; } = [];
    public string? GameRoot { get; set; }
    public List<string> OutputArchives { get; set; } = [];
}

public sealed class PublishChannelState
{
    public string Status { get; set; } = "pending";
    public string Link { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
}

public sealed class JobPublishState
{
    public PublishChannelState Baidu { get; set; } = new();
    public PublishChannelState Quark { get; set; } = new();
    public PublishChannelState Telegram { get; set; } = new();
    public string TemplateId { get; set; } = string.Empty;
    public string GeneratedCopy { get; set; } = string.Empty;
    public DateTime? CopyGeneratedAt { get; set; }
}

public sealed partial class PublishJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public JobSource Source { get; set; } = new();
    public JobPlatform Platform { get; set; } = JobPlatform.Both;
    public bool SelfPurchase { get; set; }
    public bool Enable7z { get; set; } = true;
    public bool EnableTarZst { get; set; } = true;
    public string Notes { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Draft;
    public string? Error { get; set; }
    public JobPaths Paths { get; set; } = new();
    public JobArtifacts Artifacts { get; set; } = new();
    public JobPublishState Publish { get; set; } = new();
    public JobStatus? ArchivedFromStatus { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool IsPinned { get; set; }
    public List<string> Log { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public void AppendLog(string message)
    {
        Log.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        UpdatedAt = DateTime.Now;
    }
}