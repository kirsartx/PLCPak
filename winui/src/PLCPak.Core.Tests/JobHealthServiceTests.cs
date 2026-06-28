using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class JobHealthServiceTests
{
    [Fact]
    public void Compute_reports_failed_jobs_as_errors()
    {
        var jobs = new[]
        {
            new PublishJob { Id = "1", Title = "失败任务", Status = JobStatus.Failed, Error = "解压失败" }
        };

        var report = JobHealthService.Compute(jobs);

        Assert.Equal(1, report.ErrorCount);
        Assert.Single(report.Issues);
        Assert.Equal("failed", report.Issues[0].Code);
    }

    [Fact]
    public void Compute_warns_when_processed_jobs_missing_links()
    {
        var jobs = new[]
        {
            new PublishJob { Id = "1", Title = "待回填", Status = JobStatus.Processed }
        };

        var report = JobHealthService.Compute(jobs);

        Assert.Equal(2, report.WarningCount);
        Assert.Contains(report.Issues, i => i.Code == "missing-baidu-link");
        Assert.Contains(report.Issues, i => i.Code == "missing-quark-link");
    }

    [Fact]
    public void FilterForBatchPipeline_pending_includes_active_pipeline_jobs()
    {
        var jobs = new[]
        {
            new PublishJob { Title = "A", Status = JobStatus.InboxReady },
            new PublishJob { Title = "B", Status = JobStatus.Processed },
            new PublishJob { Title = "C", Status = JobStatus.Archived }
        };

        var filtered = JobHealthService.FilterForBatchPipeline(jobs, JobListFilter.Active);

        Assert.Single(filtered);
        Assert.Equal("A", filtered[0].Title);
    }

    [Fact]
    public void ParseBatchPipelineFilter_maps_pending_to_active()
    {
        Assert.Equal(JobListFilter.Active, JobHealthService.ParseBatchPipelineFilter("pending"));
        Assert.Equal(JobListFilter.Failed, JobHealthService.ParseBatchPipelineFilter("failed"));
    }

    [Fact]
    public void InspectJob_warns_when_processed_job_is_stale_without_full_links()
    {
        var job = new PublishJob
        {
            Id = "stale",
            Title = "长期待发布",
            Status = JobStatus.Processed,
            UpdatedAt = DateTime.Now.AddDays(-10)
        };

        var issues = JobHealthService.InspectJob(job);

        Assert.Contains(issues, i => i.Code == "stale-pending-publish" && i.Message == "长期待发布");
    }
}