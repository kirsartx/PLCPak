using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class GlobalShortcutDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Keys { get; init; } = string.Empty;
}

public static class GlobalShortcutRegistry
{
    public const string JobsBatchArchiveFiltered = "jobs.batchArchiveFiltered";
    public const string JobsBatchArchiveSelected = "jobs.batchArchiveSelected";
    public const string JobsBatchUnarchiveSelected = "jobs.batchUnarchiveSelected";
    public const string JobsBatchDeleteSelected = "jobs.batchDeleteSelected";
    public const string JobsBatchAppendTags = "jobs.batchAppendTags";
    public const string JobsExportFilteredCsv = "jobs.exportFilteredCsv";
    public const string JobsExportJobJson = "jobs.exportJobJson";
    public const string OpsExportStatsCsv = "ops.exportStatsCsv";
    public const string OpsExportStatsHtml = "ops.exportStatsHtml";
    public const string OpsFilterBatchActivity = "ops.filterBatchActivity";
    public const string OpsExportBatchStatsJson = "ops.exportBatchStatsJson";
    public const string OpsExportBatchStatsCsv = "ops.exportBatchStatsCsv";
    public const string OpsExportBatchStatsAll = "ops.exportBatchStatsAll";
    public const string OpsLoadMoreActivityLog = "ops.loadMoreActivityLog";
    public const string GlobalCommandPalette = "global.commandPalette";

    public static IReadOnlyList<GlobalShortcutDefinition> Definitions { get; } =
    [
        new() { Id = GlobalCommandPalette, Keys = "Ctrl+K", Label = "命令面板" },
        new() { Id = JobsExportJobJson, Keys = "Ctrl+Shift+E", Label = "导出当前任务 JSON" },
        new() { Id = JobsExportFilteredCsv, Keys = "Ctrl+Shift+C", Label = "导出筛选任务 CSV" },
        new() { Id = JobsBatchArchiveFiltered, Keys = "Ctrl+Shift+A", Label = "批量归档筛选" },
        new() { Id = JobsBatchArchiveSelected, Keys = "Ctrl+Shift+G", Label = "多选批量归档" },
        new() { Id = JobsBatchUnarchiveSelected, Keys = "Ctrl+Shift+U", Label = "多选批量恢复" },
        new() { Id = JobsBatchDeleteSelected, Keys = "Ctrl+Shift+D", Label = "多选批量删除" },
        new() { Id = JobsBatchAppendTags, Keys = "Ctrl+Shift+T", Label = "多选批量追加标签" },
        new() { Id = OpsExportStatsCsv, Keys = "Ctrl+Shift+S", Label = "导出活动日志统计 CSV" },
        new() { Id = OpsExportStatsHtml, Keys = "Ctrl+Shift+H", Label = "导出活动日志统计 HTML" },
        new() { Id = OpsFilterBatchActivity, Keys = "Ctrl+Shift+B", Label = "筛选批量操作日志" },
        new() { Id = OpsExportBatchStatsJson, Keys = "Ctrl+Shift+J", Label = "导出批量操作统计 JSON" },
        new() { Id = OpsExportBatchStatsCsv, Keys = "Ctrl+Shift+?", Label = "导出批量操作统计 CSV" },
        new() { Id = OpsExportBatchStatsAll, Keys = "Ctrl+Shift+O", Label = "导出批量操作统计 JSON+CSV" },
        new() { Id = OpsLoadMoreActivityLog, Keys = "Ctrl+Shift+L", Label = "加载更多活动日志" }
    ];

    public static GlobalShortcutDefinition? FindDefinition(string shortcutId)
        => Definitions.FirstOrDefault(definition =>
            string.Equals(definition.Id, shortcutId, StringComparison.OrdinalIgnoreCase));

    public static bool IsEnabled(UiPreferences prefs, string shortcutId)
    {
        if (prefs.DisableGlobalShortcuts)
            return false;

        return !prefs.DisabledShortcuts.Contains(shortcutId, StringComparer.OrdinalIgnoreCase);
    }
}