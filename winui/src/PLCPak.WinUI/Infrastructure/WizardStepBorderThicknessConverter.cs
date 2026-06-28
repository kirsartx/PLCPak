using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PLCPak.WinUI.Infrastructure;

public sealed class WizardStepBorderThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? new Thickness(2) : new Thickness(1);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}