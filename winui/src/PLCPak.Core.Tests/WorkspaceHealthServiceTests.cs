using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class WorkspaceHealthServiceTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceService _workspace;
    private readonly JobStore _store;
    private readonly WorkspaceHealthService _health;

    public WorkspaceHealthServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-health-" + Guid.NewGuid().ToString("N"));
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
        _health = new WorkspaceHealthService(_workspace, _store);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Scan_detects_orphan_dirs_and_missing_inbox()
    {
        var job = _store.Create("健康检查游戏");
        Directory.Delete(job.Paths.Inbox, recursive: true);

        var orphan = Path.Combine(_workspace.InboxDirectory, "orphan-slug");
        Directory.CreateDirectory(orphan);
        File.WriteAllText(Path.Combine(orphan, "old.7z"), "x");

        var report = _health.Scan();

        Assert.Contains(orphan, report.OrphanInboxDirs);
        Assert.Single(report.JobsWithoutInbox);
        Assert.Equal(job.Id, report.JobsWithoutInbox[0].JobId);
        Assert.True(report.TotalBytes > 0);
    }
}