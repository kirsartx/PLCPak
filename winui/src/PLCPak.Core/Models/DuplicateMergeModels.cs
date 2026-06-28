namespace PLCPak.Core.Models;

public sealed class DuplicateMergeSuggestion
{
    public string Reason { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string TargetJobId { get; set; } = string.Empty;
    public string TargetTitle { get; set; } = string.Empty;
    public List<string> SourceJobIds { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class DuplicateMergeSuggestionResult
{
    public int GroupCount { get; set; }
    public int MergeActionCount { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public List<DuplicateMergeSuggestion> Suggestions { get; set; } = [];
}

public sealed class DuplicateMergeExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public int GroupCount { get; set; }
    public int MergeActionCount { get; set; }
    public int EntryCount { get; set; }
}

public sealed class BatchDuplicateMergeResult
{
    public int GroupsProcessed { get; set; }
    public int Merged { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public List<string> Messages { get; set; } = [];
}

public sealed class BatchDuplicateMergePreviewItem
{
    public string TargetJobId { get; set; } = string.Empty;
    public string TargetTitle { get; set; } = string.Empty;
    public List<string> SourceJobIds { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}

public sealed class BatchDuplicateMergePreviewResult
{
    public int GroupCount { get; set; }
    public int MergeActionCount { get; set; }
    public int WouldMergeCount { get; set; }
    public int WouldSkipCount { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public List<BatchDuplicateMergePreviewItem> Items { get; set; } = [];
}

public sealed class BatchDuplicateMergePreviewExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public int GroupCount { get; set; }
    public int MergeActionCount { get; set; }
    public int ItemCount { get; set; }
}