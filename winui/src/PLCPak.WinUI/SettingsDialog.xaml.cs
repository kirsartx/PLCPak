using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PLCPak.Core.Models;
using PLCPak.Core.Services;
using PLCPak.WinUI.Infrastructure;

namespace PLCPak.WinUI;

public sealed partial class SettingsDialog : ContentDialog
{
    private Dictionary<string, string> _shortcutOverrides = new(StringComparer.OrdinalIgnoreCase);
    private string _previewTheme = "light";
    private string? _configPath;

    public event EventHandler? ReplayOnboardingRequested;

    public SettingsDialog()
    {
        InitializeComponent();
        NightlyExportActivityLogStatsBothCheck.Checked += (_, _) => ApplyNightlyStatsBothEnabled(true);
        AppThemeBox.SelectionChanged += AppThemeBox_SelectionChanged;
        UiLanguageBox.SelectionChanged += UiLanguageBox_SelectionChanged;
        LocalizationService.LanguageChanged += OnLanguageChanged;
        ApplyLocalizedLabels();
    }

    private void AppThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppThemeBox.SelectedItem is null)
            return;

        ThemeService.Apply(GetSelectedAppTheme());
    }

    private void UiLanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UiLanguageBox.SelectedItem is null)
            return;

        LocalizationService.SetLanguage(GetSelectedUiLanguage(), persist: false);
    }

    private void OnLanguageChanged() => ApplyLocalizedLabels();

    private void ApplyLocalizedLabels()
    {
        Title = LocalizationService.T("settings.title");
        PrimaryButtonText = LocalizationService.T("settings.save");
        CloseButtonText = LocalizationService.T("settings.cancel");
        AppearanceExpander.Header = LocalizationService.T("settings.appearance");
        WorkspaceExpander.Header = LocalizationService.T("settings.section.workspace");
        JobsExpander.Header = LocalizationService.T("settings.jobs.sectionTitle");
        ShortcutsExpander.Header = LocalizationService.T("settings.section.shortcuts");
        IntegrationsExpander.Header = LocalizationService.T("settings.section.integrations");
        NightlyExpander.Header = LocalizationService.T("settings.nightly.sectionTitle");
        AdvancedExpander.Header = LocalizationService.T("settings.section.advanced");
        AppThemeBox.Header = LocalizationService.T("settings.theme");
        UiLanguageBox.Header = LocalizationService.T("settings.language");
        if (AppThemeBox.Items.Count >= 2)
        {
            ((ComboBoxItem)AppThemeBox.Items[0]).Content = LocalizationService.T("settings.theme.light");
            ((ComboBoxItem)AppThemeBox.Items[1]).Content = LocalizationService.T("settings.theme.dark");
        }

        if (UiLanguageBox.Items.Count >= 2)
        {
            ((ComboBoxItem)UiLanguageBox.Items[0]).Content = LocalizationService.T("settings.language.zh");
            ((ComboBoxItem)UiLanguageBox.Items[1]).Content = LocalizationService.T("settings.language.en");
        }

        UpdateConfigPathText();
        ExportConfigBtn.Content = LocalizationService.T("settings.general.exportConfig");
        ImportConfigBtn.Content = LocalizationService.T("settings.general.importConfig");
        WorkspaceRootBox.Header = LocalizationService.T("settings.general.workspaceRoot");
        WorkspaceRootBox.PlaceholderText = LocalizationService.T("settings.general.workspaceRootPlaceholder");
        TelegramChannelUrlBox.Header = LocalizationService.T("settings.general.telegramChannelUrl");
        ExtractPasswordsBox.Header = LocalizationService.T("settings.general.extractPasswords");
        DefaultPublishTemplateIdBox.Header = LocalizationService.T("settings.general.defaultPublishTemplateId");
        VolumeSizeBox.Header = LocalizationService.T("settings.general.volumeSize");
        ScanTimeoutBox.Header = LocalizationService.T("settings.general.scanTimeout");
        ConfirmThresholdBox.Header = LocalizationService.T("settings.general.confirmThreshold");
        CompressThreadsBox.Header = LocalizationService.T("settings.general.compressThreads");
        TempDirBox.Header = LocalizationService.T("settings.general.tempDir");
        TempDirBox.PlaceholderText = LocalizationService.T("settings.general.tempDirPlaceholder");
        ManifestStaleBox.Header = LocalizationService.T("settings.general.manifestStale");
        RecycleBinCheck.Content = LocalizationService.T("settings.general.recycleBin");
        SkipStoreCheck.Content = LocalizationService.T("settings.general.skipStore");
        PreviewBeforeCleanCheck.Content = LocalizationService.T("settings.general.previewBeforeClean");
        WhitelistSectionTitle.Text = LocalizationService.T("settings.general.whitelist");
        DefaultJobTagsBox.Header = LocalizationService.T("settings.jobs.defaultTags");
        DefaultJobTagsBox.PlaceholderText = LocalizationService.T("settings.jobs.defaultTagsPlaceholder");
        ShowWelcomeOnStartupCheck.Content = LocalizationService.T("settings.jobs.showWelcomeOnStartup");
        ReplayOnboardingBtn.Content = LocalizationService.T("settings.replayOnboarding");
        ProfessionalModeCheck.Content = LocalizationService.T("settings.jobs.professionalMode");
        WizardCompletionFeedbackCheck.Content = LocalizationService.T("settings.wizardFeedback");
        ReduceMotionCheck.Content = LocalizationService.T("settings.reduceMotion");
        DisableGlobalShortcutsCheck.Content = LocalizationService.T("settings.shortcuts.disableGlobal");
        ShortcutsDisableSectionTitle.Text = LocalizationService.T("settings.shortcuts.disableIndividualTitle");
        DisableShortcutBatchArchiveFiltered.Content = LocalizationService.T("settings.shortcuts.batchArchiveFiltered");
        DisableShortcutBatchArchiveSelected.Content = LocalizationService.T("settings.shortcuts.batchArchiveSelected");
        DisableShortcutBatchUnarchiveSelected.Content = LocalizationService.T("settings.shortcuts.batchUnarchiveSelected");
        DisableShortcutBatchDeleteSelected.Content = LocalizationService.T("settings.shortcuts.batchDeleteSelected");
        DisableShortcutBatchTags.Content = LocalizationService.T("settings.shortcuts.batchTags");
        DisableShortcutExportStatsHtml.Content = LocalizationService.T("settings.shortcuts.exportStatsHtml");
        DisableShortcutFilterBatchActivity.Content = LocalizationService.T("settings.shortcuts.filterBatchActivity");
        DisableShortcutExportBatchStatsJson.Content = LocalizationService.T("settings.shortcuts.exportBatchStatsJson");
        DisableShortcutExportBatchStatsCsv.Content = LocalizationService.T("settings.shortcuts.exportBatchStatsCsv");
        DisableShortcutExportBatchStatsAll.Content = LocalizationService.T("settings.shortcuts.exportBatchStatsAll");
        HtmlReportThemeBox.Header = LocalizationService.T("settings.shortcuts.htmlReportTheme");
        if (HtmlReportThemeBox.Items.Count >= 2)
        {
            ((ComboBoxItem)HtmlReportThemeBox.Items[0]).Content = LocalizationService.T("settings.theme.light");
            ((ComboBoxItem)HtmlReportThemeBox.Items[1]).Content = LocalizationService.T("settings.theme.dark");
        }

        EditShortcutOverridesBtn.Content = LocalizationService.T("settings.shortcuts.editOverrides");
        ExportShortcutProfileBtn.Content = LocalizationService.T("settings.shortcuts.exportProfile");
        ImportShortcutProfileBtn.Content = LocalizationService.T("settings.shortcuts.importProfile");
        AutoDownloadThreadAttachmentsCheck.Content = LocalizationService.T("settings.telegram.autoDownloadAttachments");
        ForumDownloadMaxSizeBox.Header = LocalizationService.T("settings.telegram.forumDownloadMaxSize");
        AutoArchiveAfterPublishCheck.Content = LocalizationService.T("settings.publish.autoArchiveAfterPublish");
        AutoGenerateCopyAfterProcessCheck.Content = LocalizationService.T("settings.publish.autoGenerateCopyAfterProcess");
        NightlyAutoSendTgCheck.Content = LocalizationService.T("settings.nightly.autoSendTg");
        NightlyAutoScanDuplicatesCheck.Content = LocalizationService.T("settings.nightly.autoScanDuplicates");
        NightlyAutoMergeDuplicatesCheck.Content = LocalizationService.T("settings.nightly.autoMergeDuplicates");
        NightlyAutoTrimActivityLogCheck.Content = LocalizationService.T("settings.nightly.autoTrimActivityLog");
        NightlyExportActivityLogStatsBothCheck.Content = LocalizationService.T("settings.nightly.exportStatsBoth");
        NightlyExportActivityLogStatsCheck.Content = LocalizationService.T("settings.nightly.exportStatsCsv");
        NightlyExportActivityLogStatsHtmlCheck.Content = LocalizationService.T("settings.nightly.exportStatsHtml");
        NightlyExportActivityLogBatchStatsAllCheck.Content = LocalizationService.T("settings.nightly.exportBatchStatsAll");
        NightlyExportPinnedJobsCsvCheck.Content = LocalizationService.T("settings.nightly.exportPinnedJobsCsv");
        ActivityLogKeepDaysBox.Header = LocalizationService.T("settings.activityLog.keepDays");
        ActivityLogPageSizeBox.Header = LocalizationService.T("settings.activityLog.pageSize");
        EnableScheduledBatchCheck.Content = LocalizationService.T("settings.activityLog.scheduledBatchEnable");
        ScheduledBatchHourBox.Header = LocalizationService.T("settings.activityLog.scheduledHour");
        ScheduledBatchMinuteBox.Header = LocalizationService.T("settings.activityLog.scheduledMinute");
        ScheduledBatchFilterBox.Header = LocalizationService.T("settings.activityLog.scheduledFilter");
        ShowShortcutsBtn.Content = LocalizationService.T("settings.shortcuts.showHelp");
    }

    private void UpdateConfigPathText()
    {
        ConfigPathText.Text = string.IsNullOrWhiteSpace(_configPath)
            ? LocalizationService.T("settings.configPathUnknown")
            : string.Format(LocalizationService.T("settings.configPathFormat"), _configPath);
    }

    public event Func<Task>? ExportConfigRequested;
    public event Func<Task>? ImportConfigRequested;
    public event Func<Task>? ExportShortcutProfileRequested;
    public event Func<Task>? ImportShortcutProfileRequested;

    public void SetConfigPath(string path)
    {
        _configPath = path;
        UpdateConfigPathText();
    }

    public void Load(CompressConfig config, StudioConfig studio, UiPreferences? uiPreferences = null)
    {
        WorkspaceRootBox.Text = studio.WorkspaceRoot;
        TelegramChannelUrlBox.Text = studio.TelegramChannelUrl;
        ExtractPasswordsBox.Text = string.Join(Environment.NewLine, studio.ExtractPasswords);
        DefaultPublishTemplateIdBox.Text = studio.DefaultPublishTemplateId;
        VolumeSizeBox.Text = config.VolumeSizeMB.ToString();
        ScanTimeoutBox.Text = config.AdScanTimeoutSec.ToString();
        ConfirmThresholdBox.Text = config.AdConfirmThreshold.ToString();
        CompressThreadsBox.Text = config.CompressThreads.ToString();
        TempDirBox.Text = config.TempDir;
        ManifestStaleBox.Text = config.ManifestStaleDays.ToString();
        RecycleBinCheck.IsChecked = config.UseRecycleBin;
        SkipStoreCheck.IsChecked = config.SkipStoreCompress;
        PreviewBeforeCleanCheck.IsChecked = config.PreviewBeforeClean;
        WhitelistBox.Text = string.Join(Environment.NewLine, config.Whitelist);
        DefaultJobTagsBox.Text = string.Join(Environment.NewLine, studio.DefaultJobTags);
        ShowWelcomeOnStartupCheck.IsChecked = studio.ShowWelcomeOnStartup;
        ProfessionalModeCheck.IsChecked = uiPreferences?.ProfessionalMode == true;
        WizardCompletionFeedbackCheck.IsChecked = uiPreferences?.EnableWizardCompletionFeedback != false;
        ReduceMotionCheck.IsChecked = uiPreferences?.ReduceMotion == true;
        TelegramBotTokenBox.Password = studio.TelegramBotToken;
        AutoDownloadThreadAttachmentsCheck.IsChecked = studio.AutoDownloadThreadAttachments;
        ForumDownloadMaxSizeBox.Text = studio.ForumDownloadMaxSizeMB.ToString();
        AutoArchiveAfterPublishCheck.IsChecked = studio.AutoArchiveOnPublished;
        AutoGenerateCopyAfterProcessCheck.IsChecked = studio.AutoGenerateCopyAfterProcess;
        NightlyAutoSendTgCheck.IsChecked = studio.NightlyAutoSendTg;
        NightlyAutoScanDuplicatesCheck.IsChecked = studio.NightlyAutoScanDuplicates;
        NightlyAutoMergeDuplicatesCheck.IsChecked = studio.NightlyAutoMergeDuplicates;
        NightlyAutoTrimActivityLogCheck.IsChecked = studio.NightlyAutoTrimActivityLog;
        NightlyExportActivityLogStatsBothCheck.IsChecked = studio.NightlyExportActivityLogStatsBoth;
        NightlyExportActivityLogStatsCheck.IsChecked = studio.NightlyExportActivityLogStats;
        NightlyExportActivityLogStatsHtmlCheck.IsChecked = studio.NightlyExportActivityLogStatsHtml;
        NightlyExportActivityLogBatchStatsAllCheck.IsChecked = studio.NightlyExportActivityLogBatchStatsAll;
        NightlyExportPinnedJobsCsvCheck.IsChecked = studio.NightlyExportPinnedJobsCsv;
        ActivityLogKeepDaysBox.Text = studio.ActivityLogKeepDays.ToString();
        ActivityLogPageSizeBox.Text = studio.ActivityLogPageSize.ToString();
        EnableScheduledBatchCheck.IsChecked = studio.EnableScheduledBatch;
        ScheduledBatchHourBox.Text = studio.ScheduledBatchHour.ToString();
        ScheduledBatchMinuteBox.Text = studio.ScheduledBatchMinute.ToString();
        ScheduledBatchFilterBox.Text = studio.ScheduledBatchFilter;
        DisableGlobalShortcutsCheck.IsChecked = uiPreferences?.DisableGlobalShortcuts == true;
        var disabled = uiPreferences?.DisabledShortcuts ?? [];
        DisableShortcutBatchArchiveFiltered.IsChecked = disabled.Contains(
            GlobalShortcutRegistry.JobsBatchArchiveFiltered, StringComparer.OrdinalIgnoreCase);
        DisableShortcutBatchArchiveSelected.IsChecked = disabled.Contains(
            GlobalShortcutRegistry.JobsBatchArchiveSelected, StringComparer.OrdinalIgnoreCase);
        DisableShortcutBatchUnarchiveSelected.IsChecked = disabled.Contains(
            GlobalShortcutRegistry.JobsBatchUnarchiveSelected, StringComparer.OrdinalIgnoreCase);
        DisableShortcutBatchDeleteSelected.IsChecked = disabled.Contains(
            GlobalShortcutRegistry.JobsBatchDeleteSelected, StringComparer.OrdinalIgnoreCase);
        DisableShortcutBatchTags.IsChecked = disabled.Contains(
            GlobalShortcutRegistry.JobsBatchAppendTags, StringComparer.OrdinalIgnoreCase);
        DisableShortcutExportStatsHtml.IsChecked = disabled.Contains(
            GlobalShortcutRegistry.OpsExportStatsHtml, StringComparer.OrdinalIgnoreCase);
        DisableShortcutFilterBatchActivity.IsChecked = disabled.Contains(
            GlobalShortcutRegistry.OpsFilterBatchActivity, StringComparer.OrdinalIgnoreCase);
        DisableShortcutExportBatchStatsJson.IsChecked = disabled.Contains(
            GlobalShortcutRegistry.OpsExportBatchStatsJson, StringComparer.OrdinalIgnoreCase);
        DisableShortcutExportBatchStatsCsv.IsChecked = disabled.Contains(
            GlobalShortcutRegistry.OpsExportBatchStatsCsv, StringComparer.OrdinalIgnoreCase);
        DisableShortcutExportBatchStatsAll.IsChecked = disabled.Contains(
            GlobalShortcutRegistry.OpsExportBatchStatsAll, StringComparer.OrdinalIgnoreCase);
        _shortcutOverrides = uiPreferences?.ShortcutOverrides is { Count: > 0 } overrides
            ? new Dictionary<string, string>(overrides, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SelectHtmlReportTheme(uiPreferences?.HtmlReportTheme);
        _previewTheme = UiStringTable.NormalizeTheme(uiPreferences?.AppTheme);
        SelectAppTheme(_previewTheme);
        SelectUiLanguage(uiPreferences?.UiLanguage);
    }

    public string LoadedTheme => _previewTheme;

    public void ApplyUiPreferences(UiPreferences prefs)
    {
        prefs.ProfessionalMode = ProfessionalModeCheck.IsChecked == true;
        prefs.EnableWizardCompletionFeedback = WizardCompletionFeedbackCheck.IsChecked != false;
        prefs.ReduceMotion = ReduceMotionCheck.IsChecked == true;
        prefs.DisableGlobalShortcuts = DisableGlobalShortcutsCheck.IsChecked == true;
        prefs.DisabledShortcuts = BuildDisabledShortcuts();
        prefs.ShortcutOverrides = new Dictionary<string, string>(_shortcutOverrides, StringComparer.OrdinalIgnoreCase);
        prefs.HtmlReportTheme = GetSelectedHtmlReportTheme();
        prefs.AppTheme = GetSelectedAppTheme();
        prefs.UiLanguage = GetSelectedUiLanguage();
    }

    private void SelectAppTheme(string? theme)
    {
        var normalized = UiStringTable.NormalizeTheme(theme);
        foreach (var item in AppThemeBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                AppThemeBox.SelectedItem = item;
                return;
            }
        }

        AppThemeBox.SelectedIndex = 0;
    }

    private void SelectUiLanguage(string? language)
    {
        var normalized = UiStringTable.NormalizeLanguage(language);
        foreach (var item in UiLanguageBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                UiLanguageBox.SelectedItem = item;
                return;
            }
        }

        UiLanguageBox.SelectedIndex = 0;
    }

    private string GetSelectedAppTheme()
    {
        if (AppThemeBox.SelectedItem is ComboBoxItem { Tag: { } tag }
            && string.Equals(tag.ToString(), "dark", StringComparison.OrdinalIgnoreCase))
            return "dark";

        return "light";
    }

    private string GetSelectedUiLanguage()
    {
        if (UiLanguageBox.SelectedItem is ComboBoxItem { Tag: { } tag }
            && string.Equals(tag.ToString(), "en", StringComparison.OrdinalIgnoreCase))
            return "en";

        return "zh";
    }

    private void SelectHtmlReportTheme(string? theme)
    {
        var normalized = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";
        foreach (var item in HtmlReportThemeBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                HtmlReportThemeBox.SelectedItem = item;
                return;
            }
        }

        HtmlReportThemeBox.SelectedIndex = 0;
    }

    private string GetSelectedHtmlReportTheme()
    {
        if (HtmlReportThemeBox.SelectedItem is ComboBoxItem { Tag: { } tag }
            && string.Equals(tag.ToString(), "dark", StringComparison.OrdinalIgnoreCase))
            return "dark";

        return "light";
    }

    private List<string> BuildDisabledShortcuts()
    {
        var disabled = new List<string>();
        if (DisableShortcutBatchArchiveFiltered.IsChecked == true)
            disabled.Add(GlobalShortcutRegistry.JobsBatchArchiveFiltered);
        if (DisableShortcutBatchArchiveSelected.IsChecked == true)
            disabled.Add(GlobalShortcutRegistry.JobsBatchArchiveSelected);
        if (DisableShortcutBatchUnarchiveSelected.IsChecked == true)
            disabled.Add(GlobalShortcutRegistry.JobsBatchUnarchiveSelected);
        if (DisableShortcutBatchDeleteSelected.IsChecked == true)
            disabled.Add(GlobalShortcutRegistry.JobsBatchDeleteSelected);
        if (DisableShortcutBatchTags.IsChecked == true)
            disabled.Add(GlobalShortcutRegistry.JobsBatchAppendTags);
        if (DisableShortcutExportStatsHtml.IsChecked == true)
            disabled.Add(GlobalShortcutRegistry.OpsExportStatsHtml);
        if (DisableShortcutFilterBatchActivity.IsChecked == true)
            disabled.Add(GlobalShortcutRegistry.OpsFilterBatchActivity);
        if (DisableShortcutExportBatchStatsJson.IsChecked == true)
            disabled.Add(GlobalShortcutRegistry.OpsExportBatchStatsJson);
        if (DisableShortcutExportBatchStatsCsv.IsChecked == true)
            disabled.Add(GlobalShortcutRegistry.OpsExportBatchStatsCsv);
        if (DisableShortcutExportBatchStatsAll.IsChecked == true)
            disabled.Add(GlobalShortcutRegistry.OpsExportBatchStatsAll);
        return disabled;
    }

    public CompressConfig BuildConfig(CompressConfig current)
    {
        var config = current.Clone();
        config.VolumeSizeMB = ParseInt(VolumeSizeBox.Text, config.VolumeSizeMB);
        config.AdScanTimeoutSec = ParseInt(ScanTimeoutBox.Text, config.AdScanTimeoutSec);
        config.AdConfirmThreshold = ParseInt(ConfirmThresholdBox.Text, config.AdConfirmThreshold);
        config.CompressThreads = ParseInt(CompressThreadsBox.Text, config.CompressThreads);
        config.TempDir = TempDirBox.Text.Trim();
        config.ManifestStaleDays = ParseInt(ManifestStaleBox.Text, config.ManifestStaleDays);
        config.UseRecycleBin = RecycleBinCheck.IsChecked == true;
        config.SkipStoreCompress = SkipStoreCheck.IsChecked == true;
        config.PreviewBeforeClean = PreviewBeforeCleanCheck.IsChecked == true;
        config.Whitelist = WhitelistBox.Text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        return config;
    }

    public StudioConfig BuildStudioConfig(StudioConfig current)
    {
        var config = current.Clone();
        config.WorkspaceRoot = WorkspaceRootBox.Text.Trim();
        config.TelegramChannelUrl = TelegramChannelUrlBox.Text.Trim();
        config.ExtractPasswords = ExtractPasswordsBox.Text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        config.DefaultPublishTemplateId = string.IsNullOrWhiteSpace(DefaultPublishTemplateIdBox.Text)
            ? config.DefaultPublishTemplateId
            : DefaultPublishTemplateIdBox.Text.Trim();
        config.TelegramBotToken = TelegramBotTokenBox.Password;
        config.DefaultJobTags = DefaultJobTagsBox.Text
            .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        config.ShowWelcomeOnStartup = ShowWelcomeOnStartupCheck.IsChecked != false;
        config.AutoDownloadThreadAttachments = AutoDownloadThreadAttachmentsCheck.IsChecked == true;
        config.ForumDownloadMaxSizeMB = ParseInt(ForumDownloadMaxSizeBox.Text, config.ForumDownloadMaxSizeMB);
        config.AutoArchiveOnPublished = AutoArchiveAfterPublishCheck.IsChecked == true;
        config.AutoGenerateCopyAfterProcess = AutoGenerateCopyAfterProcessCheck.IsChecked == true;
        config.NightlyAutoSendTg = NightlyAutoSendTgCheck.IsChecked == true;
        config.NightlyAutoScanDuplicates = NightlyAutoScanDuplicatesCheck.IsChecked == true;
        config.NightlyAutoMergeDuplicates = NightlyAutoMergeDuplicatesCheck.IsChecked == true;
        config.NightlyAutoTrimActivityLog = NightlyAutoTrimActivityLogCheck.IsChecked == true;
        config.NightlyExportActivityLogStatsBoth = NightlyExportActivityLogStatsBothCheck.IsChecked == true;
        config.NightlyExportActivityLogStats = NightlyExportActivityLogStatsCheck.IsChecked == true;
        config.NightlyExportActivityLogStatsHtml = NightlyExportActivityLogStatsHtmlCheck.IsChecked == true;
        config.NightlyExportActivityLogBatchStatsAll = NightlyExportActivityLogBatchStatsAllCheck.IsChecked == true;
        config.NightlyExportPinnedJobsCsv = NightlyExportPinnedJobsCsvCheck.IsChecked == true;
        config.ActivityLogKeepDays = Math.Max(1, ParseInt(ActivityLogKeepDaysBox.Text, config.ActivityLogKeepDays));
        config.ActivityLogPageSize = Math.Max(1, ParseInt(ActivityLogPageSizeBox.Text, config.ActivityLogPageSize));
        config.EnableScheduledBatch = EnableScheduledBatchCheck.IsChecked == true;
        config.ScheduledBatchHour = ParseInt(ScheduledBatchHourBox.Text, config.ScheduledBatchHour);
        config.ScheduledBatchMinute = ParseInt(ScheduledBatchMinuteBox.Text, config.ScheduledBatchMinute);
        config.ScheduledBatchFilter = string.IsNullOrWhiteSpace(ScheduledBatchFilterBox.Text)
            ? config.ScheduledBatchFilter
            : ScheduledBatchFilterBox.Text.Trim();
        return config;
    }

    private static int ParseInt(string? text, int fallback)
        => int.TryParse(text, out var value) ? value : fallback;

    private void ApplyNightlyStatsBothEnabled(bool enabled)
    {
        if (!enabled)
            return;

        NightlyExportActivityLogStatsCheck.IsChecked = true;
        NightlyExportActivityLogStatsHtmlCheck.IsChecked = true;
        NightlyExportActivityLogBatchStatsAllCheck.IsChecked = true;
    }

    private async void ExportConfigBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ExportConfigRequested is not null)
            await ExportConfigRequested();
    }

    private async void ImportConfigBtn_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ImportConfigRequested is not null)
            await ImportConfigRequested();
    }

    private async void ExportShortcutProfileBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ExportShortcutProfileRequested is not null)
            await ExportShortcutProfileRequested();
    }

    private async void ImportShortcutProfileBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ImportShortcutProfileRequested is not null)
            await ImportShortcutProfileRequested();
    }

    private async void EditShortcutOverridesBtn_Click(object sender, RoutedEventArgs e)
    {
        var fields = new Dictionary<string, TextBox>(StringComparer.OrdinalIgnoreCase);
        var panel = new StackPanel { Spacing = 10, MinWidth = 360 };
        panel.Children.Add(new TextBlock
        {
            Text = LocalizationService.T("settings.shortcuts.overrideHint"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
        });

        foreach (var definition in GlobalShortcutRegistry.Definitions)
        {
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(new TextBlock
            {
                Text = string.Format(
                    LocalizationService.T("settings.shortcuts.overrideLabelFormat"),
                    definition.Label,
                    definition.Keys),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            var box = new TextBox
            {
                PlaceholderText = definition.Keys,
                Text = _shortcutOverrides.TryGetValue(definition.Id, out var value) ? value : string.Empty
            };
            fields[definition.Id] = box;
            row.Children.Add(box);
            panel.Children.Add(row);
        }

        var dialog = new ContentDialog
        {
            Title = LocalizationService.T("settings.shortcuts.overrideTitle"),
            Content = new ScrollViewer { Content = panel, MaxHeight = 420 },
            PrimaryButtonText = LocalizationService.T("settings.confirm"),
            CloseButtonText = LocalizationService.T("settings.cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var provisionalOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (shortcutId, box) in fields)
        {
            var text = box.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (!GlobalShortcutBindingService.TryParse(text, out _))
            {
                var error = new ContentDialog
                {
                    Title = LocalizationService.T("settings.shortcuts.invalidFormatTitle"),
                    Content = string.Format(
                        LocalizationService.T("settings.shortcuts.invalidFormatContent"),
                        GlobalShortcutRegistry.FindDefinition(shortcutId)?.Label,
                        text),
                    CloseButtonText = LocalizationService.T("settings.close"),
                    XamlRoot = XamlRoot
                };
                await error.ShowAsync();
                return;
            }

            provisionalOverrides[shortcutId] = text;
        }

        var previewPrefs = new UiPreferences
        {
            DisableGlobalShortcuts = DisableGlobalShortcutsCheck.IsChecked == true,
            DisabledShortcuts = BuildDisabledShortcuts(),
            ShortcutOverrides = provisionalOverrides
        };
        var conflicts = GlobalShortcutConflictService.FindConflicts(previewPrefs);
        if (conflicts.Count > 0)
        {
            var conflictDialog = new ContentDialog
            {
                Title = LocalizationService.T("settings.shortcuts.conflictTitle"),
                Content = new TextBlock
                {
                    Text = GlobalShortcutConflictService.BuildConflictMessage(conflicts)
                        + Environment.NewLine + Environment.NewLine
                        + LocalizationService.T("settings.shortcuts.conflictAdjust"),
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = LocalizationService.T("settings.close"),
                XamlRoot = XamlRoot
            };
            await conflictDialog.ShowAsync();
            return;
        }

        _shortcutOverrides.Clear();
        foreach (var (shortcutId, text) in provisionalOverrides)
            _shortcutOverrides[shortcutId] = text;
    }

    private void ReplayOnboardingBtn_Click(object sender, RoutedEventArgs e)
        => ReplayOnboardingRequested?.Invoke(this, EventArgs.Empty);

    private async void ShowShortcutsBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = LocalizationService.T("settings.shortcuts.helpTitle"),
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = LocalizationService.T("settings.shortcuts.helpText"),
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
                },
                MaxHeight = 360
            },
            CloseButtonText = LocalizationService.T("settings.close"),
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }
}