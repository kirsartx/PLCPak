using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class GlobalShortcutBindingServiceTests
{
    [Fact]
    public void TryParse_parses_ctrl_shift_key()
    {
        Assert.True(GlobalShortcutBindingService.TryParse("Ctrl+Shift+G", out var binding));
        Assert.True(binding.Ctrl);
        Assert.True(binding.Shift);
        Assert.Equal("G", binding.Key);
    }

    [Fact]
    public void GetEffectiveBinding_uses_override_when_present()
    {
        var prefs = new UiPreferences
        {
            ShortcutOverrides =
            {
                [GlobalShortcutRegistry.JobsBatchArchiveSelected] = "Ctrl+Shift+X"
            }
        };

        var binding = GlobalShortcutBindingService.GetEffectiveBinding(
            prefs,
            GlobalShortcutRegistry.JobsBatchArchiveSelected);

        Assert.Equal("X", binding.Key);
        Assert.True(binding.Ctrl);
        Assert.True(binding.Shift);
    }

    [Fact]
    public void Matches_compares_binding_with_key_state()
    {
        var binding = new GlobalShortcutBinding { Ctrl = true, Shift = true, Key = "H" };
        Assert.True(GlobalShortcutBindingService.Matches(binding, ctrl: true, shift: true, alt: false, "H"));
        Assert.False(GlobalShortcutBindingService.Matches(binding, ctrl: true, shift: false, alt: false, "H"));
    }
}