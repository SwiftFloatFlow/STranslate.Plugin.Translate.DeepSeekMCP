using System.Globalization;
using System.Windows.Data;
using STranslate.Plugin;

namespace STranslate.Plugin.Translate.DeepSeek.Converters;

/// <summary>
/// 提示词显示名称转换器：为全局提示词添加"★"前缀标识，并支持超长名称截断
/// </summary>
public class PromptDisplayNameConverter : IValueConverter
{
    /// <summary>最大显示长度（包含标识符）</summary>
    public const int MaxDisplayLength = 15;
    
    /// <summary>省略号</summary>
    public const string Ellipsis = "...";
    
    /// <summary>全局提示词前缀</summary>
    public const string GlobalPrefix = "★";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Prompt prompt)
        {
            return GetDisplayName(prompt);
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
    
    /// <summary>
    /// 获取提示词的显示名称（静态方法，供ViewModel使用）
    /// </summary>
    public static string GetDisplayName(Prompt? prompt)
    {
        if (prompt == null) return "";
        
        var tag = prompt.Tag?.ToString();
        bool isGlobal = tag?.StartsWith("Global:") == true;
        string prefix = isGlobal ? GlobalPrefix : "";
        string name = prompt.Name ?? "";
        
        // 计算总长度
        int totalLength = prefix.Length + name.Length;
        
        // 如果超长，截断名称（保留前缀）
        if (totalLength > MaxDisplayLength)
        {
            int availableLength = MaxDisplayLength - prefix.Length - Ellipsis.Length;
            if (availableLength > 0)
            {
                name = name.Substring(0, availableLength) + Ellipsis;
            }
            else
            {
                // 如果空间不够，只显示前缀+省略号
                name = Ellipsis;
            }
        }
        
        return prefix + name;
    }
}
