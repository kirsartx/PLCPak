using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class BatchPinFilteredServiceTests : IDisposable
{
    private readonly string _root;
    private readonly JobStore _store;

    public BatchPinFilteredServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-batch-pin-" + Guid.NewGuid().ToString("N"));
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
    public void Preview_returns_applicable_count_for_unpinned_jobs_when_pin_true()
    {
        var unpinned = SaveJob("待置顶", pinned: false, tags: ["vip"]);
        SaveJob("已置顶", pinned: true, tags: ["vip"]);
        SaveJob("其他", pinned: false, tags: ["other"]);

        var preview = BatchPinFilteredService.Preview(
            _store.List(),
            JobListFilter.All,
            tagFilter: "vip",
            pin: true);

        Assert.Equal(2, preview.TotalMatched);
        Assert.Equal(1, preview.ApplicableCount);
        Assert.Equal(1, preview.AlreadyInTargetStateCount);
        Assert.True(preview.Pin);
        Assert.Single(preview.SampleTitles);
        Assert.Equal(unpinned.Title, preview.SampleTitles[0]);
        Assert.Contains("可置顶 1 个", preview.SummaryText);
    }

    [Fact]
    public void Preview_returns_applicable_count_for_pinned_jobs_when_pin_false()
    {
        SaveJob("待取消", pinned: true);
        SaveJob("未置顶", pinned: false);

        var preview = BatchPinFilteredService.Preview(
            _store.List(),
            JobListFilter.All,
            pin: false);

        Assert.Equal(2, preview.TotalMatched);
        Assert.Equal(1, preview.ApplicableCount);
        Assert.False(preview.Pin);
        Assert.Contains("可取消置顶 1 个", preview.SummaryText);
    }

    [Fact]
    public void BatchPinFilteredJobs_pins_matching_jobs_and_skips_already_pinned()
    {
        var unpinned = SaveJob("待置顶", pinned: false);
        var pinned = SaveJob("已置顶", pinned: true);

        var result = BatchPinFilteredService.BatchPinFilteredJobs(
            _store.List(),
            _store,
            JobListFilter.All,
            pin: true);

        Assert.Equal(1, result.Applied);
        Assert.Equal(1, result.Skipped);
        Assert.True(_store.Get(unpinned.Id)!.IsPinned);
        Assert.True(_store.Get(pinned.Id)!.IsPinned);
        Assert.Contains(result.Messages, message => message.Contains("[置顶] 待置顶"));
        Assert.Contains(result.Messages, message => message.Contains("[跳过] 已置顶"));
    }

    [Fact]
    public void BatchPinFilteredJobs_unpins_matching_jobs_and_skips_already_unpinned()
    {
        var pinned = SaveJob("待取消", pinned: true);
        var unpinned = SaveJob("未置顶", pinned: false);

        var result = BatchPinFilteredService.BatchPinFilteredJobs(
            _store.List(),
            _store,
            JobListFilter.All,
            pin: false);

        Assert.Equal(1, result.Applied);
        Assert.Equal(1, result.Skipped);
        Assert.False(_store.Get(pinned.Id)!.IsPinned);
        Assert.False(_store.Get(unpinned.Id)!.IsPinned);
        Assert.Contains(result.Messages, message => message.Contains("[取消置顶] 待取消"));
    }

    private PublishJob SaveJob(string title, bool pinned = false, IReadOnlyList<string>? tags = null)
    {
        var job = _store.Create(title);
        job.IsPinned = pinned;
        if (tags is not null)
            job.Tags = tags.ToList();
        _store.Save(job);
        return job;
    }
}