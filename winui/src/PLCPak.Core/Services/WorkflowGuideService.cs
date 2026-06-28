using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class WorkflowGuideService
{
    public static WorkflowGuideSnapshot Build(IEnumerable<PublishJob> jobs, int staleDays = 7)
    {
        var list = jobs as IReadOnlyList<PublishJob> ?? jobs.ToList();
        var workflow = PublishWorkflowService.BuildSnapshot(list, 1);
        var stats = PublishDashboardService.ComputeStats(list);
        var tgPending = TgPendingService.GetPending(list, limit: 100).Count;
        var stale = MaintenanceService.BuildReport(list, staleDays).StaleCount;
        var primary = workflow.Primary;

        var page = ResolveRecommendedPage(primary?.Action ?? JobNextActionType.None);
        var pageLabel = PageLabel(page);

        return new WorkflowGuideSnapshot
        {
            SummaryText = primary is null
                ? "暂无待办。可去任务工作台新建任务，或去发布看板查看队列。"
                : $"{primary.ActionLabel} — {primary.Title}",
            ActionLabel = primary?.ActionLabel ?? string.Empty,
            Reason = primary?.Reason ?? string.Empty,
            JobId = primary?.JobId,
            JobTitle = primary?.Title,
            Action = primary?.Action ?? JobNextActionType.None,
            RecommendedPage = page,
            RecommendedPageLabel = pageLabel,
            JobsBadgeCount = stats.PendingPublish + stats.Failed,
            TgPendingCount = tgPending,
            StaleCount = stale
        };
    }

    public static string ResolveRecommendedPage(JobNextActionType action)
        => action switch
        {
            JobNextActionType.None => "dashboard",
            JobNextActionType.SendTelegram => "dashboard",
            JobNextActionType.RetryFailed
                or JobNextActionType.DownloadInbox
                or JobNextActionType.RunPipeline
                or JobNextActionType.FillLinks
                or JobNextActionType.GenerateCopy
                or JobNextActionType.MarkPublished => "jobs",
            _ => "dashboard"
        };

    public static string PageLabel(string page)
        => page.Trim().ToLowerInvariant() switch
        {
            "quick" => "快速处理",
            "jobs" => "任务工作台",
            "dashboard" => "发布看板",
            "wizard" => "发布向导",
            "operations" => "运维中心",
            _ => "发布看板"
        };
}