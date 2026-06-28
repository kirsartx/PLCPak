using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLCPak.Core.Models;
using PLCPak.Core.Services;
using PLCPak.WinUI.Infrastructure;
using Windows.ApplicationModel.DataTransfer;

namespace PLCPak.WinUI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly PlcPakAppContext _app;
    private readonly UiDispatcher _ui;
    private readonly UiModeNotifier _uiMode;
    private readonly List<PublishJob> _allJobs = [];

    private PublishStats _lastStats = new();

    public DashboardViewModel(PlcPakAppContext app)
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

    public ObservableCollection<DashboardMetricCard> MetricCards { get; } = [];
    public ObservableCollection<RecentActivityEntry> RecentJobs { get; } = [];
    public ObservableCollection<PublishQueueEntry> PublishQueue { get; } = [];
    public ObservableCollection<TgPendingEntry> TgPendingEntries { get; } = [];
    public ObservableCollection<JobHealthIssue> HealthIssues { get; } = [];

    [ObservableProperty] private string _tgPendingSummary = string.Empty;
    [ObservableProperty] private string _tgPendingEmptyHint = string.Empty;
    [ObservableProperty] private string _publishQueueEmptyHint = string.Empty;

    [ObservableProperty] private string _statsSummaryText = string.Empty;
    [ObservableProperty] private string _publishQueueSummaryText = string.Empty;
    [ObservableProperty] private string _workspaceHealthText = string.Empty;
    [ObservableProperty] private string _jobHealthText = string.Empty;
    [ObservableProperty] private string _workspacePath = string.Empty;
    [ObservableProperty] private string _nextActionSummaryText = string.Empty;
    [ObservableProperty] private string _nextActionLabel = string.Empty;
    [ObservableProperty] private string _nextActionReason = string.Empty;
    [ObservableProperty] private string _scheduleCommandText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNextAction))]
    [NotifyCanExecuteChangedFor(nameof(GoToNextActionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteNextActionCommand))]
    private string _nextActionJobId = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteNextActionCommand))]
    [NotifyCanExecuteChangedFor(nameof(RegisterScheduleTaskCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyScheduleCommandCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchSendTgPendingCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BatchSendTgPendingCommand))]
    private int _tgPendingCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isRefreshing;

    public bool HasNextAction => !string.IsNullOrWhiteSpace(NextActionJobId);
    public bool IsProfessionalMode => _uiMode.IsProfessionalMode;
    public bool IsSimpleMode => _uiMode.IsSimpleMode;

    public string DashboardTitle => LocalizationService.T("dashboard.title");
    public string DashboardSimpleHint => LocalizationService.T("dashboard.simpleHint");
    public string DashboardProHint => LocalizationService.T("dashboard.proHint");
    public string SmartNextTitle => LocalizationService.T("dashboard.smartNext");
    public string GoLabel => LocalizationService.T("dashboard.go");
    public string RunLabel => LocalizationService.T("dashboard.run");
    public string StatsTitle => LocalizationService.T("dashboard.stats");
    public string ViewFailedLabel => LocalizationService.T("dashboard.viewFailed");
    public string ViewPendingLabel => LocalizationService.T("dashboard.viewPending");
    public string ViewArchivedLabel => LocalizationService.T("dashboard.viewArchived");
    public string WorkspaceHealthTitle => LocalizationService.T("dashboard.workspaceHealth");
    public string JobHealthTitle => LocalizationService.T("dashboard.jobHealth");
    public string TgPendingTitle => LocalizationService.T("dashboard.tgPending");
    public string PublishQueueTitle => LocalizationService.T("dashboard.publishQueue");
    public string RecentJobsTitle => LocalizationService.T("dashboard.recentJobs");
    public bool IsRecentJobsEmpty => RecentJobs.Count == 0;
    public bool IsHealthIssuesEmpty => HealthIssues.Count == 0;
    public string RecentJobsEmptyHint => LocalizationService.T("dashboard.emptyRecentJobs");
    public string HealthIssuesEmptyHint => LocalizationService.T("dashboard.emptyHealthIssues");
    public string RefreshLabel => LocalizationService.T("dashboard.refresh");
    public string RefreshingHint => LocalizationService.T("dashboard.refreshing");
    public string MetricsLoadingHint => LocalizationService.T("dashboard.metricsLoading");
    public string StatsLoadingHint => LocalizationService.T("dashboard.statsLoading");
    public string QueuesLoadingHint => LocalizationService.T("dashboard.queuesLoading");
    public string RecentJobsLoadingHint => LocalizationService.T("dashboard.recentJobsLoading");
    public string HealthLoadingHint => LocalizationService.T("dashboard.healthLoading");
    public string SmartNextLoadingHint => LocalizationService.T("dashboard.smartNextLoading");
    public string RecentJobsCountText => RecentJobs.Count > 0
        ? string.Format(LocalizationService.T("dashboard.recentJobsCount"), RecentJobs.Count)
        : string.Empty;
    public string OpenWorkspaceLabel => LocalizationService.T("dashboard.openWorkspace");
    public string PreviewTgLabel => LocalizationService.T("dashboard.previewTg");
    public string BatchTgLabel => LocalizationService.T("dashboard.batchTg");
    public string SendLabel => LocalizationService.T("dashboard.send");
    public string RefreshQueueLabel => LocalizationService.T("dashboard.refreshQueue");
    public string RegisterScheduleLabel => LocalizationService.T("dashboard.registerSchedule");
    public string CopyScheduleLabel => LocalizationService.T("dashboard.copySchedule");
    public string ExportScheduleLabel => LocalizationService.T("dashboard.exportSchedule");
    public string BatchGenCopyLabel => LocalizationService.T("dashboard.batchGenCopy");
    public string ExportQueueCsvLabel => LocalizationService.T("dashboard.exportQueueCsv");
    public string BatchAutoRunLabel => LocalizationService.T("dashboard.batchAutoRun");
    public string BatchMarkPublishedLabel => LocalizationService.T("dashboard.batchMarkPublished");
    public string BackupJobsLabel => LocalizationService.T("dashboard.backupJobs");
    public string BackupFullLabel => LocalizationService.T("dashboard.backupFull");
    public string RestoreJobsLabel => LocalizationService.T("dashboard.restoreJobs");
    public string RestoreFullLabel => LocalizationService.T("dashboard.restoreFull");
    public string ExportHealthLabel => LocalizationService.T("dashboard.exportHealth");
    public string ExportDailyLabel => LocalizationService.T("dashboard.exportDaily");
    public string ExportTgCsvLabel => LocalizationService.T("dashboard.exportTgCsv");
    public string ChannelsFilledLabel => LocalizationService.T("dashboard.channelsFilled");
    public string CopyLabelText => LocalizationService.T("dashboard.copyLabel");
    public string GoToJobsLabel => LocalizationService.T("dashboard.goToJobs");

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        RegisterScheduleTaskCommand.NotifyCanExecuteChanged();
        CopyScheduleCommandCommand.NotifyCanExecuteChanged();
        BatchRunAutomatableChainCommand.NotifyCanExecuteChanged();
        BatchSendTgPendingCommand.NotifyCanExecuteChanged();
        PreviewTgPendingCommand.NotifyCanExecuteChanged();
        ExportTgPendingCsvCommand.NotifyCanExecuteChanged();
        SendTgPendingJobCommand.NotifyCanExecuteChanged();
    }

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

    private void RefreshCore()
    {
        _allJobs.Clear();
        _allJobs.AddRange(_app.Jobs.List());
        WorkspacePath = _app.Workspace.GetWorkspaceRoot();
        WorkspaceHealthText = _app.WorkspaceHealth.GetSummaryText();

        var snapshot = _app.JobRunner.GetDashboardSnapshot();
        ApplyDashboardSnapshot(snapshot);

        RecentJobs.Clear();
        foreach (var entry in _allJobs
                     .OrderByDescending(j => j.UpdatedAt)
                     .Take(10)
                     .Select(ToRecentEntry))
        {
            RecentJobs.Add(entry);
        }

        var scheduleExport = _app.JobRunner.ExportScheduleBatchScript();
        ScheduleCommandText = scheduleExport.SchTasksCommand;

        OnPropertyChanged(nameof(IsRecentJobsEmpty));
        OnPropertyChanged(nameof(IsHealthIssuesEmpty));
        OnPropertyChanged(nameof(RecentJobsCountText));
        DashboardRefreshed?.Invoke(_allJobs.Count);
    }

    private void RefreshTgPendingOnly()
    {
        var tgPending = _app.JobRunner.GetTgPendingSnapshot();
        ApplyTgPendingSnapshot(tgPending);
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(DashboardTitle));
        OnPropertyChanged(nameof(DashboardSimpleHint));
        OnPropertyChanged(nameof(DashboardProHint));
        OnPropertyChanged(nameof(SmartNextTitle));
        OnPropertyChanged(nameof(GoLabel));
        OnPropertyChanged(nameof(RunLabel));
        OnPropertyChanged(nameof(StatsTitle));
        OnPropertyChanged(nameof(ViewFailedLabel));
        OnPropertyChanged(nameof(ViewPendingLabel));
        OnPropertyChanged(nameof(ViewArchivedLabel));
        OnPropertyChanged(nameof(WorkspaceHealthTitle));
        OnPropertyChanged(nameof(JobHealthTitle));
        OnPropertyChanged(nameof(TgPendingTitle));
        OnPropertyChanged(nameof(PublishQueueTitle));
        OnPropertyChanged(nameof(RecentJobsTitle));
        OnPropertyChanged(nameof(RecentJobsEmptyHint));
        OnPropertyChanged(nameof(HealthIssuesEmptyHint));
        OnPropertyChanged(nameof(RefreshLabel));
        OnPropertyChanged(nameof(RefreshingHint));
        OnPropertyChanged(nameof(MetricsLoadingHint));
        OnPropertyChanged(nameof(StatsLoadingHint));
        OnPropertyChanged(nameof(QueuesLoadingHint));
        OnPropertyChanged(nameof(RecentJobsLoadingHint));
        OnPropertyChanged(nameof(HealthLoadingHint));
        OnPropertyChanged(nameof(SmartNextLoadingHint));
        OnPropertyChanged(nameof(RecentJobsCountText));
        OnPropertyChanged(nameof(OpenWorkspaceLabel));
        OnPropertyChanged(nameof(PreviewTgLabel));
        OnPropertyChanged(nameof(BatchTgLabel));
        OnPropertyChanged(nameof(SendLabel));
        OnPropertyChanged(nameof(RefreshQueueLabel));
        OnPropertyChanged(nameof(RegisterScheduleLabel));
        OnPropertyChanged(nameof(CopyScheduleLabel));
        OnPropertyChanged(nameof(ExportScheduleLabel));
        OnPropertyChanged(nameof(BatchGenCopyLabel));
        OnPropertyChanged(nameof(ExportQueueCsvLabel));
        OnPropertyChanged(nameof(BatchAutoRunLabel));
        OnPropertyChanged(nameof(BatchMarkPublishedLabel));
        OnPropertyChanged(nameof(BackupJobsLabel));
        OnPropertyChanged(nameof(BackupFullLabel));
        OnPropertyChanged(nameof(RestoreJobsLabel));
        OnPropertyChanged(nameof(RestoreFullLabel));
        OnPropertyChanged(nameof(ExportHealthLabel));
        OnPropertyChanged(nameof(ExportDailyLabel));
        OnPropertyChanged(nameof(ExportTgCsvLabel));
        OnPropertyChanged(nameof(ChannelsFilledLabel));
        OnPropertyChanged(nameof(CopyLabelText));
        OnPropertyChanged(nameof(GoToJobsLabel));
        ApplyPublishQueueEmptyHint();
        ApplyTgPendingEmptyHint();
        RebuildMetricCards(_lastStats, TgPendingCount, HealthIssues.Count);
        RefreshRecentJobLabels();
    }

    private void RefreshRecentJobLabels()
    {
        if (RecentJobs.Count == 0)
            return;

        var entries = RecentJobs
            .Select(entry => _allJobs.FirstOrDefault(j => j.Id == entry.JobId))
            .Where(job => job is not null)
            .Select(job => ToRecentEntry(job!))
            .ToList();

        if (entries.Count == 0)
            return;

        RecentJobs.Clear();
        foreach (var entry in entries)
            RecentJobs.Add(entry);
    }

    private void ApplyDashboardSnapshot(DashboardSnapshot snapshot)
    {
        _lastStats = snapshot.StatsSummary;
        StatsSummaryText = snapshot.StatsSummary.SummaryText;
        RebuildMetricCards(snapshot.StatsSummary, snapshot.TgPending.Count, snapshot.JobHealth.IssueCount);

        JobHealthText = snapshot.JobHealth.SummaryText;
        HealthIssues.Clear();
        foreach (var issue in snapshot.JobHealth.Issues)
            HealthIssues.Add(issue);

        PublishQueueSummaryText = snapshot.PublishQueue.SummaryText;
        PublishQueue.Clear();
        foreach (var entry in snapshot.PublishQueue.Entries)
            PublishQueue.Add(entry);
        ApplyPublishQueueEmptyHint(snapshot.PublishQueue.Entries.Count);

        NextActionSummaryText = snapshot.Workflow.SummaryText;
        NextActionLabel = snapshot.Workflow.Primary?.ActionLabel ?? string.Empty;
        NextActionReason = snapshot.Workflow.Primary?.Reason ?? string.Empty;
        NextActionJobId = snapshot.Workflow.Primary?.JobId ?? string.Empty;

        ApplyTgPendingSnapshot(snapshot.TgPending);
    }

    private void RebuildMetricCards(PublishStats stats, int tgCount, int healthIssues)
    {
        MetricCards.Clear();
        MetricCards.Add(new DashboardMetricCard
        {
            Label = LocalizationService.T("metric.total"),
            Value = stats.Total.ToString(),
            AccentBrushKey = "PlcAccentBrush",
            FilterKey = "jobs",
            Tip = LocalizationService.T("metric.tip.total")
        });
        MetricCards.Add(new DashboardMetricCard
        {
            Label = LocalizationService.T("metric.pending"),
            Value = stats.PendingPublish.ToString(),
            AccentBrushKey = "PlcWarningBrush",
            FilterKey = "pending",
            Tip = LocalizationService.T("metric.tip.pending")
        });
        MetricCards.Add(new DashboardMetricCard
        {
            Label = LocalizationService.T("metric.failed"),
            Value = stats.Failed.ToString(),
            AccentBrushKey = "PlcDangerBrush",
            FilterKey = "failed",
            Tip = LocalizationService.T("metric.tip.failed")
        });
        MetricCards.Add(new DashboardMetricCard
        {
            Label = LocalizationService.T("metric.tg"),
            Value = tgCount.ToString(),
            AccentBrushKey = "PlcSuccessBrush",
            FilterKey = "tg",
            Tip = LocalizationService.T("metric.tip.tg")
        });
        MetricCards.Add(new DashboardMetricCard
        {
            Label = LocalizationService.T("metric.health"),
            Value = healthIssues.ToString(),
            AccentBrushKey = "PlcAccentBrush",
            FilterKey = "health",
            Tip = LocalizationService.T("metric.tip.health")
        });
    }

    private void ApplyPublishQueueEmptyHint(int? entryCount = null)
    {
        var count = entryCount ?? PublishQueue.Count;
        PublishQueueEmptyHint = count == 0
            ? LocalizationService.T("dashboard.emptyPublishQueue")
            : string.Empty;
    }

    private void ApplyTgPendingEmptyHint(int? count = null)
    {
        var pendingCount = count ?? TgPendingCount;
        TgPendingEmptyHint = pendingCount == 0
            ? LocalizationService.T("dashboard.emptyTgPending")
            : string.Empty;
    }

    private void ApplyTgPendingSnapshot(TgPendingSnapshot tgPending)
    {
        TgPendingSummary = tgPending.SummaryText;
        TgPendingCount = tgPending.Count;
        RebuildMetricCards(_lastStats, tgPending.Count, HealthIssues.Count);
        TgPendingEntries.Clear();
        foreach (var entry in tgPending.List)
            TgPendingEntries.Add(entry);
        ApplyTgPendingEmptyHint(tgPending.Count);
    }

    [RelayCommand]
    private void OpenMetricFilter(string? key)
    {
        switch (key?.Trim().ToLowerInvariant())
        {
            case "jobs":
                NavigateToFilterRequested?.Invoke("全部");
                break;
            case "pending":
                FilterPendingCommand.Execute(null);
                break;
            case "failed":
                FilterFailedCommand.Execute(null);
                break;
            case "tg":
                RefreshTgPendingOnly();
                NavigateToTgPendingRequested?.Invoke();
                break;
            case "health":
                NavigateToOperationsRequested?.Invoke();
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(HasNextAction))]
    private void GoToNextAction()
    {
        if (!string.IsNullOrWhiteSpace(NextActionJobId))
            NavigateToJobRequested?.Invoke(NextActionJobId);
    }

    [RelayCommand(CanExecute = nameof(HasNextAction))]
    private async Task ExecuteNextActionAsync()
    {
        if (string.IsNullOrWhiteSpace(NextActionJobId))
            return;

        IsBusy = true;
        var jobId = NextActionJobId;
        try
        {
            var result = await _app.JobRunner.ExecuteNextActionAsync(
                jobId,
                action: null,
                confirmCleanupAsync: OnConfirmCleanupAsync);
            _ui.Run(() =>
            {
                RefreshCore();
                NavigateToJobRequested?.Invoke(jobId);
                var title = result.Success
                    ? Title("dashboard.msg.title.oneClickRun")
                    : result.NeedsUserInput ? Title("wizard.page.needsManual") : Title("wizard.page.executeFailed");
                MessageRequested?.Invoke(result.Message, title);
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("wizard.page.executeFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunScheduleCommands))]
    private void RegisterScheduleTask()
    {
        var result = _app.JobRunner.RegisterScheduleBatch();
        ScheduleCommandText = result.Export?.SchTasksCommand ?? ScheduleCommandText;

        MessageRequested?.Invoke(
            result.Message,
            result.Success ? Title("dashboard.msg.title.registerSchedule") : Title("dashboard.msg.title.registerScheduleFailed"));

        if (!string.IsNullOrWhiteSpace(result.Export?.BatPath))
            OpenFolder(Path.GetDirectoryName(result.Export.BatPath));
    }

    [RelayCommand(CanExecute = nameof(CanRunScheduleCommands))]
    private void CopyScheduleCommand()
    {
        if (string.IsNullOrWhiteSpace(ScheduleCommandText))
        {
            var export = _app.JobRunner.ExportScheduleBatchScript();
            ScheduleCommandText = export.SchTasksCommand;
        }

        var package = new DataPackage();
        package.SetText(ScheduleCommandText);
        Clipboard.SetContent(package);
        MessageRequested?.Invoke(Msg("dashboard.msg.scheduleCmdCopied"), Title("common.msg.title.copied"));
    }

    private bool CanRunScheduleCommands() => !IsBusy;

    private bool CanRunBatchCommands() => !IsBusy;

    [RelayCommand]
    private void ExportScheduleBatch()
    {
        var export = _app.JobRunner.ExportScheduleBatchScript();
        ScheduleCommandText = export.SchTasksCommand;
        MessageRequested?.Invoke(
            Msg("dashboard.msg.scheduleBatchExportedFormat", export.BatPath, export.SchTasksCommand),
            Title("dashboard.msg.title.scheduleBatch"));
        OpenFolder(Path.GetDirectoryName(export.BatPath));
    }

    [RelayCommand]
    private void BatchGenerateQueueCopy()
    {
        var result = _app.JobRunner.BatchGenerateCopyForQueue(20);
        RefreshCore();
        MessageRequested?.Invoke(
            Msg("dashboard.msg.batchGenerateQueueDoneFormat", result.Success, result.Skipped, result.Failed),
            Title("dashboard.msg.title.batchGenerateQueueCopy"));
    }

    [RelayCommand(CanExecute = nameof(CanBatchSendTgPending))]
    private void PreviewTgPending()
    {
        var preview = _app.JobRunner.GetTgPreviewSnapshot(20);
        if (preview.Count == 0)
        {
            MessageRequested?.Invoke(Msg("dashboard.msg.noTgPreviewJobs"), Title("dashboard.msg.title.tgPreview"));
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(preview.SummaryText);
        sb.AppendLine();
        foreach (var entry in preview.Entries.Take(5))
        {
            sb.AppendLine($"【{entry.Title}】");
            sb.AppendLine(entry.PreviewText);
            sb.AppendLine();
        }

        if (preview.Entries.Count > 5)
            sb.AppendLine($"... 另有 {preview.Entries.Count - 5} 个任务");

        PreviewTextRequested?.Invoke(sb.ToString());
    }

    [RelayCommand(CanExecute = nameof(CanBatchSendTgPending))]
    private async Task BatchSendTgPendingAsync()
    {
        var sendCount = TgPendingEntries.Count(e => e.HasCopy);
        if (sendCount == 0)
        {
            MessageRequested?.Invoke(Msg("dashboard.msg.noTgSendJobs"), Title("dashboard.msg.title.batchSendTg"));
            return;
        }

        if (ConfirmBatchSendTgPendingRequested is not null)
        {
            var confirmed = await ConfirmBatchSendTgPendingRequested(sendCount);
            if (!confirmed)
                return;
        }

        IsBusy = true;
        try
        {
            var result = await _app.JobRunner.BatchSendTgPendingAsync(sendCount);
            _ui.Run(() =>
            {
                RefreshTgPendingOnly();
                var message = $"批量发送完成：成功 {result.Success}，跳过 {result.Skipped}，失败 {result.Failed}";
                if (result.Messages.Count > 0)
                    message += Environment.NewLine + string.Join(Environment.NewLine, result.Messages.Take(8));

                MessageRequested?.Invoke(message, Title("dashboard.msg.title.batchSendTg"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("dashboard.msg.title.batchSendTgFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    private bool CanBatchSendTgPending() => !IsBusy && TgPendingCount > 0;

    [RelayCommand]
    private void GoToTgPendingJob(TgPendingEntry? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.JobId))
            return;

        NavigateToJobRequested?.Invoke(entry.JobId);
    }

    [RelayCommand(CanExecute = nameof(CanBatchSendTgPending))]
    private void ExportTgPendingCsv()
    {
        var export = _app.JobRunner.ExportTgPendingCsv(TgPendingCount > 0 ? TgPendingCount : 50);
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("dashboard.msg.title.exportTgPendingCsv"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void ExportPublishQueueCsv()
    {
        var export = _app.JobRunner.ExportPublishQueueCsv(50);
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("dashboard.msg.title.exportPublishQueueCsv"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand(CanExecute = nameof(CanSendSingleTg))]
    private async Task SendTgPendingJobAsync(TgPendingEntry? entry)
    {
        if (entry is null || string.IsNullOrWhiteSpace(entry.JobId) || !entry.HasCopy)
            return;

        if (ConfirmSendSingleTgRequested is not null)
        {
            var confirmed = await ConfirmSendSingleTgRequested(entry.Title);
            if (!confirmed)
                return;
        }

        IsBusy = true;
        try
        {
            var result = await _app.JobRunner.SendPublishCopyToTelegramAsync(entry.JobId);
            _ui.Run(() =>
            {
                RefreshTgPendingOnly();
                MessageRequested?.Invoke(
                    result.Success ? Msg("dashboard.msg.tgSentFormat", entry.Title) : (result.Error ?? Msg("jobs.msg.sendFailedFallback")),
                    result.Success ? Title("dashboard.msg.title.sendTg") : Title("dashboard.msg.title.sendTgFailed"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("dashboard.msg.title.sendTgFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    private bool CanSendSingleTg(TgPendingEntry? entry)
        => !IsBusy && entry is not null && entry.HasCopy && !string.IsNullOrWhiteSpace(entry.JobId);

    [RelayCommand(CanExecute = nameof(CanRunBatchCommands))]
    private async Task BatchRunAutomatableChainAsync()
    {
        var jobCount = JobHealthService.FilterForBatchPipeline(_allJobs, JobListFilter.Active).Count;
        if (jobCount == 0)
        {
            MessageRequested?.Invoke(Msg("dashboard.msg.noBatchAutoRunJobs"), Title("dashboard.msg.title.batchAutoRun"));
            return;
        }

        if (ConfirmBatchAutomatableChainRequested is not null)
        {
            var confirmed = await ConfirmBatchAutomatableChainRequested(jobCount);
            if (!confirmed)
                return;
        }

        IsBusy = true;
        try
        {
            var result = await _app.JobRunner.RunBatchAutomatableChainAsync(
                JobListFilter.Active,
                confirmCleanupAsync: OnConfirmCleanupAsync);
            _ui.Run(() =>
            {
                RefreshCore();
                MessageRequested?.Invoke(BuildBatchAutomatableChainSummary(result), Title("dashboard.msg.title.batchAutoRun"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("dashboard.msg.title.batchAutoRunFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand]
    private void BatchMarkQueuePublished()
    {
        var ready = PublishQueue.Where(e => e.ReadyChannelCount >= 3).ToList();
        if (ready.Count == 0)
        {
            MessageRequested?.Invoke(Msg("dashboard.msg.noBatchMarkPublishedJobs"), Title("dashboard.msg.title.batchMarkPublished"));
            return;
        }

        var success = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var entry in ready)
        {
            try
            {
                _app.JobRunner.MarkAllChannelsPublished(entry.JobId);
                success++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{entry.Title}: {ex.Message}");
            }
        }

        RefreshCore();
        var message = $"批量标记完成：成功 {success}，失败 {failed}";
        if (errors.Count > 0)
            message += Environment.NewLine + string.Join(Environment.NewLine, errors.Take(5));

        MessageRequested?.Invoke(message, Title("dashboard.msg.title.batchMarkPublished"));
    }

    [RelayCommand]
    private void ExportHealthReport()
    {
        var export = _app.WorkspaceHealth.ExportReport();
        MessageRequested?.Invoke(
            Msg("dashboard.msg.healthReportExportedFormat", export.ExportPath, export.JsonPath),
            Title("dashboard.msg.title.workspaceHealth"));
        if (!string.IsNullOrWhiteSpace(export.ExportPath))
            OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand]
    private void ExportDailyReport()
    {
        var path = _app.JobRunner.ExportDailyReport();
        MessageRequested?.Invoke(Msg("dashboard.msg.dailyReportExportedFormat", path), Title("common.msg.title.exportDaily"));
        OpenFolder(Path.GetDirectoryName(path));
    }

    [RelayCommand]
    private void ExportAllJobsBackup()
    {
        var path = _app.JobRunner.ExportAllJobsBackup();
        MessageRequested?.Invoke(Msg("dashboard.msg.jobsBackupExportedFormat", path), Title("common.msg.title.backupAllJobs"));
        OpenFolder(Path.GetDirectoryName(path));
    }

    [RelayCommand]
    private void ExportFullBackup()
    {
        var export = _app.JobRunner.ExportAllBackup();
        var message = Msg("dashboard.msg.fullBackupExportedFormat", export.JobCount, export.ExportPath);
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
    private async Task ImportJobsBackupAsync()
    {
        if (PickBackupFileRequested is null)
        {
            MessageRequested?.Invoke(Msg("common.msg.title.pickerNotReady"), Title("dashboard.msg.title.restoreJobsBackup"));
            return;
        }

        var path = await PickBackupFileRequested.Invoke();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var result = _app.JobRunner.ImportJobsBackup(path, merge: true);
            RefreshCore();
            var message = Msg("dashboard.msg.restoreJobsDoneFormat", result.Imported, result.Updated, result.Skipped);
            if (result.Messages.Count > 0)
                message += Environment.NewLine + string.Join(Environment.NewLine, result.Messages.Take(5));

            MessageRequested?.Invoke(message, Title("dashboard.msg.title.restoreJobsBackup"));
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(ex.Message, Title("dashboard.msg.title.restoreJobsBackupFailed"));
        }
    }

    [RelayCommand]
    private void OpenWorkspace() => OpenFolder(_app.Workspace.GetWorkspaceRoot());

    [RelayCommand]
    private void FilterFailed() => NavigateToFilterRequested?.Invoke("失败");

    [RelayCommand]
    private void FilterPending() => NavigateToFilterRequested?.Invoke("待发布");

    [RelayCommand]
    private void FilterArchived() => NavigateToFilterRequested?.Invoke("已归档");

    [RelayCommand]
    private void GoToJobsFromEmptyQueue() => NavigateToJobsRequested?.Invoke();

    private Task<bool> OnConfirmCleanupAsync(CleanupConfirmation confirmation)
    {
        var tcs = new TaskCompletionSource<bool>();
        ConfirmCleanupRequested?.Invoke(confirmation, tcs);
        return tcs.Task;
    }

    private static RecentActivityEntry ToRecentEntry(PublishJob job) => new()
    {
        JobId = job.Id,
        Title = job.Title,
        Status = job.Status,
        StatusLabel = JobStatusDisplayHelper.ToLocalized(job.Status),
        UpdatedAt = job.UpdatedAt,
        LastLogLine = job.Log.LastOrDefault()
    };

    private static string BuildBatchAutomatableChainSummary(BatchChainResult result)
    {
        var sb = new StringBuilder();
        sb.Append($"批量自动链完成：成功 {result.Success}，待手动 {result.StoppedForManual}，跳过 {result.Skipped}，失败 {result.Failed}");

        if (result.Messages.Count > 0)
        {
            sb.AppendLine();
            foreach (var line in result.Messages.Take(BatchLogService.MaxPreviewLines))
                sb.AppendLine(line);

            if (result.Messages.Count > BatchLogService.MaxPreviewLines)
                sb.AppendLine(string.IsNullOrWhiteSpace(result.BatchLogPath) ? "详见日志" : $"详见日志: {result.BatchLogPath}");
        }

        return sb.ToString().TrimEnd();
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

    public event Action<int>? DashboardRefreshed;
    public event Action<string, string>? MessageRequested;
    public event Action<string>? NavigateToFilterRequested;
    public event Action? NavigateToOperationsRequested;
    public event Action? NavigateToTgPendingRequested;
    public event Action<string>? NavigateToJobRequested;
    public event Action? NavigateToJobsRequested;
    public event Action<CleanupConfirmation, TaskCompletionSource<bool>>? ConfirmCleanupRequested;
    public event Func<int, Task<bool>>? ConfirmBatchAutomatableChainRequested;
    public event Func<int, Task<bool>>? ConfirmBatchSendTgPendingRequested;
    public event Func<string, Task<bool>>? ConfirmSendSingleTgRequested;
    public event Action<string>? PreviewTextRequested;
    public event Func<Task<bool?>>? ConfirmImportFullBackupRequested;
    public event Func<Task<string?>>? PickBackupFileRequested;
}