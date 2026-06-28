using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class SelectedJobsCsvExportService
{
    public static FilteredJobsCsvExportResult Export(
        IEnumerable<PublishJob> allJobs,
        IEnumerable<string> jobIds,
        string workspaceRoot,
        string? outputPath = null)
    {
        var jobsById = allJobs.ToDictionary(job => job.Id, StringComparer.OrdinalIgnoreCase);
        var selected = SelectJobs(jobsById, jobIds);
        var csv = JobsCsvExportHelper.BuildCsv(selected);
        var fullPath = ResolveExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, csv, Encoding.UTF8);

        return new FilteredJobsCsvExportResult
        {
            ExportPath = fullPath,
            EntryCount = selected.Count,
            SummaryText = $"已导出选中任务 CSV：{selected.Count} 条"
        };
    }

    private static List<PublishJob> SelectJobs(
        IReadOnlyDictionary<string, PublishJob> jobsById,
        IEnumerable<string> jobIds)
    {
        var selected = new List<PublishJob>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var jobId in jobIds)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                continue;

            var normalized = jobId.Trim();
            if (!seen.Add(normalized))
                continue;

            if (jobsById.TryGetValue(normalized, out var job))
                selected.Add(job);
        }

        return selected;
    }

    private static string ResolveExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"selected-jobs-{DateTime.Now:yyyy-MM-dd-HHmmss}.csv";
        return Path.Combine(reportsDir, fileName);
    }
}