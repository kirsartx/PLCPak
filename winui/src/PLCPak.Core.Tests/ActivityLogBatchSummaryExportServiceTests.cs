using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ActivityLogBatchSummaryExportServiceTests : IDisposable
{
    private readonly string _root;

    public ActivityLogBatchSummaryExportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-batch-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Export_writes_json_with_batch_stats()
    {
        ActivityLogService.Append(_root, "archive", "批量归档选中: 成功 2");

        var export = ActivityLogBatchSummaryExportService.Export(_root);

        Assert.True(File.Exists(export.ExportPath));
        Assert.Contains("activity-log-batch-stats", export.ExportPath);
        Assert.Equal(1, export.Stats.TotalCount);
        Assert.Contains("已导出批量操作统计 JSON", export.SummaryText);
    }

    [Fact]
    public void Export_includes_since_days_in_result_and_summary()
    {
        ActivityLogService.Append(_root, "tags", "批量标签: 更新 1");

        var export = ActivityLogBatchSummaryExportService.Export(_root, sinceDays: 7);

        Assert.Equal(7, export.SinceDays);
        Assert.Equal(7, export.Stats.SinceDays);
        Assert.Contains("最近 7 天", export.SummaryText);
    }
}