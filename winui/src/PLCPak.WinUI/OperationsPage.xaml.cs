using Microsoft.UI.Xaml.Controls;
using PLCPak.Core.Models;
using PLCPak.WinUI.ViewModels;

namespace PLCPak.WinUI;

public sealed partial class OperationsPage : UserControl
{
    public OperationsPage()
    {
        InitializeComponent();
    }

    public event Action<string>? NavigateToJobRequested;

    private void StaleJobs_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not StaleJobItem item)
            return;

        if (DataContext is OperationsViewModel vm)
            vm.GoToStaleJobCommand.Execute(item);
        else if (!string.IsNullOrWhiteSpace(item.JobId))
            NavigateToJobRequested?.Invoke(item.JobId);
    }

    private void DuplicateGroups_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not DuplicateGroupItem group || group.Jobs.Count == 0)
            return;

        NavigateToJobRequested?.Invoke(group.Jobs[0].JobId);
    }

    private void DuplicateMergeSuggestions_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not DuplicateMergeSuggestionItem item)
            return;

        if (!string.IsNullOrWhiteSpace(item.TargetJobId))
            NavigateToJobRequested?.Invoke(item.TargetJobId);
    }

    private void BatchMergePreviewItems_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not BatchMergePreviewItem item)
            return;

        if (!string.IsNullOrWhiteSpace(item.TargetJobId))
            NavigateToJobRequested?.Invoke(item.TargetJobId);
    }
}