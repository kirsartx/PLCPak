using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class ActivityLogDailyStatsService
{
    public static ActivityLogDailyStatsResult GetDailyCounts(
        string workspaceRoot,
        int days = 7,
        string? category = null)
    {
        var windowDays = Math.Max(1, days);
        var today = DateTime.Today;
        var startDate = today.AddDays(-(windowDays - 1));
        var entries = ActivityLogService.Search(
            workspaceRoot,
            query: null,
            limit: int.MaxValue,
            category: category,
            since: startDate,
            until: today.AddDays(1).AddTicks(-1));

        var countsByDate = entries
            .GroupBy(entry => entry.Timestamp.Date)
            .ToDictionary(group => group.Key, group => group.Count());

        var items = new List<ActivityLogDailyCountItem>();
        for (var date = startDate; date <= today; date = date.AddDays(1))
        {
            countsByDate.TryGetValue(date, out var count);
            items.Add(new ActivityLogDailyCountItem
            {
                Date = date,
                DateLabel = date.ToString("yyyy-MM-dd"),
                Count = count
            });
        }

        var totalCount = items.Sum(item => item.Count);
        var categoryHint = string.IsNullOrWhiteSpace(category) ? string.Empty : $"（分类 {category.Trim()}）";

        return new ActivityLogDailyStatsResult
        {
            Days = windowDays,
            TotalCount = totalCount,
            Items = items,
            SummaryText = totalCount == 0
                ? $"活动日志每日统计：近 {windowDays} 天暂无记录{categoryHint}"
                : $"活动日志每日统计：近 {windowDays} 天共 {totalCount} 条{categoryHint}"
        };
    }

    public static ActivityLogDailyStatsCsvExportResult ExportCsv(
        string workspaceRoot,
        string? outputPath = null,
        int days = 7,
        string? category = null)
    {
        var stats = GetDailyCounts(workspaceRoot, days, category);
        var csv = BuildCsv(stats);
        var fullPath = ResolveCsvExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, csv, Encoding.UTF8);

        return new ActivityLogDailyStatsCsvExportResult
        {
            ExportPath = fullPath,
            Stats = stats,
            SummaryText = $"已导出活动日志每日统计 CSV：近 {stats.Days} 天 {stats.TotalCount} 条"
        };
    }

    private static string BuildCsv(ActivityLogDailyStatsResult stats)
    {
        var sb = new StringBuilder();
        sb.AppendLine("date,count");
        foreach (var item in stats.Items)
        {
            sb.Append(Csv(item.DateLabel)).Append(',');
            sb.Append(item.Count);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ResolveCsvExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"activity-log-daily-stats-{DateTime.Now:yyyy-MM-dd-HHmmss}.csv";
        return Path.Combine(reportsDir, fileName);
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            return $"\"{text.Replace("\"", "\"\"")}\"";
        return text;
    }
}