using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace STranslate.Plugin.Translate.DeepSeek;

public class BoolToColorConverter : IValueConverter
{
    public Brush TrueColor { get; set; } = Brushes.Green;
    public Brush FalseColor { get; set; } = Brushes.Red;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueColor : FalseColor;
        }
        return FalseColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 布尔值转描述文本的转换器（用于MCP开关状态显示）
/// </summary>
public class BoolToDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "启用" : "禁用";
        }
        return "未知";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}