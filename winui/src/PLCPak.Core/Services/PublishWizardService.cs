using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class PublishWizardService
{
    private static readonly (int Order, string Key, string Title, string Description)[] StepDefinitions =
    [
        (1, "inbox", "放入压缩包", "将下载的压缩包放入 inbox 目录"),
        (2, "pipeline", "执行流水线", "解压、去广告并压缩"),
        (3, "links", "回填链接", "补齐百度/夸克/TG 分享链接"),
        (4, "copy", "生成文案", "根据模板生成发布文案"),
        (5, "publish", "标记发布", "将各渠道标记为已发布"),
        (6, "telegram", "发送 Telegram", "通过 Bot 发送 TG 文案")
    ];

    public static PublishWizardState BuildState(PublishJob? job)
    {
        if (job is null)
        {
            return new PublishWizardState
            {
                Steps = CreateDefaultSteps(WizardStepStatus.Pending),
                SummaryText = "发布向导：请选择或创建任务"
            };
        }

        var steps = new List<WizardStep>
        {
            CreateStep("inbox", ResolveInboxStatus(job)),
            CreateStep("pipeline", ResolvePipelineStatus(job)),
            CreateStep("links", ResolveLinksStatus(job)),
            CreateStep("copy", ResolveCopyStatus(job)),
            CreateStep("publish", ResolvePublishStatus(job)),
            CreateStep("telegram", ResolveTelegramStatus(job))
        };

        var nextAction = PublishWorkflowService.GetNextActionForJob(job);

        return new PublishWizardState
        {
            JobId = job.Id,
            Title = job.Title,
            Steps = steps,
            SummaryText = BuildSummaryText(job, steps, nextAction),
            CurrentAction = nextAction?.Action
        };
    }

    private static List<WizardStep> CreateDefaultSteps(WizardStepStatus status)
        => StepDefinitions
            .Select(def => new WizardStep
            {
                Order = def.Order,
                Key = def.Key,
                Title = def.Title,
                Description = def.Description,
                Status = status
            })
            .ToList();

    private static WizardStep CreateStep(string key, WizardStepStatus status)
    {
        var def = StepDefinitions.First(s => s.Key == key);
        return new WizardStep
        {
            Order = def.Order,
            Key = def.Key,
            Title = def.Title,
            Description = def.Description,
            Status = status
        };
    }

    private static WizardStepStatus ResolveInboxStatus(PublishJob job)
    {
        if (HasArchives(job))
            return WizardStepStatus.Done;

        if (job.Status is JobStatus.Draft or JobStatus.InboxReady)
            return WizardStepStatus.Active;

        if (job.Status is JobStatus.Extracting or JobStatus.Extracted or JobStatus.Processing
            or JobStatus.Processed or JobStatus.Published)
            return WizardStepStatus.Done;

        return WizardStepStatus.Skipped;
    }

    private static WizardStepStatus ResolvePipelineStatus(PublishJob job)
    {
        if (IsPipelineDone(job))
            return WizardStepStatus.Done;

        if (!HasArchives(job))
            return WizardStepStatus.Blocked;

        if (job.Status is JobStatus.InboxReady or JobStatus.Extracted or JobStatus.Extracting
            or JobStatus.Processing or JobStatus.Failed)
            return WizardStepStatus.Active;

        if (job.Status == JobStatus.Draft && HasArchives(job))
            return WizardStepStatus.Active;

        return WizardStepStatus.Pending;
    }

    private static WizardStepStatus ResolveLinksStatus(PublishJob job)
    {
        if (HasAllLinks(job))
            return WizardStepStatus.Done;

        if (job.Status == JobStatus.Processed)
            return WizardStepStatus.Active;

        return WizardStepStatus.Pending;
    }

    private static WizardStepStatus ResolveCopyStatus(PublishJob job)
    {
        if (!string.IsNullOrWhiteSpace(job.Publish.GeneratedCopy))
            return WizardStepStatus.Done;

        if (HasAllLinks(job))
            return WizardStepStatus.Active;

        return WizardStepStatus.Pending;
    }

    private static WizardStepStatus ResolvePublishStatus(PublishJob job)
    {
        if (PublishStatusHelper.IsFullyPublished(job.Publish))
            return WizardStepStatus.Done;

        if (!string.IsNullOrWhiteSpace(job.Publish.GeneratedCopy))
            return WizardStepStatus.Active;

        return WizardStepStatus.Pending;
    }

    private static WizardStepStatus ResolveTelegramStatus(PublishJob job)
    {
        if (job.Publish.Telegram.Status == PublishStatusHelper.Published)
            return WizardStepStatus.Done;

        if (IsBaiduQuarkPublished(job))
            return WizardStepStatus.Active;

        return WizardStepStatus.Pending;
    }

    private static string BuildSummaryText(
        PublishJob job,
        IReadOnlyList<WizardStep> steps,
        JobNextActionEntry? nextAction)
    {
        var active = steps.FirstOrDefault(step => step.Status == WizardStepStatus.Active);
        if (active is not null)
            return $"当前步骤：{active.Title} — {active.Description}";

        if (nextAction is not null)
            return $"建议：{nextAction.ActionLabel} — {nextAction.Reason}";

        if (job.Status == JobStatus.Published && PublishStatusHelper.IsFullyPublished(job.Publish))
            return "发布向导：所有步骤已完成";

        return $"发布向导：{job.Title}";
    }

    private static bool HasArchives(PublishJob job) => job.Artifacts.InboxArchives.Count > 0;

    private static bool HasAllLinks(PublishJob job)
        => !string.IsNullOrWhiteSpace(job.Publish.Baidu.Link)
            && !string.IsNullOrWhiteSpace(job.Publish.Quark.Link)
            && !string.IsNullOrWhiteSpace(job.Publish.Telegram.Link);

    private static bool IsPipelineDone(PublishJob job)
        => job.Status is JobStatus.Processed or JobStatus.Published or JobStatus.Archived;

    private static bool IsBaiduQuarkPublished(PublishJob job)
        => job.Publish.Baidu.Status == PublishStatusHelper.Published
            && job.Publish.Quark.Status == PublishStatusHelper.Published;
}