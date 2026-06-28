using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchTagServiceTests
{
    [Fact]
    public void Apply_appends_tags_to_matching_jobs()
    {
        var jobs = new[]
        {
            CreateJob("a", ["existing"]),
            CreateJob("b", [])
        };

        var result = BatchTagService.Apply(
            jobs,
            "vip, hot",
            BatchTagMode.Append,
            jobIds: ["a"]);

        Assert.Equal(1, result.Applied);
        Assert.Equal(0, result.Skipped);
        Assert.Equal(["existing", "vip", "hot"], jobs[0].Tags);
    }

    [Fact]
    public void Apply_respects_tag_filter_from_query()
    {
        var jobs = new[]
        {
            CreateJob("a", ["existing"]),
            CreateJob("b", ["other"])
        };

        var filtered = JobQueryService.Query(jobs, JobListFilter.All, searchText: null, tagFilter: "existing");
        var result = BatchTagService.Apply(filtered, "vip", BatchTagMode.Append);

        Assert.Single(filtered);
        Assert.Equal(1, result.Applied);
        Assert.Contains("vip", jobs[0].Tags);
    }

    [Fact]
    public void Apply_replaces_tags_when_mode_is_replace()
    {
        var jobs = new[] { CreateJob("a", ["old"]) };

        var result = BatchTagService.Apply(jobs, "new", BatchTagMode.Replace);

        Assert.Equal(1, result.Applied);
        Assert.Equal(["new"], jobs[0].Tags);
    }

    private static PublishJob CreateJob(string id, IReadOnlyList<string> tags)
        => new()
        {
            Id = id,
            Title = $"Job {id}",
            Paths = new JobPaths { Slug = id },
            Tags = tags.ToList()
        };
}