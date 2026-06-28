using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class TgPendingServiceTests
{
    [Fact]
    public void GetPending_returns_jobs_with_copy_and_ready_pan_links_not_sent_to_telegram()
    {
        var jobs = new[]
        {
            new PublishJob
            {
                Id = "pending-1",
                Title = "待发送",
                Publish = new JobPublishState
                {
                    GeneratedCopy = "发布文案",
                    Baidu = new PublishChannelState { Link = "https://baidu.test", Status = PublishStatusHelper.Published },
                    Quark = new PublishChannelState { Link = "https://quark.test", Status = PublishStatusHelper.Published },
                    Telegram = new PublishChannelState { Status = PublishStatusHelper.Ready }
                }
            },
            new PublishJob
            {
                Id = "done-1",
                Title = "已发送",
                Publish = new JobPublishState
                {
                    GeneratedCopy = "发布文案",
                    Baidu = new PublishChannelState { Status = PublishStatusHelper.Published },
                    Quark = new PublishChannelState { Status = PublishStatusHelper.Published },
                    Telegram = new PublishChannelState { Status = PublishStatusHelper.Published }
                }
            },
            new PublishJob
            {
                Id = "no-copy",
                Title = "无文案",
                Publish = new JobPublishState
                {
                    Baidu = new PublishChannelState { Link = "https://baidu.test", Status = PublishStatusHelper.Published },
                    Quark = new PublishChannelState { Link = "https://quark.test", Status = PublishStatusHelper.Published }
                }
            }
        };

        var snapshot = TgPendingService.GetPending(jobs);

        Assert.Equal(1, snapshot.Count);
        Assert.Single(snapshot.List);
        Assert.Equal("pending-1", snapshot.List[0].JobId);
        Assert.True(snapshot.List[0].HasCopy);
        Assert.True(snapshot.List[0].BaiduPublished);
        Assert.True(snapshot.List[0].QuarkPublished);
        Assert.Contains("TG", snapshot.List[0].Reason);
        Assert.Contains("待发送 1", snapshot.SummaryText);
    }

    [Fact]
    public void GetPending_accepts_filled_links_when_channels_not_marked_published()
    {
        var jobs = new[]
        {
            new PublishJob
            {
                Id = "links-only",
                Title = "仅填链接",
                Publish = new JobPublishState
                {
                    GeneratedCopy = "文案",
                    Baidu = new PublishChannelState { Link = "https://baidu.test", Status = PublishStatusHelper.Ready },
                    Quark = new PublishChannelState { Link = "https://quark.test", Status = PublishStatusHelper.Ready },
                    Telegram = new PublishChannelState { Status = PublishStatusHelper.Pending }
                }
            }
        };

        var snapshot = TgPendingService.GetPending(jobs);

        Assert.Single(snapshot.List);
        Assert.False(snapshot.List[0].BaiduPublished);
        Assert.False(snapshot.List[0].QuarkPublished);
        Assert.True(snapshot.List[0].HasCopy);
    }

    [Fact]
    public void GetPending_respects_limit_and_sorts_by_updated_at()
    {
        var older = CreateJob("旧任务", new DateTime(2026, 6, 24));
        var newer = CreateJob("新任务", new DateTime(2026, 6, 25));

        var snapshot = TgPendingService.GetPending([older, newer], limit: 1);

        Assert.Equal(1, snapshot.Count);
        Assert.Equal("新任务", snapshot.List[0].Title);
    }

    private static PublishJob CreateJob(string title, DateTime updatedAt)
    {
        return new PublishJob
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            UpdatedAt = updatedAt,
            Publish = new JobPublishState
            {
                GeneratedCopy = "文案",
                Baidu = new PublishChannelState { Link = "https://baidu.test", Status = PublishStatusHelper.Published },
                Quark = new PublishChannelState { Link = "https://quark.test", Status = PublishStatusHelper.Published },
                Telegram = new PublishChannelState { Status = PublishStatusHelper.Ready }
            }
        };
    }
}