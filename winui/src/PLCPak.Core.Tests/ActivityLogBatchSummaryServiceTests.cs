using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ActivityLogBatchSummaryServiceTests : IDisposable
{
    private readonly string _root;

    public ActivityLogBatchSummaryServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-batch-summary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Build_counts_batch_messages_by_category()
    {
        ActivityLogService.Append(_root, "archive", "批量归档选中: 成功 2");
        ActivityLogService.Append(_root, "delete", "批量删除选中: 成功 1");
        ActivityLogService.Append(_root, "export", "导出活动日志");

        var summary = ActivityLogBatchSummaryService.Build(_root);

        Assert.Equal(2, summary.TotalCount);
        Assert.Equal(2, summary.Items.Count);
        Assert.Contains("批量操作统计：2 条", summary.SummaryText);
    }

    [Fact]
    public void Build_includes_since_days_in_summary_text()
    {
        ActivityLogService.Append(_root, "archive", "批量归档选中: 成功 1");

        var summary = ActivityLogBatchSummaryService.Build(_root, sinceDays: 14);

        Assert.Equal(14, summary.SinceDays);
        Assert.Contains("最近 14 天", summary.SummaryText);
    }

    [Fact]
    public void Build_returns_empty_summary_when_no_batch_logs()
    {
        ActivityLogService.Append(_root, "export", "导出活动日志");

        var summary = ActivityLogBatchSummaryService.Build(_root);

        Assert.Equal(0, summary.TotalCount);
        Assert.Contains("暂无记录", summary.SummaryText);
    }
}