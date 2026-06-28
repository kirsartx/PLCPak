using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ThreadParseServiceTests
{
    [Fact]
    public void ParseHtml_extracts_baidu_quark_links_and_passwords()
    {
        const string html = """
            <html><head>
            <meta property="og:title" content="测试游戏 1.0" />
            </head><body>
            <p>百度网盘：https://pan.baidu.com/s/abc123 提取码：abcd</p>
            <p>夸克网盘：<a href="https://pan.quark.cn/s/xyz789">下载</a> 密码：ef12</p>
            <p>解压密码：secret99</p>
            </body></html>
            """;

        var result = ThreadParseService.ParseHtml(html);

        Assert.True(result.Success);
        Assert.Equal("测试游戏 1.0", result.Title);
        Assert.Equal("og:title", result.TitleSource);
        Assert.Equal("https://pan.baidu.com/s/abc123", result.BaiduLink);
        Assert.Equal("abcd", result.BaiduPassword);
        Assert.Equal("https://pan.quark.cn/s/xyz789", result.QuarkLink);
        Assert.Equal("ef12", result.QuarkPassword);
        Assert.Equal("secret99", result.ArchivePassword);
        Assert.Equal(2, result.AllLinks.Count);
    }

    [Fact]
    public void ParseHtml_decodes_entities_and_finds_archive_password()
    {
        const string html = """
            <html><head><title>实体测试</title></head>
            <body>解压密码：&amp;pass123</body></html>
            """;

        var result = ThreadParseService.ParseHtml(html);

        Assert.True(result.Success);
        Assert.Equal("实体测试", result.Title);
        Assert.Equal("&pass123", result.ArchivePassword);
    }

    [Fact]
    public void ParseHtml_returns_error_when_no_useful_content()
    {
        const string html = "<html><body>无下载信息</body></html>";

        var result = ThreadParseService.ParseHtml(html);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ParseHtml_extracts_direct_attachment_links_and_resolves_relative_urls()
    {
        const string html = """
            <html><head><title>附件测试</title></head><body>
            <a href="attachments/game.7z">下载</a>
            <a href="https://forum.example.com/attachment.php?aid=99">附件</a>
            <a href="https://pan.baidu.com/s/abc">百度</a>
            <img src="/files/patch.zip" />
            </body></html>
            """;

        var result = ThreadParseService.ParseHtml(html, "https://forum.example.com/thread/42/");

        Assert.True(result.Success);
        Assert.Equal(3, result.AttachmentLinks.Count);
        Assert.Contains("https://forum.example.com/thread/42/attachments/game.7z", result.AttachmentLinks);
        Assert.Contains("https://forum.example.com/attachment.php?aid=99", result.AttachmentLinks);
        Assert.Contains("https://forum.example.com/files/patch.zip", result.AttachmentLinks);
        Assert.DoesNotContain(result.AttachmentLinks, l => l.Contains("pan.baidu.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParseHtml_excludes_quark_links_from_attachment_links()
    {
        const string html = """
            <html><body>
            <a href="https://pan.quark.cn/s/xyz">夸克</a>
            <a href="https://forum.example.com/data/game.rar">直链</a>
            </body></html>
            """;

        var result = ThreadParseService.ParseHtml(html);

        Assert.Single(result.AttachmentLinks);
        Assert.Equal("https://forum.example.com/data/game.rar", result.AttachmentLinks[0]);
        Assert.Single(result.AllLinks);
    }
}