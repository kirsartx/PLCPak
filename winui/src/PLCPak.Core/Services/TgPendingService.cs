using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class TgPendingService
{
    public static TgPendingSnapshot GetPending(IEnumerable<PublishJob> jobs, int? limit = null)
    {
        IEnumerable<TgPendingEntry> query = jobs
            .Select(TryCreateEntry)
            .Where(entry => entry is not null)
            .Cast<TgPendingEntry>()
            .OrderByDescending(entry => entry.UpdatedAt);

        if (limit is > 0)
            query = query.Take(limit.Value);

        var pending = query.ToList();
        var withCopy = pending.Count(e => e.HasCopy);

        return new TgPendingSnapshot
        {
            Count = pending.Count,
            SummaryText = pending.Count == 0
                ? "TG 待发送：暂无任务"
                : $"TG 待发送 {pending.Count} 个 | 已有文案 {withCopy} 个",
            List = pending
        };
    }

    private static TgPendingEntry? TryCreateEntry(PublishJob job)
    {
        if (string.IsNullOrWhiteSpace(job.Publish.GeneratedCopy))
            return null;

        if (job.Publish.Telegram.Status == PublishStatusHelper.Published)
            return null;

        var baiduPublished = job.Publish.Baidu.Status == PublishStatusHelper.Published;
        var quarkPublished = job.Publish.Quark.Status == PublishStatusHelper.Published;
        var baiduReady = baiduPublished || !string.IsNullOrWhiteSpace(job.Publish.Baidu.Link);
        var quarkReady = quarkPublished || !string.IsNullOrWhiteSpace(job.Publish.Quark.Link);

        if (!baiduReady || !quarkReady)
            return null;

        return new TgPendingEntry
        {
            JobId = job.Id,
            Title = job.Title,
            Slug = job.Paths.Slug,
            Status = job.Status,
            UpdatedAt = job.UpdatedAt,
            PublishSummary = job.PublishStatusLabel,
            HasCopy = true,
            BaiduPublished = baiduPublished,
            QuarkPublished = quarkPublished,
            Reason = BuildReason(baiduPublished, quarkPublished)
        };
    }

    private static string BuildReason(bool baiduPublished, bool quarkPublished)
    {
        if (!baiduPublished && !quarkPublished)
            return "百度/夸克链接已填，TG 待发送";

        if (!baiduPublished)
            return "夸克已发布，百度链接已填，TG 待发送";

        if (!quarkPublished)
            return "百度已发布，夸克链接已填，TG 待发送";

        return "百度/夸克已发布，TG 待发送";
    }
}