using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class GlobalShortcutRegistryTests
{
    [Fact]
    public void IsEnabled_returns_false_when_global_disabled()
    {
        var prefs = new UiPreferences { DisableGlobalShortcuts = true };
        Assert.False(GlobalShortcutRegistry.IsEnabled(prefs, GlobalShortcutRegistry.JobsBatchArchiveSelected));
    }

    [Fact]
    public void IsEnabled_returns_false_when_shortcut_in_disabled_list()
    {
        var prefs = new UiPreferences
        {
            DisabledShortcuts = [GlobalShortcutRegistry.OpsExportStatsHtml]
        };
        Assert.False(GlobalShortcutRegistry.IsEnabled(prefs, GlobalShortcutRegistry.OpsExportStatsHtml));
        Assert.True(GlobalShortcutRegistry.IsEnabled(prefs, GlobalShortcutRegistry.JobsBatchAppendTags));
    }

    [Fact]
    public void Definitions_include_batch_activity_shortcuts()
    {
        Assert.NotNull(GlobalShortcutRegistry.FindDefinition(GlobalShortcutRegistry.OpsFilterBatchActivity));
        Assert.NotNull(GlobalShortcutRegistry.FindDefinition(GlobalShortcutRegistry.OpsExportBatchStatsJson));
        Assert.NotNull(GlobalShortcutRegistry.FindDefinition(GlobalShortcutRegistry.OpsExportBatchStatsCsv));
        Assert.Equal("Ctrl+Shift+B", GlobalShortcutRegistry.FindDefinition(GlobalShortcutRegistry.OpsFilterBatchActivity)!.Keys);
        Assert.Equal("Ctrl+Shift+?", GlobalShortcutRegistry.FindDefinition(GlobalShortcutRegistry.OpsExportBatchStatsCsv)!.Keys);
        Assert.NotNull(GlobalShortcutRegistry.FindDefinition(GlobalShortcutRegistry.OpsExportBatchStatsAll));
        Assert.Equal("Ctrl+Shift+O", GlobalShortcutRegistry.FindDefinition(GlobalShortcutRegistry.OpsExportBatchStatsAll)!.Keys);
    }
}