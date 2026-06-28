using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class QueueCsvExportService
{
    public static QueueCsvExportResult ExportTgPending(
        IEnumerable<PublishJob> jobs,
        string workspaceRoot,
        int limit = 50,
        string? outputPath = null)
    {
        var snapshot = TgPendingService.GetPending(jobs, limit);
        var csv = BuildTgPendingCsv(snapshot.List);
        return WriteExport(workspaceRoot, "tg-pending", csv, snapshot.Count, outputPath);
    }

    public static QueueCsvExportResult ExportPublishQueue(
        IEnumerable<PublishJob> jobs,
        string workspaceRoot,
        int limit = 50,
        string? outputPath = null)
    {
        var snapshot = PublishQueueService.BuildQueue(jobs, limit);
        var csv = BuildPublishQueueCsv(snapshot.Entries);
        return WriteExport(workspaceRoot, "publish-queue", csv, snapshot.Count, outputPath);
    }

    private static string BuildTgPendingCsv(IReadOnlyList<TgPendingEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("jobId,title,slug,status,publishSummary,hasCopy,baiduPublished,quarkPublished,reason,updatedAt");
        foreach (var entry in entries)
        {
            sb.Append(Csv(entry.JobId)).Append(',');
            sb.Append(Csv(entry.Title)).Append(',');
            sb.Append(Csv(entry.Slug)).Append(',');
            sb.Append(Csv(entry.Status.ToString())).Append(',');
            sb.Append(Csv(entry.PublishSummary)).Append(',');
            sb.Append(entry.HasCopy ? "true" : "false").Append(',');
            sb.Append(entry.BaiduPublished ? "true" : "false").Append(',');
            sb.Append(entry.QuarkPublished ? "true" : "false").Append(',');
            sb.Append(Csv(entry.Reason)).Append(',');
            sb.Append(Csv(FormatDate(entry.UpdatedAt)));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildPublishQueueCsv(IReadOnlyList<PublishQueueEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("jobId,title,slug,status,publishSummary,hasCopy,baiduReady,quarkReady,telegramReady,readyChannelCount,updatedAt");
        foreach (var entry in entries)
        {
            sb.Append(Csv(entry.JobId)).Append(',');
            sb.Append(Csv(entry.Title)).Append(',');
            sb.Append(Csv(entry.Slug)).Append(',');
            sb.Append(Csv(entry.Status.ToString())).Append(',');
            sb.Append(Csv(entry.PublishSummary)).Append(',');
            sb.Append(entry.HasCopy ? "true" : "false").Append(',');
            sb.Append(entry.BaiduReady ? "true" : "false").Append(',');
            sb.Append(entry.QuarkReady ? "true" : "false").Append(',');
            sb.Append(entry.TelegramReady ? "true" : "false").Append(',');
            sb.Append(entry.ReadyChannelCount).Append(',');
            sb.Append(Csv(FormatDate(entry.UpdatedAt)));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static QueueCsvExportResult WriteExport(
        string workspaceRoot,
        string queueType,
        string csv,
        int entryCount,
        string? outputPath)
    {
        var fullPath = ResolveExportPath(workspaceRoot, queueType, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, csv, Encoding.UTF8);

        return new QueueCsvExportResult
        {
            ExportPath = fullPath,
            EntryCount = entryCount,
            QueueType = queueType,
            SummaryText = $"已导出 {queueType} CSV：{entryCount} 条"
        };
    }

    private static string ResolveExportPath(string workspaceRoot, string queueType, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"{queueType}-{DateTime.Now:yyyy-MM-dd-HHmmss}.csv";
        return Path.Combine(reportsDir, fileName);
    }

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