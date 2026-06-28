using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ActivityLogCsvExportServiceTests : IDisposable
{
    private readonly string _root;

    public ActivityLogCsvExportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-activity-csv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Export_writes_csv_with_header_and_rows()
    {
        ActivityLogService.Append(_root, "telegram", "send message");
        ActivityLogService.Append(_root, "import", "csv import");

        var export = ActivityLogCsvExportService.Export(_root);

        Assert.Equal(2, export.EntryCount);
        Assert.True(File.Exists(export.ExportPath));
        Assert.Contains("activity-log", export.ExportPath);
        Assert.EndsWith(".csv", export.ExportPath, StringComparison.OrdinalIgnoreCase);

        var text = File.ReadAllText(export.ExportPath);
        Assert.Contains("timestamp,category,message", text);
        Assert.Contains("telegram", text);
        Assert.Contains("send message", text);
        Assert.Contains("import", text);
    }

    [Fact]
    public void Export_filters_by_category()
    {
        ActivityLogService.Append(_root, "telegram", "send");
        ActivityLogService.Append(_root, "import", "csv");

        var export = ActivityLogCsvExportService.Export(_root, category: "import");

        Assert.Equal(1, export.EntryCount);
        Assert.Equal("import", export.CategoryFilter);
        var text = File.ReadAllText(export.ExportPath);
        Assert.Contains("csv", text);
        Assert.DoesNotContain("telegram", text);
    }

    [Fact]
    public void Export_filters_by_query_keyword()
    {
        ActivityLogService.Append(_root, "archive", "批量归档选中: 成功 2");
        ActivityLogService.Append(_root, "export", "导出活动日志");

        var export = ActivityLogCsvExportService.Export(_root, query: "批量");

        Assert.Equal(1, export.EntryCount);
        Assert.Equal("批量", export.QueryFilter);
        Assert.Contains("批量归档", File.ReadAllText(export.ExportPath));
    }

    [Fact]
    public void Export_escapes_csv_special_characters()
    {
        ActivityLogService.Append(_root, "test", "line1,line2");

        var export = ActivityLogCsvExportService.Export(_root);
        var text = File.ReadAllText(export.ExportPath);

        Assert.Contains("\"line1,line2\"", text);
    }
}