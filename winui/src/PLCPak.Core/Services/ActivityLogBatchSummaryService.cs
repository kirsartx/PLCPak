using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class ActivityLogBatchSummaryService
{
    public static ActivityLogBatchSummaryResult Build(string workspaceRoot, int? sinceDays = null)
    {
        var entries = ActivityLogService.Search(
            workspaceRoot,
            ActivityLogBatchFilterService.BatchSearchKeyword,
            limit: int.MaxValue,
            sinceDays: sinceDays);

        var grouped = entries
            .GroupBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ActivityLogBatchSummaryItem
            {
                Category = group.Key,
                Label = ActivityLogBatchFilterService.GetBatchCategoryLabel(group.Key),
                Count = group.Count()
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sinceHint = FormatSinceDaysHint(sinceDays);
        return new ActivityLogBatchSummaryResult
        {
            TotalCount = entries.Count,
            SinceDays = sinceDays,
            Items = grouped,
            SummaryText = entries.Count == 0
                ? $"批量操作统计{sinceHint}：暂无记录"
                : $"批量操作统计{sinceHint}：{entries.Count} 条，{grouped.Count} 个分类"
        };
    }

    internal static string FormatSinceDaysHint(int? sinceDays)
        => sinceDays is > 0 ? $"（最近 {sinceDays} 天）" : string.Empty;
}