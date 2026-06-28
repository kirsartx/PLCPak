using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class BatchTagService
{
    public static BatchTagResult Apply(
        IEnumerable<PublishJob> jobs,
        string tags,
        BatchTagMode mode = BatchTagMode.Append,
        string? tagFilter = null,
        JobListFilter filter = JobListFilter.All,
        string? searchText = null,
        IEnumerable<string>? jobIds = null)
    {
        var newTags = ParseTags(tags);
        if (newTags.Count == 0)
        {
            return new BatchTagResult
            {
                Skipped = 0,
                SummaryText = "未提供有效标签",
                Messages = ["标签为空或无效"]
            };
        }

        IEnumerable<PublishJob> candidates = jobIds is not null
            ? jobs.Where(job => jobIds.Contains(job.Id))
            : JobQueryService.Query(jobs, filter, searchText, tagFilter: tagFilter);
        var result = new BatchTagResult();

        foreach (var job in candidates)
        {
            var before = job.Tags.ToList();
            job.Tags = mode switch
            {
                BatchTagMode.Replace => newTags.ToList(),
                _ => before
                    .Concat(newTags)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            if (before.SequenceEqual(job.Tags, StringComparer.OrdinalIgnoreCase))
            {
                result.Skipped++;
                continue;
            }

            job.AppendLog(mode == BatchTagMode.Replace
                ? $"批量设置标签: {string.Join(", ", job.Tags)}"
                : $"批量追加标签: {string.Join(", ", newTags)}");
            result.Applied++;
            result.UpdatedJobIds.Add(job.Id);
            result.Messages.Add($"[已更新] {job.Title}: {string.Join(", ", job.Tags)}");
        }

        result.SummaryText = $"批量标签：更新 {result.Applied}，跳过 {result.Skipped}";
        return result;
    }

    public static BatchTagMode ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return BatchTagMode.Append;

        return value.Trim().ToLowerInvariant() switch
        {
            "replace" or "替换" or "set" => BatchTagMode.Replace,
            _ => BatchTagMode.Append
        };
    }

    private static List<string> ParseTags(string tags)
        => tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}