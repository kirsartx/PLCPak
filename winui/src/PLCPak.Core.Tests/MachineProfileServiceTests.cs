using PLCPak.Core;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class MachineProfileServiceTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceService _workspace;
    private readonly StudioConfigService _studioConfig;
    private readonly UiPreferencesService _uiPreferences;

    public MachineProfileServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-profile-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        Directory.CreateDirectory(data);

        var workspaceRoot = Path.Combine(_root, "workspace");
        File.WriteAllText(Path.Combine(data, "studio-config.json"),
            $"{{\"workspaceRoot\":\"{workspaceRoot.Replace("\\", "\\\\")}\"}}");

        var paths = new AppPaths(Path.Combine(_root, "app"));
        _studioConfig = new StudioConfigService(paths);
        _workspace = new WorkspaceService(paths, _studioConfig);
        _workspace.EnsureLayout();
        _uiPreferences = new UiPreferencesService(_workspace);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Export_and_import_roundtrip_preserves_studio_and_ui_prefs()
    {
        File.WriteAllText(_studioConfig.ConfigPath, """{"telegramBotToken":"test-token"}""");
        File.WriteAllText(_uiPreferences.PrefsPath, """{"hasSeenWelcome":true}""");

        var service = new MachineProfileService(_workspace, _studioConfig, _uiPreferences);
        var export = service.ExportMachineProfile();

        Assert.True(File.Exists(export.ExportPath));
        Assert.True(export.IncludesStudioConfig);
        Assert.True(export.IncludesUiPreferences);
        Assert.True(export.IncludesShortcutProfile);

        var bundle = JsonHelper.ReadFile<PLCPak.Core.Models.MachineProfileBundle>(export.ExportPath);
        Assert.NotNull(bundle);
        Assert.False(string.IsNullOrWhiteSpace(bundle!.ShortcutProfileJson));

        File.Delete(_studioConfig.ConfigPath);
        File.Delete(_uiPreferences.PrefsPath);

        var import = service.ImportMachineProfile(export.ExportPath, merge: false);

        Assert.True(import.Success);
        Assert.True(import.StudioConfigImported);
        Assert.True(import.UiPreferencesImported);
        Assert.True(import.ShortcutProfileImported);
        Assert.True(File.Exists(_studioConfig.ConfigPath));
        Assert.True(File.Exists(_uiPreferences.PrefsPath));
    }

    [Fact]
    public void Export_includes_shortcut_profile_with_overrides()
    {
        File.WriteAllText(_uiPreferences.PrefsPath, """
            {
              "shortcutOverrides": { "jobs.batchArchiveSelected": "Ctrl+Alt+G" },
              "disabledShortcuts": ["ops.exportStatsHtml"]
            }
            """);

        var service = new MachineProfileService(_workspace, _studioConfig, _uiPreferences);
        var export = service.ExportMachineProfile();

        Assert.Equal(1, export.ShortcutOverrideCount);
        Assert.Equal(1, export.ShortcutDisabledCount);
    }

    [Fact]
    public void PreviewMachineProfile_detects_shortcut_conflicts_before_import()
    {
        File.WriteAllText(_uiPreferences.PrefsPath, """
            {
              "shortcutOverrides": {
                "jobs.batchArchiveSelected": "Ctrl+Shift+G",
                "jobs.batchDeleteSelected": "Ctrl+Shift+G"
              }
            }
            """);

        var exportPath = Path.Combine(_root, "profile.json");
        var service = new MachineProfileService(_workspace, _studioConfig, _uiPreferences);
        service.ExportMachineProfile(exportPath);

        var preview = service.PreviewMachineProfile(exportPath, merge: false);

        Assert.False(preview.Valid);
        Assert.True(preview.ShortcutConflictCount > 0);
    }

    [Fact]
    public void Export_persists_operations_center_summary_in_bundle()
    {
        var service = new MachineProfileService(_workspace, _studioConfig, _uiPreferences);
        var export = service.ExportMachineProfile(operationsCenterSummary: "待发布 1 | 活动日志统计（最近 30 天）：1 条");

        Assert.Equal("待发布 1 | 活动日志统计（最近 30 天）：1 条", export.OperationsCenterSummary);
        Assert.Contains("运维快照（已写入 JSON）", export.SummaryText);

        var bundle = JsonHelper.ReadFile<PLCPak.Core.Models.MachineProfileBundle>(export.ExportPath);
        Assert.Equal(export.OperationsCenterSummary, bundle!.OperationsCenterSummary);

        var preview = service.PreviewMachineProfile(export.ExportPath);
        Assert.Equal(export.OperationsCenterSummary, preview.OperationsCenterSummary);
        Assert.Contains("导出时运维", preview.SummaryText);
    }

    [Fact]
    public void Export_reports_nightly_batch_stats_and_keep_days()
    {
        File.WriteAllText(_studioConfig.ConfigPath, """
            {
              "nightlyExportActivityLogBatchStatsAll": true,
              "activityLogKeepDays": 45
            }
            """);

        var service = new MachineProfileService(_workspace, _studioConfig, _uiPreferences);
        var export = service.ExportMachineProfile();

        Assert.True(export.NightlyExportActivityLogBatchStatsAll);
        Assert.Equal(45, export.ActivityLogKeepDays);
        Assert.Contains("夜间批量统计", export.SummaryText);
        Assert.Contains("SinceDays 45", export.SummaryText);
    }

    [Fact]
    public void PreviewMachineProfile_reports_nightly_batch_stats_from_studio_config()
    {
        File.WriteAllText(_studioConfig.ConfigPath, """
            {
              "nightlyExportActivityLogStatsBoth": true,
              "activityLogKeepDays": 30
            }
            """);

        var exportPath = Path.Combine(_root, "nightly-profile.json");
        var service = new MachineProfileService(_workspace, _studioConfig, _uiPreferences);
        service.ExportMachineProfile(exportPath);

        var preview = service.PreviewMachineProfile(exportPath);

        Assert.True(preview.Valid);
        Assert.True(preview.NightlyExportActivityLogBatchStatsAll);
        Assert.Equal(30, preview.ActivityLogKeepDays);
        Assert.Contains("夜间批量统计", preview.SummaryText);
    }

    [Fact]
    public void PreviewMachineProfile_reports_html_theme_from_bundle()
    {
        File.WriteAllText(_uiPreferences.PrefsPath, """{"htmlReportTheme":"dark"}""");

        var exportPath = Path.Combine(_root, "profile.json");
        var service = new MachineProfileService(_workspace, _studioConfig, _uiPreferences);
        service.ExportMachineProfile(exportPath);

        var preview = service.PreviewMachineProfile(exportPath);

        Assert.True(preview.Valid);
        Assert.Equal("dark", preview.HtmlReportTheme);
    }

    [Fact]
    public void ImportMachineProfile_via_JobRunner_preview_rejects_conflicting_shortcuts()
    {
        File.WriteAllText(_uiPreferences.PrefsPath, """
            {
              "shortcutOverrides": {
                "jobs.batchArchiveSelected": "Ctrl+Shift+G",
                "jobs.batchDeleteSelected": "Ctrl+Shift+G"
              }
            }
            """);

        var exportPath = Path.Combine(_root, "conflict-profile.json");
        var service = new MachineProfileService(_workspace, _studioConfig, _uiPreferences);
        service.ExportMachineProfile(exportPath);

        var import = service.ImportMachineProfile(exportPath, merge: false);

        Assert.False(import.Success);
        Assert.Contains("冲突", import.Message, StringComparison.OrdinalIgnoreCase);
    }
}