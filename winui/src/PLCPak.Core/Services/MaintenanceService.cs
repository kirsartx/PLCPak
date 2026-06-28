using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class MaintenanceService
{
    public static MaintenanceReport BuildReport(IEnumerable<PublishJob> jobs, int staleDays = 7)
    {
        var list = jobs as IReadOnlyList<PublishJob> ?? jobs.ToList();
        var staleJobs = GetStaleJobs(list, staleDays);
        var publishedReady = list.Count(j => j.Status == JobStatus.Published);

        return new MaintenanceReport
        {
            StaleCount = staleJobs.Count,
            PublishedReadyToArchive = publishedReady,
            StaleJobs = staleJobs,
            SummaryText = BuildSummaryText(staleJobs.Count, publishedReady)
        };
    }

    public static List<StaleJobEntry> GetStaleJobs(IEnumerable<PublishJob> jobs, int staleDays = 7)
    {
        var threshold = DateTime.Now.AddDays(-Math.Max(1, staleDays));
        return jobs
            .Where(j => j.Status == JobStatus.Processed
                && !HasFullLinks(j)
                && j.UpdatedAt < threshold)
            .OrderBy(j => j.UpdatedAt)
            .Select(j => new StaleJobEntry
            {
                JobId = j.Id,
                Title = j.Title,
                DaysSinceUpdate = Math.Max(0, (int)Math.Floor((DateTime.Now - j.UpdatedAt).TotalDays)),
                Reason = DescribeMissingLinks(j)
            })
            .ToList();
    }

    public static BulkArchiveResult BulkArchivePublished(
        IEnumerable<PublishJob> jobs,
        JobStore jobStore,
        int olderThanDays = 0)
    {
        var result = new BulkArchiveResult();
        var cutoff = olderThanDays > 0 ? DateTime.Now.AddDays(-olderThanDays) : (DateTime?)null;

        foreach (var job in jobs)
        {
            if (job.Status != JobStatus.Published)
            {
                result.Skipped++;
                result.Messages.Add($"[跳过] {job.Title}: 非已发布状态");
                continue;
            }

            if (cutoff is not null && job.UpdatedAt >= cutoff.Value)
            {
                result.Skipped++;
                result.Messages.Add($"[跳过] {job.Title}: 更新未满 {olderThanDays} 天");
                continue;
            }

            jobStore.Archive(job.Id);
            result.Archived++;
            result.Messages.Add($"[归档] {job.Title}");
        }

        return result;
    }

    internal static bool HasFullLinks(PublishJob job)
        => !string.IsNullOrWhiteSpace(job.Publish.Baidu.Link)
            && !string.IsNullOrWhiteSpace(job.Publish.Quark.Link)
            && !string.IsNullOrWhiteSpace(job.Publish.Telegram.Link);

    private static string DescribeMissingLinks(PublishJob job)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(job.Publish.Baidu.Link))
            missing.Add("百度");
        if (string.IsNullOrWhiteSpace(job.Publish.Quark.Link))
            missing.Add("夸克");
        if (string.IsNullOrWhiteSpace(job.Publish.Telegram.Link))
            missing.Add("TG");

        return missing.Count == 0
            ? "长期未更新"
            : $"缺少链接: {string.Join("/", missing)}";
    }

    private static string BuildSummaryText(int staleCount, int publishedReady)
    {
        if (staleCount == 0 && publishedReady == 0)
            return "维护检查：无待处理项";

        var parts = new List<string>();
        if (staleCount > 0)
            parts.Add($"长期待发布 {staleCount} 个");
        if (publishedReady > 0)
            parts.Add($"可归档已发布 {publishedReady} 个");

        return $"维护检查：{string.Join("，", parts)}";
    }
}