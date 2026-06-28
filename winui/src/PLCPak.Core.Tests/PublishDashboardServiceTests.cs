using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class PublishDashboardServiceTests
{
    [Fact]
    public void ComputeStats_counts_job_buckets()
    {
        var jobs = new[]
        {
            new PublishJob { Status = JobStatus.Published },
            new PublishJob { Status = JobStatus.Processed },
            new PublishJob { Status = JobStatus.Failed },
            new PublishJob { Status = JobStatus.Extracted }
        };

        var stats = PublishDashboardService.ComputeStats(jobs);

        Assert.Equal(4, stats.Total);
        Assert.Equal(1, stats.Published);
        Assert.Equal(1, stats.PendingPublish);
        Assert.Equal(1, stats.Processed);
        Assert.Equal(1, stats.Failed);
        Assert.Equal(1, stats.Active);
        Assert.Contains("共 4", stats.SummaryText);
    }

    [Fact]
    public void Filter_pending_publish_returns_processed_only()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "A", Status = JobStatus.Processed },
            new PublishJob { Title = "B", Status = JobStatus.Published },
            new PublishJob { Title = "C", Status = JobStatus.Extracted }
        };

        var filtered = PublishDashboardService.Filter(jobs, JobListFilter.PendingPublish);

        Assert.Single(filtered);
        Assert.Equal("A", filtered[0].Title);
    }

    [Fact]
    public void BuildHistory_includes_published_jobs()
    {
        var jobs = new[]
        {
            new PublishJob
            {
                Id = "1",
                Title = "已发布",
                Status = JobStatus.Published,
                Publish = new JobPublishState
                {
                    Baidu = new PublishChannelState { Status = PublishStatusHelper.Published }
                }
            },
            new PublishJob
            {
                Id = "2",
                Title = "草稿",
                Status = JobStatus.Draft
            }
        };

        var history = PublishDashboardService.BuildHistory(jobs);

        Assert.Single(history);
        Assert.Equal("已发布", history[0].Title);
    }

    [Fact]
    public void ToCsv_escapes_commas()
    {
        var csv = PublishDashboardService.ToCsv(
        [
            new PublishHistoryEntry
            {
                JobId = "abc",
                Title = "游戏,测试",
                Status = "Published"
            }
        ]);

        Assert.Contains("\"游戏,测试\"", csv);
        Assert.Contains("jobId,title", csv);
    }

    [Fact]
    public void GetRecentActivity_returns_latest_jobs()
    {
        var jobs = new[]
        {
            new PublishJob { Id = "1", Title = "旧", UpdatedAt = new DateTime(2026, 1, 1), Log = ["old"] },
            new PublishJob { Id = "2", Title = "新", UpdatedAt = new DateTime(2026, 2, 1), Log = ["new line"] }
        };

        var activity = PublishDashboardService.GetRecentActivity(jobs, limit: 1);

        Assert.Single(activity);
        Assert.Equal("2", activity[0].JobId);
        Assert.Equal("new line", activity[0].LastLogLine);
    }

    [Fact]
    public void ParseFilter_accepts_aliases()
    {
        Assert.Equal(JobListFilter.PendingPublish, PublishDashboardService.ParseFilter("pending"));
        Assert.Equal(JobListFilter.All, PublishDashboardService.ParseFilter("all"));
        Assert.Equal(JobListFilter.Published, PublishDashboardService.ParseFilter("已发布"));
    }
}