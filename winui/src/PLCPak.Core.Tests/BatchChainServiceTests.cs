using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchChainServiceTests
{
    [Fact]
    public void ParseBatchChainFilter_maps_active_failed_all()
    {
        Assert.Equal(JobListFilter.Active, JobHealthService.ParseBatchChainFilter("active"));
        Assert.Equal(JobListFilter.Failed, JobHealthService.ParseBatchChainFilter("failed"));
        Assert.Equal(JobListFilter.All, JobHealthService.ParseBatchChainFilter("all"));
        Assert.Equal(JobListFilter.Active, JobHealthService.ParseBatchChainFilter(null));
    }

    [Fact]
    public void FilterForBatchChain_active_includes_failed_and_processed_with_automatable_actions()
    {
        var jobs = new[]
        {
            new PublishJob { Id = "active", Title = "进行中", Status = JobStatus.InboxReady },
            new PublishJob { Id = "failed", Title = "失败", Status = JobStatus.Failed },
            new PublishJob
            {
                Id = "copy",
                Title = "生成文案",
                Status = JobStatus.Processed,
                Publish = new JobPublishState
                {
                    Baidu = new PublishChannelState { Link = "https://baidu.test" },
                    Quark = new PublishChannelState { Link = "https://quark.test" },
                    Telegram = new PublishChannelState { Link = "https://t.me/test" }
                }
            },
            new PublishJob
            {
                Id = "fill",
                Title = "待回填",
                Status = JobStatus.Processed,
                Publish = new JobPublishState()
            },
            new PublishJob { Id = "archived", Title = "归档", Status = JobStatus.Archived }
        };

        var filtered = JobHealthService.FilterForBatchChain(jobs, JobListFilter.Active);

        Assert.Equal(3, filtered.Count);
        Assert.Contains(filtered, j => j.Id == "active");
        Assert.Contains(filtered, j => j.Id == "failed");
        Assert.Contains(filtered, j => j.Id == "copy");
        Assert.DoesNotContain(filtered, j => j.Id == "fill");
        Assert.DoesNotContain(filtered, j => j.Id == "archived");
    }

    [Fact]
    public void FilterForBatchChain_failed_only_returns_failed_jobs()
    {
        var jobs = new[]
        {
            new PublishJob { Id = "failed", Title = "失败", Status = JobStatus.Failed },
            new PublishJob { Id = "active", Title = "进行中", Status = JobStatus.InboxReady }
        };

        var filtered = JobHealthService.FilterForBatchChain(jobs, JobListFilter.Failed);

        Assert.Single(filtered);
        Assert.Equal("failed", filtered[0].Id);
    }

    [Fact]
    public void HasAutomatableFirstAction_excludes_manual_only_steps()
    {
        var fillLinks = new PublishJob
        {
            Id = "fill",
            Status = JobStatus.Processed,
            Publish = new JobPublishState()
        };
        var generateCopy = new PublishJob
        {
            Id = "copy",
            Status = JobStatus.Processed,
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState { Link = "https://baidu.test" },
                Quark = new PublishChannelState { Link = "https://quark.test" },
                Telegram = new PublishChannelState { Link = "https://t.me/test" }
            }
        };

        Assert.False(WorkflowChainService.HasAutomatableFirstAction(fillLinks));
        Assert.True(WorkflowChainService.HasAutomatableFirstAction(generateCopy));
    }
}