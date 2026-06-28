using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class BatchPinFilteredService
{
    public static BatchPinFilteredPreviewResult Preview(
        IEnumerable<PublishJob> jobs,
        JobListFilter filter = JobListFilter.All,
        string? searchText = null,
        string? tagFilter = null,
        bool pin = true)
    {
        var matched = JobQueryService.Query(jobs, filter, searchText, tagFilter: tagFilter).ToList();
        var applicable = matched.Where(job => job.IsPinned != pin).ToList();
        var alreadyInTargetState = matched.Count - applicable.Count;

        return new BatchPinFilteredPreviewResult
        {
            TotalMatched = matched.Count,
            ApplicableCount = applicable.Count,
            AlreadyInTargetStateCount = alreadyInTargetState,
            Pin = pin,
            SampleTitles = applicable.Take(8).Select(job => job.Title).ToList(),
            SummaryText = BuildPreviewSummary(matched.Count, applicable.Count, alreadyInTargetState, pin)
        };
    }

    public static BatchPinFilteredResult BatchPinFilteredJobs(
        IEnumerable<PublishJob> jobs,
        JobStore jobStore,
        JobListFilter filter = JobListFilter.All,
        string? searchText = null,
        string? tagFilter = null,
        bool pin = true)
    {
        var result = new BatchPinFilteredResult { Pin = pin };
        var matched = JobQueryService.Query(jobs, filter, searchText, tagFilter: tagFilter);

        foreach (var job in matched)
        {
            if (job.IsPinned == pin)
            {
                result.Skipped++;
                result.Messages.Add($"[跳过] {job.Title}: 已{(pin ? "置顶" : "取消置顶")}");
                continue;
            }

            job.IsPinned = pin;
            job.AppendLog(pin ? "批量置顶" : "批量取消置顶");
            jobStore.Save(job);
            result.Applied++;
            result.UpdatedJobIds.Add(job.Id);
            result.Messages.Add($"[{(pin ? "置顶" : "取消置顶")}] {job.Title}");
        }

        result.SummaryText = result.Applied == 0 && result.Skipped == 0
            ? $"批量{(pin ? "置顶" : "取消置顶")}筛选：无匹配任务"
            : $"批量{(pin ? "置顶" : "取消置顶")}筛选：更新 {result.Applied}，跳过 {result.Skipped}";
        return result;
    }

    private static string BuildPreviewSummary(int totalMatched, int applicableCount, int alreadyInTargetStateCount, bool pin)
    {
        var action = pin ? "置顶" : "取消置顶";

        if (totalMatched == 0)
            return $"批量{action}筛选：当前筛选条件下没有任务";

        if (applicableCount == 0)
            return $"批量{action}筛选：匹配 {totalMatched} 个任务，均已{action}，无需操作";

        var skipHint = alreadyInTargetStateCount > 0
            ? $"，跳过已{action} {alreadyInTargetStateCount} 个"
            : string.Empty;
        return $"批量{action}筛选：匹配 {totalMatched} 个任务，可{action} {applicableCount} 个{skipHint}";
    }
}