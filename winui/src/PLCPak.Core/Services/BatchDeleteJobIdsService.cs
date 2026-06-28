using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class BatchDeleteJobIdsService
{
    public static BatchDeleteJobIdsPreviewResult Preview(
        IEnumerable<string> jobIds,
        JobStore jobStore,
        WorkspaceService workspace)
    {
        var matched = ResolveJobs(jobIds, jobStore);
        var folderPaths = CollectFolderPaths(matched, workspace);

        return new BatchDeleteJobIdsPreviewResult
        {
            Count = matched.Count,
            JobIds = matched.Select(job => job.Id).ToList(),
            SampleTitles = matched.Take(8).Select(job => job.Title).ToList(),
            FolderCandidateCount = folderPaths.Count,
            SampleFolderPaths = folderPaths.Take(6).ToList(),
            SummaryText = matched.Count == 0
                ? "批量删除预览：没有可删除的有效任务"
                : $"批量删除预览：{matched.Count} 个任务；删目录可选永久删除或移到回收站，涉及 {folderPaths.Count} 个目录路径"
        };
    }

    public static BatchDeleteJobIdsResult Delete(
        IEnumerable<string> jobIds,
        JobStore jobStore,
        WorkspaceService workspace,
        bool deleteFolders = false,
        bool useRecycleBin = false)
    {
        var result = new BatchDeleteJobIdsResult
        {
            DeletedFolders = deleteFolders,
            UseRecycleBin = deleteFolders && useRecycleBin
        };
        var matched = ResolveJobs(jobIds, jobStore);

        foreach (var job in matched)
        {
            try
            {
                if (deleteFolders)
                {
                    var removed = JobFolderRemovalService.RemoveJobFolders(job, workspace, useRecycleBin);
                    result.RemovedPaths.AddRange(removed);
                }

                jobStore.Delete(job.Id);
                result.Deleted++;
                result.DeletedJobIds.Add(job.Id);
                var mode = deleteFolders
                    ? useRecycleBin ? "删除+回收站" : "删除+目录"
                    : "仅删记录";
                result.Messages.Add($"[{mode}] {job.Title}");
            }
            catch (Exception ex)
            {
                result.Skipped++;
                result.Messages.Add($"[失败] {job.Title}: {ex.Message}");
            }
        }

        result.SummaryText = result.Deleted == 0 && result.Skipped == 0
            ? "批量删除：没有可删除的有效任务"
            : $"批量删除：成功 {result.Deleted}，失败/跳过 {result.Skipped}";
        return result;
    }

    private static List<PublishJob> ResolveJobs(IEnumerable<string> jobIds, JobStore jobStore)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matched = new List<PublishJob>();

        foreach (var rawId in jobIds)
        {
            var jobId = rawId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(jobId) || !seen.Add(jobId))
                continue;

            var job = jobStore.Get(jobId);
            if (job is null)
                continue;

            matched.Add(job);
        }

        return matched;
    }

    private static List<string> CollectFolderPaths(IEnumerable<PublishJob> jobs, WorkspaceService workspace)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in jobs)
        {
            foreach (var dir in JobFolderRemovalService.GetRemovableDirectories(job, workspace))
                paths.Add(dir);
        }

        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
    }
}