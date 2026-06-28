using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class JobMergeServiceTests
{
    [Fact]
    public void Merge_fills_empty_target_fields_from_source()
    {
        var target = new PublishJob
        {
            Id = "target",
            Title = "目标任务",
            Paths = new JobPaths { Slug = "target-slug" },
            Source = new JobSource { ThreadUrl = string.Empty, ArchivePassword = string.Empty },
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState(),
                Quark = new PublishChannelState(),
                GeneratedCopy = string.Empty
            }
        };

        var source = new PublishJob
        {
            Id = "source",
            Title = "来源任务",
            Source = new JobSource
            {
                ThreadUrl = "https://forum.test/thread/1",
                ArchivePassword = "pwd123",
                DownloadHint = "见百度"
            },
            Notes = "来源备注",
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState { Link = "https://pan.baidu.com/a", Password = "1111" },
                Quark = new PublishChannelState { Link = "https://pan.quark.cn/b", Password = "2222" },
                GeneratedCopy = "发布文案"
            }
        };

        var result = JobMergeService.Merge(target, source);

        Assert.True(result.Success);
        Assert.Equal("target", result.TargetJobId);
        Assert.Equal("source", result.SourceJobId);
        Assert.Equal("https://forum.test/thread/1", target.Source.ThreadUrl);
        Assert.Equal("pwd123", target.Source.ArchivePassword);
        Assert.Equal("见百度", target.Source.DownloadHint);
        Assert.Equal("来源备注", target.Notes);
        Assert.Equal("https://pan.baidu.com/a", target.Publish.Baidu.Link);
        Assert.Equal("发布文案", target.Publish.GeneratedCopy);
        Assert.Contains(nameof(JobSource.ThreadUrl), result.MergedFields);
        Assert.Contains(nameof(JobPublishState.GeneratedCopy), result.MergedFields);
    }

    [Fact]
    public void Merge_appends_notes_when_target_already_has_notes()
    {
        var target = new PublishJob
        {
            Id = "target",
            Title = "目标任务",
            Notes = "已有备注"
        };

        var source = new PublishJob
        {
            Id = "source",
            Title = "来源任务",
            Notes = "新增备注"
        };

        var result = JobMergeService.Merge(target, source);

        Assert.True(result.Success);
        Assert.Contains("已有备注", target.Notes);
        Assert.Contains("新增备注", target.Notes);
        Assert.Contains("---", target.Notes);
        Assert.Contains(nameof(PublishJob.Notes), result.MergedFields);
    }

    [Fact]
    public void Merge_rejects_same_job()
    {
        var job = new PublishJob { Id = "same", Title = "同一任务" };

        var result = JobMergeService.Merge(job, job);

        Assert.False(result.Success);
        Assert.Contains("同一个任务", result.Error);
    }

    [Fact]
    public void PreviewMerge_does_not_mutate_original_jobs()
    {
        var target = new PublishJob
        {
            Id = "target",
            Title = "目标任务",
            Source = new JobSource { ThreadUrl = string.Empty },
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState(),
                Quark = new PublishChannelState()
            }
        };

        var source = new PublishJob
        {
            Id = "source",
            Title = "来源任务",
            Source = new JobSource { ThreadUrl = "https://forum.test/thread/2" },
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState { Link = "https://pan.baidu.com/b" }
            }
        };

        var result = JobMergeService.PreviewMerge(target, source);

        Assert.True(result.Success);
        Assert.Equal(string.Empty, target.Source.ThreadUrl);
        Assert.Equal(string.Empty, target.Publish.Baidu.Link);
        Assert.Contains(nameof(JobSource.ThreadUrl), result.MergedFields);
    }
}