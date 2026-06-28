using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class PublishWorkflowServiceTests
{
    [Fact]
    public void BuildSnapshot_prioritizes_failed_over_pipeline()
    {
        var failed = CreateJob("失败任务", JobStatus.Failed, updatedAt: new DateTime(2026, 6, 25, 12, 0, 0));
        failed.Error = "解压失败";
        var draft = CreateJob("草稿任务", JobStatus.Draft, updatedAt: new DateTime(2026, 6, 25, 13, 0, 0));

        var snapshot = PublishWorkflowService.BuildSnapshot([failed, draft], limit: 5);

        Assert.NotNull(snapshot.Primary);
        Assert.Equal(JobNextActionType.RetryFailed, snapshot.Primary!.Action);
        Assert.Equal(1, snapshot.Primary.Priority);
        Assert.Equal("失败任务", snapshot.Primary.Title);
        Assert.Single(snapshot.Suggestions);
        Assert.Equal(JobNextActionType.RunPipeline, snapshot.Suggestions[0].Action);
    }

    [Fact]
    public void BuildSnapshot_prioritizes_download_inbox_over_pipeline()
    {
        var download = CreateJob("待下载", JobStatus.Draft, updatedAt: new DateTime(2026, 6, 25, 10, 0, 0));
        download.Source.ThreadUrl = "https://forum.test/thread/1";

        var inboxReady = CreateJob("可流水线", JobStatus.InboxReady, updatedAt: new DateTime(2026, 6, 25, 11, 0, 0));
        inboxReady.Artifacts.InboxArchives = [@"C:\inbox\game.7z"];

        var snapshot = PublishWorkflowService.BuildSnapshot([download, inboxReady], limit: 5);

        Assert.Equal(JobNextActionType.DownloadInbox, snapshot.Primary!.Action);
        Assert.Equal(2, snapshot.Primary.Priority);
        Assert.Equal("待下载", snapshot.Primary.Title);
    }

    [Fact]
    public void BuildSnapshot_orders_same_priority_by_updated_at_desc()
    {
        var older = CreateJob("较旧失败", JobStatus.Failed, updatedAt: new DateTime(2026, 6, 24, 8, 0, 0));
        var newer = CreateJob("较新失败", JobStatus.Failed, updatedAt: new DateTime(2026, 6, 25, 9, 0, 0));

        var snapshot = PublishWorkflowService.BuildSnapshot([older, newer], limit: 5);

        Assert.Equal("较新失败", snapshot.Primary!.Title);
        Assert.Single(snapshot.Suggestions);
        Assert.Equal("较旧失败", snapshot.Suggestions[0].Title);
    }

    [Fact]
    public void BuildSnapshot_for_single_job_matches_get_next_action_scenario()
    {
        var job = CreateJob("单任务文案", JobStatus.Processed, updatedAt: new DateTime(2026, 6, 25, 5, 0, 0));
        job.Publish.Baidu.Link = "https://pan.baidu.com/s/test";
        job.Publish.Quark.Link = "https://pan.quark.cn/s/test";
        job.Publish.Telegram.Link = "https://t.me/test";

        var snapshot = PublishWorkflowService.BuildSnapshot([job], limit: 1);

        Assert.NotNull(snapshot.Primary);
        Assert.Equal(job.Id, snapshot.Primary!.JobId);
        Assert.Equal(JobNextActionType.GenerateCopy, snapshot.Primary.Action);
        Assert.Equal("生成发布文案", snapshot.Primary.ActionLabel);
        Assert.Empty(snapshot.Suggestions);
    }

    [Fact]
    public void BuildSnapshot_follows_publish_workflow_priority_chain()
    {
        var fillLinks = CreateJob("缺链接", JobStatus.Processed, updatedAt: new DateTime(2026, 6, 25, 1, 0, 0));
        var generateCopy = CreateJob("缺文案", JobStatus.Processed, updatedAt: new DateTime(2026, 6, 25, 2, 0, 0));
        generateCopy.Publish.Baidu.Link = "https://pan.baidu.com/s/test";
        generateCopy.Publish.Quark.Link = "https://pan.quark.cn/s/test";
        generateCopy.Publish.Telegram.Link = "https://t.me/test";

        var markPublished = CreateJob("待标记", JobStatus.Processed, updatedAt: new DateTime(2026, 6, 25, 3, 0, 0));
        markPublished.Publish.Baidu.Link = "https://pan.baidu.com/s/test";
        markPublished.Publish.Quark.Link = "https://pan.quark.cn/s/test";
        markPublished.Publish.Telegram.Link = "https://t.me/test";
        markPublished.Publish.GeneratedCopy = "发布文案";

        var sendTelegram = CreateJob("待发TG", JobStatus.Published, updatedAt: new DateTime(2026, 6, 25, 4, 0, 0));
        sendTelegram.Publish.GeneratedCopy = "发布文案";
        sendTelegram.Publish.Baidu.Link = "https://pan.baidu.com/s/test";
        sendTelegram.Publish.Quark.Link = "https://pan.quark.cn/s/test";
        PublishStatusHelper.MarkPublished(sendTelegram.Publish.Baidu);
        PublishStatusHelper.MarkPublished(sendTelegram.Publish.Quark);
        sendTelegram.Publish.Telegram.Link = string.Empty;
        sendTelegram.Publish.Telegram.Status = PublishStatusHelper.Ready;

        var snapshot = PublishWorkflowService.BuildSnapshot(
            [sendTelegram, markPublished, generateCopy, fillLinks],
            limit: 4);

        Assert.Equal(JobNextActionType.FillLinks, snapshot.Primary!.Action);
        Assert.Equal(3, snapshot.Suggestions.Count);
        Assert.Equal(JobNextActionType.GenerateCopy, snapshot.Suggestions[0].Action);
        Assert.Equal(JobNextActionType.MarkPublished, snapshot.Suggestions[1].Action);
        Assert.Equal(JobNextActionType.SendTelegram, snapshot.Suggestions[2].Action);
        Assert.Contains("建议操作 4 项", snapshot.SummaryText);
    }

    [Theory]
    [InlineData("retry", JobNextActionType.RetryFailed)]
    [InlineData("download", JobNextActionType.DownloadInbox)]
    [InlineData("pipeline", JobNextActionType.RunPipeline)]
    [InlineData("copy", JobNextActionType.GenerateCopy)]
    [InlineData("mark", JobNextActionType.MarkPublished)]
    [InlineData("telegram", JobNextActionType.SendTelegram)]
    public void ParseActionOverride_maps_cli_names(string actionName, JobNextActionType expected)
    {
        Assert.Equal(expected, PublishWorkflowService.ParseActionOverride(actionName));
    }

    [Fact]
    public void ParseActionOverride_rejects_unknown_action()
    {
        Assert.Throws<ArgumentException>(() => PublishWorkflowService.ParseActionOverride("invalid"));
    }

    private static PublishJob CreateJob(string title, JobStatus status, DateTime updatedAt)
    {
        return new PublishJob
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            Status = status,
            UpdatedAt = updatedAt
        };
    }
}