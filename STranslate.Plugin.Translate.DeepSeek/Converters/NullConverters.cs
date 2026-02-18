using System.Globalization;
using System.Windows.Data;
using STranslate.Plugin;

namespace STranslate.Plugin.Translate.DeepSeek.Converters;

/// <summary>
/// 将 null 转换为 bool 的转换器（null -> false, not null -> true）
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 将提示词名称转换为带策略标签的显示文本
/// 需要配合包含 PromptStrategyMap 的 DataContext 使用
/// </summary>
public class PromptToDisplayTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Prompt prompt)
            return string.Empty;

        // 返回提示词名称，策略标签通过其他方式显示
        return prompt.Name;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
