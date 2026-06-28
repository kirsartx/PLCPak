using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PLCPak.WinUI.Infrastructure;

public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visible = value is string s && !string.IsNullOrWhiteSpace(s);
        if (parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            visible = !visible;

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}