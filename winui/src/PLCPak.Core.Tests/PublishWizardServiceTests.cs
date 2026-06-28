using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class PublishWizardServiceTests
{
    [Fact]
    public void BuildState_for_processed_job_missing_links_marks_links_active()
    {
        var job = new PublishJob
        {
            Id = "job-processed-links",
            Title = "缺链接任务",
            Status = JobStatus.Processed,
            Artifacts =
            {
                InboxArchives = [@"C:\inbox\game.7z"],
                OutputArchives = [@"C:\output\game.7z"]
            }
        };

        var state = PublishWizardService.BuildState(job);

        Assert.Equal(job.Id, state.JobId);
        Assert.Equal(JobNextActionType.FillLinks, state.CurrentAction);
        Assert.Equal("links", state.Steps.First(step => step.Status == WizardStepStatus.Active).Key);
        Assert.Equal(WizardStepStatus.Done, state.Steps.First(step => step.Key == "inbox").Status);
        Assert.Equal(WizardStepStatus.Done, state.Steps.First(step => step.Key == "pipeline").Status);
        Assert.Equal(WizardStepStatus.Pending, state.Steps.First(step => step.Key == "copy").Status);
        Assert.Contains("回填链接", state.SummaryText);
    }

    [Fact]
    public void BuildState_for_null_job_returns_empty_state()
    {
        var state = PublishWizardService.BuildState(null);

        Assert.Null(state.JobId);
        Assert.Equal(6, state.Steps.Count);
        Assert.All(state.Steps, step => Assert.Equal(WizardStepStatus.Pending, step.Status));
        Assert.Contains("请选择或创建任务", state.SummaryText);
    }
}