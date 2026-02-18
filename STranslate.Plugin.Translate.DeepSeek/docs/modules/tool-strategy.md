# 工具调用策略

## 策略概述

插件提供5种MCP工具调用策略，通过 `McpToolStrategy` 枚举定义。支持**提示词级策略绑定**，可以为不同提示词设置不同的MCP策略。

```csharp
public enum McpToolStrategy
{
    Disabled,      // 禁用服务
    Blank,         // 空白策略
    Hybrid,        // 混合判断
    ToolFirst,     // 工具优先
    ToolForced     // 工具强制
}
```

## 两层优先级架构

插件采用两层优先级策略系统：

```
┌─────────────────────────────────────────┐
│ 第1层：MCP服务功能（总开关）              │
│   - 控制所有MCP功能的启用/禁用            │
│   - 关闭时所有策略绑定和命令系统失效      │
│   - 影响：提示词策略、命令系统均不可用    │
└─────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────┐
│ 第2层：提示词级策略绑定                   │
│   - 为每个提示词独立设置MCP策略          │
│   - 5种策略可选：禁用服务/空白策略/        │
│     混合判断/工具优先/工具强制            │
│   - 新提示词默认使用"禁用服务"策略        │
│   - 可通过设置界面或命令系统修改          │
└─────────────────────────────────────────┘
```

### 优先级规则

1. **MCP服务功能关闭** → 所有提示词策略绑定和命令系统失效，完全禁用MCP
2. **提示词已绑定策略** → 使用绑定的策略（仅当MCP启用时生效）
3. **提示词未绑定策略** → 使用默认策略（Disabled）
4. **新提示词** → 默认使用"禁用服务"策略

### 实际应用示例

```
场景1：全局禁用MCP
- MCP服务功能：关闭
- 结果：所有翻译使用传统DeepSeek API，命令系统不可用

场景2：提示词绑定特定策略
- MCP服务功能：开启
- 提示词"翻译"：绑定"工具优先"
- 提示词"润润色"：绑定"禁用服务"（默认）
- 结果：
  - 使用"翻译"提示词 → 使用"工具优先"策略
  - 使用"润色"提示词 → 使用"禁用服务"策略（传统翻译）

场景3：不同提示词不同策略
- 提示词"技术文档"：绑定"工具优先"
- 提示词"日常对话"：绑定"禁用服务"
- 提示词"新闻报道"：绑定"混合判断"
- 提示词"代码注释"：未绑定（默认"禁用服务"）
- 结果：根据选择的提示词自动切换MCP策略

场景4：通过命令快速切换
- 用户输入：/切换 工具强制
- 结果：当前提示词的策略切换为"工具强制"
- 当前提示词：立即生效，无需重启
```

## 策略对比

| 策略 | MCP连接时机 | 工具使用 | AI自由度 | 适用场景 |
|------|------------|---------|---------|---------|
| **禁用服务** | 不连接 | 不使用 | 无 | 传统翻译 |
| **空白策略** | 立即连接 | AI决定 | 最高 | 灵活翻译 |
| **混合判断** | 立即连接 | 可选使用 | 高 | 智能翻译 |
| **工具优先** | 立即连接 | 优先使用 | 中 | 增强翻译 |
| **工具强制** | 立即连接 | 必须使用 | 低 | 工具依赖 |

## 策略详解

### 1. Disabled（禁用）

**行为：**
- 不连接任何MCP服务器
- 使用传统DeepSeek API直接翻译

**系统提示词：**
```
（无MCP相关提示）
```

**适用场景：**
- 不需要外部工具的普通翻译
- 快速翻译，无需额外信息

---

### 2. Blank（空白策略）

**行为：**
- 立即连接MCP并获取工具列表
- 让AI自行判断是否需要使用工具
- AI根据工具描述和自身知识决定

**系统提示词：**
```
Available MCP tools:
- tool1: 工具1描述
- tool2: 工具2描述

（无其他指令）
```

**工作流程：**
```
1. 连接MCP并获取工具列表
2. 发送带工具列表的请求
   → AI决定是否需要使用工具
3. 如需要，执行工具调用
4. 返回最终结果
```

**适用场景：**
- 让AI完全自主决策
- 不确定是否需要工具时

---

### 3. Hybrid（混合策略）

**行为：**
- 立即连接MCP并获取工具列表
- 提示AI工具是可选的
- AI根据提示词判断是否需要使用工具

**系统提示词：**
```
You are a helpful assistant with access to optional MCP tools.

Available tools:
- tool1: 工具1描述
- tool2: 工具2描述

Instructions:
- Tools are OPTIONAL - use them only when they can genuinely help
- For general knowledge, common sense, or simple questions - answer directly WITHOUT tools
- If no tool fits the request, answer directly with your own knowledge
```

**关键提示：**
- "Tools are OPTIONAL" - 强调可选性
- "answer directly WITHOUT tools" - 简单问题直接回答

