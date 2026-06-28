namespace PLCPak.Core.Services;

/// <summary>
/// 全局 UI 模式：默认简易（傻瓜式），开启后为专业模式（完整功能）。
/// </summary>
public sealed class UiModeService
{
    private readonly UiPreferencesService _prefs;
    private bool _isProfessionalMode;

    public UiModeService(UiPreferencesService prefs) => _prefs = prefs;

    public event Action? ModeChanged;

    public bool IsProfessionalMode => _isProfessionalMode;

    public bool IsSimpleMode => !_isProfessionalMode;

    public void LoadFromPreferences()
        => _isProfessionalMode = _prefs.Load().ProfessionalMode;

    public void SetProfessionalMode(bool value)
    {
        if (_isProfessionalMode == value)
            return;

        _isProfessionalMode = value;
        var prefs = _prefs.Load();
        prefs.ProfessionalMode = value;
        _prefs.Save(prefs);
        ModeChanged?.Invoke();
    }
}