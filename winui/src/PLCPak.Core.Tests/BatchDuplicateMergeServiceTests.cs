using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchDuplicateMergeServiceTests
{
    [Fact]
    public void MergeAll_invokes_merge_for_each_source_in_group()
    {
        var jobs = new[]
        {
            CreateJob("a", "Dup"),
            CreateJob("b", "Dup", baiduLink: "https://pan.baidu.com/s/a")
        };

        var calls = new List<(string Target, string Source)>();
        var result = BatchDuplicateMergeService.MergeAll(
            jobs,
            (target, source) =>
            {
                calls.Add((target, source));
                return new JobMergeResult
                {
                    Success = true,
                    TargetJobId = target,
                    SourceJobId = source,
                    Message = "ok"
                };
            });

        Assert.Equal(1, result.GroupsProcessed);
        Assert.Equal(1, result.Merged);
        Assert.Equal("b", calls[0].Target);
        Assert.Equal("a", calls[0].Source);
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