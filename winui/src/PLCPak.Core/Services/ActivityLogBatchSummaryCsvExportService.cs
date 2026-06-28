using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class ActivityLogBatchSummaryCsvExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public int? SinceDays { get; set; }
    public ActivityLogBatchSummaryResult Stats { get; set; } = new();
    public string SummaryText { get; set; } = string.Empty;
}

public static class ActivityLogBatchSummaryCsvExportService
{
    public static ActivityLogBatchSummaryCsvExportResult Export(
        string workspaceRoot,
        string? outputPath = null,
        int? sinceDays = null)
    {
        var stats = ActivityLogBatchSummaryService.Build(workspaceRoot, sinceDays);
        var fullPath = Write(stats, workspaceRoot, fileStamp: null, outputPath);

        var sinceHint = ActivityLogBatchSummaryService.FormatSinceDaysHint(sinceDays);
        return new ActivityLogBatchSummaryCsvExportResult
        {
            ExportPath = fullPath,
            SinceDays = sinceDays,
            Stats = stats,
            SummaryText = $"已导出批量操作统计 CSV{sinceHint}：{stats.TotalCount} 条，{stats.Items.Count} 个分类"
        };
    }

    internal static string Write(
        ActivityLogBatchSummaryResult stats,
        string workspaceRoot,
        string? fileStamp = null,
        string? outputPath = null)
    {
        var fullPath = ResolveExportPath(workspaceRoot, outputPath, fileStamp);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, BuildCsv(stats), Encoding.UTF8);
        return fullPath;
    }

    private static string BuildCsv(ActivityLogBatchSummaryResult stats)
    {
        var sb = new StringBuilder();
        sb.AppendLine("category,label,count");
        foreach (var item in stats.Items)
        {
            sb.Append(Csv(item.Category)).Append(',');
            sb.Append(Csv(item.Label)).Append(',');
            sb.Append(item.Count);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            return $"\"{text.Replace("\"", "\"\"")}\"";
        return text;
    }

    private static string ResolveExportPath(string workspaceRoot, string? outputPath, string? fileStamp = null)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var stamp = fileStamp ?? DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
        return ActivityLogBatchSummaryExportService.ResolveExportPathForStamp(workspaceRoot, stamp, "csv");
    }
}