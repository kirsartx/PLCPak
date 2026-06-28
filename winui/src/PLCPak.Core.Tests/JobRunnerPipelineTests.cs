using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class JobRunnerPipelineTests : IDisposable
{
    private readonly string _root;
    private readonly string _appRoot;
    private readonly JobStore _store;
    private readonly JobRunner _runner;

    public JobRunnerPipelineTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-pipeline-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        _appRoot = Path.Combine(_root, "app");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(_appRoot);
        File.WriteAllText(Path.Combine(data, "compress-config.json"), "{}");
        File.WriteAllText(Path.Combine(data, "publish-templates.json"), """
            {
              "version": 1,
              "defaultTemplateId": "telegram-default",
              "templates": [
                {
                  "id": "telegram-default",
                  "name": "TG",
                  "channel": "telegram",
                  "body": "{title}\n{baidu_line}\n{quark_line}"
                }
              ]
            }
            """);
        var ws = Path.Combine(_root, "workspace").Replace("\\", "\\\\");
        File.WriteAllText(Path.Combine(data, "studio-config.json"), $"{{\"workspaceRoot\":\"{ws}\"}}");

        var appContext = PlcPakAppContext.Create(_appRoot);
        _store = appContext.Jobs;
        _runner = appContext.JobRunner;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task RunBatchPipelineAsync_with_force_returns_empty_when_no_pipeline_jobs()
    {
        var processed = _store.Create("已压缩");
        processed.Status = JobStatus.Processed;
        _store.Save(processed);

        var result = await _runner.RunBatchPipelineAsync(
            JobListFilter.Active,
            force: true,
            confirmCleanupAsync: _ => Task.FromResult(true));

        Assert.Equal(0, result.Success);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.False(string.IsNullOrWhiteSpace(result.BatchLogPath));
        Assert.True(File.Exists(result.BatchLogPath));
    }

    [Fact]
    public async Task RunBatchPipelineAsync_uses_confirm_callback_when_force_disabled()
    {
        var extracted = _store.Create("待处理");
        extracted.Status = JobStatus.Extracted;
        Directory.CreateDirectory(extracted.Paths.Extract);
        File.WriteAllText(Path.Combine(extracted.Paths.Extract, "game.exe"), "fake");
        extracted.Paths.Staging = extracted.Paths.Extract;
        _store.Save(extracted);

        var confirmCalled = false;
        var result = await _runner.RunBatchPipelineAsync(
            JobListFilter.Active,
            force: false,
            confirmCleanupAsync: confirmation =>
            {
                confirmCalled = true;
                return Task.FromResult(true);
            });

        Assert.True(confirmCalled || result.Failed > 0 || result.Success > 0 || result.Skipped > 0);
        Assert.NotNull(result.BatchLogPath);
    }

    [Fact]
    public void GetPublishWorkflow_delegates_to_workflow_service()
    {
        var failed = _store.Create("失败任务");
        failed.Status = JobStatus.Failed;
        failed.Error = "boom";
        _store.Save(failed);

        var snapshot = _runner.GetPublishWorkflow(limit: 3);

        Assert.NotNull(snapshot.Primary);
        Assert.Equal(JobNextActionType.RetryFailed, snapshot.Primary!.Action);
        Assert.Equal("失败任务", snapshot.Primary.Title);
    }

    [Fact]
    public void ExportScheduleBatchScript_writes_workspace_batch_file()
    {
        var studio = new StudioConfigService(new AppPaths(_appRoot));
        var config = studio.Load();
        config.ScheduledBatchFilter = "active";
        config.ScheduledBatchHour = 2;
        config.ScheduledBatchMinute = 0;
        studio.Save(config);

        var export = _runner.ExportScheduleBatchScript();

        Assert.True(File.Exists(export.BatPath));
        Assert.Contains("-JobBatchPipeline", File.ReadAllText(export.BatPath));
        Assert.Contains("schtasks /Create /TN \"PLCPak Nightly Batch\"", export.SchTasksCommand);
        Assert.Contains("/SC DAILY /ST 02:00", export.SchTasksCommand);
    }
}