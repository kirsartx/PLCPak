using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class DuplicateScanServiceTests
{
    [Fact]
    public void Scan_finds_same_thread_url_groups()
    {
        var jobs = new[]
        {
            CreateJob("a", "Game A", "https://laowang.vip/thread/1/"),
            CreateJob("b", "Game B", "https://laowang.vip/thread/1/"),
            CreateJob("c", "Game C", "https://laowang.vip/thread/2/")
        };

        var result = DuplicateScanService.Scan(jobs);

        Assert.Equal(1, result.GroupCount);
        Assert.Equal(2, result.DuplicateJobCount);
        Assert.Equal("SameThreadUrl", result.Groups[0].Reason);
    }

    [Fact]
    public void Scan_finds_same_title_groups()
    {
        var jobs = new[]
        {
            CreateJob("a", "Same Title", null),
            CreateJob("b", "Same Title", null)
        };

        var result = DuplicateScanService.Scan(jobs);

        Assert.Equal(1, result.GroupCount);
        Assert.Equal(2, result.DuplicateJobCount);
        Assert.Equal("SameTitle", result.Groups[0].Reason);
    }

    [Fact]
    public void ExportReport_writes_json_file()
    {
        var jobs = new[]
        {
            CreateJob("a", "Dup", null),
            CreateJob("b", "Dup", null)
        };
        var scan = DuplicateScanService.Scan(jobs);
        var root = Path.Combine(Path.GetTempPath(), "plcpak-dup-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            var export = DuplicateScanService.ExportReport(scan, root);
            Assert.True(File.Exists(export.ExportPath));
            Assert.Equal(1, export.GroupCount);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static PublishJob CreateJob(string id, string title, string? threadUrl)
        => new()
        {
            Id = id,
            Title = title,
            Paths = new JobPaths { Slug = title.ToLowerInvariant().Replace(' ', '-') },
            Source = new JobSource { ThreadUrl = threadUrl ?? string.Empty }
        };
}