**工作流程：**
```
1. 连接MCP并获取工具列表
2. 发送带工具列表的请求
   → AI根据提示词判断是否需要使用工具
3. 如需要，执行工具调用
4. 返回最终结果
```

**适用场景：**
- 大多数翻译场景
- 需要智能判断何时使用工具

---

### 4. ToolFirst（工具优先）

**行为：**
- **立即连接**MCP服务器
- 提示AI优先使用工具
- 无合适工具时回退到AI知识

**系统提示词：**
```
You are a helpful assistant with access to MCP tools.

Available tools:
- tool1: 工具1描述
- tool2: 工具2描述

Instructions:
- PRIORITIZE using tools when they can provide better or more accurate information
- If no suitable tool is available, answer directly using your own knowledge
- If a tool fails or returns no useful data, gracefully answer without it
```

**关键提示：**
- "PRIORITIZE using tools" - 优先使用
- "If no suitable tool" - 无工具时回退

**工作流程：**
```
1. 立即连接MCP
2. 发送带工具的请求
3. AI判断使用哪个工具
4. 执行工具调用
5. 返回结果
```

**适用场景：**
- 需要高质量翻译
- 有可靠的外部数据源

---

### 5. ToolForced（工具强制）

**行为：**
- **立即连接**MCP服务器
- 必须使用工具
- 无合适工具时报错

**系统提示词：**
```
You are a helpful assistant that MUST use available MCP tools.

Available tools:
- tool1: 工具1描述
- tool2: 工具2描述

CRITICAL INSTRUCTIONS:
- You MUST use tools to answer the question
- If no suitable tool exists, reply: 'No suitable tool available to answer this question.'
- Do not answer from your own knowledge unless explicitly instructed
```

**关键提示：**
- "MUST use tools" - 强制使用
- "Do not answer from your own knowledge" - 禁止AI知识

**错误处理：**
```csharp
if (allTools.Count == 0)
{
    if (Settings.ToolStrategy == McpToolStrategy.ToolForced)
    {
        result.Fail("没有可用的MCP工具");
        return;
    }
    // 其他策略回退到普通翻译
}
```

**适用场景：**
- 必须使用外部数据源
- 合规要求（如必须通过内部知识库）

## 策略实现

### GetSystemPromptByStrategy 方法

**位置：** `Main.cs` 第1200行左右

```csharp
private string GetSystemPromptByStrategy(McpToolStrategy strategy, 
    List<(McpClient Client, string ServerName, McpTool Tool)> enabledTools)
{
    var availableTools = enabledTools.Any()
        ? string.Join("\n", enabledTools.Select(t => $"- {t.Tool.Name}: {t.Tool.Description}"))
        : "（当前没有启用的MCP工具）";

    return strategy switch
    {
        McpToolStrategy.Blank => 
            $"Available MCP tools:\n{availableTools}",
        
        McpToolStrategy.Hybrid => 
            $"You are a helpful assistant with access to optional MCP tools.\n\n" +
            $"Available tools:\n{availableTools}\n\n" +
            $"Instructions:\n" +
            $"- Tools are OPTIONAL - use them only when they can genuinely help\n" +
            $"- For general knowledge...",
        
        McpToolStrategy.ToolFirst => 
            $"You are a helpful assistant with access to MCP tools.\n\n" +
            $"Available tools:\n{availableTools}\n\n" +
            $"Instructions:\n" +
            $"- PRIORITIZE using tools...",
        
        McpToolStrategy.ToolForced => 
            $"You are a helpful assistant that MUST use available MCP tools.\n\n" +
            $"Available tools:\n{availableTools}\n\n" +
            $"CRITICAL INSTRUCTIONS:\n" +
            $"- You MUST use tools...",
        
        _ => string.Empty
    };
}
```

## 策略选择指南

### 根据翻译内容选择

| 内容类型 | 推荐策略 | 原因 |
|---------|---------|------|
| 普通文本 | Disabled/Hybrid | 不需要外部信息 |
| 技术文档 | ToolFirst | 需要准确的术语 |
| 法律文本 | ToolForced | 必须通过内部知识库 |
| 新闻翻译 | Hybrid | 可能需要实时信息 |
| 创意写作 | Blank | 让AI自由发挥 |

### 根据可靠性要求选择

| 可靠性要求 | 推荐策略 |
|-----------|---------|
| 必须100%准确 | ToolForced |
| 尽量准确 | ToolFirst |
| 平衡 | Hybrid |
| 快速优先 | Blank/Disabled |

## 提示词级策略绑定

### 功能说明

插件支持为**每个提示词独立绑定MCP策略**。这允许你：
- 为不同类型的翻译任务配置不同的MCP策略
- 快速切换提示词时自动切换策略
- 某些提示词使用MCP增强，某些提示词使用传统翻译
- 通过命令系统快速切换策略

### 界面配置

