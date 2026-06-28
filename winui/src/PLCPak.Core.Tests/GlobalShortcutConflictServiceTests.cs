using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class GlobalShortcutConflictServiceTests
{
    [Fact]
    public void FindConflicts_detects_duplicate_bindings()
    {
        var prefs = new UiPreferences
        {
            ShortcutOverrides =
            {
                [GlobalShortcutRegistry.JobsBatchArchiveSelected] = "Ctrl+Shift+X",
                [GlobalShortcutRegistry.JobsBatchDeleteSelected] = "Ctrl+Shift+X"
            }
        };

        var conflicts = GlobalShortcutConflictService.FindConflicts(prefs);

        Assert.Single(conflicts);
        Assert.Equal("Ctrl+Shift+X", conflicts[0].BindingText);
        Assert.Equal(2, conflicts[0].ShortcutIds.Count);
    }

    [Fact]
    public void FindConflicts_ignores_disabled_shortcuts()
    {
        var prefs = new UiPreferences
        {
            DisabledShortcuts = [GlobalShortcutRegistry.JobsBatchDeleteSelected],
            ShortcutOverrides =
            {
                [GlobalShortcutRegistry.JobsBatchArchiveSelected] = "Ctrl+Shift+X",
                [GlobalShortcutRegistry.JobsBatchDeleteSelected] = "Ctrl+Shift+X"
            }
        };

        var conflicts = GlobalShortcutConflictService.FindConflicts(prefs);

        Assert.Empty(conflicts);
    }
}