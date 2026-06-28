using System.Text;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class DailyReportService
{
    public static DailyReportSnapshot BuildReport(IEnumerable<PublishJob> jobs, DateTime? date = null)
    {
        var list = jobs as IReadOnlyList<PublishJob> ?? jobs.ToList();
        var stats = PublishDashboardService.ComputeStats(list);
        var reportDate = (date ?? DateTime.Today).Date;

        return new DailyReportSnapshot
        {
            Date = reportDate,
            TotalJobs = list.Count,
            Stats = stats,
            PendingPublishCount = stats.PendingPublish,
            FailedCount = stats.Failed,
            Entries = list
                .OrderByDescending(j => j.UpdatedAt)
                .Select(ToEntry)
                .ToList()
        };
    }

    public static string ToCsv(DailyReportSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("jobId,title,status,statusLabel,publishSummary,nextActionLabel,updatedAt");
        foreach (var entry in snapshot.Entries)
        {
            sb.Append(Csv(entry.JobId)).Append(',');
            sb.Append(Csv(entry.Title)).Append(',');
            sb.Append(Csv(entry.Status.ToString())).Append(',');
            sb.Append(Csv(entry.StatusLabel)).Append(',');
            sb.Append(Csv(entry.PublishSummary)).Append(',');
            sb.Append(Csv(entry.NextActionLabel)).Append(',');
            sb.Append(Csv(FormatDate(entry.UpdatedAt)));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string Export(string workspaceRoot, DailyReportSnapshot snapshot)
    {
        var reportsDir = Path.Combine(workspaceRoot, "reports");
        Directory.CreateDirectory(reportsDir);

        var dateKey = snapshot.Date.ToString("yyyy-MM-dd");
        var csvPath = Path.Combine(reportsDir, $"daily-{dateKey}.csv");
        var jsonPath = Path.Combine(reportsDir, $"daily-{dateKey}.json");

        File.WriteAllText(csvPath, ToCsv(snapshot), Encoding.UTF8);
        JsonHelper.WriteFile(jsonPath, snapshot, utf8Bom: true);

        snapshot.CsvPath = csvPath;
        snapshot.JsonPath = jsonPath;
        return csvPath;
    }

    private static DailyReportEntry ToEntry(PublishJob job)
    {
        var next = PublishWorkflowService.GetNextActionForJob(job);
        return new DailyReportEntry
        {
            JobId = job.Id,
            Title = job.Title,
            Status = job.Status,
            StatusLabel = job.StatusLabel,
            PublishSummary = job.PublishStatusLabel,
            NextActionLabel = next?.ActionLabel ?? string.Empty,
            UpdatedAt = job.UpdatedAt
        };
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