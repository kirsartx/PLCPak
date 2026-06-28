using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using FrameworkElement = Microsoft.UI.Xaml.FrameworkElement;
using PLCPak.Core.Models;
using PLCPak.WinUI.Infrastructure;
using PLCPak.WinUI.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace PLCPak.WinUI;

public sealed partial class JobsPage : UserControl
{
    private JobsViewModel? _viewModel;
    private bool _wizardScrollScheduled;
    private int _pendingWizardScrollTabIndex;
    private bool _wizardCompletionAnimating;
    private bool _syncingJobsListSelection;
    private bool _syncListSelectionScheduled;
    private bool _syncingWizardTab;
    private bool _syncingFilterCombos;
    private int _listSelectionRetryCount;
    private bool _visibilitySyncScheduled;

    public JobsPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WireViewModel();
        Loaded += OnJobsPageLoaded;
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => OnVisibilityPropertyChanged());
    }

    private void OnVisibilityPropertyChanged()
    {
        if (Visibility != Visibility.Visible || _visibilitySyncScheduled)
            return;

        _visibilitySyncScheduled = true;
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _visibilitySyncScheduled = false;
            if (Visibility != Visibility.Visible)
                return;

            SyncFilterCombosFromViewModel();
            ScheduleSyncListSelection(lowPriority: true);
            SyncWizardTabFromViewModel();
        });
    }

    private void OnJobsPageLoaded(object sender, RoutedEventArgs e)
    {
        WireViewModel();
        SyncFilterCombosFromViewModel();
        ScheduleSyncListSelection();
        SyncWizardTabFromViewModel();
    }

    private JobsViewModel? ViewModel => DataContext as JobsViewModel;

    private void WireViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.JobLogScrollToEndRequested -= ScrollJobLogToEnd;
            _viewModel.WizardStepScrollRequested -= ScrollToWizardStep;
            _viewModel.WizardStepCompletedAnimationRequested -= PlayWizardStepCompletionAnimation;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Jobs.CollectionChanged -= OnJobsCollectionChanged;
        }

        _viewModel = ViewModel;
        if (_viewModel is not null)
        {
            _viewModel.JobLogScrollToEndRequested += ScrollJobLogToEnd;
            _viewModel.WizardStepScrollRequested += ScrollToWizardStep;
            _viewModel.WizardStepCompletedAnimationRequested += PlayWizardStepCompletionAnimation;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Jobs.CollectionChanged += OnJobsCollectionChanged;
            ScheduleSyncListSelection();
            SyncWizardTabFromViewModel();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(JobsViewModel.SelectedJob))
            ScheduleSyncListSelection();
        else if (e.PropertyName is nameof(JobsViewModel.SelectedWizardTabIndex))
            SyncWizardTabFromViewModel();
        else if (e.PropertyName is nameof(JobsViewModel.SelectedJobFilter)
                 or nameof(JobsViewModel.SelectedJobSort)
                 or nameof(JobsViewModel.SelectedTagFilter))
            SyncFilterCombosFromViewModel();
        else if (e.PropertyName is nameof(JobsViewModel.HasWizardJob))
            SyncWizardTabFromViewModel();
    }

    private void OnJobsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => ScheduleSyncListSelection();

    private bool IsJobsListReady()
        => IsLoaded && Visibility == Visibility.Visible && JobsListView is not null;

    private void ScheduleSyncListSelection(bool lowPriority = false)
    {
        if (_syncListSelectionScheduled)
            return;

        _syncListSelectionScheduled = true;
        var priority = lowPriority ? DispatcherQueuePriority.Low : DispatcherQueuePriority.Normal;
        DispatcherQueue.TryEnqueue(priority, () =>
        {
            _syncListSelectionScheduled = false;
            if (_viewModel?.IsSyncingJobsList == true)
            {
                ScheduleSyncListSelection(lowPriority);
                return;
            }

            SyncListSelectionFromViewModel(lowPriority);
        });
    }

    private void SyncListSelectionFromViewModel(bool lowPriority = false)
    {
        if (_viewModel is null || _viewModel.IsSyncingJobsList || !IsJobsListReady())
            return;

        var job = _viewModel.SelectedJob;
        if (job is null || !_viewModel.Jobs.Contains(job))
            return;

        _syncingJobsListSelection = true;
        try
        {
            if (TryApplyListSelection(job))
            {
                _listSelectionRetryCount = 0;
                return;
            }

            if (_listSelectionRetryCount < 3)
            {
                _listSelectionRetryCount++;
                var priority = lowPriority ? DispatcherQueuePriority.Low : DispatcherQueuePriority.Normal;
                DispatcherQueue.TryEnqueue(priority, () => SyncListSelectionFromViewModel(lowPriority));
            }
        }
        catch (Exception)
        {
            // ListView 布局未就绪时程序化选中会抛 E_INVALIDARG。
        }
        finally
        {
            _syncingJobsListSelection = false;
        }
    }

    private bool TryApplyListSelection(PublishJob job)
    {
        if (JobsListView is null)
            return false;

        if (JobsListView.ContainerFromItem(job) is null)
            return false;

        if (ReferenceEquals(JobsListView.SelectedItem, job))
            return true;

        JobsListView.SelectedItem = job;
        return true;
    }

    private void SyncFilterCombosFromViewModel()
    {
        if (_viewModel is null)
            return;

        _syncingFilterCombos = true;
        try
        {
            SyncComboSelection(JobFilterComboBox, _viewModel.SelectedJobFilter);
            SyncComboSelection(JobSortComboBox, _viewModel.SelectedJobSort);
            SyncComboSelection(JobTagFilterComboBox, _viewModel.SelectedTagFilter);
        }
        finally
        {
            _syncingFilterCombos = false;
        }
    }

    private static void SyncComboSelection(ComboBox? combo, string? value)
    {
        if (combo is null || string.IsNullOrEmpty(value))
            return;

        if (combo.SelectedItem as string == value)
            return;

        try
        {
            combo.SelectedItem = value;
        }
        catch (ArgumentException)
        {
        }
    }

    private void SyncWizardTabFromViewModel()
    {
        if (_viewModel is null || WizardTabView is null || _syncingWizardTab)
            return;

        if (!_viewModel.HasWizardJob || Visibility != Visibility.Visible)
            return;

        var index = Math.Clamp(_viewModel.SelectedWizardTabIndex, 0, 3);
        if (WizardTabView.SelectedIndex == index)
            return;

        _syncingWizardTab = true;
        try
        {
            WizardTabView.SelectedIndex = index;
        }
        catch (ArgumentException)
        {
            // TabView can throw E_INVALIDARG while headers/layout are updating.
        }
        finally
        {
            _syncingWizardTab = false;
        }
    }

    private void PlayWizardStepCompletionAnimation(int completedStepIndex)
    {
        WizardCompletionFeedback.Play();

        if (_wizardCompletionAnimating || UiMotionHelper.ShouldReduceMotion())
            return;

        DispatcherQueue.TryEnqueue(() =>
        {
            if (WizardSectionBorder is null)
                return;

            _wizardCompletionAnimating = true;
            FlashWizardElementOpacity(WizardSectionBorder, 0.78, 160, 240, () =>
            {
                _wizardCompletionAnimating = false;
            });

            if (WizardStepProgressBar is FrameworkElement progressBar)
                FlashWizardElementOpacity(progressBar, 0.55, 140, 200, onCompleted: null);

            if (completedStepIndex >= 0)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (TryGetWizardStepElement(completedStepIndex) is FrameworkElement stepElement)
                        FlashWizardElementOpacity(stepElement, 0.5, 150, 220, onCompleted: null);
                });
            }
        });
    }

    private FrameworkElement? TryGetWizardStepElement(int tabIndex)
    {
        if (ViewModel?.IsSimpleMode == true && WizardStepsListView is ListView listView)
            return listView.ContainerFromIndex(tabIndex) as FrameworkElement;

        if (WizardStepsItemsControl is ItemsControl itemsControl)
            return itemsControl.ItemContainerGenerator.ContainerFromIndex(tabIndex) as FrameworkElement;

        return null;
    }

    private static void FlashWizardElementOpacity(
        FrameworkElement target,
        double dipOpacity,
        int fadeOutMs,
        int fadeInMs,
        Action? onCompleted)
    {
        var baseOpacity = target.Opacity;
        if (baseOpacity <= 0)
            baseOpacity = 1;

        RunOpacityAnimation(target, baseOpacity, dipOpacity, fadeOutMs, EasingMode.EaseOut, () =>
            RunOpacityAnimation(target, dipOpacity, baseOpacity, fadeInMs, EasingMode.EaseIn, onCompleted));
    }

    private static void RunOpacityAnimation(
        FrameworkElement target,
        double from,
        double to,
        int durationMs,
        EasingMode easingMode,
        Action? onCompleted)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = easingMode }
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        if (onCompleted is not null)
            storyboard.Completed += (_, _) => onCompleted();

        try
        {
            storyboard.Begin();
        }
        catch (ArgumentException)
        {
            onCompleted?.Invoke();
        }
    }

    private void ScrollJobLogToEnd()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (JobLogBox is null)
                return;

            var length = JobLogBox.Text?.Length ?? 0;
            if (length > 0)
            {
                JobLogBox.SelectionStart = Math.Max(0, length - 1);
                JobLogBox.SelectionLength = 0;
            }

            if (FindScrollViewer(JobLogBox) is ScrollViewer scrollViewer)
                scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null, disableAnimation: true);
        });
    }

    private void ScrollToWizardStep(int tabIndex)
    {
        if (tabIndex is < 0 or > 3 || Visibility != Visibility.Visible)
            return;

        _pendingWizardScrollTabIndex = tabIndex;
        if (_wizardScrollScheduled)
            return;

        _wizardScrollScheduled = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _wizardScrollScheduled = false;
            var index = _pendingWizardScrollTabIndex;
            DispatcherQueue.TryEnqueue(() => BringWizardStepIntoView(index));
        });
    }

    private void BringWizardStepIntoView(int tabIndex)
    {
        var animate = !UiMotionHelper.ShouldReduceMotion();
        var options = new BringIntoViewOptions
        {
            VerticalAlignmentRatio = 0.08,
            AnimationDesired = animate
        };

        try
        {
            if (WizardSectionBorder is FrameworkElement wizardSection)
                wizardSection.StartBringIntoView(options);

            if (GetWizardStepPanel(tabIndex) is FrameworkElement stepPanel)
            {
                stepPanel.StartBringIntoView(new BringIntoViewOptions
                {
                    VerticalAlignmentRatio = 0.12,
                    AnimationDesired = animate
                });
            }
            else if (WizardTabView is FrameworkElement tabView)
            {
                tabView.StartBringIntoView(options);
            }
        }
        catch (ArgumentException)
        {
            // WinUI BringIntoView can throw E_INVALIDARG when layout is not ready.
        }
    }

    private FrameworkElement? GetWizardStepPanel(int tabIndex)
        => tabIndex switch
        {
            0 => WizardStepPreparePanel,
            1 => WizardStepLinksPanel,
            2 => WizardStepCopyPanel,
            3 => WizardStepPublishPanel,
            _ => null
        };

    private static ScrollViewer? FindScrollViewer(DependencyObject element)
    {
        if (element is ScrollViewer scrollViewer)
            return scrollViewer;

        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            var found = FindScrollViewer(child);
            if (found is not null)
                return found;
        }

        return null;
    }

    private void PasswordDropPanel_DragOver(object sender, DragEventArgs e)
    {
        var accepts = e.DataView.Contains(StandardDataFormats.StorageItems);
        e.AcceptedOperation = accepts ? DataPackageOperation.Copy : DataPackageOperation.None;
        if (sender is Border border)
            SetDropHighlight(border, accepts);
    }

    private void PasswordDropPanel_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
            SetDropHighlight(border, active: false);
    }

    private async void PasswordDropPanel_Drop(object sender, DragEventArgs e)
    {
        if (sender is Border border)
            SetDropHighlight(border, active: false);
        if (ViewModel is null || !e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var items = await e.DataView.GetStorageItemsAsync();
        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (paths.Count > 0)
            await ViewModel.ImportPasswordSamplesAsync(paths);
    }

    private void InboxDropPanel_DragOver(object sender, DragEventArgs e)
    {
        var accepts = e.DataView.Contains(StandardDataFormats.StorageItems);
        e.AcceptedOperation = accepts ? DataPackageOperation.Copy : DataPackageOperation.None;
        if (sender is Border border)
            SetDropHighlight(border, accepts);
    }

    private void InboxDropPanel_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
            SetDropHighlight(border, active: false);
    }

    private async void InboxDropPanel_Drop(object sender, DragEventArgs e)
    {
        if (sender is Border border)
            SetDropHighlight(border, active: false);
        if (ViewModel is null || !e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var items = await e.DataView.GetStorageItemsAsync();
        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (paths.Count > 0)
            await ViewModel.ImportInboxFilesAsync(paths);
    }

    private static void SetDropHighlight(Border border, bool active)
    {
        if (active)
        {
            border.Background = Application.Current?.Resources["PlcAccentSubtleBrush"] as Brush
                ?? border.Background;
            border.BorderBrush = Application.Current?.Resources["PlcAccentBrush"] as Brush
                ?? border.BorderBrush;
            border.BorderThickness = new Thickness(2);
            return;
        }

        border.Background = Application.Current?.Resources["PlcLayerFillBrush"] as Brush;
        border.BorderBrush = Application.Current?.Resources["PlcCardStrokeBrush"] as Brush;
        border.BorderThickness = new Thickness(1);
    }

    private void JobsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingJobsListSelection
            || ViewModel is null
            || ViewModel.IsSyncingJobsList
            || JobsListView?.SelectedItems is not { } selectedItems)
            return;

        try
        {
            var jobs = selectedItems.OfType<PublishJob>().ToList();
            ViewModel.SyncSelectedJobIds(jobs.Select(job => job.Id));

            var primary = jobs.FirstOrDefault();
            if (primary is not null && !ReferenceEquals(ViewModel.SelectedJob, primary))
                ViewModel.SelectedJob = primary;
        }
        catch (Exception)
        {
        }
    }

    private void JobsListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel?.SelectedJob is not { } job)
            return;

        if (job.Status == JobStatus.Draft && !string.IsNullOrWhiteSpace(job.Source.ThreadUrl))
        {
            if (ViewModel.OpenThreadCommand.CanExecute(null))
                ViewModel.OpenThreadCommand.Execute(null);
            return;
        }

        if (ViewModel.ExecuteSelectedNextActionCommand.CanExecute(null))
            ViewModel.ExecuteSelectedNextActionCommand.Execute(null);
    }

    private void WizardStepsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (ViewModel is null || e.ClickedItem is not JobWizardStepItem step)
            return;

        ViewModel.SelectWizardStepCommand.Execute(step.TabIndex);
    }

    private void WizardTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingWizardTab || ViewModel is null || WizardTabView is null)
            return;

        try
        {
            var index = Math.Clamp(WizardTabView.SelectedIndex, 0, 3);
            if (ViewModel.SelectedWizardTabIndex != index)
                ViewModel.SelectedWizardTabIndex = index;
        }
        catch (ArgumentException)
        {
        }
    }

    private void JobFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingFilterCombos || ViewModel is null || sender is not ComboBox combo)
            return;

        if (combo.SelectedItem is not string value || value == ViewModel.SelectedJobFilter)
            return;

        ViewModel.SelectedJobFilter = value;
    }

    private void JobSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingFilterCombos || ViewModel is null || sender is not ComboBox combo)
            return;

        if (combo.SelectedItem is not string value || value == ViewModel.SelectedJobSort)
            return;

        ViewModel.SelectedJobSort = value;
    }

    private void JobTagFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingFilterCombos || ViewModel is null || sender is not ComboBox combo)
            return;

        if (combo.SelectedItem is not string value || value == ViewModel.SelectedTagFilter)
            return;

        ViewModel.SelectedTagFilter = value;
    }
}