namespace PLCPak.Core.Models;

public sealed class ForumDownloadItem
{
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public string LocalPath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public sealed class ForumDownloadResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int DownloadedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<ForumDownloadItem> Items { get; set; } = [];
    public List<string> Messages { get; set; } = [];
    public PublishJob? Job { get; set; }
}