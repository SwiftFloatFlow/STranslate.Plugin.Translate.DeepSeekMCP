using System.Globalization;
using System.Windows.Data;

namespace STranslate.Plugin.Translate.DeepSeek.Converters;

/// <summary>
/// MCP工具策略到描述文本的转换器
/// </summary>
public class StrategyToDescriptionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is McpToolStrategy strategy)
        {
            return strategy switch
            {
                McpToolStrategy.Disabled => "完全禁用MCP服务器功能，AI仅使用自身知识回答问题",
                McpToolStrategy.Blank => "启用MCP但不添加任何提示词控制，完全由AI自主决定是否使用工具",
                McpToolStrategy.Hybrid => "提示AI工具是可选的，由AI自行判断是否需要使用工具",
                McpToolStrategy.ToolFirst => "提示AI优先使用工具获取信息，没有合适工具时再自行回答",
                McpToolStrategy.ToolForced => "强制要求AI必须使用工具，无合适工具时将返回错误提示",
                _ => "未知策略"
            };
        }
        return "未知策略";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// MCP工具策略到显示名称的转换器
/// </summary>
public class StrategyToNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is McpToolStrategy strategy)
        {
            return strategy switch
            {
                McpToolStrategy.Disabled => "禁用",
                McpToolStrategy.Blank => "空白策略",
                McpToolStrategy.Hybrid => "混合判断",
                McpToolStrategy.ToolFirst => "工具优先",
                McpToolStrategy.ToolForced => "工具强制",
                _ => "未知"
            };
        }
        return "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}