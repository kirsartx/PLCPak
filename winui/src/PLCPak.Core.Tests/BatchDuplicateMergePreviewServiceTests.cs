using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchDuplicateMergePreviewServiceTests
{
    [Fact]
    public void PreviewAll_builds_items_without_invoking_merge()
    {
        var jobs = new[]
        {
            CreateJob("a", "Dup"),
            CreateJob("b", "Dup", baiduLink: "https://pan.baidu.com/s/a")
        };

        var preview = BatchDuplicateMergePreviewService.PreviewAll(jobs);

        Assert.Equal(1, preview.GroupCount);
        Assert.Equal(1, preview.MergeActionCount);
        Assert.Equal(1, preview.WouldMergeCount);
        Assert.Equal(0, preview.WouldSkipCount);
        Assert.Single(preview.Items);
        Assert.Equal("b", preview.Items[0].TargetJobId);
        Assert.Equal("Dup", preview.Items[0].TargetTitle);
        Assert.Equal(["a"], preview.Items[0].SourceJobIds);
        Assert.Equal("SameTitle", preview.Items[0].Reason);
        Assert.Contains("1 组", preview.SummaryText);
    }

    [Fact]
    public void PreviewAll_returns_empty_when_no_duplicates()
    {
        var jobs = new[]
        {
            CreateJob("a", "Alpha"),
            CreateJob("b", "Beta")
        };

        var preview = BatchDuplicateMergePreviewService.PreviewAll(jobs);

        Assert.Equal(0, preview.GroupCount);
        Assert.Equal(0, preview.MergeActionCount);
        Assert.Empty(preview.Items);
        Assert.Contains("未发现", preview.SummaryText);
    }

    private static PublishJob CreateJob(string id, string title, string? baiduLink = null)
        => new()
        {
            Id = id,
            Title = title,
            Status = JobStatus.Processed,
            Paths = new JobPaths { Slug = id },
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState { Link = baiduLink ?? string.Empty }
            }
        };
}