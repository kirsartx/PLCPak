using System.Text.Json;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class JobMergeService
{
    public static JobMergeResult PreviewMerge(PublishJob target, PublishJob source)
    {
        var targetCopy = CloneJob(target);
        var sourceCopy = CloneJob(source);
        return Merge(targetCopy, sourceCopy, archiveSource: true);
    }

    public static JobMergeResult Merge(PublishJob target, PublishJob source, bool archiveSource = true)
    {
        if (string.IsNullOrWhiteSpace(target.Id) || string.IsNullOrWhiteSpace(source.Id))
        {
            return new JobMergeResult
            {
                Success = false,
                Error = "任务 ID 无效"
            };
        }

        if (target.Id.Equals(source.Id, StringComparison.OrdinalIgnoreCase))
        {
            return new JobMergeResult
            {
                Success = false,
                Error = "不能合并同一个任务",
                TargetJobId = target.Id,
                SourceJobId = source.Id
            };
        }

        var mergedFields = new List<string>();

        if (FillIfEmpty(target.Source.ThreadUrl, source.Source.ThreadUrl, out var threadUrl))
        {
            target.Source.ThreadUrl = threadUrl;
            mergedFields.Add(nameof(JobSource.ThreadUrl));
        }

        if (FillIfEmpty(target.Source.ArchivePassword, source.Source.ArchivePassword, out var archivePassword))
        {
            target.Source.ArchivePassword = archivePassword;
            mergedFields.Add(nameof(JobSource.ArchivePassword));
        }

        if (FillIfEmpty(target.Source.DownloadHint, source.Source.DownloadHint, out var downloadHint))
        {
            target.Source.DownloadHint = downloadHint;
            mergedFields.Add(nameof(JobSource.DownloadHint));
        }

        if (!string.IsNullOrWhiteSpace(source.Notes))
        {
            if (string.IsNullOrWhiteSpace(target.Notes))
            {
                target.Notes = source.Notes.Trim();
                mergedFields.Add(nameof(PublishJob.Notes));
            }
            else if (!target.Notes.Contains(source.Notes.Trim(), StringComparison.Ordinal))
            {
                target.Notes = $"{target.Notes.Trim()}\n---\n{source.Notes.Trim()}";
                mergedFields.Add(nameof(PublishJob.Notes));
            }
        }

        MergePublishChannel(target.Publish.Baidu, source.Publish.Baidu, "Baidu", mergedFields);
        MergePublishChannel(target.Publish.Quark, source.Publish.Quark, "Quark", mergedFields);
        MergePublishChannel(target.Publish.Telegram, source.Publish.Telegram, "Telegram", mergedFields);

        if (string.IsNullOrWhiteSpace(target.Publish.GeneratedCopy)
            && !string.IsNullOrWhiteSpace(source.Publish.GeneratedCopy))
        {
            target.Publish.GeneratedCopy = source.Publish.GeneratedCopy;
            target.Publish.CopyGeneratedAt = source.Publish.CopyGeneratedAt;
            if (string.IsNullOrWhiteSpace(target.Publish.TemplateId) && !string.IsNullOrWhiteSpace(source.Publish.TemplateId))
                target.Publish.TemplateId = source.Publish.TemplateId;
            mergedFields.Add(nameof(JobPublishState.GeneratedCopy));
        }

        var action = archiveSource ? "已归档源任务" : "已删除源任务";
        return new JobMergeResult
        {
            Success = true,
            TargetJobId = target.Id,
            SourceJobId = source.Id,
            Message = mergedFields.Count == 0
                ? $"未合并任何字段（目标任务字段已齐全），{action}"
                : $"已合并 {mergedFields.Count} 个字段: {string.Join(", ", mergedFields)}，{action}",
            MergedFields = mergedFields
        };
    }

    private static void MergePublishChannel(
        PublishChannelState target,
        PublishChannelState source,
        string prefix,
        List<string> mergedFields)
    {
        if (FillIfEmpty(target.Link, source.Link, out var link))
        {
            target.Link = link;
            mergedFields.Add($"{prefix}.Link");
        }

        if (FillIfEmpty(target.Password, source.Password, out var password))
        {
            target.Password = password;
            mergedFields.Add($"{prefix}.Password");
        }
    }

    private static bool FillIfEmpty(string targetValue, string sourceValue, out string value)
    {
        value = sourceValue.Trim();
        if (string.IsNullOrWhiteSpace(targetValue) && !string.IsNullOrWhiteSpace(value))
            return true;

        value = string.Empty;
        return false;
    }

    private static PublishJob CloneJob(PublishJob job)
    {
        var json = JsonSerializer.Serialize(job, JsonHelper.Options);
        return JsonSerializer.Deserialize<PublishJob>(json, JsonHelper.Options)
            ?? throw new InvalidOperationException("无法克隆任务用于合并预览");
    }
}