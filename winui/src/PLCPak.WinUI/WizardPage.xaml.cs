using System.Collections.Specialized;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PLCPak.Core;
using PLCPak.WinUI.Infrastructure;
using PLCPak.WinUI.ViewModels;

namespace PLCPak.WinUI;

public sealed partial class WizardPage : UserControl
{
    private WizardViewModel? _viewModel;
    private bool _scrollScheduled;

    public WizardPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WireViewModel();
    }

    private void WireViewModel()
    {
        if (_viewModel is not null)
            _viewModel.Steps.CollectionChanged -= OnStepsChanged;

        _viewModel = DataContext as WizardViewModel;
        if (_viewModel is not null)
            _viewModel.Steps.CollectionChanged += OnStepsChanged;
    }

    private void WizardPage_Loaded(object sender, RoutedEventArgs e)
    {
        WireViewModel();
        if (DataContext is WizardViewModel vm)
        {
            vm.RefreshWizardCommand.Execute(null);
            if (WizardTitleBlock is not null && string.IsNullOrWhiteSpace(WizardTitleBlock.Text))
                WizardTitleBlock.Text = $"{LocalizationService.T("wizard.page.title")} v{AppVersion.Current}";
        }

        ScheduleScrollCurrentStepIntoView();
    }

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => ScheduleScrollCurrentStepIntoView();

    private void ScheduleScrollCurrentStepIntoView()
    {
        if (_scrollScheduled)
            return;

        _scrollScheduled = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _scrollScheduled = false;
            ScrollCurrentStepIntoView();
        });
    }

    private void ScrollCurrentStepIntoView()
    {
        if (_viewModel is null)
            return;

        var current = _viewModel.Steps.FirstOrDefault(step => step.IsCurrent);
        if (current is null)
            return;

        try
        {
            WizardStepsListView.ScrollIntoView(current);
        }
        catch (ArgumentException)
        {
            // ListView container may not be ready yet.
        }
    }

    private void WorkflowSuggestions_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (DataContext is WizardViewModel vm && e.ClickedItem is WorkflowSuggestionItem suggestion)
            vm.SelectSuggestionCommand.Execute(suggestion);
    }
}