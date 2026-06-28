using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class JobHealthService
{
    public static JobHealthReport Compute(IEnumerable<PublishJob> jobs)
    {
        var list = jobs as IReadOnlyList<PublishJob> ?? jobs.ToList();
        var issues = new List<JobHealthIssue>();

        foreach (var job in list)
            issues.AddRange(InspectJob(job));

        var errors = issues.Count(i => i.Severity == "error");
        var warnings = issues.Count - errors;
        return new JobHealthReport
        {
            TotalJobs = list.Count,
            IssueCount = issues.Count,
            ErrorCount = errors,
            WarningCount = warnings,
            Issues = issues,
            SummaryText = issues.Count == 0
                ? $"健康检查通过：{list.Count} 个任务无异常"
                : $"健康检查：{list.Count} 个任务，{errors} 个错误，{warnings} 个警告"
        };
    }

    public static IReadOnlyList<JobHealthIssue> InspectJob(PublishJob job)
    {
        var issues = new List<JobHealthIssue>();

        if (job.Status == JobStatus.Failed)
        {
            issues.Add(Issue(job, "failed", "error", string.IsNullOrWhiteSpace(job.Error)
                ? "任务处于失败状态"
                : $"任务失败: {job.Error}"));
        }

        if (job.Status is JobStatus.Processed or JobStatus.Published)
        {
            if (string.IsNullOrWhiteSpace(job.Publish.Baidu.Link))
                issues.Add(Issue(job, "missing-baidu-link", "warning", "已压缩但未填写百度链接"));
            if (string.IsNullOrWhiteSpace(job.Publish.Quark.Link))
                issues.Add(Issue(job, "missing-quark-link", "warning", "已压缩但未填写夸克链接"));
        }

        if (job.Status == JobStatus.Processed
            && !MaintenanceService.HasFullLinks(job)
            && job.UpdatedAt < DateTime.Now.AddDays(-7))
        {
            issues.Add(Issue(job, "stale-pending-publish", "warning", "长期待发布"));
        }

        if (job.Status is JobStatus.Draft or JobStatus.InboxReady && job.Artifacts.InboxArchives.Count == 0)
            issues.Add(Issue(job, "empty-inbox", "warning", "inbox 尚无压缩包"));

        if (job.Status == JobStatus.Extracted)
            issues.Add(Issue(job, "awaiting-process", "warning", "已解压，等待去广告压缩"));

        if (job.Status == JobStatus.Archived && job.ArchivedFromStatus is null)
            issues.Add(Issue(job, "archived-no-source-status", "warning", "归档任务缺少原始状态记录"));

        return issues;
    }

    public static bool IsPendingPipeline(PublishJob job)
        => job.Status is JobStatus.Draft
            or JobStatus.InboxReady
            or JobStatus.Extracted
            or JobStatus.Failed;

    public static JobListFilter ParseBatchPipelineFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return JobListFilter.Active;

        return value.Trim().ToLowerInvariant() switch
        {
            "pending" or "pipeline" or "待处理" => JobListFilter.Active,
            "failed" or "失败" => JobListFilter.Failed,
            "all" or "全部" => JobListFilter.All,
            _ => PublishDashboardService.ParseFilter(value)
        };
    }

    public static IReadOnlyList<PublishJob> FilterForBatchPipeline(IEnumerable<PublishJob> jobs, JobListFilter filter)
    {
        var filtered = PublishDashboardService.Filter(jobs, filter);
        if (filter == JobListFilter.Active)
            return filtered.Where(j => IsPendingPipeline(j) || j.Status == JobStatus.Processed).ToList();

        if (filter == JobListFilter.All)
            return filtered.Where(j => j.Status != JobStatus.Archived && IsPendingPipeline(j)).ToList();

        return filtered.Where(IsPendingPipeline).ToList();
    }

    public static JobListFilter ParseBatchChainFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return JobListFilter.Active;

        return value.Trim().ToLowerInvariant() switch
        {
            "active" or "进行中" => JobListFilter.Active,
            "failed" or "失败" => JobListFilter.Failed,
            "all" or "全部" => JobListFilter.All,
            _ => JobListFilter.Active
        };
    }

    public static bool HasAutomatableChainAction(PublishJob job)
        => PublishWorkflowService.GetNextActionForJob(job)?.Action is { } action
            && action is not (JobNextActionType.None or JobNextActionType.FillLinks
                or JobNextActionType.MarkPublished or JobNextActionType.SendTelegram);

    public static IReadOnlyList<PublishJob> FilterForBatchChain(IEnumerable<PublishJob> jobs, JobListFilter filter)
    {
        IEnumerable<PublishJob> filtered = filter switch
        {
            JobListFilter.Failed => jobs.Where(j => j.Status == JobStatus.Failed),
            JobListFilter.All => jobs.Where(j => j.Status != JobStatus.Archived),
            _ => jobs.Where(j =>
                PublishDashboardService.IsActive(j)
                || j.Status is JobStatus.Failed or JobStatus.Processed or JobStatus.Published)
        };

        return filtered
            .Where(HasAutomatableChainAction)
            .OrderByDescending(j => j.UpdatedAt)
            .ToList();
    }

    private static JobHealthIssue Issue(PublishJob job, string code, string severity, string message) => new()
    {
        JobId = job.Id,
        Title = job.Title,
        Code = code,
        Severity = severity,
        Message = message
    };
}