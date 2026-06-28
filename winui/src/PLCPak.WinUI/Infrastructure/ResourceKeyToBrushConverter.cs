using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace PLCPak.WinUI.Infrastructure;

public sealed class ResourceKeyToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value as string ?? parameter as string ?? "PlcAccentBrush";
        if (Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush)
            return brush;

        return Application.Current.Resources["PlcAccentBrush"] as Brush ?? new SolidColorBrush();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}