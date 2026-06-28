namespace PLCPak.Core.Models;

public enum BatchTagMode
{
    Append,
    Replace
}

public sealed class BatchTagResult
{
    public int Applied { get; set; }
    public int Skipped { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public List<string> Messages { get; set; } = [];
    public List<string> UpdatedJobIds { get; set; } = [];
}