# 策略提示词自定义功能

## 功能概述

支持对每个MCP策略（空白策略、混合判断、工具优先、工具强制）进行详细配置：

1. **自定义系统提示词** - 编辑策略的提示词，精确控制AI行为
2. **工具调用上限** - 设置总工具调用次数上限和同一工具连续调用上限
3. **工具结果显示模式** - 配置4种显示模式（禁用/粗略/混合/详细）
4. **工具链显示** - 开关控制是否显示工具链

用户可以通过图形界面编辑每个策略的这些设置。

## 界面入口

在**提示词配置**卡片中，新增了一行：

```
MCP策略：[工具策略 ▼] [编辑策略]
```

点击**"编辑策略"**按钮即可打开策略提示词编辑对话框。

## 编辑对话框

### 布局结构

```
┌─ 策略提示词配置 ──────────────────────────────────────────┐
│                                                            │
│  ┌─ 左侧 ───┐  ┌─ 右侧（可滚动）────────────────────────┐   │
│  │          │  │                                      │   │
│  │ 策略列表 │  │ 当前编辑：混合判断                    │   │
│  │          │  │                                      │   │
│  │ ● 空白   │  │ 💡 可用变量占位符                     │   │
│  │   策略   │  │   $description_rough → 简单列表      │   │
│  │          │  │   $description_detailed → 详细列表   │   │
│  │ ● 混合   │  │                                      │   │
│  │   判断   │  │ 📊 总工具调用上限 [══════════●════]   │   │
│  │   (选中) │  │                                      │   │
│  │          │  │ 🔄 同一工具连续调用上限 [════●════]   │   │
│  │ ● 工具   │  │                                      │   │
│  │   优先   │  │ 📋 工具结果显示模式 [混合显示 ▼]      │   │
│  │          │  │                                      │   │
│  │ ● 工具   │  │ ⛓️ 显示工具链 [开关]                  │   │
│  │   强制   │  │                                      │   │
│  │          │  │ ┌───────────────────────────────┐   │   │
│  │          │  │ │                               │   │   │
│  │          │  │ │ You are a helpful assistant   │   │   │
│  │          │  │ │ ...                           │   │   │
│  │          │  │ │                               │   │   │
│  │          │  │ └───────────────────────────────┘   │   │
│  │          │  │                                      │   │
│  │          │  │ ⚠️ 提示词中未包含变量占位符...       │   │
│  │          │  │                                      │   │
│  │          │  │ [重置为默认] [保存] [取消]            │   │
│  │          │  │                                      │   │
│  └──────────┘  └──────────────────────────────────────┘   │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

### 功能说明

1. **策略列表**（左侧）：固定显示4个策略，不可增删
2. **变量占位符**：
   - `$description_rough` - 简单工具列表（仅工具名称）
   - `$description_detailed` - 详细工具列表（名称+描述）
3. **总工具调用上限**（滑块）：设置该策略的最大总工具调用次数（0=无限）
4. **同一工具连续调用上限**（滑块）：设置同一工具的最大连续调用次数（0=无限）
5. **工具结果显示模式**（下拉框）：选择4种显示模式之一
   - **禁用** - 不显示工具结果和内联标记
   - **粗略** - 仅内联显示工具名
   - **混合** - 内联+截断显示结果
   - **详细** - 内联+完整显示结果
6. **显示工具链**（开关）：控制是否显示工具链
7. **文本编辑框**：编辑策略的系统提示词
8. **验证警告**：当提示词不包含变量时显示警告，再次点击保存可强制保存
9. **按钮**：
   - **重置为默认**：恢复该策略的所有设置为默认值
   - **保存**：保存所有自定义设置
   - **取消**：放弃修改

## 默认提示词

### 空白策略
```
Available MCP tools:
$description_rough
```

### 混合判断
```
You are a helpful assistant with access to optional MCP tools.

Available tools:
$description_detailed

Instructions:
- Tools are OPTIONAL - use them only when they can genuinely help answer the question
- For general knowledge, common sense, or simple questions - answer directly WITHOUT tools
- If no tool fits the request, answer directly with your own knowledge
```

### 工具优先
```
You are a helpful assistant with access to MCP tools.

Available tools:
$description_detailed

Instructions:
- PRIORITIZE using tools when they can provide better or more accurate information
- If no suitable tool is available, answer directly using your own knowledge
- If a tool fails or returns no useful data, gracefully answer without it
```

### 工具强制
```
You are a helpful assistant that MUST use available MCP tools.

Available tools:
$description_detailed

