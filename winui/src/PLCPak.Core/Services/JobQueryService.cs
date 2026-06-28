using System.Text.RegularExpressions;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class JobQueryService
{
    private static readonly Regex BaiduLinkSlugPattern = new(@"/s/([^/?#]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    public static IReadOnlyList<PublishJob> Query(
        IEnumerable<PublishJob> jobs,
        JobListFilter filter,
        string? searchText,
        JobListSort sort = JobListSort.Updated,
        string? tagFilter = null)
    {
        var filtered = PublishDashboardService.Filter(jobs, filter);
        var tagged = FilterByTag(filtered, tagFilter);
        var searched = Search(tagged, searchText);
        return Sort(searched, sort);
    }

    public static IReadOnlyList<PublishJob> FilterByTag(IEnumerable<PublishJob> jobs, string? tagFilter)
    {
        var tag = tagFilter?.Trim();
        if (string.IsNullOrWhiteSpace(tag))
            return jobs.ToList();

        if (tag.Contains(','))
        {
            var requiredTags = tag.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (requiredTags.Count == 0)
                return jobs.ToList();

            return jobs.Where(job => requiredTags.All(required =>
                    job.Tags.Any(jobTag => jobTag.Equals(required, StringComparison.OrdinalIgnoreCase))))
                .ToList();
        }

        return jobs.Where(job => job.Tags.Any(jobTag =>
                jobTag.Equals(tag, StringComparison.OrdinalIgnoreCase)
                || jobTag.Contains(tag, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static IReadOnlyList<PublishJob> Sort(IEnumerable<PublishJob> jobs, JobListSort sort)
        => sort switch
        {
            JobListSort.Title => Sort(jobs, JobSortOrder.TitleAsc),
            _ => Sort(jobs, JobSortOrder.UpdatedDesc)
        };

    public static IReadOnlyList<PublishJob> Sort(IEnumerable<PublishJob> jobs, JobSortOrder sortOrder)
        => sortOrder switch
        {
            JobSortOrder.UpdatedAsc => jobs.OrderBy(j => j.UpdatedAt).ThenBy(j => j.Title, StringComparer.OrdinalIgnoreCase).ToList(),
            JobSortOrder.TitleAsc => jobs.OrderBy(j => j.Title, StringComparer.OrdinalIgnoreCase).ThenByDescending(j => j.UpdatedAt).ToList(),
            JobSortOrder.TitleDesc => jobs.OrderByDescending(j => j.Title, StringComparer.OrdinalIgnoreCase).ThenByDescending(j => j.UpdatedAt).ToList(),
            JobSortOrder.PinnedFirst => SortPinnedFirst(jobs),
            _ => jobs.OrderByDescending(j => j.UpdatedAt).ThenBy(j => j.Title, StringComparer.OrdinalIgnoreCase).ToList()
        };

    public static IReadOnlyList<PublishJob> SortPinnedFirst(IEnumerable<PublishJob> jobs)
        => jobs.OrderByDescending(j => j.IsPinned)
            .ThenByDescending(j => j.UpdatedAt)
            .ThenBy(j => j.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static JobListSort ParseSort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return JobListSort.Updated;

        return value.Trim().ToLowerInvariant() switch
        {
            "title" or "名称" or "标题" => JobListSort.Title,
            "updated" or "update" or "更新时间" => JobListSort.Updated,
            _ => JobListSort.Updated
        };
    }

    public const string AllTagsLabel = "全部标签";

    public static IReadOnlyList<PublishJob> QueryJobsByTag(IEnumerable<PublishJob> jobs, string? tag)
    {
        var normalized = tag?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized == AllTagsLabel)
            return jobs.ToList();

        return jobs
            .Where(job => job.Tags.Any(existing => existing.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public static IReadOnlyList<PublishJob> Search(IEnumerable<PublishJob> jobs, string? searchText)
    {
        var keyword = searchText?.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
            return jobs.ToList();

        return jobs.Where(job => Matches(job, keyword)).ToList();
    }

    public static bool Matches(PublishJob job, string keyword)
    {
        return Contains(job.Title, keyword)
            || Contains(job.Paths.Slug, keyword)
            || Contains(job.Source.ThreadUrl, keyword)
            || Contains(job.Id, keyword)
            || Contains(job.Source.Site, keyword)
            || Contains(job.Notes, keyword)
            || Contains(GetGeneratedCopySearchText(job.Publish.GeneratedCopy), keyword)
            || Contains(ExtractBaiduLinkSlug(job.Publish.Baidu.Link), keyword)
            || job.Tags.Any(tag => Contains(tag, keyword));
    }

    private static string? GetGeneratedCopySearchText(string? copy)
    {
        if (string.IsNullOrWhiteSpace(copy))
            return null;

        return copy.Length <= 200 ? copy : copy[..200];
    }

    private static string? ExtractBaiduLinkSlug(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
            return null;

        var match = BaiduLinkSlugPattern.Match(link);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static string NormalizeThreadUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var text = url.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            return text.ToLowerInvariant();

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
            Fragment = string.Empty
        };

        if ((builder.Scheme == "http" && builder.Port == 80)
            || (builder.Scheme == "https" && builder.Port == 443))
        {
            builder.Port = -1;
        }

        var path = builder.Path.TrimEnd('/');
        builder.Path = string.IsNullOrEmpty(path) ? "/" : path;
        return builder.Uri.GetLeftPart(UriPartial.Path).ToLowerInvariant();
    }

    public static JobCreateCheckResult CheckDuplicates(
        string title,
        string? threadUrl,
        IEnumerable<PublishJob> existingJobs)
    {
        var result = new JobCreateCheckResult();
        var normalizedUrl = NormalizeThreadUrl(threadUrl);
        var slug = WorkspaceService.Slugify(title);

        foreach (var job in existingJobs)
        {
            if (!string.IsNullOrWhiteSpace(normalizedUrl)
                && NormalizeThreadUrl(job.Source.ThreadUrl) == normalizedUrl)
            {
                result.Duplicates.Add(ToMatch(job, "SameThreadUrl"));
                continue;
            }

            if (job.Paths.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)
                || job.Title.Equals(title.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                result.Duplicates.Add(ToMatch(job, "SameTitle"));
            }
        }

        result.HasDuplicates = result.Duplicates.Count > 0;
        result.Blocked = result.Duplicates.Any(d => d.Reason == "SameThreadUrl");
        result.Message = BuildDuplicateMessage(result);
        return result;
    }

    public static string EnsureUniqueSlug(string title, IEnumerable<PublishJob> existingJobs)
    {
        var baseSlug = WorkspaceService.Slugify(title);
        var slug = baseSlug;
        var suffix = 2;

        while (existingJobs.Any(job => job.Paths.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase)))
        {
            slug = $"{baseSlug}_{suffix}";
            suffix++;
        }

        return slug;
    }

    private static JobDuplicateMatch ToMatch(PublishJob job, string reason) => new()
    {
        Reason = reason,
        JobId = job.Id,
        Title = job.Title,
        Slug = job.Paths.Slug,
        ThreadUrl = job.Source.ThreadUrl,
        Status = job.Status
    };

    private static string BuildDuplicateMessage(JobCreateCheckResult result)
    {
        if (!result.HasDuplicates)
            return string.Empty;

        var lines = result.Duplicates.Select(d => d.Reason switch
        {
            "SameThreadUrl" => $"同帖链接: {d.Title} ({d.Status})",
            "SameTitle" => $"同名任务: {d.Title} ({d.Status})",
            _ => $"{d.Title} ({d.Status})"
        });

        var body = string.Join(Environment.NewLine, lines);
        return result.Blocked
            ? $"已存在相同帖子链接的任务，请直接选择现有任务：{Environment.NewLine}{body}"
            : $"发现相似任务，仍将创建（目录会自动加后缀）：{Environment.NewLine}{body}";
    }

    private static bool Contains(string? value, string keyword)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}