using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class DuplicateMergeSuggestionService
{
    public static DuplicateMergeSuggestionResult BuildSuggestions(
        IEnumerable<PublishJob> jobs,
        DuplicateScanResult? precomputedScan = null)
    {
        var list = jobs as IReadOnlyList<PublishJob> ?? jobs.ToList();
        var byId = list.ToDictionary(job => job.Id, StringComparer.OrdinalIgnoreCase);
        var scan = precomputedScan ?? DuplicateScanService.Scan(list);
        var suggestions = new List<DuplicateMergeSuggestion>();

        foreach (var group in scan.Groups)
        {
            var groupJobs = group.Jobs
                .Select(match => byId.GetValueOrDefault(match.JobId))
                .Where(job => job is not null)
                .Cast<PublishJob>()
                .ToList();

            if (groupJobs.Count <= 1)
                continue;

            var target = PickPrimaryJob(groupJobs);
            var sources = groupJobs
                .Where(job => !job.Id.Equals(target.Id, StringComparison.OrdinalIgnoreCase))
                .Select(job => job.Id)
                .ToList();

            if (sources.Count == 0)
                continue;

            suggestions.Add(new DuplicateMergeSuggestion
            {
                Reason = group.Reason,
                Key = group.Key,
                TargetJobId = target.Id,
                TargetTitle = target.Title,
                SourceJobIds = sources,
                SummaryText = $"保留「{target.Title}」，合并 {sources.Count} 个重复项"
            });
        }

        var mergeActions = suggestions.Sum(s => s.SourceJobIds.Count);

        return new DuplicateMergeSuggestionResult
        {
            GroupCount = suggestions.Count,
            MergeActionCount = mergeActions,
            Suggestions = suggestions,
            SummaryText = suggestions.Count == 0
                ? "重复合并建议：暂无"
                : $"重复合并建议：{suggestions.Count} 组，可合并 {mergeActions} 个任务"
        };
    }

    public static PublishJob PickPrimaryJob(IReadOnlyList<PublishJob> jobs)
        => jobs
            .OrderByDescending(ScoreJob)
            .ThenByDescending(job => job.UpdatedAt)
            .First();

    private static int ScoreJob(PublishJob job)
    {
        var score = job.Status switch
        {
            JobStatus.Published => 100,
            JobStatus.Processed => 80,
            JobStatus.Extracted => 60,
            JobStatus.InboxReady => 40,
            JobStatus.Processing or JobStatus.Extracting => 35,
            JobStatus.Failed => 10,
            _ => 0
        };

        if (!string.IsNullOrWhiteSpace(job.Publish.Baidu.Link))
            score += 10;
        if (!string.IsNullOrWhiteSpace(job.Publish.Quark.Link))
            score += 10;
        if (!string.IsNullOrWhiteSpace(job.Publish.Telegram.Link))
            score += 10;
        if (!string.IsNullOrWhiteSpace(job.Publish.GeneratedCopy))
            score += 20;
        if (job.Tags.Count > 0)
            score += 5;
        if (job.IsPinned)
            score += 15;

        return score;
    }
}