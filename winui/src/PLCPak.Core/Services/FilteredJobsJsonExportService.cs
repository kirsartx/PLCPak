using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class FilteredJobsJsonExportService
{
    public static FilteredJobsJsonExportResult Export(
        IEnumerable<PublishJob> jobs,
        string workspaceRoot,
        JobListFilter filter,
        string? searchText,
        string? tagFilter,
        JobSortOrder sort,
        int? limit = null,
        string? outputPath = null)
    {
        var queried = JobQueryService.Query(jobs, filter, searchText, JobListSort.Updated, tagFilter);
        var sorted = JobQueryService.Sort(queried, sort);
        var selected = ApplyLimit(sorted, limit);
        var fullPath = ResolveExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        JsonHelper.WriteFile(fullPath, selected);

        return new FilteredJobsJsonExportResult
        {
            ExportPath = fullPath,
            EntryCount = selected.Count,
            SummaryText = $"已导出筛选任务 JSON：{selected.Count} 条"
        };
    }

    private static IReadOnlyList<PublishJob> ApplyLimit(IReadOnlyList<PublishJob> jobs, int? limit)
    {
        if (limit is not int value || value <= 0)
            return jobs;

        return jobs.Take(value).ToList();
    }

    private static string ResolveExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"filtered-jobs-{DateTime.Now:yyyy-MM-dd-HHmmss}.json";
        return Path.Combine(reportsDir, fileName);
    }
}