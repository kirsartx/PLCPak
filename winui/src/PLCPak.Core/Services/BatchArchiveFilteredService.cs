using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class BatchArchiveFilteredService
{
    public static BatchArchiveFilteredResult Preview(
        IEnumerable<PublishJob> jobs,
        JobListFilter filter = JobListFilter.All,
        string? searchText = null,
        string? tagFilter = null,
        JobSortOrder sort = JobSortOrder.UpdatedDesc)
    {
        var preview = PreviewDetailed(jobs, filter, searchText, tagFilter, sort);
        var archivable = GetArchivableJobs(jobs, filter, searchText, tagFilter, sort);
        return new BatchArchiveFilteredResult
        {
            Count = preview.ArchivableCount,
            JobIds = archivable.Select(job => job.Id).ToList(),
            SummaryText = preview.SummaryText
        };
    }

    public static BatchArchiveFilteredPreviewResult PreviewDetailed(
        IEnumerable<PublishJob> jobs,
        JobListFilter filter,
        string? searchText,
        string? tagFilter,
        JobSortOrder sort)
    {
        var matched = QueryFilteredJobs(jobs, filter, searchText, tagFilter, sort);
        var archivable = matched.Where(job => job.Status != JobStatus.Archived).ToList();
        var alreadyArchived = matched.Count - archivable.Count;

        return new BatchArchiveFilteredPreviewResult
        {
            TotalMatched = matched.Count,
            ArchivableCount = archivable.Count,
            AlreadyArchivedCount = alreadyArchived,
            SampleTitles = archivable.Take(8).Select(job => job.Title).ToList(),
            SummaryText = BuildPreviewSummary(matched.Count, archivable.Count, alreadyArchived)
        };
    }

    public static BatchArchiveFilteredResult Archive(
        IEnumerable<PublishJob> jobs,
        JobStore jobStore,
        JobListFilter filter = JobListFilter.All,
        string? searchText = null,
        string? tagFilter = null,
        JobSortOrder sort = JobSortOrder.UpdatedDesc)
    {
        var bulk = ArchiveJobs(jobs, jobStore, filter, searchText, tagFilter, sort);
        return new BatchArchiveFilteredResult
        {
            Count = bulk.Archived,
            Archived = bulk.Archived,
            Skipped = bulk.Skipped,
            Messages = bulk.Messages,
            SummaryText = bulk.Archived == 0 && bulk.Skipped == 0
                ? "筛选归档：无匹配任务"
                : $"筛选归档：归档 {bulk.Archived}，跳过 {bulk.Skipped}"
        };
    }

    public static BulkArchiveResult ArchiveJobs(
        IEnumerable<PublishJob> jobs,
        JobStore jobStore,
        JobListFilter filter,
        string? searchText,
        string? tagFilter,
        JobSortOrder sort)
    {
        var result = new BulkArchiveResult();
        var matched = QueryFilteredJobs(jobs, filter, searchText, tagFilter, sort);

        foreach (var job in matched)
        {
            if (job.Status == JobStatus.Archived)
            {
                result.Skipped++;
                result.Messages.Add($"[跳过] {job.Title}: 已归档");
                continue;
            }

            jobStore.Archive(job.Id);
            result.Archived++;
            result.Messages.Add($"[归档] {job.Title}");
        }

        return result;
    }

    private static List<PublishJob> GetArchivableJobs(
        IEnumerable<PublishJob> jobs,
        JobListFilter filter,
        string? searchText,
        string? tagFilter,
        JobSortOrder sort)
        => QueryFilteredJobs(jobs, filter, searchText, tagFilter, sort)
            .Where(job => job.Status != JobStatus.Archived)
            .ToList();

    private static IReadOnlyList<PublishJob> QueryFilteredJobs(
        IEnumerable<PublishJob> jobs,
        JobListFilter filter,
        string? searchText,
        string? tagFilter,
        JobSortOrder sort)
        => JobQueryService.Sort(
            JobQueryService.Query(jobs, filter, searchText, tagFilter: tagFilter),
            sort);

    private static string BuildPreviewSummary(int totalMatched, int archivableCount, int alreadyArchivedCount)
    {
        if (totalMatched == 0)
            return "批量归档筛选：当前筛选条件下没有任务";

        if (archivableCount == 0)
            return $"批量归档筛选：匹配 {totalMatched} 个任务，均已归档，无需操作";

        var archivedHint = alreadyArchivedCount > 0
            ? $"，跳过已归档 {alreadyArchivedCount} 个"
            : string.Empty;
        return $"批量归档筛选：匹配 {totalMatched} 个任务，可归档 {archivableCount} 个{archivedHint}";
    }
}