CRITICAL INSTRUCTIONS:
- You MUST use tools to answer the question
- If no suitable tool exists, reply: 'No suitable tool available to answer this question.'
- Do not answer from your own knowledge unless explicitly instructed
```

## 技术实现

### 数据结构

```csharp
// Settings.cs
public Dictionary<McpToolStrategy, string> CustomStrategyPrompts { get; set; } = new();           // 自定义提示词
public Dictionary<McpToolStrategy, int> StrategyConsecutiveToolLimits { get; set; } = new();     // 连续调用上限
public Dictionary<McpToolStrategy, int> StrategyTotalToolCallsLimits { get; set; } = new();      // 总工具调用上限
public Dictionary<McpToolStrategy, ToolResultDisplayMode> StrategyToolResultDisplayModes { get; set; } = new();  // 工具结果显示模式
public Dictionary<McpToolStrategy, bool> StrategyToolChainDisplay { get; set; } = new();         // 工具链显示开关

// Main.cs - 默认提示词
public static class DefaultStrategyPrompts
{
    public static string GetDefaultPrompt(McpToolStrategy strategy);
}
```

### 提示词生成逻辑

```csharp
private string GetSystemPromptByStrategy(McpToolStrategy strategy, ...)
{
    // 1. 获取自定义提示词或默认提示词
    string prompt = Settings.CustomStrategyPrompts.TryGetValue(...) 
        ?? DefaultStrategyPrompts.GetDefaultPrompt(strategy);
    
    // 2. 生成两种格式的工具描述
    var descriptionRough = string.Join("\n", tools.Select(t => $"- {t.Tool.Name}"));
    var descriptionDetailed = string.Join("\n", tools.Select(t => $"- {t.Tool.Name}: {t.Tool.Description}"));
    
    // 3. 替换变量占位符
    return prompt
        .Replace("$description_rough", descriptionRough)
        .Replace("$description_detailed", descriptionDetailed);
}
```

### 主题说明

**已实现**：编辑对话框已支持跟随 STranslate 主程序的主题切换（暗色/亮色）。

**实现方式**：
- 使用 Fork 版本的 `IPluginContext.ApplyTheme(Window)` 方法
- 在对话框构造函数中传入 `IPluginContext` 实例
- 窗口加载时自动调用 `_context.ApplyTheme(this)` 应用主题

**代码示例**：
```csharp
// StrategyPromptDialog.xaml.cs
public StrategyPromptDialog(IPluginContext context)
{
    InitializeComponent();
    _context = context;
    Loaded += OnLoaded;
}

private void OnLoaded(object sender, RoutedEventArgs e)
{
    // 应用主题（使用 Fork 版本的 ApplyTheme 接口）
    _context.ApplyTheme(this);
    // ...
}
```

**注意**：此功能需要 STranslate 的 Fork 版本支持（`IPluginContext` 接口包含 `ApplyTheme` 方法）。

## 使用建议

### 何时需要自定义提示词？

1. **混合判断策略太保守**：如果觉得AI从不使用工具，可以修改提示词让AI更积极
2. **工具优先策略不够明确**：可以增强"优先"的描述，让AI更主动使用工具
3. **特定场景需求**：针对特定类型的查询定制AI的行为

### 自定义示例

**让混合判断策略更积极：**
```
You are a helpful assistant with access to MCP tools.

Available tools:
$description_detailed

Instructions:
- You SHOULD use tools whenever they might provide useful information
- Don't hesitate to call tools - it's better to check than to guess
- For factual questions, always use tools to verify before answering
```

**简化空白策略：**
```
You have access to these tools:
$description_detailed

Use them when needed.
```

### 注意事项

1. **变量必须保留**：如果不包含`$description_rough`或`$description_detailed`，系统不会插入工具列表
2. **语言一致性**：提示词语言应与AI对话语言一致（中文提示词配中文对话）
3. **重置功能**：随时可以重置为默认提示词
4. **保存即生效**：保存后下次翻译立即生效，无需重启

## 相关文件

- `Settings.cs` - 策略级设置数据字典（CustomStrategyPrompts, StrategyConsecutiveToolLimits, StrategyTotalToolCallsLimits, StrategyToolResultDisplayModes, StrategyToolChainDisplay）
- `Main.cs` - DefaultStrategyPrompts 默认提示词，GetSystemPromptByStrategy 方法，ThreeStageContentBuilder 内容构建器
- `View/StrategyPromptDialog.xaml` - 编辑对话框界面（带滚动条）
- `ViewModel/StrategyPromptDialogViewModel.cs` - 编辑对话框视图模型
- `View/SettingsView.xaml` - 设置界面（添加编辑按钮）
- `ViewModel/SettingsViewModel.cs` - EditStrategyPromptCommand 命令
