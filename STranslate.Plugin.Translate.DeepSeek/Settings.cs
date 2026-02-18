using CommunityToolkit.Mvvm.ComponentModel;

namespace STranslate.Plugin.Translate.DeepSeek;

/// <summary>
/// MCP工具使用策略枚举
/// </summary>
public enum McpToolStrategy
{
    /// <summary>禁用MCP服务器</summary>
    Disabled,
    /// <summary>空白策略 - 无提示词控制</summary>
    Blank,
    /// <summary>混合判断 - AI自行决定是否使用工具</summary>
    Hybrid,
    /// <summary>工具优先 - 优先使用工具，无工具时自行回答</summary>
    ToolFirst,
    /// <summary>工具强制 - 必须使用工具</summary>
    ToolForced
}

/// <summary>
/// 工具结果显示模式枚举（4种模式）
/// </summary>
public enum ToolResultDisplayMode
{
    /// <summary>禁用结果 - 不显示工具结果模块，也不在AI回复中内联显示</summary>
    Disabled,
    /// <summary>粗略结果 - 只显示内联显示，不显示工具结果模块</summary>
    Minimal,
    /// <summary>混合显示 - 同时显示内联显示和工具结果模块（中等详细）</summary>
    Mixed,
    /// <summary>详细结果 - 同时显示内联显示和工具模块，显示完整返回值</summary>
    Detailed
}

public class Settings
{
    /// <summary>
    /// 配置版本号，用于配置迁移
    /// </summary>
    public int ConfigVersion { get; set; } = 1;
    
    public string ApiKey { get; set; } = string.Empty;
    public string Url { get; set; } = "https://api.deepseek.com/";
    public string Model { get; set; } = "deepseek-chat";
    public List<string> Models { get; set; } =
    [
        "deepseek-chat",
        "deepseek-reasoner",
    ];
    public int MaxTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.7;
    public int TopP { get; set; } = 1;
    public int N { get; set; } = 1;
    public bool Stream { get; set; } = true;
    public int? MaxRetries { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 1000;

    // 命令系统配置（独立于MCP）
    public bool EnableCommandSystem { get; set; } = false;

    // MCP全局配置
    public bool EnableMcp { get; set; } = false;
    public int LogLevel { get; set; } = 0; // 0=粗略, 1=中等, 2=详细
    
    // MCP客户端配置
    public int McpToolCacheMinutes { get; set; } = 5; // 工具列表缓存时间（分钟）
    public int MaxConcurrentTools { get; set; } = 5; // 最大并发工具数
    public int McpConnectionPoolSize { get; set; } = 10; // HTTP/2连接池大小
    public int McpSessionTimeoutMinutes { get; set; } = 30; // Session超时时间（分钟）
    
    // 多服务器配置
    public List<McpServerConfig> McpServers { get; set; } = [];
    public int CurrentServerIndex { get; set; } = 0;

    // 提示词级 MCP 策略映射表（Key: 提示词名称, Value: 策略，默认为 Disabled）
    public Dictionary<string, McpToolStrategy> PromptStrategyMap { get; set; } = new();
    
    // 策略自定义提示词（Key: 策略类型, Value: 自定义系统提示词）
    public Dictionary<McpToolStrategy, string> CustomStrategyPrompts { get; set; } = new();
    
    // 策略连续调用上限（Key: 策略类型, Value: 同一工具最大连续调用次数，0=无限）
    public Dictionary<McpToolStrategy, int> StrategyConsecutiveToolLimits { get; set; } = new();
    
    // 策略总工具调用上限（Key: 策略类型, Value: 最大总调用次数，0=无限）
    public Dictionary<McpToolStrategy, int> StrategyTotalToolCallsLimits { get; set; } = new();
    
    // 策略工具结果显示模式（Key: 策略类型, Value: 显示模式，默认为Disabled）
    public Dictionary<McpToolStrategy, ToolResultDisplayMode> StrategyToolResultDisplayModes { get; set; } = new();
    
    // 策略工具链显示开关（Key: 策略类型, Value: 是否显示，默认false）
    public Dictionary<McpToolStrategy, bool> StrategyToolChainDisplay { get; set; } = new();

    public List<Prompt> Prompts { get; set; } =
    [
        new("翻译",
        [
            new PromptItem("system", "You are a professional, authentic translation engine. You only return the translated text, without any explanations."),
            new PromptItem("user", "Please translate  into $target (avoid explaining the original text):\r\n\r\n$content"),
        ], true),
        new("润色",
        [
            new PromptItem("system", "You are a professional, authentic text polishing engine. You only return the polished text, without any explanations."),
            new PromptItem("user", "Please polish the following text in $source (avoid explaining the original text):\r\n\r\n$content"),
        ]),
        new("总结",
        [
            new PromptItem("system", "You are a professional, authentic text summarization engine. You only return the summarized text, without any explanations."),
            new PromptItem("user", "Please summarize the following text in $source (avoid explaining the original text):\r\n\r\n$content"),
        ]),
    ];
    
    /// <summary>
    /// 执行配置迁移
    /// </summary>
    public void Migrate()
    {
        if (ConfigVersion < 2)
        {
            // 迁移到版本2：添加新的策略级设置默认值
            foreach (var strategy in Enum.GetValues<McpToolStrategy>())
            {
                // 初始化工具链显示默认值
                if (!StrategyToolChainDisplay.ContainsKey(strategy))
                {
                    StrategyToolChainDisplay[strategy] = false;
                }
                
                // 初始化工具结果显示模式默认值
                if (!StrategyToolResultDisplayModes.ContainsKey(strategy))
                {
                    StrategyToolResultDisplayModes[strategy] = ToolResultDisplayMode.Disabled;
                }
                
                // 初始化连续调用上限默认值
                if (!StrategyConsecutiveToolLimits.ContainsKey(strategy))
                {
                    StrategyConsecutiveToolLimits[strategy] = StrategyConsecutiveLimitHelper.DEFAULT_LIMIT;
                }
                
                // 初始化总工具调用上限默认值
                if (!StrategyTotalToolCallsLimits.ContainsKey(strategy))
                {
                    StrategyTotalToolCallsLimits[strategy] = StrategyTotalToolCallsHelper.DEFAULT_LIMIT;
                }
            }
            
            ConfigVersion = 2;
        }
        
        // 未来的迁移逻辑可以在这里添加
        // if (ConfigVersion < 3) { ... }
    }
}

/// <summary>
/// MCP服务器配置
/// </summary>
public partial class McpServerConfig : ObservableObject
{
    [ObservableProperty] private string _name = "示例服务器";
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private int _maxToolCalls = 10;
    [ObservableProperty] private List<McpToolConfig> _tools = [];
    [ObservableProperty] private string _lastConnectedUrl = string.Empty; // 用于判断是否需要重置工具配置
}

/// <summary>
/// MCP工具配置
/// </summary>
public partial class McpToolConfig : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private string _inputSchema = "{}";
}

