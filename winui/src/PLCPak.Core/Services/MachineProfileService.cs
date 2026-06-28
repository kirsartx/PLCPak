using System.Text.Json;
using System.Text.Json.Nodes;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class MachineProfileService
{
    private readonly WorkspaceService _workspace;
    private readonly StudioConfigService _studioConfig;
    private readonly StudioConfigExportService _studioExport;
    private readonly UiPreferencesService _uiPreferences;

    public MachineProfileService(
        WorkspaceService workspace,
        StudioConfigService studioConfig,
        UiPreferencesService? uiPreferences = null)
    {
        _workspace = workspace;
        _studioConfig = studioConfig;
        _studioExport = new StudioConfigExportService(studioConfig);
        _uiPreferences = uiPreferences ?? new UiPreferencesService(workspace);
    }

    public MachineProfileExportResult ExportMachineProfile(
        string? outputPath = null,
        string? operationsCenterSummary = null)
    {
        _workspace.EnsureLayout();
        var bundle = new MachineProfileBundle
        {
            Version = AppVersion.Current,
            ExportedAt = DateTime.Now,
            OperationsCenterSummary = string.IsNullOrWhiteSpace(operationsCenterSummary)
                ? null
                : operationsCenterSummary.Trim()
        };

        if (File.Exists(_studioConfig.ConfigPath))
            bundle.StudioConfigJson = File.ReadAllText(_studioConfig.ConfigPath).TrimStart('\uFEFF');

        var prefs = _uiPreferences.Load();
        if (File.Exists(_uiPreferences.PrefsPath))
            bundle.UiPreferencesJson = File.ReadAllText(_uiPreferences.PrefsPath).TrimStart('\uFEFF');

        var shortcutProfile = GlobalShortcutProfileService.BuildProfile(prefs);
        bundle.ShortcutProfileJson = JsonSerializer.Serialize(shortcutProfile, JsonHelper.Options);

        var studio = _studioConfig.Load();
        var nightlyBatchStatsAll = ResolveNightlyBatchStatsAllEnabled(studio);

        var fullPath = ResolveExportPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        JsonHelper.WriteFile(fullPath, bundle);

        var export = new MachineProfileExportResult
        {
            ExportPath = fullPath,
            IncludesStudioConfig = !string.IsNullOrWhiteSpace(bundle.StudioConfigJson),
            IncludesUiPreferences = !string.IsNullOrWhiteSpace(bundle.UiPreferencesJson),
            IncludesShortcutProfile = !string.IsNullOrWhiteSpace(bundle.ShortcutProfileJson),
            ShortcutOverrideCount = shortcutProfile.ShortcutOverrides.Count,
            ShortcutDisabledCount = shortcutProfile.DisabledShortcuts.Count,
            HtmlReportTheme = string.Equals(prefs.HtmlReportTheme, "dark", StringComparison.OrdinalIgnoreCase)
                ? "dark"
                : "light",
            NightlyExportActivityLogBatchStatsAll = nightlyBatchStatsAll,
            ActivityLogKeepDays = studio.ActivityLogKeepDays,
            OperationsCenterSummary = bundle.OperationsCenterSummary ?? string.Empty
        };
        export.SummaryText = BuildExportSummary(export);
        return export;
    }

    public MachineProfilePreviewResult PreviewMachineProfile(string inputPath, bool merge = true)
    {
        var fullPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullPath))
        {
            return new MachineProfilePreviewResult
            {
                Valid = false,
                Message = $"配置文件不存在: {fullPath}"
            };
        }

        var bundle = JsonHelper.ReadFile<MachineProfileBundle>(fullPath);
        if (bundle is null
            || (string.IsNullOrWhiteSpace(bundle.StudioConfigJson)
                && string.IsNullOrWhiteSpace(bundle.UiPreferencesJson)
                && string.IsNullOrWhiteSpace(bundle.ShortcutProfileJson)))
        {
            return new MachineProfilePreviewResult
            {
                Valid = false,
                Message = "机器配置 JSON 无效或为空"
            };
        }

        var preview = new MachineProfilePreviewResult
        {
            Valid = true,
            Version = bundle.Version,
            ExportedAt = bundle.ExportedAt,
            HasStudioConfig = !string.IsNullOrWhiteSpace(bundle.StudioConfigJson),
            HasUiPreferences = !string.IsNullOrWhiteSpace(bundle.UiPreferencesJson),
            HasShortcutProfile = !string.IsNullOrWhiteSpace(bundle.ShortcutProfileJson),
            OperationsCenterSummary = bundle.OperationsCenterSummary?.Trim() ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(bundle.StudioConfigJson))
        {
            var studioHints = ReadStudioNightlyHints(bundle.StudioConfigJson);
            preview.NightlyExportActivityLogBatchStatsAll = studioHints.NightlyBatchStatsAll;
            preview.ActivityLogKeepDays = studioHints.ActivityLogKeepDays;
        }

        if (!string.IsNullOrWhiteSpace(bundle.UiPreferencesJson))
        {
            var prefs = JsonHelper.Deserialize<UiPreferences>(bundle.UiPreferencesJson.TrimStart('\uFEFF'))
                ?? new UiPreferences();
            preview.HtmlReportTheme = string.Equals(prefs.HtmlReportTheme, "dark", StringComparison.OrdinalIgnoreCase)
                ? "dark"
                : "light";
        }

        if (!string.IsNullOrWhiteSpace(bundle.ShortcutProfileJson))
        {
            var shortcutPreview = PreviewShortcutProfile(bundle.ShortcutProfileJson, merge);
            if (!shortcutPreview.Valid)
            {
                preview.Valid = false;
                preview.Message = shortcutPreview.Message;
                return preview;
            }

            preview.ShortcutOverrideCount = shortcutPreview.OverrideCount;
            preview.ShortcutDisabledCount = shortcutPreview.DisabledCount;
            preview.ShortcutConflictCount = shortcutPreview.ConflictCount;
            preview.ShortcutConflictMessage = shortcutPreview.ConflictMessage;
        }

        if (preview.ShortcutConflictCount > 0)
        {
            preview.Valid = false;
            preview.Message = preview.ShortcutConflictMessage;
        }

        preview.SummaryText = BuildPreviewSummary(preview, merge);
        preview.Message = preview.Valid ? "预览通过，可安全导入" : preview.Message;
        return preview;
    }

    public MachineProfileImportResult ImportMachineProfile(string inputPath, bool merge = true)
    {
        var fullPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullPath))
        {
            return new MachineProfileImportResult
            {
                Success = false,
                Message = $"配置文件不存在: {fullPath}"
            };
        }

        var bundle = JsonHelper.ReadFile<MachineProfileBundle>(fullPath);
        if (bundle is null
            || (string.IsNullOrWhiteSpace(bundle.StudioConfigJson)
                && string.IsNullOrWhiteSpace(bundle.UiPreferencesJson)
                && string.IsNullOrWhiteSpace(bundle.ShortcutProfileJson)))
        {
            return new MachineProfileImportResult
            {
                Success = false,
                Message = "机器配置 JSON 无效或为空"
            };
        }

        var result = new MachineProfileImportResult { Merged = merge };

        if (!string.IsNullOrWhiteSpace(bundle.UiPreferencesJson))
        {
            ImportUiPreferences(bundle.UiPreferencesJson);
            result.UiPreferencesImported = true;
        }

        if (!string.IsNullOrWhiteSpace(bundle.ShortcutProfileJson))
        {
            var shortcutResult = ImportShortcutProfile(bundle.ShortcutProfileJson, merge);
            if (!shortcutResult.Success)
            {
                result.Success = false;
                result.Message = shortcutResult.Message;
                return result;
            }

            result.ShortcutProfileImported = true;
        }

        if (!string.IsNullOrWhiteSpace(bundle.StudioConfigJson))
        {
            var studioResult = merge
                ? ImportStudioConfigMerged(bundle.StudioConfigJson)
                : ImportStudioConfigReplace(bundle.StudioConfigJson);

            result.StudioConfigImported = studioResult.Success;
            if (!studioResult.Success)
            {
                result.Success = false;
                result.Message = studioResult.Message;
                return result;
            }
        }

        result.Success = true;
        result.Message = BuildImportMessage(result, merge);
        return result;
    }

    private static string BuildPreviewSummary(MachineProfilePreviewResult preview, bool merge)
    {
        var sections = new List<string>();
        if (preview.HasStudioConfig)
            sections.Add("studio-config");
        if (preview.HasUiPreferences)
            sections.Add($"UI 偏好（HTML 主题 {preview.HtmlReportTheme ?? "light"}）");
        if (preview.HasShortcutProfile)
            sections.Add($"快捷键（{preview.ShortcutOverrideCount} 映射，{preview.ShortcutDisabledCount} 禁用）");
        if (preview.NightlyExportActivityLogBatchStatsAll)
        {
            var sinceHint = preview.ActivityLogKeepDays > 0
                ? $"，夜间 -SinceDays {preview.ActivityLogKeepDays}"
                : string.Empty;
            sections.Add($"夜间批量统计 JSON+CSV{sinceHint}");
        }

        var mode = merge ? "合并导入" : "替换导入";
        var summary = sections.Count == 0
            ? $"机器配置预览：{mode}，无有效内容"
            : $"机器配置预览：{mode} — {string.Join("、", sections)}";
        if (!string.IsNullOrWhiteSpace(preview.OperationsCenterSummary))
            summary += $" | 导出时运维：{preview.OperationsCenterSummary}";
        return summary;
    }

    private (bool Valid, string Message, int OverrideCount, int DisabledCount, int ConflictCount, string ConflictMessage)
        PreviewShortcutProfile(string shortcutProfileJson, bool merge)
    {
        GlobalShortcutProfile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<GlobalShortcutProfile>(
                shortcutProfileJson.TrimStart('\uFEFF'),
                JsonHelper.Options);
        }
        catch (Exception ex)
        {
            return (false, $"快捷键配置 JSON 无效: {ex.Message}", 0, 0, 0, string.Empty);
        }

        if (profile is null)
            return (false, "快捷键配置 JSON 无效", 0, 0, 0, string.Empty);

        foreach (var (shortcutId, bindingText) in profile.ShortcutOverrides)
        {
            if (!GlobalShortcutBindingService.TryParse(bindingText, out _))
                return (false, $"快捷键映射无效：{shortcutId} -> {bindingText}", 0, 0, 0, string.Empty);
        }

        var prefs = _uiPreferences.Load();
        var target = merge ? ClonePreferences(prefs) : new UiPreferences();
        GlobalShortcutProfileService.ApplyProfileForImport(target, profile, merge);
        var conflicts = GlobalShortcutConflictService.FindConflicts(target);
        return (
            true,
            string.Empty,
            profile.ShortcutOverrides.Count,
            profile.DisabledShortcuts.Count,
            conflicts.Count,
            conflicts.Count > 0 ? GlobalShortcutConflictService.BuildConflictMessage(conflicts) : string.Empty);
    }

    private MachineProfileImportResult ImportShortcutProfile(string shortcutProfileJson, bool merge)
    {
        GlobalShortcutProfile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<GlobalShortcutProfile>(
                shortcutProfileJson.TrimStart('\uFEFF'),
                JsonHelper.Options);
        }
        catch (Exception ex)
        {
            return new MachineProfileImportResult
            {
                Success = false,
                Message = $"快捷键配置 JSON 无效: {ex.Message}"
            };
        }

        if (profile is null)
        {
            return new MachineProfileImportResult
            {
                Success = false,
                Message = "快捷键配置 JSON 无效"
            };
        }

        foreach (var (shortcutId, bindingText) in profile.ShortcutOverrides)
        {
            if (!GlobalShortcutBindingService.TryParse(bindingText, out _))
            {
                return new MachineProfileImportResult
                {
                    Success = false,
                    Message = $"快捷键映射无效：{shortcutId} -> {bindingText}"
                };
            }
        }

        var prefs = _uiPreferences.Load();
        var preview = merge ? ClonePreferences(prefs) : new UiPreferences();
        GlobalShortcutProfileService.ApplyProfileForImport(preview, profile, merge);
        var conflicts = GlobalShortcutConflictService.FindConflicts(preview);
        if (conflicts.Count > 0)
        {
            return new MachineProfileImportResult
            {
                Success = false,
                Message = GlobalShortcutConflictService.BuildConflictMessage(conflicts)
            };
        }

        GlobalShortcutProfileService.ApplyProfileForImport(prefs, profile, merge);
        _uiPreferences.Save(prefs);
        return new MachineProfileImportResult { Success = true };
    }

    private static string BuildImportMessage(MachineProfileImportResult result, bool merge)
    {
        var parts = new List<string> { merge ? "已合并导入机器配置" : "已替换导入机器配置" };
        if (result.StudioConfigImported)
            parts.Add("studio-config");
        if (result.UiPreferencesImported)
            parts.Add("UI 偏好");
        if (result.ShortcutProfileImported)
            parts.Add("快捷键");
        return string.Join("：", parts[0], string.Join("、", parts.Skip(1)));
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

    private void ImportUiPreferences(string uiPreferencesJson)
    {
        _workspace.EnsureLayout();
        Directory.CreateDirectory(_workspace.GetWorkspaceRoot());
        File.WriteAllText(_uiPreferences.PrefsPath, uiPreferencesJson.TrimStart('\uFEFF'));
    }

    private ImportResult ImportStudioConfigReplace(string studioConfigJson)
    {
        try
        {
            var node = JsonNode.Parse(studioConfigJson.TrimStart('\uFEFF'));
            if (node is not JsonObject)
            {
                return new ImportResult
                {
                    Success = false,
                    Message = "备份中的 studio-config 必须是 JSON 对象"
                };
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_studioConfig.ConfigPath)!);
            File.WriteAllText(_studioConfig.ConfigPath, node.ToJsonString(JsonHelper.Options));
            return new ImportResult
            {
                Success = true,
                Message = "已替换 studio-config"
            };
        }
        catch (Exception ex)
        {
            return new ImportResult
            {
                Success = false,
                Message = $"studio-config 导入失败: {ex.Message}"
            };
        }
    }

    private ImportResult ImportStudioConfigMerged(string studioConfigJson)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"plcpak-profile-merge-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempPath, studioConfigJson);
            return _studioExport.ImportStudioConfig(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private string ResolveExportPath(string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var backupsDir = Path.Combine(_workspace.GetWorkspaceRoot(), "backups");
        var fileName = $"plcpak-machine-profile-{DateTime.Now:yyyy-MM-dd-HHmmss}.json";
        return Path.Combine(backupsDir, fileName);
    }

    private static bool ResolveNightlyBatchStatsAllEnabled(StudioConfig studio)
        => studio.NightlyExportActivityLogBatchStatsAll
            || studio.NightlyExportActivityLogStatsBoth
            || studio.NightlyExportActivityLogStats
            || studio.NightlyExportActivityLogStatsHtml;

    private static (bool NightlyBatchStatsAll, int ActivityLogKeepDays) ReadStudioNightlyHints(string studioConfigJson)
    {
        var studio = JsonHelper.Deserialize<StudioConfig>(studioConfigJson.TrimStart('\uFEFF')) ?? new StudioConfig();
        return (ResolveNightlyBatchStatsAllEnabled(studio), studio.ActivityLogKeepDays);
    }

    private static string BuildExportSummary(MachineProfileExportResult export)
    {
        var parts = new List<string> { "机器配置已导出" };
        if (export.IncludesStudioConfig)
            parts.Add("studio-config");
        if (export.IncludesUiPreferences)
            parts.Add($"UI 偏好（HTML {export.HtmlReportTheme}）");
        if (export.IncludesShortcutProfile)
            parts.Add($"快捷键（{export.ShortcutOverrideCount} 映射，{export.ShortcutDisabledCount} 禁用）");
        if (export.NightlyExportActivityLogBatchStatsAll)
        {
            var sinceHint = export.ActivityLogKeepDays > 0
                ? $" -SinceDays {export.ActivityLogKeepDays}"
                : string.Empty;
            parts.Add($"夜间批量统计{sinceHint}");
        }
        if (!string.IsNullOrWhiteSpace(export.OperationsCenterSummary))
            parts.Add($"运维快照（已写入 JSON）");

        return string.Join("：", parts[0], string.Join("、", parts.Skip(1)));
    }
}