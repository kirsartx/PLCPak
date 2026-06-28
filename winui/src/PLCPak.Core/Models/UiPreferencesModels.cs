namespace PLCPak.Core.Models;

public sealed class UiPreferences
{
    public string LastNavMode { get; set; } = string.Empty;
    public string? LastSelectedJobId { get; set; }
    public string? LastJobFilter { get; set; }
    public string? LastCommandPaletteFilter { get; set; }
    public List<string> RecentCommandPaletteIds { get; set; } = [];
    public bool HasSeenWelcome { get; set; }
    public bool DisableGlobalShortcuts { get; set; }
    public List<string> DisabledShortcuts { get; set; } = [];
    public Dictionary<string, string> ShortcutOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string HtmlReportTheme { get; set; } = "light";
    /// <summary>专业模式：显示完整功能；默认 false 为简易傻瓜式流程。</summary>
    public bool ProfessionalMode { get; set; }
    /// <summary>界面主题：light / dark</summary>
    public string AppTheme { get; set; } = "light";
    /// <summary>界面语言：zh / en</summary>
    public string UiLanguage { get; set; } = "zh";
    /// <summary>是否已看过动画引导（与 HasSeenWelcome 独立）。</summary>
    public bool HasSeenOnboarding { get; set; }
    /// <summary>向导步骤完成时播放声音/触觉反馈。</summary>
    public bool EnableWizardCompletionFeedback { get; set; } = true;
    /// <summary>简易模式顶栏提示是否已被用户关闭（切换模式后重置）。</summary>
    public bool HasDismissedSimpleModeHint { get; set; }
    /// <summary>减少动画：跳过页面切换淡入淡出与向导步骤动效。</summary>
    public bool ReduceMotion { get; set; }
}