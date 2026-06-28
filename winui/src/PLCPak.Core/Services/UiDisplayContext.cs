namespace PLCPak.Core.Services;

/// <summary>Core 层 UI 显示上下文（语言等），由 WinUI LocalizationService 同步。</summary>
public static class UiDisplayContext
{
    public static string CurrentLanguage { get; private set; } = "zh";

    public static void SetLanguage(string? language)
        => CurrentLanguage = UiStringTable.NormalizeLanguage(language);
}