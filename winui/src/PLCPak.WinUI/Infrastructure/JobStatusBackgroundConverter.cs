using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using PLCPak.Core.Models;
using Windows.UI;

namespace PLCPak.WinUI.Infrastructure;

public sealed class JobStatusBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush FailedBrush = new(Color.FromArgb(0x22, 0xFF, 0x00, 0x00));
    private static readonly SolidColorBrush ArchivedBrush = new(Color.FromArgb(0x33, 0x80, 0x80, 0x80));
    private static readonly SolidColorBrush TransparentBrush = new(Color.FromArgb(0, 0, 0, 0));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is JobStatus.Failed)
            return FailedBrush;

        if (value is JobStatus.Archived)
            return ArchivedBrush;

        return TransparentBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}