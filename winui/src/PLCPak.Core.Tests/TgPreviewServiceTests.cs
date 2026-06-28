using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class TgPreviewServiceTests
{
    [Fact]
    public void BuildPreview_includes_chunk_count_for_long_copy()
    {
        var copy = new string('x', 5000);
        var job = CreatePendingJob(copy);

        var preview = TgPreviewService.BuildPreview([job], limit: 10);

        Assert.Equal(1, preview.Count);
        Assert.True(preview.Entries[0].PartCount > 1);
        Assert.Contains("分", preview.Entries[0].PreviewText);
    }

    [Fact]
    public void BuildPreview_returns_empty_for_non_pending_jobs()
    {
        var job = new PublishJob
        {
            Id = "a",
            Title = "No Copy",
            Paths = new JobPaths { Slug = "no-copy" },
            Publish = new JobPublishState()
        };

        var preview = TgPreviewService.BuildPreview([job]);

        Assert.Equal(0, preview.Count);
    }

    private static PublishJob CreatePendingJob(string copy)
        => new()
        {
            Id = "a",
            Title = "Ready Game",
            Paths = new JobPaths { Slug = "ready-game" },
            Publish = new JobPublishState
            {
                GeneratedCopy = copy,
                Baidu = new PublishChannelState { Link = "https://pan.baidu.com/s/abc", Status = PublishStatusHelper.Published },
                Quark = new PublishChannelState { Link = "https://pan.quark.cn/s/xyz", Status = PublishStatusHelper.Published },
                Telegram = new PublishChannelState { Status = PublishStatusHelper.Pending }
            }
        };
}