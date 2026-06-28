using PLCPak.Core;
using PLCPak.Core.Diagnostics;

namespace PLCPak.WinUI.Infrastructure;

public static class UiDiagLog
{
    private static string LogPath => AppPaths.FromExecutableDirectory().StartupLogPath;

    public static void Breadcrumb(string message) => AppLogger.Write(LogPath, $"Breadcrumb: {message}");
}