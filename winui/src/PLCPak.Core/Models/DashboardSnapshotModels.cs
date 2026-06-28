namespace PLCPak.Core.Models;

public sealed class DashboardSnapshot
{
    public PublishStats StatsSummary { get; set; } = new();
    public PublishQueueSnapshot PublishQueue { get; set; } = new();
    public TgPendingSnapshot TgPending { get; set; } = new();
    public PublishWorkflowSnapshot Workflow { get; set; } = new();
    public JobHealthReport JobHealth { get; set; } = new();
    public string SummaryText { get; set; } = string.Empty;
}