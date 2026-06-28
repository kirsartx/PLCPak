using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class BatchCopySnapshot
{
    public int Success { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public List<string> Messages { get; set; } = [];
}

public static class PublishQueueService
{
    public static PublishQueueSnapshot BuildQueue(IEnumerable<PublishJob> jobs, int limit = 50)
    {
        var take = Math.Max(1, limit);
        var pending = jobs
            .Where(PublishDashboardService.IsPendingPublish)
            .OrderByDescending(j => j.UpdatedAt)
            .Take(take)
            .Select(ToEntry)
            .ToList();

        var copyCount = pending.Count(e => e.HasCopy);

        return new PublishQueueSnapshot
        {
            Count = pending.Count,
            SummaryText = $"待发布队列 {pending.Count} 个 | 已生成文案 {copyCount} 个",
            Entries = pending
        };
    }

    public static BatchCopySnapshot BuildBatchCopySummary(
        IEnumerable<PublishJob> jobs,
        Func<PublishJob, bool> tryCopy,
        Func<PublishJob, string?>? skipReason = null)
    {
        var snapshot = new BatchCopySnapshot();

        foreach (var job in jobs)
        {
            var skip = skipReason?.Invoke(job);
            if (skip is not null)
            {
                snapshot.Skipped++;
                snapshot.Messages.Add($"[跳过] {job.Title}: {skip}");
                continue;
            }

            try
            {
                if (tryCopy(job))
                {
                    snapshot.Success++;
                    snapshot.Messages.Add($"[成功] {job.Title}");
                }
                else
                {
                    snapshot.Failed++;
                    snapshot.Messages.Add($"[失败] {job.Title}");
                }
            }
            catch (Exception ex)
            {
                snapshot.Failed++;
                snapshot.Messages.Add($"[失败] {job.Title}: {ex.Message}");
            }
        }

        return snapshot;
    }

    private static PublishQueueEntry ToEntry(PublishJob job)
    {
        var baiduReady = !string.IsNullOrWhiteSpace(job.Publish.Baidu.Link);
        var quarkReady = !string.IsNullOrWhiteSpace(job.Publish.Quark.Link);
        var telegramReady = !string.IsNullOrWhiteSpace(job.Publish.Telegram.Link);
        var readyChannelCount = (baiduReady ? 1 : 0) + (quarkReady ? 1 : 0) + (telegramReady ? 1 : 0);

        return new PublishQueueEntry
        {
            JobId = job.Id,
            Title = job.Title,
            Slug = job.Paths.Slug,
            Status = job.Status,
            UpdatedAt = job.UpdatedAt,
            PublishSummary = job.PublishStatusLabel,
            HasCopy = !string.IsNullOrWhiteSpace(job.Publish.GeneratedCopy),
            BaiduReady = baiduReady,
            QuarkReady = quarkReady,
            TelegramReady = telegramReady,
            ReadyChannelCount = readyChannelCount
        };
    }
}