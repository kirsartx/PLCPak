using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchDuplicateMergePreviewExportServiceTests : IDisposable
{
    private readonly string _root;

    public BatchDuplicateMergePreviewExportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-batch-merge-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Export_writes_default_json_report()
    {
        var preview = new BatchDuplicateMergePreviewResult
        {
            GroupCount = 1,
            MergeActionCount = 1,
            WouldMergeCount = 1,
            SummaryText = "批量合并重复预览：1 组",
            Items =
            [
                new BatchDuplicateMergePreviewItem
                {
                    TargetJobId = "b",
                    TargetTitle = "Dup",
                    SourceJobIds = ["a"],
                    Reason = "SameTitle"
                }
            ]
        };

        var export = BatchDuplicateMergePreviewExportService.Export(preview, _root);

        Assert.Equal(1, export.GroupCount);
        Assert.Equal(1, export.MergeActionCount);
        Assert.Equal(1, export.ItemCount);
        Assert.True(File.Exists(export.ExportPath));
        Assert.Contains("batch-merge-preview", export.ExportPath);
        Assert.EndsWith(".json", export.ExportPath, StringComparison.OrdinalIgnoreCase);

        var text = File.ReadAllText(export.ExportPath);
        Assert.Contains("Dup", text);
        Assert.Contains("SameTitle", text);
    }

    [Fact]
    public void Export_honors_custom_output_path()
    {
        var preview = new BatchDuplicateMergePreviewResult
        {
            GroupCount = 0,
            MergeActionCount = 0,
            SummaryText = "批量合并重复预览：未发现可合并组"
        };
        var customPath = Path.Combine(_root, "custom-preview.json");

        var export = BatchDuplicateMergePreviewExportService.Export(preview, _root, customPath);

        Assert.Equal(customPath, export.ExportPath);
        Assert.True(File.Exists(customPath));
    }
}