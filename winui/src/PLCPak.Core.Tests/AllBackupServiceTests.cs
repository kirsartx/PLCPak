using PLCPak.Core;
using PLCPak.Core.Models;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class AllBackupServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _appRoot;
    private readonly JobStore _store;
    private readonly WorkspaceService _workspace;
    private readonly StudioConfigService _studio;
    private readonly UiPreferencesService _uiPreferences;
    private readonly AllBackupService _service;

    public AllBackupServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-all-backup-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        _appRoot = Path.Combine(_root, "app");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(_appRoot);
        File.WriteAllText(Path.Combine(data, "compress-config.json"), "{}");
        var ws = Path.Combine(_root, "workspace").Replace("\\", "\\\\");
        File.WriteAllText(Path.Combine(data, "studio-config.json"), $$"""
            {
              "workspaceRoot": "{{ws}}",
              "telegramBotToken": "token-a"
            }
            """);

        var appContext = PlcPakAppContext.Create(_appRoot);
        _workspace = appContext.Workspace;
        _store = appContext.Jobs;
        _studio = appContext.StudioConfig;
        _uiPreferences = appContext.UiPreferences;
        _service = new AllBackupService(_store, _workspace, _studio, _uiPreferences);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void ExportAllBackup_writes_jobs_and_studio_config()
    {
        var job = _store.Create("备份任务");
        job.Notes = "note";
        _store.Save(job);

        var exportPath = Path.Combine(_root, "backup.json");
        var export = _service.ExportAllBackup(exportPath);

        Assert.Equal(exportPath, export.ExportPath);
        Assert.True(File.Exists(exportPath));
        Assert.Equal(1, export.JobCount);
        Assert.True(export.IncludesStudioConfig);

        var bundle = JsonHelper.ReadFile<AllBackupBundle>(exportPath);
        Assert.NotNull(bundle);
        Assert.Equal(AppVersion.Current, bundle!.Version);
        Assert.Single(bundle.Jobs);
        Assert.Equal("备份任务", bundle.Jobs[0].Title);
        Assert.Contains("token-a", bundle.StudioConfigJson);
    }

    [Fact]
    public void ExportAllBackup_includes_ui_preferences()
    {
        _uiPreferences.Save(new UiPreferences
        {
            LastNavMode = "jobs",
            LastSelectedJobId = "job-42",
            LastJobFilter = "PendingPublish"
        });

        var exportPath = Path.Combine(_root, "backup-with-prefs.json");
        _service.ExportAllBackup(exportPath);

        var bundle = JsonHelper.ReadFile<AllBackupBundle>(exportPath);
        Assert.NotNull(bundle);
        Assert.Contains("jobs", bundle!.UiPreferencesJson);
        Assert.Contains("job-42", bundle.UiPreferencesJson);
    }

    [Fact]
    public void ImportAllBackup_restores_ui_preferences()
    {
        _uiPreferences.Save(new UiPreferences { LastNavMode = "dashboard" });
        var exportPath = Path.Combine(_root, "prefs-restore.json");
        _service.ExportAllBackup(exportPath);

        if (File.Exists(_uiPreferences.PrefsPath))
            File.Delete(_uiPreferences.PrefsPath);

        var import = _service.ImportAllBackup(exportPath, merge: false);

        Assert.True(import.Success);
        var loaded = _uiPreferences.Load();
        Assert.Equal("dashboard", loaded.LastNavMode);
        Assert.True(File.Exists(_uiPreferences.PrefsPath));
    }

    [Fact]
    public void ImportAllBackup_replace_mode_replaces_jobs_and_studio_config()
    {
        var existing = _store.Create("旧任务");
        _store.Save(existing);

        var backupPath = Path.Combine(_root, "replace.json");
        _service.ExportAllBackup(backupPath);

        _store.Delete(existing.Id);
        _store.Create("干扰任务");

        File.WriteAllText(_studio.ConfigPath, """{"telegramBotToken":"old"}""");

        var import = _service.ImportAllBackup(backupPath, merge: false);

        Assert.True(import.Success);
        Assert.True(import.StudioConfigImported);
        Assert.Equal(1, import.ReplacedJobs);
        Assert.Contains("token-a", File.ReadAllText(_studio.ConfigPath));
        Assert.DoesNotContain(_store.List(), j => j.Title == "干扰任务");
        Assert.Contains(_store.List(), j => j.Title == "旧任务");
    }

    [Fact]
    public void ImportAllBackup_merge_mode_keeps_existing_jobs_and_merges_config()
    {
        var local = _store.Create("本地任务");
        _store.Save(local);

        var backupPath = Path.Combine(_root, "merge.json");
        var export = _service.ExportAllBackup(backupPath);
        Assert.Equal(1, export.JobCount);

        File.WriteAllText(backupPath, File.ReadAllText(backupPath).Replace(
            "token-a",
            "token-b",
            StringComparison.Ordinal));

        var import = _service.ImportAllBackup(backupPath, merge: true);

        Assert.True(import.Success);
        Assert.True(import.Merged);
        Assert.Equal(0, import.ImportedJobs);
        Assert.Equal(1, import.SkippedJobs);
        Assert.Contains(_store.List(), j => j.Title == "本地任务");
        Assert.Contains("token-b", File.ReadAllText(_studio.ConfigPath));
    }
}