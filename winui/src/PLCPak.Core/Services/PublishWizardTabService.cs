using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class PublishWizardTabService
{
    private static readonly (PublishWizardTab Tab, string Id, string Label)[] TabDefs =
    [
        (PublishWizardTab.Prepare, "prepare", "素材准备"),
        (PublishWizardTab.Links, "links", "链接回填"),
        (PublishWizardTab.Copy, "copy", "文案生成"),
        (PublishWizardTab.Publish, "publish", "渠道发布")
    ];

    public static JobWizardStateSnapshot BuildForJob(PublishJob job)
    {
        var completes = new[]
        {
            IsPrepareComplete(job),
            IsLinksComplete(job),
            IsCopyComplete(job),
            IsPublishComplete(job)
        };

        var currentIndex = 0;
        for (var i = 0; i < completes.Length; i++)
        {
            if (!completes[i])
            {
                currentIndex = i;
                break;
            }

            currentIndex = i;
        }

        if (completes.All(c => c))
            currentIndex = (int)PublishWizardTab.Publish;

        var tabs = new List<PublishWizardTabState>();
        for (var i = 0; i < TabDefs.Length; i++)
        {
            var def = TabDefs[i];
            tabs.Add(new PublishWizardTabState
            {
                Index = (int)def.Tab,
                Id = def.Id,
                Label = def.Label,
                IsComplete = completes[i],
                IsCurrent = i == currentIndex,
                Summary = BuildTabSummary(job, def.Tab, completes[i])
            });
        }

        var currentDef = TabDefs[currentIndex];
        var nextAction = PublishWorkflowService.GetNextActionForJob(job);

        return new JobWizardStateSnapshot
        {
            JobId = job.Id,
            Title = job.Title,
            Status = JobStatusDisplayHelper.ToLocalized(job.Status),
            CurrentTabIndex = currentIndex,
            CurrentTabId = currentDef.Id,
            CurrentTabLabel = currentDef.Label,
            Tabs = tabs,
            SummaryText = $"发布向导：{currentDef.Label}（{tabs[currentIndex].Summary}）",
            NextAction = nextAction
        };
    }

    public static JobWizardStateListSnapshot BuildForJobs(IEnumerable<PublishJob> jobs)
    {
        var snapshots = jobs
            .Where(j => j.Status != JobStatus.Archived)
            .OrderByDescending(j => j.UpdatedAt)
            .Select(BuildForJob)
            .ToList();

        var inProgress = snapshots.Count(s => !s.Tabs.All(t => t.IsComplete));
        return new JobWizardStateListSnapshot
        {
            Jobs = snapshots,
            SummaryText = snapshots.Count == 0
                ? "发布向导：暂无任务"
                : $"发布向导：{snapshots.Count} 个任务，{inProgress} 个进行中"
        };
    }

    private static bool IsPrepareComplete(PublishJob job)
        => job.Status is JobStatus.Processed or JobStatus.Published;

    private static bool IsLinksComplete(PublishJob job)
        => !string.IsNullOrWhiteSpace(job.Publish.Baidu.Link)
            && !string.IsNullOrWhiteSpace(job.Publish.Quark.Link)
            && !string.IsNullOrWhiteSpace(job.Publish.Telegram.Link);

    private static bool IsCopyComplete(PublishJob job)
        => !string.IsNullOrWhiteSpace(job.Publish.GeneratedCopy);

    private static bool IsPublishComplete(PublishJob job)
        => PublishStatusHelper.IsFullyPublished(job.Publish);

    private static string BuildTabSummary(PublishJob job, PublishWizardTab tab, bool complete)
    {
        if (complete)
            return "已完成";

        return tab switch
        {
            PublishWizardTab.Prepare => job.Status switch
            {
                JobStatus.Failed => $"失败: {job.Error ?? "未知错误"}",
                JobStatus.Draft => job.Artifacts.InboxArchives.Count == 0
                    ? "请放入 inbox 压缩包"
                    : "可执行流水线",
                JobStatus.InboxReady or JobStatus.Extracted => "可执行流水线",
                JobStatus.Processing or JobStatus.Extracting => "处理中",
                _ => JobStatusDisplayHelper.ToLocalized(job.Status)
            },
            PublishWizardTab.Links => BuildLinksSummary(job),
            PublishWizardTab.Copy => "请生成发布文案",
            PublishWizardTab.Publish => PublishStatusHelper.BuildSummary(job.Publish),
            _ => string.Empty
        };
    }

    private static string BuildLinksSummary(PublishJob job)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(job.Publish.Baidu.Link))
            missing.Add("百度");
        if (string.IsNullOrWhiteSpace(job.Publish.Quark.Link))
            missing.Add("夸克");
        if (string.IsNullOrWhiteSpace(job.Publish.Telegram.Link))
            missing.Add("TG");

        return missing.Count == 0
            ? "链接已齐"
            : $"待填: {string.Join("、", missing)}";
    }
}