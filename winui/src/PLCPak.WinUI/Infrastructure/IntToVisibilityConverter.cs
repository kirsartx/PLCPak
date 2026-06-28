using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PLCPak.WinUI.Infrastructure;

public sealed class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visible = value is int i && i > 0;
        if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase))
            visible = !visible;

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}