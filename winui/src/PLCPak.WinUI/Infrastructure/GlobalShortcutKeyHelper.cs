using PLCPak.Core.Models;
using PLCPak.Core.Services;
using Windows.System;

namespace PLCPak.WinUI.Infrastructure;

public static class GlobalShortcutKeyHelper
{
    // US keyboard '/' and '?' share VK_OEM_2 (191); VirtualKey has no named Slash member.
    private const VirtualKey Oem2 = (VirtualKey)191;

    public static bool IsShortcutPressed(
        VirtualKey key,
        bool ctrl,
        bool shift,
        UiPreferences prefs,
        string shortcutId)
    {
        if (!GlobalShortcutRegistry.IsEnabled(prefs, shortcutId))
            return false;

        var binding = GlobalShortcutBindingService.GetEffectiveBinding(prefs, shortcutId);
        return GlobalShortcutBindingService.Matches(binding, ctrl, shift, alt: false, ToKeyToken(key, shift));
    }

    public static string ToKeyToken(VirtualKey key, bool shift = false)
        => key switch
        {
            Oem2 when shift => "?",
            Oem2 => "/",
            VirtualKey.Enter => "Enter",
            VirtualKey.Escape => "Escape",
            VirtualKey.Space => "Space",
            >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((int)key - (int)VirtualKey.Number0).ToString(),
            >= VirtualKey.A and <= VirtualKey.Z => ((char)('A' + ((int)key - (int)VirtualKey.A))).ToString(),
            >= VirtualKey.F1 and <= VirtualKey.F24 => $"F{(int)key - (int)VirtualKey.F1 + 1}",
            _ => key.ToString()
        };
}