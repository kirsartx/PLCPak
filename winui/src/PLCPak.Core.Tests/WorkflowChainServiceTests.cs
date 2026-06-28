using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class WorkflowChainServiceTests
{
    [Fact]
    public async Task RunAutomatableChain_stops_at_fill_links_without_executing()
    {
        var job = new PublishJob
        {
            Id = "job-fill",
            Title = "待回填",
            Status = JobStatus.Processed
        };

        var executed = new List<JobNextActionType>();
        var result = await WorkflowChainService.RunAutomatableChainAsync(
            job,
            action =>
            {
                executed.Add(action);
                return Task.FromResult(new JobNextActionResult { Success = true, Action = action, Job = job });
            },
            _ => job);

        Assert.True(result.NeedsUserInput);
        Assert.Equal("需要手动回填链接", result.Message);
        Assert.Equal("FillLinks", result.StopReason);
        Assert.Single(result.Steps);
        Assert.True(result.Steps[0].Stopped);
        Assert.Equal(JobNextActionType.FillLinks, result.Steps[0].Action);
        Assert.Empty(executed);
    }

    [Fact]
    public async Task RunAutomatableChain_stops_at_mark_published()
    {
        var job = new PublishJob
        {
            Id = "job-mark",
            Title = "待标记",
            Status = JobStatus.Processed,
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState { Link = "https://baidu.test", Status = PublishStatusHelper.Ready },
                Quark = new PublishChannelState { Link = "https://quark.test", Status = PublishStatusHelper.Ready },
                Telegram = new PublishChannelState { Link = "https://t.me/test", Status = PublishStatusHelper.Ready },
                GeneratedCopy = "copy text"
            }
        };

        var executed = new List<JobNextActionType>();
        var result = await WorkflowChainService.RunAutomatableChainAsync(
            job,
            action =>
            {
                executed.Add(action);
                return Task.FromResult(new JobNextActionResult { Success = true, Action = action, Job = job });
            },
            _ => job);

        Assert.True(result.NeedsUserInput);
        Assert.Equal("需要手动确认发布状态", result.Message);
        Assert.Equal("MarkPublished", result.StopReason);
        Assert.Empty(executed);
    }

    [Fact]
    public async Task RunAutomatableChain_stops_at_send_telegram()
    {
        var job = new PublishJob
        {
            Id = "job-tg",
            Title = "待发送",
            Status = JobStatus.Published,
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState { Link = "https://baidu.test", Status = PublishStatusHelper.Published },
                Quark = new PublishChannelState { Link = "https://quark.test", Status = PublishStatusHelper.Published },
                Telegram = new PublishChannelState { Link = "https://t.me/test", Status = PublishStatusHelper.Pending },
                GeneratedCopy = "copy text"
            }
        };

        var executed = new List<JobNextActionType>();
        var result = await WorkflowChainService.RunAutomatableChainAsync(
            job,
            action =>
            {
                executed.Add(action);
                return Task.FromResult(new JobNextActionResult { Success = true, Action = action, Job = job });
            },
            _ => job);

        Assert.True(result.NeedsUserInput);
        Assert.Equal("需要手动发送 Telegram", result.Message);
        Assert.Equal("SendTelegram", result.StopReason);
        Assert.Empty(executed);
    }

    [Fact]
    public async Task RunAutomatableChain_executes_generate_copy_and_stops_when_complete()
    {
        var job = new PublishJob
        {
            Id = "job-copy",
            Title = "生成文案",
            Status = JobStatus.Processed,
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState { Link = "https://baidu.test", Status = PublishStatusHelper.Ready },
                Quark = new PublishChannelState { Link = "https://quark.test", Status = PublishStatusHelper.Ready },
                Telegram = new PublishChannelState { Link = "https://t.me/test", Status = PublishStatusHelper.Ready }
            }
        };

        var executed = new List<JobNextActionType>();
        var result = await WorkflowChainService.RunAutomatableChainAsync(
            job,
            action =>
            {
                executed.Add(action);
                job.Publish.GeneratedCopy = "generated";
                return Task.FromResult(new JobNextActionResult
                {
                    Success = true,
                    Action = action,
                    Message = "已生成发布文案",
                    Job = job
                });
            },
            _ => job);

        Assert.Single(executed);
        Assert.Equal(JobNextActionType.GenerateCopy, executed[0]);
        Assert.True(result.NeedsUserInput);
        Assert.Equal("需要手动确认发布状态", result.Message);
        Assert.Equal(2, result.Steps.Count);
        Assert.False(result.Steps[0].Stopped);
        Assert.True(result.Steps[1].Stopped);
    }

    [Fact]
    public async Task RunAutomatableChain_stops_on_action_failure()
    {
        var job = new PublishJob
        {
            Id = "job-fail",
            Title = "失败任务",
            Status = JobStatus.Failed
        };

        var result = await WorkflowChainService.RunAutomatableChainAsync(
            job,
            _ => Task.FromResult(new JobNextActionResult
            {
                Success = false,
                Action = JobNextActionType.RetryFailed,
                Message = "重试失败",
                Job = job
            }),
            _ => job);

        Assert.False(result.Success);
        Assert.Equal("ActionFailed", result.StopReason);
        Assert.Equal("重试失败", result.Message);
        Assert.Single(result.Steps);
        Assert.False(result.Steps[0].Success);
    }

    [Fact]
    public async Task RunAutomatableChain_stops_when_no_next_action()
    {
        var job = new PublishJob
        {
            Id = "job-done",
            Title = "已完成",
            Status = JobStatus.Archived,
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState { Status = PublishStatusHelper.Published },
                Quark = new PublishChannelState { Status = PublishStatusHelper.Published },
                Telegram = new PublishChannelState { Status = PublishStatusHelper.Published },
                GeneratedCopy = "copy"
            }
        };

        var result = await WorkflowChainService.RunAutomatableChainAsync(
            job,
            _ => throw new InvalidOperationException("不应执行"),
            _ => job);

        Assert.True(result.NeedsUserInput);
        Assert.Equal("需要手动回填链接", result.Message);
        Assert.Equal("None", result.StopReason);
    }
}