using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class StudioConfigService
{
    private readonly AppPaths _paths;

    public StudioConfigService(AppPaths paths) => _paths = paths;

    public string ConfigPath => Path.Combine(_paths.DataRoot, "studio-config.json");

    public StudioConfig Load()
    {
        var config = JsonHelper.ReadFile<StudioConfig>(ConfigPath);
        return config ?? new StudioConfig();
    }

    public void Save(StudioConfig config)
    {
        Directory.CreateDirectory(_paths.DataRoot);
        JsonHelper.WriteFile(ConfigPath, config);
    }
}