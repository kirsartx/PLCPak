using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class ForumDownloadServiceTests
{
    [Theory]
    [InlineData("https://forum.example.com/attachments/game.7z", true)]
    [InlineData("https://forum.example.com/attachment.php?aid=123", true)]
    [InlineData("https://forum.example.com/index.php?mod=attachment&id=1", true)]
    [InlineData("https://forum.example.com/files/archive.ZIP", true)]
    [InlineData("https://forum.example.com/files/archive.tar.zst", true)]
    [InlineData("https://pan.baidu.com/s/abc", false)]
    [InlineData("https://pan.quark.cn/s/xyz", false)]
    [InlineData("https://forum.example.com/page.html", false)]
    public void IsDirectAttachmentUrl_detects_archive_and_attachment_patterns(string url, bool expected)
    {
        Assert.Equal(expected, ForumDownloadService.IsDirectAttachmentUrl(url));
    }

    [Fact]
    public void ResolveRelativeUrl_resolves_against_base()
    {
        var resolved = ForumDownloadService.ResolveRelativeUrl(
            "https://forum.example.com/thread/123/",
            "attachments/game.7z");

        Assert.Equal("https://forum.example.com/thread/123/attachments/game.7z", resolved);
    }

    [Fact]
    public void ResolveRelativeUrl_returns_absolute_unchanged()
    {
        const string url = "https://forum.example.com/files/game.rar";
        Assert.Equal(url, ForumDownloadService.ResolveRelativeUrl("https://forum.example.com/", url));
    }
}