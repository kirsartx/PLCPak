namespace PLCPak.Core.Models;

public sealed class TelegramSendResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int? MessageId { get; set; }
    public string ChatId { get; set; } = string.Empty;
    public bool WasTruncated { get; set; }
    public int OriginalLength { get; set; }
    public int PartsSent { get; set; }
}