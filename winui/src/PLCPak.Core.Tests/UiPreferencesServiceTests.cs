using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class UiPreferencesServiceTests : IDisposable
{
    private readonly string _root;
    private readonly UiPreferencesService _service;

    public UiPreferencesServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-uiprefs-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        Directory.CreateDirectory(data);

        var workspaceRoot = Path.Combine(_root, "workspace");
        File.WriteAllText(Path.Combine(data, "studio-config.json"),
            $"{{\"workspaceRoot\":\"{workspaceRoot.Replace("\\", "\\\\")}\"}}");

        var paths = new AppPaths(Path.Combine(_root, "app"));
        var workspace = new WorkspaceService(paths, new StudioConfigService(paths));
        _service = new UiPreferencesService(workspace);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Load_returns_defaults_when_file_missing()
    {
        var prefs = _service.Load();

        Assert.Equal(string.Empty, prefs.LastNavMode);
        Assert.Null(prefs.LastSelectedJobId);
        Assert.Null(prefs.LastJobFilter);
        Assert.True(prefs.EnableWizardCompletionFeedback);
        Assert.False(prefs.HasDismissedSimpleModeHint);
    }

    [Fact]
    public void Save_and_load_roundtrip_persists_preferences()
    {
        var prefs = new UiPreferences
        {
            LastNavMode = "jobs",
            LastSelectedJobId = "abc123",
            LastJobFilter = "PendingPublish"
        };

        _service.Save(prefs);
        var loaded = _service.Load();

        Assert.Equal("jobs", loaded.LastNavMode);
        Assert.Equal("abc123", loaded.LastSelectedJobId);
        Assert.Equal("PendingPublish", loaded.LastJobFilter);
        Assert.True(File.Exists(_service.PrefsPath));
        Assert.Equal(Path.Combine(_root, "workspace", "ui-prefs.json"), _service.PrefsPath);
    }

    [Fact]
    public void Save_and_load_roundtrip_persists_disable_global_shortcuts()
    {
        var prefs = new UiPreferences { DisableGlobalShortcuts = true };

        _service.Save(prefs);
        var loaded = _service.Load();

        Assert.True(loaded.DisableGlobalShortcuts);
    }

    [Fact]
    public void Save_and_load_roundtrip_persists_professional_mode()
    {
        var prefs = new UiPreferences { ProfessionalMode = true };

        _service.Save(prefs);
        var loaded = _service.Load();

        Assert.True(loaded.ProfessionalMode);
    }

    [Fact]
    public void Save_and_load_roundtrip_persists_wizard_completion_feedback()
    {
        var prefs = new UiPreferences { EnableWizardCompletionFeedback = false };

        _service.Save(prefs);
        var loaded = _service.Load();

        Assert.False(loaded.EnableWizardCompletionFeedback);
    }

    [Fact]
    public void Save_and_load_roundtrip_persists_simple_mode_hint_dismiss()
    {
        var prefs = new UiPreferences { HasDismissedSimpleModeHint = true };

        _service.Save(prefs);
        var loaded = _service.Load();

        Assert.True(loaded.HasDismissedSimpleModeHint);
    }

    [Fact]
    public void Save_and_load_roundtrip_persists_recent_command_palette_ids()
    {
        var prefs = new UiPreferences
        {
            RecentCommandPaletteIds = ["shell.palette.refreshJobs.title", "shell.palette.navOps.title"]
        };

        _service.Save(prefs);
        var loaded = _service.Load();

        Assert.Equal(2, loaded.RecentCommandPaletteIds.Count);
        Assert.Equal("shell.palette.refreshJobs.title", loaded.RecentCommandPaletteIds[0]);
        Assert.Equal("shell.palette.navOps.title", loaded.RecentCommandPaletteIds[1]);
    }

    [Fact]
    public void Save_and_load_roundtrip_persists_theme_language_and_onboarding()
    {
        var prefs = new UiPreferences
        {
            AppTheme = "dark",
            UiLanguage = "en",
            HasSeenOnboarding = true
        };

        _service.Save(prefs);
        var loaded = _service.Load();

        Assert.Equal("dark", loaded.AppTheme);
        Assert.Equal("en", loaded.UiLanguage);
        Assert.True(loaded.HasSeenOnboarding);
    }
}