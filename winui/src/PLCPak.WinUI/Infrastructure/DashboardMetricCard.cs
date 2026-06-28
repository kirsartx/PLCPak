namespace PLCPak.WinUI.Infrastructure;

public sealed class DashboardMetricCard
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string AccentBrushKey { get; init; } = "PlcAccentBrush";
    public string FilterKey { get; init; } = string.Empty;
    public string Tip { get; init; } = string.Empty;
}