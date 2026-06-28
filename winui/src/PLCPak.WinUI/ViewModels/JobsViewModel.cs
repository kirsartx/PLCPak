using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;
using PLCPak.WinUI.Infrastructure;
using Windows.ApplicationModel.DataTransfer;

namespace PLCPak.WinUI.ViewModels;

public sealed class JobWizardStepItem
{
    public int Order { get; init; }
    public int TabIndex { get; init; }
    public string TabId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public WizardStepStatus Status { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsComplete { get; init; }
}

public partial class JobsViewModel : ObservableObject
{
    private const int JobSearchDebounceMs = 400;
    private const string DefaultJobFilter = "全部";
    private const string DefaultJobSort = "最近更新";

    private readonly PlcPakAppContext _app;
    private readonly UiDispatcher _ui;
    private readonly UiModeNotifier _uiMode;
    private readonly Dictionary<string, string> _templateNameToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, JobListFilter> _filterLabelToValue = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, JobSortOrder> _sortLabelToValue = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _mergeOptionToJobId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PublishJob> _allJobs = [];
    private CancellationTokenSource? _logPollCts;
    private CancellationTokenSource? _jobSearchDebounceCts;
    private string? _wizardTrackedJobId;
    private int _previousWizardCompletedCount = -1;
    private bool[] _previousWizardStepComplete = [];
    private int? _pendingWizardCompletionAnimationIndex;
    private bool _syncingJobsList;
    private bool _suppressJobFilterApply;

    public JobsViewModel(PlcPakAppContext app)
    {
        _app = app;
        _ui = UiDispatcher.ForCurrentThread();
        _uiMode = new UiModeNotifier(app, () =>
        {
            OnPropertyChanged(nameof(IsProfessionalMode));
            OnPropertyChanged(nameof(IsSimpleMode));
            NotifyWizardTabHeaderProperties();
            ResetJobDetailMoreSettingsCollapse();
        });
        LocalizationService.LanguageChanged += OnLanguageChanged;
        _app.Workspace.EnsureLayout();
        LoadPublishTemplates();
        LoadJobFilters();
        LoadJobSortOptions();
        LoadStudioSettings();
        RefreshJobsCore();
    }

