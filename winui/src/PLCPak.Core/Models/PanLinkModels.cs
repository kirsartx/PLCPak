namespace PLCPak.Core.Models;

public sealed class PanLinkParseResult
{
    public bool Success { get; set; }
    public string BaiduLink { get; set; } = string.Empty;
    public string BaiduPassword { get; set; } = string.Empty;
    public string QuarkLink { get; set; } = string.Empty;
    public string QuarkPassword { get; set; } = string.Empty;
    public List<string> TelegramLinks { get; set; } = [];
    public List<string> Messages { get; set; } = [];
}