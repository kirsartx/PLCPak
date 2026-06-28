using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class OperationsCenterServiceTests
{
    [Fact]
    public void BuildSnapshotFromJobs_matches_BuildSnapshot_for_same_jobs_list()
    {
        var jobs = new[]
        {
            CreateJob("待发布", JobStatus.Processed, DateTime.Now.AddDays(-10)),
            CreateJob("失败任务", JobStatus.Failed, DateTime.Now)
        };
        jobs[1].Error = "解压失败";

        var fromJobs = OperationsCenterService.BuildSnapshotFromJobs(jobs);
        var snapshot = OperationsCenterService.BuildSnapshot(jobs);

        Assert.Equal(snapshot.SummaryText, fromJobs.SummaryText);
        Assert.Equal(snapshot.Queue.Count, fromJobs.Queue.Count);
        Assert.Equal(snapshot.Sections.Count, fromJobs.Sections.Count);
    }

    [Fact]
    public void BuildSnapshot_aggregates_quick_stats_workflow_maintenance_health_and_queue()
    {
        var failed = CreateJob("失败任务", JobStatus.Failed, DateTime.Now);
        failed.Error = "解压失败";

        var processed = CreateJob("待发布", JobStatus.Processed, DateTime.Now.AddDays(-10));
        processed.Publish.Baidu.Link = "https://pan.baidu.com/s/test";

        var published = CreateJob("已发布", JobStatus.Published, DateTime.Now.AddDays(-1));
        published.Publish.GeneratedCopy = "文案";
        PublishStatusHelper.MarkPublished(published.Publish.Baidu, requireLink: false);
        PublishStatusHelper.MarkPublished(published.Publish.Quark, requireLink: false);

        var active = CreateJob("进行中", JobStatus.InboxReady, DateTime.Now);

        var snapshot = OperationsCenterService.BuildSnapshot(
            [failed, processed, published, active],
            workflowLimit: 3,
            queueLimit: 10,
            staleDays: 7);

        Assert.Contains("待发布 1", snapshot.QuickStatsLine);
        Assert.Contains("失败 1", snapshot.QuickStatsLine);
        Assert.Equal(1, snapshot.Queue.Count);
        Assert.Equal(1, snapshot.Maintenance.StaleCount);
        Assert.Equal(1, snapshot.Maintenance.PublishedReadyToArchive);
        Assert.True(snapshot.Health.IssueCount >= 2);
        Assert.NotNull(snapshot.Workflow.Primary);
        Assert.Equal(JobNextActionType.RetryFailed, snapshot.Workflow.Primary!.Action);
    }

    [Fact]
    public void BuildSnapshot_exposes_named_sections()
    {
        var snapshot = OperationsCenterService.BuildSnapshot([]);

        Assert.Equal(5, snapshot.Sections.Count);
        Assert.Equal(["quick-stats", "workflow", "maintenance", "health", "queue"],
            snapshot.Sections.Select(s => s.Key).ToList());
        Assert.Equal("概览", snapshot.Sections[0].Title);
        Assert.Contains("待发布 0", snapshot.Sections[0].SummaryText);
        Assert.Contains("失败 0", snapshot.Sections[0].SummaryText);
        Assert.Equal("维护检查：无待处理项", snapshot.Sections[2].SummaryText);
    }

    [Fact]
    public void BuildSnapshot_summary_includes_queue_health_and_primary_workflow_action()
    {
        var job = CreateJob("缺链接", JobStatus.Processed, DateTime.Now.AddDays(-8));

        var snapshot = OperationsCenterService.BuildSnapshot([job], workflowLimit: 1, queueLimit: 5);

        Assert.Contains("待发布 1", snapshot.SummaryText);
        Assert.Contains("失败 0", snapshot.SummaryText);
        Assert.Contains("队列 1", snapshot.SummaryText);
        Assert.Contains("健康", snapshot.SummaryText);
        Assert.Contains("维护检查", snapshot.SummaryText);
        Assert.Contains("首要: 回填发布链接", snapshot.SummaryText);
    }

    [Fact]
    public void BuildSnapshot_uses_no_action_hint_when_workflow_empty()
    {
        var snapshot = OperationsCenterService.BuildSnapshot([]);

        Assert.Contains("首要: 暂无建议操作", snapshot.SummaryText);
        Assert.Equal("发布工作流：暂无建议操作", snapshot.Workflow.SummaryText);
    }

    [Fact]
    public void BuildSnapshot_stale_days_affects_maintenance_counts()
    {
        var stale = CreateJob("陈旧任务", JobStatus.Processed, DateTime.Now.AddDays(-14));
        var fresh = CreateJob("新任务", JobStatus.Processed, DateTime.Now.AddDays(-1));

        var strict = OperationsCenterService.BuildSnapshot([stale, fresh], staleDays: 7);
        var relaxed = OperationsCenterService.BuildSnapshot([stale, fresh], staleDays: 30);

        Assert.Equal(1, strict.Maintenance.StaleCount);
        Assert.Equal(0, relaxed.Maintenance.StaleCount);
        Assert.Contains("维护检查", strict.SummaryText);
    }

    [Fact]
    public void FormatSectionLines_formats_snapshot_sections_for_cli_and_gui()
    {
        var snapshot = OperationsCenterService.BuildSnapshot([]);
        snapshot.Sections.Add(new OperationsCenterSection
        {
            Key = "activity-log-stats",
            Title = "活动日志统计",
            SummaryText = "活动日志统计（最近 30 天）：1 条"
        });

        var lines = OperationsCenterService.FormatSectionLines(snapshot);

        Assert.Contains("活动日志统计: 活动日志统计（最近 30 天）：1 条", lines);
    }

    [Fact]
    public void BuildSnapshot_queue_limit_caps_publish_queue_entries()
    {
        var jobs = Enumerable.Range(1, 5)
            .Select(i => CreateJob($"队列{i}", JobStatus.Processed, DateTime.Now.AddMinutes(-i)))
            .ToList();

        var snapshot = OperationsCenterService.BuildSnapshot(jobs, queueLimit: 2);

        Assert.Equal(2, snapshot.Queue.Count);
        Assert.Contains("队列 2", snapshot.SummaryText);
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