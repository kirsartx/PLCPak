using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class QueueCsvExportServiceTests : IDisposable
{
    private readonly string _root;

    public QueueCsvExportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-queue-csv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void ExportTgPending_writes_csv_with_header_and_rows()
    {
        var jobs = new[] { CreateTgPendingJob("a", "Game A") };

        var export = QueueCsvExportService.ExportTgPending(jobs, _root, limit: 10);

        Assert.Equal(1, export.EntryCount);
        Assert.True(File.Exists(export.ExportPath));
        var text = File.ReadAllText(export.ExportPath);
        Assert.Contains("jobId,title", text);
        Assert.Contains("Game A", text);
    }

    [Fact]
    public void ExportPublishQueue_writes_csv_for_pending_jobs()
    {
        var jobs = new[]
        {
            new PublishJob
            {
                Id = "q1",
                Title = "Pending Game",
                Status = JobStatus.Processed,
                Paths = new JobPaths { Slug = "pending-game" },
                Publish = new JobPublishState()
            }
        };

        var export = QueueCsvExportService.ExportPublishQueue(jobs, _root, limit: 10);

        Assert.Equal(1, export.EntryCount);
        Assert.Contains("publish-queue", export.ExportPath);
        Assert.Contains("Pending Game", File.ReadAllText(export.ExportPath));
    }

    private static PublishJob CreateTgPendingJob(string id, string title)
        => new()
        {
            Id = id,
            Title = title,
            Paths = new JobPaths { Slug = id },
            Publish = new JobPublishState
            {
                GeneratedCopy = "copy text",
                Baidu = new PublishChannelState { Link = "https://pan.baidu.com/s/a", Status = PublishStatusHelper.Published },
                Quark = new PublishChannelState { Link = "https://pan.quark.cn/s/b", Status = PublishStatusHelper.Published },
                Telegram = new PublishChannelState { Status = PublishStatusHelper.Pending }
            }
        };
}