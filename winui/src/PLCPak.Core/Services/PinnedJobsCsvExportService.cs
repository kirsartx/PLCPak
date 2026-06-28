using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class PinnedJobsCsvExportService
{
    public static FilteredJobsCsvExportResult Export(
        IEnumerable<PublishJob> jobs,
        string workspaceRoot,
        string? outputPath = null)
    {
        var pinned = jobs.Where(job => job.IsPinned).ToList();
        var csv = JobsCsvExportHelper.BuildCsv(pinned);
        var fullPath = ResolveExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, csv, Encoding.UTF8);

        return new FilteredJobsCsvExportResult
        {
            ExportPath = fullPath,
            EntryCount = pinned.Count,
            SummaryText = $"已导出置顶任务 CSV：{pinned.Count} 条"
        };
    }

    private static string ResolveExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"pinned-jobs-{DateTime.Now:yyyy-MM-dd-HHmmss}.csv";
        return Path.Combine(reportsDir, fileName);
    }
}