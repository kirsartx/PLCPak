using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class MaintenanceServiceTests : IDisposable
{
    private readonly string _root;
    private readonly JobStore _store;
    private readonly WorkspaceService _workspace;

    public MaintenanceServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-maint-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        var appRoot = Path.Combine(_root, "app");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(appRoot);
        File.WriteAllText(Path.Combine(data, "compress-config.json"), "{}");
        var ws = Path.Combine(_root, "workspace").Replace("\\", "\\\\");
        File.WriteAllText(Path.Combine(data, "studio-config.json"), $"{{\"workspaceRoot\":\"{ws}\"}}");

        var appContext = PlcPakAppContext.Create(appRoot);
        _store = appContext.Jobs;
        _workspace = appContext.Workspace;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void GetStaleJobs_flags_processed_jobs_without_full_links_older_than_threshold()
    {
        var stale = CreateJob("长期待回填", JobStatus.Processed, DateTime.Now.AddDays(-10));
        var recent = CreateJob("刚压缩", JobStatus.Processed, DateTime.Now.AddDays(-2));
        var complete = CreateJob("链接已齐", JobStatus.Processed, DateTime.Now.AddDays(-10));
        complete.Publish.Baidu.Link = "https://pan.baidu.com/s/test";
        complete.Publish.Quark.Link = "https://pan.quark.cn/s/test";
        complete.Publish.Telegram.Link = "https://t.me/test";

        var staleJobs = MaintenanceService.GetStaleJobs([stale, recent, complete], staleDays: 7);

        Assert.Single(staleJobs);
        Assert.Equal(stale.Id, staleJobs[0].JobId);
        Assert.True(staleJobs[0].DaysSinceUpdate >= 10);
        Assert.Contains("百度", staleJobs[0].Reason);
    }

    [Fact]
    public void BuildReport_counts_stale_and_published_ready_to_archive()
    {
        var stale = CreateJob("长期待回填", JobStatus.Processed, DateTime.Now.AddDays(-8));
        var published = CreateJob("可归档", JobStatus.Published, DateTime.Now.AddDays(-1));
        var archived = CreateJob("已归档", JobStatus.Archived, DateTime.Now.AddDays(-30));

        var report = MaintenanceService.BuildReport([stale, published, archived], staleDays: 7);

        Assert.Equal(1, report.StaleCount);
        Assert.Equal(1, report.PublishedReadyToArchive);
        Assert.Single(report.StaleJobs);
        Assert.Contains("长期待发布 1 个", report.SummaryText);
        Assert.Contains("可归档已发布 1 个", report.SummaryText);
    }

    [Fact]
    public void BuildReport_reports_no_pending_items_when_clean()
    {
        var job = CreateJob("正常", JobStatus.InboxReady, DateTime.Now);

        var report = MaintenanceService.BuildReport([job], staleDays: 7);

        Assert.Equal(0, report.StaleCount);
        Assert.Equal(0, report.PublishedReadyToArchive);
        Assert.Equal("维护检查：无待处理项", report.SummaryText);
    }

    [Fact]
    public void BulkArchivePublished_archives_all_published_jobs_when_older_than_days_is_zero()
    {
        var published = _store.Create("已发布A");
        published.Status = JobStatus.Published;
        _store.Save(published);

        var draft = _store.Create("草稿B");

        var result = MaintenanceService.BulkArchivePublished(_store.List(), _store, olderThanDays: 0);

        Assert.Equal(1, result.Archived);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(JobStatus.Archived, _store.Get(published.Id)!.Status);
        Assert.Equal(JobStatus.Draft, _store.Get(draft.Id)!.Status);
        Assert.Contains(result.Messages, m => m.Contains("[归档] 已发布A"));
        Assert.Contains(result.Messages, m => m.Contains("[跳过] 草稿B"));
    }

    [Fact]
    public void BulkArchivePublished_respects_older_than_days_filter()
    {
        var oldPublished = SavePublishedJob("旧已发布", DateTime.Now.AddDays(-10));
        var newPublished = SavePublishedJob("新已发布", DateTime.Now.AddDays(-1));

        var result = MaintenanceService.BulkArchivePublished(_store.List(), _store, olderThanDays: 7);

        Assert.Equal(1, result.Archived);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(JobStatus.Archived, _store.Get(oldPublished.Id)!.Status);
        Assert.Equal(JobStatus.Published, _store.Get(newPublished.Id)!.Status);
    }

    private PublishJob SavePublishedJob(string title, DateTime updatedAt)
    {
        var job = _store.Create(title);
        job.Status = JobStatus.Published;
        job.UpdatedAt = updatedAt;
        JsonHelper.WriteFile(Path.Combine(_workspace.JobsDirectory, $"{job.Id}.json"), job);
        return job;
    }

    private static PublishJob CreateJob(string title, JobStatus status, DateTime updatedAt)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            Status = status,
            UpdatedAt = updatedAt
        };
}