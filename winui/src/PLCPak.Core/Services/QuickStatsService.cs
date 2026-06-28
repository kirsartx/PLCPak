using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public static class QuickStatsService
{
    public static string BuildOneLiner(IEnumerable<PublishJob> jobs, int staleDays = 7)
    {
        var list = jobs as IReadOnlyList<PublishJob> ?? jobs.ToList();
        var stats = PublishDashboardService.ComputeStats(list);
        var tgPending = TgPendingService.GetPending(list, limit: 100).Count;
        var duplicates = DuplicateScanService.Scan(list).GroupCount;
        var stale = MaintenanceService.BuildReport(list, staleDays).StaleCount;

        return $"待发布 {stats.PendingPublish} | TG待发 {tgPending} | 失败 {stats.Failed} | 重复 {duplicates} | 陈旧 {stale}";
    }
}