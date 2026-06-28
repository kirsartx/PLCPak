using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class OperationsCenterService
{
    public static OperationsCenterSnapshot BuildSnapshotFromJobs(
        IReadOnlyList<PublishJob> jobs,
        int workflowLimit = 5,
        int queueLimit = 20,
        int staleDays = 7)
        => BuildSnapshot(jobs, workflowLimit, queueLimit, staleDays);

    public static OperationsCenterSnapshot BuildSnapshot(
        IEnumerable<PublishJob> jobs,
        int workflowLimit = 5,
        int queueLimit = 20,
        int staleDays = 7)
    {
        var list = jobs as IReadOnlyList<PublishJob> ?? jobs.ToList();
        var quickStats = QuickStatsService.BuildOneLiner(list);
        var workflow = PublishWorkflowService.BuildSnapshot(list, workflowLimit);
        var maintenance = MaintenanceService.BuildReport(list, staleDays);
        var health = JobHealthService.Compute(list);
        var queue = PublishQueueService.BuildQueue(list, queueLimit);

        var sections = new List<OperationsCenterSection>
        {
            new()
            {
                Key = "quick-stats",
                Title = "概览",
                SummaryText = quickStats
            },
            new()
            {
                Key = "workflow",
                Title = "发布工作流",
                SummaryText = workflow.SummaryText
            },
            new()
            {
                Key = "maintenance",
                Title = "维护",
                SummaryText = maintenance.SummaryText
            },
            new()
            {
                Key = "health",
                Title = "健康检查",
                SummaryText = health.SummaryText
            },
            new()
            {
                Key = "queue",
                Title = "待发布队列",
                SummaryText = queue.SummaryText
            }
        };

        return new OperationsCenterSnapshot
        {
            QuickStatsLine = quickStats,
            Workflow = workflow,
            Maintenance = maintenance,
            Health = health,
            Queue = queue,
            Sections = sections,
            SummaryText = BuildSummaryText(quickStats, workflow, maintenance, health, queue)
        };
    }

    private static string BuildSummaryText(
        string quickStats,
        PublishWorkflowSnapshot workflow,
        MaintenanceReport maintenance,
        JobHealthReport health,
        PublishQueueSnapshot queue)
    {
        var workflowHint = workflow.Primary is null
            ? "暂无建议操作"
            : workflow.Primary.ActionLabel;

        return $"{quickStats} | 队列 {queue.Count} | 健康 {health.IssueCount} 项 | {maintenance.SummaryText} | 首要: {workflowHint}";
    }

    public static IReadOnlyList<string> FormatSectionLines(OperationsCenterSnapshot snapshot)
        => snapshot.Sections
            .Select(section => $"{section.Title}: {section.SummaryText}")
            .ToList();
}