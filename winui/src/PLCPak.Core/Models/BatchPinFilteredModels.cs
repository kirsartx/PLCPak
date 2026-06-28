namespace PLCPak.Core.Models;

public sealed class BatchPinFilteredPreviewResult
{
    public int TotalMatched { get; set; }
    public int ApplicableCount { get; set; }
    public int AlreadyInTargetStateCount { get; set; }
    public bool Pin { get; set; }
    public List<string> SampleTitles { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class BatchPinFilteredResult
{
    public int Applied { get; set; }
    public int Skipped { get; set; }
    public bool Pin { get; set; }
    public List<string> UpdatedJobIds { get; set; } = [];
    public List<string> Messages { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}