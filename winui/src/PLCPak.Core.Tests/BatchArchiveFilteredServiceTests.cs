using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchArchiveFilteredServiceTests : IDisposable
{
    private readonly string _root;
    private readonly JobStore _store;

    public BatchArchiveFilteredServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-batch-archive-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        var appRoot = Path.Combine(_root, "app");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(appRoot);
        File.WriteAllText(Path.Combine(data, "compress-config.json"), "{}");
        var ws = Path.Combine(_root, "workspace").Replace("\\", "\\\\");
        File.WriteAllText(Path.Combine(data, "studio-config.json"), $"{{\"workspaceRoot\":\"{ws}\"}}");

        _store = PlcPakAppContext.Create(appRoot).Jobs;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Preview_returns_count_for_matching_non_archived_jobs()
    {
        var published = SaveJob("已发布", JobStatus.Published, ["vip"]);
        var archived = SaveJob("已归档", JobStatus.Archived, ["vip"]);
        SaveJob("其他", JobStatus.Draft, ["other"]);

        var preview = BatchArchiveFilteredService.Preview(
            _store.List(),
            JobListFilter.Published,
            tagFilter: "vip");

        Assert.Equal(1, preview.Count);
        Assert.Single(preview.JobIds);
        Assert.Equal(published.Id, preview.JobIds[0]);
        Assert.Contains("可归档 1 个", preview.SummaryText);
        Assert.Equal(JobStatus.Archived, archived.Status);
    }

    [Fact]
    public void PreviewDetailed_includes_sample_titles_and_archivable_count()
    {
        SaveJob("可归档A", JobStatus.Published);
        SaveJob("可归档B", JobStatus.Processed);

        var preview = BatchArchiveFilteredService.PreviewDetailed(
            _store.List(),
            JobListFilter.All,
            searchText: "可归档",
            tagFilter: null,
            JobSortOrder.UpdatedDesc);

        Assert.Equal(2, preview.TotalMatched);
        Assert.Equal(2, preview.ArchivableCount);
        Assert.Equal(2, preview.SampleTitles.Count);
    }

    [Fact]
    public void Archive_archives_matching_jobs_and_skips_already_archived()
    {
        var published = SaveJob("待归档", JobStatus.Published);
        var archived = SaveJob("跳过", JobStatus.Archived);

        var result = BatchArchiveFilteredService.Archive(
            _store.List(),
            _store,
            JobListFilter.All,
            searchText: null,
            tagFilter: null);

        Assert.Equal(1, result.Count);
        Assert.Equal(1, result.Archived);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(JobStatus.Archived, _store.Get(published.Id)!.Status);
        Assert.Equal(JobStatus.Archived, _store.Get(archived.Id)!.Status);
        Assert.Contains(result.Messages, message => message.Contains("[归档] 待归档"));
        Assert.Contains(result.Messages, message => message.Contains("[跳过] 跳过"));
    }

    private PublishJob SaveJob(string title, JobStatus status, IReadOnlyList<string>? tags = null)
    {
        var job = _store.Create(title);
        job.Status = status;
        if (tags is not null)
            job.Tags = tags.ToList();
        _store.Save(job);
        return job;
    }
}