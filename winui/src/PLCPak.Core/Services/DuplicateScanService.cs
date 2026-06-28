using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class DuplicateScanService
{
    public static DuplicateScanResult Scan(IEnumerable<PublishJob> jobs)
    {
        var list = jobs as IReadOnlyList<PublishJob> ?? jobs.ToList();
        var groups = new List<DuplicateScanGroup>();

        groups.AddRange(BuildThreadUrlGroups(list));
        groups.AddRange(BuildTitleGroups(list));

        var duplicateJobCount = groups.Sum(g => g.Jobs.Count);

        return new DuplicateScanResult
        {
            GroupCount = groups.Count,
            DuplicateJobCount = duplicateJobCount,
            Groups = groups,
            SummaryText = groups.Count == 0
                ? "重复扫描：未发现重复任务"
                : $"重复扫描：{groups.Count} 组重复，共 {duplicateJobCount} 个任务"
        };
    }

    private static IEnumerable<DuplicateScanGroup> BuildThreadUrlGroups(IReadOnlyList<PublishJob> jobs)
    {
        var byUrl = jobs
            .Where(job => !string.IsNullOrWhiteSpace(job.Source.ThreadUrl))
            .GroupBy(job => JobQueryService.NormalizeThreadUrl(job.Source.ThreadUrl))
            .Where(group => group.Count() > 1);

        foreach (var group in byUrl)
        {
            yield return new DuplicateScanGroup
            {
                Reason = "SameThreadUrl",
                Key = group.Key,
                Jobs = group.Select(ToMatch).ToList()
            };
        }
    }

    private static IEnumerable<DuplicateScanGroup> BuildTitleGroups(IReadOnlyList<PublishJob> jobs)
    {
        var byTitle = jobs
            .GroupBy(job => job.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        foreach (var group in byTitle)
        {
            var threadJobIds = new HashSet<string>(
                jobs.Where(j => !string.IsNullOrWhiteSpace(j.Source.ThreadUrl))
                    .GroupBy(j => JobQueryService.NormalizeThreadUrl(j.Source.ThreadUrl))
                    .Where(g => g.Count() > 1)
                    .SelectMany(g => g.Select(j => j.Id)));

            var filtered = group.Where(job => !threadJobIds.Contains(job.Id)).ToList();
            if (filtered.Count <= 1)
                continue;

            yield return new DuplicateScanGroup
            {
                Reason = "SameTitle",
                Key = group.Key,
                Jobs = filtered.Select(ToMatch).ToList()
            };
        }
    }

    public static DuplicateReportExportResult ExportReport(
        DuplicateScanResult scan,
        string workspaceRoot,
        string? outputPath = null)
    {
        var fullPath = ResolveExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        JsonHelper.WriteFile(fullPath, scan, utf8Bom: true);

        return new DuplicateReportExportResult
        {
            ExportPath = fullPath,
            GroupCount = scan.GroupCount,
            DuplicateJobCount = scan.DuplicateJobCount
        };
    }

    private static string ResolveExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        var fileName = $"duplicates-{DateTime.Now:yyyy-MM-dd-HHmmss}.json";
        return Path.Combine(reportsDir, fileName);
    }

    private static JobDuplicateMatch ToMatch(PublishJob job)
        => new()
        {
            Reason = string.Empty,
            JobId = job.Id,
            Title = job.Title,
            Slug = job.Paths.Slug,
            ThreadUrl = job.Source.ThreadUrl ?? string.Empty,
            Status = job.Status
        };
}