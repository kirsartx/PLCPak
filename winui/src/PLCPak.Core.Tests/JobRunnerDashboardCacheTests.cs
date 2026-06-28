using System.Reflection;
using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class JobRunnerDashboardCacheTests : IDisposable
{
    private readonly string _root;
    private readonly string _appRoot;
    private readonly JobStore _store;
    private readonly JobRunner _runner;

    public JobRunnerDashboardCacheTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-dash-cache-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        _appRoot = Path.Combine(_root, "app");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(_appRoot);
        File.WriteAllText(Path.Combine(data, "compress-config.json"), "{}");
        File.WriteAllText(Path.Combine(data, "publish-templates.json"), "{}");
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
    public void GetDashboardSnapshot_matches_individual_queries_with_shared_jobs_list()
    {
        var failed = _store.Create("失败任务");
        failed.Status = JobStatus.Failed;
        failed.Error = "boom";
        _store.Save(failed);

        var processed = _store.Create("待发布");
        processed.Status = JobStatus.Processed;
        processed.Publish.GeneratedCopy = "copy";
        _store.Save(processed);

        var jobs = _store.List();
        var snapshot = _runner.GetDashboardSnapshot(
            publishQueueLimit: 10,
            tgLimit: 20,
            workflowLimit: 3);

        Assert.Equal(PublishDashboardService.ComputeStats(jobs).SummaryText, snapshot.StatsSummary.SummaryText);
        Assert.Equal(_runner.GetJobHealth(jobs).SummaryText, snapshot.JobHealth.SummaryText);
        Assert.Equal(_runner.GetPublishQueue(10, jobs).SummaryText, snapshot.PublishQueue.SummaryText);
        Assert.Equal(_runner.GetPublishWorkflow(3, jobs).SummaryText, snapshot.Workflow.SummaryText);
        Assert.Equal(_runner.GetTgPendingQueue(20, jobs).SummaryText, snapshot.TgPending.SummaryText);
    }

    [Fact]
    public void GetPublishQueue_with_jobs_skips_store_list()
    {
        var job = _store.Create("内存队列");
        job.Status = JobStatus.Processed;
        job.UpdatedAt = DateTime.Now;
        var jobs = new List<PublishJob> { job };

        var snapshot = _runner.GetPublishQueue(5, jobs);

        Assert.Equal(1, snapshot.Count);
        Assert.Equal(job.Id, snapshot.Entries[0].JobId);
    }

    [Fact]
    public void GetOperationsCenterSnapshot_uses_cache_until_invalidated()
    {
        var processed = _store.Create("运营快照");
        processed.Status = JobStatus.Processed;
        _store.Save(processed);

        var first = _runner.GetOperationsCenterSnapshot();
        Assert.NotNull(GetOperationsSnapshotCache(_runner));
        Assert.Contains("待发布 1", first.SummaryText);

        var second = _runner.GetOperationsCenterSnapshot();
        Assert.Same(first, second);

        _runner.InvalidateDuplicateCache();
        Assert.Null(GetOperationsSnapshotCache(_runner));

        var third = _runner.GetOperationsCenterSnapshot();
        Assert.NotSame(first, third);
        Assert.Contains("待发布 1", third.SummaryText);
    }

    [Fact]
    public void LogActivity_refreshes_operations_snapshot_when_jobs_unchanged()
    {
        _store.Create("运营缓存");
        var first = _runner.GetOperationsCenterSnapshot();
        Assert.DoesNotContain("活动日志统计", first.SummaryText);

        _runner.LogActivity("export", "缓存刷新测试");
        var second = _runner.GetOperationsCenterSnapshot();

        Assert.NotSame(first, second);
        Assert.Contains("活动日志统计", second.SummaryText);
    }

    [Fact]
    public void CreateJob_invalidates_operations_snapshot_cache()
    {
        _store.Create("运营缓存");
        _runner.GetOperationsCenterSnapshot();
        Assert.NotNull(GetOperationsSnapshotCache(_runner));

        _runner.CreateJob("运营缓存");
        Assert.Null(GetOperationsSnapshotCache(_runner));
    }

    [Fact]
    public void GetDuplicateOperationsBundle_uses_cache_until_invalidated()
    {
        _store.Create("缓存A");
        _store.Create("缓存A");

        var first = _runner.GetDuplicateOperationsBundle();
        Assert.Equal(1, first.Scan.GroupCount);
        Assert.NotNull(GetDuplicateBundleCache(_runner));

        _runner.InvalidateDuplicateCache();
        Assert.Null(GetDuplicateBundleCache(_runner));

        var second = _runner.GetDuplicateOperationsBundle();
        Assert.Equal(1, second.Scan.GroupCount);
        Assert.NotNull(GetDuplicateBundleCache(_runner));
    }

    [Fact]
    public void MergeJobs_invalidates_duplicate_cache()
    {
        var source = _store.Create("合并缓存");
        var target = _store.Create("合并缓存");
        target.Publish.Baidu.Link = "https://pan.baidu.com/s/keep";
        _store.Save(target);

        _runner.GetDuplicateOperationsBundle();
        Assert.NotNull(GetDuplicateBundleCache(_runner));

        var merge = _runner.MergeJobs(target.Id, source.Id, archiveSource: false);
        Assert.True(merge.Success);
        Assert.Null(GetDuplicateBundleCache(_runner));
        Assert.Equal(0, _runner.ScanDuplicateJobs().GroupCount);
    }

    [Fact]
    public void DeleteJob_invalidates_duplicate_cache()
    {
        var keep = _store.Create("保留");
        var remove = _store.Create("保留");
        _store.Save(keep);

        _runner.GetDuplicateOperationsBundle();
        Assert.NotNull(GetDuplicateBundleCache(_runner));

        _runner.DeleteJob(remove.Id);
        Assert.Null(GetDuplicateBundleCache(_runner));
    }

    [Fact]
    public void ArchiveJob_invalidates_duplicate_cache()
    {
        var first = _store.Create("归档缓存");
        var second = _store.Create("归档缓存");

        _runner.GetDuplicateOperationsBundle();
        Assert.NotNull(GetDuplicateBundleCache(_runner));

        _runner.ArchiveJob(second.Id);
        Assert.Null(GetDuplicateBundleCache(_runner));
        Assert.Equal(2, _store.List().Count);
        Assert.Equal(1, _runner.ScanDuplicateJobs().GroupCount);
    }

    [Fact]
    public void CreateJob_invalidates_duplicate_cache()
    {
        _store.Create("创建缓存");
        _store.Create("创建缓存");

        _runner.GetDuplicateOperationsBundle();
        Assert.NotNull(GetDuplicateBundleCache(_runner));

        _runner.CreateJob("创建缓存");
        Assert.Null(GetDuplicateBundleCache(_runner));
    }

    [Fact]
    public void ImportJob_invalidates_duplicate_cache()
    {
        _store.Create("导入缓存");
        _store.Create("导入缓存");

        _runner.GetDuplicateOperationsBundle();
        Assert.NotNull(GetDuplicateBundleCache(_runner));

        var exportJob = _store.Create("导入新任务");
        var exportPath = Path.Combine(_root, "import-cache.json");
        _runner.ExportJobJson(exportJob.Id, exportPath);

        _runner.ImportJob(exportPath);
        Assert.Null(GetDuplicateBundleCache(_runner));
    }

    [Fact]
    public void BatchMergeDuplicateJobs_invalidates_duplicate_cache_when_merged()
    {
        _store.Create("批量合并缓存");
        _store.Create("批量合并缓存");

        var cached = _runner.GetDuplicateOperationsBundle();
        Assert.NotNull(GetDuplicateBundleCache(_runner));

        var result = _runner.BatchMergeDuplicateJobs();
        Assert.Equal(1, result.Merged);
        Assert.Null(GetDuplicateBundleCache(_runner));

        var refreshed = _runner.GetDuplicateOperationsBundle();
        Assert.NotSame(cached, refreshed);
        Assert.Equal(1, result.Merged);
        Assert.Equal(1, _store.List().Count(j => j.Status != JobStatus.Archived));
    }

    private static object? GetDuplicateBundleCache(JobRunner runner)
    {
        var field = typeof(JobRunner).GetField("_duplicateBundleCache", BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(runner);
    }

    private static object? GetOperationsSnapshotCache(JobRunner runner)
    {
        var field = typeof(JobRunner).GetField("_operationsSnapshotCache", BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(runner);
    }
}