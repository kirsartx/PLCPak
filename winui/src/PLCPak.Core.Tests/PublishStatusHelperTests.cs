using PLCPak.Core.Models;

namespace PLCPak.Core.Tests;

public sealed class PublishStatusHelperTests
{
    [Fact]
    public void ApplyLinkStatus_sets_pending_when_empty()
    {
        var channel = new PublishChannelState { Link = string.Empty };
        PublishStatusHelper.ApplyLinkStatus(channel);
        Assert.Equal(PublishStatusHelper.Pending, channel.Status);
    }

    [Fact]
    public void ApplyLinkStatus_sets_ready_when_link_filled()
    {
        var channel = new PublishChannelState { Link = "https://pan.baidu.com/abc" };
        PublishStatusHelper.ApplyLinkStatus(channel);
        Assert.Equal(PublishStatusHelper.Ready, channel.Status);
    }

    [Fact]
    public void ApplyLinkStatus_preserves_published()
    {
        var channel = new PublishChannelState
        {
            Status = PublishStatusHelper.Published,
            Link = string.Empty
        };
        PublishStatusHelper.ApplyLinkStatus(channel);
        Assert.Equal(PublishStatusHelper.Published, channel.Status);
    }

    [Fact]
    public void MarkPublished_requires_link_by_default()
    {
        var channel = new PublishChannelState();
        Assert.Throws<InvalidOperationException>(() => PublishStatusHelper.MarkPublished(channel));
    }

    [Fact]
    public void MarkPublished_sets_timestamp()
    {
        var channel = new PublishChannelState { Link = "https://pan.baidu.com/abc" };
        PublishStatusHelper.MarkPublished(channel);
        Assert.Equal(PublishStatusHelper.Published, channel.Status);
        Assert.NotNull(channel.PublishedAt);
    }

    [Fact]
    public void MarkPublished_allows_tg_without_link()
    {
        var channel = new PublishChannelState();
        PublishStatusHelper.MarkPublished(channel, requireLink: false);
        Assert.Equal(PublishStatusHelper.Published, channel.Status);
    }

    [Fact]
    public void BuildSummary_describes_all_channels()
    {
        var publish = new JobPublishState
        {
            Baidu = new PublishChannelState { Status = PublishStatusHelper.Ready, Link = "https://baidu" },
            Quark = new PublishChannelState { Status = PublishStatusHelper.Pending },
            Telegram = new PublishChannelState { Status = PublishStatusHelper.Published }
        };

        var summary = PublishStatusHelper.BuildSummary(publish);
        Assert.Contains("百度待发布", summary);
        Assert.Contains("夸克未填", summary);
        Assert.Contains("TG已发布", summary);
    }

    [Fact]
    public void IsFullyPublished_requires_all_three()
    {
        var publish = new JobPublishState
        {
            Baidu = new PublishChannelState { Status = PublishStatusHelper.Published },
            Quark = new PublishChannelState { Status = PublishStatusHelper.Published },
            Telegram = new PublishChannelState { Status = PublishStatusHelper.Ready }
        };

        Assert.False(PublishStatusHelper.IsFullyPublished(publish));

        publish.Telegram.Status = PublishStatusHelper.Published;
        Assert.True(PublishStatusHelper.IsFullyPublished(publish));
    }
}