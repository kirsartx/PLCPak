using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class DuplicateMergeSuggestionServiceTests
{
    [Fact]
    public void BuildSuggestions_picks_processed_job_as_target()
    {
        var jobs = new[]
        {
            CreateJob("a", "Same Game", JobStatus.Draft),
            CreateJob("b", "Same Game", JobStatus.Processed, baiduLink: "https://pan.baidu.com/s/a")
        };

        var result = DuplicateMergeSuggestionService.BuildSuggestions(jobs);

        Assert.Equal(1, result.GroupCount);
        Assert.Equal("b", result.Suggestions[0].TargetJobId);
        Assert.Equal(["a"], result.Suggestions[0].SourceJobIds);
    }

    [Fact]
    public void BuildSuggestions_with_precomputed_scan_matches_full_build()
    {
        var jobs = new[]
        {
            CreateJob("a", "Same Game", JobStatus.Draft),
            CreateJob("b", "Same Game", JobStatus.Processed, baiduLink: "https://pan.baidu.com/s/a")
        };
        var scan = DuplicateScanService.Scan(jobs);

        var withScan = DuplicateMergeSuggestionService.BuildSuggestions(jobs, scan);
        var withoutScan = DuplicateMergeSuggestionService.BuildSuggestions(jobs);

        Assert.Equal(withoutScan.GroupCount, withScan.GroupCount);
        Assert.Equal(withoutScan.MergeActionCount, withScan.MergeActionCount);
        Assert.Equal(withoutScan.Suggestions[0].TargetJobId, withScan.Suggestions[0].TargetJobId);
        Assert.Equal(withoutScan.Suggestions[0].SourceJobIds, withScan.Suggestions[0].SourceJobIds);
    }

    [Fact]
    public void PickPrimaryJob_prefers_more_complete_publish_state()
    {
        var processed = CreateJob("p", "Game", JobStatus.Processed, baiduLink: "x", hasCopy: true);
        var draft = CreateJob("d", "Game", JobStatus.Draft);

        var primary = DuplicateMergeSuggestionService.PickPrimaryJob([draft, processed]);

        Assert.Equal("p", primary.Id);
    }

    private static PublishJob CreateJob(
        string id,
        string title,
        JobStatus status,
        string? baiduLink = null,
        bool hasCopy = false)
        => new()
        {
            Id = id,
            Title = title,
            Status = status,
            Paths = new JobPaths { Slug = id },
            Publish = new JobPublishState
            {
                Baidu = new PublishChannelState { Link = baiduLink ?? string.Empty },
                GeneratedCopy = hasCopy ? "copy" : string.Empty
            }
        };
}