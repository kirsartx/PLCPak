using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ActivityLogBatchSummaryCsvExportServiceTests : IDisposable
{
    private readonly string _root;

    public ActivityLogBatchSummaryCsvExportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-batch-csv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Export_writes_csv_with_category_label_count_columns()
    {
        ActivityLogService.Append(_root, "delete", "批量删除选中: 成功 1");
        ActivityLogService.Append(_root, "archive", "批量归档选中: 成功 2");

        var export = ActivityLogBatchSummaryCsvExportService.Export(_root);

        Assert.True(File.Exists(export.ExportPath));
        Assert.EndsWith(".csv", export.ExportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, export.Stats.TotalCount);

        var text = File.ReadAllText(export.ExportPath);
        Assert.Contains("category,label,count", text);
        Assert.Contains("批量删除", text);
        Assert.Contains("批量归档", text);
    }

    [Fact]
    public void Export_includes_since_days_in_result()
    {
        ActivityLogService.Append(_root, "tags", "批量标签: 更新 1");

        var export = ActivityLogBatchSummaryCsvExportService.Export(_root, sinceDays: 30);

        Assert.Equal(30, export.SinceDays);
        Assert.Contains("最近 30 天", export.SummaryText);
    }
}