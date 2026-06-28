using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class FilteredJobsCsvExportServiceTests : IDisposable
{
    private readonly string _root;

    public FilteredJobsCsvExportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-filtered-jobs-csv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Export_writes_csv_with_required_columns()
    {
        var jobs = new[]
        {
            CreateJob("a", "Game A", JobStatus.Processed, ["hot", "pc"], pinned: true),
            CreateJob("b", "Game B", JobStatus.Failed)
        };

        var export = FilteredJobsCsvExportService.Export(
            jobs,
            _root,
            JobListFilter.All,
            searchText: null,
            tagFilter: null,
            JobSortOrder.UpdatedDesc);

        Assert.Equal(2, export.EntryCount);
        Assert.True(File.Exists(export.ExportPath));
        Assert.Contains("filtered-jobs", export.ExportPath);

        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("jobId,title,status,slug,tags,publishSummary,updatedAt,isPinned,baiduLink,baiduPwd,quarkLink,quarkPwd,telegramLink,hasCopy", csv);
        Assert.Contains("Game A", csv);
        Assert.Contains("hot;pc", csv);
        Assert.Contains("true", csv);
        Assert.Contains("Game B", csv);
    }

    [Fact]
    public void Export_applies_filter_search_tag_sort_and_limit()
    {
        var jobs = new[]
        {
            CreateJob("a", "Alpha Game", JobStatus.Processed, ["hot"]),
            CreateJob("b", "Beta Game", JobStatus.Failed, ["hot"]),
            CreateJob("c", "Gamma Task", JobStatus.Processed)
        };

        var export = FilteredJobsCsvExportService.Export(
            jobs,
            _root,
            JobListFilter.Processed,
            searchText: "alpha",
            tagFilter: "hot",
            JobSortOrder.TitleAsc,
            limit: 1);

        Assert.Equal(1, export.EntryCount);
        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("Alpha Game", csv);
        Assert.DoesNotContain("Beta Game", csv);
        Assert.DoesNotContain("Gamma Task", csv);
    }

    [Fact]
    public void Export_includes_publish_links_and_has_copy_flag()
    {
        var jobs = new[]
        {
            CreateJob("links", "Link Game", JobStatus.Processed, publish: new JobPublishState
            {
                Baidu = new PublishChannelState { Link = "https://pan.baidu.com/s/test", Password = "abcd" },
                Quark = new PublishChannelState { Link = "https://pan.quark.cn/s/quark", Password = "efgh" },
                Telegram = new PublishChannelState { Link = "https://t.me/test" },
                GeneratedCopy = "发布文案"
            })
        };

        var export = FilteredJobsCsvExportService.Export(
            jobs,
            _root,
            JobListFilter.All,
            null,
            null,
            JobSortOrder.UpdatedDesc);

        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("https://pan.baidu.com/s/test", csv);
        Assert.Contains("abcd", csv);
        Assert.Contains("https://pan.quark.cn/s/quark", csv);
        Assert.Contains("efgh", csv);
        Assert.Contains("https://t.me/test", csv);
        Assert.Contains(",true", csv);
    }

    [Fact]
    public void Export_escapes_csv_special_characters()
    {
        var jobs = new[]
        {
            CreateJob("x", "Title,Quoted", JobStatus.Draft)
        };

        var export = FilteredJobsCsvExportService.Export(
            jobs,
            _root,
            JobListFilter.All,
            null,
            null,
            JobSortOrder.UpdatedDesc);

        var csv = File.ReadAllText(export.ExportPath);
        Assert.Contains("\"Title,Quoted\"", csv);
    }

    private static PublishJob CreateJob(
        string id,
        string title,
        JobStatus status,
        IEnumerable<string>? tags = null,
        bool pinned = false,
        JobPublishState? publish = null)
        => new()
        {
            Id = id,
            Title = title,
            Status = status,
            Tags = tags?.ToList() ?? [],
            IsPinned = pinned,
            Paths = new JobPaths { Slug = id },
            Publish = publish ?? new JobPublishState(),
            UpdatedAt = DateTime.Now
        };
}