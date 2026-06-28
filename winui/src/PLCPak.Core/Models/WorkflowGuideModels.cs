namespace PLCPak.Core.Models;

public sealed class WorkflowGuideSnapshot
{
    public string SummaryText { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? JobId { get; set; }
    public string? JobTitle { get; set; }
    public JobNextActionType Action { get; set; } = JobNextActionType.None;
    public string RecommendedPage { get; set; } = "dashboard";
    public string RecommendedPageLabel { get; set; } = "发布看板";
    public int JobsBadgeCount { get; set; }
    public int TgPendingCount { get; set; }
    public int StaleCount { get; set; }
    public bool HasAction => !string.IsNullOrWhiteSpace(JobId);
}