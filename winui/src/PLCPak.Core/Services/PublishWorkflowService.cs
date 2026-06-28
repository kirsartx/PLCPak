using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class PublishWorkflowService
{
    public static JobNextActionEntry? GetNextActionForJob(PublishJob job)
        => TryCreateEntry(job);

    public static PublishWorkflowSnapshot BuildSnapshot(IEnumerable<PublishJob> jobs, int limit = 5)
    {
        var take = Math.Max(1, limit);
        var suggestions = jobs
            .Select(TryCreateEntry)
            .Where(entry => entry is not null)
            .Cast<JobNextActionEntry>()
            .OrderBy(entry => entry.Priority)
            .ThenByDescending(entry => entry.UpdatedAt)
            .Take(take)
            .ToList();

        var primary = suggestions.FirstOrDefault();
        return new PublishWorkflowSnapshot
        {
            Primary = primary,
            Suggestions = primary is null ? [] : suggestions.Skip(1).ToList(),
            SummaryText = primary is null
                ? "发布工作流：暂无建议操作"
                : suggestions.Count == 1
                    ? $"建议操作：{primary.ActionLabel} — {primary.Title}"
                    : $"建议操作 {suggestions.Count} 项（首要：{primary.ActionLabel} — {primary.Title}）"
        };
    }

    private static JobNextActionEntry? TryCreateEntry(PublishJob job)
    {
        if (job.Status == JobStatus.Failed)
        {
            return Entry(job, JobNextActionType.RetryFailed, 1, "重试流水线",
                string.IsNullOrWhiteSpace(job.Error) ? "任务失败，建议重试" : $"任务失败: {job.Error}");
        }

        if (job.Status == JobStatus.Draft
            && !string.IsNullOrWhiteSpace(job.Source.ThreadUrl)
            && job.Artifacts.InboxArchives.Count == 0)
        {
            return Entry(job, JobNextActionType.DownloadInbox, 2, "下载论坛附件",
                "已关联帖子链接，可先下载附件到 inbox");
        }

        if (job.Status is JobStatus.Draft or JobStatus.InboxReady or JobStatus.Extracted)
        {
            var reason = job.Status switch
            {
                JobStatus.Draft => "草稿任务，请放入压缩包并执行流水线",
                JobStatus.InboxReady => "inbox 已有压缩包，可执行流水线",
                JobStatus.Extracted => "已解压，可继续去广告压缩",
                _ => "可执行流水线"
            };
            return Entry(job, JobNextActionType.RunPipeline, 3, "执行流水线", reason);
        }

        if (job.Status == JobStatus.Processed)
        {
            if (string.IsNullOrWhiteSpace(job.Publish.Baidu.Link)
                || string.IsNullOrWhiteSpace(job.Publish.Quark.Link)
                || string.IsNullOrWhiteSpace(job.Publish.Telegram.Link))
            {
                return Entry(job, JobNextActionType.FillLinks, 4, "回填发布链接",
                    "已压缩，请补齐百度/夸克/TG 链接");
            }

            if (string.IsNullOrWhiteSpace(job.Publish.GeneratedCopy))
            {
                return Entry(job, JobNextActionType.GenerateCopy, 5, "生成发布文案",
                    "链接已齐，可生成发布文案");
            }

            if (!PublishStatusHelper.IsFullyPublished(job.Publish))
            {
                return Entry(job, JobNextActionType.MarkPublished, 6, "标记已发布",
                    "文案已生成，可标记渠道已发布");
            }
        }

        if (job.Status == JobStatus.Published
            && !string.IsNullOrWhiteSpace(job.Publish.GeneratedCopy)
            && job.Publish.Baidu.Status == PublishStatusHelper.Published
            && job.Publish.Quark.Status == PublishStatusHelper.Published
            && job.Publish.Telegram.Status != PublishStatusHelper.Published)
        {
            return Entry(job, JobNextActionType.SendTelegram, 7, "发送 TG 文案",
                "百度/夸克已发布，可发送 Telegram 文案");
        }

        return null;
    }

    public static JobNextActionType ResolveAction(PublishJob job, string? actionOverride)
    {
        if (!string.IsNullOrWhiteSpace(actionOverride))
            return ParseActionOverride(actionOverride);

        var entry = TryCreateEntry(job)
            ?? throw new InvalidOperationException("该任务暂无建议的下一步操作");

        if (entry.Action == JobNextActionType.FillLinks)
            throw new InvalidOperationException("建议操作：回填发布链接，请使用 -JobSaveLinks 手动完成");

        return entry.Action;
    }

    public static JobNextActionType ParseActionOverride(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "retry" => JobNextActionType.RetryFailed,
            "download" => JobNextActionType.DownloadInbox,
            "pipeline" => JobNextActionType.RunPipeline,
            "copy" => JobNextActionType.GenerateCopy,
            "mark" => JobNextActionType.MarkPublished,
            "telegram" => JobNextActionType.SendTelegram,
            _ => throw new ArgumentException($"未知操作: {value}。可选: retry, download, pipeline, copy, mark, telegram")
        };
    }

    public static string ActionLabel(JobNextActionType action)
        => action switch
        {
            JobNextActionType.RetryFailed => "重试流水线",
            JobNextActionType.DownloadInbox => "下载论坛附件",
            JobNextActionType.RunPipeline => "执行流水线",
            JobNextActionType.FillLinks => "回填发布链接",
            JobNextActionType.GenerateCopy => "生成发布文案",
            JobNextActionType.MarkPublished => "标记已发布",
            JobNextActionType.SendTelegram => "发送 TG 文案",
            _ => action.ToString()
        };

    private static JobNextActionEntry Entry(
        PublishJob job,
        JobNextActionType action,
        int priority,
        string label,
        string reason)
        => new()
        {
            JobId = job.Id,
            Title = job.Title,
            Action = action,
            ActionLabel = label,
            Reason = reason,
            Priority = priority,
            UpdatedAt = job.UpdatedAt
        };
}