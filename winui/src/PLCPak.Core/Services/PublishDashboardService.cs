using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class PublishDashboardService
{
    public static readonly IReadOnlyList<(JobListFilter Filter, string Label)> FilterOptions =
    [
        (JobListFilter.All, "全部"),
        (JobListFilter.PendingPublish, "待发布"),
        (JobListFilter.Published, "已发布"),
        (JobListFilter.Processed, "已压缩"),
        (JobListFilter.Active, "进行中"),
        (JobListFilter.Failed, "失败"),
        (JobListFilter.Archived, "已归档")
    ];

    public static PublishStats ComputeStats(IEnumerable<PublishJob> jobs)
    {
        var list = jobs as IReadOnlyList<PublishJob> ?? jobs.ToList();
        return new PublishStats
        {
            Total = list.Count,
            Published = list.Count(j => j.Status == JobStatus.Published),
            PendingPublish = list.Count(IsPendingPublish),
            Processed = list.Count(j => j.Status == JobStatus.Processed),
            Failed = list.Count(j => j.Status == JobStatus.Failed),
            Active = list.Count(IsActive),
            Archived = list.Count(j => j.Status == JobStatus.Archived)
        };
    }

    public static IReadOnlyList<PublishJob> Filter(IEnumerable<PublishJob> jobs, JobListFilter filter)
    {
        return filter switch
        {
            JobListFilter.PendingPublish => jobs.Where(IsPendingPublish).ToList(),
            JobListFilter.Published => jobs.Where(j => j.Status == JobStatus.Published).ToList(),
            JobListFilter.Processed => jobs.Where(j => j.Status == JobStatus.Processed).ToList(),
            JobListFilter.Failed => jobs.Where(j => j.Status == JobStatus.Failed).ToList(),
            JobListFilter.Active => jobs.Where(IsActive).ToList(),
            JobListFilter.Archived => jobs.Where(j => j.Status == JobStatus.Archived).ToList(),
            _ => jobs.ToList()
        };
    }

    public static bool IsPendingPublish(PublishJob job)
        => job.Status == JobStatus.Processed;

    public static bool IsActive(PublishJob job)
        => job.Status is JobStatus.Draft
            or JobStatus.InboxReady
            or JobStatus.Extracting
            or JobStatus.Extracted
            or JobStatus.Processing;

    public static bool ShouldIncludeInHistory(PublishJob job)
        => job.Status == JobStatus.Published
            || job.Publish.Baidu.Status == PublishStatusHelper.Published
            || job.Publish.Quark.Status == PublishStatusHelper.Published
            || job.Publish.Telegram.Status == PublishStatusHelper.Published;

    public static List<PublishHistoryEntry> BuildHistory(IEnumerable<PublishJob> jobs)
        => jobs.Where(ShouldIncludeInHistory)
            .Select(ToHistoryEntry)
            .OrderByDescending(e => e.UpdatedAt)
            .ToList();

    public static PublishHistoryEntry ToHistoryEntry(PublishJob job) => new()
    {
        JobId = job.Id,
        Title = job.Title,
        Slug = job.Paths.Slug,
        Status = job.Status.ToString(),
        PublishSummary = job.PublishStatusLabel,
        BaiduLink = job.Publish.Baidu.Link,
        QuarkLink = job.Publish.Quark.Link,
        TelegramLink = job.Publish.Telegram.Link,
        BaiduPublishedAt = job.Publish.Baidu.PublishedAt,
        QuarkPublishedAt = job.Publish.Quark.PublishedAt,
        TelegramPublishedAt = job.Publish.Telegram.PublishedAt,
        CopyGeneratedAt = job.Publish.CopyGeneratedAt,
        UpdatedAt = job.UpdatedAt
    };

    public static string ToCsv(IEnumerable<PublishHistoryEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("jobId,title,slug,status,publishSummary,baiduLink,quarkLink,telegramLink,baiduPublishedAt,quarkPublishedAt,telegramPublishedAt,copyGeneratedAt,updatedAt");
        foreach (var entry in entries)
        {
            sb.Append(Csv(entry.JobId)).Append(',');
            sb.Append(Csv(entry.Title)).Append(',');
            sb.Append(Csv(entry.Slug)).Append(',');
            sb.Append(Csv(entry.Status)).Append(',');
            sb.Append(Csv(entry.PublishSummary)).Append(',');
            sb.Append(Csv(entry.BaiduLink)).Append(',');
            sb.Append(Csv(entry.QuarkLink)).Append(',');
            sb.Append(Csv(entry.TelegramLink)).Append(',');
            sb.Append(Csv(FormatDate(entry.BaiduPublishedAt))).Append(',');
            sb.Append(Csv(FormatDate(entry.QuarkPublishedAt))).Append(',');
            sb.Append(Csv(FormatDate(entry.TelegramPublishedAt))).Append(',');
            sb.Append(Csv(FormatDate(entry.CopyGeneratedAt))).Append(',');
            sb.Append(Csv(FormatDate(entry.UpdatedAt)));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static List<RecentActivityEntry> GetRecentActivity(IEnumerable<PublishJob> jobs, int limit = 20)
    {
        var take = Math.Max(1, limit);
        return jobs
            .OrderByDescending(j => j.UpdatedAt)
            .Take(take)
            .Select(j => new RecentActivityEntry
            {
                JobId = j.Id,
                Title = j.Title,
                Status = j.Status,
                StatusLabel = JobStatusDisplayHelper.ToLocalized(j.Status),
                UpdatedAt = j.UpdatedAt,
                LastLogLine = j.Log.Count > 0 ? j.Log[^1] : null
            })
            .ToList();
    }

    public static JobListFilter ParseFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return JobListFilter.PendingPublish;

        return value.Trim().ToLowerInvariant() switch
        {
            "all" or "全部" => JobListFilter.All,
            "pending" or "pendingpublish" or "待发布" => JobListFilter.PendingPublish,
            "published" or "已发布" => JobListFilter.Published,
            "processed" or "已压缩" => JobListFilter.Processed,
            "active" or "进行中" => JobListFilter.Active,
            "failed" or "失败" => JobListFilter.Failed,
            "archived" or "已归档" => JobListFilter.Archived,
            _ => JobListFilter.PendingPublish
        };
    }

    private static string FormatDate(DateTime? value)
        => value?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            return $"\"{text.Replace("\"", "\"\"")}\"";
        return text;
    }
}