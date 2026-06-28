using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class DashboardSnapshotServiceTests
{
    [Fact]
    public void Build_aggregates_stats_queue_tg_workflow_and_health_from_single_jobs_list()
    {
        var jobs = new List<PublishJob>
        {
            new()
            {
                Id = "failed-1",
                Title = "失败任务",
                Status = JobStatus.Failed,
                UpdatedAt = DateTime.Now.AddHours(-1)
            },
            new()
            {
                Id = "pending-1",
                Title = "待发布",
                Status = JobStatus.Processed,
                UpdatedAt = DateTime.Now
            },
            new()
            {
                Id = "tg-1",
                Title = "TG待发",
                Status = JobStatus.Processed,
                UpdatedAt = DateTime.Now.AddMinutes(-5),
                Publish = new JobPublishState
                {
                    GeneratedCopy = "copy text",
                    Baidu = new PublishChannelState { Link = "https://pan.baidu.com/s/tg", Status = PublishStatusHelper.Published },
                    Quark = new PublishChannelState { Link = "https://pan.quark.cn/s/tg", Status = PublishStatusHelper.Published },
                    Telegram = new PublishChannelState()
                }
            }
        };

        var snapshot = DashboardSnapshotService.Build(jobs, publishQueueLimit: 10, tgLimit: 10, workflowLimit: 5);

        Assert.Equal(3, snapshot.StatsSummary.Total);
        Assert.Equal(1, snapshot.StatsSummary.Failed);
        Assert.Equal(2, snapshot.StatsSummary.PendingPublish);
        Assert.Equal(2, snapshot.PublishQueue.Count);
        Assert.Equal(1, snapshot.TgPending.Count);
        Assert.NotNull(snapshot.Workflow.Primary);
        Assert.Equal(JobNextActionType.RetryFailed, snapshot.Workflow.Primary!.Action);
        Assert.Contains("失败 1", snapshot.SummaryText);
        Assert.Contains("待发布队列 2", snapshot.SummaryText);
        Assert.Contains("TG待发 1", snapshot.SummaryText);
    }

    [Fact]
    public void Build_respects_publish_queue_and_tg_limits()
    {
        var jobs = Enumerable.Range(1, 6)
            .Select(i => new PublishJob
            {
                Id = $"job-{i}",
                Title = $"队列{i}",
                Status = JobStatus.Processed,
                UpdatedAt = DateTime.Now.AddMinutes(-i),
                Publish = new JobPublishState
                {
                    GeneratedCopy = $"copy-{i}",
                    Baidu = new PublishChannelState { Link = $"https://pan.baidu.com/s/{i}", Status = PublishStatusHelper.Published },
                    Quark = new PublishChannelState { Link = $"https://pan.quark.cn/s/{i}", Status = PublishStatusHelper.Published },
                    Telegram = new PublishChannelState()
                }
            })
            .ToList();

        var snapshot = DashboardSnapshotService.Build(jobs, publishQueueLimit: 2, tgLimit: 3, workflowLimit: 1);

        Assert.Equal(2, snapshot.PublishQueue.Count);
        Assert.Equal(3, snapshot.TgPending.Count);
        Assert.NotNull(snapshot.Workflow.Primary);
    }

    [Fact]
    public void DashboardSnapshotService_Build_uses_single_job_list()
    {
        var jobs = new SingleEnumerationJobs(
        [
            new()
            {
                Id = "single-enum",
                Title = "单次枚举",
                Status = JobStatus.Processed,
                UpdatedAt = DateTime.Now
            }
        ]);

        var snapshot = DashboardSnapshotService.Build(jobs, publishQueueLimit: 5, tgLimit: 5, workflowLimit: 2);

        Assert.Equal(1, jobs.EnumerationCount);
        Assert.Equal(1, snapshot.StatsSummary.Total);
        Assert.Equal(1, snapshot.PublishQueue.Count);
    }

    [Fact]
    public void Build_accepts_readonly_list_without_copying()
    {
        IReadOnlyList<PublishJob> jobs =
        [
            new()
            {
                Id = "only",
                Title = "唯一",
                Status = JobStatus.Draft,
                UpdatedAt = DateTime.Now
            }
        ];

        var snapshot = DashboardSnapshotService.Build(jobs);

        Assert.Equal(1, snapshot.StatsSummary.Total);
        Assert.Equal("共 1 | 已发布 0 | 待发布 0 | 已压缩 0 | 进行中 1 | 失败 0 | 归档 0",
            snapshot.StatsSummary.SummaryText);
    }

    [Fact]
    public void StudioConfig_Clone_includes_activity_log_page_size()
    {
        var config = new StudioConfig
        {
            ActivityLogPageSize = 35
        };

        var clone = config.Clone();

        Assert.Equal(35, clone.ActivityLogPageSize);
        Assert.Equal(20, new StudioConfig().ActivityLogPageSize);
    }

    private sealed class SingleEnumerationJobs(IReadOnlyList<PublishJob> jobs) : IEnumerable<PublishJob>
    {
        private int _enumerationCount;

        public int EnumerationCount => _enumerationCount;

        public IEnumerator<PublishJob> GetEnumerator()
        {
            _enumerationCount++;
            if (_enumerationCount > 1)
                throw new InvalidOperationException("jobs should only be enumerated once");

            return jobs.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}