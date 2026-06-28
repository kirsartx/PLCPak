namespace PLCPak.Core.Models;

public enum JobNextActionType
{
    None,
    RetryFailed,
    DownloadInbox,
    RunPipeline,
    FillLinks,
    GenerateCopy,
    MarkPublished,
    SendTelegram
}

public sealed class JobNextActionEntry
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public JobNextActionType Action { get; set; }
    public string ActionLabel { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PublishWorkflowSnapshot
{
    public JobNextActionEntry? Primary { get; set; }
    public List<JobNextActionEntry> Suggestions { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class JobNextActionResult
{
    public bool Success { get; set; }
    public bool NeedsUserInput { get; set; }
    public string? Error { get; set; }
    public string Message { get; set; } = string.Empty;
    public JobNextActionType Action { get; set; } = JobNextActionType.None;
    public JobNextActionEntry? Entry { get; set; }
    public PublishJob? Job { get; set; }
}