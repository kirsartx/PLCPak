using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class ActivityLogBatchSummaryExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public int? SinceDays { get; set; }
    public ActivityLogBatchSummaryResult Stats { get; set; } = new();
    public string SummaryText { get; set; } = string.Empty;
}

public static class ActivityLogBatchSummaryExportService
{
    public static ActivityLogBatchSummaryExportResult Export(
        string workspaceRoot,
        string? outputPath = null,
        int? sinceDays = null)
    {
        var stats = ActivityLogBatchSummaryService.Build(workspaceRoot, sinceDays);
        var fullPath = ResolveExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        JsonHelper.WriteFile(fullPath, stats);

        var sinceHint = ActivityLogBatchSummaryService.FormatSinceDaysHint(sinceDays);
        return new ActivityLogBatchSummaryExportResult
        {
            ExportPath = fullPath,
            SinceDays = sinceDays,
            Stats = stats,
            SummaryText = $"已导出批量操作统计 JSON{sinceHint}：{stats.TotalCount} 条，{stats.Items.Count} 个分类"
        };
    }

    internal static string ResolveExportPathForStamp(string workspaceRoot, string stamp, string extension)
    {
        var reportsDir = Path.Combine(workspaceRoot, "reports");
        return Path.Combine(reportsDir, $"activity-log-batch-stats-{stamp}.{extension}");
    }

    public static ActivityLogBatchBundleExportResult ExportAll(
        string workspaceRoot,
        int? sinceDays = null)
    {
        var stats = ActivityLogBatchSummaryService.Build(workspaceRoot, sinceDays);
        var stamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
        var jsonPath = WriteJson(stats, workspaceRoot, stamp);
        var csvPath = ActivityLogBatchSummaryCsvExportService.Write(stats, workspaceRoot, stamp);

        var sinceHint = ActivityLogBatchSummaryService.FormatSinceDaysHint(sinceDays);
        return new ActivityLogBatchBundleExportResult
        {
            JsonExportPath = jsonPath,
            CsvExportPath = csvPath,
            BundleStamp = stamp,
            SinceDays = sinceDays,
            Stats = stats,
            SummaryText = $"已导出批量操作统计 JSON + CSV{sinceHint}：{stats.TotalCount} 条，{stats.Items.Count} 个分类"
        };
    }

    private static string WriteJson(
        ActivityLogBatchSummaryResult stats,
        string workspaceRoot,
        string? fileStamp = null)
    {
        var fullPath = ResolveExportPath(workspaceRoot, outputPath: null, fileStamp);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        JsonHelper.WriteFile(fullPath, stats);
        return fullPath;
    }

    private static string ResolveExportPath(string workspaceRoot, string? outputPath, string? fileStamp = null)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var stamp = fileStamp ?? DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
        var fileName = $"activity-log-batch-stats-{stamp}.json";
        return Path.Combine(reportsDir, fileName);
    }
}