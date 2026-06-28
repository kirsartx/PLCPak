using Microsoft.UI.Xaml;
using PLCPak.Core;
using PLCPak.Core.Services;

namespace PLCPak.WinUI.Infrastructure;

public static class ThemeService
{
    public static event Action? ThemeChanged;

    public static string CurrentTheme { get; private set; } = "light";

    private static WeakReference<FrameworkElement>? _themeRoot;

    public static void RegisterThemeRoot(FrameworkElement root)
        => _themeRoot = new WeakReference<FrameworkElement>(root);

    public static string ReadThemeFromDisk(string appRoot)
    {
        try
        {
            var paths = new AppPaths(appRoot);
            var studioConfig = new StudioConfigService(paths);
            var workspace = new WorkspaceService(paths, studioConfig);
            return UiStringTable.NormalizeTheme(new UiPreferencesService(workspace).Load().AppTheme);
        }
        catch
        {
            return "light";
        }
    }

    /// <summary>Must run in App ctor before InitializeComponent.</summary>
    public static void ApplyAtStartup(Application app, string theme)
    {
        CurrentTheme = UiStringTable.NormalizeTheme(theme);
        app.RequestedTheme = ToApplicationTheme(CurrentTheme);
    }

    public static void LoadFromPreferences(UiPreferencesService prefs)
    {
        var theme = UiStringTable.NormalizeTheme(prefs.Load().AppTheme);
        if (theme == CurrentTheme)
            return;

        Apply(theme, applyApplicationTheme: false);
    }

    public static void Apply(string theme, bool applyApplicationTheme = true)
    {
        CurrentTheme = UiStringTable.NormalizeTheme(theme);
        if (applyApplicationTheme)
            TrySetApplicationTheme();

        ApplyToThemeRoot();
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

    private static void TrySetApplicationTheme()
    {
        if (Application.Current is null)
            return;

        try
        {
            Application.Current.RequestedTheme = ToApplicationTheme(CurrentTheme);
        }
        catch
        {
            ApplyToThemeRoot();
        }
    }

    private static void ApplyToThemeRoot()
    {
        if (_themeRoot?.TryGetTarget(out var root) != true)
            return;

        root.RequestedTheme = CurrentTheme == "dark" ? ElementTheme.Dark : ElementTheme.Light;
    }

    private static ApplicationTheme ToApplicationTheme(string theme)
        => theme == "dark" ? ApplicationTheme.Dark : ApplicationTheme.Light;
}