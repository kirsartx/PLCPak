using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchChainTests
{
    [Fact]
    public async Task RunBatchAutomatableChain_two_jobs_one_stops_at_filllinks_one_skipped()
    {
        var jobPipeline = new PublishJob
        {
            Id = "job-pipeline",
            Title = "流水线任务",
            Status = JobStatus.InboxReady
        };
        var jobManual = new PublishJob
        {
            Id = "job-manual",
            Title = "待回填",
            Status = JobStatus.Processed
        };

        var states = new Dictionary<string, PublishJob>
        {
            [jobPipeline.Id] = jobPipeline,
            [jobManual.Id] = jobManual
        };

        PublishJob Reload(string id) => states[id];

        var result = await WorkflowChainService.RunBatchAutomatableChainAsync(
            [jobPipeline, jobManual],
            job => WorkflowChainService.RunAutomatableChainAsync(
                job,
                action =>
                {
                    if (action == JobNextActionType.RunPipeline)
                    {
                        states[job.Id].Status = JobStatus.Processed;
                        return Task.FromResult(new JobNextActionResult
                        {
                            Success = true,
                            Action = action,
                            Message = "流水线完成",
                            Job = states[job.Id]
                        });
                    }

                    return Task.FromResult(new JobNextActionResult
                    {
                        Success = true,
                        Action = action,
                        Job = states[job.Id]
                    });
                },
                Reload),
            CancellationToken.None);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(1, result.StoppedForManual);
        Assert.Equal(0, result.Failed);
        Assert.Equal(0, result.Success);
        Assert.Single(result.JobResults);
        Assert.Equal("FillLinks", result.JobResults[0].StopReason);
        Assert.Contains(result.Messages, m => m.Contains("待回填") && m.Contains("[跳过]"));
        Assert.Contains(result.Messages, m => m.Contains("流水线任务") && m.Contains("[待手动]"));
    }
}