using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ActivityLogStatsServiceTests : IDisposable
{
    private readonly string _root;

    public ActivityLogStatsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-activity-stats-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void BuildStats_groups_entries_by_category_with_counts_and_latest_timestamp()
    {
        WriteEntry(DateTime.Now.AddDays(-2), "telegram", "old send");
        WriteEntry(DateTime.Now.AddHours(-1), "telegram", "new send");
        ActivityLogService.Append(_root, "import", "csv import");
        ActivityLogService.Append(_root, "import", "csv import 2");

        var stats = ActivityLogStatsService.BuildStats(_root);

        Assert.Equal(4, stats.TotalCount);
        Assert.Equal(2, stats.Items.Count);

        var telegram = stats.Items.First(item => item.Category == "telegram");
        Assert.Equal(2, telegram.Count);
        var latestTelegram = ActivityLogService.ReadAll(_root)
            .Where(entry => entry.Category == "telegram")
            .OrderByDescending(entry => entry.Timestamp)
            .First();
        Assert.Equal("new send", latestTelegram.Message);
        Assert.Equal(latestTelegram.Timestamp, telegram.LatestTimestamp);

        var import = stats.Items.First(item => item.Category == "import");
        Assert.Equal(2, import.Count);
        Assert.Contains("4 条", stats.SummaryText);
        Assert.Contains("2 个分类", stats.SummaryText);
    }

    [Fact]
    public void BuildStats_respects_since_days_and_category_filters()
    {
        WriteEntry(DateTime.Now.AddDays(-40), "telegram", "stale");
        ActivityLogService.Append(_root, "telegram", "fresh");
        ActivityLogService.Append(_root, "import", "csv");

        var stats = ActivityLogStatsService.BuildStats(_root, sinceDays: 7, category: "telegram");

        Assert.Equal(1, stats.TotalCount);
        Assert.Single(stats.Items);
        Assert.Equal("telegram", stats.Items[0].Category);
        Assert.Contains("分类 telegram", stats.SummaryText);
        Assert.Equal(7, stats.SinceDays);
        Assert.Contains("（最近 7 天）", stats.SummaryText);
    }

    [Fact]
    public void BuildStats_sets_since_days_on_result_and_export()
    {
        ActivityLogService.Append(_root, "export", "json stats");

        var stats = ActivityLogStatsService.BuildStats(_root, sinceDays: 30);
        var jsonExport = ActivityLogStatsService.Export(_root, sinceDays: 30);
        var csvExport = ActivityLogStatsService.ExportCsv(_root, sinceDays: 30);

        Assert.Equal(30, stats.SinceDays);
        Assert.Equal(30, jsonExport.SinceDays);
        Assert.Equal(30, csvExport.SinceDays);
        Assert.Contains("（最近 30 天）", stats.SummaryText);
        Assert.Contains("（最近 30 天）", jsonExport.SummaryText);
        Assert.Contains("（最近 30 天）", csvExport.SummaryText);
    }

    [Fact]
    public void ExportCsv_writes_csv_report_to_reports_directory()
    {
        ActivityLogService.Append(_root, "export", "csv stats");

        var export = ActivityLogStatsService.ExportCsv(_root);

        Assert.True(File.Exists(export.ExportPath));
        Assert.Contains("activity-log-stats", export.ExportPath);
        Assert.EndsWith(".csv", export.ExportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, export.Stats.TotalCount);

        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("category,count,latestTimestamp", csv);
        Assert.Contains("export", csv);
        Assert.Contains("已导出活动日志统计 CSV", export.SummaryText);
    }

    [Fact]
    public void BuildBarItems_scales_category_counts_to_max_bar_width()
    {
        ActivityLogService.Append(_root, "telegram", "send");
        ActivityLogService.Append(_root, "telegram", "send 2");
        ActivityLogService.Append(_root, "import", "csv");

        var stats = ActivityLogStatsService.BuildStats(_root);
        var bars = ActivityLogStatsService.BuildBarItems(stats, maxBarWidth: 100);

        Assert.Equal(2, bars.Count);
        var telegram = bars.First(item => item.Category == "telegram");
        var import = bars.First(item => item.Category == "import");
        Assert.Equal(2, telegram.Count);
        Assert.Equal(1, import.Count);
        Assert.Equal(100, telegram.BarPercent);
        Assert.Equal(50, import.BarPercent);
    }

    [Fact]
    public void BuildBarItems_returns_zero_percent_when_no_entries()
    {
        var stats = ActivityLogStatsService.BuildStats(_root);
        var bars = ActivityLogStatsService.BuildBarItems(stats);

        Assert.Empty(bars);
    }

    [Fact]
    public void HtmlExportService_writes_html_report_with_bar_chart()
    {
        ActivityLogService.Append(_root, "export", "html stats");
        ActivityLogService.Append(_root, "telegram", "send");

        var stats = ActivityLogStatsService.BuildStats(_root);
        var export = ActivityLogStatsHtmlExportService.Export(stats, _root);

        Assert.True(File.Exists(export.ExportPath));
        Assert.Contains("activity-log-stats", export.ExportPath);
        Assert.EndsWith(".html", export.ExportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, export.Stats.TotalCount);

        var html = File.ReadAllText(export.ExportPath);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("class=\"bar\"", html);
        Assert.Contains("telegram", html);
        Assert.Contains("已导出活动日志统计 HTML", export.SummaryText);
    }

    [Fact]
    public void Export_writes_json_report_to_reports_directory()
    {
        ActivityLogService.Append(_root, "export", "test export");

        var export = ActivityLogStatsService.Export(_root);

        Assert.True(File.Exists(export.ExportPath));
        Assert.Contains("activity-log-stats", export.ExportPath);
        Assert.EndsWith(".json", export.ExportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, export.Stats.TotalCount);
        Assert.Contains("totalCount", File.ReadAllText(export.ExportPath), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("已导出活动日志统计 JSON", export.SummaryText);
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