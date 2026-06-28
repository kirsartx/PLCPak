using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class WorkflowGuideServiceTests
{
    [Theory]
    [InlineData(JobNextActionType.RunPipeline, "jobs")]
    [InlineData(JobNextActionType.FillLinks, "jobs")]
    [InlineData(JobNextActionType.SendTelegram, "dashboard")]
    [InlineData(JobNextActionType.None, "dashboard")]
    public void ResolveRecommendedPage_maps_action_to_page(JobNextActionType action, string expected)
    {
        Assert.Equal(expected, WorkflowGuideService.ResolveRecommendedPage(action));
    }

    [Fact]
    public void Build_surfaces_primary_action_and_badges()
    {
        var jobs = new List<PublishJob>
        {
            new()
            {
                Id = "job-1",
                Title = "测试游戏",
                Status = JobStatus.Processed,
                UpdatedAt = DateTime.UtcNow,
                Publish = new JobPublishState
                {
                    Baidu = new PublishChannelState { Link = "https://pan.baidu.com/s/1" },
                    Quark = new PublishChannelState { Link = "https://pan.quark.cn/s/1" },
                    Telegram = new PublishChannelState { Link = "https://t.me/test/1" }
                }
            },
            new()
            {
                Id = "job-2",
                Title = "失败任务",
                Status = JobStatus.Failed,
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                Error = "解压失败"
            }
        };

        var guide = WorkflowGuideService.Build(jobs);

        Assert.True(guide.HasAction);
        Assert.Equal("job-2", guide.JobId);
        Assert.Equal("重试流水线", guide.ActionLabel);
        Assert.Equal("jobs", guide.RecommendedPage);
        Assert.True(guide.JobsBadgeCount >= 1);
    }
}