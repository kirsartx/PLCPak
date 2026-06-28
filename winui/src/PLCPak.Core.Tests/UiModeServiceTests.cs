using PLCPak.Core.Services;

namespace PLCPak.Core.Tests;

public sealed class UiModeServiceTests : IDisposable
{
    private readonly string _root;
    private readonly UiPreferencesService _prefs;
    private readonly UiModeService _mode;

    public UiModeServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "plcpak-uimode-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(_root, "data");
        Directory.CreateDirectory(data);

        var workspaceRoot = Path.Combine(_root, "workspace");
        File.WriteAllText(Path.Combine(data, "studio-config.json"),
            $"{{\"workspaceRoot\":\"{workspaceRoot.Replace("\\", "\\\\")}\"}}");

        var paths = new AppPaths(Path.Combine(_root, "app"));
        var workspace = new WorkspaceService(paths, new StudioConfigService(paths));
        _prefs = new UiPreferencesService(workspace);
        _mode = new UiModeService(_prefs);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void LoadFromPreferences_defaults_to_simple_mode()
    {
        _mode.LoadFromPreferences();

        Assert.False(_mode.IsProfessionalMode);
        Assert.True(_mode.IsSimpleMode);
    }

    [Fact]
    public void SetProfessionalMode_persists_and_raises_event()
    {
        var changed = false;
        _mode.ModeChanged += () => changed = true;

        _mode.SetProfessionalMode(true);

        Assert.True(_mode.IsProfessionalMode);
        Assert.True(changed);
        Assert.True(_prefs.Load().ProfessionalMode);
    }
}