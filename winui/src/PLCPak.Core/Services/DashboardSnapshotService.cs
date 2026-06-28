using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class DashboardSnapshotService
{
    public static DashboardSnapshot Build(
        IEnumerable<PublishJob> jobs,
        int publishQueueLimit = 20,
        int tgLimit = 50,
        int workflowLimit = 5)
    {
        var list = jobs as IReadOnlyList<PublishJob> ?? jobs.ToList();
        var stats = PublishDashboardService.ComputeStats(list);
        var queue = PublishQueueService.BuildQueue(list, publishQueueLimit);
        var tgPending = TgPendingService.GetPending(list, tgLimit);
        var workflow = PublishWorkflowService.BuildSnapshot(list, workflowLimit);
        var health = JobHealthService.Compute(list);

        return new DashboardSnapshot
        {
            StatsSummary = stats,
            PublishQueue = queue,
            TgPending = tgPending,
            Workflow = workflow,
            JobHealth = health,
            SummaryText = BuildSummaryText(stats, queue, tgPending, workflow, health)
        };
    }

    private static string BuildSummaryText(
        PublishStats stats,
        PublishQueueSnapshot queue,
        TgPendingSnapshot tgPending,
        PublishWorkflowSnapshot workflow,
        JobHealthReport health)
    {
        var workflowHint = workflow.Primary is null
            ? "暂无建议操作"
            : workflow.Primary.ActionLabel;

        return $"{stats.SummaryText} | 待发布队列 {queue.Count} | TG待发 {tgPending.Count} | 健康 {health.IssueCount} 项 | 首要: {workflowHint}";
    }
}