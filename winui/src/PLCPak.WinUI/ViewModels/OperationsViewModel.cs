using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLCPak.Core.Models;
using PLCPak.Core.Services;
using PLCPak.WinUI.Infrastructure;

namespace PLCPak.WinUI.ViewModels;

public sealed class StaleJobItem
{
    public string JobId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public int Days { get; init; }
    public string DisplayText { get; init; } = string.Empty;
    public string DaysNotUpdatedText { get; init; } = string.Empty;
}

public sealed class DuplicateGroupItem
{
    public string Reason { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<JobDuplicateMatch> Jobs { get; init; } = [];
}

public sealed class DuplicateMergeSuggestionItem
{
    public string TargetJobId { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class BatchMergePreviewItem
{
    public string TargetJobId { get; init; } = string.Empty;
    public string TargetTitle { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public int SourceCount { get; init; }
}

public sealed class ActivityLogStatItem
{
    public string Category { get; init; } = string.Empty;
    public int Count { get; init; }
    public double BarPercent { get; init; }
    public string Summary => string.IsNullOrWhiteSpace(Category)
        ? $"(未分类): {Count}"
        : $"{Category}: {Count}";
}

public sealed class ActivityLogDailyItem
{
    public string Date { get; init; } = string.Empty;
    public int Count { get; init; }
    public double BarPercent { get; init; }
    public string Summary => $"{Date}: {Count}";
}

public sealed class ActivityLogBatchStatItem
{
    public string Category { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    public double BarPercent { get; init; }
    public string Summary => $"{Label}: {Count}";
}

public sealed class OperationsSectionItem
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string SummaryText { get; init; } = string.Empty;
    public bool IsActivityLogSection =>
        Key.StartsWith("activity-log", StringComparison.OrdinalIgnoreCase);
}

public partial class OperationsViewModel : ObservableObject
{
    private const int ActivityLogSearchDebounceMs = 400;

    private readonly PlcPakAppContext _app;
    private readonly UiDispatcher _ui;
    private readonly UiModeNotifier _uiMode;
    private CancellationTokenSource? _activityLogSearchDebounceCts;
    private bool _activityLogSinceDaysDefaulted;

    public OperationsViewModel(PlcPakAppContext app)
    {
        _app = app;
        _ui = UiDispatcher.ForCurrentThread();
        _uiMode = new UiModeNotifier(app, () =>
        {
            OnPropertyChanged(nameof(IsProfessionalMode));
            OnPropertyChanged(nameof(IsSimpleMode));
        });
        LocalizationService.LanguageChanged += OnLanguageChanged;
        RefreshCore();
    }

    public bool IsProfessionalMode => _uiMode.IsProfessionalMode;
    public bool IsSimpleMode => _uiMode.IsSimpleMode;

    public string OpsTitle => LocalizationService.T("ops.page.title");
    public string OpsSimpleHint => LocalizationService.T("ops.page.simpleHint");
    public string OpsProHint => LocalizationService.T("ops.page.proHint");
    public string OpsRefreshLabel => LocalizationService.T("ops.page.refresh");
    public string OpsRefreshingHint => LocalizationService.T("ops.page.refreshing");
    public string OpsTaskOverviewTitle => LocalizationService.T("ops.page.taskOverview");
    public string OpsSectionsTitle => LocalizationService.T("ops.page.opsSections");
    public string OpsFullSummaryHeader => LocalizationService.T("ops.page.fullOpsSummary");
    public string OpsMaintenanceCheckTitle => LocalizationService.T("ops.page.maintenanceCheck");
    public string OpsJobHealthTitle => LocalizationService.T("ops.page.jobHealth");
    public string OpsStaleJobsTitle => LocalizationService.T("ops.page.staleJobs");
    public string OpsStaleJobsHint => LocalizationService.T("ops.page.staleJobsHint");
    public string OpsDuplicateScanTitle => LocalizationService.T("ops.page.duplicateScan");
    public string OpsAutoMergeDuplicatesLabel => LocalizationService.T("ops.page.autoMergeDuplicates");
    public string OpsPreviewMergeLabel => LocalizationService.T("ops.page.previewMerge");
    public string OpsExportMergePreviewJsonLabel => LocalizationService.T("ops.page.exportMergePreviewJson");
    public string OpsExportDuplicateReportLabel => LocalizationService.T("ops.page.exportDuplicateReport");
    public string OpsExportMergeSuggestionsLabel => LocalizationService.T("ops.page.exportMergeSuggestions");
    public string OpsActivityLogAndStatsHeader => LocalizationService.T("ops.page.activityLogAndStats");
    public string OpsActivityLogTitle => LocalizationService.T("ops.page.activityLog");
    public string OpsActivityLogHint => LocalizationService.T("ops.page.activityLogHint");
    public string OpsRecentActiveCategoriesLabel => LocalizationService.T("ops.page.recentActiveCategories");
    public string OpsCategoryHeader => LocalizationService.T("ops.page.category");
    public string OpsKeywordHeader => LocalizationService.T("ops.page.keyword");
    public string OpsDaysHeader => LocalizationService.T("ops.page.days");
    public string OpsSearchActivityLogPlaceholder => LocalizationService.T("ops.page.searchActivityLogPlaceholder");
    public string OpsDaysPlaceholder => LocalizationService.T("ops.page.daysPlaceholder");
    public string OpsClearFiltersLabel => LocalizationService.T("ops.page.clearFilters");
    public string StaleJobsCountText => StaleJobCount > 0
        ? string.Format(LocalizationService.T("ops.page.staleJobsCount"), StaleJobCount)
        : string.Empty;
    public string StaleJobsLoadingHint => LocalizationService.T("ops.page.staleJobsLoading");
    public bool ShowStaleJobsList => !IsRefreshing && StaleJobCount > 0;
    public string DuplicateGroupsCountText => DuplicateGroupCount > 0
        ? string.Format(LocalizationService.T("ops.page.duplicateGroupsCount"), DuplicateGroupCount)
        : string.Empty;
    public string DuplicateMergeSuggestionsCountText => DuplicateMergeSuggestions.Count > 0
        ? string.Format(LocalizationService.T("ops.page.duplicateSuggestionsCount"), DuplicateMergeSuggestions.Count)
        : string.Empty;
    public bool HasBulkArchiveCandidates => PublishedArchivableCount > 0;
    public string BulkArchiveButtonText => PublishedArchivableCount > 0
        ? string.Format(LocalizationService.T("ops.page.bulkArchiveButtonCount"), PublishedArchivableCount)
        : OpsBulkArchivePublishedLabel;
    public string OpsBatchFilterShortcutLabel => LocalizationService.T("ops.page.batchFilterShortcut");
    public string OpsFilterAllBatchLabel => LocalizationService.T("ops.page.filterAllBatch");
    public string OpsFilterBatchArchiveLabel => LocalizationService.T("ops.page.filterBatchArchive");
    public string OpsFilterBatchDeleteLabel => LocalizationService.T("ops.page.filterBatchDelete");
    public string OpsFilterBatchTagsLabel => LocalizationService.T("ops.page.filterBatchTags");
    public string OpsFilterBatchUnarchiveLabel => LocalizationService.T("ops.page.filterBatchUnarchive");
    public string OpsExportBatchJsonCsvLabel => LocalizationService.T("ops.page.exportBatchJsonCsv");
    public string OpsExportBatchJsonOnlyLabel => LocalizationService.T("ops.page.exportBatchJsonOnly");
    public string OpsExportBatchCsvOnlyLabel => LocalizationService.T("ops.page.exportBatchCsvOnly");
    public string OpsCategoryStatsTitle => LocalizationService.T("ops.page.categoryStats");
    public string OpsExportStatsJsonLabel => LocalizationService.T("ops.page.exportStatsJson");
    public string OpsExportStatsCsvLabel => LocalizationService.T("ops.page.exportStatsCsv");
    public string OpsExportStatsHtmlLabel => LocalizationService.T("ops.page.exportStatsHtml");
    public string OpsExportDailyStatsCsvLabel => LocalizationService.T("ops.page.exportDailyStatsCsv");
    public string OpsLast7DaysActivityTitle => LocalizationService.T("ops.page.last7DaysActivity");
    public string OpsLoadMoreLabel => LocalizationService.T("ops.page.loadMore");
    public string ActivityLogLoadingMoreHint => LocalizationService.T("ops.page.activityLogLoadingMore");
    public string ActivityLogLoadedSummary => RecentActivity.Count > 0
        ? string.Format(LocalizationService.T("ops.page.activityLogLoaded"), RecentActivity.Count)
        : string.Empty;
    public string OpsCommonOpsHeader => LocalizationService.T("ops.page.commonOps");
    public string OpsBulkArchivePublishedLabel => LocalizationService.T("ops.page.bulkArchivePublished");
    public string OpsExportDailyLabel => LocalizationService.T("ops.page.exportDaily");
    public string OpsExportOpsReportLabel => LocalizationService.T("ops.page.exportOpsReport");
    public string OpsExportNightlyScriptLabel => LocalizationService.T("ops.page.exportNightlyScript");
    public string OpsBackupRestoreHeader => LocalizationService.T("ops.page.backupRestore");
    public string OpsBackupAllJobsLabel => LocalizationService.T("ops.page.backupAllJobs");
    public string OpsBackupFullLabel => LocalizationService.T("ops.page.backupFull");
    public string OpsRestoreFullBackupLabel => LocalizationService.T("ops.page.restoreFullBackup");
    public string OpsActivityLogMaintenanceHeader => LocalizationService.T("ops.page.activityLogMaintenance");
    public string OpsExportActivityLogLabel => LocalizationService.T("ops.page.exportActivityLog");
    public string OpsExportCsvLabel => LocalizationService.T("ops.page.exportCsv");
    public string OpsTrimActivityLogLabel => LocalizationService.T("ops.page.trimActivityLog");
    public string OpsArchiveActivityLogLabel => LocalizationService.T("ops.page.archiveActivityLog");
    public string OpsExportMachineProfileLabel => LocalizationService.T("ops.page.exportMachineProfile");
    public string OpsImportMachineProfileLabel => LocalizationService.T("ops.page.importMachineProfile");

    public ObservableCollection<StaleJobItem> StaleJobs { get; } = [];
    public ObservableCollection<DuplicateGroupItem> DuplicateGroups { get; } = [];
    public ObservableCollection<DuplicateMergeSuggestionItem> DuplicateMergeSuggestions { get; } = [];
    public ObservableCollection<BatchMergePreviewItem> BatchMergePreviewItems { get; } = [];
    public ObservableCollection<ActivityLogStatItem> ActivityLogStats { get; } = [];
    public ObservableCollection<ActivityLogDailyItem> ActivityLogDailyStats { get; } = [];
    public ObservableCollection<ActivityLogBatchStatItem> ActivityLogBatchStats { get; } = [];
    public ObservableCollection<ActivityLogEntry> RecentActivity { get; } = [];
    public ObservableCollection<string> ActivityCategories { get; } = [];
    public ObservableCollection<string> RecentCategories { get; } = [];
    public ObservableCollection<OperationsSectionItem> OperationsSections { get; } = [];

    [ObservableProperty] private string _operationsSummaryText = string.Empty;
    [ObservableProperty] private string _maintenanceSummary = string.Empty;
    [ObservableProperty] private string _workspacePath = string.Empty;
    [ObservableProperty] private string _quickStatsLine = string.Empty;
    [ObservableProperty] private string _healthSummaryText = string.Empty;
    [ObservableProperty] private string _workflowSummaryText = string.Empty;
    [ObservableProperty] private string _duplicateScanSummary = string.Empty;
    [ObservableProperty] private string _duplicateMergeSuggestionSummary = string.Empty;
    [ObservableProperty] private string _activityCategoryFilter = string.Empty;
    [ObservableProperty] private string _activityLogSearchText = string.Empty;
    [ObservableProperty] private string _activityLogSinceDays = string.Empty;
    [ObservableProperty] private string _activityLogEmptyHint = string.Empty;
    [ObservableProperty] private string _activityLogStatsSummary = string.Empty;
    [ObservableProperty] private string _activityLogStatsEmptyHint = string.Empty;
    [ObservableProperty] private string _activityLogDailyStatsSummary = string.Empty;
    [ObservableProperty] private string _mergePreviewEmptyHint = string.Empty;
    [ObservableProperty] private string _activityLogPageSummary = string.Empty;
    [ObservableProperty] private int _activityLogMatchCount;
    [ObservableProperty] private string _activityLogResultBadge = string.Empty;
    [ObservableProperty] private string _activityLogBatchSummary = string.Empty;
    [ObservableProperty] private string _activityLogBatchShortcutHint = string.Empty;
    [ObservableProperty] private string _activityLogFilterSummary = string.Empty;

    public bool IsActivityLogFilterActive =>
        !string.IsNullOrWhiteSpace(ActivityCategoryFilter)
        || !string.IsNullOrWhiteSpace(ActivityLogSearchText)
        || IsSinceDaysFilterActive();
    [ObservableProperty] private string _activityLogSinceDaysHint = string.Empty;
    [ObservableProperty] private string _staleJobsEmptyHint = string.Empty;
    [ObservableProperty] private string _duplicateGroupsEmptyHint = string.Empty;
    [ObservableProperty] private string _bulkArchiveHint = string.Empty;
    [ObservableProperty] private int _staleJobCount;
    [ObservableProperty] private int _duplicateGroupCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadMoreActivityLogCommand))]
    private int _activityLogOffset;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadMoreActivityLogCommand))]
    private bool _activityLogHasMore;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadMoreActivityLogCommand))]
    private bool _isLoadingMoreActivityLog;

    [ObservableProperty] private bool _isActivityLogSearchPending;

    public string ActivityLogSearchPendingHint => LocalizationService.T("ops.page.activityLogSearchPending");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BulkArchivePublishedCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchMergeDuplicatesCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStaleJobsList))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBulkArchiveCandidates))]
    [NotifyPropertyChangedFor(nameof(BulkArchiveButtonText))]
    [NotifyCanExecuteChangedFor(nameof(BulkArchivePublishedCommand))]
    private int _publishedArchivableCount;

    partial void OnIsBusyChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();

    private bool CanRefresh() => !IsRefreshing && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        if (IsRefreshing)
            return;

        IsRefreshing = true;
        try
        {
            await Task.Yield();
            RefreshCore();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public void RefreshCore()
    {
        EnsureDefaultActivityLogSinceDays();
        WorkspacePath = _app.Workspace.GetWorkspaceRoot();

        var snapshot = _app.JobRunner.GetOperationsCenterSnapshot();
        OperationsSummaryText = snapshot.SummaryText;
        QuickStatsLine = snapshot.QuickStatsLine;
        WorkflowSummaryText = snapshot.Workflow.SummaryText;
        HealthSummaryText = snapshot.Health.SummaryText;

        OperationsSections.Clear();
        foreach (var section in snapshot.Sections)
        {
            OperationsSections.Add(new OperationsSectionItem
            {
                Key = section.Key,
                Title = section.Title,
                SummaryText = section.SummaryText
            });
        }

        var maintenance = snapshot.Maintenance;
        MaintenanceSummary = maintenance.SummaryText;
        PublishedArchivableCount = maintenance.PublishedReadyToArchive;
        ApplyBulkArchiveHint();

        StaleJobs.Clear();
        foreach (var entry in maintenance.StaleJobs)
            StaleJobs.Add(CreateStaleJobItem(entry.JobId, entry.Title, entry.Reason, entry.DaysSinceUpdate));

        StaleJobCount = StaleJobs.Count;
        OnPropertyChanged(nameof(StaleJobsCountText));
        OnPropertyChanged(nameof(ShowStaleJobsList));
        ApplyStaleJobsEmptyHint();

        RefreshDuplicatesOnly();

        ActivityCategories.Clear();
        ActivityCategories.Add(string.Empty);
        foreach (var category in ActivityLogService.GetCategories(WorkspacePath))
            ActivityCategories.Add(category);

        RecentCategories.Clear();
        foreach (var category in ActivityLogService.GetRecentCategories(WorkspacePath, limit: 5))
            RecentCategories.Add(category);

        RefreshActivityLogOnly(reset: true);
        RefreshActivityLogStats();
        RefreshActivityLogDailyStats();
        RefreshActivityLogBatchSummary();
        RefreshActivityLogBatchShortcutHint();
        NotifyActivityLogFilterStateChanged();
        ApplyActivityLogSinceDaysHint();
    }

    [RelayCommand]
    private void ClearActivityLogFilters()
    {
        ActivityCategoryFilter = string.Empty;
        ActivityLogSearchText = string.Empty;
        ActivityLogSinceDays = string.Empty;
        _activityLogSinceDaysDefaulted = false;
        EnsureDefaultActivityLogSinceDays();
        NotifyActivityLogFilterStateChanged();
        ApplyActivityLogSinceDaysHint();
    }

    private void NotifyActivityLogFilterStateChanged()
    {
        OnPropertyChanged(nameof(IsActivityLogFilterActive));
        UpdateActivityLogFilterSummary();
    }

    private bool IsSinceDaysFilterActive()
    {
        var keepDays = _app.StudioConfig.Load().ActivityLogKeepDays;
        if (string.IsNullOrWhiteSpace(ActivityLogSinceDays))
            return false;

        if (!int.TryParse(ActivityLogSinceDays.Trim(), out var days) || days <= 0)
            return true;

        return keepDays <= 0 || days != keepDays;
    }

    private void UpdateActivityLogFilterSummary()
    {
        if (!IsActivityLogFilterActive)
        {
            ActivityLogFilterSummary = string.Empty;
            return;
        }

        var parts = new List<string>();
        if (IsSinceDaysFilterActive())
        {
            if (int.TryParse(ActivityLogSinceDays.Trim(), out var days) && days > 0)
                parts.Add(string.Format(LocalizationService.T("ops.page.filterPart.days"), days));
            else
                parts.Add(LocalizationService.T("ops.page.filterPart.allDays"));
        }

        if (!string.IsNullOrWhiteSpace(ActivityCategoryFilter))
            parts.Add(string.Format(LocalizationService.T("ops.page.filterCategory"), ActivityCategoryFilter.Trim()));
        if (!string.IsNullOrWhiteSpace(ActivityLogSearchText))
            parts.Add(string.Format(LocalizationService.T("ops.page.filterKeyword"), ActivityLogSearchText.Trim()));

        ActivityLogFilterSummary = string.Format(
            LocalizationService.T("ops.page.filterSummary"),
            string.Join(" · ", parts));
    }

    public void RefreshShortcutHints() => RefreshActivityLogBatchShortcutHint();

    private void EnsureDefaultActivityLogSinceDays()
    {
        if (_activityLogSinceDaysDefaulted || !string.IsNullOrWhiteSpace(ActivityLogSinceDays))
            return;

        var keepDays = _app.StudioConfig.Load().ActivityLogKeepDays;
        if (keepDays > 0)
            ActivityLogSinceDays = keepDays.ToString();

        _activityLogSinceDaysDefaulted = true;
    }

    public void SyncActivityLogSinceDaysFromStudio(int? previousKeepDays = null)
    {
        var keepDays = _app.StudioConfig.Load().ActivityLogKeepDays;
        if (keepDays <= 0)
            return;

        if (string.IsNullOrWhiteSpace(ActivityLogSinceDays)
            || (previousKeepDays is > 0
                && string.Equals(ActivityLogSinceDays.Trim(), previousKeepDays.Value.ToString(), StringComparison.Ordinal)))
        {
            ActivityLogSinceDays = keepDays.ToString();
        }
    }

    private void RefreshActivityLogBatchShortcutHint()
    {
        var prefs = _app.UiPreferences.Load();
        var all = GlobalShortcutBindingService.GetDisplayKeys(prefs, GlobalShortcutRegistry.OpsExportBatchStatsAll);
        var json = GlobalShortcutBindingService.GetDisplayKeys(prefs, GlobalShortcutRegistry.OpsExportBatchStatsJson);
        var csv = GlobalShortcutBindingService.GetDisplayKeys(prefs, GlobalShortcutRegistry.OpsExportBatchStatsCsv);
        var filter = GlobalShortcutBindingService.GetDisplayKeys(prefs, GlobalShortcutRegistry.OpsFilterBatchActivity);
        ActivityLogBatchShortcutHint = string.Format(
            LocalizationService.T("ops.page.activityLogShortcut"),
            all,
            json,
            csv,
            filter);
    }

    private void RefreshDuplicatesOnly()
    {
        var duplicates = _app.JobRunner.ScanDuplicateJobs();
        DuplicateScanSummary = duplicates.SummaryText;
        var mergeSuggestions = _app.JobRunner.GetDuplicateMergeSuggestions();
        DuplicateMergeSuggestionSummary = mergeSuggestions.SummaryText;
        DuplicateMergeSuggestions.Clear();
        foreach (var suggestion in mergeSuggestions.Suggestions)
        {
            DuplicateMergeSuggestions.Add(new DuplicateMergeSuggestionItem
            {
                TargetJobId = suggestion.TargetJobId,
                Summary = suggestion.SummaryText,
                Reason = suggestion.Reason
            });
        }

        DuplicateGroups.Clear();
        foreach (var group in duplicates.Groups)
        {
            DuplicateGroups.Add(new DuplicateGroupItem
            {
                Reason = group.Reason,
                Key = group.Key,
                Summary = string.Format(
                    LocalizationService.T("ops.page.duplicateGroupSummary"),
                    group.Reason,
                    group.Key,
                    group.Jobs.Count),
                Jobs = group.Jobs
            });
        }

        DuplicateGroupCount = DuplicateGroups.Count;
        OnPropertyChanged(nameof(DuplicateGroupsCountText));
        OnPropertyChanged(nameof(DuplicateMergeSuggestionsCountText));
        ApplyDuplicateGroupsEmptyHint();
        RefreshBatchMergePreviewSilently();
    }

    private void RefreshActivityLogOnly(bool reset = true)
    {
        if (reset)
            ActivityLogOffset = 0;

        var categoryFilter = string.IsNullOrWhiteSpace(ActivityCategoryFilter) ? null : ActivityCategoryFilter;
        var searchQuery = string.IsNullOrWhiteSpace(ActivityLogSearchText) ? null : ActivityLogSearchText;
        var sinceDays = ResolveActivityLogSinceDays();
        var page = _app.JobRunner.SearchActivityLogPage(
            searchQuery,
            offset: ActivityLogOffset,
            category: categoryFilter,
            sinceDays: sinceDays);

        if (reset)
            RecentActivity.Clear();

        foreach (var entry in page.Entries)
            RecentActivity.Add(entry);

        ActivityLogHasMore = page.HasMore;
        ActivityLogPageSummary = page.SummaryText;
        ActivityLogMatchCount = page.TotalMatched;
        ApplyActivityLogResultBadge();
        ActivityLogEmptyHint = page.TotalMatched == 0
            ? BuildActivityLogEmptyHint(categoryFilter, searchQuery)
            : string.Empty;
        NotifyActivityLogLoadedSummary();
    }

    private void NotifyActivityLogLoadedSummary()
        => OnPropertyChanged(nameof(ActivityLogLoadedSummary));

    private void RefreshActivityLogStats()
    {
        var sinceDays = ResolveActivityLogSinceDays();
        var categoryFilter = string.IsNullOrWhiteSpace(ActivityCategoryFilter) ? null : ActivityCategoryFilter;
        var stats = _app.JobRunner.GetActivityLogStats(sinceDays, categoryFilter);
        var barItems = ActivityLogStatsService.BuildBarItems(stats);

        ActivityLogStats.Clear();
        foreach (var item in barItems)
        {
            ActivityLogStats.Add(new ActivityLogStatItem
            {
                Category = item.Category,
                Count = item.Count,
                BarPercent = item.BarPercent
            });
        }

        ActivityLogStatsSummary = stats.SummaryText;
        UpdateActivityLogStatsEmptyHint();
    }

    private void RefreshActivityLogDailyStats()
    {
        const int days = 7;
        var categoryFilter = string.IsNullOrWhiteSpace(ActivityCategoryFilter) ? null : ActivityCategoryFilter;
        var daily = _app.JobRunner.GetActivityLogDailyStats(days, categoryFilter);
        var barItems = BuildBarItems(daily.Items.Select(item => (item.DateLabel, item.Count)));

        ActivityLogDailyStats.Clear();
        foreach (var item in barItems)
        {
            ActivityLogDailyStats.Add(new ActivityLogDailyItem
            {
                Date = item.Label,
                Count = item.Count,
                BarPercent = item.BarPercent
            });
        }

        ActivityLogDailyStatsSummary = daily.SummaryText;
    }

    private static List<(string Label, int Count, double BarPercent)> BuildBarItems(
        IEnumerable<(string Label, int Count)> items)
    {
        var list = items.ToList();
        var maxCount = list.Count == 0 ? 0 : list.Max(item => item.Count);
        return list
            .Select(item => (
                item.Label,
                item.Count,
                BarPercent: maxCount == 0 ? 0 : item.Count * 100.0 / maxCount))
            .ToList();
    }

    [RelayCommand(CanExecute = nameof(CanLoadMoreActivityLog))]
    private async Task LoadMoreActivityLogAsync()
    {
        if (IsLoadingMoreActivityLog || !ActivityLogHasMore)
            return;

        IsLoadingMoreActivityLog = true;
        try
        {
            await Task.Yield();
            ActivityLogOffset = RecentActivity.Count;
            RefreshActivityLogOnly(reset: false);
        }
        finally
        {
            IsLoadingMoreActivityLog = false;
        }
    }

    private bool CanLoadMoreActivityLog() => ActivityLogHasMore && !IsLoadingMoreActivityLog;

    private void RefreshBatchMergePreviewSilently()
    {
        var preview = _app.JobRunner.PreviewBatchMergeDuplicates();
        ApplyBatchMergePreview(preview);
    }

    private void ApplyBatchMergePreview(BatchDuplicateMergePreviewResult preview)
    {
        BatchMergePreviewItems.Clear();
        foreach (var item in preview.Items)
        {
            BatchMergePreviewItems.Add(new BatchMergePreviewItem
            {
                TargetJobId = item.TargetJobId,
                TargetTitle = item.TargetTitle,
                Reason = item.Reason,
                SourceCount = item.SourceJobIds.Count,
                Summary = $"保留「{item.TargetTitle}」，合并 {item.SourceJobIds.Count} 个重复项（{item.Reason}）"
            });
        }

        ApplyMergePreviewEmptyHint();
    }

    private void UpdateActivityLogStatsEmptyHint()
    {
        ActivityLogStatsEmptyHint = ActivityLogStats.Count == 0
            ? BuildActivityLogStatsEmptyHint()
            : string.Empty;
    }

    private string BuildActivityLogStatsEmptyHint()
        => $"当前时间范围内暂无分类统计（{ResolveActivityLogSinceDaysHint()}）。可放宽天数条件，或执行任务操作后刷新。";

    private string BuildActivityLogEmptyHint(string? categoryFilter, string? searchQuery)
    {
        var parts = new List<string>();
        parts.Add(string.IsNullOrWhiteSpace(categoryFilter) ? "全部分类" : $"分类「{categoryFilter}」");
        parts.Add(ResolveActivityLogSinceDaysHint());
        if (!string.IsNullOrWhiteSpace(searchQuery))
            parts.Add($"关键词「{searchQuery.Trim()}」");

        return $"暂无符合条件的活动记录（{string.Join("，", parts)}）。可放宽筛选条件，或执行任务操作后刷新。";
    }

    private int? ResolveActivityLogSinceDays()
    {
        if (string.IsNullOrWhiteSpace(ActivityLogSinceDays))
            return _app.JobRunner.GetActivityLogKeepDays();

        return int.TryParse(ActivityLogSinceDays.Trim(), out var days) && days > 0
            ? days
            : null;
    }

    private string ResolveActivityLogSinceDaysHint()
    {
        if (string.IsNullOrWhiteSpace(ActivityLogSinceDays))
            return $"默认保留 {_app.JobRunner.GetActivityLogKeepDays()} 天";

        return int.TryParse(ActivityLogSinceDays.Trim(), out var days) && days > 0
            ? $"最近 {days} 天"
            : "不限天数";
    }

    partial void OnActivityCategoryFilterChanged(string value)
    {
        RefreshActivityLogOnly(reset: true);
        RefreshActivityLogDailyStats();
        NotifyActivityLogFilterStateChanged();
    }

    [RelayCommand]
    private void SelectRecentCategory(string? category)
        => ActivityCategoryFilter = category ?? string.Empty;

    [RelayCommand]
    private void FilterBatchActivityLogs()
    {
        ActivityLogSearchText = ActivityLogBatchFilterService.BatchSearchKeyword;
        ActivityCategoryFilter = string.Empty;
        RefreshActivityLogOnly(reset: true);
    }

    [RelayCommand]
    private void FilterBatchCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return;

        ActivityCategoryFilter = category;
        ActivityLogSearchText = ActivityLogBatchFilterService.BatchSearchKeyword;
        RefreshActivityLogOnly(reset: true);
    }

    [RelayCommand]
    private void FilterBatchCategoryFromStat(ActivityLogBatchStatItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Category))
            return;

        FilterBatchCategory(item.Category);
    }

    partial void OnActivityLogSinceDaysChanged(string value)
    {
        RefreshActivityLogOnly(reset: true);
        RefreshActivityLogStats();
        RefreshActivityLogBatchSummary();
        NotifyActivityLogFilterStateChanged();
        ApplyActivityLogSinceDaysHint();
    }

    private void RefreshActivityLogBatchSummary()
    {
        var sinceDays = ResolveActivityLogSinceDays();
        var summary = _app.JobRunner.GetActivityLogBatchSummary(sinceDays);
        ActivityLogBatchSummary = summary.TotalCount == 0
            ? string.Empty
            : summary.SummaryText;

        var maxCount = summary.Items.Count == 0 ? 0 : summary.Items.Max(item => item.Count);
        ActivityLogBatchStats.Clear();
        foreach (var item in summary.Items)
        {
            ActivityLogBatchStats.Add(new ActivityLogBatchStatItem
            {
                Category = item.Category,
                Label = item.Label,
                Count = item.Count,
                BarPercent = maxCount == 0 ? 0 : item.Count * 100.0 / maxCount
            });
        }
    }

    [RelayCommand]
    private void ExportActivityLogBatchStats()
    {
        var sinceDays = ResolveActivityLogSinceDays();
        var export = _app.JobRunner.ExportActivityLogBatchStats(sinceDays: sinceDays);
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("ops.msg.title.exportBatchStats"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void ExportActivityLogBatchStatsCsv()
    {
        var sinceDays = ResolveActivityLogSinceDays();
        var export = _app.JobRunner.ExportActivityLogBatchStatsCsv(sinceDays: sinceDays);
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("ops.msg.title.exportBatchStatsCsv"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void ExportActivityLogBatchStatsAll()
    {
        var sinceDays = ResolveActivityLogSinceDays();
        var export = _app.JobRunner.ExportActivityLogBatchStatsAll(sinceDays: sinceDays);
        MessageRequested?.Invoke(
            Msg("ops.msg.exportBatchStatsAllFormat", export.SummaryText, export.JsonExportPath, export.CsvExportPath),
            Title("ops.msg.title.exportBatchStats"));
        OpenFolder(Path.GetDirectoryName(export.JsonExportPath));
    }

    partial void OnActivityLogSearchTextChanged(string value)
    {
        NotifyActivityLogFilterStateChanged();
        IsActivityLogSearchPending = !string.IsNullOrWhiteSpace(value);
        _ = DebounceRefreshActivityLogAsync();
    }

    private async Task DebounceRefreshActivityLogAsync()
    {
        _activityLogSearchDebounceCts?.Cancel();
        _activityLogSearchDebounceCts?.Dispose();
        _activityLogSearchDebounceCts = new CancellationTokenSource();
        var token = _activityLogSearchDebounceCts.Token;

        try
        {
            await Task.Delay(ActivityLogSearchDebounceMs, token);
            _ui.Run(() =>
            {
                IsActivityLogSearchPending = false;
                RefreshActivityLogOnly(reset: true);
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    private void GoToStaleJob(StaleJobItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.JobId))
            return;

        NavigateToJobRequested?.Invoke(item.JobId);
    }

    [RelayCommand(CanExecute = nameof(CanBulkArchive))]
    private async Task BulkArchivePublishedAsync()
    {
        if (PublishedArchivableCount == 0)
        {
            MessageRequested?.Invoke(Msg("ops.msg.noBulkArchiveJobs"), Title("ops.msg.title.bulkArchive"));
            return;
        }

        if (ConfirmBulkArchivePublishedRequested is not null)
        {
            var confirmed = await ConfirmBulkArchivePublishedRequested(PublishedArchivableCount);
            if (!confirmed)
                return;
        }

        IsBusy = true;
        try
        {
            var result = _app.JobRunner.BulkArchivePublishedJobs();
            _ui.Run(() =>
            {
                RefreshDuplicatesOnly();
                var message = $"批量归档完成：成功 {result.Archived}，跳过 {result.Skipped}";
                if (result.Messages.Count > 0)
                    message += Environment.NewLine + string.Join(Environment.NewLine, result.Messages.Take(5));

                MessageRequested?.Invoke(message, Title("ops.msg.title.bulkArchivePublished"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("ops.msg.title.bulkArchiveFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    private bool CanBulkArchive() => !IsBusy && PublishedArchivableCount > 0;

    [RelayCommand]
    private void ExportDailyReport()
    {
        var path = _app.JobRunner.ExportDailyReport();
        MessageRequested?.Invoke(Msg("ops.msg.dailyReportExportedFormat", path), Title("common.msg.title.exportDaily"));
        OpenFolder(Path.GetDirectoryName(path));
    }

    [RelayCommand]
    private void ExportAllJobsBackup()
    {
        var path = _app.JobRunner.ExportAllJobsBackup();
        MessageRequested?.Invoke(Msg("ops.msg.jobsBackupExportedFormat", path), Title("common.msg.title.backupAllJobs"));
        OpenFolder(Path.GetDirectoryName(path));
    }

    [RelayCommand]
    private void ExportFullBackup()
    {
        var export = _app.JobRunner.ExportAllBackup();
        var message = Msg("ops.msg.fullBackupExportedFormat", export.JobCount, export.ExportPath);
        MessageRequested?.Invoke(message, Title("common.msg.title.fullBackup"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private async Task ImportFullBackupAsync()
    {
        if (PickBackupFileRequested is null)
        {
            MessageRequested?.Invoke(Msg("common.msg.title.pickerNotReady"), Title("common.msg.title.restoreFullBackup"));
            return;
        }

        var path = await PickBackupFileRequested.Invoke();
        if (string.IsNullOrWhiteSpace(path))
            return;

        var merge = true;
        if (ConfirmImportFullBackupRequested is not null)
        {
            var choice = await ConfirmImportFullBackupRequested.Invoke();
            if (choice is null)
                return;

            merge = choice.Value;
        }

        try
        {
            var result = _app.JobRunner.ImportAllBackup(path, merge);
            RefreshCore();
            var message = result.Message;
            if (result.ImportedJobs > 0 || result.ReplacedJobs > 0 || result.SkippedJobs > 0)
                message += $"\n任务: 导入 {result.ImportedJobs}，替换 {result.ReplacedJobs}，跳过 {result.SkippedJobs}";

            MessageRequested?.Invoke(
                message,
                result.Success ? Title("common.msg.title.restoreFullBackup") : Title("common.msg.title.restoreFullBackupFailed"));
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(ex.Message, Title("common.msg.title.restoreFullBackupFailed"));
        }
    }

    [RelayCommand]
    private void ExportOperationsReport()
    {
        var snapshot = _app.JobRunner.GetOperationsCenterSnapshot();
        var path = _app.JobRunner.ExportOperationsCenterReport();
        var lines = OperationsCenterService.FormatSectionLines(snapshot);
        var sectionHint = lines.Count == 0
            ? string.Empty
            : $"\n{string.Join("\n", lines)}";
        MessageRequested?.Invoke(Msg("ops.msg.opsReportExportedFormat", path, snapshot.SummaryText, sectionHint), Title("ops.msg.title.exportOpsReport"));
        OpenFolder(Path.GetDirectoryName(path));
    }

    [RelayCommand]
    private void ExportNightlyAutomationScript()
    {
        var export = _app.JobRunner.ExportNightlyAutomationScript();
        MessageRequested?.Invoke(
            Msg("ops.msg.nightlyScriptExportedFormat", export.SummaryText, export.BatPath, export.ReadmeText),
            Title("common.msg.title.exportNightlyScript"));
        OpenFolder(Path.GetDirectoryName(export.BatPath));
    }

    [RelayCommand(CanExecute = nameof(CanBatchMergeDuplicates))]
    private async Task BatchMergeDuplicatesAsync()
    {
        var suggestions = _app.JobRunner.GetDuplicateMergeSuggestions();
        if (suggestions.MergeActionCount == 0)
        {
            MessageRequested?.Invoke(Msg("ops.msg.noAutoMergeDuplicates"), Title("ops.msg.title.batchMergeDuplicates"));
            return;
        }

        if (ConfirmBatchMergeDuplicatesRequested is not null)
        {
            var confirmed = await ConfirmBatchMergeDuplicatesRequested(
                suggestions.GroupCount,
                suggestions.MergeActionCount);
            if (!confirmed)
                return;
        }

        IsBusy = true;
        try
        {
            var result = _app.JobRunner.BatchMergeDuplicateJobs();
            _ui.Run(() =>
            {
                RefreshDuplicatesOnly();
                var message = result.SummaryText;
                if (result.Messages.Count > 0)
                    message += Environment.NewLine + string.Join(Environment.NewLine, result.Messages.Take(8));

                MessageRequested?.Invoke(message, Title("ops.msg.title.batchMergeDuplicates"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("ops.msg.title.batchMergeDuplicatesFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    private bool CanBatchMergeDuplicates() => !IsBusy;

    [RelayCommand]
    private async Task TrimActivityLogAsync()
    {
        var keepDays = _app.JobRunner.GetActivityLogKeepDays();
        var category = string.IsNullOrWhiteSpace(ActivityCategoryFilter) ? null : ActivityCategoryFilter;
        var sinceDays = ResolveActivityLogSinceDays();
        var preview = _app.JobRunner.PreviewActivityLogTrim(keepDays, category, sinceDays);
        if (preview.WouldRemoveCount == 0)
        {
            MessageRequested?.Invoke(preview.SummaryText, Title("ops.msg.title.trimActivityLog"));
            return;
        }

        if (ConfirmTrimActivityLogRequested is not null)
        {
            var confirmed = await ConfirmTrimActivityLogRequested(preview);
            if (!confirmed)
                return;
        }

        var result = _app.JobRunner.TrimActivityLog(keepDays, category, sinceDays);
        RefreshCore();
        MessageRequested?.Invoke(result.SummaryText, Title("ops.msg.title.trimActivityLog"));
    }

    [RelayCommand]
    private async Task ArchiveActivityLogAsync()
    {
        var keepDays = _app.JobRunner.GetActivityLogKeepDays();
        var category = string.IsNullOrWhiteSpace(ActivityCategoryFilter) ? null : ActivityCategoryFilter;
        var sinceDays = ResolveActivityLogSinceDays();
        var preview = _app.JobRunner.PreviewActivityLogArchive(keepDays, category, sinceDays);
        if (preview.WouldRemoveCount == 0)
        {
            MessageRequested?.Invoke(preview.SummaryText, Title("ops.msg.title.archiveActivityLog"));
            return;
        }

        if (ConfirmArchiveActivityLogRequested is not null)
        {
            var confirmed = await ConfirmArchiveActivityLogRequested(preview);
            if (!confirmed)
                return;
        }

        var result = _app.JobRunner.ArchiveActivityLog(keepDays, category, sinceDays);
        RefreshActivityLogOnly(reset: true);
        RefreshActivityLogStats();
        MessageRequested?.Invoke(result.SummaryText, Title("ops.msg.title.archiveActivityLog"));
    }

    [RelayCommand]
    private void ExportDuplicateReport()
    {
        var export = _app.JobRunner.ExportDuplicateReport();
        MessageRequested?.Invoke(
            Msg("ops.msg.duplicateReportExportedFormat", export.GroupCount, export.DuplicateJobCount, export.ExportPath),
            Title("ops.msg.title.exportDuplicateReport"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void ExportDuplicateMergeSuggestions()
    {
        var export = _app.JobRunner.ExportDuplicateMergeSuggestions();
        MessageRequested?.Invoke(
            Msg("ops.msg.mergeSuggestionsExportedFormat", export.GroupCount, export.MergeActionCount, export.ExportPath),
            Title("ops.msg.title.exportMergeSuggestions"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void ExportActivityLog()
    {
        var category = string.IsNullOrWhiteSpace(ActivityCategoryFilter) ? null : ActivityCategoryFilter;
        var sinceDays = ResolveActivityLogSinceDays();
        var export = _app.JobRunner.ExportActivityLog(category: category, sinceDays: sinceDays);
        MessageRequested?.Invoke(
            Msg("ops.msg.activityLogExportedFormat", export.EntryCount, export.ExportPath),
            Title("ops.msg.title.exportActivityLog"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void ExportActivityLogCsv()
    {
        var category = string.IsNullOrWhiteSpace(ActivityCategoryFilter) ? null : ActivityCategoryFilter;
        var query = string.IsNullOrWhiteSpace(ActivityLogSearchText) ? null : ActivityLogSearchText;
        var sinceDays = ResolveActivityLogSinceDays();
        var export = _app.JobRunner.ExportActivityLogCsv(query: query, category: category, sinceDays: sinceDays);
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("ops.msg.title.exportActivityLogCsv"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void ExportActivityLogStats()
    {
        var sinceDays = ResolveActivityLogSinceDays();
        var export = _app.JobRunner.ExportActivityLogStats(sinceDays: sinceDays);
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("ops.msg.title.exportActivityLogStats"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void ExportActivityLogStatsCsv()
    {
        var sinceDays = ResolveActivityLogSinceDays();
        var export = _app.JobRunner.ExportActivityLogStatsCsv(sinceDays: sinceDays);
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("ops.msg.title.exportActivityLogStatsCsv"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void ExportActivityLogStatsHtml()
    {
        var sinceDays = ResolveActivityLogSinceDays();
        var export = _app.JobRunner.ExportActivityLogStatsHtml(sinceDays: sinceDays);
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("ops.msg.title.exportActivityLogStatsHtml"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void ExportActivityLogDailyStatsCsv()
    {
        var days = ResolveActivityLogSinceDays() ?? 7;
        var export = _app.JobRunner.ExportActivityLogDailyStatsCsv(days: days);
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("ops.msg.title.exportActivityLogDailyStatsCsv"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void PreviewBatchMergeDuplicates()
    {
        var preview = _app.JobRunner.PreviewBatchMergeDuplicates();
        ApplyBatchMergePreview(preview);
        MessageRequested?.Invoke(preview.SummaryText, Title("ops.msg.title.previewBatchMergeDuplicates"));
    }

    [RelayCommand]
    private void ExportBatchMergePreview()
    {
        var export = _app.JobRunner.ExportBatchMergePreview();
        MessageRequested?.Invoke(
            Msg("ops.msg.batchMergePreviewExportedFormat", export.GroupCount, export.MergeActionCount, export.ExportPath),
            Title("ops.msg.title.exportMergePreviewJson"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void ExportMachineProfile()
    {
        var export = _app.JobRunner.ExportMachineProfile();
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("ops.msg.title.exportMachineProfile"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private async Task ImportMachineProfileAsync()
    {
        if (PickBackupFileRequested is null)
        {
            MessageRequested?.Invoke(Msg("common.msg.title.pickerNotReady"), Title("ops.msg.title.importMachineProfile"));
            return;
        }

        var path = await PickBackupFileRequested.Invoke();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var preview = _app.JobRunner.PreviewMachineProfile(path, merge: true);
            if (!preview.Valid)
            {
                MessageRequested?.Invoke(
                    string.IsNullOrWhiteSpace(preview.Message) ? preview.SummaryText : preview.Message,
                    Title("ops.msg.title.importMachineProfileFailed"));
                return;
            }

            if (ConfirmMachineProfileImportRequested is not null)
            {
                var confirmed = await ConfirmMachineProfileImportRequested.Invoke(preview);
                if (confirmed != true)
                    return;
            }

            var previousKeepDays = _app.StudioConfig.Load().ActivityLogKeepDays;
            var result = _app.JobRunner.ImportMachineProfile(path, merge: true);
            if (result.Success)
            {
                SyncActivityLogSinceDaysFromStudio(previousKeepDays);
                RefreshShortcutHints();
            }

            RefreshCore();
            MessageRequested?.Invoke(result.Message, result.Success ? Title("ops.msg.title.importMachineProfile") : Title("ops.msg.title.importMachineProfileFailed"));
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(ex.Message, Title("ops.msg.title.importMachineProfileFailed"));
        }
    }


    private static string Msg(string key) => LocalizationService.T(key);

    private static string Title(string key) => LocalizationService.T(key);

    private static string Msg(string key, params object[] args) => string.Format(LocalizationService.T(key), args);
    private static void OpenFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true
        });
    }

    private static StaleJobItem CreateStaleJobItem(string jobId, string title, string reason, int days)
        => new()
        {
            JobId = jobId,
            Title = title,
            Reason = reason,
            Days = days,
            DisplayText = string.Format(LocalizationService.T("ops.page.staleDaysFormat"), title, reason, days),
            DaysNotUpdatedText = string.Format(LocalizationService.T("ops.page.staleDaysNotUpdated"), days)
        };

    private void ApplyBulkArchiveHint()
    {
        BulkArchiveHint = PublishedArchivableCount > 0
            ? string.Format(LocalizationService.T("ops.page.bulkArchiveHint"), PublishedArchivableCount)
            : LocalizationService.T("ops.page.bulkArchiveNone");
        OnPropertyChanged(nameof(HasBulkArchiveCandidates));
        OnPropertyChanged(nameof(BulkArchiveButtonText));
    }

    private void ApplyStaleJobsEmptyHint()
    {
        StaleJobsEmptyHint = StaleJobCount == 0
            ? LocalizationService.T("ops.page.staleJobsEmpty")
            : string.Empty;
    }

    private void ApplyMergePreviewEmptyHint()
    {
        MergePreviewEmptyHint = BatchMergePreviewItems.Count == 0
            ? LocalizationService.T("ops.page.mergePreviewEmpty")
            : string.Empty;
    }

    private void ApplyDuplicateGroupsEmptyHint()
    {
        DuplicateGroupsEmptyHint = DuplicateGroupCount == 0
            ? LocalizationService.T("ops.page.duplicateGroupsEmpty")
            : string.Empty;
    }

    private void ApplyActivityLogResultBadge()
    {
        ActivityLogResultBadge = ActivityLogMatchCount > 0
            ? string.Format(LocalizationService.T("ops.page.activityLogMatchBadge"), ActivityLogMatchCount)
            : string.Empty;
    }

    private void ApplyActivityLogSinceDaysHint()
    {
        ActivityLogSinceDaysHint = string.Format(
            LocalizationService.T("ops.page.activityLogSinceDaysHint"),
            ResolveActivityLogSinceDaysHint());
    }

    private void RefreshStaleJobLocalizedTexts()
    {
        if (StaleJobs.Count == 0)
            return;

        var items = StaleJobs
            .Select(item => CreateStaleJobItem(item.JobId, item.Title, item.Reason, item.Days))
            .ToList();
        StaleJobs.Clear();
        foreach (var item in items)
            StaleJobs.Add(item);
    }

    private void RefreshDuplicateGroupLocalizedTexts()
    {
        if (DuplicateGroups.Count == 0)
            return;

        var items = DuplicateGroups
            .Select(group => new DuplicateGroupItem
            {
                Reason = group.Reason,
                Key = group.Key,
                Summary = string.Format(
                    LocalizationService.T("ops.page.duplicateGroupSummary"),
                    group.Reason,
                    group.Key,
                    group.Jobs.Count),
                Jobs = group.Jobs
            })
            .ToList();

        DuplicateGroups.Clear();
        foreach (var item in items)
            DuplicateGroups.Add(item);
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(OpsTitle));
        OnPropertyChanged(nameof(OpsSimpleHint));
        OnPropertyChanged(nameof(OpsProHint));
        OnPropertyChanged(nameof(OpsRefreshLabel));
        OnPropertyChanged(nameof(OpsRefreshingHint));
        OnPropertyChanged(nameof(OpsTaskOverviewTitle));
        OnPropertyChanged(nameof(OpsSectionsTitle));
        OnPropertyChanged(nameof(OpsFullSummaryHeader));
        OnPropertyChanged(nameof(OpsMaintenanceCheckTitle));
        OnPropertyChanged(nameof(OpsJobHealthTitle));
        OnPropertyChanged(nameof(OpsStaleJobsTitle));
        OnPropertyChanged(nameof(OpsStaleJobsHint));
        OnPropertyChanged(nameof(StaleJobsCountText));
        OnPropertyChanged(nameof(StaleJobsLoadingHint));
        OnPropertyChanged(nameof(DuplicateGroupsCountText));
        OnPropertyChanged(nameof(DuplicateMergeSuggestionsCountText));
        OnPropertyChanged(nameof(OpsDuplicateScanTitle));
        OnPropertyChanged(nameof(OpsAutoMergeDuplicatesLabel));
        OnPropertyChanged(nameof(OpsPreviewMergeLabel));
        OnPropertyChanged(nameof(OpsExportMergePreviewJsonLabel));
        OnPropertyChanged(nameof(OpsExportDuplicateReportLabel));
        OnPropertyChanged(nameof(OpsExportMergeSuggestionsLabel));
        OnPropertyChanged(nameof(OpsActivityLogAndStatsHeader));
        OnPropertyChanged(nameof(OpsActivityLogTitle));
        OnPropertyChanged(nameof(OpsActivityLogHint));
        OnPropertyChanged(nameof(OpsRecentActiveCategoriesLabel));
        OnPropertyChanged(nameof(OpsCategoryHeader));
        OnPropertyChanged(nameof(OpsKeywordHeader));
        OnPropertyChanged(nameof(OpsDaysHeader));
        OnPropertyChanged(nameof(OpsSearchActivityLogPlaceholder));
        OnPropertyChanged(nameof(OpsDaysPlaceholder));
        OnPropertyChanged(nameof(OpsClearFiltersLabel));
        OnPropertyChanged(nameof(OpsBatchFilterShortcutLabel));
        OnPropertyChanged(nameof(OpsFilterAllBatchLabel));
        OnPropertyChanged(nameof(OpsFilterBatchArchiveLabel));
        OnPropertyChanged(nameof(OpsFilterBatchDeleteLabel));
        OnPropertyChanged(nameof(OpsFilterBatchTagsLabel));
        OnPropertyChanged(nameof(OpsFilterBatchUnarchiveLabel));
        OnPropertyChanged(nameof(OpsExportBatchJsonCsvLabel));
        OnPropertyChanged(nameof(OpsExportBatchJsonOnlyLabel));
        OnPropertyChanged(nameof(OpsExportBatchCsvOnlyLabel));
        OnPropertyChanged(nameof(OpsCategoryStatsTitle));
        OnPropertyChanged(nameof(OpsExportStatsJsonLabel));
        OnPropertyChanged(nameof(OpsExportStatsCsvLabel));
        OnPropertyChanged(nameof(OpsExportStatsHtmlLabel));
        OnPropertyChanged(nameof(OpsExportDailyStatsCsvLabel));
        OnPropertyChanged(nameof(OpsLast7DaysActivityTitle));
        OnPropertyChanged(nameof(OpsLoadMoreLabel));
        OnPropertyChanged(nameof(ActivityLogSearchPendingHint));
        OnPropertyChanged(nameof(ActivityLogLoadingMoreHint));
        NotifyActivityLogLoadedSummary();
        OnPropertyChanged(nameof(OpsCommonOpsHeader));
        OnPropertyChanged(nameof(OpsBulkArchivePublishedLabel));
        OnPropertyChanged(nameof(OpsExportDailyLabel));
        OnPropertyChanged(nameof(OpsExportOpsReportLabel));
        OnPropertyChanged(nameof(OpsExportNightlyScriptLabel));
        OnPropertyChanged(nameof(OpsBackupRestoreHeader));
        OnPropertyChanged(nameof(OpsBackupAllJobsLabel));
        OnPropertyChanged(nameof(OpsBackupFullLabel));
        OnPropertyChanged(nameof(OpsRestoreFullBackupLabel));
        OnPropertyChanged(nameof(OpsActivityLogMaintenanceHeader));
        OnPropertyChanged(nameof(OpsExportActivityLogLabel));
        OnPropertyChanged(nameof(OpsExportCsvLabel));
        OnPropertyChanged(nameof(OpsTrimActivityLogLabel));
        OnPropertyChanged(nameof(OpsArchiveActivityLogLabel));
        OnPropertyChanged(nameof(OpsExportMachineProfileLabel));
        OnPropertyChanged(nameof(OpsImportMachineProfileLabel));
        ApplyBulkArchiveHint();
        ApplyStaleJobsEmptyHint();
        RefreshStaleJobLocalizedTexts();
        NotifyActivityLogFilterStateChanged();
        ApplyActivityLogSinceDaysHint();
        RefreshActivityLogBatchShortcutHint();
        ApplyMergePreviewEmptyHint();
        ApplyDuplicateGroupsEmptyHint();
        ApplyActivityLogResultBadge();
        RefreshDuplicateGroupLocalizedTexts();
    }

    public event Action<string, string>? MessageRequested;
    public event Action<string>? NavigateToJobRequested;
    public event Func<int, Task<bool>>? ConfirmBulkArchivePublishedRequested;
    public event Func<int, int, Task<bool>>? ConfirmBatchMergeDuplicatesRequested;
    public event Func<ActivityLogTrimPreviewResult, Task<bool>>? ConfirmTrimActivityLogRequested;
    public event Func<ActivityLogTrimPreviewResult, Task<bool>>? ConfirmArchiveActivityLogRequested;
    public event Func<Task<bool?>>? ConfirmImportFullBackupRequested;
    public event Func<Task<string?>>? PickBackupFileRequested;
    public event Func<MachineProfilePreviewResult, Task<bool?>>? ConfirmMachineProfileImportRequested;
}