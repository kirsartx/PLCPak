using System.Text.Json.Nodes;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class AllBackupService
{
    private readonly JobStore _store;
    private readonly WorkspaceService _workspace;
    private readonly StudioConfigService _studioConfig;
    private readonly StudioConfigExportService _studioExport;
    private readonly UiPreferencesService _uiPreferences;

    public AllBackupService(
        JobStore store,
        WorkspaceService workspace,
        StudioConfigService studioConfig,
        UiPreferencesService? uiPreferences = null)
    {
        _store = store;
        _workspace = workspace;
        _studioConfig = studioConfig;
        _studioExport = new StudioConfigExportService(studioConfig);
        _uiPreferences = uiPreferences ?? new UiPreferencesService(workspace);
    }

    public AllBackupExportResult ExportAllBackup(string? outputPath = null)
    {
        _workspace.EnsureLayout();
        var jobs = _store.List();
        var bundle = new AllBackupBundle
        {
            Version = AppVersion.Current,
            ExportedAt = DateTime.Now,
            Jobs = jobs.ToList()
        };

        if (File.Exists(_studioConfig.ConfigPath))
            bundle.StudioConfigJson = File.ReadAllText(_studioConfig.ConfigPath).TrimStart('\uFEFF');

        if (File.Exists(_uiPreferences.PrefsPath))
            bundle.UiPreferencesJson = File.ReadAllText(_uiPreferences.PrefsPath).TrimStart('\uFEFF');

        var fullPath = ResolveExportPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        JsonHelper.WriteFile(fullPath, bundle);

        return new AllBackupExportResult
        {
            ExportPath = fullPath,
            JobCount = jobs.Count,
            IncludesStudioConfig = !string.IsNullOrWhiteSpace(bundle.StudioConfigJson)
        };
    }

    public AllBackupImportResult ImportAllBackup(string inputPath, bool merge = false)
    {
        var fullPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullPath))
        {
            return new AllBackupImportResult
            {
                Success = false,
                Message = $"备份文件不存在: {fullPath}"
            };
        }

        var bundle = JsonHelper.ReadFile<AllBackupBundle>(fullPath);
        if (bundle is null || bundle.Jobs.Count == 0
            && string.IsNullOrWhiteSpace(bundle.StudioConfigJson)
            && string.IsNullOrWhiteSpace(bundle.UiPreferencesJson))
        {
            return new AllBackupImportResult
            {
                Success = false,
                Message = "备份 JSON 无效或为空"
            };
        }

        var result = new AllBackupImportResult { Merged = merge };

        if (!string.IsNullOrWhiteSpace(bundle.UiPreferencesJson))
            ImportUiPreferences(bundle.UiPreferencesJson);

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

        if (merge)
            ImportJobsMerged(bundle.Jobs, result);
        else
            ImportJobsReplace(bundle.Jobs, result);

        result.Success = true;
        result.Message = merge
            ? $"已合并导入 {result.ImportedJobs} 个任务（跳过 {result.SkippedJobs}）"
            : $"已替换导入 {result.ReplacedJobs} 个任务";
        return result;
    }

    private void ImportUiPreferences(string uiPreferencesJson)
    {
        _workspace.EnsureLayout();
        Directory.CreateDirectory(_workspace.GetWorkspaceRoot());
        File.WriteAllText(_uiPreferences.PrefsPath, uiPreferencesJson.TrimStart('\uFEFF'));
    }

    private string ResolveExportPath(string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return Path.GetFullPath(outputPath);

        var backupsDir = Path.Combine(_workspace.GetWorkspaceRoot(), "backups");
        var fileName = $"plcpak-backup-{DateTime.Now:yyyy-MM-dd-HHmmss}.json";
        return Path.Combine(backupsDir, fileName);
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
        var tempPath = Path.Combine(Path.GetTempPath(), $"plcpak-studio-merge-{Guid.NewGuid():N}.json");
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

    private void ImportJobsReplace(IReadOnlyList<PublishJob> jobs, AllBackupImportResult result)
    {
        _workspace.EnsureLayout();
        foreach (var existing in _store.List())
            _store.Delete(existing.Id);

        foreach (var job in jobs)
        {
            var imported = ImportJobPreservingId(job);
            result.ReplacedJobs++;
            result.ImportedJobs++;
            _ = imported;
        }
    }

    private void ImportJobsMerged(IReadOnlyList<PublishJob> jobs, AllBackupImportResult result)
    {
        foreach (var job in jobs)
        {
            var existing = _store.Get(job.Id);
            if (existing is not null
                && existing.Title.Equals(job.Title, StringComparison.OrdinalIgnoreCase)
                && existing.Paths.Slug.Equals(job.Paths.Slug, StringComparison.OrdinalIgnoreCase))
            {
                result.SkippedJobs++;
                continue;
            }

            _store.Import(job);
            result.ImportedJobs++;
        }
    }

    private PublishJob ImportJobPreservingId(PublishJob job)
    {
        _workspace.EnsureLayout();
        if (string.IsNullOrWhiteSpace(job.Id))
            job.Id = Guid.NewGuid().ToString("N");

        Directory.CreateDirectory(job.Paths.Inbox);
        Directory.CreateDirectory(job.Paths.Extract);
        Directory.CreateDirectory(job.Paths.Output);

        job.UpdatedAt = DateTime.Now;
        if (job.CreatedAt == default)
            job.CreatedAt = job.UpdatedAt;

        job.AppendLog("任务已从全量备份导入");
        _store.Save(job);
        return job;
    }
}