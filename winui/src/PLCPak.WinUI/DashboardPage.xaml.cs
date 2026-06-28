using Microsoft.UI.Xaml.Controls;
using Button = Microsoft.UI.Xaml.Controls.Button;
using PLCPak.Core.Models;
using PLCPak.WinUI.ViewModels;

namespace PLCPak.WinUI;

public sealed partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
    }

    public event Action<string>? NavigateToJobRequested;

    public void FocusTgPendingSection()
        => TgPendingList?.StartBringIntoView();

    private void RecentJobs_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentActivityEntry entry)
            NavigateToJobRequested?.Invoke(entry.JobId);
    }

    private void PublishQueue_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PublishQueueEntry entry)
            NavigateToJobRequested?.Invoke(entry.JobId);
    }

    private void HealthIssues_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is JobHealthIssue issue)
            NavigateToJobRequested?.Invoke(issue.JobId);
    }

    private void TgPending_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not TgPendingEntry entry)
            return;

        if (DataContext is DashboardViewModel vm)
            vm.GoToTgPendingJobCommand.Execute(entry);
        else if (!string.IsNullOrWhiteSpace(entry.JobId))
            NavigateToJobRequested?.Invoke(entry.JobId);
    }

    private void SendTgPending_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not TgPendingEntry entry)
            return;

        if (DataContext is DashboardViewModel vm)
            vm.SendTgPendingJobCommand.Execute(entry);
    }
}