using PLCPak.Core;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class WorkspaceServiceTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceService _workspace;

    public WorkspaceServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-ws-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        var app = Path.Combine(_root, "app");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(app);
        File.WriteAllText(Path.Combine(data, "compress-config.json"), "{}");

        var paths = new AppPaths(app);
        _workspace = new WorkspaceService(paths, new StudioConfigService(paths));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void FindArchives_detects_archives_in_inbox()
    {
        var inbox = Path.Combine(_workspace.GetWorkspaceRoot(), "inbox", "demo");
        Directory.CreateDirectory(inbox);
        File.WriteAllText(Path.Combine(inbox, "game.7z"), "fake");
        File.WriteAllText(Path.Combine(inbox, "readme.txt"), "x");

        var archives = _workspace.FindArchives(inbox);

        Assert.Single(archives);
        Assert.EndsWith("game.7z", archives[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Slugify_removes_invalid_path_characters()
    {
        var slug = WorkspaceService.Slugify("测试:游戏*名称");
        Assert.DoesNotContain(":", slug);
        Assert.DoesNotContain("*", slug);
    }
}