namespace PLCPak.Core.Models;

public sealed class CreateJobFromThreadResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public PublishJob? Job { get; set; }
    public ThreadParseResult? ParseResult { get; set; }
    public ForumDownloadResult? DownloadResult { get; set; }
    public string Message { get; set; } = string.Empty;
}