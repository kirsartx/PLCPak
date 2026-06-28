using PLCPak.Core.Services;

namespace PLCPak.WinUI.Infrastructure;

/// <summary>将 UiModeService 变更转发到 ViewModel 属性通知。</summary>
public sealed class UiModeNotifier
{
    private readonly UiModeService _mode;
    private readonly Action _notify;

    public UiModeNotifier(PlcPakAppContext app, Action notify)
    {
        _mode = app.UiMode;
        _notify = notify;
        _mode.ModeChanged += OnModeChanged;
    }

    public bool IsProfessionalMode => _mode.IsProfessionalMode;

    public bool IsSimpleMode => _mode.IsSimpleMode;

    private void OnModeChanged() => _notify();
}