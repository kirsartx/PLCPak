using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class DuplicateMergeExportServiceTests
{
    [Fact]
    public void Export_writes_json_file_with_counts()
    {
        var jobs = new[]
        {
            CreateJob("a", "Same Game"),
            CreateJob("b", "Same Game")
        };
        var suggestions = DuplicateMergeSuggestionService.BuildSuggestions(jobs);
        var root = Path.Combine(Path.GetTempPath(), "plcpak-dup-merge-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            var export = DuplicateMergeExportService.Export(suggestions, root);

            Assert.True(File.Exists(export.ExportPath));
            Assert.Contains("duplicate-merge-suggestions-", Path.GetFileName(export.ExportPath));
            Assert.Equal(1, export.GroupCount);
            Assert.Equal(1, export.MergeActionCount);
            Assert.Equal(1, export.EntryCount);
            Assert.StartsWith(Path.Combine(root, "reports"), export.ExportPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Export_honors_custom_output_path()
    {
        var suggestions = new DuplicateMergeSuggestionResult();
        var root = Path.Combine(Path.GetTempPath(), "plcpak-dup-merge-custom-" + Guid.NewGuid().ToString("N"));
        var customPath = Path.Combine(root, "custom", "merge.json");
        try
        {
            var export = DuplicateMergeExportService.Export(suggestions, root, customPath);

            Assert.Equal(Path.GetFullPath(customPath), export.ExportPath);
            Assert.True(File.Exists(export.ExportPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static PublishJob CreateJob(string id, string title)
        => new()
        {
            Id = id,
            Title = title,
            Paths = new JobPaths { Slug = id },
            Source = new JobSource()
        };
}