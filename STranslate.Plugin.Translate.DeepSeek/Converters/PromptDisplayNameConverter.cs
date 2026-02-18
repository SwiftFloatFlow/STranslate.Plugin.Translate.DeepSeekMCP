using System.Globalization;
using System.Windows.Data;
using STranslate.Plugin;

namespace STranslate.Plugin.Translate.DeepSeek.Converters;

public class PromptDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Prompt prompt)
        {
            return prompt.GetPromptDisplayName();
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
