using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

internal static class JobsCsvExportHelper
{
    public const string Header = "jobId,title,status,slug,tags,publishSummary,updatedAt,isPinned,baiduLink,baiduPwd,quarkLink,quarkPwd,telegramLink,hasCopy";

    public static string BuildCsv(IReadOnlyList<PublishJob> jobs)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);
        foreach (var job in jobs)
        {
            sb.Append(Csv(job.Id)).Append(',');
            sb.Append(Csv(job.Title)).Append(',');
            sb.Append(Csv(job.Status.ToString())).Append(',');
            sb.Append(Csv(job.Paths.Slug)).Append(',');
            sb.Append(Csv(FormatTags(job.Tags))).Append(',');
            sb.Append(Csv(job.PublishStatusLabel)).Append(',');
            sb.Append(Csv(FormatDate(job.UpdatedAt))).Append(',');
            sb.Append(job.IsPinned ? "true" : "false").Append(',');
            sb.Append(Csv(job.Publish.Baidu.Link)).Append(',');
            sb.Append(Csv(job.Publish.Baidu.Password)).Append(',');
            sb.Append(Csv(job.Publish.Quark.Link)).Append(',');
            sb.Append(Csv(job.Publish.Quark.Password)).Append(',');
            sb.Append(Csv(job.Publish.Telegram.Link)).Append(',');
            sb.Append(!string.IsNullOrWhiteSpace(job.Publish.GeneratedCopy) ? "true" : "false");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatTags(IEnumerable<string> tags)
        => string.Join(";", tags.Select(tag => tag.Trim()).Where(tag => !string.IsNullOrWhiteSpace(tag)));

    private static string FormatDate(DateTime value)
        => value.ToString("yyyy-MM-dd HH:mm:ss");

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            return $"\"{text.Replace("\"", "\"\"")}\"";
        return text;
    }
}