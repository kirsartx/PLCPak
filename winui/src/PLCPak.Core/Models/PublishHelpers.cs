using System.Text.Json.Serialization;
using PLCPak.Core.Services;

namespace PLCPak.Core.Models;

public static class PublishStatusHelper
{
    public const string Pending = "pending";
    public const string Ready = "ready";
    public const string Published = "published";

    public static string DescribeChannel(PublishChannelState channel, string name)
        => DescribeChannel(channel, name, UiDisplayContext.CurrentLanguage);

    public static string DescribeChannel(PublishChannelState channel, string name, string? language)
    {
        var lang = UiStringTable.NormalizeLanguage(language ?? UiDisplayContext.CurrentLanguage);
        var formatKey = channel.Status switch
        {
            Published => "publish.channel.published",
            Ready => "publish.channel.ready",
            _ => "publish.channel.pending"
        };
        return string.Format(UiStringTable.Get(formatKey, lang), name);
    }

    public static string BuildSummary(JobPublishState publish)
        => BuildSummary(publish, UiDisplayContext.CurrentLanguage);

    public static string BuildSummary(JobPublishState publish, string? language)
    {
        var lang = UiStringTable.NormalizeLanguage(language ?? UiDisplayContext.CurrentLanguage);
        return string.Join(" | ",
            DescribeChannel(publish.Baidu, UiStringTable.Get("publish.channel.baidu", lang), lang),
            DescribeChannel(publish.Quark, UiStringTable.Get("publish.channel.quark", lang), lang),
            DescribeChannel(publish.Telegram, UiStringTable.Get("publish.channel.telegram", lang), lang));
    }

    public static bool IsFullyPublished(JobPublishState publish)
        => publish.Baidu.Status == Published
            && publish.Quark.Status == Published
            && publish.Telegram.Status == Published;

    public static void ApplyLinkStatus(PublishChannelState channel)
    {
        if (channel.Status == Published)
            return;

        channel.Status = string.IsNullOrWhiteSpace(channel.Link) ? Pending : Ready;
    }

    public static void MarkPublished(PublishChannelState channel, bool requireLink = true)
    {
        if (requireLink && string.IsNullOrWhiteSpace(channel.Link))
            throw new InvalidOperationException("尚未填写链接，无法标记已发布");

        channel.Status = Published;
        channel.PublishedAt = DateTime.Now;
    }
}

public sealed partial class PublishJob
{
    [JsonIgnore]
    public string PublishStatusLabel => PublishStatusHelper.BuildSummary(Publish);

    [JsonIgnore]
    public string StatusLabel => JobStatusDisplayHelper.ToLocalized(Status);
}

public static class PublishLinkFormatter
{
    public static PublishLinksSnapshot Build(PublishJob job)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(job.Publish.Baidu.Link))
        {
            lines.Add($"百度: {job.Publish.Baidu.Link}");
            if (!string.IsNullOrWhiteSpace(job.Publish.Baidu.Password))
                lines.Add($"百度提取码: {job.Publish.Baidu.Password}");
        }

        if (!string.IsNullOrWhiteSpace(job.Publish.Quark.Link))
        {
            lines.Add($"夸克: {job.Publish.Quark.Link}");
            if (!string.IsNullOrWhiteSpace(job.Publish.Quark.Password))
                lines.Add($"夸克提取码: {job.Publish.Quark.Password}");
        }

        if (!string.IsNullOrWhiteSpace(job.Publish.Telegram.Link))
            lines.Add($"TG: {job.Publish.Telegram.Link}");

        return new PublishLinksSnapshot
        {
            JobId = job.Id,
            Title = job.Title,
            BaiduLink = job.Publish.Baidu.Link,
            BaiduPassword = job.Publish.Baidu.Password,
            QuarkLink = job.Publish.Quark.Link,
            QuarkPassword = job.Publish.Quark.Password,
            TelegramLink = job.Publish.Telegram.Link,
            FormattedText = lines.Count == 0 ? "(尚未填写发布链接)" : string.Join(Environment.NewLine, lines)
        };
    }
}