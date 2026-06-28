using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class GlobalShortcutProfileServiceTests : IDisposable
{
    private readonly string _root;

    public GlobalShortcutProfileServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-shortcut-profile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Export_and_import_roundtrip_shortcut_profile()
    {
        var prefs = new UiPreferences
        {
            ShortcutOverrides =
            {
                [GlobalShortcutRegistry.JobsBatchDeleteSelected] = "Ctrl+Shift+X"
            },
            DisabledShortcuts = [GlobalShortcutRegistry.OpsExportStatsHtml]
        };

        var exportPath = Path.Combine(_root, "shortcut-profile.json");
        var export = GlobalShortcutProfileService.Export(prefs, exportPath, _root);
        Assert.True(File.Exists(export.ExportPath));

        var target = new UiPreferences();
        var import = GlobalShortcutProfileService.Import(exportPath, target, merge: false);

        Assert.True(import.Success);
        Assert.Equal("Ctrl+Shift+X", target.ShortcutOverrides[GlobalShortcutRegistry.JobsBatchDeleteSelected]);
        Assert.Contains(GlobalShortcutRegistry.OpsExportStatsHtml, target.DisabledShortcuts);
    }

    [Fact]
    public void Import_rejects_conflicting_profile()
    {
        var profilePath = Path.Combine(_root, "conflict.json");
        JsonHelper.WriteFile(profilePath, new GlobalShortcutProfile
        {
            ShortcutOverrides =
            {
                [GlobalShortcutRegistry.JobsBatchArchiveSelected] = "Ctrl+Shift+Y",
                [GlobalShortcutRegistry.JobsBatchDeleteSelected] = "Ctrl+Shift+Y"
            }
        });

        var import = GlobalShortcutProfileService.Import(profilePath, new UiPreferences(), merge: false);

        Assert.False(import.Success);
        Assert.NotEmpty(import.Conflicts);
    }
}