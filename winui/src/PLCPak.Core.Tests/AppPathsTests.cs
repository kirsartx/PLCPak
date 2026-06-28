using PLCPak.Core;

namespace PLCPak.Core.Tests;

public sealed class AppPathsTests : IDisposable
{
    private readonly string _root;
    private readonly List<string> _cleanup = [];

    public AppPathsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _cleanup.Add(_root);
    }

    public void Dispose()
    {
        foreach (var path in _cleanup)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
                // ignore cleanup failures in tests
            }
        }
    }

    [Fact]
    public void Resolves_shared_dist_data_layout()
    {
        var dist = Path.Combine(_root, "dist");
        var app = Path.Combine(dist, "PLCPak.WinUI", "app");
        var data = Path.Combine(dist, "data");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "compress-config.json"), "{}");

        var paths = new AppPaths(app);

        Assert.Equal(Path.GetFullPath(data), paths.DataRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(dist, "PLCPak.WinUI")), paths.PackageRoot);
    }

    [Fact]
    public void Resolves_per_package_data_when_shared_missing()
    {
        var package = Path.Combine(_root, "PLCPak.WinUI");
        var app = Path.Combine(package, "app");
        var data = Path.Combine(package, "data");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(Path.Combine(data, "AD-Samples"));

        var paths = new AppPaths(app);

        Assert.Equal(Path.GetFullPath(data), paths.DataRoot);
    }

    [Fact]
    public void Startup_log_uses_shared_logs_directory()
    {
        var dist = Path.Combine(_root, "dist");
        var app = Path.Combine(dist, "PLCPak.Cli", "app");
        var logs = Path.Combine(dist, "logs");
        var data = Path.Combine(dist, "data");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(logs);
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "compress-config.json"), "{}");

        var paths = new AppPaths(app);

        Assert.Equal(Path.GetFullPath(logs), paths.LogsRoot);
        Assert.Equal(Path.Combine(logs, "plcpak-startup.log"), paths.StartupLogPath);
    }
}