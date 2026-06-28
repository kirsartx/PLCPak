using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class PublishTemplateServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly PublishTemplateService _templates;

    public PublishTemplateServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "plcpak-publish-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _templates = new PublishTemplateService(new AppPaths(_dir));
        _templates.Save(new PublishTemplateCatalog
        {
            DefaultTemplateId = "telegram-default",
            Templates =
            [
                new PublishTemplate
                {
                    Id = "telegram-default",
                    Name = "TG",
                    Body = "{title}\n{baidu_line}\n{quark_line}\n{telegram_channel}"
                }
            ]
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Render_replaces_links_and_title()
    {
        var job = new PublishJob
        {
            Title = "测试游戏",
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState { Link = "https://pan.baidu.com/abc", Password = "1234" },
                Quark = new PublishChannelState { Link = "https://pan.quark.cn/xyz", Password = "abcd" }
            }
        };

        var result = _templates.Render(job, new StudioConfig());

        Assert.Contains("测试游戏", result.Text);
        Assert.Contains("https://pan.baidu.com/abc", result.Text);
        Assert.Contains("1234", result.Text);
        Assert.Contains("https://pan.quark.cn/xyz", result.Text);
    }

    [Fact]
    public void Render_includes_telegram_channel_variable()
    {
        var job = new PublishJob { Title = "测试游戏" };
        var studio = new StudioConfig { TelegramChannelUrl = "https://t.me/demo_channel" };

        var result = _templates.Render(job, studio);

        Assert.Contains("https://t.me/demo_channel", result.Text);
    }

    [Fact]
    public void PreviewTemplate_uses_sample_placeholders_when_job_is_null()
    {
        var studio = new StudioConfig { TelegramChannelUrl = "https://t.me/demo_channel" };

        var result = _templates.PreviewTemplate("telegram-default", null, studio);

        Assert.Contains("示例游戏标题", result.Text);
        Assert.Contains("https://pan.baidu.com/s/example", result.Text);
        Assert.Contains("https://pan.quark.cn/s/example", result.Text);
        Assert.Contains("https://t.me/demo_channel", result.Text);
        Assert.Empty(result.MissingFields);
    }

    [Fact]
    public void PreviewTemplate_uses_provided_job_when_present()
    {
        var job = new PublishJob
        {
            Title = "真实任务",
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState { Link = "https://pan.baidu.com/real", Password = "9999" },
                Quark = new PublishChannelState { Link = "https://pan.quark.cn/real", Password = "8888" }
            }
        };

        var result = _templates.PreviewTemplate("telegram-default", job, new StudioConfig());

        Assert.Contains("真实任务", result.Text);
        Assert.Contains("https://pan.baidu.com/real", result.Text);
        Assert.DoesNotContain("示例游戏标题", result.Text);
    }
}