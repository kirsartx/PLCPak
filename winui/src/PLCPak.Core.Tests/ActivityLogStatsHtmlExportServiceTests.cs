using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ActivityLogStatsHtmlExportServiceTests : IDisposable
{
    private readonly string _root;

    public ActivityLogStatsHtmlExportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-activity-html-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Export_writes_html_report_with_table_and_bar_divs()
    {
        ActivityLogService.Append(_root, "export", "html stats");
        ActivityLogService.Append(_root, "telegram", "send");
        ActivityLogService.Append(_root, "archive", "批量归档选中: 成功 1");

        var stats = ActivityLogStatsService.BuildStats(_root);
        var daily = ActivityLogDailyStatsService.GetDailyCounts(_root, days: 7);
        var batch = ActivityLogBatchSummaryService.Build(_root);
        var export = ActivityLogStatsHtmlExportService.Export(stats, _root, dailyStats: daily, batchSummary: batch);

        Assert.True(File.Exists(export.ExportPath));
        Assert.Contains("activity-log-stats", export.ExportPath);
        Assert.EndsWith(".html", export.ExportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, export.Stats.TotalCount);

        var html = File.ReadAllText(export.ExportPath);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("活动日志分类统计", html);
        Assert.Contains("class=\"bar\"", html);
        Assert.Contains("近 7 天每日条数", html);
        Assert.Contains("class=\"daily-bar\"", html);
        Assert.Contains("category-chart", html);
        Assert.Contains("daily-chart", html);
        Assert.Contains("renderBars", html);
        Assert.Contains("window.print()", html);
        Assert.Contains("打印 / 导出 PDF", html);
        Assert.Contains("切换暗色主题", html);
        Assert.Contains("toggleTheme", html);
        Assert.Contains("body.dark", html);
        Assert.Contains("prefTheme", html);
        Assert.Contains("批量操作统计", html);
        Assert.Contains("batch-chart", html);
        Assert.Contains("batchData", html);
        Assert.Contains("telegram", html);
        Assert.Contains("已导出活动日志统计 HTML", export.SummaryText);
    }

    [Fact]
    public void Export_embeds_dark_theme_preference_when_requested()
    {
        var stats = new ActivityLogStatsResult
        {
            TotalCount = 1,
            SummaryText = "活动日志统计：1 条",
            Items =
            [
                new ActivityLogCategoryStat
                {
                    Category = "export",
                    Count = 1,
                    LatestTimestamp = DateTime.Now
                }
            ]
        };

        var export = ActivityLogStatsHtmlExportService.Export(stats, _root, initialTheme: "dark");

        var html = File.ReadAllText(export.ExportPath);
        Assert.Contains("\"dark\"", html);
    }

    [Fact]
    public void Export_includes_since_days_filter_in_batch_section_when_provided()
    {
        ActivityLogService.Append(_root, "archive", "批量归档选中: 成功 1");

        var stats = ActivityLogStatsService.BuildStats(_root);
        var batch = ActivityLogBatchSummaryService.Build(_root);
        var export = ActivityLogStatsHtmlExportService.Export(stats, _root, batchSummary: batch, sinceDays: 14);

        var html = File.ReadAllText(export.ExportPath);
        Assert.Contains("批量操作统计", html);
        Assert.Contains("筛选最近 14 天", html);
    }

    [Fact]
    public void Export_includes_since_days_filter_in_meta_when_provided()
    {
        var stats = new ActivityLogStatsResult
        {
            TotalCount = 1,
            SinceDays = 14,
            SummaryText = "活动日志统计（最近 14 天）：1 条",
            Items =
            [
                new ActivityLogCategoryStat
                {
                    Category = "export",
                    Count = 1,
                    LatestTimestamp = DateTime.Now
                }
            ]
        };

        var export = ActivityLogStatsHtmlExportService.Export(stats, _root);

        Assert.Equal(14, export.SinceDays);
        Assert.Contains("（最近 14 天）", export.SummaryText);

        var html = File.ReadAllText(export.ExportPath);
        Assert.Contains("筛选最近 14 天", html);
    }

    [Fact]
    public void Export_uses_reports_directory_when_output_path_not_provided()
    {
        var stats = new ActivityLogStatsResult
        {
            TotalCount = 0,
            SummaryText = "活动日志统计：暂无记录"
        };

        var daily = ActivityLogDailyStatsService.GetDailyCounts(_root, days: 7);
        var export = ActivityLogStatsHtmlExportService.Export(stats, _root, dailyStats: daily);

        Assert.StartsWith(Path.Combine(_root, "reports"), export.ExportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("activity-log-stats-", Path.GetFileName(export.ExportPath));
    }
}