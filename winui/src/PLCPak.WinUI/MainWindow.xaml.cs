using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;
using UiPreferencesModel = PLCPak.Core.Models.UiPreferences;
using PLCPak.WinUI.Infrastructure;
using PLCPak.WinUI.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using WinRT.Interop;

namespace PLCPak.WinUI;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    public MainViewModel ViewModel { get; private set; } = null!;
    public JobsViewModel JobsViewModel { get; private set; } = null!;
    public DashboardViewModel DashboardViewModel { get; private set; } = null!;
    public WizardViewModel WizardViewModel { get; private set; } = null!;
    public OperationsViewModel OperationsViewModel { get; private set; } = null!;

    private bool _isGlobalBusy;
    private string _globalBusyText = string.Empty;
    private string _quickStatsText = string.Empty;
    private bool _isProfessionalMode;
    private WorkflowGuideSnapshot _workflowGuide = new();
    private bool _isWorkflowGuideRefreshing;
    private bool _manualGlobalBusy;
    private string _manualGlobalBusyText = string.Empty;
    private string? _manualGlobalBusyTextKey;
    private DispatcherTimer? _infoBarAutoCloseTimer;
    private bool _contentAnimating;
    private bool _initialNavigationComplete;
    private int _titleBarJobCount;

    private static string T(string key, params object[] args)
        => string.Format(LocalizationService.T(key), args);

    public bool IsGlobalBusy
    {
        get => _isGlobalBusy;
        private set
        {
            if (_isGlobalBusy == value)
                return;

            _isGlobalBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GlobalBusyVisibility));
        }
    }

    public string GlobalBusyText
    {
        get => _globalBusyText;
        private set
        {
            if (_globalBusyText == value)
                return;

            _globalBusyText = value;
            OnPropertyChanged();
        }
    }

    public Visibility GlobalBusyVisibility => IsGlobalBusy ? Visibility.Visible : Visibility.Collapsed;

    public bool ShowGlobalPipelineProgress => IsGlobalBusy && JobsViewModel.PipelineProgressPercent > 0;

    public int GlobalPipelinePercent => JobsViewModel.PipelineProgressPercent;

    public string QuickStatsText
    {
        get => _quickStatsText;
        private set
        {
            if (_quickStatsText == value)
                return;

            _quickStatsText = value;
            OnPropertyChanged();
        }
    }

    public bool IsProfessionalMode => _isProfessionalMode;

    public bool IsSimpleMode => !IsProfessionalMode;

    public string ProfessionalModeHint => IsProfessionalMode
        ? LocalizationService.T("mode.proHint")
        : LocalizationService.T("mode.simpleBadge");

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        ThemeService.RegisterThemeRoot(RootGrid);
        TitleBarText.Text = AppVersion.DisplayName;
        ViewModel = new MainViewModel(App.Services);
        JobsViewModel = new JobsViewModel(App.Services);
        DashboardViewModel = new DashboardViewModel(App.Services);
        WizardViewModel = new WizardViewModel(App.Services);
        OperationsViewModel = new OperationsViewModel(App.Services);
        MainPageControl.DataContext = ViewModel;
        JobsPageControl.DataContext = JobsViewModel;
        DashboardPageControl.DataContext = DashboardViewModel;
        WizardPageControl.DataContext = WizardViewModel;
        OperationsPageControl.DataContext = OperationsViewModel;

        ViewModel.SetWindowHandleProvider(() => WindowNative.GetWindowHandle(this));
        ViewModel.MessageRequested += OnMainMessageRequested;
        ViewModel.PreviewRequested += OnPreviewRequested;
        ViewModel.ConfirmCleanupRequested += OnConfirmCleanupRequestedAsync;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.IsBusy))
                UpdateGlobalBusyState();
        };

        JobsViewModel.ConfirmCleanupRequested += OnJobsConfirmCleanupRequestedAsync;
        JobsViewModel.ConfirmDeleteRequested += OnJobsConfirmDeleteRequestedAsync;
        JobsViewModel.ConfirmBatchPipelineRequested += OnConfirmBatchPipelineRequestedAsync;
        JobsViewModel.ConfirmBatchAutomatableChainRequested += OnConfirmBatchAutomatableChainRequestedAsync;
        JobsViewModel.ConfirmMergeRequested += OnConfirmMergeRequestedAsync;
        JobsViewModel.MessageRequested += OnJobsOrDashboardMessageRequested;
        JobsViewModel.PickImportJsonFileRequested += PickImportJsonFileAsync;
        JobsViewModel.PickCsvFileRequested += PickCsvFileAsync;
        JobsViewModel.JobsRefreshed += UpdateQuickStats;
        JobsViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(JobsViewModel.IsBusy)
                or nameof(JobsViewModel.PipelineProgressText)
                or nameof(JobsViewModel.PipelineProgressPercent))
            {
                UpdateGlobalBusyState();
                NotifyGlobalPipelineProgress();
            }

            if (e.PropertyName is nameof(JobsViewModel.IsRefreshingJobs))
                UpdateWorkflowGuideRefreshingState();
        };

        DashboardViewModel.ConfirmCleanupRequested += OnDashboardConfirmCleanupRequestedAsync;
        DashboardViewModel.ConfirmBatchAutomatableChainRequested += OnConfirmDashboardBatchAutomatableChainRequestedAsync;
        DashboardViewModel.MessageRequested += OnJobsOrDashboardMessageRequested;
        DashboardViewModel.PickBackupFileRequested += PickBackupFileAsync;
        DashboardViewModel.ConfirmBatchSendTgPendingRequested += OnConfirmBatchSendTgPendingRequestedAsync;
        DashboardViewModel.ConfirmSendSingleTgRequested += OnConfirmSendSingleTgRequestedAsync;
        DashboardViewModel.PreviewTextRequested += OnDashboardPreviewTextRequested;
        JobsViewModel.ConfirmBatchTagsRequested += OnConfirmBatchTagsRequestedAsync;
        JobsViewModel.ConfirmBatchArchiveFilteredRequested += OnConfirmBatchArchiveFilteredRequestedAsync;
        JobsViewModel.ConfirmBatchArchiveSelectedRequested += OnConfirmBatchArchiveSelectedRequestedAsync;
        JobsViewModel.ConfirmBatchUnarchiveSelectedRequested += OnConfirmBatchUnarchiveSelectedRequestedAsync;
        JobsViewModel.ConfirmBatchDeleteSelectedRequested += OnConfirmBatchDeleteSelectedRequestedAsync;
        JobsViewModel.ConfirmBatchPinFilteredRequested += OnConfirmBatchPinFilteredRequestedAsync;
        DashboardViewModel.ConfirmImportFullBackupRequested += OnConfirmImportFullBackupRequestedAsync;
        DashboardViewModel.NavigateToFilterRequested += filter => NavigateToJobsWithFilter(filter, focusFirstJob: true);
        DashboardViewModel.NavigateToJobRequested += jobId => NavigateToJob(jobId, focusWizard: true);
        DashboardViewModel.NavigateToJobsRequested += ShowJobsMode;
        DashboardViewModel.DashboardRefreshed += UpdateWindowTitleJobCount;
        DashboardPageControl.NavigateToJobRequested += jobId => NavigateToJob(jobId, focusWizard: true);
        DashboardViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DashboardViewModel.IsBusy))
                UpdateGlobalBusyState();
            if (e.PropertyName is nameof(DashboardViewModel.IsRefreshing))
                UpdateWorkflowGuideRefreshingState();
        };

        WizardViewModel.ConfirmCleanupRequested += OnWizardConfirmCleanupRequestedAsync;
        WizardViewModel.MessageRequested += OnJobsOrDashboardMessageRequested;
        WizardViewModel.NavigateToJobRequested += jobId => NavigateToJob(jobId, focusWizard: true);
        WizardViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(WizardViewModel.IsBusy))
                UpdateGlobalBusyState();
            if (e.PropertyName is nameof(WizardViewModel.IsRefreshingWizard))
                UpdateWorkflowGuideRefreshingState();
        };

        OperationsViewModel.MessageRequested += OnJobsOrDashboardMessageRequested;
        // Operations: focus wizard only in simple mode (pro users land on job detail).
        OperationsViewModel.NavigateToJobRequested += jobId => NavigateToJob(jobId, focusWizard: IsSimpleMode);
        OperationsPageControl.NavigateToJobRequested += jobId => NavigateToJob(jobId, focusWizard: IsSimpleMode);
        OperationsViewModel.ConfirmBulkArchivePublishedRequested += OnConfirmBulkArchivePublishedRequestedAsync;
        OperationsViewModel.ConfirmBatchMergeDuplicatesRequested += OnConfirmBatchMergeDuplicatesRequestedAsync;
        OperationsViewModel.ConfirmTrimActivityLogRequested += OnConfirmTrimActivityLogRequestedAsync;
        OperationsViewModel.ConfirmArchiveActivityLogRequested += OnConfirmArchiveActivityLogRequestedAsync;
        OperationsViewModel.ConfirmImportFullBackupRequested += OnConfirmImportFullBackupRequestedAsync;
        OperationsViewModel.PickBackupFileRequested += PickBackupFileAsync;
        OperationsViewModel.ConfirmMachineProfileImportRequested += OnConfirmMachineProfileImportRequestedAsync;
        OperationsViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(OperationsViewModel.IsBusy))
            {
                UpdateGlobalBusyState();
                if (!OperationsViewModel.IsBusy)
                    RefreshWorkflowGuide();
            }

            if (e.PropertyName is nameof(OperationsViewModel.IsRefreshing))
                UpdateWorkflowGuideRefreshingState();
        };

        _isProfessionalMode = App.Services.UiMode.IsProfessionalMode;
        ProfessionalModeSwitch.IsOn = _isProfessionalMode;
        UpdateProfessionalModeUi();
        App.Services.UiMode.ModeChanged += OnUiModeChanged;
        LocalizationService.LanguageChanged += OnLanguageChanged;
        ThemeService.ThemeChanged += OnThemeChanged;
        OnboardingOverlayControl.Completed += OnOnboardingCompleted;

        _globalBusyText = LocalizationService.T("busy.processing");
        _manualGlobalBusyText = LocalizationService.T("busy.processing");
        DashboardViewModel.NavigateToOperationsRequested += ShowOperationsMode;
        DashboardViewModel.NavigateToTgPendingRequested += FocusDashboardTgPending;
        RefreshLocalizedChrome();
        SetTitleBar();
        ApplyTitleBarTheme();
        RootGrid.AllowDrop = true;
        RootGrid.DragOver += MainWindow_DragOver;
        RootGrid.Drop += MainWindow_Drop;
        Activated += MainWindow_Activated;
    }

    public void NavigateToJob(string jobId, string? filter = null, bool focusWizard = false)
    {
        SaveLastSelectedJobId(jobId);
        var openingJobsPage = JobsPageControl.Visibility != Visibility.Visible;
        if (openingJobsPage)
            ShowJobsMode();

        void Complete()
        {
            if (!string.IsNullOrWhiteSpace(filter))
                JobsViewModel.SelectedJobFilter = filter;
            else if (!openingJobsPage)
                JobsViewModel.RefreshJobsCommand.Execute(null);

            var job = JobsViewModel.Jobs.FirstOrDefault(j => j.Id == jobId)
                ?? JobsViewModel.Jobs.FirstOrDefault(j => j.Id.StartsWith(jobId, StringComparison.OrdinalIgnoreCase));

            if (job is not null)
                JobsViewModel.SelectedJob = job;

            if (WizardPageControl.Visibility == Visibility.Visible)
                WizardViewModel.RefreshWizardCommand.Execute(null);

            if (focusWizard)
                ScheduleWizardFocusAfterNavigation();
        }

        if (openingJobsPage)
            DispatcherQueue.TryEnqueue(Complete);
        else
            Complete();
    }

    private void SetSelectedJobDeferred(PublishJob job)
    {
        if (_initialNavigationComplete)
        {
            JobsViewModel.SelectedJob = job;
            return;
        }

        DispatcherQueue.TryEnqueue(() => JobsViewModel.SelectedJob = job);
    }

    private void ScheduleWizardFocusAfterNavigation()
    {
        var timer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(180);
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            JobsViewModel.RequestWizardFocus();
        };
        timer.Start();
    }

    public void NavigateToJobsWithFilter(string filterLabel, bool focusFirstJob = false)
    {
        var openingJobsPage = JobsPageControl.Visibility != Visibility.Visible;
        if (openingJobsPage)
            ShowJobsMode();

        void Complete()
        {
            JobsViewModel.SelectedJobFilter = filterLabel;
            if (!openingJobsPage)
                JobsViewModel.RefreshJobsCommand.Execute(null);

            if (!focusFirstJob || !IsSimpleMode || JobsViewModel.Jobs.Count == 0)
                return;

            JobsViewModel.SelectedJob = JobsViewModel.Jobs[0];

            if (WizardPageControl.Visibility == Visibility.Visible)
                WizardViewModel.RefreshWizardCommand.Execute(null);

            ScheduleWizardFocusAfterNavigation();
        }

        if (openingJobsPage)
            DispatcherQueue.TryEnqueue(Complete);
        else
            Complete();
    }

    private void OnUiModeChanged()
    {
        var mode = App.Services.UiMode.IsProfessionalMode;
        if (_isProfessionalMode == mode)
            return;

        _isProfessionalMode = mode;
        ProfessionalModeSwitch.IsOn = mode;
        ResetSimpleModeHintDismissOnModeSwitch();
        UpdateProfessionalModeUi();
        RedirectIfOnHiddenSimpleModePage();
    }

    private void ProfessionalModeSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle)
            return;

        if (_isProfessionalMode == toggle.IsOn)
            return;

        _isProfessionalMode = toggle.IsOn;
        App.Services.UiMode.SetProfessionalMode(toggle.IsOn);
        ResetSimpleModeHintDismissOnModeSwitch();
        UpdateProfessionalModeUi();
        RedirectIfOnHiddenSimpleModePage();
    }

    private void UpdateProfessionalModeUi()
    {
        ProfessionalModeHintText.Text = ProfessionalModeHint;
        UpdateNavButtonVisibility();
        UpdateSimpleModeHintVisibility();
        UpdateNavHighlight();
        UpdateWorkflowGuideBarEmphasis(_workflowGuide.HasAction);
        OnPropertyChanged(nameof(IsProfessionalMode));
        OnPropertyChanged(nameof(IsSimpleMode));
        OnPropertyChanged(nameof(ProfessionalModeHint));
    }

    private void ResetSimpleModeHintDismissOnModeSwitch()
    {
        var prefs = App.Services.UiPreferences.Load();
        if (!prefs.HasDismissedSimpleModeHint)
            return;

        prefs.HasDismissedSimpleModeHint = false;
        App.Services.UiPreferences.Save(prefs);
    }

    private void UpdateNavButtonVisibility()
    {
        QuickModeBtn.Visibility = Visibility.Visible;
        var showAdvancedNav = IsProfessionalMode;
        var visibility = showAdvancedNav ? Visibility.Visible : Visibility.Collapsed;
        WizardModeBtn.Visibility = visibility;
        OperationsModeBtn.Visibility = visibility;
    }

    private void UpdateSimpleModeHintVisibility()
    {
        if (!IsSimpleMode)
        {
            SimpleModeHintPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var prefs = App.Services.UiPreferences.Load();
        SimpleModeHintPanel.Visibility = prefs.HasDismissedSimpleModeHint
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void SimpleModeHintDismissBtn_Click(object sender, RoutedEventArgs e)
    {
        var prefs = App.Services.UiPreferences.Load();
        prefs.HasDismissedSimpleModeHint = true;
        App.Services.UiPreferences.Save(prefs);
        UpdateSimpleModeHintVisibility();
    }

    private void RedirectIfOnHiddenSimpleModePage()
    {
        if (!IsSimpleMode)
            return;

        var current = GetCurrentNavPage();
        if (current is not ("wizard" or "operations"))
            return;

        var target = _workflowGuide.RecommendedPage is "jobs" or "dashboard"
            ? _workflowGuide.RecommendedPage
            : "jobs";
        NavigateToRecommendedPage(target);
    }

    private string GetCurrentNavPage()
    {
        if (MainPageControl.Visibility == Visibility.Visible)
            return "quick";
        if (JobsPageControl.Visibility == Visibility.Visible)
            return "jobs";
        if (DashboardPageControl.Visibility == Visibility.Visible)
            return "dashboard";
        if (WizardPageControl.Visibility == Visibility.Visible)
            return "wizard";
        if (OperationsPageControl.Visibility == Visibility.Visible)
            return "operations";

        return "quick";
    }

    private void OnLanguageChanged() => RefreshLocalizedChrome();

    private void OnThemeChanged()
    {
        ApplyTitleBarTheme();
        UpdateNavHighlight();
    }

    private void RefreshLocalizedChrome()
    {
        QuickModeBtn.Content = LocalizationService.T("nav.quick");
        JobsModeBtn.Content = LocalizationService.T("nav.jobs");
        DashboardModeBtn.Content = LocalizationService.T("nav.dashboard");
        WizardModeBtn.Content = LocalizationService.T("nav.wizard");
        OperationsModeBtn.Content = LocalizationService.T("nav.operations");
        CommandPaletteBtn.Content = LocalizationService.T("nav.commandPalette");
        HelpBtn.Content = LocalizationService.T("nav.help");
        SettingsBtn.Content = LocalizationService.T("nav.settings");
        ToolTipService.SetToolTip(CommandPaletteBtn, LocalizationService.T("tooltip.nav.commandPalette"));
        ToolTipService.SetToolTip(QuickModeBtn, LocalizationService.T("tooltip.nav.quick"));
        ToolTipService.SetToolTip(JobsModeBtn, LocalizationService.T("tooltip.nav.jobs"));
        ToolTipService.SetToolTip(DashboardModeBtn, LocalizationService.T("tooltip.nav.dashboard"));
        ToolTipService.SetToolTip(WizardModeBtn, LocalizationService.T("tooltip.nav.wizard"));
        ToolTipService.SetToolTip(OperationsModeBtn, LocalizationService.T("tooltip.nav.operations"));
        ToolTipService.SetToolTip(HelpBtn, LocalizationService.T("tooltip.nav.help"));
        ToolTipService.SetToolTip(SettingsBtn, LocalizationService.T("tooltip.nav.settings"));
        ToolTipService.SetToolTip(ProfessionalModeSwitch, LocalizationService.T("tooltip.mode.switch"));
        ToolTipService.SetToolTip(QuickStatsBtn, LocalizationService.T("tooltip.quickStats"));
        ProfessionalModeSwitch.OffContent = LocalizationService.T("mode.simple");
        ProfessionalModeSwitch.OnContent = LocalizationService.T("mode.pro");
        SimpleModeHintText.Text = LocalizationService.T("mode.simpleHint");
        SimpleModeHintDismissBtn.Content = LocalizationService.T("mode.simpleHintDismiss");
        UpdateNavButtonVisibility();
        UpdateSimpleModeHintVisibility();
        WorkflowGuideTitleText.Text = LocalizationService.T("guide.title");
        WorkflowGuideLoadingText.Text = LocalizationService.T("guide.refreshing");
        WorkflowGoJobBtn.Content = LocalizationService.T("guide.openJob");
        WorkflowExecuteBtn.Content = LocalizationService.T("guide.execute");
        ProfessionalModeHintText.Text = ProfessionalModeHint;
        OnPropertyChanged(nameof(ProfessionalModeHint));
        if (_manualGlobalBusyTextKey is not null)
            _manualGlobalBusyText = LocalizationService.T(_manualGlobalBusyTextKey);
        RefreshTitleBar();
        UpdateNavBadges();
        RefreshWorkflowGuide();
    }

    private void RefreshTitleBar()
    {
        UpdateWindowTitleJobCount(_titleBarJobCount);
    }

    private void OnOnboardingCompleted(object? sender, EventArgs e)
    {
        var prefs = App.Services.UiPreferences.Load();
        prefs.HasSeenOnboarding = true;
        prefs.HasSeenWelcome = true;
        App.Services.UiPreferences.Save(prefs);
    }

    private void NotifyGlobalPipelineProgress()
    {
        OnPropertyChanged(nameof(ShowGlobalPipelineProgress));
        OnPropertyChanged(nameof(GlobalPipelinePercent));
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
            return;

        if (!_initialNavigationComplete)
            return;

        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                RootGrid.Focus(FocusState.Programmatic);
            }
            catch (ArgumentException)
            {
                // Focus before visual tree is ready can throw E_INVALIDARG on some builds.
            }
        });
    }

    private void RootGrid_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            if (GlobalInfoBar.IsOpen)
            {
                StopInfoBarAutoCloseTimer();
                GlobalInfoBar.IsOpen = false;
                e.Handled = true;
                return;
            }

            if (OnboardingOverlayControl.Visibility == Visibility.Visible)
            {
                OnboardingOverlayControl.SkipOrFinish();
                e.Handled = true;
                return;
            }
        }

        var prefs = App.Services.UiPreferences.Load();

        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (e.Key == VirtualKey.F5)
        {
            if (TryRefreshVisiblePage(f5Only: true))
            {
                e.Handled = true;
                return;
            }
        }

        if (JobsPageControl.Visibility == Visibility.Visible)
        {
            var alt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (ctrl && alt && !shift)
            {
                switch (e.Key)
                {
                    case VirtualKey.Left:
                        if (JobsViewModel.WizardPrevStepCommand.CanExecute(null))
                            JobsViewModel.WizardPrevStepCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case VirtualKey.Right:
                        if (JobsViewModel.WizardNextStepCommand.CanExecute(null))
                            JobsViewModel.WizardNextStepCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case VirtualKey.Enter:
                        if (JobsViewModel.ExecuteWizardStepCommand.CanExecute(null))
                            JobsViewModel.ExecuteWizardStepCommand.Execute(null);
                        e.Handled = true;
                        return;
                }
            }

            switch (e.Key)
            {
                case VirtualKey.S when ctrl:
                    if (JobsViewModel.SavePublishLinksCommand.CanExecute(null))
                        JobsViewModel.SavePublishLinksCommand.Execute(null);
                    e.Handled = true;
                    return;
                default:
                    if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.JobsExportJobJson))
                    {
                        if (JobsViewModel.ExportJobJsonCommand.CanExecute(null))
                            JobsViewModel.ExportJobJsonCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }

                    if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.JobsExportFilteredCsv))
                    {
                        if (JobsViewModel.ExportFilteredJobsCsvCommand.CanExecute(null))
                            JobsViewModel.ExportFilteredJobsCsvCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }

                    if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.JobsBatchArchiveFiltered))
                    {
                        if (JobsViewModel.BatchArchiveFilteredCommand.CanExecute(null))
                            JobsViewModel.BatchArchiveFilteredCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }

                    if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.JobsBatchArchiveSelected))
                    {
                        if (JobsViewModel.BatchArchiveSelectedCommand.CanExecute(null))
                            JobsViewModel.BatchArchiveSelectedCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }

                    if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.JobsBatchUnarchiveSelected))
                    {
                        if (JobsViewModel.BatchUnarchiveSelectedCommand.CanExecute(null))
                            JobsViewModel.BatchUnarchiveSelectedCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }

                    if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.JobsBatchDeleteSelected))
                    {
                        if (JobsViewModel.BatchDeleteSelectedCommand.CanExecute(null))
                            JobsViewModel.BatchDeleteSelectedCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }

                    if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.JobsBatchAppendTags))
                    {
                        if (JobsViewModel.BatchAppendTagsToSelectedCommand.CanExecute(null))
                            JobsViewModel.BatchAppendTagsToSelectedCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }

                    break;
                case VirtualKey.Enter when ctrl:
                    if (JobsViewModel.RunFullPipelineCommand.CanExecute(null))
                        JobsViewModel.RunFullPipelineCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }

        if (OperationsPageControl.Visibility == Visibility.Visible)
        {
            if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.OpsExportStatsCsv))
            {
                if (OperationsViewModel.ExportActivityLogStatsCsvCommand.CanExecute(null))
                    OperationsViewModel.ExportActivityLogStatsCsvCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.OpsExportStatsHtml))
            {
                if (OperationsViewModel.ExportActivityLogStatsHtmlCommand.CanExecute(null))
                    OperationsViewModel.ExportActivityLogStatsHtmlCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.OpsFilterBatchActivity))
            {
                if (OperationsViewModel.FilterBatchActivityLogsCommand.CanExecute(null))
                    OperationsViewModel.FilterBatchActivityLogsCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.OpsExportBatchStatsJson))
            {
                if (OperationsViewModel.ExportActivityLogBatchStatsCommand.CanExecute(null))
                    OperationsViewModel.ExportActivityLogBatchStatsCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.OpsExportBatchStatsCsv))
            {
                if (OperationsViewModel.ExportActivityLogBatchStatsCsvCommand.CanExecute(null))
                    OperationsViewModel.ExportActivityLogBatchStatsCsvCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.OpsExportBatchStatsAll))
            {
                if (OperationsViewModel.ExportActivityLogBatchStatsAllCommand.CanExecute(null))
                    OperationsViewModel.ExportActivityLogBatchStatsAllCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.OpsLoadMoreActivityLog))
            {
                if (OperationsViewModel.LoadMoreActivityLogCommand.CanExecute(null))
                    OperationsViewModel.LoadMoreActivityLogCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        if (ctrl && shift && e.Key == VirtualKey.P)
        {
            ProfessionalModeSwitch.IsOn = !ProfessionalModeSwitch.IsOn;
            e.Handled = true;
            return;
        }

        if (GlobalShortcutKeyHelper.IsShortcutPressed(e.Key, ctrl, shift, prefs, GlobalShortcutRegistry.GlobalCommandPalette))
        {
            _ = ShowCommandPaletteAsync();
            e.Handled = true;
            return;
        }

        if (ctrl && !shift && e.Key == VirtualKey.R)
        {
            if (TryRefreshVisiblePage())
            {
                e.Handled = true;
                return;
            }
        }

        if (!ctrl)
            return;

        switch (e.Key)
        {
            case VirtualKey.Number1:
                ShowQuickMode();
                e.Handled = true;
                break;
            case VirtualKey.Number2:
                ShowJobsMode();
                e.Handled = true;
                break;
            case VirtualKey.Number3:
                ShowDashboardMode();
                e.Handled = true;
                break;
            case VirtualKey.Number4:
                ShowWizardMode();
                e.Handled = true;
                break;
            case VirtualKey.Number5:
                if (!IsSimpleMode)
                    ShowOperationsMode();
                e.Handled = true;
                break;
        }
    }

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            RestoreNavigationFromPreferences();
            UpdateQuickStats();
            RedirectIfOnHiddenSimpleModePage();
            var prefs = App.Services.UiPreferences.Load();
            var willShowOnboarding = !prefs.HasSeenOnboarding;
            ShowOnboardingIfNeeded();
            if (!willShowOnboarding)
                await ShowWelcomeDialogIfNeededAsync();
        }
        finally
        {
            _initialNavigationComplete = true;
        }

        try
        {
            var localInfo = App.Services.Version.ReadVersionInfo();
            if (string.IsNullOrWhiteSpace(localInfo.ManifestUrl))
                return;

            var result = await new VersionCheckService(App.Services.Paths)
                .CheckAsync(localInfo.ManifestUrl)
                .ConfigureAwait(true);

            if (!result.HasUpdate)
                return;

            var message = result.Message
                ?? T("shell.versionUpdate.message", result.RemoteVersion, result.LocalVersion);
            if (!string.IsNullOrWhiteSpace(localInfo.ReleaseNotes))
                message += Environment.NewLine + localInfo.ReleaseNotes;
            if (!string.IsNullOrWhiteSpace(localInfo.ToolUrl))
                message += Environment.NewLine + T("shell.versionUpdate.download", localInfo.ToolUrl);

            ShowInfoBar(message, LocalizationService.T("feedback.versionUpdate"), InfoBarSeverity.Warning);
        }
        catch
        {
            // 版本检查失败不阻断启动
        }
    }

    private void QuickModeBtn_Click(object sender, RoutedEventArgs e) => ShowQuickMode();

    private void JobsModeBtn_Click(object sender, RoutedEventArgs e) => ShowJobsMode();

    private void DashboardModeBtn_Click(object sender, RoutedEventArgs e) => ShowDashboardMode();

    private void WizardModeBtn_Click(object sender, RoutedEventArgs e) => ShowWizardMode();

    private void OperationsModeBtn_Click(object sender, RoutedEventArgs e) => ShowOperationsMode();

    private async void CommandPaletteBtn_Click(object sender, RoutedEventArgs e)
        => await ShowCommandPaletteAsync();

    private async void HelpBtn_Click(object sender, RoutedEventArgs e)
        => await ShowHelpDialogAsync();

    private async void SettingsBtn_Click(object sender, RoutedEventArgs e)
        => await OpenSettingsAsync();

    private void RestoreNavigationFromPreferences()
    {
        var prefs = App.Services.UiPreferences.Load();
        var rawMode = prefs.LastNavMode?.Trim().ToLowerInvariant();
        var focusWizardOnLoad = rawMode == "wizard";
        switch (NormalizeStartupNavMode(prefs.LastNavMode))
        {
            case "jobs":
                ApplyJobsModeVisibility();
                JobsViewModel.RefreshJobsCommand.Execute(null);
                RestoreLastSelectedJob(prefs);
                if (focusWizardOnLoad)
                    DispatcherQueue.TryEnqueue(FocusWizardForCurrentJob);
                break;
            case "dashboard":
                ApplyDashboardModeVisibility();
                DashboardViewModel.RefreshCommand.Execute(null);
                break;

            case "operations":
                ApplyOperationsModeVisibility();
                OperationsViewModel.RefreshCommand.Execute(null);
                break;
            default:
                ApplyQuickModeVisibility();
                break;
        }
    }

    private string NormalizeStartupNavMode(string? mode)
    {
        mode = mode?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(mode))
            return IsSimpleMode ? "jobs" : "quick";

        if (mode == "wizard")
            return "jobs";

        if (IsSimpleMode && mode == "operations")
            return "jobs";

        return mode;
    }

    private void FocusWizardForCurrentJob()
    {
        var jobId = JobsViewModel.SelectedJob?.Id ?? _workflowGuide.JobId;
        if (!string.IsNullOrWhiteSpace(jobId))
            NavigateToJob(jobId, focusWizard: true);
    }

    private void RestoreLastSelectedJob(UiPreferencesModel prefs)
    {
        if (string.IsNullOrWhiteSpace(prefs.LastSelectedJobId))
            return;

        var jobId = prefs.LastSelectedJobId;
        var job = JobsViewModel.Jobs.FirstOrDefault(j => j.Id == jobId)
            ?? JobsViewModel.Jobs.FirstOrDefault(j => j.Id.StartsWith(jobId, StringComparison.OrdinalIgnoreCase));

        if (job is null)
            return;

        DispatcherQueue.TryEnqueue(() =>
        {
            var restored = JobsViewModel.Jobs.FirstOrDefault(j => j.Id == job.Id)
                ?? JobsViewModel.Jobs.FirstOrDefault(j => j.Id.StartsWith(job.Id, StringComparison.OrdinalIgnoreCase));
            if (restored is not null)
                JobsViewModel.SelectedJob = restored;
        });
    }

    private void ShowQuickMode()
    {
        if (MainPageControl.Visibility == Visibility.Visible)
        {
            UpdateQuickStats();
            return;
        }

        AnimateContentSwitch(() =>
        {
            ApplyQuickModeVisibility();
            SaveNavMode("quick");
            UpdateQuickStats();
        });
    }

    private void ShowJobsMode()
    {
        if (JobsPageControl.Visibility == Visibility.Visible)
        {
            UpdateQuickStats();
            return;
        }

        AnimateContentSwitch(
            () =>
            {
                ApplyJobsModeVisibility();
                SaveNavMode("jobs");
                UpdateQuickStats();
            },
            () => JobsViewModel.RefreshJobsCommand.Execute(null));
    }

    private void ShowDashboardMode()
    {
        if (DashboardPageControl.Visibility == Visibility.Visible)
        {
            UpdateQuickStats();
            return;
        }

        AnimateContentSwitch(
            () =>
            {
                ApplyDashboardModeVisibility();
                SaveNavMode("dashboard");
                UpdateQuickStats();
            },
            () => DashboardViewModel.RefreshCommand.Execute(null));
    }

    private void ShowWizardMode()
    {
        if (JobsPageControl.Visibility == Visibility.Visible)
        {
            SetNavChecked(quick: false, jobs: true, dashboard: false, wizard: false, operations: false);
            FocusWizardForCurrentJob();
            UpdateQuickStats();
            return;
        }

        AnimateContentSwitch(
            () =>
            {
                ApplyJobsModeVisibility();
                SaveNavMode("jobs");
                UpdateQuickStats();
            },
            () =>
            {
                if (JobsViewModel.RefreshJobsCommand.CanExecute(null))
                    JobsViewModel.RefreshJobsCommand.Execute(null);
                FocusWizardForCurrentJob();
            });
    }

    private void ShowOperationsMode()
    {
        if (OperationsPageControl.Visibility == Visibility.Visible)
        {
            UpdateQuickStats();
            return;
        }

        AnimateContentSwitch(
            () =>
            {
                ApplyOperationsModeVisibility();
                SaveNavMode("operations");
                UpdateQuickStats();
            },
            () => OperationsViewModel.RefreshCommand.Execute(null));
    }

    private void QuickStatsBtn_Click(object sender, RoutedEventArgs e) => ShowDashboardMode();

    private void AnimateContentSwitch(Action switchAction, Action? prepareAction = null)
    {
        if (!_initialNavigationComplete || _contentAnimating || UiMotionHelper.ShouldReduceMotion())
        {
            prepareAction?.Invoke();
            switchAction();
            return;
        }

        _contentAnimating = true;
        RunContentOpacityAnimation(ContentScrollViewer.Opacity, 0, 90, EasingMode.EaseIn, () =>
        {
            prepareAction?.Invoke();
            switchAction();
            RunContentOpacityAnimation(0, 1, 110, EasingMode.EaseOut, () => _contentAnimating = false);
        });
    }

    private void RunContentOpacityAnimation(
        double from,
        double to,
        int durationMs,
        EasingMode easingMode,
        Action onCompleted)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = easingMode }
        };
        Storyboard.SetTarget(animation, ContentScrollViewer);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) => onCompleted();
        try
        {
            storyboard.Begin();
        }
        catch (ArgumentException)
        {
            onCompleted();
        }
    }

    private void ApplyQuickModeVisibility()
    {
        MainPageControl.Visibility = Visibility.Visible;
        JobsPageControl.Visibility = Visibility.Collapsed;
        DashboardPageControl.Visibility = Visibility.Collapsed;
        WizardPageControl.Visibility = Visibility.Collapsed;
        OperationsPageControl.Visibility = Visibility.Collapsed;
        SetNavChecked(quick: true, jobs: false, dashboard: false, wizard: false, operations: false);
        UpdateNavHighlight();
    }

    private void ApplyJobsModeVisibility()
    {
        MainPageControl.Visibility = Visibility.Collapsed;
        JobsPageControl.Visibility = Visibility.Visible;
        DashboardPageControl.Visibility = Visibility.Collapsed;
        WizardPageControl.Visibility = Visibility.Collapsed;
        OperationsPageControl.Visibility = Visibility.Collapsed;
        SetNavChecked(quick: false, jobs: true, dashboard: false, wizard: false, operations: false);
        UpdateNavHighlight();
    }

    private void ApplyDashboardModeVisibility()
    {
        MainPageControl.Visibility = Visibility.Collapsed;
        JobsPageControl.Visibility = Visibility.Collapsed;
        DashboardPageControl.Visibility = Visibility.Visible;
        WizardPageControl.Visibility = Visibility.Collapsed;
        OperationsPageControl.Visibility = Visibility.Collapsed;
        SetNavChecked(quick: false, jobs: false, dashboard: true, wizard: false, operations: false);
        UpdateNavHighlight();
    }

    private void ApplyWizardModeVisibility()
    {
        MainPageControl.Visibility = Visibility.Collapsed;
        JobsPageControl.Visibility = Visibility.Collapsed;
        DashboardPageControl.Visibility = Visibility.Collapsed;
        WizardPageControl.Visibility = Visibility.Visible;
        OperationsPageControl.Visibility = Visibility.Collapsed;
        SetNavChecked(quick: false, jobs: false, dashboard: false, wizard: true, operations: false);
        UpdateNavHighlight();
    }

    private void ApplyOperationsModeVisibility()
    {
        MainPageControl.Visibility = Visibility.Collapsed;
        JobsPageControl.Visibility = Visibility.Collapsed;
        DashboardPageControl.Visibility = Visibility.Collapsed;
        WizardPageControl.Visibility = Visibility.Collapsed;
        OperationsPageControl.Visibility = Visibility.Visible;
        SetNavChecked(quick: false, jobs: false, dashboard: false, wizard: false, operations: true);
        UpdateNavHighlight();
    }

    private static void SaveNavMode(string mode)
    {
        var prefs = App.Services.UiPreferences.Load();
        prefs.LastNavMode = mode;
        App.Services.UiPreferences.Save(prefs);
    }

    private static void SaveLastSelectedJobId(string jobId)
    {
        var prefs = App.Services.UiPreferences.Load();
        prefs.LastSelectedJobId = jobId;
        prefs.LastNavMode = "jobs";
        App.Services.UiPreferences.Save(prefs);
    }

    private void UpdateQuickStats()
    {
        var stats = App.Services.JobRunner.GetQuickStatsOneLiner();
        QuickStatsText = string.IsNullOrWhiteSpace(stats)
            ? $"v{AppVersion.Current}"
            : $"v{AppVersion.Current} · {stats}";
        RefreshWorkflowGuide();
    }

    private void UpdateWorkflowGuideRefreshingState()
    {
        var refreshing = JobsViewModel.IsRefreshingJobs
            || DashboardViewModel.IsRefreshing
            || OperationsViewModel.IsRefreshing
            || WizardViewModel.IsRefreshingWizard;

        if (refreshing == _isWorkflowGuideRefreshing)
            return;

        _isWorkflowGuideRefreshing = refreshing;
        if (refreshing)
            ShowWorkflowGuideLoading();
        else
            RefreshWorkflowGuide();
    }

    private void ShowWorkflowGuideLoading()
    {
        WorkflowGuideLoadingText.Text = LocalizationService.T("guide.refreshing");
        WorkflowGuideLoadingPanel.Visibility = Visibility.Visible;
        WorkflowGuideSummaryText.Visibility = Visibility.Collapsed;
        WorkflowGuideReasonText.Visibility = Visibility.Collapsed;
        WorkflowGoPageBtn.IsEnabled = false;
        WorkflowGoJobBtn.IsEnabled = false;
        WorkflowExecuteBtn.IsEnabled = false;
    }

    private void RefreshWorkflowGuide()
    {
        _isWorkflowGuideRefreshing = false;
        WorkflowGuideLoadingPanel.Visibility = Visibility.Collapsed;
        WorkflowGuideSummaryText.Visibility = Visibility.Visible;

        _workflowGuide = App.Services.JobRunner.GetWorkflowGuide();
        var hasAction = _workflowGuide.HasAction;
        WorkflowGuideSummaryText.Text = hasAction
            ? _workflowGuide.SummaryText
            : LocalizationService.T("guide.noTodo");
        WorkflowGuideReasonText.Text = string.IsNullOrWhiteSpace(_workflowGuide.Reason)
            ? string.Empty
            : _workflowGuide.Reason;
        WorkflowGuideReasonText.Visibility = string.IsNullOrWhiteSpace(_workflowGuide.Reason)
            ? Visibility.Collapsed
            : Visibility.Visible;

        WorkflowGoJobBtn.IsEnabled = hasAction;
        WorkflowGoJobBtn.Content = hasAction && !string.IsNullOrWhiteSpace(_workflowGuide.JobTitle)
            ? $"{LocalizationService.T("guide.openJob")} · {_workflowGuide.JobTitle}"
            : LocalizationService.T("guide.openJob");
        WorkflowExecuteBtn.IsEnabled = hasAction
            && _workflowGuide.Action is not JobNextActionType.FillLinks;
        WorkflowExecuteBtn.Content = hasAction && !string.IsNullOrWhiteSpace(_workflowGuide.ActionLabel)
            ? $"{LocalizationService.T("guide.execute")} · {_workflowGuide.ActionLabel}"
            : LocalizationService.T("guide.execute");
        WorkflowExecuteBtn.Visibility = IsSimpleMode && !hasAction
            ? Visibility.Collapsed
            : Visibility.Visible;

        var onRecommendedPage = string.Equals(
            GetCurrentNavPage(),
            _workflowGuide.RecommendedPage,
            StringComparison.OrdinalIgnoreCase);
        WorkflowGoPageBtn.Visibility = onRecommendedPage ? Visibility.Collapsed : Visibility.Visible;
        WorkflowGoPageBtn.Content = $"{LocalizationService.T("guide.goPage")} · {LocalizationService.TNavPage(_workflowGuide.RecommendedPage)}";
        WorkflowGuideBar.Visibility = Visibility.Visible;

        UpdateNavBadges();
        UpdateNavHighlight();
        UpdateWorkflowGuideBarEmphasis(hasAction);
    }

    private void UpdateWorkflowGuideBarEmphasis(bool hasAction)
    {
        if (hasAction)
        {
            WorkflowGuideBar.Opacity = 1;
            WorkflowGuideBar.BorderThickness = new Thickness(2);
            WorkflowGuideBar.BorderBrush = Application.Current?.Resources["PlcAccentBrush"] as Brush
                ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));
            return;
        }

        if (IsSimpleMode)
            WorkflowGuideBar.Opacity = 0.72;
        else
            WorkflowGuideBar.Opacity = 1;

        WorkflowGuideBar.ClearValue(Border.BorderThicknessProperty);
        WorkflowGuideBar.ClearValue(Border.BorderBrushProperty);
    }

    private void UpdateNavBadges()
    {
        JobsModeBtn.Content = _workflowGuide.JobsBadgeCount > 0
            ? $"{LocalizationService.T("nav.jobs")} ({_workflowGuide.JobsBadgeCount})"
            : LocalizationService.T("nav.jobs");
        DashboardModeBtn.Content = _workflowGuide.TgPendingCount > 0
            ? $"{LocalizationService.T("nav.dashboard")} ({_workflowGuide.TgPendingCount})"
            : LocalizationService.T("nav.dashboard");
        OperationsModeBtn.Content = _workflowGuide.StaleCount > 0
            ? $"{LocalizationService.T("nav.operations")} ({_workflowGuide.StaleCount})"
            : LocalizationService.T("nav.operations");
    }

    private void ShowOnboardingIfNeeded()
    {
        var prefs = App.Services.UiPreferences.Load();
        if (prefs.HasSeenOnboarding)
            return;

        OnboardingOverlayControl.Show();
    }

    private void UpdateNavHighlight()
    {
        var clear = NavClearBrush();
        var active = NavActiveBrush();
        var recommended = IsSimpleMode ? NavRecommendBrush() : clear;
        var current = GetCurrentNavPage();
        var suggest = _workflowGuide.RecommendedPage;

        SetNavButtonBackground(QuickModeBtn, "quick", current, suggest, active, recommended, clear);
        SetNavButtonBackground(JobsModeBtn, "jobs", current, suggest, active, recommended, clear);
        SetNavButtonBackground(DashboardModeBtn, "dashboard", current, suggest, active, recommended, clear);
        SetNavButtonBackground(WizardModeBtn, "wizard", current, suggest, active, recommended, clear);
        SetNavButtonBackground(OperationsModeBtn, "operations", current, suggest, active, recommended, clear);
    }

    private static void SetNavButtonBackground(
        ToggleButton button,
        string page,
        string current,
        string suggested,
        Brush active,
        Brush recommended,
        Brush clear)
    {
        if (current == page)
            button.Background = active;
        else if (suggested == page)
            button.Background = recommended;
        else
            button.Background = clear;
    }

    private static Brush NavActiveBrush()
        => Application.Current?.Resources["PlcNavHighlightBrush"] as Brush
           ?? new SolidColorBrush(Windows.UI.Color.FromArgb(0x24, 0x00, 0x78, 0xD4));

    private static Brush NavRecommendBrush()
        => Application.Current?.Resources["PlcAccentSubtleBrush"] as Brush
           ?? new SolidColorBrush(Windows.UI.Color.FromArgb(0x14, 0x00, 0x78, 0xD4));

    private static Brush NavClearBrush()
        => new SolidColorBrush(Colors.Transparent);

    private void WorkflowGoPageBtn_Click(object sender, RoutedEventArgs e)
        => NavigateToRecommendedPage(_workflowGuide.RecommendedPage);

    private void WorkflowGoJobBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_workflowGuide.JobId))
            return;

        NavigateToJob(_workflowGuide.JobId, focusWizard: IsSimpleMode);
    }

    private async void WorkflowExecuteBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_workflowGuide.JobId))
            return;

        if (_workflowGuide.Action == JobNextActionType.FillLinks)
        {
            NavigateToJob(_workflowGuide.JobId, focusWizard: IsSimpleMode);
            ShowInfoBar(
                LocalizationService.T("shell.fillLinks.message"),
                LocalizationService.T("feedback.fillLinks"));
            return;
        }

        try
        {
            SetGlobalBusy(true, textKey: "busy.executing");
            var result = await App.Services.JobRunner.ExecuteNextActionAsync(
                _workflowGuide.JobId,
                action: null,
                confirmCleanupAsync: OnConfirmCleanupRequestedAsync).ConfigureAwait(true);

            if (result.NeedsUserInput)
            {
                ShowInfoBar(result.Message, LocalizationService.T("wizard.page.needsManual"), InfoBarSeverity.Warning);
                NavigateToJob(_workflowGuide.JobId, focusWizard: IsSimpleMode);
                return;
            }

            if (!result.Success)
            {
                ShowInfoBar(result.Error ?? result.Message, LocalizationService.T("wizard.page.executeFailed"), InfoBarSeverity.Error);
                return;
            }

            ShowInfoBar(result.Message, LocalizationService.T("feedback.executionComplete"), InfoBarSeverity.Success);
            JobsViewModel.RefreshJobsCommand.Execute(null);
            DashboardViewModel.RefreshCommand.Execute(null);
            RefreshWorkflowGuide();
        }
        catch (Exception ex)
        {
            ShowInfoBar(ex.Message, LocalizationService.T("wizard.page.executeFailed"), InfoBarSeverity.Error);
        }
        finally
        {
            SetGlobalBusy(false);
        }
    }

    private void NavigateToRecommendedPage(string page)
    {
        switch (page.Trim().ToLowerInvariant())
        {
            case "quick":
                ShowQuickMode();
                break;
            case "jobs":
                ShowJobsMode();
                if (!string.IsNullOrWhiteSpace(_workflowGuide.JobId))
                    NavigateToJob(_workflowGuide.JobId);
                break;
            case "wizard":
                ShowWizardMode();
                break;
            case "operations":
                ShowOperationsMode();
                break;
            default:
                ShowDashboardMode();
                break;
        }
    }

    private void UpdateWindowTitleJobCount(int jobCount)
    {
        _titleBarJobCount = jobCount;
        var title = jobCount > 0
            ? $"{AppVersion.DisplayName} · {T("shell.titleBar.jobCount", jobCount)}"
            : AppVersion.DisplayName;
        TitleBarText.Text = title;
        AppWindow.Title = title;
    }

    private void SetNavChecked(bool quick, bool jobs, bool dashboard, bool wizard, bool operations)
    {
        QuickModeBtn.IsChecked = quick;
        JobsModeBtn.IsChecked = jobs;
        DashboardModeBtn.IsChecked = dashboard;
        WizardModeBtn.IsChecked = wizard;
        OperationsModeBtn.IsChecked = operations;
    }

    private async Task OpenSettingsAsync()
    {
        var dialog = new SettingsDialog { XamlRoot = Content.XamlRoot };
        var studio = App.Services.StudioConfig.Load();
        var previousKeepDays = studio.ActivityLogKeepDays;
        var prefs = App.Services.UiPreferences.Load();
        var originalTheme = prefs.AppTheme;
        var originalLanguage = prefs.UiLanguage;
        dialog.SetConfigPath(App.Services.StudioConfig.ConfigPath);
        dialog.Load(ViewModel.CurrentConfig, studio, prefs);
        dialog.ExportConfigRequested += () => ExportStudioConfigFromDialogAsync(dialog);
        dialog.ImportConfigRequested += () => ImportStudioConfigFromDialogAsync(dialog);
        dialog.ExportShortcutProfileRequested += () => ExportShortcutProfileFromDialogAsync(dialog, prefs);
        dialog.ImportShortcutProfileRequested += () => ImportShortcutProfileFromDialogAsync(dialog, prefs);
        dialog.ReplayOnboardingRequested += (_, _) => OnboardingOverlayControl.Show();
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            ThemeService.Apply(originalTheme);
            LocalizationService.SetLanguage(originalLanguage, persist: false);
            return;
        }

        ViewModel.ApplySettings(dialog.BuildConfig(ViewModel.CurrentConfig));
        App.Services.StudioConfig.Save(dialog.BuildStudioConfig(studio));
        dialog.ApplyUiPreferences(prefs);
        App.Services.UiPreferences.Save(prefs);
        ThemeService.Apply(prefs.AppTheme);
        LocalizationService.SetLanguage(prefs.UiLanguage, persist: false);
        App.Services.UiMode.SetProfessionalMode(prefs.ProfessionalMode);
        RefreshLocalizedChrome();
        JobsViewModel.ReloadStudioSettings();
        OperationsViewModel.SyncActivityLogSinceDaysFromStudio(previousKeepDays);
        OperationsViewModel.RefreshShortcutHints();
        OperationsViewModel.RefreshCore();
        ShowInfoBar(
            LocalizationService.T("feedback.settingsSaved"),
            LocalizationService.T("settings.title"),
            InfoBarSeverity.Success);
    }

    private async Task ExportStudioConfigFromDialogAsync(SettingsDialog dialog)
    {
        var path = await PickExportJsonFileAsync();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var exportPath = App.Services.JobRunner.ExportStudioConfig(path);
            ShowInfoBar(
                T("feedback.exportConfigSuccess", exportPath),
                LocalizationService.T("settings.general.exportConfig"));
        }
        catch (Exception ex)
        {
            ShowInfoBar(ex.Message, LocalizationService.T("feedback.exportConfigFailed"), InfoBarSeverity.Error);
        }
    }

    private async Task ExportShortcutProfileFromDialogAsync(SettingsDialog dialog, UiPreferencesModel prefs)
    {
        var path = await PickExportJsonFileAsync();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            dialog.ApplyUiPreferences(prefs);
            var export = GlobalShortcutProfileService.Export(prefs, path, App.Services.Workspace.GetWorkspaceRoot());
            ShowInfoBar(
                $"{export.SummaryText}\n{export.ExportPath}",
                LocalizationService.T("settings.shortcuts.exportProfile"));
        }
        catch (Exception ex)
        {
            ShowInfoBar(ex.Message, LocalizationService.T("feedback.exportShortcutsFailed"), InfoBarSeverity.Error);
        }
    }

    private async Task ImportShortcutProfileFromDialogAsync(SettingsDialog dialog, UiPreferencesModel prefs)
    {
        var path = await PickBackupFileAsync();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var result = GlobalShortcutProfileService.Import(path, prefs, merge: true);
            if (!result.Success)
            {
                ShowInfoBar(result.Message, LocalizationService.T("feedback.importShortcutsFailed"), InfoBarSeverity.Error);
                return;
            }

            dialog.Load(ViewModel.CurrentConfig, App.Services.StudioConfig.Load(), prefs);
            ShowInfoBar(
                $"{result.SummaryText}\n{LocalizationService.T("feedback.importShortcutsDraftNote")}",
                LocalizationService.T("settings.shortcuts.importProfile"));
        }
        catch (Exception ex)
        {
            ShowInfoBar(ex.Message, LocalizationService.T("feedback.importShortcutsFailed"), InfoBarSeverity.Error);
        }
    }

    private async Task ImportStudioConfigFromDialogAsync(SettingsDialog dialog)
    {
        var path = await PickImportJsonFileAsync();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var result = App.Services.JobRunner.ImportStudioConfig(path);
            if (!result.Success)
            {
                ShowInfoBar(result.Message, LocalizationService.T("feedback.importConfigFailed"), InfoBarSeverity.Error);
                return;
            }

            var imported = App.Services.StudioConfig.Load();
            dialog.Load(ViewModel.CurrentConfig, imported);
            var message = result.Message;
            if (result.ImportedFields.Count > 0)
                message += Environment.NewLine + T("feedback.importConfigFields", string.Join("、", result.ImportedFields));

            ShowInfoBar(message, LocalizationService.T("settings.general.importConfig"));
            JobsViewModel.ReloadStudioSettings();
        }
        catch (Exception ex)
        {
            ShowInfoBar(ex.Message, LocalizationService.T("feedback.importConfigFailed"), InfoBarSeverity.Error);
        }
    }

    private void SetGlobalBusy(bool busy, string? text = null, string? textKey = null)
    {
        _manualGlobalBusy = busy;
        if (textKey is not null)
        {
            _manualGlobalBusyTextKey = textKey;
            _manualGlobalBusyText = LocalizationService.T(textKey);
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            _manualGlobalBusyTextKey = null;
            _manualGlobalBusyText = text;
        }
        else if (!busy)
            _manualGlobalBusyTextKey = null;

        UpdateGlobalBusyState();
    }

    private void UpdateGlobalBusyState()
    {
        var busy = _manualGlobalBusy
            || ViewModel.IsBusy
            || JobsViewModel.IsBusy
            || DashboardViewModel.IsBusy
            || WizardViewModel.IsBusy
            || OperationsViewModel.IsBusy;
        IsGlobalBusy = busy;
        if (_manualGlobalBusy)
        {
            GlobalBusyText = _manualGlobalBusyText;
            return;
        }

        GlobalBusyText = JobsViewModel.IsBusy
            ? (string.IsNullOrWhiteSpace(JobsViewModel.PipelineProgressText)
                ? LocalizationService.T("busy.jobProcessing")
                : JobsViewModel.PipelineProgressText)
            : (string.IsNullOrWhiteSpace(ViewModel.ProgressText)
                ? LocalizationService.T("busy.processing")
                : ViewModel.ProgressText);
        NotifyGlobalPipelineProgress();
    }

    private void SetTitleBar()
    {
        AppWindow.Title = AppVersion.DisplayName;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
    }

    private void ApplyTitleBarTheme()
    {
        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        var dark = ThemeService.CurrentTheme == "dark";
        if (dark)
        {
            var light = Windows.UI.Color.FromArgb(255, 245, 245, 245);
            titleBar.ForegroundColor = light;
            titleBar.InactiveForegroundColor = Windows.UI.Color.FromArgb(255, 176, 176, 176);
            titleBar.ButtonForegroundColor = light;
            titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 200, 200, 200);
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 140, 140, 140);
        }
        else
        {
            var darkText = Windows.UI.Color.FromArgb(255, 26, 26, 26);
            titleBar.ForegroundColor = darkText;
            titleBar.InactiveForegroundColor = Windows.UI.Color.FromArgb(255, 102, 102, 102);
            titleBar.ButtonForegroundColor = darkText;
            titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
            titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 64, 64, 64);
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 140, 140, 140);
        }

        if (Application.Current?.Resources["PlcTitleBarTextBrush"] is Brush titleBrush)
            TitleBarText.Foreground = titleBrush;
    }

    private void FocusDashboardTgPending()
    {
        ShowDashboardMode();
        DashboardPageControl.FocusTgPendingSection();
    }

    private void MainWindow_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems)
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
    }

    private async void MainWindow_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var items = await e.DataView.GetStorageItemsAsync();
        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (paths.Count == 0)
            return;

        if (MainPageControl.Visibility == Visibility.Visible)
            await ViewModel.HandleDroppedPathsAsync(paths);
        else if (JobsPageControl.Visibility == Visibility.Visible)
            await JobsViewModel.ImportInboxFilesAsync(paths);
    }

    private async Task<string?> PickImportJsonFileAsync()
        => await PickJsonFileAsync(forSave: false);

    private async Task<string?> PickBackupFileAsync()
        => await PickJsonFileAsync(forSave: false);

    private async Task<string?> PickCsvFileAsync()
        => await PickCsvFileInternalAsync();

    private async Task<string?> PickExportJsonFileAsync()
        => await PickJsonFileAsync(forSave: true);

    private async Task<string?> PickCsvFileInternalAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".csv");
        InitializePicker(picker);
        var picked = await picker.PickSingleFileAsync();
        return picked?.Path;
    }

    private async Task<string?> PickJsonFileAsync(bool forSave)
    {
        if (forSave)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedFileName = "studio-config-export",
                DefaultFileExtension = ".json"
            };
            picker.FileTypeChoices.Add("JSON", [".json"]);
            InitializePicker(picker);
            var file = await picker.PickSaveFileAsync();
            return file?.Path;
        }

        var openPicker = new Windows.Storage.Pickers.FileOpenPicker();
        openPicker.FileTypeFilter.Add(".json");
        InitializePicker(openPicker);
        var picked = await openPicker.PickSingleFileAsync();
        return picked?.Path;
    }

    private void InitializePicker(Windows.Storage.Pickers.FileOpenPicker picker)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    private void InitializePicker(Windows.Storage.Pickers.FileSavePicker picker)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    private async Task<bool> OnConfirmCleanupRequestedAsync(CleanupConfirmation confirmation)
        => await ShowCleanupDialogAsync(confirmation);

    private void OnJobsConfirmCleanupRequestedAsync(CleanupConfirmation confirmation, TaskCompletionSource<bool> tcs)
        => _ = ConfirmJobsCleanupAsync(confirmation, tcs);

    private void OnDashboardConfirmCleanupRequestedAsync(CleanupConfirmation confirmation, TaskCompletionSource<bool> tcs)
        => _ = ConfirmJobsCleanupAsync(confirmation, tcs);

    private void OnWizardConfirmCleanupRequestedAsync(CleanupConfirmation confirmation, TaskCompletionSource<bool> tcs)
        => _ = ConfirmJobsCleanupAsync(confirmation, tcs);

    private async Task ConfirmJobsCleanupAsync(CleanupConfirmation confirmation, TaskCompletionSource<bool> tcs)
    {
        var result = await ShowCleanupDialogAsync(confirmation);
        tcs.TrySetResult(result);
    }

    private void OnJobsConfirmDeleteRequestedAsync(string title, Func<JobDeleteOption, Task> handler)
        => _ = ConfirmJobsDeleteAsync(title, handler);

    private async Task ConfirmJobsDeleteAsync(string title, Func<JobDeleteOption, Task> handler)
    {
        var (content, getOption) = BuildDeleteOptionSelector(
            $"确认删除「{title}」？",
            "仅删记录：保留 inbox/extract/output 目录");
        var dialog = new ContentDialog
        {
            Title = "删除任务",
            Content = content,
            PrimaryButtonText = "确认删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            await handler(JobDeleteOption.Cancel);
            return;
        }

        await handler(getOption());
    }

    private async Task<bool> OnConfirmBatchPipelineRequestedAsync(int jobCount)
    {
        var dialog = new ContentDialog
        {
            Title = "批量流水线确认",
            Content = new TextBlock
            {
                Text = $"将处理 {jobCount} 个任务，确认？",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private Task<bool> OnConfirmDashboardBatchAutomatableChainRequestedAsync(int jobCount)
        => ConfirmBatchAutomatableChainAsync(jobCount, activeJobs: true);

    private Task<bool> OnConfirmBatchAutomatableChainRequestedAsync(int jobCount)
        => ConfirmBatchAutomatableChainAsync(jobCount, activeJobs: false);

    private async Task<bool> ConfirmBatchAutomatableChainAsync(int jobCount, bool activeJobs)
    {
        var message = activeJobs
            ? $"将对 {jobCount} 个活跃任务执行自动链，确认？"
            : $"将对 {jobCount} 个当前筛选任务执行自动链，确认？";

        var dialog = new ContentDialog
        {
            Title = "批量自动链确认",
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> OnConfirmBatchMergeDuplicatesRequestedAsync(int groupCount, int mergeCount)
    {
        var dialog = new ContentDialog
        {
            Title = "自动合并重复任务",
            Content = new TextBlock
            {
                Text = $"将按建议合并 {groupCount} 组重复任务（约 {mergeCount} 次合并），重复项将归档，确认？",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool?> OnConfirmMachineProfileImportRequestedAsync(MachineProfilePreviewResult preview)
    {
        var message = preview.SummaryText;
        if (preview.HasShortcutProfile)
            message += $"\n快捷键：{preview.ShortcutOverrideCount} 映射，{preview.ShortcutDisabledCount} 禁用";
        if (!string.IsNullOrWhiteSpace(preview.HtmlReportTheme))
            message += $"\nHTML 报告主题：{preview.HtmlReportTheme}";
        if (preview.NightlyExportActivityLogBatchStatsAll)
        {
            var sinceHint = preview.ActivityLogKeepDays > 0
                ? $"（夜间 -SinceDays {preview.ActivityLogKeepDays}）"
                : string.Empty;
            message += $"\n夜间批量统计 JSON+CSV{sinceHint}";
        }
        if (!string.IsNullOrWhiteSpace(preview.OperationsCenterSummary))
            message += $"\n导出时运维快照：{preview.OperationsCenterSummary}";

        message += "\n\n确认合并导入机器配置？";

        var dialog = new ContentDialog
        {
            Title = "导入机器配置",
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认导入",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> OnConfirmTrimActivityLogRequestedAsync(ActivityLogTrimPreviewResult preview)
    {
        var categoryFilter = string.IsNullOrWhiteSpace(OperationsViewModel.ActivityCategoryFilter)
            ? null
            : OperationsViewModel.ActivityCategoryFilter;
        var message = preview.SummaryText;
        if (!string.IsNullOrWhiteSpace(categoryFilter))
            message += $"\n\n分类筛选：{categoryFilter}";

        message += $"\n\n确认删除 {preview.WouldRemoveCount} 条旧记录？";

        var dialog = new ContentDialog
        {
            Title = "清理活动日志",
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> OnConfirmArchiveActivityLogRequestedAsync(ActivityLogTrimPreviewResult preview)
    {
        var message = preview.SummaryText;
        message += $"\n\n确认将 {preview.WouldRemoveCount} 条旧记录归档到 reports/activity-archive-*.json，并从主日志移除？";

        var dialog = new ContentDialog
        {
            Title = "归档活动日志",
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认归档",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> OnConfirmBulkArchivePublishedRequestedAsync(int jobCount)
    {
        var dialog = new ContentDialog
        {
            Title = "批量归档已发布",
            Content = new TextBlock
            {
                Text = $"将归档 {jobCount} 个已发布任务，确认？",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void OnDashboardPreviewTextRequested(string text)
    {
        var dialog = new ContentDialog
        {
            Title = "TG 待发预览",
            Content = new ScrollViewer
            {
                Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                MaxHeight = 420
            },
            CloseButtonText = "关闭",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async Task<bool> OnConfirmBatchArchiveSelectedRequestedAsync(int jobCount)
    {
        var dialog = new ContentDialog
        {
            Title = "批量归档选中",
            Content = new TextBlock
            {
                Text = $"确认归档多选的 {jobCount} 个任务？已归档项将自动跳过。",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认归档",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> OnConfirmBatchUnarchiveSelectedRequestedAsync(int jobCount)
    {
        var dialog = new ContentDialog
        {
            Title = "批量恢复选中",
            Content = new TextBlock
            {
                Text = $"确认恢复多选的 {jobCount} 个任务？未归档项将自动跳过。",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认恢复",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<JobDeleteOption> OnConfirmBatchDeleteSelectedRequestedAsync(
        BatchDeleteJobIdsPreviewResult preview)
    {
        var sampleText = preview.SampleTitles.Count == 0
            ? string.Empty
            : Environment.NewLine + Environment.NewLine
                + "示例任务：" + Environment.NewLine
                + string.Join(Environment.NewLine, preview.SampleTitles.Select(title => $"· {title}"));

        var folderHint = preview.FolderCandidateCount > 0
            ? $"{Environment.NewLine}若选择删记录+目录，将涉及约 {preview.FolderCandidateCount} 个目录路径。"
            : string.Empty;

        var (content, getOption) = BuildDeleteOptionSelector(
            $"{preview.SummaryText}{folderHint}{sampleText}",
            "仅删记录：保留各任务工作目录");

        var dialog = new ContentDialog
        {
            Title = "批量删除选中",
            Content = content,
            PrimaryButtonText = "确认删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return JobDeleteOption.Cancel;

        return getOption();
    }

    private static (UIElement Content, Func<JobDeleteOption> GetOption) BuildDeleteOptionSelector(
        string headerText,
        string recordOnlyHint)
    {
        var recordOnly = new RadioButton
        {
            Content = recordOnlyHint,
            IsChecked = true,
            GroupName = "delete-option"
        };
        var permanent = new RadioButton
        {
            Content = "删记录+永久删除目录",
            GroupName = "delete-option"
        };
        var recycle = new RadioButton
        {
            Content = "删记录+目录移到回收站",
            GroupName = "delete-option"
        };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = headerText,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(recordOnly);
        panel.Children.Add(permanent);
        panel.Children.Add(recycle);

        JobDeleteOption GetOption()
        {
            if (recycle.IsChecked == true)
                return JobDeleteOption.WithFoldersRecycleBin;
            if (permanent.IsChecked == true)
                return JobDeleteOption.WithFolders;
            return JobDeleteOption.RecordOnly;
        }

        return (panel, GetOption);
    }

    private async Task<bool> OnConfirmBatchArchiveFilteredRequestedAsync(BatchArchiveFilteredPreviewResult preview)
    {
        var sampleText = preview.SampleTitles.Count == 0
            ? string.Empty
            : Environment.NewLine + Environment.NewLine
                + "示例任务：" + Environment.NewLine
                + string.Join(Environment.NewLine, preview.SampleTitles.Select(title => $"· {title}"));

        var dialog = new ContentDialog
        {
            Title = "批量归档筛选",
            Content = new TextBlock
            {
                Text = $"{preview.SummaryText}\n\n确认归档 {preview.ArchivableCount} 个任务？{sampleText}",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认归档",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> OnConfirmBatchPinFilteredRequestedAsync(BatchPinFilteredPreviewResult preview)
    {
        var action = preview.Pin ? "置顶" : "取消置顶";
        var sampleText = preview.SampleTitles.Count == 0
            ? string.Empty
            : Environment.NewLine + Environment.NewLine
                + "示例任务：" + Environment.NewLine
                + string.Join(Environment.NewLine, preview.SampleTitles.Select(title => $"· {title}"));

        var dialog = new ContentDialog
        {
            Title = preview.Pin ? "批量置顶筛选" : "批量取消置顶筛选",
            Content = new TextBlock
            {
                Text = $"{preview.SummaryText}\n\n确认{action} {preview.ApplicableCount} 个任务？{sampleText}",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = $"确认{action}",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> OnConfirmBatchTagsRequestedAsync(int jobCount, bool append, bool multiSelect)
    {
        var scopeText = multiSelect
            ? $"多选的 {jobCount} 个任务"
            : $"当前列表中的 {jobCount} 个任务";
        var dialog = new ContentDialog
        {
            Title = append ? "批量追加标签" : "批量设置标签",
            Content = new TextBlock
            {
                Text = $"将向{scopeText}{(append ? "追加" : "设置")}标签，确认？",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> OnConfirmSendSingleTgRequestedAsync(string title)
    {
        var dialog = new ContentDialog
        {
            Title = "发送 TG",
            Content = new TextBlock
            {
                Text = $"将向 Telegram 发送「{title}」的发布文案，确认？",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> OnConfirmBatchSendTgPendingRequestedAsync(int jobCount)
    {
        var dialog = new ContentDialog
        {
            Title = "批量发送 TG",
            Content = new TextBlock
            {
                Text = $"将向 {jobCount} 个任务发送 TG，确认？",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool?> OnConfirmImportFullBackupRequestedAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "恢复全量备份",
            Content = new TextBlock
            {
                Text = "选择导入模式：\n\n合并：保留现有任务，合并 studio-config，跳过重复任务\n替换：清空现有任务与配置后导入",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "合并导入",
            SecondaryButtonText = "替换导入",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        return await dialog.ShowAsync() switch
        {
            ContentDialogResult.Primary => true,
            ContentDialogResult.Secondary => false,
            _ => null
        };
    }

    private List<CommandPaletteItem> BuildCommandPaletteActions()
    {
        static CommandPaletteItem P(string titleKey, string descKey, Action execute)
            => new(titleKey, LocalizationService.T(titleKey), LocalizationService.T(descKey), execute);

        if (IsSimpleMode)
        {
            return
            [
                P("shell.palette.navQuick.title", "shell.palette.navQuick.desc", ShowQuickMode),
                P("shell.palette.navJobs.title", "shell.palette.navJobs.desc", ShowJobsMode),
                P("shell.palette.navDashboard.title", "shell.palette.navDashboard.desc", ShowDashboardMode),
                P("shell.palette.refreshJobs.title", "shell.palette.refreshJobs.desc", () =>
                {
                    if (JobsViewModel.RefreshJobsCommand.CanExecute(null))
                        JobsViewModel.RefreshJobsCommand.Execute(null);
                }),
                P("shell.palette.saveLinks.title", "shell.palette.saveLinks.desc", () =>
                {
                    ShowJobsMode();
                    if (JobsViewModel.SavePublishLinksCommand.CanExecute(null))
                        JobsViewModel.SavePublishLinksCommand.Execute(null);
                }),
                P("shell.palette.fullPipeline.title", "shell.palette.fullPipeline.desc", () =>
                {
                    ShowJobsMode();
                    if (JobsViewModel.RunFullPipelineCommand.CanExecute(null))
                        JobsViewModel.RunFullPipelineCommand.Execute(null);
                }),
                P("shell.palette.previewTgPending.title", "shell.palette.previewTgPending.desc", () =>
                {
                    ShowDashboardMode();
                    if (DashboardViewModel.PreviewTgPendingCommand.CanExecute(null))
                        DashboardViewModel.PreviewTgPendingCommand.Execute(null);
                }),
                P("shell.palette.openSettings.title", "shell.palette.openSettings.desc", () => _ = OpenSettingsAsync())
            ];
        }

        return
        [
            P("shell.palette.navOps.title", "shell.palette.navOps.desc", ShowOperationsMode),
            P("shell.palette.navWizard.title", "shell.palette.navWizard.desc", ShowWizardMode),
            P("shell.palette.refreshJobs.title", "shell.palette.refreshJobs.desc", () =>
            {
                if (JobsViewModel.RefreshJobsCommand.CanExecute(null))
                    JobsViewModel.RefreshJobsCommand.Execute(null);
            }),
            P("shell.palette.saveLinks.title", "shell.palette.saveLinks.desc", () =>
            {
                ShowJobsMode();
                if (JobsViewModel.SavePublishLinksCommand.CanExecute(null))
                    JobsViewModel.SavePublishLinksCommand.Execute(null);
            }),
            P("shell.palette.fullPipeline.title", "shell.palette.fullPipeline.desc", () =>
            {
                ShowJobsMode();
                if (JobsViewModel.RunFullPipelineCommand.CanExecute(null))
                    JobsViewModel.RunFullPipelineCommand.Execute(null);
            }),
            P("shell.palette.dashboardSnapshot.title", "shell.palette.dashboardSnapshot.desc", ExecuteDashboardSnapshotFromPalette),
            P("shell.palette.previewTgPending.title", "shell.palette.previewTgPending.desc", () =>
            {
                ShowDashboardMode();
                if (DashboardViewModel.PreviewTgPendingCommand.CanExecute(null))
                    DashboardViewModel.PreviewTgPendingCommand.Execute(null);
            }),
            P("shell.palette.exportTgCsv.title", "shell.palette.exportTgCsv.desc", () =>
            {
                ShowDashboardMode();
                DashboardViewModel.ExportTgPendingCsvCommand.Execute(null);
            }),
            P("shell.palette.exportPublishQueueCsv.title", "shell.palette.exportPublishQueueCsv.desc", () =>
            {
                ShowDashboardMode();
                DashboardViewModel.ExportPublishQueueCsvCommand.Execute(null);
            }),
            P("shell.palette.scanDuplicates.title", "shell.palette.scanDuplicates.desc", () =>
            {
                ShowOperationsMode();
                OperationsViewModel.RefreshCommand.Execute(null);
            }),
            P("shell.palette.autoMergeDuplicates.title", "shell.palette.autoMergeDuplicates.desc", () =>
                OperationsViewModel.BatchMergeDuplicatesCommand.Execute(null)),
            P("shell.palette.previewBatchMerge.title", "shell.palette.previewBatchMerge.desc", () =>
                OperationsViewModel.PreviewBatchMergeDuplicatesCommand.Execute(null)),
            P("shell.palette.trimActivityLog.title", "shell.palette.trimActivityLog.desc", () =>
                OperationsViewModel.TrimActivityLogCommand.Execute(null)),
            P("shell.palette.archiveActivityLog.title", "shell.palette.archiveActivityLog.desc", () =>
                OperationsViewModel.ArchiveActivityLogCommand.Execute(null)),
            P("shell.palette.exportDaily.title", "shell.palette.exportDaily.desc", () =>
            {
                if (OperationsViewModel.ExportDailyReportCommand.CanExecute(null))
                    OperationsViewModel.ExportDailyReportCommand.Execute(null);
                else if (DashboardViewModel.ExportDailyReportCommand.CanExecute(null))
                    DashboardViewModel.ExportDailyReportCommand.Execute(null);
            }),
            P("shell.palette.backupJobs.title", "shell.palette.backupJobs.desc", () =>
            {
                if (OperationsViewModel.ExportAllJobsBackupCommand.CanExecute(null))
                    OperationsViewModel.ExportAllJobsBackupCommand.Execute(null);
                else if (DashboardViewModel.ExportAllJobsBackupCommand.CanExecute(null))
                    DashboardViewModel.ExportAllJobsBackupCommand.Execute(null);
            }),
            P("shell.palette.exportNightlyScript.title", "shell.palette.exportNightlyScript.desc", () =>
            {
                if (OperationsViewModel.ExportNightlyAutomationScriptCommand.CanExecute(null))
                    OperationsViewModel.ExportNightlyAutomationScriptCommand.Execute(null);
                else if (JobsViewModel.ExportNightlyAutomationScriptCommand.CanExecute(null))
                    JobsViewModel.ExportNightlyAutomationScriptCommand.Execute(null);
            }),
            P("shell.palette.exportDuplicateReport.title", "shell.palette.exportDuplicateReport.desc", () =>
                OperationsViewModel.ExportDuplicateReportCommand.Execute(null)),
            P("shell.palette.exportMergeSuggestions.title", "shell.palette.exportMergeSuggestions.desc", () =>
                OperationsViewModel.ExportDuplicateMergeSuggestionsCommand.Execute(null)),
            P("shell.palette.exportMergePreviewJson.title", "shell.palette.exportMergePreviewJson.desc", () =>
                OperationsViewModel.ExportBatchMergePreviewCommand.Execute(null)),
            P("shell.palette.exportActivityLog.title", "shell.palette.exportActivityLog.desc", () =>
                OperationsViewModel.ExportActivityLogCommand.Execute(null)),
            P("shell.palette.exportActivityLogCsv.title", "shell.palette.exportActivityLogCsv.desc", () =>
                OperationsViewModel.ExportActivityLogCsvCommand.Execute(null)),
            P("shell.palette.exportActivityLogStats.title", "shell.palette.exportActivityLogStats.desc", ExecuteExportActivityLogStatsFromPalette),
            P("shell.palette.exportActivityLogStatsCsv.title", "shell.palette.exportActivityLogStatsCsv.desc", ExecuteExportActivityLogStatsCsvFromPalette),
            P("shell.palette.exportActivityLogStatsHtml.title", "shell.palette.exportActivityLogStatsHtml.desc", ExecuteExportActivityLogStatsHtmlFromPalette),
            P("shell.palette.filterBatchActivity.title", "shell.palette.filterBatchActivity.desc", () =>
                OperationsViewModel.FilterBatchActivityLogsCommand.Execute(null)),
            P("shell.palette.exportBatchStatsAll.title", "shell.palette.exportBatchStatsAll.desc", () =>
                OperationsViewModel.ExportActivityLogBatchStatsAllCommand.Execute(null)),
            P("shell.palette.exportBatchStatsJson.title", "shell.palette.exportBatchStatsJson.desc", () =>
                OperationsViewModel.ExportActivityLogBatchStatsCommand.Execute(null)),
            P("shell.palette.exportBatchStatsCsv.title", "shell.palette.exportBatchStatsCsv.desc", () =>
                OperationsViewModel.ExportActivityLogBatchStatsCsvCommand.Execute(null)),
            P("shell.palette.batchAppendTags.title", "shell.palette.batchAppendTags.desc", ExecuteBatchAppendTagsToSelectedFromPalette),
            P("shell.palette.batchArchiveSelected.title", "shell.palette.batchArchiveSelected.desc", ExecuteBatchArchiveSelectedFromPalette),
            P("shell.palette.batchUnarchiveSelected.title", "shell.palette.batchUnarchiveSelected.desc", ExecuteBatchUnarchiveSelectedFromPalette),
            P("shell.palette.batchDeleteSelected.title", "shell.palette.batchDeleteSelected.desc", ExecuteBatchDeleteSelectedFromPalette),
            P("shell.palette.exportMachineProfile.title", "shell.palette.exportMachineProfile.desc", () =>
                OperationsViewModel.ExportMachineProfileCommand.Execute(null)),
            P("shell.palette.exportFilteredCsv.title", "shell.palette.exportFilteredCsv.desc", ExecuteExportFilteredJobsCsvFromPalette),
            P("shell.palette.exportFilteredJson.title", "shell.palette.exportFilteredJson.desc", ExecuteExportFilteredJobsJsonFromPalette),
            P("shell.palette.exportPinnedCsv.title", "shell.palette.exportPinnedCsv.desc", ExecuteExportPinnedJobsCsvFromPalette)
        ];
    }

    private async Task ShowCommandPaletteAsync()
    {
        var actions = BuildCommandPaletteActions();

        var palettePrefs = App.Services.UiPreferences.Load();
        var filteredActions = new List<CommandPaletteItem>(actions);
        var listView = new ListView
        {
            ItemsSource = filteredActions,
            SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = true,
            MaxHeight = 360,
            ItemTemplate = CreateCommandPaletteDataTemplate()
        };

        var recentHeader = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["PlcSecondaryTextBrush"],
            Margin = new Thickness(0, 0, 0, 4),
            Visibility = Visibility.Collapsed
        };

        var resultCountText = new TextBlock
        {
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["PlcSecondaryTextBrush"],
            Margin = new Thickness(0, 0, 0, 8)
        };

        var emptyResultText = new TextBlock
        {
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["PlcSecondaryTextBrush"],
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = Visibility.Collapsed
        };

        var currentPaletteQuery = string.Empty;

        void ApplyCommandPaletteFilter(string? rawQuery)
        {
            var query = rawQuery?.Trim() ?? string.Empty;
            currentPaletteQuery = query;
            filteredActions.Clear();
            if (string.IsNullOrEmpty(query))
            {
                var recentIds = palettePrefs.RecentCommandPaletteIds ?? [];
                var recentItems = recentIds
                    .Select(id => actions.FirstOrDefault(item =>
                        string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)))
                    .Where(item => item is not null)
                    .Cast<CommandPaletteItem>()
                    .ToList();

                if (recentItems.Count > 0)
                {
                    recentHeader.Text = LocalizationService.T("shell.commandPalette.recent");
                    recentHeader.Visibility = Visibility.Visible;
                    filteredActions.AddRange(recentItems);
                }
                else
                {
                    recentHeader.Visibility = Visibility.Collapsed;
                }

                var recentIdSet = new HashSet<string>(
                    recentItems.Select(item => item.Id),
                    StringComparer.OrdinalIgnoreCase);
                filteredActions.AddRange(actions.Where(item => !recentIdSet.Contains(item.Id)));
            }
            else
            {
                recentHeader.Visibility = Visibility.Collapsed;
                foreach (var item in actions)
                {
                    if (item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || item.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                        filteredActions.Add(item);
                }
            }

            listView.ItemsSource = null;
            listView.ItemsSource = filteredActions;
            if (filteredActions.Count > 0)
                listView.SelectedIndex = 0;
            else
                listView.SelectedIndex = -1;

            resultCountText.Text = string.Format(
                LocalizationService.T("shell.commandPalette.resultCount"),
                filteredActions.Count);

            if (!string.IsNullOrEmpty(query) && filteredActions.Count == 0)
            {
                emptyResultText.Text = LocalizationService.T("shell.commandPalette.noResults");
                emptyResultText.Visibility = Visibility.Visible;
            }
            else
            {
                emptyResultText.Visibility = Visibility.Collapsed;
            }
        }

        var filterBox = new TextBox
        {
            PlaceholderText = LocalizationService.T("shell.commandPalette.filterPlaceholder"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        filterBox.TextChanged += (_, _) => ApplyCommandPaletteFilter(filterBox.Text);

        var keyboardHint = new TextBlock
        {
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["PlcSecondaryTextBrush"],
            Text = LocalizationService.T("shell.commandPalette.keyboardHint"),
            Margin = new Thickness(0, 0, 0, 4)
        };

        var tcs = new TaskCompletionSource<CommandPaletteItem?>();
        void TryExecutePaletteSelection()
        {
            if (listView.SelectedItem is CommandPaletteItem selectedItem)
            {
                tcs.TrySetResult(selectedItem);
                return;
            }

            if (filteredActions.Count > 0)
                tcs.TrySetResult(filteredActions[0]);
        }

        filterBox.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Down && filteredActions.Count > 0)
            {
                listView.SelectedIndex = 0;
                listView.Focus(FocusState.Programmatic);
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Enter)
            {
                TryExecutePaletteSelection();
                e.Handled = true;
            }
        };

        listView.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter)
            {
                TryExecutePaletteSelection();
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Up && listView.SelectedIndex <= 0)
            {
                filterBox.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        };

        listView.ItemClick += (_, args) =>
        {
            if (args.ClickedItem is CommandPaletteItem item)
                tcs.TrySetResult(item);
        };
        listView.ContainerContentChanging += (_, args) =>
        {
            if (args.InRecycleQueue || args.Item is not CommandPaletteItem item)
                return;

            void ApplyTemplateText()
            {
                if (args.ItemContainer.ContentTemplateRoot is not StackPanel root)
                    return;

                if (root.Children.ElementAtOrDefault(0) is TextBlock titleBlock)
                    SetCommandPaletteHighlightedText(titleBlock, item.Title, currentPaletteQuery, emphasize: true);
                if (root.Children.ElementAtOrDefault(1) is TextBlock descriptionBlock)
                    SetCommandPaletteHighlightedText(descriptionBlock, item.Description, currentPaletteQuery, emphasize: false);
            }

            if (args.Phase == 0)
            {
                args.RegisterUpdateCallback((_, updateArgs) =>
                {
                    if (updateArgs.Phase == 1)
                        ApplyTemplateText();
                });
            }
            else if (args.Phase == 1)
            {
                ApplyTemplateText();
            }
        };

        var paletteContent = new StackPanel { Spacing = 4 };
        paletteContent.Children.Add(filterBox);
        paletteContent.Children.Add(keyboardHint);
        paletteContent.Children.Add(resultCountText);
        paletteContent.Children.Add(emptyResultText);
        paletteContent.Children.Add(recentHeader);
        paletteContent.Children.Add(listView);

        var dialog = new ContentDialog
        {
            Title = LocalizationService.T("shell.commandPalette.title"),
            Content = paletteContent,
            CloseButtonText = LocalizationService.T("settings.close"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        dialog.Closed += (_, _) =>
        {
            palettePrefs.LastCommandPaletteFilter = filterBox.Text?.Trim() ?? string.Empty;
            App.Services.UiPreferences.Save(palettePrefs);
            tcs.TrySetResult(null);
        };

        if (!string.IsNullOrWhiteSpace(palettePrefs.LastCommandPaletteFilter))
            filterBox.Text = palettePrefs.LastCommandPaletteFilter;
        else
            ApplyCommandPaletteFilter(filterBox.Text);

        _ = dialog.ShowAsync();
        filterBox.Focus(FocusState.Programmatic);

        var selected = await tcs.Task.ConfigureAwait(true);
        if (selected is null)
            return;

        RecordRecentCommandPaletteId(selected.Id);
        selected.Execute();
    }

    private void RecordRecentCommandPaletteId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        var prefs = App.Services.UiPreferences.Load();
        var recent = prefs.RecentCommandPaletteIds ?? [];
        recent.RemoveAll(existing => string.Equals(existing, id, StringComparison.OrdinalIgnoreCase));
        recent.Insert(0, id);
        if (recent.Count > 5)
            recent = recent.Take(5).ToList();

        prefs.RecentCommandPaletteIds = recent;
        App.Services.UiPreferences.Save(prefs);
    }

    private static void SetCommandPaletteHighlightedText(
        TextBlock textBlock,
        string text,
        string? query,
        bool emphasize)
    {
        textBlock.Inlines.Clear();
        if (string.IsNullOrEmpty(text))
            return;

        if (string.IsNullOrWhiteSpace(query))
        {
            textBlock.Inlines.Add(new Run { Text = text });
            return;
        }

        var accentBrush = Application.Current?.Resources["PlcAccentBrush"] as Brush;
        var startIndex = 0;
        while (startIndex < text.Length)
        {
            var matchIndex = text.IndexOf(query, startIndex, StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0)
            {
                textBlock.Inlines.Add(new Run { Text = text[startIndex..] });
                break;
            }

            if (matchIndex > startIndex)
                textBlock.Inlines.Add(new Run { Text = text[startIndex..matchIndex] });

            textBlock.Inlines.Add(new Run
            {
                Text = text.Substring(matchIndex, query.Length),
                FontWeight = emphasize
                    ? Microsoft.UI.Text.FontWeights.SemiBold
                    : Microsoft.UI.Text.FontWeights.Bold,
                Foreground = accentBrush
            });
            startIndex = matchIndex + query.Length;
        }
    }

    private static DataTemplate CreateCommandPaletteDataTemplate()
    {
        const string xaml = """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <StackPanel Spacing="2" Margin="4,8">
                    <TextBlock x:Name="TitleBlock" FontWeight="SemiBold" />
                    <TextBlock x:Name="DescriptionBlock" FontSize="12" Opacity="0.72" TextWrapping="Wrap" />
                </StackPanel>
            </DataTemplate>
            """;

        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml);
    }

    private async Task ShowWelcomeDialogIfNeededAsync()
    {
        var studio = App.Services.StudioConfig.Load();
        if (!studio.ShowWelcomeOnStartup)
            return;

        var prefs = App.Services.UiPreferences.Load();
        if (prefs.HasSeenWelcome)
            return;

        var dialog = new ContentDialog
        {
            Title = T("shell.welcome.title", AppVersion.Current),
            PrimaryButtonText = LocalizationService.T("shell.welcome.startButton"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                Content = new TextBlock
                {
                    Text = T("shell.welcome.body", AppVersion.Current),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return;

        prefs.HasSeenWelcome = true;
        App.Services.UiPreferences.Save(prefs);
    }

    private async Task ShowHelpDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = LocalizationService.T("shell.help.title"),
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = LocalizationService.T("shell.help.text"),
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
                },
                MaxHeight = 320
            },
            CloseButtonText = LocalizationService.T("settings.close"),
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async Task<bool> OnConfirmMergeRequestedAsync(string previewMessage)
    {
        var dialog = new ContentDialog
        {
            Title = "合并任务确认",
            Content = new ScrollViewer
            {
                Content = new TextBlock { Text = previewMessage, TextWrapping = TextWrapping.Wrap },
                MaxHeight = 360
            },
            PrimaryButtonText = "确认合并",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> ShowCleanupDialogAsync(CleanupConfirmation confirmation)
    {
        if (!confirmation.RequiresConfirmation)
            return true;

        var dialog = new ContentDialog
        {
            Title = "PLCPak 清理确认",
            Content = $"共 {confirmation.FolderCount} 个文件夹、{confirmation.TotalMatched} 项广告待删除。\n\n{confirmation.Summary}\n\n确认删除？",
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void OnPreviewRequested(string text)
    {
        var dialog = new ContentDialog
        {
            Title = LocalizationService.T("quick.preview.title"),
            Content = new ScrollViewer
            {
                Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                MaxHeight = 400
            },
            CloseButtonText = LocalizationService.T("common.close"),
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void OnJobsOrDashboardMessageRequested(string message, string title)
        => ShowInfoBar(message, title);

    private void OnPaletteMessageRequested(string message, string title)
        => ShowPaletteInfoBar(message, title);

    private void OnMainMessageRequested(string message, string title)
        => ShowInfoBar(message, title);

    private void ShowInfoBar(string message, string title, InfoBarSeverity severity = InfoBarSeverity.Informational)
        => ShowInfoBar(message, title, severity, autoCloseSeconds: null);

    private void ShowPaletteInfoBar(string message, string title)
        => ShowInfoBar(message, title, InfoBarSeverity.Informational, autoCloseSeconds: 1.5);

    private void ShowInfoBar(
        string message,
        string title,
        InfoBarSeverity severity,
        double? autoCloseSeconds)
    {
        StopInfoBarAutoCloseTimer();
        GlobalInfoBar.Title = title;
        GlobalInfoBar.Message = message;
        GlobalInfoBar.Severity = severity;
        GlobalInfoBar.IsOpen = true;

        if (severity is InfoBarSeverity.Informational or InfoBarSeverity.Success)
        {
            EnsureInfoBarAutoCloseTimer(severity, autoCloseSeconds);
            _infoBarAutoCloseTimer!.Start();
        }
        else
        {
            StopInfoBarAutoCloseTimer();
        }
    }

    private void EnsureInfoBarAutoCloseTimer(InfoBarSeverity severity, double? autoCloseSeconds = null)
    {
        var interval = autoCloseSeconds.HasValue
            ? TimeSpan.FromSeconds(autoCloseSeconds.Value)
            : severity == InfoBarSeverity.Success
                ? TimeSpan.FromSeconds(3)
                : TimeSpan.FromSeconds(2.5);

        if (_infoBarAutoCloseTimer is null)
        {
            _infoBarAutoCloseTimer = new DispatcherTimer();
            _infoBarAutoCloseTimer.Tick += (_, _) =>
            {
                StopInfoBarAutoCloseTimer();
                GlobalInfoBar.IsOpen = false;
            };
        }

        _infoBarAutoCloseTimer.Interval = interval;
    }

    private void StopInfoBarAutoCloseTimer() => _infoBarAutoCloseTimer?.Stop();

    private void GlobalInfoBar_Closed(InfoBar sender, InfoBarClosedEventArgs args)
        => StopInfoBarAutoCloseTimer();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void ExecuteExportActivityLogStatsFromPalette()
    {
        if (TryExecuteViewModelCommand(OperationsViewModel, "ExportActivityLogStatsCommand"))
            return;

        var stats = App.Services.JobRunner.GetActivityLogStats();
        OnPaletteMessageRequested(stats.SummaryText, LocalizationService.T("feedback.activityLogStats"));
    }

    private void ExecuteExportActivityLogStatsCsvFromPalette()
    {
        ShowOperationsMode();
        if (TryExecuteViewModelCommand(OperationsViewModel, "ExportActivityLogStatsCsvCommand"))
            return;

        var export = App.Services.JobRunner.ExportActivityLogStatsCsv();
        OnPaletteMessageRequested(
            $"{export.SummaryText}\n{export.ExportPath}",
            LocalizationService.T("feedback.exportActivityLogStatsCsv"));
    }

    private void ExecuteExportActivityLogStatsHtmlFromPalette()
    {
        ShowOperationsMode();
        if (TryExecuteViewModelCommand(OperationsViewModel, "ExportActivityLogStatsHtmlCommand"))
            return;

        var export = App.Services.JobRunner.ExportActivityLogStatsHtml();
        OnPaletteMessageRequested(
            $"{export.SummaryText}\n{export.ExportPath}",
            LocalizationService.T("feedback.exportActivityLogStatsHtml"));
    }

    private void ExecuteBatchAppendTagsToSelectedFromPalette()
    {
        ShowJobsMode();
        if (TryExecuteViewModelCommand(JobsViewModel, "BatchAppendTagsToSelectedCommand"))
            return;

        OnPaletteMessageRequested(
            LocalizationService.T("shell.palette.batchTagsHint"),
            LocalizationService.T("feedback.batchTags"));
    }

    private void ExecuteBatchArchiveSelectedFromPalette()
    {
        ShowJobsMode();
        if (TryExecuteViewModelCommand(JobsViewModel, "BatchArchiveSelectedCommand"))
            return;

        OnPaletteMessageRequested(
            LocalizationService.T("shell.palette.batchArchiveHint"),
            LocalizationService.T("feedback.batchArchive"));
    }

    private void ExecuteBatchUnarchiveSelectedFromPalette()
    {
        ShowJobsMode();
        if (TryExecuteViewModelCommand(JobsViewModel, "BatchUnarchiveSelectedCommand"))
            return;

        OnPaletteMessageRequested(
            LocalizationService.T("shell.palette.batchUnarchiveHint"),
            LocalizationService.T("feedback.batchUnarchive"));
    }

    private void ExecuteBatchDeleteSelectedFromPalette()
    {
        ShowJobsMode();
        if (TryExecuteViewModelCommand(JobsViewModel, "BatchDeleteSelectedCommand"))
            return;

        OnPaletteMessageRequested(
            LocalizationService.T("shell.palette.batchDeleteHint"),
            LocalizationService.T("feedback.batchDelete"));
    }

    private void ExecuteExportFilteredJobsCsvFromPalette()
    {
        ShowJobsMode();
        if (TryExecuteViewModelCommand(JobsViewModel, "ExportFilteredJobsCsvCommand"))
            return;

        var tag = JobsViewModel.SelectedTagFilter == JobQueryService.AllTagsLabel
            ? null
            : JobsViewModel.SelectedTagFilter;
        var export = App.Services.JobRunner.ExportFilteredJobsCsv(
            ResolveJobsFilterFromPalette(),
            JobsViewModel.JobSearchText,
            tag,
            ResolveJobsSortFromPalette());
        OnPaletteMessageRequested(
            $"{export.SummaryText}\n{export.ExportPath}",
            LocalizationService.T("feedback.exportFilteredCsv"));
    }

    private void ExecuteExportFilteredJobsJsonFromPalette()
    {
        ShowJobsMode();
        if (TryExecuteViewModelCommand(JobsViewModel, "ExportFilteredJobsJsonCommand"))
            return;

        var tag = JobsViewModel.SelectedTagFilter == JobQueryService.AllTagsLabel
            ? null
            : JobsViewModel.SelectedTagFilter;
        var searchText = string.IsNullOrWhiteSpace(JobsViewModel.JobSearchText)
            ? null
            : JobsViewModel.JobSearchText;
        var export = App.Services.JobRunner.ExportFilteredJobsJson(
            ResolveJobsFilterFromPalette(),
            searchText,
            tag,
            ResolveJobsSortFromPalette());
        OnPaletteMessageRequested(
            $"{export.SummaryText}\n{export.ExportPath}",
            LocalizationService.T("feedback.exportFilteredJson"));
    }

    private void ExecuteExportPinnedJobsCsvFromPalette()
    {
        ShowJobsMode();
        if (TryExecuteViewModelCommand(JobsViewModel, "ExportPinnedJobsCsvCommand"))
            return;

        var export = App.Services.JobRunner.ExportPinnedJobsCsv();
        OnPaletteMessageRequested(
            $"{export.SummaryText}\n{export.ExportPath}",
            LocalizationService.T("feedback.exportPinnedCsv"));
    }

    private void ExecuteDashboardSnapshotFromPalette()
    {
        ShowDashboardMode();
        var snapshot = App.Services.JobRunner.GetDashboardSnapshot();
        OnPaletteMessageRequested(
            snapshot.SummaryText,
            LocalizationService.T("feedback.dashboardSnapshot"));
    }

    private JobListFilter ResolveJobsFilterFromPalette()
    {
        foreach (var (filter, label) in PublishDashboardService.FilterOptions)
        {
            if (string.Equals(label, JobsViewModel.SelectedJobFilter, StringComparison.OrdinalIgnoreCase))
                return filter;
        }

        return JobListFilter.All;
    }

    private JobSortOrder ResolveJobsSortFromPalette()
        => JobsViewModel.SelectedJobSort switch
        {
            "置顶优先" => JobSortOrder.PinnedFirst,
            "最近更新" => JobSortOrder.UpdatedDesc,
            "最早更新" => JobSortOrder.UpdatedAsc,
            "标题A-Z" => JobSortOrder.TitleAsc,
            "标题Z-A" => JobSortOrder.TitleDesc,
            _ => JobSortOrder.UpdatedDesc
        };

    private bool TryRefreshVisiblePage(bool f5Only = false)
    {
        if (DashboardPageControl.Visibility == Visibility.Visible)
            return TryExecutePageRefresh(DashboardViewModel.RefreshCommand, "dashboard.title");

        if (JobsPageControl.Visibility == Visibility.Visible)
            return TryExecutePageRefresh(JobsViewModel.RefreshJobsCommand, "nav.jobs");

        if (OperationsPageControl.Visibility == Visibility.Visible)
            return TryExecutePageRefresh(OperationsViewModel.RefreshCommand, "nav.operations");

        if (f5Only)
            return false;

        if (MainPageControl.Visibility == Visibility.Visible)
        {
            var refreshed = TryExecutePageRefresh(ViewModel.RefreshCommand, "nav.quick");
            if (refreshed)
                UpdateQuickStats();
            return refreshed;
        }

        if (WizardPageControl.Visibility == Visibility.Visible)
            return TryExecutePageRefresh(WizardViewModel.RefreshWizardCommand, "nav.wizard");

        return false;
    }

    private bool TryExecutePageRefresh(System.Windows.Input.ICommand command, string titleKey)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
            return true;
        }

        ShowInfoBar(
            LocalizationService.T("feedback.refreshInProgress"),
            LocalizationService.T(titleKey),
            InfoBarSeverity.Informational);
        return true;
    }

    private static bool TryExecuteViewModelCommand(object viewModel, string commandPropertyName)
    {
        var command = viewModel.GetType().GetProperty(commandPropertyName)?.GetValue(viewModel);
        if (command is null)
            return false;

        var canExecute = command.GetType().GetMethod("CanExecute", [typeof(object)]);
        if (canExecute?.Invoke(command, [null]) is not true)
            return false;

        command.GetType().GetMethod("Execute", [typeof(object)])?.Invoke(command, [null]);
        return true;
    }

    private sealed class CommandPaletteItem(string id, string title, string description, Action execute)
    {
        public string Id { get; } = id;
        public string Title { get; } = title;
        public string Description { get; } = description;
        public void Execute() => execute();
    }
}