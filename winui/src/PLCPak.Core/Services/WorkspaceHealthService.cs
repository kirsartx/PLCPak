using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class WorkspaceHealthService
{
    private readonly WorkspaceService _workspace;
    private readonly JobStore _store;

    public WorkspaceHealthService(WorkspaceService workspace, JobStore store)
    {
        _workspace = workspace;
        _store = store;
    }

    public WorkspaceHealthReport GetReport() => Scan();

    public string GetSummaryText()
    {
        var report = Scan();
        var orphanCount = report.OrphanInboxDirs.Count + report.OrphanExtractDirs.Count + report.OrphanOutputDirs.Count;
        var sizeGb = report.TotalBytes / (1024.0 * 1024 * 1024);
        return $"工作区 {sizeGb:F1} GB | 孤儿目录 {orphanCount} | 缺 inbox {report.JobsWithoutInbox.Count}";
    }

    public WorkspaceHealthExport ExportReport()
    {
        var report = Scan();
        var health = JobHealthService.Compute(_store.List());
        Directory.CreateDirectory(_workspace.PublishedDirectory);
        var textPath = Path.Combine(_workspace.PublishedDirectory, "workspace-health.txt");
        var jsonPath = Path.Combine(_workspace.PublishedDirectory, "workspace-health.json");
        var text = BuildTextReport(report, health);
        File.WriteAllText(textPath, text, Encoding.UTF8);
        JsonHelper.WriteFile(jsonPath, new { exportedAt = DateTime.Now, summary = GetSummaryText(), jobHealth = health, workspace = report }, utf8Bom: true);
        return new WorkspaceHealthExport { ExportPath = textPath, JsonPath = jsonPath, Report = report, JobHealth = health };
    }

    public WorkspaceHealthReport Scan()
    {
        _workspace.EnsureLayout();
        var jobs = _store.List();
        var knownSlugs = new HashSet<string>(
            jobs.Select(j => j.Paths.Slug).Where(s => !string.IsNullOrWhiteSpace(s)),
            StringComparer.OrdinalIgnoreCase);

        var report = new WorkspaceHealthReport
        {
            OrphanInboxDirs = FindOrphanDirs(_workspace.InboxDirectory, knownSlugs),
            OrphanExtractDirs = FindOrphanDirs(_workspace.ExtractDirectory, knownSlugs),
            OrphanOutputDirs = FindOrphanDirs(_workspace.OutputDirectory, knownSlugs),
            JobsWithoutInbox = jobs
                .Where(j => !Directory.Exists(j.Paths.Inbox))
                .Select(j => new JobMissingInboxEntry
                {
                    JobId = j.Id,
                    Title = j.Title,
                    Slug = j.Paths.Slug,
                    ExpectedInboxPath = j.Paths.Inbox
                })
                .ToList(),
            InboxBytes = GetDirectorySize(_workspace.InboxDirectory),
            ExtractBytes = GetDirectorySize(_workspace.ExtractDirectory),
            OutputBytes = GetDirectorySize(_workspace.OutputDirectory),
            PublishedBytes = GetDirectorySize(_workspace.PublishedDirectory),
            JobsBytes = GetDirectorySize(_workspace.JobsDirectory)
        };

        report.TotalBytes = report.InboxBytes
            + report.ExtractBytes
            + report.OutputBytes
            + report.PublishedBytes
            + report.JobsBytes;

        return report;
    }

    private static List<string> FindOrphanDirs(string parentDirectory, IReadOnlySet<string> knownSlugs)
    {
        if (!Directory.Exists(parentDirectory))
            return [];

        return Directory.EnumerateDirectories(parentDirectory)
            .Where(dir => !knownSlugs.Contains(Path.GetFileName(dir)))
            .OrderBy(dir => dir, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static long GetDirectorySize(string directory)
    {
        if (!Directory.Exists(directory))
            return 0;

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                try
                {
                    return new FileInfo(path).Length;
                }
                catch
                {
                    return 0L;
                }
            })
            .Sum();
    }

    private static string BuildTextReport(WorkspaceHealthReport report, JobHealthReport health)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PLCPak 工作区健康报告");
        sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(health.SummaryText);
        sb.AppendLine($"总占用: {FormatBytes(report.TotalBytes)}");
        foreach (var issue in health.Issues)
            sb.AppendLine($"[{issue.Severity}] {issue.Title}: {issue.Message}");
        return sb.ToString();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}

public sealed class WorkspaceHealthExport
{
    public string ExportPath { get; set; } = string.Empty;
    public string JsonPath { get; set; } = string.Empty;
    public WorkspaceHealthReport Report { get; set; } = new();
    public JobHealthReport JobHealth { get; set; } = new();
}