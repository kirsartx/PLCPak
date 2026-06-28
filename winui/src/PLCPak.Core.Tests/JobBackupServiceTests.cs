using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class JobBackupServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _workspaceRoot;
    private readonly JobStore _store;

    public JobBackupServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-backup-" + Guid.NewGuid().ToString("N"));
        _workspaceRoot = Path.Combine(_root, "workspace");
        Directory.CreateDirectory(_workspaceRoot);

        var data = Path.Combine(_root, "data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "studio-config.json"),
            $"{{\"workspaceRoot\":\"{_workspaceRoot.Replace("\\", "\\\\")}\"}}");

        var workspace = new WorkspaceService(new AppPaths(Path.Combine(_root, "app")), new StudioConfigService(new AppPaths(Path.Combine(_root, "app"))));
        _store = new JobStore(workspace);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void ExportAll_captures_jobs_and_version()
    {
        var job = _store.Create("备份任务一");
        var snapshot = JobBackupService.ExportAll(_store.List());

        Assert.Equal(AppVersion.Current, snapshot.Version);
        Assert.Equal(1, snapshot.Count);
        Assert.Single(snapshot.Jobs);
        Assert.Equal(job.Id, snapshot.Jobs[0].Id);
    }

    [Fact]
    public void ExportToFile_writes_backup_under_workspace_backups()
    {
        _store.Create("导出任务");
        var path = JobBackupService.ExportToFile(_workspaceRoot, _store.List());

        Assert.StartsWith(Path.Combine(_workspaceRoot, "backups", "jobs-backup-"), path);
        Assert.EndsWith(".json", path);
        Assert.True(File.Exists(path));

        var snapshot = JsonHelper.ReadFile<JobBackupSnapshot>(path);
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.Count);
    }

    [Fact]
    public void ImportFromFile_roundtrip_restores_jobs()
    {
        var first = _store.Create("任务 A");
        var second = _store.Create("任务 B");
        var path = JobBackupService.ExportToFile(_workspaceRoot, _store.List());

        _store.Delete(first.Id);
        _store.Delete(second.Id);
        Assert.Empty(_store.List());

        var result = JobBackupService.ImportFromFile(_store, path, JobBackupImportMode.Merge);

        Assert.Equal(2, result.Imported);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(2, _store.List().Count);
        Assert.Contains(_store.List(), j => j.Title == "任务 A");
        Assert.Contains(_store.List(), j => j.Title == "任务 B");
    }

    [Fact]
    public void ImportFromFile_skipExisting_skips_conflicting_ids()
    {
        var job = _store.Create("已存在任务");
        var path = JobBackupService.ExportToFile(_workspaceRoot, _store.List());

        var result = JobBackupService.ImportFromFile(_store, path, JobBackupImportMode.SkipExisting);

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Skipped);
        Assert.Single(_store.List());
        Assert.Equal(job.Id, _store.List()[0].Id);
    }

    [Fact]
    public void ImportFromFile_merge_assigns_new_ids_on_conflict()
    {
        var job = _store.Create("冲突任务");
        var path = JobBackupService.ExportToFile(_workspaceRoot, _store.List());

        var result = JobBackupService.ImportFromFile(_store, path, JobBackupImportMode.Merge);

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Updated);
        Assert.Equal(2, _store.List().Count);
        Assert.Equal(2, _store.List().Count(j => j.Title == "冲突任务"));
        Assert.NotEqual(job.Id, _store.List().First(j => j.Id != job.Id).Id);
    }
}