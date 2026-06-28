using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class PanLinkParseServiceTests
{
    [Fact]
    public void ParseShareText_extracts_baidu_quark_and_telegram_from_chinese_block()
    {
        const string text = """
            游戏名称：测试游戏

            百度网盘链接：https://pan.baidu.com/s/1a2b3c4d5e
            提取码：abcd

            夸克网盘链接：https://pan.quark.cn/s/xyz789
            密码：ef12gh

            TG频道：https://t.me/testchannel/123
            """;

        var result = PanLinkParseService.ParseShareText(text);

        Assert.True(result.Success);
        Assert.Equal("https://pan.baidu.com/s/1a2b3c4d5e", result.BaiduLink);
        Assert.Equal("abcd", result.BaiduPassword);
        Assert.Equal("https://pan.quark.cn/s/xyz789", result.QuarkLink);
        Assert.Equal("ef12gh", result.QuarkPassword);
        Assert.Single(result.TelegramLinks);
        Assert.Equal("https://t.me/testchannel/123", result.TelegramLinks[0]);
        Assert.Contains(result.Messages, m => m.Contains("百度链接"));
        Assert.Contains(result.Messages, m => m.Contains("夸克链接"));
    }

    [Fact]
    public void ParseShareText_handles_labeled_link_and_extract_code_patterns()
    {
        const string text = """
            链接：https://pan.baidu.com/s/sample99
            提取码：wxyz
            """;

        var result = PanLinkParseService.ParseShareText(text);

        Assert.True(result.Success);
        Assert.Equal("https://pan.baidu.com/s/sample99", result.BaiduLink);
        Assert.Equal("wxyz", result.BaiduPassword);
    }

    [Fact]
    public void ParseShareText_returns_failure_for_empty_text()
    {
        var result = PanLinkParseService.ParseShareText("   ");

        Assert.False(result.Success);
        Assert.Contains("分享文本为空", result.Messages[0]);
    }
}