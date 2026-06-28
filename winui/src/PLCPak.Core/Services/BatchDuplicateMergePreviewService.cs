using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class BatchDuplicateMergePreviewService
{
    public static BatchDuplicateMergePreviewResult PreviewAll(IEnumerable<PublishJob> jobs)
    {
        var suggestions = DuplicateMergeSuggestionService.BuildSuggestions(jobs);
        var items = suggestions.Suggestions
            .Select(suggestion => new BatchDuplicateMergePreviewItem
            {
                TargetJobId = suggestion.TargetJobId,
                TargetTitle = suggestion.TargetTitle,
                SourceJobIds = [..suggestion.SourceJobIds],
                Reason = suggestion.Reason
            })
            .ToList();

        var mergeActionCount = suggestions.MergeActionCount;

        return new BatchDuplicateMergePreviewResult
        {
            GroupCount = suggestions.GroupCount,
            MergeActionCount = mergeActionCount,
            WouldMergeCount = mergeActionCount,
            WouldSkipCount = 0,
            Items = items,
            SummaryText = suggestions.GroupCount == 0
                ? "批量合并重复预览：未发现可合并组"
                : $"批量合并重复预览：{suggestions.GroupCount} 组，将合并 {mergeActionCount} 个任务"
        };
    }
}