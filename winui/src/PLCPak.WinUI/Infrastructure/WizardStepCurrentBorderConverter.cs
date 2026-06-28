using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace PLCPak.WinUI.Infrastructure;

public sealed class WizardStepCurrentBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isCurrent = value is true;
        var key = isCurrent ? "PlcAccentBrush" : "PlcCardStrokeBrush";
        return Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush
            ? brush
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xC8, 0xC8, 0xC8));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}