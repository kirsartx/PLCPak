using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class UiPreferencesService
{
    private readonly WorkspaceService _workspace;

    public UiPreferencesService(WorkspaceService workspace) => _workspace = workspace;

    public string PrefsPath => Path.Combine(_workspace.GetWorkspaceRoot(), "ui-prefs.json");

    public UiPreferences Load()
        => JsonHelper.ReadFile<UiPreferences>(PrefsPath) ?? new UiPreferences();

    public void Save(UiPreferences prefs)
    {
        Directory.CreateDirectory(_workspace.GetWorkspaceRoot());
        JsonHelper.WriteFile(PrefsPath, prefs);
    }
}