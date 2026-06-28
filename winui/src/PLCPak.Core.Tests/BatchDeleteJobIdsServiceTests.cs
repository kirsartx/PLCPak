using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchDeleteJobIdsServiceTests : IDisposable
{
    private readonly string _root;
    private readonly JobStore _store;
    private readonly WorkspaceService _workspace;

    public BatchDeleteJobIdsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-batch-delete-ids-" + Guid.NewGuid().ToString("N"));
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
    public void Preview_lists_jobs_and_folder_candidates()
    {
        var job = _store.Create("待删任务");
        Directory.CreateDirectory(job.Paths.Inbox);

        var preview = BatchDeleteJobIdsService.Preview([job.Id, "missing"], _store, _workspace);

        Assert.Equal(1, preview.Count);
        Assert.Contains(job.Title, preview.SampleTitles);
        Assert.True(preview.FolderCandidateCount >= 1);
        Assert.Contains("批量删除预览", preview.SummaryText);
    }

    [Fact]
    public void Delete_removes_jobs_and_optional_folders()
    {
        var job = _store.Create("删除目录");
        Directory.CreateDirectory(job.Paths.Inbox);

        var result = BatchDeleteJobIdsService.Delete([job.Id], _store, _workspace, deleteFolders: true);

        Assert.Equal(1, result.Deleted);
        Assert.Null(_store.Get(job.Id));
        Assert.False(Directory.Exists(job.Paths.Inbox));
    }

    [Fact]
    public void Delete_with_recycle_flag_sets_use_recycle_bin()
    {
        var job = _store.Create("回收站标记");
        var result = BatchDeleteJobIdsService.Delete(
            [job.Id],
            _store,
            _workspace,
            deleteFolders: true,
            useRecycleBin: true);

        Assert.Equal(1, result.Deleted);
        Assert.True(result.UseRecycleBin);
    }
}