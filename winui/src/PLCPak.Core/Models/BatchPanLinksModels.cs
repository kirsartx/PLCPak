namespace PLCPak.Core.Models;

public sealed class BatchPanLinksCsvRow
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BaiduLink { get; set; } = string.Empty;
    public string BaiduPwd { get; set; } = string.Empty;
    public string QuarkLink { get; set; } = string.Empty;
    public string QuarkPwd { get; set; } = string.Empty;
    public string TelegramLink { get; set; } = string.Empty;
}

public sealed class BatchPanLinksImportResult
{
    public bool Success { get; set; }
    public int Applied { get; set; }
    public int Failed { get; set; }
    public List<string> Messages { get; set; } = [];
}