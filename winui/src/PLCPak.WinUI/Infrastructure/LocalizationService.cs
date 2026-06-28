using PLCPak.Core.Services;

namespace PLCPak.WinUI.Infrastructure;

public static class LocalizationService
{
    public static event Action? LanguageChanged;

    public static string CurrentLanguage { get; private set; } = "zh";

    public static void LoadFromPreferences(UiPreferencesService prefs)
        => SetLanguage(prefs.Load().UiLanguage, persist: false);

    public static string T(string key) => UiStringTable.Get(key, CurrentLanguage);

    public static string TNavPage(string page) => UiStringTable.GetNavLabel(page, CurrentLanguage);

    public static void SetLanguage(string? language, bool persist = true, UiPreferencesService? prefs = null)
    {
        var normalized = UiStringTable.NormalizeLanguage(language);
        if (CurrentLanguage == normalized && persist)
            return;

        CurrentLanguage = normalized;
        UiDisplayContext.SetLanguage(normalized);
        if (persist && prefs is not null)
        {
            var uiPrefs = prefs.Load();
            uiPrefs.UiLanguage = normalized;
            prefs.Save(uiPrefs);
        }

        LanguageChanged?.Invoke();
    }

    public static void SaveLanguage(UiPreferencesService prefs, string language)
        => SetLanguage(language, persist: true, prefs: prefs);
}