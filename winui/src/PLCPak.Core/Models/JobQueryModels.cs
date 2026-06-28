namespace PLCPak.Core.Models;

public enum JobSortOrder
{
    UpdatedDesc,
    UpdatedAsc,
    TitleAsc,
    TitleDesc,
    PinnedFirst
}

public sealed class JobDuplicateMatch
{
    public string Reason { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ThreadUrl { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
}

public sealed class JobCreateCheckResult
{
    public bool HasDuplicates { get; set; }
    public bool Blocked { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<JobDuplicateMatch> Duplicates { get; set; } = [];
}

public sealed class ThreadTitleResult
{
    public bool Success { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string Source { get; set; } = string.Empty;
}