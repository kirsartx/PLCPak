using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class ActivityLogCsvExportService
{
    public static ActivityLogCsvExportResult Export(
        string workspaceRoot,
        string? outputPath = null,
        string? query = null,
        string? category = null,
        int? sinceDays = null,
        DateTime? since = null,
        DateTime? until = null)
    {
        var entries = ActivityLogService.Search(
            workspaceRoot,
            query: query,
            limit: int.MaxValue,
            category: category,
            sinceDays: sinceDays,
            since: since,
            until: until);

        var csv = BuildCsv(entries);
        var fullPath = ResolveExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, csv, Encoding.UTF8);

        var categoryFilter = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        var queryFilter = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        var hints = new List<string>();
        if (categoryFilter is not null)
            hints.Add($"分类 {categoryFilter}");
        if (queryFilter is not null)
            hints.Add($"关键词「{queryFilter}」");
        var hintText = hints.Count == 0 ? string.Empty : $"（{string.Join("，", hints)}）";

        return new ActivityLogCsvExportResult
        {
            ExportPath = fullPath,
            EntryCount = entries.Count,
            CategoryFilter = categoryFilter,
            QueryFilter = queryFilter,
            SummaryText = $"已导出活动日志 CSV：{entries.Count} 条{hintText}"
        };
    }

    private static string BuildCsv(IReadOnlyList<ActivityLogEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("timestamp,category,message");
        foreach (var entry in entries)
        {
            sb.Append(Csv(FormatTimestamp(entry.Timestamp))).Append(',');
            sb.Append(Csv(entry.Category)).Append(',');
            sb.Append(Csv(entry.Message));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ResolveExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"activity-log-{DateTime.Now:yyyy-MM-dd-HHmmss}.csv";
        return Path.Combine(reportsDir, fileName);
    }

    private static string FormatTimestamp(DateTime value)
        => value.ToString("yyyy-MM-dd HH:mm:ss");

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            return $"\"{text.Replace("\"", "\"\"")}\"";
        return text;
    }
}