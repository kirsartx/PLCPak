using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class JobStoreTests : IDisposable
{
    private readonly string _root;
    private readonly JobStore _store;
    private readonly WorkspaceService _workspace;

    public JobStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-job-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        var app = Path.Combine(_root, "app");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(app);
        File.WriteAllText(Path.Combine(data, "compress-config.json"), "{}");
        var ws = Path.Combine(_root, "workspace").Replace("\\", "\\\\");
        File.WriteAllText(Path.Combine(data, "studio-config.json"), $"{{\"workspaceRoot\":\"{ws}\"}}");

        var paths = new AppPaths(app);
        var studio = new StudioConfigService(paths);
        _workspace = new WorkspaceService(paths, studio);
        _store = new JobStore(_workspace);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Create_persists_job_with_inbox_paths()
    {
        var job = _store.Create("测试游戏");

        Assert.False(string.IsNullOrWhiteSpace(job.Id));
        Assert.Equal("测试游戏", job.Title);
        Assert.True(Directory.Exists(job.Paths.Inbox));

        var loaded = _store.Get(job.Id);
        Assert.NotNull(loaded);
        Assert.Equal(job.Id, loaded!.Id);
    }

    [Fact]
    public void Archive_and_unarchive_restore_processed_when_output_exists()
    {
        var job = _store.Create("归档测试");
        Directory.CreateDirectory(job.Paths.Output);
        var outputFile = Path.Combine(job.Paths.Output, "game.7z");
        File.WriteAllText(outputFile, "fake");
        job.Status = JobStatus.Processed;
        job.Artifacts.OutputArchives = [outputFile];
        _store.Save(job);

        var archived = _store.Archive(job.Id);
        Assert.Equal(JobStatus.Archived, archived.Status);
        Assert.Equal(JobStatus.Processed, archived.ArchivedFromStatus);

        var restored = _store.Unarchive(job.Id);
        Assert.Equal(JobStatus.Processed, restored.Status);
        Assert.Null(restored.ArchivedFromStatus);
    }

    [Fact]
    public void Unarchive_restores_inbox_ready_when_archives_exist()
    {
        var job = _store.Create("取消归档");
        File.WriteAllText(Path.Combine(job.Paths.Inbox, "game.7z"), "fake");
        _store.Archive(job.Id);

        var restored = _store.Unarchive(job.Id);

        Assert.Equal(JobStatus.InboxReady, restored.Status);
    }

    [Fact]
    public void List_returns_jobs_newest_first()
    {
        var first = _store.Create("A");
        Thread.Sleep(20);
        var second = _store.Create("B");

        var list = _store.List();
        Assert.Equal(2, list.Count);
        Assert.Equal(second.Id, list[0].Id);
        Assert.Equal(first.Id, list[1].Id);
    }
}