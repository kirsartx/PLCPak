using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class PublishQueueServiceTests
{
    [Fact]
    public void BuildQueue_filters_pending_publish_and_orders_by_updated_desc()
    {
        var jobs = new[]
        {
            new PublishJob
            {
                Id = "1",
                Title = "旧待发布",
                Status = JobStatus.Processed,
                UpdatedAt = new DateTime(2026, 1, 1),
                Publish = new JobPublishState { GeneratedCopy = "copy" }
            },
            new PublishJob
            {
                Id = "2",
                Title = "新待发布",
                Status = JobStatus.Processed,
                UpdatedAt = new DateTime(2026, 3, 1),
                Publish = new JobPublishState
                {
                    Baidu = new PublishChannelState { Link = "https://pan.baidu.com/a" },
                    Quark = new PublishChannelState { Link = "https://pan.quark.cn/b" }
                }
            },
            new PublishJob
            {
                Id = "3",
                Title = "已发布",
                Status = JobStatus.Published,
                UpdatedAt = new DateTime(2026, 4, 1)
            },
            new PublishJob
            {
                Id = "4",
                Title = "进行中",
                Status = JobStatus.Extracted,
                UpdatedAt = new DateTime(2026, 5, 1)
            }
        };

        var snapshot = PublishQueueService.BuildQueue(jobs, limit: 10);

        Assert.Equal(2, snapshot.Count);
        Assert.Equal("2", snapshot.Entries[0].JobId);
        Assert.Equal("1", snapshot.Entries[1].JobId);
        Assert.True(snapshot.Entries[0].BaiduReady);
        Assert.True(snapshot.Entries[0].QuarkReady);
        Assert.Equal(2, snapshot.Entries[0].ReadyChannelCount);
        Assert.True(snapshot.Entries[1].HasCopy);
        Assert.Contains("待发布队列 2 个", snapshot.SummaryText);
        Assert.Contains("已生成文案 1 个", snapshot.SummaryText);
    }

    [Fact]
    public void BuildQueue_respects_limit()
    {
        var jobs = Enumerable.Range(1, 5)
            .Select(i => new PublishJob
            {
                Id = i.ToString(),
                Title = $"任务{i}",
                Status = JobStatus.Processed,
                UpdatedAt = new DateTime(2026, 1, i)
            })
            .ToList();

        var snapshot = PublishQueueService.BuildQueue(jobs, limit: 3);

        Assert.Equal(3, snapshot.Count);
        Assert.Equal(3, snapshot.Entries.Count);
    }
}