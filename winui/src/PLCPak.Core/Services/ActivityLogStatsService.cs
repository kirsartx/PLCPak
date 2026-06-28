using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class ActivityLogStatsService
{
    public static ActivityLogStatsResult BuildStats(
        string workspaceRoot,
        int? sinceDays = null,
        string? category = null)
    {
        var entries = ActivityLogService.Search(
            workspaceRoot,
            query: null,
            limit: int.MaxValue,
            category: category,
            sinceDays: sinceDays);

        var items = entries
            .GroupBy(entry => NormalizeCategory(entry.Category), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var latest = group.Max(entry => entry.Timestamp);
                return new ActivityLogCategoryStat
                {
                    Category = group.First().Category.Trim().Length == 0 ? "(空)" : group.First().Category.Trim(),
                    Count = group.Count(),
                    LatestTimestamp = latest
                };
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var categoryHint = string.IsNullOrWhiteSpace(category) ? string.Empty : $"（分类 {category.Trim()}）";
        var sinceHint = ActivityLogBatchSummaryService.FormatSinceDaysHint(sinceDays);

        return new ActivityLogStatsResult
        {
            Items = items,
            TotalCount = entries.Count,
            SinceDays = sinceDays,
            SummaryText = entries.Count == 0
                ? $"活动日志统计{sinceHint}：暂无记录{categoryHint}"
                : $"活动日志统计{sinceHint}：共 {entries.Count} 条，{items.Count} 个分类{categoryHint}"
        };
    }

    public static ActivityLogStatsExportResult Export(
        string workspaceRoot,
        string? outputPath = null,
        int? sinceDays = null,
        string? category = null)
    {
        var stats = BuildStats(workspaceRoot, sinceDays, category);
        var fullPath = ResolveJsonExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        JsonHelper.WriteFile(fullPath, stats);

        var sinceHint = ActivityLogBatchSummaryService.FormatSinceDaysHint(sinceDays);
        return new ActivityLogStatsExportResult
        {
            ExportPath = fullPath,
            Stats = stats,
            SinceDays = sinceDays,
            SummaryText = $"已导出活动日志统计 JSON{sinceHint}：{stats.TotalCount} 条，{stats.Items.Count} 个分类"
        };
    }

    public static List<ActivityLogStatBarItem> BuildBarItems(
        ActivityLogStatsResult stats,
        int maxBarWidth = 100)
    {
        if (stats.Items.Count == 0)
            return [];

        var maxCount = stats.Items.Max(item => item.Count);
        if (maxCount <= 0)
        {
            return stats.Items
                .Select(item => new ActivityLogStatBarItem
                {
                    Category = item.Category,
                    Count = item.Count,
                    BarPercent = 0
                })
                .ToList();
        }

        var width = Math.Max(1, maxBarWidth);
        return stats.Items
            .Select(item => new ActivityLogStatBarItem
            {
                Category = item.Category,
                Count = item.Count,
                BarPercent = Math.Round((double)item.Count / maxCount * width, 2)
            })
            .ToList();
    }

    public static ActivityLogStatsCsvExportResult ExportCsv(
        string workspaceRoot,
        string? outputPath = null,
        int? sinceDays = null,
        string? category = null)
    {
        var stats = BuildStats(workspaceRoot, sinceDays, category);
        var csv = BuildCsv(stats);
        var fullPath = ResolveCsvExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, csv, Encoding.UTF8);

        var sinceHint = ActivityLogBatchSummaryService.FormatSinceDaysHint(sinceDays);
        return new ActivityLogStatsCsvExportResult
        {
            ExportPath = fullPath,
            Stats = stats,
            SinceDays = sinceDays,
            SummaryText = $"已导出活动日志统计 CSV{sinceHint}：{stats.TotalCount} 条，{stats.Items.Count} 个分类"
        };
    }

    private static string BuildCsv(ActivityLogStatsResult stats)
    {
        var sb = new StringBuilder();
        sb.AppendLine("category,count,latestTimestamp");
        foreach (var item in stats.Items)
        {
            sb.Append(Csv(item.Category)).Append(',');
            sb.Append(item.Count).Append(',');
            sb.Append(Csv(FormatTimestamp(item.LatestTimestamp)));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string NormalizeCategory(string? category)
        => string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();

    private static string ResolveJsonExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"activity-log-stats-{DateTime.Now:yyyy-MM-dd-HHmmss}.json";
        return Path.Combine(reportsDir, fileName);
    }

    private static string ResolveCsvExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"activity-log-stats-{DateTime.Now:yyyy-MM-dd-HHmmss}.csv";
        return Path.Combine(reportsDir, fileName);
    }

    private static string FormatTimestamp(DateTime value)
        => value.ToString("yyyy-MM-dd HH:mm:ss");

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            return $"\"{text.Replace("\"", "\"\"")}\"";
        return text;
    }
}