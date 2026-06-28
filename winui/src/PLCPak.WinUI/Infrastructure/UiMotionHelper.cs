namespace PLCPak.WinUI.Infrastructure;

public static class UiMotionHelper
{
    public static bool ShouldReduceMotion()
    {
        try
        {
            return App.Services.UiPreferences.Load().ReduceMotion;
        }
        catch
        {
            return false;
        }
    }
}