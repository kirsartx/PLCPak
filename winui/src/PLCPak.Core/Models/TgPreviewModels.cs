namespace PLCPak.Core.Models;

public sealed class TgPreviewEntry
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int OriginalLength { get; set; }
    public int PartCount { get; set; }
    public bool WasTruncated { get; set; }
    public string PreviewText { get; set; } = string.Empty;
}

public sealed class TgPreviewSnapshot
{
    public int Count { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public List<TgPreviewEntry> Entries { get; set; } = [];
}