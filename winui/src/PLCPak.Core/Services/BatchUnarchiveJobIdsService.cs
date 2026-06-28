using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class BatchUnarchiveJobIdsService
{
    public static BulkUnarchiveResult Unarchive(IEnumerable<string> jobIds, JobStore jobStore)
    {
        var result = new BulkUnarchiveResult();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawId in jobIds)
        {
            var jobId = rawId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(jobId) || !seen.Add(jobId))
                continue;

            var job = jobStore.Get(jobId);
            if (job is null)
            {
                result.Skipped++;
                result.Messages.Add($"[跳过] {jobId}: 任务不存在");
                continue;
            }

            if (job.Status != JobStatus.Archived)
            {
                result.Skipped++;
                result.Messages.Add($"[跳过] {job.Title}: 未归档");
                continue;
            }

            jobStore.Unarchive(jobId);
            result.Unarchived++;
            result.AffectedJobIds.Add(jobId);
            result.Messages.Add($"[恢复] {job.Title}");
        }

        return result;
    }
}