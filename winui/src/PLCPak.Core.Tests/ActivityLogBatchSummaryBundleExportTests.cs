using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ActivityLogBatchSummaryBundleExportTests : IDisposable
{
    private readonly string _root;

    public ActivityLogBatchSummaryBundleExportTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-batch-bundle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void ExportAll_writes_json_and_csv_files()
    {
        ActivityLogService.Append(_root, "tags", "批量标签: 更新 1");

        var export = ActivityLogBatchSummaryExportService.ExportAll(_root);

        Assert.True(File.Exists(export.JsonExportPath));
        Assert.True(File.Exists(export.CsvExportPath));
        Assert.Equal(1, export.Stats.TotalCount);
        Assert.Contains("JSON + CSV", export.SummaryText);
    }

    [Fact]
    public void ExportAll_includes_since_days_in_result()
    {
        ActivityLogService.Append(_root, "tags", "批量标签: 更新 1");

        var export = ActivityLogBatchSummaryExportService.ExportAll(_root, sinceDays: 7);

        Assert.Equal(7, export.SinceDays);
        Assert.Contains("最近 7 天", export.SummaryText);
    }

    [Fact]
    public void ExportAll_uses_matching_timestamp_in_json_and_csv_filenames()
    {
        ActivityLogService.Append(_root, "tags", "批量标签: 更新 1");

        var export = ActivityLogBatchSummaryExportService.ExportAll(_root);

        Assert.False(string.IsNullOrWhiteSpace(export.BundleStamp));
        Assert.Contains($"activity-log-batch-stats-{export.BundleStamp}.json", export.JsonExportPath);
        Assert.Contains($"activity-log-batch-stats-{export.BundleStamp}.csv", export.CsvExportPath);
    }
}