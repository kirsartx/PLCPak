using Microsoft.UI.Xaml.Data;
using PLCPak.Core.Models;

namespace PLCPak.WinUI.Infrastructure;

public sealed class WizardStepStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is WizardStepStatus status)
        {
            return status switch
            {
                WizardStepStatus.Pending => "待办",
                WizardStepStatus.Active => "进行中",
                WizardStepStatus.Done => "完成",
                WizardStepStatus.Skipped => "跳过",
                WizardStepStatus.Blocked => "阻塞",
                _ => status.ToString()
            };
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}