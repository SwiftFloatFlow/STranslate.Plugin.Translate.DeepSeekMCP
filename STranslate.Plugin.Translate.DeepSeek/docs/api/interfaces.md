# API参考 - 核心接口

## ITranslator 接口

**命名空间：** `STranslate.Plugin.Translate.DeepSeek`

**实现类：** `Main`

### TranslateAsync 方法

**签名：**
```csharp
Task TranslateAsync(
    TranslateRequest request,
    TranslateResult result,
    CancellationToken cancellationToken
);
```

**参数：**
- `request`: 翻译请求参数
- `result`: 翻译结果对象（输出）
- `cancellationToken`: 取消令牌

**示例：**
```csharp
var main = new Main(context, settings);
var request = new TranslateRequest
{
    Text = "Hello World",
    SourceLang = LangEnum.en,
    TargetLang = LangEnum.zh_cn
};
var result = new TranslateResult();

await main.TranslateAsync(request, result, CancellationToken.None);
Console.WriteLine(result.Text);  // 输出翻译结果
```

## 数据模型

### TranslateRequest

```csharp
public class TranslateRequest
{
    public string Text { get; set; }              // 要翻译的文本
    public LangEnum SourceLang { get; set; }      // 源语言
    public LangEnum TargetLang { get; set; }      // 目标语言
}
```

### TranslateResult

```csharp
public class TranslateResult
{
    public string Text { get; set; }              // 翻译结果
    public bool IsError { get; set; }             // 是否错误
    public string ErrorMessage { get; set; }      // 错误信息
    
    public void Fail(string message)              // 设置错误
    {
        IsError = true;
        ErrorMessage = message;
    }
}
```

### Settings

```csharp
public class Settings
{
    // DeepSeek API设置
    public string Url { get; set; }               // API地址
    public string ApiKey { get; set; }            // API密钥
    public string Model { get; set; }             // 模型名称
    
    // MCP设置
    public bool EnableMcp { get; set; }           // 启用MCP
    public McpToolStrategy ToolStrategy { get; set; }  // 工具策略
    public List<McpServerConfig> McpServers { get; set; }  // 服务器列表
}
```

### McpServerConfig

```csharp
public class McpServerConfig
{
    public string Name { get; set; }              // 服务器名称
    public string Url { get; set; }               // 服务器地址
    public string ApiKey { get; set; }            // API密钥
    public bool Enabled { get; set; }             // 是否启用
    public int MaxToolCalls { get; set; }         // 最大工具调用次数
    public List<McpToolConfig> Tools { get; set; }  // 工具列表
}
```

### McpToolConfig

```csharp
public class McpToolConfig
{
    public string Name { get; set; }              // 工具名称
    public string Description { get; set; }       // 工具描述
    public bool Enabled { get; set; }             // 是否启用
}
```

## 枚举

### McpToolStrategy

```csharp
public enum McpToolStrategy
{
    Disabled,      // 禁用MCP
    Blank,         // 空白策略 - AI自行判断
    Hybrid,        // 混合策略 - 工具可选
    ToolFirst,     // 工具优先
    ToolForced     // 工具强制
}
```

## IPluginContext 接口

**提供：** STranslate主程序

```csharp
public interface IPluginContext
{
    ILogger Logger { get; }                       // 日志记录器
    void SaveSettings(Settings settings);         // 保存设置
    Settings LoadSettings();                      // 加载设置
}
```

**使用：**
```csharp
public class Main : ITranslator
{
    private readonly IPluginContext _context;
    private readonly ILogger _logger;
    
    public Main(IPluginContext context, Settings settings)
    {
        _context = context;
        _logger = context.Logger;
    }
    
    public void Save()
    {
        _context.SaveSettings(_settings);
    }
}
```

## McpClient 类

### 构造函数

```csharp
public McpClient(
    string serverUrl,                              // 服务器URL
    string apiKey,                                 // API密钥（可选）
    ILogger logger,                                // 日志记录器
    int logLevel                                   // 日志级别
)
```

### 方法

```csharp
// 连接服务器
Task<bool> ConnectAsync(CancellationToken cancellationToken);

// 获取可用工具列表
Task<List<McpTool>> ListToolsAsync(CancellationToken cancellationToken);

// 调用工具
Task<JsonNode> CallToolAsync(
    string toolName,                               // 工具名称
    JsonNode arguments,                            // 参数（JSON）
    CancellationToken cancellationToken
);
```

## SettingsViewModel 类

### 属性

```csharp
// 基础设置
public string Url { get; set; }
public string ApiKey { get; set; }

// MCP设置
public bool EnableMcp { get; set; }
public McpToolStrategy ToolStrategy { get; set; }
public ObservableCollection<McpToolStrategy> ToolStrategies { get; }

// 服务器列表
public ObservableCollection<McpServerConfig> McpServers { get; }
public int CurrentServerIndex { get; set; }

// 当前服务器属性
public string CurrentServerName { get; set; }
public string CurrentServerUrl { get; set; }
public bool CurrentServerEnabled { get; set; }
```

### 命令

```csharp
// 添加新服务器
IRelayCommand AddNewServerCommand { get; }

// 复制当前服务器
IRelayCommand DuplicateCurrentServerCommand { get; }

// 删除当前服务器
IRelayCommand DeleteCurrentServerCommand { get; }

// 测试连接并发现工具
IAsyncRelayCommand TestAndDiscoverToolsCommand { get; }
```

## 事件

### PropertyChanged

**来源：** 所有ViewModel类

**使用：**
```csharp
viewModel.PropertyChanged += (sender, e) =>
{
    if (e.PropertyName == nameof(SettingsViewModel.CurrentServerName))
    {
        // 服务器名称变更
    }
};
```

## 扩展方法

### StringExtensions

```csharp
public static class StringExtensions
{
    // 截断字符串
    public static string Truncate(this string value, int maxLength);
    
    // 检查是否为空或空白
    public static bool IsNullOrWhiteSpace(this string value);
}
```

## 常量

### DefaultValues

```csharp
public static class DefaultValues
{
    public const string DefaultModel = "deepseek-chat";
    public const string DefaultUrl = "https://api.deepseek.com/v1";
    public const int DefaultMaxToolCalls = 10;
    public const int DefaultTimeout = 30;
}
```

## 类型别名

```csharp
// 语言枚举（来自STranslate）
using LangEnum = STranslate.Util.LangEnum;

// JSON节点
using JsonNode = System.Text.Json.Nodes.JsonNode;
```