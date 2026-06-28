using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchArchiveJobIdsServiceTests : IDisposable
{
    private readonly string _root;
    private readonly JobStore _store;

    public BatchArchiveJobIdsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-batch-archive-ids-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        var app = Path.Combine(_root, "app");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(app);
        File.WriteAllText(Path.Combine(data, "compress-config.json"), "{}");
        var ws = Path.Combine(_root, "workspace").Replace("\\", "\\\\");
        File.WriteAllText(Path.Combine(data, "studio-config.json"), $"{{\"workspaceRoot\":\"{ws}\"}}");

        var paths = new AppPaths(app);
        var studio = new StudioConfigService(paths);
        var workspace = new WorkspaceService(paths, studio);
        _store = new JobStore(workspace);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best effort cleanup
        }
    }

    [Fact]
    public void BatchArchiveJobIds_archives_selected_jobs_and_skips_archived()
    {
        var active = _store.Create("活跃任务");
        var archived = _store.Create("已归档");
        _store.Archive(archived.Id);

        var result = BatchArchiveJobIdsService.Archive([active.Id, archived.Id, "missing-id"], _store);

        Assert.Equal(1, result.Archived);
        Assert.Equal(2, result.Skipped);
        Assert.Single(result.AffectedJobIds);
        Assert.Equal(active.Id, result.AffectedJobIds[0]);
        Assert.Equal(JobStatus.Archived, _store.Get(active.Id)!.Status);
    }
}