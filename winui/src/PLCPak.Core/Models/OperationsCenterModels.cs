namespace PLCPak.Core.Models;

public sealed class StaleJobEntry
{
    public string JobId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int DaysSinceUpdate { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class MaintenanceReport
{
    public int StaleCount { get; set; }
    public int PublishedReadyToArchive { get; set; }
    public List<StaleJobEntry> StaleJobs { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class BulkArchiveResult
{
    public int Archived { get; set; }
    public int Skipped { get; set; }
    public List<string> AffectedJobIds { get; set; } = [];
    public List<string> Messages { get; set; } = [];
}

public sealed class BulkUnarchiveResult
{
    public int Unarchived { get; set; }
    public int Skipped { get; set; }
    public List<string> AffectedJobIds { get; set; } = [];
    public List<string> Messages { get; set; } = [];
}

public sealed class BatchArchiveFilteredResult
{
    public int Count { get; set; }
    public int Archived { get; set; }
    public int Skipped { get; set; }
    public List<string> JobIds { get; set; } = [];
    public List<string> Messages { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class BatchArchiveFilteredPreviewResult
{
    public int TotalMatched { get; set; }
    public int ArchivableCount { get; set; }
    public int AlreadyArchivedCount { get; set; }
    public List<string> SampleTitles { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class OperationsCenterSection
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class OperationsCenterSnapshot
{
    public string SummaryText { get; set; } = string.Empty;
    public string QuickStatsLine { get; set; } = string.Empty;
    public PublishWorkflowSnapshot Workflow { get; set; } = new();
    public MaintenanceReport Maintenance { get; set; } = new();
    public JobHealthReport Health { get; set; } = new();
    public PublishQueueSnapshot Queue { get; set; } = new();
    public ActivityLogBatchSummaryResult ActivityLogBatchSummary { get; set; } = new();
    public ActivityLogStatsResult ActivityLogStatsSummary { get; set; } = new();
    public List<OperationsCenterSection> Sections { get; set; } = [];
}