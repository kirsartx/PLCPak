using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ActivityLogDailyStatsServiceTests : IDisposable
{
    private readonly string _root;

    public ActivityLogDailyStatsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-activity-daily-stats-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void GetDailyCounts_groups_entries_by_calendar_day_with_zero_fill()
    {
        WriteEntry(DateTime.Today.AddDays(-2).AddHours(12), "export", "older");
        WriteEntry(DateTime.Today.AddHours(10), "export", "today-1");
        WriteEntry(DateTime.Today.AddHours(11), "telegram", "today-2");

        var stats = ActivityLogDailyStatsService.GetDailyCounts(_root, days: 3);

        Assert.Equal(3, stats.Days);
        Assert.Equal(3, stats.TotalCount);
        Assert.Equal(3, stats.Items.Count);
        Assert.Equal(1, stats.Items[0].Count);
        Assert.Equal(2, stats.Items[2].Count);
        Assert.Contains("近 3 天共 3 条", stats.SummaryText);
    }

    [Fact]
    public void GetDailyCounts_returns_zero_counts_when_no_entries()
    {
        var stats = ActivityLogDailyStatsService.GetDailyCounts(_root, days: 7);

        Assert.Equal(7, stats.Items.Count);
        Assert.Equal(0, stats.TotalCount);
        Assert.All(stats.Items, item => Assert.Equal(0, item.Count));
        Assert.Contains("近 7 天暂无记录", stats.SummaryText);
    }

    [Fact]
    public void ExportCsv_writes_daily_stats_csv_to_reports_directory()
    {
        ActivityLogService.Append(_root, "export", "daily stats");

        var export = ActivityLogDailyStatsService.ExportCsv(_root, days: 7);

        Assert.True(File.Exists(export.ExportPath));
        Assert.Contains("activity-log-daily-stats", export.ExportPath);
        Assert.EndsWith(".csv", export.ExportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, export.Stats.TotalCount);

        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("date,count", csv);
        Assert.Contains("已导出活动日志每日统计 CSV", export.SummaryText);
    }

    private void WriteEntry(DateTime timestamp, string category, string message)
    {
        var entry = new Models.ActivityLogEntry
        {
            Timestamp = timestamp,
            Category = category,
            Message = message
        };
        var path = ActivityLogService.GetLogPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path,
            System.Text.Json.JsonSerializer.Serialize(entry) + Environment.NewLine);
    }
}