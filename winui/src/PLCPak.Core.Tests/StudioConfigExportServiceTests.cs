using PLCPak.Core;
using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class StudioConfigExportServiceTests : IDisposable
{
    private readonly string _root;
    private readonly StudioConfigService _studio;
    private readonly StudioConfigExportService _service;

    public StudioConfigExportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-studio-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        var paths = new AppPaths(Path.Combine(_root, "app"));
        Directory.CreateDirectory(paths.DataRoot);
        _studio = new StudioConfigService(paths);
        _service = new StudioConfigExportService(_studio);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Export_and_import_roundtrip_merges_without_wiping_unknown_fields()
    {
        File.WriteAllText(_studio.ConfigPath, """
            {
              "workspaceRoot": "C:\\workspace",
              "customField": "keep-me",
              "telegramBotToken": "old-token"
            }
            """);

        var exportPath = Path.Combine(_root, "studio-export.json");
        var exportedPath = _service.ExportStudioConfig(exportPath);

        Assert.Equal(exportPath, exportedPath);
        Assert.True(File.Exists(exportedPath));

        File.WriteAllText(exportPath, """
            {
              "telegramBotToken": "new-token",
              "defaultPublishTemplateId": "custom-template"
            }
            """);

        var import = _service.ImportStudioConfig(exportPath);

        Assert.True(import.Success);
        Assert.Contains("telegramBotToken", import.ImportedFields);
        Assert.Contains("defaultPublishTemplateId", import.ImportedFields);

        var raw = File.ReadAllText(_studio.ConfigPath);
        Assert.Contains("customField", raw);
        Assert.Contains("keep-me", raw);
        Assert.Contains("new-token", raw);

        var config = _studio.Load();
        Assert.Equal("new-token", config.TelegramBotToken);
        Assert.Equal("custom-template", config.DefaultPublishTemplateId);
        Assert.Equal("C:\\workspace", config.WorkspaceRoot);
    }
}