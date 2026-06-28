using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class SelectedJobsCsvExportServiceTests : IDisposable
{
    private readonly string _root;

    public SelectedJobsCsvExportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-selected-jobs-csv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Export_writes_csv_with_same_columns_as_filtered_jobs_export()
    {
        var jobs = new[]
        {
            CreateJob("a", "Game A", JobStatus.Processed, ["hot"]),
            CreateJob("b", "Game B", JobStatus.Failed),
            CreateJob("c", "Game C", JobStatus.Draft)
        };

        var export = SelectedJobsCsvExportService.Export(
            jobs,
            ["b", "a", "missing"],
            _root);

        Assert.Equal(2, export.EntryCount);
        Assert.True(File.Exists(export.ExportPath));
        Assert.Contains("selected-jobs", export.ExportPath);

        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("jobId,title,status,slug,tags,publishSummary,updatedAt,isPinned,baiduLink,baiduPwd,quarkLink,quarkPwd,telegramLink,hasCopy", csv);
        Assert.Contains("Game B", csv);
        Assert.Contains("Game A", csv);
        Assert.DoesNotContain("Game C", csv);

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Game B", ExtractTitle(lines[1]));
        Assert.Equal("Game A", ExtractTitle(lines[2]));
    }

    [Fact]
    public void Export_deduplicates_job_ids_and_skips_unknown_ids()
    {
        var jobs = new[] { CreateJob("only", "Only Game", JobStatus.Processed) };

        var export = SelectedJobsCsvExportService.Export(
            jobs,
            ["only", "only", "", "  ", "ghost"],
            _root);

        Assert.Equal(1, export.EntryCount);
        Assert.Contains("Only Game", File.ReadAllText(export.ExportPath));
    }

    [Fact]
    public void Export_uses_custom_output_path_when_provided()
    {
        var jobs = new[] { CreateJob("x", "Custom Path", JobStatus.Draft) };
        var outputPath = Path.Combine(_root, "custom", "selected.csv");

        var export = SelectedJobsCsvExportService.Export(jobs, ["x"], _root, outputPath);

        Assert.Equal(outputPath, export.ExportPath);
        Assert.True(File.Exists(outputPath));
    }

    private static string ExtractTitle(string csvLine)
    {
        var comma = csvLine.IndexOf(',');
        return comma < 0 ? csvLine : csvLine[(comma + 1)..].Split(',')[0].Trim('"');
    }

    private static PublishJob CreateJob(
        string id,
        string title,
        JobStatus status,
        IEnumerable<string>? tags = null)
        => new()
        {
            Id = id,
            Title = title,
            Status = status,
            Tags = tags?.ToList() ?? [],
            Paths = new JobPaths { Slug = id },
            Publish = new JobPublishState(),
            UpdatedAt = DateTime.Now
        };
}