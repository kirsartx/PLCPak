using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PLCPak.Core.Models;
using PLCPak.WinUI.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace PLCPak.WinUI;

public sealed partial class MainPage : UserControl
{
    public MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var dialog = new SettingsDialog { XamlRoot = XamlRoot };
        var studio = App.Services.StudioConfig.Load();
        var prefs = App.Services.UiPreferences.Load();
        dialog.Load(ViewModel.CurrentConfig, studio, prefs);
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.ApplySettings(dialog.BuildConfig(ViewModel.CurrentConfig));
            App.Services.StudioConfig.Save(dialog.BuildStudioConfig(studio));
            dialog.ApplyUiPreferences(prefs);
            App.Services.UiPreferences.Save(prefs);
        }
    }

    private async void RecentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel?.SelectedRecentProject is not null)
            await ViewModel.SelectRecentProjectCommand.ExecuteAsync(null);
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.RemoveSelectedCommand.Execute(SourceListView.SelectedItems);
    }

    private void SourceDropPanel_DragOver(object sender, DragEventArgs e)
    {
        var accepts = e.DataView.Contains(StandardDataFormats.StorageItems);
        e.AcceptedOperation = accepts ? DataPackageOperation.Copy : DataPackageOperation.None;
        SetDropHighlight(SourceDropPanel, accepts);
    }

    private void SourceDropPanel_DragLeave(object sender, DragEventArgs e)
        => SetDropHighlight(SourceDropPanel, active: false);

    private async void SourceDropPanel_Drop(object sender, DragEventArgs e)
    {
        SetDropHighlight(SourceDropPanel, active: false);
        if (ViewModel is null || !e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var items = await e.DataView.GetStorageItemsAsync();
        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (paths.Count > 0)
            await ViewModel.HandleDroppedPathsAsync(paths);
    }

    private void AdDropPanel_DragOver(object sender, DragEventArgs e)
    {
        var accepts = e.DataView.Contains(StandardDataFormats.StorageItems);
        e.AcceptedOperation = accepts ? DataPackageOperation.Copy : DataPackageOperation.None;
        SetDropHighlight(AdDropPanel, accepts);
    }

    private void AdDropPanel_DragLeave(object sender, DragEventArgs e)
        => SetDropHighlight(AdDropPanel, active: false);

    private async void AdDropPanel_Drop(object sender, DragEventArgs e)
    {
        SetDropHighlight(AdDropPanel, active: false);
        if (ViewModel is null || !e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var items = await e.DataView.GetStorageItemsAsync();
        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (paths.Count > 0)
            await ViewModel.HandleDroppedAdSamplesAsync(paths);
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
}