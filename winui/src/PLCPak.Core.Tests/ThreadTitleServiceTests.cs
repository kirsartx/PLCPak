using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ThreadTitleServiceTests
{
    [Fact]
    public void ExtractTitle_prefers_og_title()
    {
        const string html = """
            <html><head>
            <meta property="og:title" content="测试游戏 0.31.1" />
            <title>页面标题</title>
            </head></html>
            """;

        var (title, source) = ThreadTitleService.ExtractTitle(html);

        Assert.Equal("测试游戏 0.31.1", title);
        Assert.Equal("og:title", source);
    }

    [Fact]
    public void CleanTitle_removes_forum_suffix()
    {
        var cleaned = ThreadTitleService.CleanTitle("妹妹使我强大0.31.1 - 老王论坛");
        Assert.Equal("妹妹使我强大0.31.1", cleaned);
    }

    [Fact]
    public void ExtractTitle_falls_back_to_title_tag()
    {
        const string html = "<html><head><title>Fallback Title</title></head></html>";
        var (title, source) = ThreadTitleService.ExtractTitle(html);
        Assert.Equal("Fallback Title", title);
        Assert.Equal("title", source);
    }
}