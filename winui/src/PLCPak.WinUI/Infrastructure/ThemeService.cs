using Microsoft.UI.Xaml;
using PLCPak.Core.Services;

namespace PLCPak.WinUI.Infrastructure;

public static class ThemeService
{
    public static event Action? ThemeChanged;

    public static string CurrentTheme { get; private set; } = "light";

    public static void LoadFromPreferences(UiPreferencesService prefs)
        => Apply(UiStringTable.NormalizeTheme(prefs.Load().AppTheme));

    public static void Apply(string theme)
    {
        CurrentTheme = UiStringTable.NormalizeTheme(theme);
        if (Application.Current is not null)
            Application.Current.RequestedTheme = CurrentTheme == "dark"
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;

        ThemeChanged?.Invoke();
    }

    public static void SaveTheme(UiPreferencesService prefs, string theme)
    {
        var normalized = UiStringTable.NormalizeTheme(theme);
        var uiPrefs = prefs.Load();
        uiPrefs.AppTheme = normalized;
        prefs.Save(uiPrefs);
        Apply(normalized);
    }
}