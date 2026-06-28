namespace PLCPak.Core.Models;

public sealed class DuplicateOperationsBundle
{
    public DuplicateScanResult Scan { get; set; } = new();
    public DuplicateMergeSuggestionResult Suggestions { get; set; } = new();
}