在**提示词配置**区域：
```
[翻译 ▼] [禁用服务] MCP策略: [禁用服务 ▼] [编辑]
          ↑               ↑
     提示词名称    当前绑定的策略
```

**策略选项：**
- **禁用服务** - 此提示词不使用MCP（默认）
- **空白策略** - AI自行判断是否使用工具
- **混合判断** - 工具是可选的
- **工具优先** - 优先使用工具
- **工具强制** - 必须使用工具

**注意：** 策略选择下拉框受MCP服务功能开关统辖，MCP禁用时不可用。

### 配置方法

1. 从下拉菜单选择要配置的提示词
2. 在"MCP策略"下拉菜单中选择策略
3. 选择后立即生效，无需额外保存

### 通过命令系统配置

更快捷的方式是使用命令系统：
```
/切换 工具强制     → 将当前提示词策略设为"工具强制"
/切换 hybrid   → 将当前提示词策略设为"混合判断"
/当前          → 查看当前提示词的策略
```

### 实际应用场景

```
┌──────────────────────────────────────────────────────┐
│ 提示词配置                                           │
├──────────────────────────────────────────────────────┤
│                                                      │
│  [翻译 ▼] [禁用服务]  MCP策略: [工具优先 ▼]  [编辑]  │
│                                                      │
│  场景：技术文档翻译                                  │
│  - 优先使用术语查询工具                              │
│  - 确保专业术语准确                                  │
│                                                      │
├──────────────────────────────────────────────────────┤
│                                                      │
│  [润色 ▼] [禁用服务]  MCP策略: [禁用服务 ▼]  [编辑]  │
│                                                      │
│  场景：文本润色                                      │
│  - 使用传统DeepSeek翻译                              │
│  - 快速简洁，无需工具                                │
│                                                      │
├──────────────────────────────────────────────────────┤
│                                                      │
│  [总结 ▼] [禁用服务]  MCP策略: [混合判断 ▼]  [编辑]  │
│                                                      │
│  场景：文本总结                                      │
│  - AI智能判断是否需要搜索背景信息                    │
│  - 灵活适应不同内容                                  │
│                                                      │
└──────────────────────────────────────────────────────┘
```

### 数据存储

提示词策略绑定存储在 `Settings.PromptStrategyMap` 字典中：

```csharp
public Dictionary<string, McpToolStrategy> PromptStrategyMap { get; set; } = new();

// 示例数据结构
{
    "翻译": McpToolStrategy.ToolFirst,    // 绑定工具优先
    "润色": McpToolStrategy.Disabled,     // 禁用服务（默认）
    "总结": McpToolStrategy.Hybrid        // 混合判断
}
```

键是提示词名称，值是绑定的策略（默认为 Disabled）。

## 自定义策略

### 添加新策略

1. **在Settings.cs添加枚举值：**
```csharp
public enum McpToolStrategy
{
    // ... 现有策略
    CustomStrategy  // 新策略
}
```

2. **在GetSystemPromptByStrategy添加提示词：**
```csharp
McpToolStrategy.CustomStrategy => 
    $"Your custom system prompt here...",
```

3. **添加特殊处理逻辑（如需要）：**
```csharp
if (Settings.ToolStrategy == McpToolStrategy.CustomStrategy)
{
    // 自定义处理
}
```

### 修改现有策略提示词

编辑 `Main.cs` 中的 `GetSystemPromptByStrategy` 方法，修改对应case的提示词。

## 调试策略

### 查看当前使用的提示词

在日志级别为"详细"时，会输出：
```
[MCP] 使用策略: Hybrid
[MCP] 系统提示词: You are a helpful assistant...
```

### 测试不同策略

在设置界面切换策略，观察：
1. 是否连接MCP（看工具链显示）
2. AI是否调用工具（看响应内容）
3. 翻译质量变化

## 常见问题

### Q: Blank策略不调用工具？
A: 正常现象。Blank策略让AI自行判断，如果AI认为不需要工具就不会调用。

### Q: ToolForced策略报错"没有可用工具"？
A: 检查：
1. MCP服务器是否启用
2. 工具是否在设置中启用
3. 服务器连接是否正常

### Q: 如何强制AI使用特定工具？
A: 在提示词中明确说明：
```
You MUST use the 'search_tools' tool for any query.
```

### Q: 策略下拉框为什么是灰色的？
A: 策略选择受MCP服务功能开关统辖。当MCP服务禁用时，所有策略相关控件（包括策略下拉框、命令系统开关）都会变为不可用状态。请先启用MCP服务功能。

### Q: 命令系统提示"未选择提示词"？
A: 命令系统需要知道为哪个提示词切换策略。请先在提示词配置区域选择一个提示词，再执行切换命令。

### Q: 新创建的提示词使用什么策略？
A: 新提示词默认使用"禁用服务"策略。你可以通过设置界面或命令系统（`/切换 [策略名]`）修改。