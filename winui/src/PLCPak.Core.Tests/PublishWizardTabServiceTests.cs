using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class PublishWizardTabServiceTests
{
    [Fact]
    public void BuildForJob_processed_without_links_targets_links_tab()
    {
        var job = CreateJob("待填链接", JobStatus.Processed);

        var state = PublishWizardTabService.BuildForJob(job);

        Assert.Equal(1, state.CurrentTabIndex);
        Assert.Equal("links", state.CurrentTabId);
        Assert.True(state.Tabs[0].IsComplete);
        Assert.False(state.Tabs[1].IsComplete);
    }

    [Fact]
    public void BuildForJob_wizard_tabs_match_completion_flags()
    {
        var job = CreateJob("向导步骤", JobStatus.Processed);

        var state = PublishWizardTabService.BuildForJob(job);

        Assert.Equal(4, state.Tabs.Count);
        Assert.True(state.Tabs[0].IsComplete);
        Assert.True(state.Tabs[0].IsCurrent == false || state.CurrentTabIndex == 0);
        Assert.False(state.Tabs[1].IsComplete);
        Assert.Equal("prepare", state.Tabs[0].Id);
        Assert.Equal("links", state.Tabs[1].Id);
        Assert.Equal("copy", state.Tabs[2].Id);
        Assert.Equal("publish", state.Tabs[3].Id);
    }

    [Fact]
    public void BuildForJobs_excludes_archived_jobs()
    {
        var active = CreateJob("进行中", JobStatus.Processed);
        var archived = CreateJob("已归档", JobStatus.Archived);

        var list = PublishWizardTabService.BuildForJobs([active, archived]);

        Assert.Single(list.Jobs);
        Assert.Equal(active.Id, list.Jobs[0].JobId);
    }

    private static PublishJob CreateJob(string title, JobStatus status)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            Status = status,
            UpdatedAt = new DateTime(2026, 6, 25, 12, 0, 0)
        };
}