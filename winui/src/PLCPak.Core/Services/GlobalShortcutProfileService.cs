using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class GlobalShortcutProfile
{
    public string Version { get; set; } = AppVersion.Current;
    public DateTime ExportedAt { get; set; } = DateTime.Now;
    public Dictionary<string, string> ShortcutOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> DisabledShortcuts { get; set; } = [];
}

public sealed class GlobalShortcutProfileExportResult
{
    public string ExportPath { get; set; } = string.Empty;
    public int OverrideCount { get; set; }
    public int DisabledCount { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class GlobalShortcutProfileImportResult
{
    public bool Success { get; set; }
    public bool Merged { get; set; }
    public int OverrideCount { get; set; }
    public int DisabledCount { get; set; }
    public List<GlobalShortcutConflict> Conflicts { get; set; } = [];
    public string SummaryText { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public static class GlobalShortcutProfileService
{
    public static GlobalShortcutProfileExportResult Export(UiPreferences prefs, string? outputPath, string workspaceRoot)
    {
        var profile = BuildProfile(prefs);
        var fullPath = ResolveExportPath(workspaceRoot, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        JsonHelper.WriteFile(fullPath, profile);

        return new GlobalShortcutProfileExportResult
        {
            ExportPath = fullPath,
            OverrideCount = profile.ShortcutOverrides.Count,
            DisabledCount = profile.DisabledShortcuts.Count,
            SummaryText = $"已导出快捷键配置：{profile.ShortcutOverrides.Count} 项映射，{profile.DisabledShortcuts.Count} 项禁用"
        };
    }

    public static GlobalShortcutProfileImportResult Import(
        string inputPath,
        UiPreferences prefs,
        bool merge = true)
    {
        var fullPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullPath))
        {
            return new GlobalShortcutProfileImportResult
            {
                Success = false,
                Message = $"快捷键配置不存在: {fullPath}"
            };
        }

        var profile = JsonHelper.ReadFile<GlobalShortcutProfile>(fullPath);
        if (profile is null)
        {
            return new GlobalShortcutProfileImportResult
            {
                Success = false,
                Message = "快捷键配置 JSON 无效"
            };
        }

        foreach (var (shortcutId, bindingText) in profile.ShortcutOverrides)
        {
            if (!GlobalShortcutBindingService.TryParse(bindingText, out _))
            {
                return new GlobalShortcutProfileImportResult
                {
                    Success = false,
                    Message = $"快捷键映射无效：{shortcutId} -> {bindingText}"
                };
            }
        }

        var preview = merge ? ClonePreferences(prefs) : new UiPreferences();
        ApplyProfile(preview, profile, merge);
        var conflicts = GlobalShortcutConflictService.FindConflicts(preview);
        if (conflicts.Count > 0)
        {
            return new GlobalShortcutProfileImportResult
            {
                Success = false,
                Conflicts = conflicts.ToList(),
                Message = GlobalShortcutConflictService.BuildConflictMessage(conflicts)
            };
        }

        ApplyProfile(prefs, profile, merge);
        return new GlobalShortcutProfileImportResult
        {
            Success = true,
            Merged = merge,
            OverrideCount = prefs.ShortcutOverrides.Count,
            DisabledCount = prefs.DisabledShortcuts.Count,
            SummaryText = merge
                ? $"已合并导入快捷键配置：{prefs.ShortcutOverrides.Count} 项映射，{prefs.DisabledShortcuts.Count} 项禁用"
                : $"已替换导入快捷键配置：{prefs.ShortcutOverrides.Count} 项映射，{prefs.DisabledShortcuts.Count} 项禁用",
            Message = "导入成功"
        };
    }

    public static GlobalShortcutProfile BuildProfile(UiPreferences prefs)
        => new()
        {
            Version = AppVersion.Current,
            ExportedAt = DateTime.Now,
            ShortcutOverrides = new Dictionary<string, string>(prefs.ShortcutOverrides, StringComparer.OrdinalIgnoreCase),
            DisabledShortcuts = prefs.DisabledShortcuts
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

    public static void ApplyProfileForImport(UiPreferences prefs, GlobalShortcutProfile profile, bool merge)
        => ApplyProfile(prefs, profile, merge);

    private static void ApplyProfile(UiPreferences prefs, GlobalShortcutProfile profile, bool merge)
    {
        if (!merge)
        {
            prefs.ShortcutOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            prefs.DisabledShortcuts = [];
        }

        foreach (var (shortcutId, bindingText) in profile.ShortcutOverrides)
            prefs.ShortcutOverrides[shortcutId] = bindingText.Trim();

        foreach (var shortcutId in profile.DisabledShortcuts.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (!prefs.DisabledShortcuts.Contains(shortcutId, StringComparer.OrdinalIgnoreCase))
                prefs.DisabledShortcuts.Add(shortcutId.Trim());
        }
    }

    private static UiPreferences ClonePreferences(UiPreferences prefs)
        => new()
        {
            LastNavMode = prefs.LastNavMode,
            LastSelectedJobId = prefs.LastSelectedJobId,
            LastJobFilter = prefs.LastJobFilter,
            LastCommandPaletteFilter = prefs.LastCommandPaletteFilter,
            RecentCommandPaletteIds = prefs.RecentCommandPaletteIds.ToList(),
            HasSeenWelcome = prefs.HasSeenWelcome,
            DisableGlobalShortcuts = prefs.DisableGlobalShortcuts,
            DisabledShortcuts = prefs.DisabledShortcuts.ToList(),
            ShortcutOverrides = new Dictionary<string, string>(prefs.ShortcutOverrides, StringComparer.OrdinalIgnoreCase),
            HtmlReportTheme = prefs.HtmlReportTheme,
            ProfessionalMode = prefs.ProfessionalMode,
            AppTheme = prefs.AppTheme,
            UiLanguage = prefs.UiLanguage,
            HasSeenOnboarding = prefs.HasSeenOnboarding
        };

    private static string ResolveExportPath(string workspaceRoot, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var reportsDir = Path.Combine(workspaceRoot, "reports");
        return Path.Combine(reportsDir, $"shortcut-profile-{DateTime.Now:yyyy-MM-dd-HHmmss}.json");
    }
}