    public ObservableCollection<PublishJob> Jobs { get; } = [];
    public ObservableCollection<string> PublishTemplateNames { get; } = [];
    public ObservableCollection<string> JobFilterOptions { get; } = [];
    public ObservableCollection<string> JobSortOptions { get; } = [];
    public ObservableCollection<string> TagFilterOptions { get; } = [];
    public ObservableCollection<string> MergeSourceJobOptions { get; } = [];
    public ObservableCollection<string> ForumSiteOptions { get; } = [];
    public ObservableCollection<string> SelectedJobIds { get; } = [];
    public ObservableCollection<JobWizardStepItem> WizardSteps { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelectedJobArchived))]
    [NotifyPropertyChangedFor(nameof(IsSelectedJobFailed))]
    [NotifyPropertyChangedFor(nameof(IsSelectedJobPinned))]
    [NotifyPropertyChangedFor(nameof(TogglePinJobButtonText))]
    [NotifyPropertyChangedFor(nameof(SelectedJobDisplayTitle))]
    private PublishJob? _selectedJob;
    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private string _threadUrl = string.Empty;
    [ObservableProperty] private string _selectedSite = "老王论坛";
    [ObservableProperty] private string _archivePassword = string.Empty;
    [ObservableProperty] private string _jobLogText = string.Empty;
    [ObservableProperty] private string _workspacePath = string.Empty;
    [ObservableProperty] private string _passwordEntryCountText = string.Empty;
    [ObservableProperty] private string _baiduLink = string.Empty;
    [ObservableProperty] private string _baiduPassword = string.Empty;
    [ObservableProperty] private string _quarkLink = string.Empty;
    [ObservableProperty] private string _quarkPassword = string.Empty;
    [ObservableProperty] private string _telegramPostLink = string.Empty;
    [ObservableProperty] private string _telegramChannelUrl = string.Empty;
    [ObservableProperty] private string _publishStatusText = string.Empty;
    [ObservableProperty] private string _selectedPublishTemplate = string.Empty;
    [ObservableProperty] private string _publishCopyText = string.Empty;
    [ObservableProperty] private string _statsSummaryText = string.Empty;
    [ObservableProperty] private string _selectedJobFilter = "全部";
    [ObservableProperty] private string _selectedJobSort = "最近更新";
    [ObservableProperty] private string _selectedTagFilter = JobQueryService.AllTagsLabel;
    [ObservableProperty] private string _jobSearchText = string.Empty;
    [ObservableProperty] private string _jobNotes = string.Empty;
    [ObservableProperty] private string _jobTagsText = string.Empty;
    [ObservableProperty] private string _selectedMergeSourceJob = string.Empty;
    [ObservableProperty] private string _pipelineProgressText = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedJobNextActionDisplayText))]
    private string _selectedJobNextActionText = string.Empty;
    [ObservableProperty] private string _wizardStepHintText = string.Empty;
    [ObservableProperty] private string _wizardSummaryText = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanWizardGoPrev))]
    [NotifyPropertyChangedFor(nameof(CanWizardGoNext))]
    [NotifyCanExecuteChangedFor(nameof(WizardPrevStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(WizardNextStepCommand))]
    private int _selectedWizardTabIndex;

    [ObservableProperty] private double _wizardStepProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDuplicateWarning))]
    private string _duplicateWarningText = string.Empty;

    public bool HasDuplicateWarning => !string.IsNullOrWhiteSpace(DuplicateWarningText);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPipelineProgress))]
    private int _pipelineProgressPercent;

    [ObservableProperty] private bool _enable7z = true;
    [ObservableProperty] private bool _enableTarZst = true;

    [ObservableProperty] private bool _isJobDetailMoreSettingsExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    [NotifyPropertyChangedFor(nameof(IsJobsEmpty))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWizardStepsContent))]
    [NotifyCanExecuteChangedFor(nameof(RefreshJobsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteWizardStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteSelectedNextActionCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunFullPipelineCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunAutomatableChainCommand))]
    [NotifyCanExecuteChangedFor(nameof(WizardPrevStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(WizardNextStepCommand))]
    private bool _isRefreshingJobs;

    public bool CanRun => !IsBusy;
    public bool IsProfessionalMode => _uiMode.IsProfessionalMode;
    public bool IsSimpleMode => _uiMode.IsSimpleMode;
    public bool IsJobsEmpty => Jobs.Count == 0;
    public bool IsSyncingJobsList => _syncingJobsList;
    public bool IsWorkspaceJobsEmpty => _allJobs.Count == 0;
    public bool IsFilterEmpty => !IsWorkspaceJobsEmpty && Jobs.Count == 0;
    public bool IsFilterActive =>
        !string.IsNullOrWhiteSpace(JobSearchText)
        || !string.Equals(SelectedJobFilter, DefaultJobFilter, StringComparison.Ordinal)
        || !string.Equals(SelectedJobSort, DefaultJobSort, StringComparison.Ordinal)
        || !string.Equals(SelectedTagFilter, JobQueryService.AllTagsLabel, StringComparison.Ordinal);
    public bool HasMultiJobSelection => SelectedJobIds.Count >= 2;
    public bool HasJobSelection => SelectedJobIds.Count > 0;
    public string JobsListCountText => IsFilterActive && Jobs.Count != _allJobs.Count
        ? string.Format(LocalizationService.T("jobs.listCountFiltered"), Jobs.Count, _allJobs.Count)
        : string.Format(LocalizationService.T("jobs.listCount"), _allJobs.Count);
    public string JobsMultiSelectHint => SelectedJobIds.Count > 0
        ? string.Format(LocalizationService.T("jobs.multiSelectCount"), SelectedJobIds.Count)
        : string.Empty;
    public string JobsRefreshingHint => LocalizationService.T("jobs.refreshing");
    public string JobsNextActionLoadingHint => LocalizationService.T("jobs.detail.nextActionLoading");
    public string JobsWizardStepsLoadingHint => LocalizationService.T("jobs.wizard.stepsLoading");
    public bool ShowWizardStepsContent => HasWizardJob && !IsRefreshingJobs;

    [ObservableProperty] private string _jobsInboxDropFeedbackText = string.Empty;
    [ObservableProperty] private string _jobsPasswordDropFeedbackText = string.Empty;

    private CancellationTokenSource? _inboxDropFeedbackCts;
    private CancellationTokenSource? _passwordDropFeedbackCts;
    public string JobsFilterSummaryText => BuildJobsFilterSummaryText();
    public bool ShowPipelineProgress => PipelineProgressPercent > 0;
    public bool IsSelectedJobArchived => SelectedJob?.Status == JobStatus.Archived;
    public bool IsSelectedJobFailed => SelectedJob?.Status == JobStatus.Failed;
    public bool IsSelectedJobPinned => SelectedJob?.IsPinned ?? false;
    public string TogglePinJobButtonText => IsSelectedJobPinned ? JobsDetailUnpin : JobsDetailPin;
    public bool HasWizardJob => SelectedJob is not null;
    public bool ShowJobDetailExtrasCollapsed => IsSimpleMode && HasWizardJob;
    public bool CanWizardGoPrev => HasWizardJob && SelectedWizardTabIndex > 0;
    public bool CanWizardGoNext => HasWizardJob && SelectedWizardTabIndex < 3;
    private bool CanWizardGoPrevDuringRefresh() => CanRunWizardNav() && CanWizardGoPrev;
    private bool CanWizardGoNextDuringRefresh() => CanRunWizardNav() && CanWizardGoNext;

    public string JobsWorkbenchTitle => $"{LocalizationService.T("jobs.workbench.title")} v{AppVersion.Current}";
    public string JobsCollapseMoreSettings => LocalizationService.T("jobs.collapse.moreSettings");
    public string JobsSimpleHint => LocalizationService.T("jobs.simpleHint");
    public string JobsWizardTitle => LocalizationService.T("jobs.wizard.title");
    public string JobsWizardHint => LocalizationService.T("jobs.wizard.hint");
    public string JobsWizardShortcuts => LocalizationService.T("jobs.wizard.shortcuts");
    public string JobsNextActionTitle => LocalizationService.T("jobs.nextAction.title");
    public string JobsNextActionFallback => LocalizationService.T("jobs.nextAction.fallback");
    public string JobsSelectJobPrompt => LocalizationService.T("jobs.selectJobPrompt");
    public string JobsEmptyList => LocalizationService.T("jobs.emptyList");
    public string JobsFilterEmptyHint => LocalizationService.T("jobs.filterEmpty");
    public string JobsClearFiltersLabel => LocalizationService.T("jobs.clearFilters");
    public string JobsEmptyDetailTitle => LocalizationService.T("jobs.emptyDetail");
    public string JobsEmptyDetailHint => LocalizationService.T("jobs.emptyDetailHint");
    public string JobsLogTitle => LocalizationService.T("jobs.log.title");
    public string JobsProShortcutsLine => LocalizationService.T("jobs.proShortcuts.line");
    public string JobsProShortcutsTooltip => LocalizationService.T("jobs.proShortcuts.tooltip");
    public string JobsSidebarNewGameHeader => LocalizationService.T("jobs.sidebar.newGameHeader");
    public string JobsSidebarNewJobPlaceholder => LocalizationService.T("jobs.sidebar.newJobPlaceholder");
    public string JobsSidebarThreadUrlHeader => LocalizationService.T("jobs.sidebar.threadUrlHeader");
    public string JobsSidebarPasteLink => LocalizationService.T("jobs.sidebar.pasteLink");
    public string JobsSidebarFetchThreadInfo => LocalizationService.T("jobs.sidebar.fetchThreadInfo");
    public string JobsSidebarSourceForum => LocalizationService.T("jobs.sidebar.sourceForum");
    public string JobsSidebarNewJob => LocalizationService.T("jobs.sidebar.newJob");
    public string JobsSidebarCreateFromThread => LocalizationService.T("jobs.sidebar.createFromThread");
    public string JobsSidebarSearchJobs => LocalizationService.T("jobs.sidebar.searchJobs");
    public string JobsSidebarSearchPlaceholder => LocalizationService.T("jobs.sidebar.searchPlaceholder");
    public string JobsSidebarJobFilter => LocalizationService.T("jobs.sidebar.jobFilter");
    public string JobsSidebarSort => LocalizationService.T("jobs.sidebar.sort");
    public string JobsSidebarTagFilter => LocalizationService.T("jobs.sidebar.tagFilter");
    public string JobsSidebarBatchAppendTags => LocalizationService.T("jobs.sidebar.batchAppendTags");
    public string JobsSidebarBatchArchiveSelected => LocalizationService.T("jobs.sidebar.batchArchiveSelected");
    public string JobsSidebarBatchUnarchiveSelected => LocalizationService.T("jobs.sidebar.batchUnarchiveSelected");
    public string JobsSidebarBatchDeleteSelected => LocalizationService.T("jobs.sidebar.batchDeleteSelected");
    public string JobsSidebarBatchArchiveFiltered => LocalizationService.T("jobs.sidebar.batchArchiveFiltered");
    public string JobsSidebarBatchPinFiltered => LocalizationService.T("jobs.sidebar.batchPinFiltered");
    public string JobsSidebarBatchUnpinFiltered => LocalizationService.T("jobs.sidebar.batchUnpinFiltered");
    public string JobsSidebarArchiveJob => LocalizationService.T("jobs.sidebar.archiveJob");
    public string JobsSidebarUnarchiveJob => LocalizationService.T("jobs.sidebar.unarchiveJob");
    public string JobsSidebarDeleteJob => LocalizationService.T("jobs.sidebar.deleteJob");
    public string JobsSidebarExportJobJson => LocalizationService.T("jobs.sidebar.exportJobJson");
    public string JobsSidebarExportFilteredCsv => LocalizationService.T("jobs.sidebar.exportFilteredCsv");
    public string JobsSidebarExportFilteredJson => LocalizationService.T("jobs.sidebar.exportFilteredJson");
    public string JobsSidebarExportSelectedCsv => LocalizationService.T("jobs.sidebar.exportSelectedCsv");
    public string JobsSidebarExportPinnedCsv => LocalizationService.T("jobs.sidebar.exportPinnedCsv");
    public string JobsSidebarImportJobJson => LocalizationService.T("jobs.sidebar.importJobJson");
    public string JobsSidebarImportPanLinksCsv => LocalizationService.T("jobs.sidebar.importPanLinksCsv");
    public string JobsSidebarRefreshList => LocalizationService.T("jobs.sidebar.refreshList");
    public string JobsSidebarExportPublishHistory => LocalizationService.T("jobs.sidebar.exportPublishHistory");
    public string JobsSidebarBatchGenerateCopy => LocalizationService.T("jobs.sidebar.batchGenerateCopy");
    public string JobsSidebarBatchPipeline => LocalizationService.T("jobs.sidebar.batchPipeline");
    public string JobsSidebarBatchAutoChain => LocalizationService.T("jobs.sidebar.batchAutoChain");
    public string JobsSidebarOpenWorkspace => LocalizationService.T("jobs.sidebar.openWorkspace");
    public string JobsDetailExecuteSuggestedAction => LocalizationService.T("jobs.detail.executeSuggestedAction");
    public string JobsDetailRunFullPipeline => LocalizationService.T("jobs.detail.runFullPipeline");
    public string JobsDetailAutoRunUntilManual => LocalizationService.T("jobs.detail.autoRunUntilManual");
    public string JobsDetailJobNotes => LocalizationService.T("jobs.detail.jobNotes");
    public string JobsDetailJobNotesPlaceholder => LocalizationService.T("jobs.detail.jobNotesPlaceholder");
    public string JobsDetailSaveNotes => LocalizationService.T("jobs.detail.saveNotes");
    public string JobsDetailArchivePassword => LocalizationService.T("jobs.detail.archivePassword");
    public string JobsDetailArchivePasswordPlaceholder => LocalizationService.T("jobs.detail.archivePasswordPlaceholder");
    public string JobsDetailSavePassword => LocalizationService.T("jobs.detail.savePassword");
    public string JobsDetailInboxDropHint => LocalizationService.T("jobs.detail.inboxDropHint");
    public string JobsDetailOpenExtract => LocalizationService.T("jobs.detail.openExtract");
    public string JobsDetailOpenOutput => LocalizationService.T("jobs.detail.openOutput");
    public string JobsDetailPasswordLibrary => LocalizationService.T("jobs.detail.passwordLibrary");
    public string JobsDetailPasswordDropHint => LocalizationService.T("jobs.detail.passwordDropHint");
    public string JobsDetailBrowsePasswordLibrary => LocalizationService.T("jobs.detail.browsePasswordLibrary");
    public string JobsDetailScanUpdate => LocalizationService.T("jobs.detail.scanUpdate");
    public string JobsDetailMatchPassword => LocalizationService.T("jobs.detail.matchPassword");
    public string JobsDetailOpenThread => LocalizationService.T("jobs.detail.openThread");
    public string JobsDetailOpenInbox => LocalizationService.T("jobs.detail.openInbox");
    public string JobsDetailScanInbox => LocalizationService.T("jobs.detail.scanInbox");
    public string JobsDetailDownloadThreadAttachments => LocalizationService.T("jobs.detail.downloadThreadAttachments");
    public string JobsDetailExtract => LocalizationService.T("jobs.detail.extract");
    public string JobsDetailProcess => LocalizationService.T("jobs.detail.process");
    public string JobsDetailRetryJob => LocalizationService.T("jobs.detail.retryJob");
    public string JobsDetailAdvancedActions => LocalizationService.T("jobs.detail.advancedActions");
    public string JobsDetailExportNightlyScript => LocalizationService.T("jobs.detail.exportNightlyScript");
    public string JobsDetailMergeSourceJob => LocalizationService.T("jobs.detail.mergeSourceJob");
    public string JobsDetailSelectSourceJob => LocalizationService.T("jobs.detail.selectSourceJob");
    public string JobsDetailMergeIntoCurrent => LocalizationService.T("jobs.detail.mergeIntoCurrent");
    public string JobsDetailBaiduLink => LocalizationService.T("jobs.detail.baiduLink");
    public string JobsDetailExtractCode => LocalizationService.T("jobs.detail.extractCode");
    public string JobsDetailOpenBaidu => LocalizationService.T("jobs.detail.openBaidu");
    public string JobsDetailQuarkLink => LocalizationService.T("jobs.detail.quarkLink");
    public string JobsDetailOpenQuark => LocalizationService.T("jobs.detail.openQuark");
    public string JobsDetailTelegramPostLink => LocalizationService.T("jobs.detail.telegramPostLink");
    public string JobsDetailTelegramChannel => LocalizationService.T("jobs.detail.telegramChannel");
    public string JobsDetailSaveChannel => LocalizationService.T("jobs.detail.saveChannel");
    public string JobsDetailTags => LocalizationService.T("jobs.detail.tags");
    public string JobsDetailTagsPlaceholder => LocalizationService.T("jobs.detail.tagsPlaceholder");
    public string JobsDetailSaveTags => LocalizationService.T("jobs.detail.saveTags");
    public string JobsDetailBatchAppend => LocalizationService.T("jobs.detail.batchAppend");
    public string JobsDetailPin => LocalizationService.T("jobs.detail.pin");
    public string JobsDetailUnpin => LocalizationService.T("jobs.detail.unpin");
    public string JobsDetailSaveLinks => LocalizationService.T("jobs.detail.saveLinks");
    public string JobsDetailParseClipboardLinks => LocalizationService.T("jobs.detail.parseClipboardLinks");
    public string JobsDetailCopyAllLinks => LocalizationService.T("jobs.detail.copyAllLinks");
    public string JobsDetailOpenAllLinks => LocalizationService.T("jobs.detail.openAllLinks");
    public string JobsDetailBlurbTemplate => LocalizationService.T("jobs.detail.blurbTemplate");
    public string JobsDetailGenerateCopy => LocalizationService.T("jobs.detail.generateCopy");
    public string JobsDetailPreviewTemplate => LocalizationService.T("jobs.detail.previewTemplate");
    public string JobsDetailCopyTgCopy => LocalizationService.T("jobs.detail.copyTgCopy");
    public string JobsDetailEditPublishTemplates => LocalizationService.T("jobs.detail.editPublishTemplates");
    public string JobsDetailPublishCopyPreview => LocalizationService.T("jobs.detail.publishCopyPreview");
    public string JobsDetailMarkAllPublished => LocalizationService.T("jobs.detail.markAllPublished");
    public string JobsDetailMarkBaiduPublished => LocalizationService.T("jobs.detail.markBaiduPublished");
    public string JobsDetailMarkQuarkPublished => LocalizationService.T("jobs.detail.markQuarkPublished");
    public string JobsDetailMarkTelegramPublished => LocalizationService.T("jobs.detail.markTelegramPublished");
    public string JobsDetailOpenTgChannel => LocalizationService.T("jobs.detail.openTgChannel");
    public string JobsDetailCopyAndOpenTg => LocalizationService.T("jobs.detail.copyAndOpenTg");
    public string JobsDetailBotSendToTg => LocalizationService.T("jobs.detail.botSendToTg");
    public string JobsDetailOpenPublishedFolder => LocalizationService.T("jobs.detail.openPublishedFolder");
    public string JobsDetailCopyLog => LocalizationService.T("jobs.detail.copyLog");
    public string JobsDetailExportLog => LocalizationService.T("jobs.detail.exportLog");
    public string SelectedJobDisplayTitle => SelectedJob is null ? JobsSelectJobPrompt : SelectedJob.Title;
    public string SelectedJobNextActionDisplayText =>
        string.IsNullOrWhiteSpace(SelectedJobNextActionText) ? JobsNextActionFallback : SelectedJobNextActionText;
    public string WizardStepPrepareHint => LocalizationService.T("wizard.step.prepare.hint");
    public string WizardStepCopyHint => LocalizationService.T("wizard.step.copy.hint");
    public string ExecuteWizardStepLabel => LocalizationService.T("jobs.wizard.execute");
    public string WizardPrevLabel => LocalizationService.T("jobs.wizard.prev");
    public string WizardNextLabel => LocalizationService.T("jobs.wizard.next");
    public string WizardTabHeaderPrepare => LocalizationService.T("wizard.step.prepare");
    public string WizardTabHeaderLinks => LocalizationService.T("wizard.step.links");
    public string WizardTabHeaderCopy => LocalizationService.T("wizard.step.copy");
    public string WizardTabHeaderPublish => LocalizationService.T("wizard.step.publish");
    public bool ShowWizardStepList => false;

    partial void OnIsBusyChanged(bool value) => NotifyJobsCommandsCanExecuteChanged();

    partial void OnSelectedWizardTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CanWizardGoPrev));
        OnPropertyChanged(nameof(CanWizardGoNext));
        WizardPrevStepCommand.NotifyCanExecuteChanged();
        WizardNextStepCommand.NotifyCanExecuteChanged();
        if (HasWizardJob)
            WizardStepScrollRequested?.Invoke(value);
    }

    partial void OnSelectedJobFilterChanged(string value)
    {
        if (!_suppressJobFilterApply)
            ApplyJobFilter();
    }

    partial void OnSelectedJobSortChanged(string value)
    {
        if (!_suppressJobFilterApply)
            ApplyJobFilter();
    }

    partial void OnSelectedTagFilterChanged(string value)
    {
        if (!_suppressJobFilterApply)
            ApplyJobFilter();
    }

    partial void OnJobSearchTextChanged(string value) => _ = DebounceApplyJobFilterAsync();

    partial void OnSelectedJobChanged(PublishJob? value)
    {
        if (_syncingJobsList && value is null)
            return;

        ArchivePassword = value?.Source.ArchivePassword ?? string.Empty;
        JobNotes = value?.Notes ?? string.Empty;
        JobTagsText = value?.Tags is { Count: > 0 } tags
            ? string.Join(", ", tags)
            : string.Empty;
        Enable7z = value?.Enable7z ?? true;
        EnableTarZst = value?.EnableTarZst ?? true;
        LoadPublishFields(value);
        UpdateLogText();
        RefreshMergeSourceJobOptions();
        UpdateSelectedJobNextAction(value);
        var tabBeforeUpdate = SelectedWizardTabIndex;
        UpdateWizardState(value);
        if (value is not null && SelectedWizardTabIndex == tabBeforeUpdate)
            WizardStepScrollRequested?.Invoke(SelectedWizardTabIndex);
        ResetJobDetailMoreSettingsCollapse();
        ExecuteSelectedNextActionCommand.NotifyCanExecuteChanged();
        RunAutomatableChainCommand.NotifyCanExecuteChanged();
    }

    partial void OnEnable7zChanged(bool value) => PersistJobCompressFlags();

    partial void OnEnableTarZstChanged(bool value) => PersistJobCompressFlags();

    partial void OnJobLogTextChanged(string value) => JobLogScrollToEndRequested?.Invoke();

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task PasteThreadUrlAsync()
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Text))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.clipboardNoText"), Title("jobs.sidebar.pasteLink"));
            return;
        }

        var text = (await content.GetTextAsync())?.Trim() ?? string.Empty;
        if (!text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.clipboardNotHttpLink"), Title("jobs.sidebar.pasteLink"));
            return;
        }

        ThreadUrl = text;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenAllPublishLinks()
    {
        var links = new[] { BaiduLink, QuarkLink, TelegramPostLink };
        var opened = 0;
        foreach (var link in links)
        {
            if (string.IsNullOrWhiteSpace(link))
                continue;

            Process.Start(new ProcessStartInfo
            {
                FileName = link.Trim(),
                UseShellExecute = true
            });
            opened++;
        }

        if (opened == 0)
            MessageRequested?.Invoke(Msg("jobs.msg.fillPublishLinksFirst"), Title("jobs.msg.title.openPublishLinks"));
    }

    [RelayCommand(CanExecute = nameof(CanRunSelectedJob))]
    private async Task RunAutomatableChainAsync()
    {
        if (SelectedJob is null)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.selectJobFirst"), Title("wizard.page.autoRun"));
            return;
        }

        IsBusy = true;
        var jobId = SelectedJob.Id;
        StartJobLogPolling(jobId);
        try
        {
            var result = await _app.JobRunner.RunAutomatableChainAsync(
                jobId,
                confirmCleanupAsync: OnConfirmCleanupAsync).ConfigureAwait(false);

            _ui.Run(() =>
            {
                RefreshJobsCore();
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId) ?? result.Job;
                UpdateSelectedJobNextAction(SelectedJob);
                UpdateWizardState(SelectedJob);

                var title = result.NeedsUserInput
                    ? Title("wizard.page.needsManual")
                    : result.Success ? Title("wizard.page.autoRun") : Title("wizard.page.executeFailed");
                MessageRequested?.Invoke(BuildAutomatableChainMessage(result), title);
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("wizard.page.autoRunFailed")));
        }
        finally
        {
            StopJobLogPolling();
            _ui.Run(() => IsBusy = false);
        }
    }

    private bool CanRunSelectedJob() => CanRun && SelectedJob is not null;

    [RelayCommand(CanExecute = nameof(CanRunSelectedNextAction))]
    private async Task ExecuteSelectedNextActionAsync()
    {
        if (SelectedJob is null)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.selectJobFirst"), Title("jobs.detail.executeSuggestedAction"));
            return;
        }

        IsBusy = true;
        var jobId = SelectedJob.Id;
        StartJobLogPolling(jobId);
        try
        {
            var result = await _app.JobRunner.ExecuteNextActionAsync(
                jobId,
                action: null,
                confirmCleanupAsync: OnConfirmCleanupAsync,
                percent: p => _ui.Run(() => PipelineProgressPercent = p));
            _ui.Run(() =>
            {
                if (result.Job is not null)
                {
                    SelectedJob = result.Job;
                    LoadPublishFields(result.Job);
                }

                RefreshJobsCore();
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId) ?? result.Job;
                UpdateSelectedJobNextAction(SelectedJob);
                UpdateWizardState(SelectedJob);

                if (result.Action is JobNextActionType.RunPipeline or JobNextActionType.RetryFailed)
                    PipelineProgressPercent = result.Success ? 100 : 0;

                var title = result.Success
                    ? Title("jobs.detail.executeSuggestedAction")
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
            StopJobLogPolling();
            _ui.Run(() => IsBusy = false);
        }
    }

    private bool CanRunSelectedNextAction()
        => CanRun && !IsRefreshingJobs && SelectedJob is not null && !string.IsNullOrWhiteSpace(SelectedJobNextActionText);

    private bool CanRunWizardNav() => HasWizardJob && !IsRefreshingJobs;

    [RelayCommand(CanExecute = nameof(CanRunSelectedNextAction))]
    private Task ExecuteWizardStepAsync() => ExecuteSelectedNextActionAsync();

    [RelayCommand]
    private void SelectWizardStep(int? index)
    {
        if (index is >= 0 and <= 3)
            SelectedWizardTabIndex = index.Value;
    }

    [RelayCommand(CanExecute = nameof(CanWizardGoPrevDuringRefresh))]
    private void WizardPrevStep()
    {
        if (CanWizardGoPrev)
            SelectedWizardTabIndex--;
    }

    [RelayCommand(CanExecute = nameof(CanWizardGoNextDuringRefresh))]
    private void WizardNextStep()
    {
        if (CanWizardGoNext)
            SelectedWizardTabIndex++;
    }

    private bool CanRefreshJobs() => CanRun && !IsRefreshingJobs;

    [RelayCommand(CanExecute = nameof(CanRefreshJobs))]
    private async Task RefreshJobsAsync()
    {
        if (IsRefreshingJobs)
            return;

        IsRefreshingJobs = true;
        try
        {
            await Task.Yield();
            RefreshJobsCore();
        }
        finally
        {
            IsRefreshingJobs = false;
        }
    }

    private void RefreshJobsCore()
    {
        _suppressJobFilterApply = true;
        try
        {
            _allJobs.Clear();
            _allJobs.AddRange(_app.Jobs.List());
            StatsSummaryText = PublishDashboardService.ComputeStats(_allJobs).SummaryText;
            LoadTagFilterOptions();
            ApplyJobFilter();
            WorkspacePath = _app.Workspace.GetWorkspaceRoot();
            PasswordEntryCountText = $"密码规则: {_app.GetPasswordEntryCount()} 条";
            if (SelectedJob is not null)
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == SelectedJob.Id);
            UpdateLogText();
            RefreshMergeSourceJobOptions();
            UpdateSelectedJobNextAction(SelectedJob);
            UpdateWizardState(SelectedJob);
            NotifyJobListEmptyStateProperties();
            ExecuteSelectedNextActionCommand.NotifyCanExecuteChanged();
            JobsRefreshed?.Invoke();
        }
        finally
        {
            _suppressJobFilterApply = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void CreateJob()
    {
        if (string.IsNullOrWhiteSpace(NewTitle))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.fillGameNameFirst"), Title("jobs.sidebar.newJob"));
            return;
        }

        var source = new JobSource
        {
            Site = SelectedSite,
            ThreadUrl = ThreadUrl.Trim(),
            ArchivePassword = ArchivePassword.Trim()
        };

        var check = _app.JobRunner.CheckCreateJob(NewTitle, source.ThreadUrl);
        if (check.Blocked)
        {
            DuplicateWarningText = check.Message;
            RefreshJobsCore();
            var duplicateId = check.Duplicates.FirstOrDefault(d => d.Reason == "SameThreadUrl")?.JobId;
            if (!string.IsNullOrWhiteSpace(duplicateId))
                SelectedJob = _allJobs.FirstOrDefault(j => j.Id == duplicateId)
                    ?? Jobs.FirstOrDefault(j => j.Id == duplicateId);

            MessageRequested?.Invoke(check.Message, Title("jobs.msg.title.duplicateJob"));
            return;
        }

        var job = _app.JobRunner.CreateJob(NewTitle, source);
        NewTitle = string.Empty;
        RefreshJobsCore();
        SelectedJob = job;
        RequestWizardFocus();

        if (check.HasDuplicates)
        {
            DuplicateWarningText = check.Message;
            MessageRequested?.Invoke(check.Message, Title("jobs.msg.title.similarJob"));
        }
        else
        {
            DuplicateWarningText = string.Empty;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task FetchThreadInfoAsync()
    {
        if (string.IsNullOrWhiteSpace(ThreadUrl))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.fillThreadUrlFirst"), Title("jobs.sidebar.fetchThreadInfo"));
            return;
        }

        IsBusy = true;
        try
        {
            var result = await Task.Run(() => _app.JobRunner.FetchThreadInfoAsync(ThreadUrl));
            _ui.Run(() =>
            {
                if (result.Success)
                {
                    if (!string.IsNullOrWhiteSpace(result.Title))
                        NewTitle = result.Title;
                    if (!string.IsNullOrWhiteSpace(result.ArchivePassword))
                        ArchivePassword = result.ArchivePassword;
                    if (!string.IsNullOrWhiteSpace(result.BaiduLink))
                        BaiduLink = result.BaiduLink;
                    if (!string.IsNullOrWhiteSpace(result.BaiduPassword))
                        BaiduPassword = result.BaiduPassword;
                    if (!string.IsNullOrWhiteSpace(result.QuarkLink))
                        QuarkLink = result.QuarkLink;
                    if (!string.IsNullOrWhiteSpace(result.QuarkPassword))
                        QuarkPassword = result.QuarkPassword;

                    if (SelectedJob is not null)
                    {
                        var jobId = SelectedJob.Id;
                        SelectedJob = _app.JobRunner.ApplyThreadInfoToJob(jobId, result);
                        ArchivePassword = SelectedJob.Source.ArchivePassword;
                        LoadPublishFields(SelectedJob);
                        RefreshJobsCore();
                        SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId) ?? SelectedJob;
                    }

                    MessageRequested?.Invoke(BuildThreadInfoFoundMessage(result), Title("jobs.msg.title.threadInfo"));
                }
                else
                {
                    MessageRequested?.Invoke(result.Error ?? Msg("jobs.msg.threadParseFailed"), Title("jobs.msg.title.fetchFailed"));
                }
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.fetchFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    public IAsyncRelayCommand FetchTitleFromThreadCommand => FetchThreadInfoCommand;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task CreateJobFromThreadAsync()
    {
        if (string.IsNullOrWhiteSpace(ThreadUrl))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.fillThreadUrlFirst"), Title("jobs.sidebar.createFromThread"));
            return;
        }

        var precheckTitle = string.IsNullOrWhiteSpace(NewTitle) ? string.Empty : NewTitle.Trim();
        var precheck = _app.JobRunner.CheckCreateJob(precheckTitle, ThreadUrl.Trim());
        if (precheck.Blocked)
        {
            DuplicateWarningText = precheck.Message;
            RefreshJobsCore();
            var duplicateId = precheck.Duplicates.FirstOrDefault(d => d.Reason == "SameThreadUrl")?.JobId;
            if (!string.IsNullOrWhiteSpace(duplicateId))
                SelectedJob = _allJobs.FirstOrDefault(j => j.Id == duplicateId)
                    ?? Jobs.FirstOrDefault(j => j.Id == duplicateId);

            MessageRequested?.Invoke(precheck.Message, Title("jobs.msg.title.duplicateJob"));
            return;
        }

        IsBusy = true;
        try
        {
            var studio = _app.StudioConfig.Load();
            var title = string.IsNullOrWhiteSpace(NewTitle) ? null : NewTitle.Trim();
            var result = await _app.JobRunner.CreateJobFromThreadAsync(
                ThreadUrl.Trim(),
                title,
                SelectedSite,
                downloadAttachments: studio.AutoDownloadThreadAttachments);

            _ui.Run(() =>
            {
                if (!result.Success || result.Job is null)
                {
                    MessageRequested?.Invoke(result.Error ?? result.Message ?? Msg("jobs.msg.createFromThreadFailed"), Title("jobs.sidebar.createFromThread"));
                    return;
                }

                var jobId = result.Job.Id;
                RefreshJobsCore();
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId) ?? result.Job;
                FillJobCreationFields(result.Job);

                var postcheck = _app.JobRunner.CheckCreateJob(result.Job.Title, result.Job.Source.ThreadUrl);
                var similarOthers = postcheck.Duplicates.Count(d => d.JobId != result.Job.Id);
                if (similarOthers > 0 || precheck.HasDuplicates)
                {
                    DuplicateWarningText = postcheck.Message;
                    MessageRequested?.Invoke(postcheck.Message, Title("jobs.msg.title.similarJob"));
                }
                else
                {
                    DuplicateWarningText = string.Empty;
                }

                RequestWizardFocus();
                MessageRequested?.Invoke(BuildCreateJobFromThreadMessage(result), Title("jobs.sidebar.createFromThread"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.createFromThreadFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task MergeIntoSelectedJobAsync()
    {
        if (SelectedJob is null)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.selectTargetJobFirst"), Title("jobs.msg.title.mergeJob"));
            return;
        }

        var sourceJobId = ResolveMergeSourceJobId();
        if (string.IsNullOrWhiteSpace(sourceJobId))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.selectMergeSourceFirst"), Title("jobs.msg.title.mergeJob"));
            return;
        }

        var targetJobId = SelectedJob.Id;
        var preview = _app.JobRunner.PreviewMergeJobs(targetJobId, sourceJobId);
        if (!preview.Success)
        {
            MessageRequested?.Invoke(preview.Error ?? Msg("jobs.msg.mergePreviewFailed"), Title("jobs.msg.title.mergeJob"));
            return;
        }

        if (ConfirmMergeRequested is null)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.mergeConfirmNotReady"), Title("jobs.msg.title.mergeJob"));
            return;
        }

        var confirmed = await ConfirmMergeRequested(preview.Message);
        if (!confirmed)
            return;

        var result = _app.JobRunner.MergeJobs(targetJobId, sourceJobId);
        _app.JobRunner.LogActivity("merge", $"合并任务: {sourceJobId[..Math.Min(8, sourceJobId.Length)]} → {targetJobId[..Math.Min(8, targetJobId.Length)]}");
        SelectedMergeSourceJob = string.Empty;
        RefreshJobsCore();
        SelectedJob = Jobs.FirstOrDefault(j => j.Id == targetJobId);

        if (!result.Success)
        {
            MessageRequested?.Invoke(result.Error ?? Msg("jobs.msg.mergeFailed"), Title("jobs.msg.title.mergeJob"));
            return;
        }

        if (SelectedJob is not null)
        {
            ArchivePassword = SelectedJob.Source.ArchivePassword;
            JobNotes = SelectedJob.Notes;
            LoadPublishFields(SelectedJob);
        }

        MessageRequested?.Invoke(result.Message, Title("jobs.msg.title.mergeJob"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenTelegramChannel()
    {
        var url = _app.StudioConfig.Load().TelegramChannelUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.saveTgChannelFirst"), Title("jobs.msg.title.tgChannel"));
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url.Trim(),
            UseShellExecute = true
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task SendToTelegramBotAsync()
    {
        if (SelectedJob is null)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.selectJobFirst"), Title("jobs.detail.botSendToTg"));
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedJob.Publish.GeneratedCopy))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.generateCopyFirst"), Title("jobs.detail.botSendToTg"));
            return;
        }

        IsBusy = true;
        var jobId = SelectedJob.Id;
        try
        {
            var result = await _app.JobRunner.SendPublishCopyToTelegramAsync(jobId);
            _ui.Run(() =>
            {
                RefreshJobsCore();
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId);
                var message = result.Success
                    ? BuildTelegramSendMessage(result)
                    : result.Error ?? Msg("jobs.msg.sendFailedFallback");
                MessageRequested?.Invoke(message, result.Success ? Title("jobs.detail.botSendToTg") : Title("common.msg.title.sendFailed"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("common.msg.title.sendFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void CopyAndOpenTelegram()
    {
        if (string.IsNullOrWhiteSpace(PublishCopyText))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.clickGenerateCopyFirst"), Title("jobs.msg.title.copyFailed"));
            return;
        }

        var package = new DataPackage();
        package.SetText(PublishCopyText);
        Clipboard.SetContent(package);

        var url = _app.StudioConfig.Load().TelegramChannelUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.copyWithoutTgChannel"), Title("common.msg.title.copied"));
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url.Trim(),
            UseShellExecute = true
        });
        MessageRequested?.Invoke(Msg("jobs.msg.copyAndOpenTg"), Title("common.msg.title.copied"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task DownloadThreadAttachmentsAsync()
    {
        if (SelectedJob is null)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.selectJobFirst"), Title("jobs.detail.downloadThreadAttachments"));
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedJob.Source.ThreadUrl))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.noThreadLink"), Title("jobs.detail.downloadThreadAttachments"));
            return;
        }

        IsBusy = true;
        var jobId = SelectedJob.Id;
        try
        {
            var result = await _app.JobRunner.DownloadThreadAttachmentsAsync(jobId);
            _ui.Run(() =>
            {
                RefreshJobsCore();
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId);
                if (SelectedJob is not null)
                    ArchivePassword = SelectedJob.Source.ArchivePassword;

                MessageRequested?.Invoke(
                    BuildForumDownloadMessage(result),
                    result.Success ? Title("jobs.detail.downloadThreadAttachments") : Title("jobs.msg.title.downloadFailed"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.downloadFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void ScanInbox()
    {
        if (SelectedJob is null)
            return;

        PersistArchivePassword(SelectedJob);
        SelectedJob = _app.JobRunner.ScanInbox(SelectedJob.Id);
        ArchivePassword = SelectedJob.Source.ArchivePassword;
        RefreshJobsCore();
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void MatchPassword()
    {
        if (SelectedJob is null)
            return;

        PersistArchivePassword(SelectedJob);
        var jobId = SelectedJob.Id;
        var result = _app.JobRunner.MatchPasswords(jobId);
        RefreshJobsCore();
        SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId);
        ArchivePassword = SelectedJob?.Source.ArchivePassword ?? string.Empty;
        if (result.Hits.Count == 0)
            MessageRequested?.Invoke(Msg("jobs.msg.passwordNoMatch"), Title("jobs.detail.matchPassword"));
        else
            MessageRequested?.Invoke(Msg("jobs.msg.passwordHitsFormat", result.Hits.Count), Title("jobs.detail.matchPassword"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenPasswordSamples() => OpenFolder(_app.Paths.PasswordSamplesDirectory);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task SyncPasswordManifestAsync()
    {
        IsBusy = true;
        try
        {
            var stats = await Task.Run(() => _app.PasswordManifest.SyncFromFolder());
            _ui.Run(() =>
            {
                RefreshJobsCore();
                MessageRequested?.Invoke(
                    Msg("jobs.msg.passwordScanDoneFormat", stats.Added, stats.Updated),
                    Title("jobs.msg.title.passwordLibrary"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.passwordLibraryScanFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    public async Task ImportPasswordSamplesAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
            return;

        IsBusy = true;
        try
        {
            var stats = await Task.Run(() => _app.PasswordManifest.ImportPaths(paths));
            _ui.Run(() =>
            {
                RefreshJobsCore();
                _ = ShowPasswordDropFeedbackAsync(stats.Added + stats.Updated + stats.Copied);
                MessageRequested?.Invoke(
                    Msg("jobs.msg.passwordImportDoneFormat", stats.Added, stats.Updated, stats.Copied),
                    Title("jobs.msg.title.passwordLibrary"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.importPasswordSamplesFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task ExtractAsync()
    {
        if (SelectedJob is null)
            return;

        IsBusy = true;
        var jobId = SelectedJob.Id;
        StartJobLogPolling(jobId);
        try
        {
            PersistArchivePassword(SelectedJob);
            var job = await Task.Run(() => _app.JobRunner.Extract(jobId));
            _ui.Run(() =>
            {
                SelectedJob = job;
                RefreshJobsCore();
                if (job.Status == JobStatus.Failed)
                {
                    var title = job.Error?.Contains("密码", StringComparison.OrdinalIgnoreCase) == true
                        ? Title("jobs.msg.title.extractPasswordRequired")
                        : Title("jobs.msg.title.extractFailed");
                    MessageRequested?.Invoke(job.Error ?? Msg("jobs.msg.extractFailedFallback"), title);
                }
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() =>
            {
                JobLogText += $"[错误] {ex.Message}{Environment.NewLine}";
                MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.extractError"));
            });
        }
        finally
        {
            StopJobLogPolling();
            _ui.Run(() => IsBusy = false);
        }
    }

    private async Task ShowInboxDropFeedbackAsync(int count)
    {
        if (count <= 0)
            return;

        JobsInboxDropFeedbackText = string.Format(LocalizationService.T("jobs.detail.inboxDropAdded"), count);
        _inboxDropFeedbackCts?.Cancel();
        _inboxDropFeedbackCts?.Dispose();
        _inboxDropFeedbackCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(2500, _inboxDropFeedbackCts.Token);
            JobsInboxDropFeedbackText = string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ShowPasswordDropFeedbackAsync(int count)
    {
        if (count <= 0)
            return;

        JobsPasswordDropFeedbackText = string.Format(LocalizationService.T("jobs.detail.passwordDropAdded"), count);
        _passwordDropFeedbackCts?.Cancel();
        _passwordDropFeedbackCts?.Dispose();
        _passwordDropFeedbackCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(2500, _passwordDropFeedbackCts.Token);
            JobsPasswordDropFeedbackText = string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task ImportInboxFilesAsync(IReadOnlyList<string> paths)
    {
        if (SelectedJob is null || paths.Count == 0)
            return;

        IsBusy = true;
        var jobId = SelectedJob.Id;
        try
        {
            PersistArchivePassword(SelectedJob);
            var result = await Task.Run(() => _app.JobRunner.ImportInboxFiles(jobId, paths));
            _ui.Run(() =>
            {
                SelectedJob = result.Job;
                ArchivePassword = result.Job.Source.ArchivePassword;
                RefreshJobsCore();
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId) ?? result.Job;

                if (result.CopiedFiles == 0)
                    MessageRequested?.Invoke(Msg("jobs.msg.inboxNoArchives"), Title("jobs.msg.title.importInbox"));
                else
                {
                    _ = ShowInboxDropFeedbackAsync(result.CopiedFiles);
                    MessageRequested?.Invoke(Msg("jobs.msg.inboxImportedFormat", result.CopiedFiles), Title("jobs.msg.title.importInbox"));
                }
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("common.msg.title.importFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunFullPipelineAsync()
    {
        if (SelectedJob is null)
            return;

        IsBusy = true;
        var jobId = SelectedJob.Id;
        StartJobLogPolling(jobId);
        try
        {
            PersistArchivePassword(SelectedJob);
            PersistJobCompressFlags(SelectedJob);
            PipelineProgressPercent = 0;
            PipelineProgressText = "扫描 inbox...";
            var pipeline = await _app.JobRunner.RunFullPipelineAsync(
                jobId,
                OnConfirmCleanupAsync,
                percent: p => _ui.Run(() => PipelineProgressPercent = p));
            _ui.Run(() =>
            {
                SelectedJob = pipeline.Job ?? SelectedJob;
                LoadPublishFields(pipeline.Job);
                RefreshJobsCore();
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId) ?? pipeline.Job;

                PipelineProgressText = pipeline.Steps.Count > 0
                    ? $"步骤: {string.Join(" → ", pipeline.Steps)}"
                    : string.Empty;
                PipelineProgressPercent = pipeline.Success ? 100 : 0;

                if (pipeline.Success)
                    MessageRequested?.Invoke(Msg("jobs.msg.pipelineComplete"), Title("jobs.msg.title.pipelineDone"));
                else
                    MessageRequested?.Invoke(pipeline.Error ?? Msg("jobs.msg.pipelineIncomplete"), Title("jobs.msg.title.pipelineFailed"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() =>
            {
                PipelineProgressText = $"流水线错误: {ex.Message}";
                JobLogText += $"[错误] {ex.Message}{Environment.NewLine}";
                MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.pipelineError"));
            });
        }
        finally
        {
            StopJobLogPolling();
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void UnarchiveJob()
    {
        if (SelectedJob is null || SelectedJob.Status != JobStatus.Archived)
            return;

        var jobId = SelectedJob.Id;
        SelectedJob = _app.JobRunner.UnarchiveJob(jobId);
        RefreshJobsCore();
        SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId);
        MessageRequested?.Invoke(Msg("jobs.msg.jobRestored"), Title("jobs.sidebar.unarchiveJob"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void SaveNotes()
    {
        if (SelectedJob is null)
            return;

        var jobId = SelectedJob.Id;
        SelectedJob = _app.JobRunner.SaveNotes(jobId, JobNotes);
        RefreshJobsCore();
        SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId);
        MessageRequested?.Invoke(Msg("jobs.msg.notesSaved"), Title("common.msg.title.saved"));
    }

    [RelayCommand(CanExecute = nameof(CanRunSelectedJob))]
    private void SaveJobTags()
    {
        if (SelectedJob is null)
            return;

        var jobId = SelectedJob.Id;
        SelectedJob = _app.JobRunner.SaveJobTags(jobId, JobTagsText);
        RefreshJobsCore();
        SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId);
        JobTagsText = SelectedJob?.Tags is { Count: > 0 } tags
            ? string.Join(", ", tags)
            : string.Empty;
        MessageRequested?.Invoke(Msg("jobs.msg.tagsSaved"), Title("common.msg.title.saved"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task BatchAppendTagsAsync()
    {
        if (string.IsNullOrWhiteSpace(JobTagsText))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.batchTagsFillFirst"), Title("jobs.msg.title.batchTags"));
            return;
        }

        var candidateCount = Jobs.Count;
        if (candidateCount == 0)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.batchTagsEmptyFilter"), Title("jobs.msg.title.batchTags"));
            return;
        }

        if (ConfirmBatchTagsRequested is not null)
        {
            var confirmed = await ConfirmBatchTagsRequested(candidateCount, true, false);
            if (!confirmed)
                return;
        }

        var jobIds = Jobs.Select(job => job.Id).ToList();
        var result = _app.JobRunner.BatchApplyTags(
            JobTagsText,
            BatchTagMode.Append,
            jobIds: jobIds);
        RefreshJobsCore();
        MessageRequested?.Invoke(
            $"{result.SummaryText}\n{string.Join(Environment.NewLine, result.Messages.Take(5))}",
            Title("jobs.sidebar.batchAppendTags"));
    }

    [RelayCommand(CanExecute = nameof(CanBatchAppendTagsToSelected))]
    private async Task BatchAppendTagsToSelectedAsync()
    {
        if (SelectedJobIds.Count < 2)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.selectAtLeastTwoJobs"), Title("jobs.sidebar.batchAppendTags"));
            return;
        }

        if (string.IsNullOrWhiteSpace(JobTagsText))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.batchTagsFillFirst"), Title("jobs.sidebar.batchAppendTags"));
            return;
        }

        var jobIds = SelectedJobIds.ToList();
        if (ConfirmBatchTagsRequested is not null)
        {
            var confirmed = await ConfirmBatchTagsRequested(jobIds.Count, true, true);
            if (!confirmed)
                return;
        }

        var result = _app.JobRunner.BatchApplyTagsToJobIds(jobIds, JobTagsText, BatchTagMode.Append);
        RefreshJobsCore();
        MessageRequested?.Invoke(
            $"{result.SummaryText}\n{string.Join(Environment.NewLine, result.Messages.Take(5))}",
            Title("jobs.sidebar.batchAppendTags"));
    }

    private bool CanBatchAppendTagsToSelected()
        => CanRun && SelectedJobIds.Count >= 2;

    [RelayCommand(CanExecute = nameof(CanBatchArchiveSelected))]
    private async Task BatchArchiveSelectedAsync()
    {
        if (SelectedJobIds.Count < 2)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.selectAtLeastTwoJobs"), Title("jobs.sidebar.batchArchiveSelected"));
            return;
        }

        var jobIds = SelectedJobIds.ToList();
        if (ConfirmBatchArchiveSelectedRequested is not null)
        {
            var confirmed = await ConfirmBatchArchiveSelectedRequested(jobIds.Count);
            if (!confirmed)
                return;
        }

        IsBusy = true;
        try
        {
            var result = await Task.Run(() => _app.JobRunner.BatchArchiveJobIds(jobIds));
            _ui.Run(() =>
            {
                SelectedJob = null;
                ClearSelectedJobIds();
                RefreshJobsCore();
                var message = $"批量归档完成：成功 {result.Archived}，跳过 {result.Skipped}";
                if (result.Messages.Count > 0)
                    message += Environment.NewLine + string.Join(Environment.NewLine, result.Messages.Take(8));

                MessageRequested?.Invoke(message, Title("jobs.sidebar.batchArchiveSelected"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.batchArchiveSelectedFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    private bool CanBatchArchiveSelected()
        => CanRun && SelectedJobIds.Count >= 2;

    [RelayCommand(CanExecute = nameof(CanBatchUnarchiveSelected))]
    private async Task BatchUnarchiveSelectedAsync()
    {
        if (SelectedJobIds.Count < 2)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.selectAtLeastTwoJobs"), Title("jobs.sidebar.batchUnarchiveSelected"));
            return;
        }

        var jobIds = SelectedJobIds.ToList();
        if (ConfirmBatchUnarchiveSelectedRequested is not null)
        {
            var confirmed = await ConfirmBatchUnarchiveSelectedRequested(jobIds.Count);
            if (!confirmed)
                return;
        }

        IsBusy = true;
        try
        {
            var result = await Task.Run(() => _app.JobRunner.BatchUnarchiveJobIds(jobIds));
            _ui.Run(() =>
            {
                SelectedJob = null;
                ClearSelectedJobIds();
                RefreshJobsCore();
                var message = $"批量恢复完成：成功 {result.Unarchived}，跳过 {result.Skipped}";
                if (result.Messages.Count > 0)
                    message += Environment.NewLine + string.Join(Environment.NewLine, result.Messages.Take(8));

                MessageRequested?.Invoke(message, Title("jobs.sidebar.batchUnarchiveSelected"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.batchUnarchiveSelectedFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    private bool CanBatchUnarchiveSelected()
        => CanRun && SelectedJobIds.Count >= 2;

    [RelayCommand(CanExecute = nameof(CanBatchDeleteSelected))]
    private async Task BatchDeleteSelectedAsync()
    {
        if (SelectedJobIds.Count < 2)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.selectAtLeastTwoJobs"), Title("jobs.sidebar.batchDeleteSelected"));
            return;
        }

        var jobIds = SelectedJobIds.ToList();
        var preview = _app.JobRunner.PreviewBatchDeleteJobIds(jobIds);
        if (preview.Count == 0)
        {
            MessageRequested?.Invoke(preview.SummaryText, Title("jobs.sidebar.batchDeleteSelected"));
            return;
        }

        JobDeleteOption? option = null;
        if (ConfirmBatchDeleteSelectedRequested is not null)
            option = await ConfirmBatchDeleteSelectedRequested(preview);

        if (option is null or JobDeleteOption.Cancel)
            return;

        var deleteFolders = option is JobDeleteOption.WithFolders or JobDeleteOption.WithFoldersRecycleBin;
        var useRecycleBin = option == JobDeleteOption.WithFoldersRecycleBin;
        IsBusy = true;
        try
        {
            var result = await Task.Run(() =>
                _app.JobRunner.BatchDeleteJobIds(jobIds, deleteFolders, useRecycleBin));
            _ui.Run(() =>
            {
                SelectedJob = null;
                ClearSelectedJobIds();
                RefreshJobsCore();
                var modeText = option switch
                {
                    JobDeleteOption.WithFoldersRecycleBin => "删记录+回收站",
                    JobDeleteOption.WithFolders => "删记录+目录",
                    _ => "仅删记录"
                };
                var message = $"{result.SummaryText}（{modeText}）";
                if (result.Messages.Count > 0)
                    message += Environment.NewLine + string.Join(Environment.NewLine, result.Messages.Take(8));

                MessageRequested?.Invoke(message, Title("jobs.sidebar.batchDeleteSelected"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.batchDeleteSelectedFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    private bool CanBatchDeleteSelected()
        => CanRun && SelectedJobIds.Count >= 2;

    [RelayCommand(CanExecute = nameof(CanRunSelectedJob))]
    private void TogglePinJob()
    {
        if (SelectedJob is null)
            return;

        var jobId = SelectedJob.Id;
        SelectedJob = _app.JobRunner.ToggleJobPin(jobId);
        RefreshJobsCore();
        SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId);
        MessageRequested?.Invoke(
            SelectedJob?.IsPinned == true ? Msg("jobs.msg.jobPinned") : Msg("jobs.msg.jobUnpinned"),
            Title("jobs.msg.title.pin"));
    }

    [RelayCommand(CanExecute = nameof(CanRunSelectedJob))]
    private async Task ParseClipboardLinksAsync()
    {
        if (SelectedJob is null)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.selectJobFirst"), Title("jobs.msg.title.parseClipboard"));
            return;
        }

        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Text))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.clipboardNoText"), Title("jobs.msg.title.parseClipboard"));
            return;
        }

        var text = (await content.GetTextAsync())?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.clipboardEmpty"), Title("jobs.msg.title.parseClipboard"));
            return;
        }

        var parsed = PanLinkParseService.ParseShareText(text);
        var jobId = SelectedJob.Id;
        SelectedJob = _app.JobRunner.ApplyParsedPanLinks(jobId, parsed);
        LoadPublishFields(SelectedJob);
        RefreshJobsCore();
        SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId) ?? SelectedJob;

        var message = parsed.Messages.Count > 0
            ? string.Join(Environment.NewLine, parsed.Messages)
            : parsed.Success ? Msg("jobs.msg.parseLinksApplied") : Msg("jobs.msg.parseLinksNone");
        MessageRequested?.Invoke(message, parsed.Success ? Title("jobs.msg.title.parseClipboard") : Title("jobs.msg.title.parseFailed"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void CopyAllPublishLinks()
    {
        if (SelectedJob is null)
            return;

        var snapshot = _app.JobRunner.GetPublishLinks(SelectedJob.Id);
        if (string.IsNullOrWhiteSpace(snapshot.FormattedText) || snapshot.FormattedText == "(尚未填写发布链接)")
        {
            MessageRequested?.Invoke(Msg("jobs.msg.fillPublishLinksForCopy"), Title("jobs.msg.title.copyFailed"));
            return;
        }

        var package = new DataPackage();
        package.SetText(snapshot.FormattedText);
        Clipboard.SetContent(package);
        MessageRequested?.Invoke(Msg("jobs.msg.allLinksCopied"), Title("common.msg.title.copied"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void CopyJobLog()
    {
        if (SelectedJob is null || string.IsNullOrWhiteSpace(JobLogText))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.noJobLog"), Title("jobs.detail.copyLog"));
            return;
        }

        var package = new DataPackage();
        package.SetText(JobLogText);
        Clipboard.SetContent(package);
        MessageRequested?.Invoke(Msg("jobs.msg.jobLogCopied"), Title("common.msg.title.copied"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void ExportJobLog()
    {
        if (SelectedJob is null || SelectedJob.Log.Count == 0)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.noJobLog"), Title("jobs.detail.exportLog"));
            return;
        }

        var logsDir = Path.Combine(_app.Workspace.GetWorkspaceRoot(), "logs");
        Directory.CreateDirectory(logsDir);
        var path = Path.Combine(logsDir, $"job-{SelectedJob.Paths.Slug}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(path, JobLogText, Encoding.UTF8);
        MessageRequested?.Invoke(Msg("jobs.msg.logExportedFormat", path), Title("jobs.detail.exportLog"));
        OpenFolder(logsDir);
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task BatchRunAutomatableChainAsync()
    {
        var filter = ResolveSelectedFilter();
        var jobCount = JobHealthService.FilterForBatchPipeline(_allJobs, filter).Count;
        if (jobCount == 0)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.noBatchAutoChainJobs"), Title("jobs.sidebar.batchAutoChain"));
            return;
        }

        if (ConfirmBatchAutomatableChainRequested is not null)
        {
            var confirmed = await ConfirmBatchAutomatableChainRequested(jobCount);
            if (!confirmed)
                return;
        }

        IsBusy = true;
        PipelineProgressText = "批量自动链启动...";
        try
        {
            var result = await _app.JobRunner.RunBatchAutomatableChainAsync(
                filter,
                confirmCleanupAsync: OnConfirmCleanupAsync);
            _app.JobRunner.LogActivity("chain", $"批量自动链: 成功 {result.Success}，待手动 {result.StoppedForManual}，失败 {result.Failed}");
            _ui.Run(() =>
            {
                RefreshJobsCore();
                PipelineProgressText = BuildBatchAutomatableChainSummary(result);
                MessageRequested?.Invoke(PipelineProgressText, Title("jobs.sidebar.batchAutoChain"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() =>
            {
                PipelineProgressText = $"批量自动链失败: {ex.Message}";
                MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.batchAutoChainError"));
            });
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task BatchRunPipelineAsync()
    {
        var filter = ResolveSelectedFilter();
        var jobCount = JobHealthService.FilterForBatchPipeline(_allJobs, filter).Count;
        if (jobCount == 0)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.noBatchPipelineJobs"), Title("jobs.sidebar.batchPipeline"));
            return;
        }

        if (ConfirmBatchPipelineRequested is not null)
        {
            var confirmed = await ConfirmBatchPipelineRequested(jobCount);
            if (!confirmed)
                return;
        }

        IsBusy = true;
        PipelineProgressPercent = 0;
        PipelineProgressText = "批量流水线启动...";
        try
        {
            var result = await _app.JobRunner.RunBatchPipelineAsync(filter, confirmCleanupAsync: OnConfirmCleanupAsync);
            _app.JobRunner.LogActivity("pipeline", $"批量流水线: 成功 {result.Success}，失败 {result.Failed}，跳过 {result.Skipped}");
            _ui.Run(() =>
            {
                RefreshJobsCore();
                var logPath = BatchLogService.WriteLog(
                    _app.Paths.LogsRoot,
                    "pipeline",
                    result.Messages);
                PipelineProgressText = BatchLogService.FormatProgressText(result, logPath);
                MessageRequested?.Invoke(PipelineProgressText, Title("jobs.sidebar.batchPipeline"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() =>
            {
                PipelineProgressText = $"批量失败: {ex.Message}";
                MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.batchPipelineError"));
            });
        }
        finally
        {
            _ui.Run(() =>
            {
                PipelineProgressPercent = 0;
                IsBusy = false;
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RetryJobAsync()
    {
        if (SelectedJob is null || SelectedJob.Status != JobStatus.Failed)
            return;

        IsBusy = true;
        var jobId = SelectedJob.Id;
        StartJobLogPolling(jobId);
        PipelineProgressText = "重试失败任务...";
        try
        {
            PersistJobCompressFlags(SelectedJob);
            var job = await _app.JobRunner.RetryJobAsync(
                jobId,
                OnConfirmCleanupAsync,
                percent: p => _ui.Run(() => PipelineProgressPercent = p));
            _ui.Run(() =>
            {
                SelectedJob = job;
                LoadPublishFields(job);
                RefreshJobsCore();
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId) ?? job;
                PipelineProgressPercent = job.Status == JobStatus.Processed ? 100 : 0;
                PipelineProgressText = job.Status == JobStatus.Failed
                    ? $"重试失败: {job.Error}"
                    : "重试完成";
                if (job.Status == JobStatus.Processed)
                    MessageRequested?.Invoke(Msg("jobs.msg.retrySuccess"), Title("jobs.msg.title.retryDone"));
                else if (job.Status == JobStatus.Failed)
                    MessageRequested?.Invoke(job.Error ?? Msg("jobs.msg.retryFailedFallback"), Title("jobs.msg.title.retryFailed"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() =>
            {
                PipelineProgressText = $"重试错误: {ex.Message}";
                MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.retryError"));
            });
        }
        finally
        {
            StopJobLogPolling();
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void ExportJobJson()
    {
        if (SelectedJob is null)
            return;

        var exportDir = Path.Combine(_app.Workspace.PublishedDirectory, "job-exports");
        Directory.CreateDirectory(exportDir);
        var path = Path.Combine(exportDir, $"{SelectedJob.Paths.Slug}_{SelectedJob.Id}.json");
        var exportPath = _app.JobRunner.ExportJob(SelectedJob.Id, path);
        MessageRequested?.Invoke(Msg("jobs.msg.jobJsonExportedFormat", exportPath), Title("jobs.msg.title.exportJobJson"));
        OpenFolder(exportDir);
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void ExportFilteredJobsCsv()
    {
        var filter = ResolveSelectedFilter();
        var tag = SelectedTagFilter == JobQueryService.AllTagsLabel ? null : SelectedTagFilter;
        var searchText = string.IsNullOrWhiteSpace(JobSearchText) ? null : JobSearchText;
        var export = _app.JobRunner.ExportFilteredJobsCsv(
            filter,
            searchText,
            tag,
            ResolveSelectedSort());
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("jobs.sidebar.exportFilteredCsv"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void ExportFilteredJobsJson()
    {
        var filter = ResolveSelectedFilter();
        var tag = SelectedTagFilter == JobQueryService.AllTagsLabel ? null : SelectedTagFilter;
        var searchText = string.IsNullOrWhiteSpace(JobSearchText) ? null : JobSearchText;
        var export = _app.JobRunner.ExportFilteredJobsJson(
            filter,
            searchText,
            tag,
            ResolveSelectedSort());
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("jobs.sidebar.exportFilteredJson"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void ExportSelectedJobsCsv()
    {
        var jobIds = GetExportJobIds();
        if (jobIds.Count == 0)
        {
            MessageRequested?.Invoke(
                SelectedJobIds.Count > 1
                    ? Msg("jobs.msg.exportCsvNoMultiSelectIds")
                    : Msg("jobs.msg.exportCsvNoJobsInList"),
                Title("jobs.sidebar.exportSelectedCsv"));
            return;
        }

        var export = _app.JobRunner.ExportSelectedJobsCsv(jobIds);
        var selectionHint = SelectedJobIds.Count > 1 ? Msg("jobs.msg.exportSelectionMultiFormat", jobIds.Count) : Msg("jobs.msg.exportSelectionListFormat", jobIds.Count);
        MessageRequested?.Invoke($"{export.SummaryText}{selectionHint}\n{export.ExportPath}", Title("jobs.sidebar.exportSelectedCsv"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand(CanExecute = nameof(CanBatchArchiveFiltered))]
    private async Task BatchArchiveFilteredAsync()
    {
        var filter = ResolveSelectedFilter();
        var tag = SelectedTagFilter == JobQueryService.AllTagsLabel ? null : SelectedTagFilter;
        var searchText = string.IsNullOrWhiteSpace(JobSearchText) ? null : JobSearchText;
        var sort = ResolveSelectedSort();
        var preview = _app.JobRunner.PreviewBatchArchiveFiltered(filter, searchText, tag, sort);

        if (preview.ArchivableCount == 0)
        {
            MessageRequested?.Invoke(preview.SummaryText, Title("jobs.sidebar.batchArchiveFiltered"));
            return;
        }

        if (ConfirmBatchArchiveFilteredRequested is not null)
        {
            var confirmed = await ConfirmBatchArchiveFilteredRequested(preview);
            if (!confirmed)
                return;
        }

        IsBusy = true;
        try
        {
            var result = await Task.Run(() =>
                _app.JobRunner.BatchArchiveFilteredJobs(filter, searchText, tag, sort));
            _ui.Run(() =>
            {
                SelectedJob = null;
                ClearSelectedJobIds();
                RefreshJobsCore();
                var message = $"批量归档完成：成功 {result.Archived}，跳过 {result.Skipped}";
                if (result.Messages.Count > 0)
                    message += Environment.NewLine + string.Join(Environment.NewLine, result.Messages.Take(8));

                MessageRequested?.Invoke(message, Title("jobs.sidebar.batchArchiveFiltered"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.batchArchiveFilteredFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    private bool CanBatchArchiveFiltered()
    {
        if (!CanRun)
            return false;

        var filter = ResolveSelectedFilter();
        var tag = SelectedTagFilter == JobQueryService.AllTagsLabel ? null : SelectedTagFilter;
        var searchText = string.IsNullOrWhiteSpace(JobSearchText) ? null : JobSearchText;
        var preview = _app.JobRunner.PreviewBatchArchiveFiltered(
            filter,
            searchText,
            tag,
            ResolveSelectedSort());
        return preview.ArchivableCount > 0;
    }

    [RelayCommand(CanExecute = nameof(CanBatchPinFiltered))]
    private async Task BatchPinFilteredAsync()
        => await BatchPinFilteredInternalAsync(pin: true);

    [RelayCommand(CanExecute = nameof(CanBatchUnpinFiltered))]
    private async Task BatchUnpinFilteredAsync()
        => await BatchPinFilteredInternalAsync(pin: false);

    private async Task BatchPinFilteredInternalAsync(bool pin)
    {
        var filter = ResolveSelectedFilter();
        var tag = SelectedTagFilter == JobQueryService.AllTagsLabel ? null : SelectedTagFilter;
        var searchText = string.IsNullOrWhiteSpace(JobSearchText) ? null : JobSearchText;
        var preview = _app.JobRunner.PreviewBatchPinFiltered(filter, searchText, tag, pin);

        if (preview.ApplicableCount == 0)
        {
            MessageRequested?.Invoke(preview.SummaryText, pin ? Title("jobs.sidebar.batchPinFiltered") : Title("jobs.sidebar.batchUnpinFiltered"));
            return;
        }

        if (ConfirmBatchPinFilteredRequested is not null)
        {
            var confirmed = await ConfirmBatchPinFilteredRequested(preview);
            if (!confirmed)
                return;
        }

        IsBusy = true;
        try
        {
            var result = await Task.Run(() =>
                _app.JobRunner.BatchPinFilteredJobs(filter, searchText, tag, pin));
            _ui.Run(() =>
            {
                RefreshJobsCore();
                var title = pin ? Title("jobs.sidebar.batchPinFiltered") : Title("jobs.sidebar.batchUnpinFiltered");
                var message = $"{result.SummaryText}";
                if (result.Messages.Count > 0)
                    message += Environment.NewLine + string.Join(Environment.NewLine, result.Messages.Take(8));

                MessageRequested?.Invoke(message, title);
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, pin ? Title("jobs.msg.title.batchPinFilteredFailed") : Title("jobs.msg.title.batchUnpinFilteredFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    private bool CanBatchPinFiltered()
        => CanRun && GetBatchPinPreview(pin: true).ApplicableCount > 0;

    private bool CanBatchUnpinFiltered()
        => CanRun && GetBatchPinPreview(pin: false).ApplicableCount > 0;

    private BatchPinFilteredPreviewResult GetBatchPinPreview(bool pin)
    {
        var filter = ResolveSelectedFilter();
        var tag = SelectedTagFilter == JobQueryService.AllTagsLabel ? null : SelectedTagFilter;
        var searchText = string.IsNullOrWhiteSpace(JobSearchText) ? null : JobSearchText;
        return _app.JobRunner.PreviewBatchPinFiltered(filter, searchText, tag, pin);
    }

    public void SyncSelectedJobIds(IEnumerable<string> jobIds)
    {
        SelectedJobIds.Clear();
        var ids = jobIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count > 1)
        {
            foreach (var jobId in ids)
                SelectedJobIds.Add(jobId);
        }

        BatchArchiveFilteredCommand.NotifyCanExecuteChanged();
        BatchPinFilteredCommand.NotifyCanExecuteChanged();
        BatchUnpinFilteredCommand.NotifyCanExecuteChanged();
        ExportSelectedJobsCsvCommand.NotifyCanExecuteChanged();
        BatchAppendTagsToSelectedCommand.NotifyCanExecuteChanged();
        BatchArchiveSelectedCommand.NotifyCanExecuteChanged();
        BatchUnarchiveSelectedCommand.NotifyCanExecuteChanged();
        BatchDeleteSelectedCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasMultiJobSelection));
        OnPropertyChanged(nameof(HasJobSelection));
        OnPropertyChanged(nameof(JobsMultiSelectHint));
    }

    public void ClearSelectedJobIds()
    {
        if (SelectedJobIds.Count == 0)
            return;

        SelectedJobIds.Clear();
        BatchArchiveFilteredCommand.NotifyCanExecuteChanged();
        BatchPinFilteredCommand.NotifyCanExecuteChanged();
        BatchUnpinFilteredCommand.NotifyCanExecuteChanged();
        ExportSelectedJobsCsvCommand.NotifyCanExecuteChanged();
        BatchAppendTagsToSelectedCommand.NotifyCanExecuteChanged();
        BatchArchiveSelectedCommand.NotifyCanExecuteChanged();
        BatchUnarchiveSelectedCommand.NotifyCanExecuteChanged();
        BatchDeleteSelectedCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasMultiJobSelection));
        OnPropertyChanged(nameof(HasJobSelection));
        OnPropertyChanged(nameof(JobsMultiSelectHint));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void ExportPinnedJobsCsv()
    {
        var export = _app.JobRunner.ExportPinnedJobsCsv();
        MessageRequested?.Invoke($"{export.SummaryText}\n{export.ExportPath}", Title("jobs.sidebar.exportPinnedCsv"));
        OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    private IReadOnlyList<string> GetVisibleJobIds() => Jobs.Select(job => job.Id).ToList();

    private IReadOnlyList<string> GetExportJobIds()
        => SelectedJobIds.Count > 1
            ? SelectedJobIds.ToList()
            : GetVisibleJobIds();

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task ImportJobJsonAsync()
    {
        if (PickImportJsonFileRequested is null)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.pickerNotReady"), Title("common.msg.title.importFailed"));
            return;
        }

        var path = await PickImportJsonFileRequested.Invoke();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var result = await Task.Run(() => _app.JobRunner.ImportJob(path));
            _ui.Run(() =>
            {
                RefreshJobsCore();
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == result.JobId) ?? result.Job;
                RequestWizardFocus();
                MessageRequested?.Invoke(
                    result.Created ? Msg("jobs.msg.importJobCreatedFormat", result.Title) : Msg("jobs.msg.importJobUpdatedFormat", result.Title),
                    Title("jobs.msg.title.importJobJson"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("common.msg.title.importFailed")));
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void ArchiveJob()
    {
        if (SelectedJob is null)
            return;

        var jobId = SelectedJob.Id;
        _app.JobRunner.ArchiveJob(jobId);
        RefreshJobsCore();
        SelectedJob = null;
        MessageRequested?.Invoke(Msg("jobs.msg.jobArchivedHint"), Title("jobs.msg.title.archived"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void DeleteJob()
    {
        if (SelectedJob is null)
            return;

        var title = SelectedJob.Title;
        var jobId = SelectedJob.Id;
        ConfirmDeleteRequested?.Invoke(title, async option =>
        {
            if (option == JobDeleteOption.Cancel)
                return;

            try
            {
                var deleteFolders = option is JobDeleteOption.WithFolders or JobDeleteOption.WithFoldersRecycleBin;
                var useRecycleBin = option == JobDeleteOption.WithFoldersRecycleBin;
                await Task.Run(() => _app.JobRunner.DeleteJob(jobId, deleteFolders, useRecycleBin));
                _ui.Run(() =>
                {
                    SelectedJob = null;
                    RefreshJobsCore();
                    var message = option switch
                    {
                        JobDeleteOption.WithFoldersRecycleBin => Msg("jobs.msg.deleteRecordRecycleBin"),
                        JobDeleteOption.WithFolders => Msg("jobs.msg.deleteRecordAndFolders"),
                        _ => Msg("jobs.msg.deleteRecordOnly")
                    };
                    MessageRequested?.Invoke(message, Title("jobs.msg.title.deleted"));
                });
            }
            catch (Exception ex)
            {
                _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.deleteFailed")));
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task ProcessAsync()
    {
        if (SelectedJob is null)
            return;

        if (SelectedJob.Status != JobStatus.Extracted)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.extractFirstHint"), Title("jobs.msg.title.extractFirst"));
            return;
        }

        IsBusy = true;
        var jobId = SelectedJob.Id;
        StartJobLogPolling(jobId);
        try
        {
            PersistJobCompressFlags(SelectedJob);
            PipelineProgressPercent = 0;
            PipelineProgressText = "去广告 + 压缩...";
            var job = await Task.Run(() => _app.JobRunner.ProcessAsync(
                jobId,
                OnConfirmCleanupAsync,
                percent: p => _ui.Run(() => PipelineProgressPercent = p)));
            _ui.Run(() =>
            {
                SelectedJob = job;
                LoadPublishFields(job);
                RefreshJobsCore();
                PipelineProgressPercent = job.Status == JobStatus.Processed ? 100 : 0;
                PipelineProgressText = job.Status == JobStatus.Processed ? "处理完成" : string.Empty;
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() =>
            {
                PipelineProgressText = $"处理错误: {ex.Message}";
                JobLogText += $"[错误] {ex.Message}{Environment.NewLine}";
                MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.processError"));
            });
        }
        finally
        {
            StopJobLogPolling();
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void SavePublishLinks()
    {
        if (SelectedJob is null)
            return;

        var jobId = SelectedJob.Id;
        SelectedJob = _app.JobRunner.SavePublishLinks(
            jobId,
            BaiduLink,
            BaiduPassword,
            QuarkLink,
            QuarkPassword,
            TelegramPostLink);
        RefreshJobsCore();

        var warnings = PublishLinkValidator.Validate(SelectedJob.Publish);
        var message = warnings.Count > 0
            ? Msg("jobs.msg.publishLinksSavedWarningsFormat", string.Join("；", warnings))
            : Msg("jobs.msg.publishLinksSaved");
        MessageRequested?.Invoke(message, warnings.Count > 0 ? Title("jobs.msg.title.savedWithWarnings") : Title("common.msg.title.saved"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void MarkBaiduPublished() => MarkChannelPublished("baidu", "百度");

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void MarkQuarkPublished() => MarkChannelPublished("quark", "夸克");

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void MarkTelegramPublished() => MarkChannelPublished("tg", "TG");

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void MarkAllChannelsPublished()
    {
        if (SelectedJob is null)
            return;

        try
        {
            var jobId = SelectedJob.Id;
            SelectedJob = _app.JobRunner.MarkAllChannelsPublished(jobId);
            RefreshJobsCore();
            SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId);
            LoadPublishFields(SelectedJob);
            MessageRequested?.Invoke(Msg("jobs.msg.allChannelsMarkedPublished"), Title("jobs.msg.title.publishDone"));
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.markFailed"));
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void BatchGeneratePublishCopy()
    {
        var filter = ResolveSelectedFilter();
        var templateId = ResolveSelectedTemplateId();
        var result = _app.JobRunner.BatchGenerateCopy(filter, templateId);
        RefreshJobsCore();
        if (SelectedJob is not null)
            SelectedJob = Jobs.FirstOrDefault(j => j.Id == SelectedJob.Id);

        MessageRequested?.Invoke(
            Msg("jobs.msg.batchCopyDoneFormat", result.Success, result.Skipped, result.Failed),
            Title("jobs.msg.title.batchCopy"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void ExportNightlyAutomationScript()
    {
        var export = _app.JobRunner.ExportNightlyAutomationScript();
        MessageRequested?.Invoke(
            $"{export.SummaryText}\nBAT: {export.BatPath}\n\n{export.ReadmeText}",
            Title("common.msg.title.exportNightlyScript"));
        OpenFolder(Path.GetDirectoryName(export.BatPath));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task ImportPanLinksCsvAsync()
    {
        if (PickCsvFileRequested is null)
        {
            MessageRequested?.Invoke(Msg("jobs.msg.pickerNotReady"), Title("jobs.sidebar.importPanLinksCsv"));
            return;
        }

        var path = await PickCsvFileRequested.Invoke();
        if (string.IsNullOrWhiteSpace(path))
            return;

        IsBusy = true;
        try
        {
            var result = await Task.Run(() => _app.JobRunner.ImportPanLinksCsv(path));
            _ui.Run(() =>
            {
                RefreshJobsCore();
                var message = $"导入完成：成功 {result.Applied}，失败 {result.Failed}";
                if (result.Messages.Count > 0)
                    message += Environment.NewLine + string.Join(Environment.NewLine, result.Messages.Take(8));

                MessageRequested?.Invoke(message, result.Success ? Title("jobs.sidebar.importPanLinksCsv") : Title("common.msg.title.importFailed"));
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, Title("common.msg.title.importFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void ExportPublishHistory()
    {
        var export = _app.JobRunner.ExportPublishHistory();
        MessageRequested?.Invoke(
            Msg("jobs.msg.publishHistoryExportedFormat", export.Count, export.ExportPath),
            Title("jobs.msg.title.publishHistory"));
        if (!string.IsNullOrWhiteSpace(export.ExportPath))
            OpenFolder(Path.GetDirectoryName(export.ExportPath));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void SaveTelegramChannel()
    {
        _app.JobRunner.SaveStudioTelegramChannel(TelegramChannelUrl);
        MessageRequested?.Invoke(Msg("jobs.msg.tgChannelSaved"), Title("common.msg.title.saved"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenPublishTemplates()
    {
        var path = _app.PublishTemplates.CatalogPath;
        if (!File.Exists(path))
            _app.PublishTemplates.Save(_app.PublishTemplates.Load());

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void GeneratePublishCopy()
    {
        if (SelectedJob is null)
            return;

        var templateId = ResolveSelectedTemplateId();
        var result = _app.JobRunner.GeneratePublishCopy(SelectedJob.Id, templateId);
        PublishCopyText = result.Text;
        RefreshJobsCore();
        SelectedJob = Jobs.FirstOrDefault(j => j.Id == SelectedJob.Id);

        var hint = result.MissingFields.Count > 0
            ? Msg("jobs.msg.copyGeneratedFormat", string.Join("、", result.MissingFields))
            : Msg("jobs.msg.copyGeneratedReady");
        MessageRequested?.Invoke(hint, Title("jobs.msg.title.publishCopy"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void PreviewPublishTemplate()
    {
        var templateId = ResolveSelectedTemplateId();
        var studio = _app.StudioConfig.Load();
        var result = _app.PublishTemplates.PreviewTemplate(templateId, SelectedJob, studio);
        PublishCopyText = result.Text;

        var hint = SelectedJob is null
            ? Msg("jobs.msg.templatePreviewNoJob")
            : result.MissingFields.Count > 0
                ? Msg("jobs.msg.templatePreviewMissingFormat", string.Join("、", result.MissingFields))
                : Msg("jobs.msg.templatePreviewShown");
        MessageRequested?.Invoke(hint, Title("jobs.detail.previewTemplate"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void CopyPublishCopy()
    {
        if (string.IsNullOrWhiteSpace(PublishCopyText))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.clickGenerateCopyFirst"), Title("jobs.msg.title.copyFailed"));
            return;
        }

        var package = new DataPackage();
        package.SetText(PublishCopyText);
        Clipboard.SetContent(package);
        MessageRequested?.Invoke(Msg("jobs.msg.tgCopyCopied"), Title("common.msg.title.copied"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenPublishedFolder()
    {
        if (SelectedJob is null)
            return;

        var dir = _app.JobRunner.GetPublishedDirectory(SelectedJob.Id);
        Directory.CreateDirectory(dir);
        OpenFolder(dir);
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void SavePassword()
    {
        if (SelectedJob is null)
            return;

        PersistArchivePassword(SelectedJob);
        RefreshJobsCore();
        MessageRequested?.Invoke(Msg("jobs.msg.archivePasswordSaved"), Title("common.msg.title.saved"));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenInbox() => OpenFolder(SelectedJob?.Paths.Inbox);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenExtract() => OpenFolder(SelectedJob?.Paths.Extract);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenOutput() => OpenFolder(SelectedJob?.Paths.Output);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenWorkspace() => OpenFolder(_app.Workspace.GetWorkspaceRoot());

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenThread()
    {
        var url = SelectedJob?.Source.ThreadUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.noThreadLink"), Title("jobs.detail.openThread"));
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenBaiduLink()
    {
        if (string.IsNullOrWhiteSpace(BaiduLink))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.fillBaiduLinkFirst"), Title("jobs.detail.openBaidu"));
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = BaiduLink.Trim(), UseShellExecute = true });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenQuarkLink()
    {
        if (string.IsNullOrWhiteSpace(QuarkLink))
        {
            MessageRequested?.Invoke(Msg("jobs.msg.fillQuarkLinkFirst"), Title("jobs.detail.openQuark"));
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = QuarkLink.Trim(), UseShellExecute = true });
    }

    private void PersistArchivePassword(PublishJob job)
    {
        job.Source.ArchivePassword = ArchivePassword.Trim();
        _app.Jobs.Save(job);
    }

    private void PersistJobCompressFlags(PublishJob? job = null)
    {
        job ??= SelectedJob;
        if (job is null)
            return;

        job.Enable7z = Enable7z;
        job.EnableTarZst = EnableTarZst;
        _app.Jobs.Save(job);
    }

    private Task<bool> OnConfirmCleanupAsync(CleanupConfirmation confirmation)
    {
        var tcs = new TaskCompletionSource<bool>();
        ConfirmCleanupRequested?.Invoke(confirmation, tcs);
        return tcs.Task;
    }

    private void LoadPublishTemplates()
    {
        PublishTemplateNames.Clear();
        _templateNameToId.Clear();
        foreach (var template in _app.PublishTemplates.Load().Templates)
        {
            PublishTemplateNames.Add(template.Name);
            _templateNameToId[template.Name] = template.Id;
        }

        if (PublishTemplateNames.Count > 0)
            SelectedPublishTemplate = PublishTemplateNames[0];
    }

    private void LoadJobFilters()
    {
        JobFilterOptions.Clear();
        _filterLabelToValue.Clear();
        foreach (var (filter, label) in PublishDashboardService.FilterOptions)
        {
            JobFilterOptions.Add(label);
            _filterLabelToValue[label] = filter;
        }

        var prefs = _app.UiPreferences.Load();
        var restored = prefs.LastJobFilter?.Trim();
        SelectedJobFilter = !string.IsNullOrWhiteSpace(restored) && _filterLabelToValue.ContainsKey(restored)
            ? restored
            : DefaultJobFilter;
    }

    private void LoadJobSortOptions()
    {
        JobSortOptions.Clear();
        _sortLabelToValue.Clear();
        foreach (var (order, label) in new (JobSortOrder Order, string Label)[]
        {
            (JobSortOrder.PinnedFirst, "置顶优先"),
            (JobSortOrder.UpdatedDesc, "最近更新"),
            (JobSortOrder.UpdatedAsc, "最早更新"),
            (JobSortOrder.TitleAsc, "标题A-Z"),
            (JobSortOrder.TitleDesc, "标题Z-A")
        })
        {
            JobSortOptions.Add(label);
            _sortLabelToValue[label] = order;
        }

        SelectedJobSort = DefaultJobSort;
    }

    [RelayCommand]
    private void ClearFilters()
    {
        _suppressJobFilterApply = true;
        try
        {
            JobSearchText = string.Empty;
            SelectedJobFilter = DefaultJobFilter;
            SelectedJobSort = DefaultJobSort;
            SelectedTagFilter = JobQueryService.AllTagsLabel;
        }
        finally
        {
            _suppressJobFilterApply = false;
        }

        ApplyJobFilter();
    }

    private void LoadTagFilterOptions()
    {
        var previous = SelectedTagFilter;
        var tags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var job in _allJobs)
        {
            foreach (var tag in job.Tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                    tags.Add(tag.Trim());
            }
        }

        TagFilterOptions.Clear();
        TagFilterOptions.Add(JobQueryService.AllTagsLabel);
        foreach (var tag in tags)
            TagFilterOptions.Add(tag);

        SelectedTagFilter = TagFilterOptions.Contains(previous) ? previous : JobQueryService.AllTagsLabel;
    }

    private async Task DebounceApplyJobFilterAsync()
    {
        _jobSearchDebounceCts?.Cancel();
        _jobSearchDebounceCts?.Dispose();
        _jobSearchDebounceCts = new CancellationTokenSource();
        var token = _jobSearchDebounceCts.Token;

        try
        {
            await Task.Delay(JobSearchDebounceMs, token);
            _ui.Run(ApplyJobFilter);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ApplyJobFilter()
    {
        var selectedId = SelectedJob?.Id;
        var filter = ResolveSelectedFilter();
        var filtered = PublishDashboardService.Filter(_allJobs, filter);
        var tag = SelectedTagFilter == JobQueryService.AllTagsLabel ? null : SelectedTagFilter;
        var tagged = JobQueryService.QueryJobsByTag(filtered, tag);
        var searched = JobQueryService.Search(tagged, JobSearchText);
        var sorted = JobQueryService.Sort(searched, ResolveSelectedSort());

        RebuildJobsCollection(sorted, selectedId);

        ClearSelectedJobIds();
        OnPropertyChanged(nameof(IsJobsEmpty));
        BatchArchiveFilteredCommand.NotifyCanExecuteChanged();
        BatchArchiveSelectedCommand.NotifyCanExecuteChanged();
        BatchUnarchiveSelectedCommand.NotifyCanExecuteChanged();
        BatchDeleteSelectedCommand.NotifyCanExecuteChanged();
        BatchPinFilteredCommand.NotifyCanExecuteChanged();
        BatchUnpinFilteredCommand.NotifyCanExecuteChanged();
        NotifyJobListEmptyStateProperties();
        PersistJobFilterPreference();
    }

    private void NotifyJobListEmptyStateProperties()
    {
        OnPropertyChanged(nameof(IsWorkspaceJobsEmpty));
        OnPropertyChanged(nameof(IsFilterEmpty));
        OnPropertyChanged(nameof(IsFilterActive));
        OnPropertyChanged(nameof(IsJobsEmpty));
        OnPropertyChanged(nameof(JobsListCountText));
        OnPropertyChanged(nameof(JobsMultiSelectHint));
        OnPropertyChanged(nameof(HasJobSelection));
        OnPropertyChanged(nameof(JobsFilterSummaryText));
    }

    private string BuildJobsFilterSummaryText()
    {
        if (!IsFilterActive)
            return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(JobSearchText))
            parts.Add(string.Format(LocalizationService.T("jobs.filterPart.search"), JobSearchText.Trim()));

        if (!string.Equals(SelectedJobFilter, DefaultJobFilter, StringComparison.Ordinal))
            parts.Add(string.Format(LocalizationService.T("jobs.filterPart.status"), SelectedJobFilter));

        if (!string.Equals(SelectedJobSort, DefaultJobSort, StringComparison.Ordinal))
            parts.Add(string.Format(LocalizationService.T("jobs.filterPart.sort"), SelectedJobSort));

        if (!string.Equals(SelectedTagFilter, JobQueryService.AllTagsLabel, StringComparison.Ordinal))
            parts.Add(string.Format(LocalizationService.T("jobs.filterPart.tag"), SelectedTagFilter));

        return parts.Count == 0
            ? string.Empty
            : string.Format(LocalizationService.T("jobs.filterSummary"), string.Join(" · ", parts));
    }

    private void PersistJobFilterPreference()
    {
        var prefs = _app.UiPreferences.Load();
        prefs.LastJobFilter = SelectedJobFilter;
        _app.UiPreferences.Save(prefs);
    }

    private void RebuildJobsCollection(IReadOnlyList<PublishJob> desired, string? preserveSelectedId)
    {
        if (Jobs.Count == desired.Count)
        {
            var unchanged = true;
            for (var i = 0; i < desired.Count; i++)
            {
                if (!ReferenceEquals(Jobs[i], desired[i]))
                {
                    unchanged = false;
                    break;
                }
            }

            if (unchanged)
                return;
        }

        _syncingJobsList = true;
        try
        {
            // Detach ListView selection before Clear — Extended + two-way SelectedItem can throw E_INVALIDARG.
            SelectedJob = null;
            Jobs.Clear();
            foreach (var job in desired)
                Jobs.Add(job);

            if (!string.IsNullOrWhiteSpace(preserveSelectedId))
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == preserveSelectedId);
        }
        catch (ArgumentException)
        {
            Jobs.Clear();
            foreach (var job in desired)
                Jobs.Add(job);

            if (!string.IsNullOrWhiteSpace(preserveSelectedId))
                SelectedJob = Jobs.FirstOrDefault(j => j.Id == preserveSelectedId);
        }
        finally
        {
            // Defer unlock so ListView CollectionChanged/SelectionChanged handlers see the flag.
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() => _syncingJobsList = false);
        }
    }

    private void RefreshJobListDisplayBindings()
    {
        if (Jobs.Count == 0)
            return;

        RebuildJobsCollection(Jobs.ToList(), SelectedJob?.Id);
    }

    private JobSortOrder ResolveSelectedSort()
        => _sortLabelToValue.TryGetValue(SelectedJobSort, out var sort)
            ? sort
            : JobSortOrder.UpdatedDesc;

    private JobListFilter ResolveSelectedFilter()
        => _filterLabelToValue.TryGetValue(SelectedJobFilter, out var filter)
            ? filter
            : JobListFilter.All;

    private void LoadPublishFields(PublishJob? job)
    {
        if (job is null)
        {
            BaiduLink = string.Empty;
            BaiduPassword = string.Empty;
            QuarkLink = string.Empty;
            QuarkPassword = string.Empty;
            TelegramPostLink = string.Empty;
            PublishStatusText = string.Empty;
            PublishCopyText = string.Empty;
            UpdateWizardState(null);
            return;
        }

        BaiduLink = job.Publish.Baidu.Link;
        BaiduPassword = job.Publish.Baidu.Password;
        QuarkLink = job.Publish.Quark.Link;
        QuarkPassword = job.Publish.Quark.Password;
        TelegramPostLink = job.Publish.Telegram.Link;
        PublishStatusText = job.PublishStatusLabel;
        PublishCopyText = job.Publish.GeneratedCopy;

        if (!string.IsNullOrWhiteSpace(job.Publish.TemplateId))
        {
            var name = _templateNameToId.FirstOrDefault(x => x.Value == job.Publish.TemplateId).Key;
            if (!string.IsNullOrWhiteSpace(name))
                SelectedPublishTemplate = name;
        }

        UpdateWizardState(job);
    }

    private string? ResolveSelectedTemplateId()
    {
        if (string.IsNullOrWhiteSpace(SelectedPublishTemplate))
            return null;

        return _templateNameToId.TryGetValue(SelectedPublishTemplate, out var id) ? id : null;
    }

    public void ReloadStudioSettings() => LoadStudioSettings();

    private void LoadStudioSettings()
    {
        var studio = _app.StudioConfig.Load();
        TelegramChannelUrl = studio.TelegramChannelUrl;

        ForumSiteOptions.Clear();
        foreach (var forum in studio.Forums)
        {
            if (!string.IsNullOrWhiteSpace(forum.Name))
                ForumSiteOptions.Add(forum.Name);
        }

        if (ForumSiteOptions.Count == 0)
            ForumSiteOptions.Add("老王论坛");

        if (string.IsNullOrWhiteSpace(SelectedSite) || !ForumSiteOptions.Contains(SelectedSite))
            SelectedSite = ForumSiteOptions[0];
    }

    private void RefreshMergeSourceJobOptions()
    {
        var selectedId = SelectedJob?.Id;
        var previous = SelectedMergeSourceJob;
        MergeSourceJobOptions.Clear();
        _mergeOptionToJobId.Clear();

        foreach (var job in _allJobs)
        {
            if (job.Id == selectedId)
                continue;

            var label = $"{job.Title} ({job.Id[..8]})";
            MergeSourceJobOptions.Add(label);
            _mergeOptionToJobId[label] = job.Id;
        }

        if (!string.IsNullOrWhiteSpace(previous) && _mergeOptionToJobId.ContainsKey(previous))
            SelectedMergeSourceJob = previous;
        else
            SelectedMergeSourceJob = string.Empty;
    }

    private string? ResolveMergeSourceJobId()
    {
        if (!string.IsNullOrWhiteSpace(SelectedMergeSourceJob)
            && _mergeOptionToJobId.TryGetValue(SelectedMergeSourceJob, out var mappedId))
            return mappedId;

        var shortId = ParseMergeSourceJobId(SelectedMergeSourceJob);
        if (string.IsNullOrWhiteSpace(shortId))
            return null;

        return _allJobs.FirstOrDefault(j => j.Id.StartsWith(shortId, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private static string? ParseMergeSourceJobId(string? optionText)
    {
        if (string.IsNullOrWhiteSpace(optionText))
            return null;

        var start = optionText.LastIndexOf('(');
        var end = optionText.LastIndexOf(')');
        if (start < 0 || end <= start)
            return null;

        var shortId = optionText[(start + 1)..end].Trim();
        return string.IsNullOrWhiteSpace(shortId) ? null : shortId;
    }

    private void FillJobCreationFields(PublishJob job)
    {
        NewTitle = job.Title;
        ThreadUrl = job.Source.ThreadUrl ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(job.Source.Site))
            SelectedSite = job.Source.Site;
        ArchivePassword = job.Source.ArchivePassword ?? string.Empty;
    }

    private static string BuildCreateJobFromThreadMessage(CreateJobFromThreadResult result)
    {
        var message = result.Message;
        if (result.DownloadResult is { DownloadedCount: > 0 } download)
            message += $"\n已下载 {download.DownloadedCount} 个附件到 inbox。";
        else if (result.DownloadResult is { SkippedCount: > 0 } skipped)
            message += $"\n跳过 {skipped.SkippedCount} 个附件。";

        return message;
    }

    private static string BuildForumDownloadMessage(ForumDownloadResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Error) && result.DownloadedCount == 0)
            return result.Error;

        var parts = new List<string>();
        if (result.DownloadedCount > 0)
            parts.Add($"已下载 {result.DownloadedCount} 个附件");
        if (result.SkippedCount > 0)
            parts.Add($"跳过 {result.SkippedCount} 个");

        if (parts.Count > 0)
            return string.Join("，", parts) + "。";

        return result.Messages.LastOrDefault() ?? result.Error ?? "下载完成";
    }

    private void MarkChannelPublished(string channel, string label)
    {
        if (SelectedJob is null)
            return;

        try
        {
            var jobId = SelectedJob.Id;
            SelectedJob = _app.JobRunner.MarkChannelPublished(jobId, channel);
            RefreshJobsCore();
            SelectedJob = Jobs.FirstOrDefault(j => j.Id == jobId);
            LoadPublishFields(SelectedJob);

            if (SelectedJob?.Status == JobStatus.Published)
                MessageRequested?.Invoke(Msg("jobs.msg.allChannelsPublishedStatus"), Title("jobs.msg.title.publishDone"));
            else
                MessageRequested?.Invoke(Msg("jobs.msg.channelMarkedFormat", label), Title("jobs.msg.title.marked"));
        }
        catch (Exception ex)
        {
            MessageRequested?.Invoke(ex.Message, Title("jobs.msg.title.markFailed"));
        }
    }

    private void StartJobLogPolling(string jobId)
    {
        StopJobLogPolling();
        _logPollCts = new CancellationTokenSource();
        var token = _logPollCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(800, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var job = _app.Jobs.Get(jobId);
                if (job is null)
                    continue;

                _ui.Run(() =>
                {
                    if (SelectedJob?.Id != jobId)
                        return;

                    var newText = string.Join(Environment.NewLine, job.Log);
                    if (!string.Equals(JobLogText, newText, StringComparison.Ordinal))
                        JobLogText = newText;
                });
            }
        }, token);
    }

    private void StopJobLogPolling()
    {
        if (_logPollCts is null)
            return;

        _logPollCts.Cancel();
        _logPollCts.Dispose();
        _logPollCts = null;
    }

    private void UpdateLogText()
    {
        var newText = SelectedJob is null
            ? string.Empty
            : string.Join(Environment.NewLine, SelectedJob.Log);

        if (!string.Equals(JobLogText, newText, StringComparison.Ordinal))
            JobLogText = newText;
    }

    private void UpdateSelectedJobNextAction(PublishJob? job)
    {
        if (job is null)
        {
            SelectedJobNextActionText = string.Empty;
            return;
        }

        var entry = _app.JobRunner.GetNextActionForJob(job.Id);
        SelectedJobNextActionText = entry is null
            ? string.Empty
            : $"建议：{entry.ActionLabel} — {entry.Reason}";
    }

    private void UpdateWizardState(PublishJob? job)
    {
        if (job is null)
        {
            ResetWizardCompletionTracking();
            WizardSummaryText = LocalizationService.T("jobs.wizard.noJob");
            WizardStepHintText = string.Empty;
            SelectedWizardTabIndex = 0;
            WizardStepProgress = 0;
            WizardSteps.Clear();
            OnPropertyChanged(nameof(HasWizardJob));
            OnPropertyChanged(nameof(ShowWizardStepsContent));
            OnPropertyChanged(nameof(ShowJobDetailExtrasCollapsed));
            WizardPrevStepCommand.NotifyCanExecuteChanged();
            WizardNextStepCommand.NotifyCanExecuteChanged();
            ExecuteWizardStepCommand.NotifyCanExecuteChanged();
            return;
        }

        var state = _app.JobRunner.GetWizardTabState(job.Id);
        DetectAndNotifyWizardStepCompletion(job, state);
        WizardSummaryText = state.SummaryText;
        var currentTitle = LocalizeWizardTabTitle(state.CurrentTabId);
        WizardStepHintText = string.Format(
            LocalizationService.T("jobs.wizard.currentStep"),
            currentTitle);
        var tabIndex = Math.Clamp(state.CurrentTabIndex, 0, 3);
        if (SelectedWizardTabIndex != tabIndex)
            SelectedWizardTabIndex = tabIndex;
        WizardStepProgress = ComputeWizardStepProgress(state);
        RebuildWizardSteps(state);
        OnPropertyChanged(nameof(HasWizardJob));
        OnPropertyChanged(nameof(ShowWizardStepsContent));
        OnPropertyChanged(nameof(ShowJobDetailExtrasCollapsed));
        OnPropertyChanged(nameof(CanWizardGoPrev));
        OnPropertyChanged(nameof(CanWizardGoNext));
        WizardPrevStepCommand.NotifyCanExecuteChanged();
        WizardNextStepCommand.NotifyCanExecuteChanged();
        ExecuteWizardStepCommand.NotifyCanExecuteChanged();

        if (_pendingWizardCompletionAnimationIndex is int completedIndex)
        {
            _pendingWizardCompletionAnimationIndex = null;
            WizardStepCompletedAnimationRequested?.Invoke(completedIndex);
        }
    }

    private void ResetWizardCompletionTracking()
    {
        _wizardTrackedJobId = null;
        _previousWizardCompletedCount = -1;
        _previousWizardStepComplete = [];
    }

    private void DetectAndNotifyWizardStepCompletion(PublishJob job, JobWizardStateSnapshot state)
    {
        var tabs = state.Tabs;
        var newCompletedCount = tabs.Count(tab => tab.IsComplete);
        var newStepComplete = tabs.Select(tab => tab.IsComplete).ToArray();

        if (_wizardTrackedJobId != job.Id)
        {
            _wizardTrackedJobId = job.Id;
            _previousWizardCompletedCount = newCompletedCount;
            _previousWizardStepComplete = newStepComplete;
            return;
        }

        if (_previousWizardCompletedCount < 0)
        {
            _previousWizardCompletedCount = newCompletedCount;
            _previousWizardStepComplete = newStepComplete;
            return;
        }

        var newlyCompletedIndex = -1;
        for (var i = 0; i < newStepComplete.Length; i++)
        {
            if (newStepComplete[i] && (i >= _previousWizardStepComplete.Length || !_previousWizardStepComplete[i]))
            {
                newlyCompletedIndex = i;
                break;
            }
        }

        var progressAdvanced = newCompletedCount > _previousWizardCompletedCount;
        if (progressAdvanced || newlyCompletedIndex >= 0)
            _pendingWizardCompletionAnimationIndex = newlyCompletedIndex;

        _previousWizardCompletedCount = newCompletedCount;
        _previousWizardStepComplete = newStepComplete;
    }

    private void RebuildWizardSteps(JobWizardStateSnapshot state)
    {
        var tabs = state.Tabs;
        WizardSteps.Clear();
        var order = 1;
        foreach (var tab in tabs)
        {
            var status = tab.IsComplete
                ? WizardStepStatus.Done
                : tab.IsCurrent
                    ? WizardStepStatus.Active
                    : WizardStepStatus.Pending;

            WizardSteps.Add(new JobWizardStepItem
            {
                Order = order++,
                TabIndex = tab.Index,
                TabId = tab.Id,
                Title = LocalizeWizardTabTitle(tab.Id),
                Description = tab.Summary,
                Status = status,
                StatusText = LocalizeWizardStatus(status),
                IsCurrent = tab.IsCurrent,
                IsComplete = tab.IsComplete
            });
        }
    }

    private static double ComputeWizardStepProgress(JobWizardStateSnapshot state)
    {
        if (state.Tabs.Count == 0)
            return 0;

        var completed = state.Tabs.Count(tab => tab.IsComplete);
        return completed * 100.0 / state.Tabs.Count;
    }


    private static string Msg(string key) => LocalizationService.T(key);

    private static string Title(string key) => LocalizationService.T(key);

    private static string Msg(string key, params object[] args) => string.Format(LocalizationService.T(key), args);
    private static string LocalizeWizardTabTitle(string tabId)
        => tabId switch
        {
            "prepare" => LocalizationService.T("wizard.step.prepare"),
            "links" => LocalizationService.T("wizard.step.links"),
            "copy" => LocalizationService.T("wizard.step.copy"),
            "publish" => LocalizationService.T("wizard.step.publish"),
            _ => tabId
        };

    private static string LocalizeWizardStatus(WizardStepStatus status)
        => status switch
        {
            WizardStepStatus.Active => LocalizationService.T("wizard.status.active"),
            WizardStepStatus.Done => LocalizationService.T("wizard.status.done"),
            _ => LocalizationService.T("wizard.status.pending")
        };

    private void ResetJobDetailMoreSettingsCollapse()
    {
        if (ShowJobDetailExtrasCollapsed)
            IsJobDetailMoreSettingsExpanded = false;

        OnPropertyChanged(nameof(ShowJobDetailExtrasCollapsed));
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(JobsWorkbenchTitle));
        OnPropertyChanged(nameof(JobsSimpleHint));
        OnPropertyChanged(nameof(JobsCollapseMoreSettings));
        OnPropertyChanged(nameof(JobsWizardTitle));
        OnPropertyChanged(nameof(JobsWizardHint));
        OnPropertyChanged(nameof(JobsWizardShortcuts));
        OnPropertyChanged(nameof(JobsNextActionTitle));
        OnPropertyChanged(nameof(JobsNextActionFallback));
        OnPropertyChanged(nameof(JobsSelectJobPrompt));
        OnPropertyChanged(nameof(JobsEmptyList));
        OnPropertyChanged(nameof(JobsFilterEmptyHint));
        OnPropertyChanged(nameof(JobsClearFiltersLabel));
        OnPropertyChanged(nameof(JobsFilterSummaryText));
        OnPropertyChanged(nameof(JobsListCountText));
        OnPropertyChanged(nameof(JobsMultiSelectHint));
        OnPropertyChanged(nameof(JobsRefreshingHint));
        OnPropertyChanged(nameof(JobsNextActionLoadingHint));
        OnPropertyChanged(nameof(JobsWizardStepsLoadingHint));
        OnPropertyChanged(nameof(JobsLogTitle));
        OnPropertyChanged(nameof(JobsProShortcutsLine));
        OnPropertyChanged(nameof(JobsProShortcutsTooltip));
        OnPropertyChanged(nameof(JobsSidebarNewGameHeader));
        OnPropertyChanged(nameof(JobsSidebarNewJobPlaceholder));
        OnPropertyChanged(nameof(JobsSidebarThreadUrlHeader));
        OnPropertyChanged(nameof(JobsSidebarPasteLink));
        OnPropertyChanged(nameof(JobsSidebarFetchThreadInfo));
        OnPropertyChanged(nameof(JobsSidebarSourceForum));
        OnPropertyChanged(nameof(JobsSidebarNewJob));
        OnPropertyChanged(nameof(JobsSidebarCreateFromThread));
        OnPropertyChanged(nameof(JobsSidebarSearchJobs));
        OnPropertyChanged(nameof(JobsSidebarSearchPlaceholder));
        OnPropertyChanged(nameof(JobsSidebarJobFilter));
        OnPropertyChanged(nameof(JobsSidebarSort));
        OnPropertyChanged(nameof(JobsSidebarTagFilter));
        OnPropertyChanged(nameof(JobsSidebarBatchAppendTags));
        OnPropertyChanged(nameof(JobsSidebarBatchArchiveSelected));
        OnPropertyChanged(nameof(JobsSidebarBatchUnarchiveSelected));
        OnPropertyChanged(nameof(JobsSidebarBatchDeleteSelected));
        OnPropertyChanged(nameof(JobsSidebarBatchArchiveFiltered));
        OnPropertyChanged(nameof(JobsSidebarBatchPinFiltered));
        OnPropertyChanged(nameof(JobsSidebarBatchUnpinFiltered));
        OnPropertyChanged(nameof(JobsSidebarArchiveJob));
        OnPropertyChanged(nameof(JobsSidebarUnarchiveJob));
        OnPropertyChanged(nameof(JobsSidebarDeleteJob));
        OnPropertyChanged(nameof(JobsSidebarExportJobJson));
        OnPropertyChanged(nameof(JobsSidebarExportFilteredCsv));
        OnPropertyChanged(nameof(JobsSidebarExportFilteredJson));
        OnPropertyChanged(nameof(JobsSidebarExportSelectedCsv));
        OnPropertyChanged(nameof(JobsSidebarExportPinnedCsv));
        OnPropertyChanged(nameof(JobsSidebarImportJobJson));
        OnPropertyChanged(nameof(JobsSidebarImportPanLinksCsv));
        OnPropertyChanged(nameof(JobsSidebarRefreshList));
        OnPropertyChanged(nameof(JobsSidebarExportPublishHistory));
        OnPropertyChanged(nameof(JobsSidebarBatchGenerateCopy));
        OnPropertyChanged(nameof(JobsSidebarBatchPipeline));
        OnPropertyChanged(nameof(JobsSidebarBatchAutoChain));
        OnPropertyChanged(nameof(JobsSidebarOpenWorkspace));
        OnPropertyChanged(nameof(JobsDetailExecuteSuggestedAction));
        OnPropertyChanged(nameof(JobsDetailRunFullPipeline));
        OnPropertyChanged(nameof(JobsDetailAutoRunUntilManual));
        OnPropertyChanged(nameof(JobsDetailJobNotes));
        OnPropertyChanged(nameof(JobsDetailJobNotesPlaceholder));
        OnPropertyChanged(nameof(JobsDetailSaveNotes));
        OnPropertyChanged(nameof(JobsDetailArchivePassword));
        OnPropertyChanged(nameof(JobsDetailArchivePasswordPlaceholder));
        OnPropertyChanged(nameof(JobsDetailSavePassword));
        OnPropertyChanged(nameof(JobsDetailInboxDropHint));
        OnPropertyChanged(nameof(JobsDetailOpenExtract));
        OnPropertyChanged(nameof(JobsDetailOpenOutput));
        OnPropertyChanged(nameof(JobsDetailPasswordLibrary));
        OnPropertyChanged(nameof(JobsDetailPasswordDropHint));
        OnPropertyChanged(nameof(JobsDetailBrowsePasswordLibrary));
        OnPropertyChanged(nameof(JobsDetailScanUpdate));
        OnPropertyChanged(nameof(JobsDetailMatchPassword));
        OnPropertyChanged(nameof(JobsDetailOpenThread));
        OnPropertyChanged(nameof(JobsDetailOpenInbox));
        OnPropertyChanged(nameof(JobsDetailScanInbox));
        OnPropertyChanged(nameof(JobsDetailDownloadThreadAttachments));
        OnPropertyChanged(nameof(JobsDetailExtract));
        OnPropertyChanged(nameof(JobsDetailProcess));
        OnPropertyChanged(nameof(JobsDetailRetryJob));
        OnPropertyChanged(nameof(JobsDetailAdvancedActions));
        OnPropertyChanged(nameof(JobsDetailExportNightlyScript));
        OnPropertyChanged(nameof(JobsDetailMergeSourceJob));
        OnPropertyChanged(nameof(JobsDetailSelectSourceJob));
        OnPropertyChanged(nameof(JobsDetailMergeIntoCurrent));
        OnPropertyChanged(nameof(JobsDetailBaiduLink));
        OnPropertyChanged(nameof(JobsDetailExtractCode));
        OnPropertyChanged(nameof(JobsDetailOpenBaidu));
        OnPropertyChanged(nameof(JobsDetailQuarkLink));
        OnPropertyChanged(nameof(JobsDetailOpenQuark));
        OnPropertyChanged(nameof(JobsDetailTelegramPostLink));
        OnPropertyChanged(nameof(JobsDetailTelegramChannel));
        OnPropertyChanged(nameof(JobsDetailSaveChannel));
        OnPropertyChanged(nameof(JobsDetailTags));
        OnPropertyChanged(nameof(JobsDetailTagsPlaceholder));
        OnPropertyChanged(nameof(JobsDetailSaveTags));
        OnPropertyChanged(nameof(JobsDetailBatchAppend));
        OnPropertyChanged(nameof(JobsDetailPin));
        OnPropertyChanged(nameof(JobsDetailUnpin));
        OnPropertyChanged(nameof(TogglePinJobButtonText));
        OnPropertyChanged(nameof(JobsDetailSaveLinks));
        OnPropertyChanged(nameof(JobsDetailParseClipboardLinks));
        OnPropertyChanged(nameof(JobsDetailCopyAllLinks));
        OnPropertyChanged(nameof(JobsDetailOpenAllLinks));
        OnPropertyChanged(nameof(JobsDetailBlurbTemplate));
        OnPropertyChanged(nameof(JobsDetailGenerateCopy));
        OnPropertyChanged(nameof(JobsDetailPreviewTemplate));
        OnPropertyChanged(nameof(JobsDetailCopyTgCopy));
        OnPropertyChanged(nameof(JobsDetailEditPublishTemplates));
        OnPropertyChanged(nameof(JobsDetailPublishCopyPreview));
        OnPropertyChanged(nameof(JobsDetailMarkAllPublished));
        OnPropertyChanged(nameof(JobsDetailMarkBaiduPublished));
        OnPropertyChanged(nameof(JobsDetailMarkQuarkPublished));
        OnPropertyChanged(nameof(JobsDetailMarkTelegramPublished));
        OnPropertyChanged(nameof(JobsDetailOpenTgChannel));
        OnPropertyChanged(nameof(JobsDetailCopyAndOpenTg));
        OnPropertyChanged(nameof(JobsDetailBotSendToTg));
        OnPropertyChanged(nameof(JobsDetailOpenPublishedFolder));
        OnPropertyChanged(nameof(JobsDetailCopyLog));
        OnPropertyChanged(nameof(JobsDetailExportLog));
        OnPropertyChanged(nameof(SelectedJobDisplayTitle));
        OnPropertyChanged(nameof(SelectedJobNextActionDisplayText));
        OnPropertyChanged(nameof(WizardStepPrepareHint));
        OnPropertyChanged(nameof(WizardStepCopyHint));
        OnPropertyChanged(nameof(ExecuteWizardStepLabel));
        OnPropertyChanged(nameof(WizardPrevLabel));
        OnPropertyChanged(nameof(WizardNextLabel));
        NotifyWizardTabHeaderProperties();
        RefreshJobListDisplayBindings();
        if (SelectedJob is not null)
            PublishStatusText = SelectedJob.PublishStatusLabel;
        UpdateWizardState(SelectedJob);
    }

    private void NotifyWizardTabHeaderProperties()
    {
        OnPropertyChanged(nameof(WizardTabHeaderPrepare));
        OnPropertyChanged(nameof(WizardTabHeaderLinks));
        OnPropertyChanged(nameof(WizardTabHeaderCopy));
        OnPropertyChanged(nameof(WizardTabHeaderPublish));
    }

    private static string BuildThreadInfoFoundMessage(ThreadParseResult result)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.Title))
            parts.Add($"标题（{result.TitleSource}）");
        if (!string.IsNullOrWhiteSpace(result.BaiduLink))
            parts.Add("百度链接");
        if (!string.IsNullOrWhiteSpace(result.BaiduPassword))
            parts.Add("百度提取码");
        if (!string.IsNullOrWhiteSpace(result.QuarkLink))
            parts.Add("夸克链接");
        if (!string.IsNullOrWhiteSpace(result.QuarkPassword))
            parts.Add("夸克提取码");
        if (!string.IsNullOrWhiteSpace(result.ArchivePassword))
            parts.Add("解压密码");
        if (!string.IsNullOrWhiteSpace(result.DownloadHint))
            parts.Add("下载提示");

        return parts.Count > 0
            ? $"已解析: {string.Join("、", parts)}"
            : "帖子已拉取，未识别到可用字段";
    }

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

    private static string BuildTelegramSendMessage(TelegramSendResult result)
    {
        var message = $"已发送到 Telegram（chat={result.ChatId}, message_id={result.MessageId}）";
        if (result.PartsSent > 1)
            message += $"\n文案较长，已分 {result.PartsSent} 条消息发送。";
        if (result.WasTruncated)
            message += "\n注意：原文超过单条上限，已自动分段。";

        return message;
    }

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

    private static string BuildAutomatableChainMessage(WorkflowChainResult result)
    {
        var lines = new List<string> { result.Message };
        foreach (var step in result.Steps)
        {
            var mark = step.Stopped ? "■" : step.Success ? "✓" : "✗";
            lines.Add($"{mark} {step.Step}: {step.Message}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void NotifyJobsCommandsCanExecuteChanged()
    {
        RefreshJobsCommand.NotifyCanExecuteChanged();
        CreateJobCommand.NotifyCanExecuteChanged();
        FetchThreadInfoCommand.NotifyCanExecuteChanged();
        CreateJobFromThreadCommand.NotifyCanExecuteChanged();
        MergeIntoSelectedJobCommand.NotifyCanExecuteChanged();
        OpenTelegramChannelCommand.NotifyCanExecuteChanged();
        SendToTelegramBotCommand.NotifyCanExecuteChanged();
        CopyAndOpenTelegramCommand.NotifyCanExecuteChanged();
        DownloadThreadAttachmentsCommand.NotifyCanExecuteChanged();
        ScanInboxCommand.NotifyCanExecuteChanged();
        MatchPasswordCommand.NotifyCanExecuteChanged();
        OpenPasswordSamplesCommand.NotifyCanExecuteChanged();
        SyncPasswordManifestCommand.NotifyCanExecuteChanged();
        ExtractCommand.NotifyCanExecuteChanged();
        RunFullPipelineCommand.NotifyCanExecuteChanged();
        UnarchiveJobCommand.NotifyCanExecuteChanged();
        SaveNotesCommand.NotifyCanExecuteChanged();
        SaveJobTagsCommand.NotifyCanExecuteChanged();
        BatchAppendTagsCommand.NotifyCanExecuteChanged();
        BatchAppendTagsToSelectedCommand.NotifyCanExecuteChanged();
        BatchArchiveSelectedCommand.NotifyCanExecuteChanged();
        BatchUnarchiveSelectedCommand.NotifyCanExecuteChanged();
        BatchDeleteSelectedCommand.NotifyCanExecuteChanged();
        TogglePinJobCommand.NotifyCanExecuteChanged();
        ParseClipboardLinksCommand.NotifyCanExecuteChanged();
        CopyAllPublishLinksCommand.NotifyCanExecuteChanged();
        BatchRunPipelineCommand.NotifyCanExecuteChanged();
        BatchRunAutomatableChainCommand.NotifyCanExecuteChanged();
        RetryJobCommand.NotifyCanExecuteChanged();
        ExportJobJsonCommand.NotifyCanExecuteChanged();
        ExportFilteredJobsCsvCommand.NotifyCanExecuteChanged();
        ExportFilteredJobsJsonCommand.NotifyCanExecuteChanged();
        ExportSelectedJobsCsvCommand.NotifyCanExecuteChanged();
        BatchArchiveFilteredCommand.NotifyCanExecuteChanged();
        BatchPinFilteredCommand.NotifyCanExecuteChanged();
        BatchUnpinFilteredCommand.NotifyCanExecuteChanged();
        ExportPinnedJobsCsvCommand.NotifyCanExecuteChanged();
        ImportJobJsonCommand.NotifyCanExecuteChanged();
        ImportPanLinksCsvCommand.NotifyCanExecuteChanged();
        ArchiveJobCommand.NotifyCanExecuteChanged();
        DeleteJobCommand.NotifyCanExecuteChanged();
        ProcessCommand.NotifyCanExecuteChanged();
        SavePublishLinksCommand.NotifyCanExecuteChanged();
        MarkBaiduPublishedCommand.NotifyCanExecuteChanged();
        MarkQuarkPublishedCommand.NotifyCanExecuteChanged();
        MarkTelegramPublishedCommand.NotifyCanExecuteChanged();
        MarkAllChannelsPublishedCommand.NotifyCanExecuteChanged();
        BatchGeneratePublishCopyCommand.NotifyCanExecuteChanged();
        ExportNightlyAutomationScriptCommand.NotifyCanExecuteChanged();
        ExportPublishHistoryCommand.NotifyCanExecuteChanged();
        SaveTelegramChannelCommand.NotifyCanExecuteChanged();
        OpenPublishTemplatesCommand.NotifyCanExecuteChanged();
        GeneratePublishCopyCommand.NotifyCanExecuteChanged();
        PreviewPublishTemplateCommand.NotifyCanExecuteChanged();
        CopyPublishCopyCommand.NotifyCanExecuteChanged();
        CopyJobLogCommand.NotifyCanExecuteChanged();
        ExportJobLogCommand.NotifyCanExecuteChanged();
        OpenPublishedFolderCommand.NotifyCanExecuteChanged();
        SavePasswordCommand.NotifyCanExecuteChanged();
        OpenInboxCommand.NotifyCanExecuteChanged();
        OpenExtractCommand.NotifyCanExecuteChanged();
        OpenOutputCommand.NotifyCanExecuteChanged();
        OpenWorkspaceCommand.NotifyCanExecuteChanged();
        OpenThreadCommand.NotifyCanExecuteChanged();
        OpenBaiduLinkCommand.NotifyCanExecuteChanged();
        OpenQuarkLinkCommand.NotifyCanExecuteChanged();
        ExecuteSelectedNextActionCommand.NotifyCanExecuteChanged();
        ExecuteWizardStepCommand.NotifyCanExecuteChanged();
        WizardPrevStepCommand.NotifyCanExecuteChanged();
        WizardNextStepCommand.NotifyCanExecuteChanged();
        PasteThreadUrlCommand.NotifyCanExecuteChanged();
        OpenAllPublishLinksCommand.NotifyCanExecuteChanged();
        RunAutomatableChainCommand.NotifyCanExecuteChanged();
    }

    public void RequestWizardFocus()
    {
        if (!HasWizardJob)
            return;

        WizardStepScrollRequested?.Invoke(SelectedWizardTabIndex);
    }

    public event Action? JobLogScrollToEndRequested;
    public event Action<int>? WizardStepScrollRequested;
    public event Action<int>? WizardStepCompletedAnimationRequested;
    public event Action<string, string>? MessageRequested;
    public event Action<CleanupConfirmation, TaskCompletionSource<bool>>? ConfirmCleanupRequested;
    public event Action<string, Func<JobDeleteOption, Task>>? ConfirmDeleteRequested;
    public event Func<int, Task<bool>>? ConfirmBatchPipelineRequested;
    public event Func<int, Task<bool>>? ConfirmBatchAutomatableChainRequested;
    public event Func<string, Task<bool>>? ConfirmMergeRequested;
    public event Func<Task<string?>>? PickImportJsonFileRequested;
    public event Func<Task<string?>>? PickCsvFileRequested;
    public event Func<int, bool, bool, Task<bool>>? ConfirmBatchTagsRequested;
    public event Func<BatchArchiveFilteredPreviewResult, Task<bool>>? ConfirmBatchArchiveFilteredRequested;
    public event Func<int, Task<bool>>? ConfirmBatchArchiveSelectedRequested;
    public event Func<int, Task<bool>>? ConfirmBatchUnarchiveSelectedRequested;
    public event Func<BatchDeleteJobIdsPreviewResult, Task<JobDeleteOption>>? ConfirmBatchDeleteSelectedRequested;
    public event Func<BatchPinFilteredPreviewResult, Task<bool>>? ConfirmBatchPinFilteredRequested;
    public event Action? JobsRefreshed;
}

public enum JobDeleteOption
{
    Cancel,
    RecordOnly,
    WithFolders,
    WithFoldersRecycleBin
}