using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;
using PLCPak.WinUI.Infrastructure;

namespace PLCPak.WinUI.ViewModels;

public sealed class WorkflowSuggestionItem
{
    public string JobId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ActionLabel { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string DisplayText => $"{ActionLabel} — {Title}";
}

public sealed partial class WizardStepItem : ObservableObject
{
    public int Order { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public WizardStepStatus Status { get; init; }

    [ObservableProperty]
    private bool _isCurrent;
}

public partial class WizardViewModel : ObservableObject
{
    private readonly PlcPakAppContext _app;
    private readonly UiDispatcher _ui;
    private readonly UiModeNotifier _uiMode;
    private readonly Dictionary<string, string> _wizardOptionToJobId = new(StringComparer.OrdinalIgnoreCase);

    public WizardViewModel(PlcPakAppContext app)
    {
        _app = app;
        _ui = UiDispatcher.ForCurrentThread();
        _uiMode = new UiModeNotifier(app, () =>
        {
            OnPropertyChanged(nameof(IsProfessionalMode));
            OnPropertyChanged(nameof(IsSimpleMode));
        });
        LocalizationService.LanguageChanged += OnLanguageChanged;
        RefreshWizardCore();
    }

    public string WizardPageTitlePrefix => LocalizationService.T("wizard.page.title");
    public string WizardSimpleHeroTitle => LocalizationService.T("wizard.page.simpleHero");
    public string WizardSimpleFlowHint => LocalizationService.T("wizard.page.simpleFlow");
    public string WizardOtherSuggestionsTitle => LocalizationService.T("wizard.page.otherSuggestions");
    public string WizardSelectJobHeader => LocalizationService.T("wizard.page.selectJob");
    public string WizardSelectJobPlaceholder => LocalizationService.T("wizard.page.selectJobPlaceholder");
    public string WizardPublishStepsTitle => LocalizationService.T("wizard.page.publishSteps");
    public string ExecuteCurrentStepLabel => LocalizationService.T("jobs.wizard.execute");
    public string WizardRefreshLabel => LocalizationService.T("wizard.page.refresh");
    public string WizardOpenJobLabel => LocalizationService.T("guide.openJob");
    public string WizardAutoRunLabel => LocalizationService.T("wizard.page.autoRunUntilManual");

    public ObservableCollection<WizardStepItem> Steps { get; } = [];
    public ObservableCollection<string> WizardJobOptions { get; } = [];
    public ObservableCollection<WorkflowSuggestionItem> WorkflowSuggestions { get; } = [];
    public bool IsProfessionalMode => _uiMode.IsProfessionalMode;
    public bool IsSimpleMode => _uiMode.IsSimpleMode;

