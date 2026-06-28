namespace PLCPak.Core.Models;

public sealed class TgPendingEntry
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string PublishSummary { get; set; } = string.Empty;
    public bool HasCopy { get; set; }
    public bool BaiduPublished { get; set; }
    public bool QuarkPublished { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class TgPendingSnapshot
{
    public int Count { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public List<TgPendingEntry> List { get; set; } = [];

    public List<TgPendingEntry> Entries
    {
        get => List;
        set => List = value;
    }
}