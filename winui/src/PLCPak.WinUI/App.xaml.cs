using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using PLCPak.Core;
using PLCPak.Core.Diagnostics;
using PLCPak.Core.Services;
using PLCPak.WinUI.Infrastructure;

namespace PLCPak.WinUI;

public partial class App : Application
{
    private static string LogPath => AppPaths.FromExecutableDirectory().StartupLogPath;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);

    public static PlcPakAppContext Services { get; private set; } = null!;

    public App()
    {
        UnhandledException += OnUnhandledException;
        try
        {
            InitializeComponent();
            Services = PlcPakAppContext.FromExecutableDirectory();
            AppLogger.Write(LogPath, "App initialized");
        }
        catch (Exception ex)
        {
            AppLogger.WriteError(LogPath, ex, "App ctor failed");
            MessageBoxW(0, ex.Message, "PLCPak 初始化失败", 0x10);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            ThemeService.LoadFromPreferences(Services.UiPreferences);
            LocalizationService.LoadFromPreferences(Services.UiPreferences);

            var window = new MainWindow();
            window.Activate();
            AppLogger.Write(LogPath, "MainWindow activated");
        }
        catch (Microsoft.UI.Xaml.Markup.XamlParseException xpe)
        {
            AppLogger.WriteError(LogPath, xpe, "OnLaunched XAML failed");
            MessageBoxW(0, $"界面加载失败:\n{xpe.Message}\n\n详见 logs\\plcpak-startup.log", "PLCPak 窗口创建失败", 0x10);
            return;
        }
        catch (Exception ex)
        {
            AppLogger.WriteError(LogPath, ex, "OnLaunched failed");
            MessageBoxW(0, ex.Message, "PLCPak 窗口创建失败", 0x10);
            throw;
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        AppLogger.WriteError(LogPath, e.Exception, "Unhandled");
        AppLogger.Write(LogPath, $"Dispatch stack:{Environment.NewLine}{Environment.StackTrace}");
        MessageBoxW(0, $"{e.Exception.Message}\n\n详见 logs\\plcpak-startup.log", "PLCPak 运行错误", 0x10);
        e.Handled = true;
    }
}