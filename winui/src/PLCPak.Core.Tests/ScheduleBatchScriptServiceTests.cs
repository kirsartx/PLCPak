using PLCPak.Core;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ScheduleBatchScriptServiceTests : IDisposable
{
    private readonly string _workspaceRoot;

    public ScheduleBatchScriptServiceTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "plcpak-schedule-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
            Directory.Delete(_workspaceRoot, recursive: true);
    }

    [Fact]
    public void Export_writes_bat_with_batch_pipeline_flags()
    {
        var options = new ScheduleBatchOptions
        {
            CliExePath = @"..\PLCPak.Cli\app\PLCPak.Cli.exe",
            Filter = "active",
            Hour = 2,
            Minute = 30,
            TaskName = "PLCPak Nightly Batch"
        };

        var export = ScheduleBatchScriptService.Export(_workspaceRoot, options);

        Assert.True(File.Exists(export.BatPath));
        Assert.EndsWith("nightly-batch.bat", export.BatPath, StringComparison.OrdinalIgnoreCase);

        var bat = File.ReadAllText(export.BatPath);
        Assert.Contains(@"-JobBatchPipeline", bat);
        Assert.Contains(@"-Filter active", bat);
        Assert.Contains(@"-Force", bat);
        Assert.Contains(@"-NoGui", bat);
        Assert.Contains(@"..\PLCPak.Cli\app\PLCPak.Cli.exe", bat);
    }

    [Fact]
    public void Export_builds_schtasks_command_with_daily_schedule()
    {
        var options = new ScheduleBatchOptions
        {
            Filter = "active",
            Hour = 3,
            Minute = 15,
            TaskName = "PLCPak Nightly Batch"
        };

        var export = ScheduleBatchScriptService.Export(_workspaceRoot, options);
        var batPath = export.BatPath;

        Assert.Equal(
            $"schtasks /Create /TN \"PLCPak Nightly Batch\" /TR \"{batPath}\" /SC DAILY /ST 03:15",
            export.SchTasksCommand);
        Assert.Contains("注册计划任务", export.ReadmeText);
        Assert.Contains(export.SchTasksCommand, export.ReadmeText);
    }

    [Fact]
    public void TryRegisterScheduledTask_forms_schtasks_command_and_returns_message()
    {
        var options = new ScheduleBatchOptions
        {
            Filter = "active",
            Hour = 1,
            Minute = 45,
            TaskName = "PLCPak Test Batch"
        };

        var export = ScheduleBatchScriptService.Export(_workspaceRoot, options);
        var result = ScheduleBatchScriptService.TryRegisterScheduledTask(export, "PLCPak Test Batch");

        Assert.False(string.IsNullOrWhiteSpace(result.SchTasksCommand));
        Assert.Contains("schtasks /Create", result.SchTasksCommand);
        Assert.Contains("PLCPak Test Batch", result.SchTasksCommand);
        Assert.Contains(export.BatPath, result.SchTasksCommand);
        Assert.Contains("/ST 01:45", result.SchTasksCommand);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    [Fact]
    public void ResolveDefaultCliExePath_points_to_dist_cli_layout()
    {
        var appRoot = Path.Combine(_workspaceRoot, "dist", "PLCPak.WinUI", "app");
        Directory.CreateDirectory(appRoot);

        var paths = new AppPaths(appRoot);
        var cliPath = ScheduleBatchScriptService.ResolveDefaultCliExePath(paths);

        Assert.EndsWith(Path.Combine("PLCPak.Cli", "app", "PLCPak.Cli.exe"), cliPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(cliPath.Contains("dist", StringComparison.OrdinalIgnoreCase));
    }
}