/// <summary>
/// 提示词策略辅助类
/// </summary>
public static class PromptStrategyHelper
{
    /// <summary>
    /// 获取提示词显示用的策略标签文本
    /// </summary>
    public static string GetStrategyDisplayText(McpToolStrategy strategy) => strategy switch
    {
        McpToolStrategy.Disabled => "[禁用服务]",
        McpToolStrategy.Blank => "[空白策略]",
        McpToolStrategy.Hybrid => "[混合判断]",
        McpToolStrategy.ToolFirst => "[工具优先]",
        McpToolStrategy.ToolForced => "[工具强制]",
        _ => "[禁用服务]"
    };

    /// <summary>
    /// 获取策略图标（已废弃，保留用于兼容性）
    /// </summary>
    public static string GetStrategyIcon(McpToolStrategy strategy) => "";

    /// <summary>
    /// 获取策略名称
    /// </summary>
    public static string GetStrategyName(McpToolStrategy strategy) => strategy switch
    {
        McpToolStrategy.Disabled => "禁用服务",
        McpToolStrategy.Blank => "空白策略",
        McpToolStrategy.Hybrid => "混合判断",
        McpToolStrategy.ToolFirst => "工具优先",
        McpToolStrategy.ToolForced => "工具强制",
        _ => "禁用服务"
    };
}

/// <summary>
/// 提示词策略选项（用于下拉框绑定）- 纯文字，无图标
/// </summary>
public class PromptStrategyOption
{
    public McpToolStrategy Strategy { get; }
    public string Name { get; }
    public string DisplayText => Name;

    public PromptStrategyOption(McpToolStrategy strategy)
    {
        Strategy = strategy;
        Name = PromptStrategyHelper.GetStrategyName(strategy);
    }
}

/// <summary>
/// 策略连续调用上限辅助类
/// </summary>
public static class StrategyConsecutiveLimitHelper
{
    /// <summary>
    /// 默认上限值（5次）
    /// </summary>
    public const int DEFAULT_LIMIT = 5;
    
    /// <summary>
    /// 最小值（0=无限）
    /// </summary>
    public const int MIN_LIMIT = 0;
    
    /// <summary>
    /// 最大值（10次）
    /// </summary>
    public const int MAX_LIMIT = 10;
    
    /// <summary>
    /// 获取策略的连续调用上限，未设置时返回默认值
    /// </summary>
    public static int GetLimit(Dictionary<McpToolStrategy, int> limits, McpToolStrategy strategy)
    {
        if (limits.TryGetValue(strategy, out var limit))
        {
            return Math.Clamp(limit, MIN_LIMIT, MAX_LIMIT);
        }
        return DEFAULT_LIMIT;
    }
    
    /// <summary>
    /// 检查是否启用上限（0代表不启用/无限）
    /// </summary>
    public static bool IsEnabled(int limit) => limit > 0;
}

/// <summary>
/// 策略总工具调用上限辅助类
/// </summary>
public static class StrategyTotalToolCallsHelper
{
    /// <summary>
    /// 默认上限值（15次）
    /// </summary>
    public const int DEFAULT_LIMIT = 15;
    
    /// <summary>
    /// 最小值（0=无限）
    /// </summary>
    public const int MIN_LIMIT = 0;
    
    /// <summary>
    /// 最大值（50次）
    /// </summary>
    public const int MAX_LIMIT = 50;
    
    /// <summary>
    /// 获取策略的总工具调用上限，未设置时返回默认值
    /// </summary>
    public static int GetLimit(Dictionary<McpToolStrategy, int> limits, McpToolStrategy strategy)
    {
        if (limits.TryGetValue(strategy, out var limit))
        {
            return Math.Clamp(limit, MIN_LIMIT, MAX_LIMIT);
        }
        return DEFAULT_LIMIT;
    }
    
    /// <summary>
    /// 检查是否启用上限（0代表不启用/无限）
    /// </summary>
    public static bool IsEnabled(int limit) => limit > 0;
}

/// <summary>
/// 工具结果显示模式选项（用于下拉框绑定）
/// </summary>
public class ToolResultDisplayModeOption
{
    public ToolResultDisplayMode Mode { get; }
    public string DisplayName { get; }

    public ToolResultDisplayModeOption(ToolResultDisplayMode mode, string displayName)
    {
        Mode = mode;
        DisplayName = displayName;
    }
}
