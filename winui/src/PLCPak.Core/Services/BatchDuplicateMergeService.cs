using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class BatchDuplicateMergeService
{
    public static BatchDuplicateMergeResult MergeAll(
        IEnumerable<PublishJob> jobs,
        Func<string, string, JobMergeResult> mergeJobs)
    {
        var suggestions = DuplicateMergeSuggestionService.BuildSuggestions(jobs);
        var result = new BatchDuplicateMergeResult();

        foreach (var suggestion in suggestions.Suggestions)
        {
            result.GroupsProcessed++;

            foreach (var sourceJobId in suggestion.SourceJobIds)
            {
                try
                {
                    var merge = mergeJobs(suggestion.TargetJobId, sourceJobId);
                    if (!merge.Success)
                    {
                        if (merge.MergedFields.Count == 0 && string.IsNullOrWhiteSpace(merge.Error))
                        {
                            result.Skipped++;
                            result.Messages.Add($"[跳过] {suggestion.TargetTitle} ← {sourceJobId}");
                        }
                        else
                        {
                            result.Failed++;
                            result.Messages.Add($"[失败] {suggestion.TargetTitle} ← {sourceJobId}: {merge.Error ?? merge.Message}");
                        }

                        continue;
                    }

                    result.Merged++;
                    result.Messages.Add($"[合并] {suggestion.TargetTitle} ← {sourceJobId}: {merge.Message}");
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Messages.Add($"[失败] {suggestion.TargetTitle} ← {sourceJobId}: {ex.Message}");
                }
            }
        }

        result.SummaryText = result.GroupsProcessed == 0
            ? "批量合并重复：未发现可合并组"
            : $"批量合并重复：成功 {result.Merged}，跳过 {result.Skipped}，失败 {result.Failed}（{result.GroupsProcessed} 组）";

        return result;
    }
}