    [ObservableProperty] private string _wizardTitle = string.Empty;
    [ObservableProperty] private string _wizardSummary = string.Empty;
    [ObservableProperty] private bool _hasWorkflowSuggestions;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedJob))]
    [NotifyCanExecuteChangedFor(nameof(GoToJobCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteCurrentStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunAutomatableChainCommand))]
    private string _selectedWizardJob = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedJob))]
    [NotifyCanExecuteChangedFor(nameof(GoToJobCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteCurrentStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunAutomatableChainCommand))]
    private string _selectedJobId = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteCurrentStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunAutomatableChainCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshWizardCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowWizardStepsList))]
    [NotifyCanExecuteChangedFor(nameof(RefreshWizardCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteCurrentStepCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunAutomatableChainCommand))]
    private bool _isRefreshingWizard;

    [ObservableProperty] private int _wizardStepProgressPercent;
    [ObservableProperty] private string _wizardStepProgressText = string.Empty;
    [ObservableProperty] private string _wizardStepsEmptyHint = string.Empty;
    [ObservableProperty] private string _wizardNoJobsHint = string.Empty;

    public bool HasSelectedJob => !string.IsNullOrWhiteSpace(SelectedJobId);
    public bool ShowWizardStepProgress => Steps.Count > 0;
    public bool ShowWizardStepsList => ShowWizardStepProgress && !IsRefreshingWizard;
    public bool HasWizardJobs => WizardJobOptions.Count > 0;
    public string WizardJobCountText => string.Format(
        LocalizationService.T("wizard.page.jobCount"),
        WizardJobOptions.Count);
    public string WizardRefreshingHint => LocalizationService.T("wizard.page.refreshing");
    public string WizardStepsLoadingHint => LocalizationService.T("wizard.page.stepsLoading");

    partial void OnSelectedWizardJobChanged(string value)
    {
        var jobId = ResolveWizardJobId(value);
        SelectedJobId = jobId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(jobId))
            LoadWizardStateForJob(jobId);
        else
            LoadWizardStateEmpty();
    }

    public void SelectJob(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return;

        TrySelectWizardJobById(jobId);
        RefreshWizardCommand.Execute(null);
    }

    private bool CanRefreshWizard() => !IsBusy && !IsRefreshingWizard;

    [RelayCommand(CanExecute = nameof(CanRefreshWizard))]
    private async Task RefreshWizardAsync()
    {
        if (IsRefreshingWizard)
            return;

        IsRefreshingWizard = true;
        try
        {
            await Task.Yield();
            RefreshWizardCore();
        }
        finally
        {
            IsRefreshingWizard = false;
        }
    }

    private void RefreshWizardCore()
    {
        RefreshWizardJobOptions();
        ApplyWizardJobAvailabilityHints();

        if (string.IsNullOrWhiteSpace(SelectedWizardJob))
        {
            if (!string.IsNullOrWhiteSpace(SelectedJobId))
                TrySelectWizardJobById(SelectedJobId);
            else
            {
                var primaryJobId = _app.JobRunner.GetWizardState().JobId;
                if (!string.IsNullOrWhiteSpace(primaryJobId))
                    TrySelectWizardJobById(primaryJobId);
            }
        }

        var jobId = ResolveWizardJobId(SelectedWizardJob);
        if (!string.IsNullOrWhiteSpace(jobId))
            LoadWizardStateForJob(jobId);
        else
            LoadWizardStateEmpty();

        RefreshWorkflowSuggestions();
    }

    [RelayCommand]
    private void SelectSuggestion(WorkflowSuggestionItem? suggestion)
    {
        if (suggestion is null || string.IsNullOrWhiteSpace(suggestion.JobId))
            return;

        TrySelectWizardJobById(suggestion.JobId);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedJob))]
    private void GoToJob()
    {
        if (!string.IsNullOrWhiteSpace(SelectedJobId))
            NavigateToJobRequested?.Invoke(SelectedJobId);
    }

    private bool CanExecuteCurrentStep() => HasSelectedJob && !IsBusy && !IsRefreshingWizard;

    [RelayCommand(CanExecute = nameof(CanExecuteCurrentStep))]
    private async Task ExecuteCurrentStepAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedJobId))
            return;

        IsBusy = true;
        var jobId = SelectedJobId;
        try
        {
            var result = await _app.JobRunner.ExecuteNextActionAsync(
                jobId,
                action: null,
                confirmCleanupAsync: OnConfirmCleanupAsync).ConfigureAwait(false);

            _ui.Run(() =>
            {
                RefreshWizardCore();
                NavigateToJobRequested?.Invoke(jobId);
                var title = result.Success
                    ? LocalizationService.T("jobs.wizard.execute")
                    : result.NeedsUserInput
                        ? LocalizationService.T("wizard.page.needsManual")
                        : LocalizationService.T("wizard.page.executeFailed");
                MessageRequested?.Invoke(result.Message, title);
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, LocalizationService.T("wizard.page.executeFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCurrentStep))]
    private async Task RunAutomatableChainAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedJobId))
            return;

        IsBusy = true;
        var jobId = SelectedJobId;
        try
        {
            var result = await _app.JobRunner.RunAutomatableChainAsync(
                jobId,
                confirmCleanupAsync: OnConfirmCleanupAsync).ConfigureAwait(false);

            _ui.Run(() =>
            {
                RefreshWizardCore();
                var title = result.NeedsUserInput
                    ? LocalizationService.T("wizard.page.needsManual")
                    : result.Success
                        ? LocalizationService.T("wizard.page.autoRun")
                        : LocalizationService.T("wizard.page.executeFailed");
                MessageRequested?.Invoke(BuildAutomatableChainMessage(result), title);
            });
        }
        catch (Exception ex)
        {
            _ui.Run(() => MessageRequested?.Invoke(ex.Message, LocalizationService.T("wizard.page.autoRunFailed")));
        }
        finally
        {
            _ui.Run(() => IsBusy = false);
        }
    }

    private void RefreshWizardJobOptions()
    {
        var previous = SelectedWizardJob;
        WizardJobOptions.Clear();
        _wizardOptionToJobId.Clear();

        foreach (var job in _app.Jobs.List().Where(j => j.Status != JobStatus.Archived))
        {
            var label = $"{job.Title} ({job.Id[..8]})";
            WizardJobOptions.Add(label);
            _wizardOptionToJobId[label] = job.Id;
        }

        if (!string.IsNullOrWhiteSpace(previous) && _wizardOptionToJobId.ContainsKey(previous))
            SelectedWizardJob = previous;

        ApplyWizardJobAvailabilityHints();
        OnPropertyChanged(nameof(WizardJobCountText));
    }

    private void TrySelectWizardJobById(string jobId)
    {
        var match = _wizardOptionToJobId.FirstOrDefault(x => x.Value == jobId);
        if (!string.IsNullOrWhiteSpace(match.Key))
            SelectedWizardJob = match.Key;
    }

    private string? ResolveWizardJobId(string? optionText)
    {
        if (string.IsNullOrWhiteSpace(optionText))
            return null;

        if (_wizardOptionToJobId.TryGetValue(optionText, out var mappedId))
            return mappedId;

        var shortId = ParseWizardJobShortId(optionText);
        if (string.IsNullOrWhiteSpace(shortId))
            return null;

        return _app.Jobs.List()
            .FirstOrDefault(j => j.Id.StartsWith(shortId, StringComparison.OrdinalIgnoreCase))
            ?.Id;
    }

    private static string? ParseWizardJobShortId(string? optionText)
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

    private void LoadWizardStateForJob(string jobId)
    {
        var state = _app.JobRunner.GetWizardStateForJob(jobId);
        WizardTitle = string.IsNullOrWhiteSpace(state.Title)
            ? $"{WizardPageTitlePrefix} v{AppVersion.Current}"
            : $"{WizardPageTitlePrefix} v{AppVersion.Current} — {state.Title}";
        WizardSummary = state.SummaryText;
        SelectedJobId = state.JobId ?? jobId;
        PopulateSteps(state);
    }

    private void LoadWizardStateEmpty()
    {
        WizardTitle = $"{WizardPageTitlePrefix} v{AppVersion.Current}";
        WizardSummary = string.Empty;
        SelectedJobId = string.Empty;
        Steps.Clear();
        UpdateWizardStepProgress();
        ApplyWizardStepsEmptyHint();
    }

    private void RefreshWorkflowSuggestions()
    {
        WorkflowSuggestions.Clear();
        var workflow = _app.JobRunner.GetPublishWorkflow(5);
        foreach (var suggestion in workflow.Suggestions)
        {
            WorkflowSuggestions.Add(new WorkflowSuggestionItem
            {
                JobId = suggestion.JobId,
                Title = suggestion.Title,
                ActionLabel = suggestion.ActionLabel,
                Reason = suggestion.Reason
            });
        }

        HasWorkflowSuggestions = WorkflowSuggestions.Count > 0;
    }

    private void PopulateSteps(PublishWizardState state)
    {
        var activeOrder = state.Steps
            .FirstOrDefault(step => step.Status == WizardStepStatus.Active)
            ?.Order;

        Steps.Clear();
        foreach (var step in state.Steps)
        {
            Steps.Add(new WizardStepItem
            {
                Order = step.Order,
                Title = step.Title,
                Description = step.Description,
                Status = step.Status,
                IsCurrent = activeOrder == step.Order
            });
        }

        UpdateWizardStepProgress();
        ApplyWizardStepsEmptyHint();
    }

    private void UpdateWizardStepProgress()
    {
        var total = Steps.Count;
        if (total == 0)
        {
            WizardStepProgressPercent = 0;
            WizardStepProgressText = string.Empty;
            OnPropertyChanged(nameof(ShowWizardStepProgress));
            OnPropertyChanged(nameof(ShowWizardStepsList));
            return;
        }

        var done = Steps.Count(step => step.Status is WizardStepStatus.Done or WizardStepStatus.Skipped);
        WizardStepProgressPercent = (int)Math.Round(done * 100.0 / total);
        WizardStepProgressText = string.Format(LocalizationService.T("wizard.page.stepProgress"), done, total);
        OnPropertyChanged(nameof(ShowWizardStepProgress));
        OnPropertyChanged(nameof(ShowWizardStepsList));
    }

    private void ApplyWizardStepsEmptyHint()
    {
        WizardStepsEmptyHint = !HasSelectedJob
            ? LocalizationService.T("wizard.page.noJobSelected")
            : Steps.Count == 0
                ? LocalizationService.T("wizard.page.noSteps")
                : string.Empty;
    }

    private void ApplyWizardJobAvailabilityHints()
    {
        WizardNoJobsHint = WizardJobOptions.Count == 0
            ? LocalizationService.T("wizard.page.noJobsAvailable")
            : string.Empty;
        OnPropertyChanged(nameof(HasWizardJobs));
        OnPropertyChanged(nameof(WizardJobCountText));
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

    private Task<bool> OnConfirmCleanupAsync(CleanupConfirmation confirmation)
    {
        var tcs = new TaskCompletionSource<bool>();
        ConfirmCleanupRequested?.Invoke(confirmation, tcs);
        return tcs.Task;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(WizardPageTitlePrefix));
        OnPropertyChanged(nameof(WizardSimpleHeroTitle));
        OnPropertyChanged(nameof(WizardSimpleFlowHint));
        OnPropertyChanged(nameof(WizardOtherSuggestionsTitle));
        OnPropertyChanged(nameof(WizardSelectJobHeader));
        OnPropertyChanged(nameof(WizardSelectJobPlaceholder));
        OnPropertyChanged(nameof(WizardPublishStepsTitle));
        OnPropertyChanged(nameof(ExecuteCurrentStepLabel));
        OnPropertyChanged(nameof(WizardRefreshLabel));
        OnPropertyChanged(nameof(WizardOpenJobLabel));
        OnPropertyChanged(nameof(WizardAutoRunLabel));
        OnPropertyChanged(nameof(WizardRefreshingHint));
        OnPropertyChanged(nameof(WizardStepsLoadingHint));
        OnPropertyChanged(nameof(WizardJobCountText));

        ApplyWizardJobAvailabilityHints();
        ApplyWizardStepsEmptyHint();
        UpdateWizardStepProgress();

        if (!string.IsNullOrWhiteSpace(SelectedJobId))
            LoadWizardStateForJob(SelectedJobId);
        else
            LoadWizardStateEmpty();
    }

    public event Action<string, string>? MessageRequested;
    public event Action<string>? NavigateToJobRequested;
    public event Action<CleanupConfirmation, TaskCompletionSource<bool>>? ConfirmCleanupRequested;
}