using System.Globalization;
using System.Windows.Data;
using STranslate.Plugin;
using STranslate.Plugin.Translate.DeepSeek.ViewModel;

namespace STranslate.Plugin.Translate.DeepSeek.Converters;

/// <summary>
/// 将提示词和 ViewModel 组合成带策略标签的显示文本
/// </summary>
public class PromptWithStrategyConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not Prompt prompt || values[1] is not SettingsViewModel viewModel)
        {
            return string.Empty;
        }

        var strategyTag = viewModel.GetPromptStrategyTag(prompt.Name);
        return $"{prompt.Name} {strategyTag}";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
