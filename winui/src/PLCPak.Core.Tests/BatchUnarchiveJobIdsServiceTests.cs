using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchUnarchiveJobIdsServiceTests : IDisposable
{
    private readonly string _root;
    private readonly JobStore _store;

    public BatchUnarchiveJobIdsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-batch-unarchive-ids-" + Guid.NewGuid().ToString("N"));
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
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void BatchUnarchiveJobIds_restores_archived_jobs_and_skips_active()
    {
        var active = _store.Create("活跃任务");
        var archived = _store.Create("已归档");
        _store.Archive(archived.Id);

        var result = BatchUnarchiveJobIdsService.Unarchive([archived.Id, active.Id, "missing-id"], _store);

        Assert.Equal(1, result.Unarchived);
        Assert.Equal(2, result.Skipped);
        Assert.NotEqual(JobStatus.Archived, _store.Get(archived.Id)!.Status);
    }
}