using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class TgPreviewService
{
    private const int PreviewMaxLength = 500;

    public static TgPreviewSnapshot BuildPreview(IEnumerable<PublishJob> jobs, int? limit = null)
    {
        var pending = TgPendingService.GetPending(jobs, limit);
        var entries = pending.List
            .Select(entry => BuildEntry(entry, FindJob(jobs, entry.JobId)))
            .ToList();

        var multiPart = entries.Count(e => e.PartCount > 1);

        return new TgPreviewSnapshot
        {
            Count = entries.Count,
            Entries = entries,
            SummaryText = entries.Count == 0
                ? "TG 预览：暂无待发任务"
                : $"TG 预览 {entries.Count} 个 | 分条 {multiPart} 个"
        };
    }

    public static TgPreviewEntry? BuildEntryForJob(PublishJob job)
    {
        var pending = TgPendingService.GetPending([job], limit: 1);
        if (pending.Count == 0)
            return null;

        return BuildEntry(pending.List[0], job);
    }

    private static PublishJob? FindJob(IEnumerable<PublishJob> jobs, string jobId)
        => jobs.FirstOrDefault(job => job.Id == jobId);

    private static TgPreviewEntry BuildEntry(TgPendingEntry pending, PublishJob? job)
    {
        var copy = job?.Publish.GeneratedCopy ?? string.Empty;
        if (string.IsNullOrWhiteSpace(copy))
        {
            return new TgPreviewEntry
            {
                JobId = pending.JobId,
                Title = pending.Title,
                PreviewText = pending.Reason
            };
        }

        var (chunks, wasTruncated) = TelegramBotService.SplitMessageChunks(copy);

        return new TgPreviewEntry
        {
            JobId = pending.JobId,
            Title = pending.Title,
            OriginalLength = copy.Length,
            PartCount = chunks.Count,
            WasTruncated = wasTruncated,
            PreviewText = BuildPreviewText(copy, chunks)
        };
    }

    private static string BuildPreviewText(string copy, IReadOnlyList<string> chunks)
    {
        if (string.IsNullOrWhiteSpace(copy))
            return "(无文案)";

        var header = chunks.Count > 1
            ? $"[将分 {chunks.Count} 条发送，共 {copy.Length} 字符]\n\n"
            : $"[{copy.Length} 字符]\n\n";

        var body = copy.Length <= PreviewMaxLength
            ? copy
            : copy[..PreviewMaxLength] + "…";

        return header + body;
    }
}