using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class DuplicateOperationsServiceTests
{
    [Fact]
    public void DuplicateOperationsService_BuildBundle_matches_separate_calls()
    {
        var jobs = new[]
        {
            CreateJob("a", "Dup"),
            CreateJob("b", "Dup", baiduLink: "https://pan.baidu.com/s/dup")
        };

        var bundle = DuplicateOperationsService.BuildBundle(jobs);
        var scan = DuplicateScanService.Scan(jobs);
        var suggestions = DuplicateMergeSuggestionService.BuildSuggestions(jobs, scan);

        Assert.Equal(scan.GroupCount, bundle.Scan.GroupCount);
        Assert.Equal(scan.DuplicateJobCount, bundle.Scan.DuplicateJobCount);
        Assert.Equal(scan.Groups.Count, bundle.Scan.Groups.Count);
        Assert.Equal(suggestions.GroupCount, bundle.Suggestions.GroupCount);
        Assert.Equal(suggestions.MergeActionCount, bundle.Suggestions.MergeActionCount);
        Assert.Equal(suggestions.Suggestions.Count, bundle.Suggestions.Suggestions.Count);
        Assert.Equal(suggestions.Suggestions[0].TargetJobId, bundle.Suggestions.Suggestions[0].TargetJobId);
        Assert.Equal(suggestions.Suggestions[0].SourceJobIds, bundle.Suggestions.Suggestions[0].SourceJobIds);
    }

    [Fact]
    public void BuildBundle_reuses_scan_for_suggestions_consistency()
    {
        var jobs = new[]
        {
            CreateJob("x", "Same"),
            CreateJob("y", "Same")
        };

        var bundle = DuplicateOperationsService.BuildBundle(jobs);
        var standaloneSuggestions = DuplicateMergeSuggestionService.BuildSuggestions(jobs);

        Assert.Equal(standaloneSuggestions.MergeActionCount, bundle.Suggestions.MergeActionCount);
        Assert.Equal(bundle.Scan.Groups[0].Key, bundle.Suggestions.Suggestions[0].Key);
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