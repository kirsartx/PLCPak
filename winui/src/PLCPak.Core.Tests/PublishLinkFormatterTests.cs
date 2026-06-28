using PLCPak.Core.Models;

namespace PLCPak.Core.Tests;

public sealed class PublishLinkFormatterTests
{
    [Fact]
    public void Build_formats_all_publish_links()
    {
        var job = new PublishJob
        {
            Id = "abc",
            Title = "测试游戏",
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState { Link = "https://pan.baidu.com/a", Password = "1234" },
                Quark = new PublishChannelState { Link = "https://pan.quark.cn/b", Password = "abcd" },
                Telegram = new PublishChannelState { Link = "https://t.me/example/1" }
            }
        };

        var snapshot = PublishLinkFormatter.Build(job);

        Assert.Contains("百度: https://pan.baidu.com/a", snapshot.FormattedText);
        Assert.Contains("百度提取码: 1234", snapshot.FormattedText);
        Assert.Contains("夸克: https://pan.quark.cn/b", snapshot.FormattedText);
        Assert.Contains("TG: https://t.me/example/1", snapshot.FormattedText);
    }

    [Fact]
    public void Build_returns_placeholder_when_links_missing()
    {
        var snapshot = PublishLinkFormatter.Build(new PublishJob { Title = "空链接" });
        Assert.Equal("(尚未填写发布链接)", snapshot.FormattedText);
    }
}