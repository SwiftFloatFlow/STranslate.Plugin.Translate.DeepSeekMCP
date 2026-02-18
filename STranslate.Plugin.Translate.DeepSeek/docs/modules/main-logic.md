# 主翻译逻辑（Main.cs）

## 文件位置
`Main.cs`

## 类结构

```csharp
public class Main : ITranslator, IDisposable
{
    private readonly List<McpClient> _mcpClients;  // MCP客户端列表
    
    // 核心方法
    public Task TranslateAsync(...);               // 翻译入口
    private Task TranslateWithMcpTools(...);       // MCP翻译
    private Task TranslateWithTraditionalApi(...); // 传统API翻译
}
```

## 翻译流程

### 1. 入口方法：TranslateAsync

```csharp
public async Task TranslateAsync(TranslateRequest request, ...)
{
    // 检查是否为命令（以/开头且MCP服务已启用）
    if (Settings.EnableMcp && !string.IsNullOrWhiteSpace(request.Text) 
        && request.Text.TrimStart().StartsWith("/"))
    {
        var commandResult = await ExecuteCommandAsync(request.Text.Trim());
        if (commandResult.IsCommand)
        {
            if (commandResult.Success)
            {
                result.Text = commandResult.Message;
            }
            else
            {
                result.Fail(commandResult.Message);
            }
            return;
        }
        // 如果不是有效命令，继续作为普通文本处理
    }

    // 检查是否启用MCP
    var effectiveStrategy = GetEffectiveStrategy();
    if (effectiveStrategy == McpToolStrategy.Disabled || !Settings.EnableMcp)
    {
        // 使用传统API
        await TranslateWithTraditionalApi(request, result, cancellationToken);
        return;
    }
    
    // 使用MCP工具翻译
    await TranslateWithMcpTools(request, result, effectiveStrategy, cancellationToken);
}
```

### 2. 命令系统处理流程

```
ExecuteCommandAsync
    │
    ├── 1. 分割命令和参数
    │   └── text.Split([' ', '\t'], 2)
    │
    ├── 2. 匹配命令类型
    │   ├── /now, /当前 → ExecuteNowCommand()
    │   ├── /status, /状态 → ExecuteStatusCommand()
    │   ├── /help, /帮助 → ExecuteHelpCommand()
    │   ├── /switch, /切换 → ExecuteSwitchCommand(argument)
    │   ├── /chain, /工具链 → ExecuteToggleToolChainCommand()
    │   ├── /result, /工具结果 → ExecuteToolResultCommand(argument)
    │   ├── /mcp → ExecuteToggleMcpCommand()
    │   └── 其他 → 返回"未知命令"错误
    │
    ├── 3. 执行命令
    │   └── 验证参数、更新设置、触发事件
    │
    └── 4. 返回 CommandResult
        ├── IsCommand: true（表示已处理为命令）
        ├── Success: true/false
        └── Message: 响应文本
```

### 3. MCP翻译流程：TranslateWithMcpTools

```
TranslateWithMcpTools
    │
    ├── 1. 初始化MCP客户端（InitializeMcpAndGetSystemPrompt）
    │   └── 为每个启用的服务器创建McpClient
    │
    ├── 2. 连接服务器并获取工具
    │   └── client.ConnectAsync() → client.ListToolsAsync()
    │
    ├── 3. 构建OpenAI格式请求
    │   ├── 系统提示词（根据策略生成）
    │   ├── 用户消息
    │   └── 工具列表（Function Calling格式）
    │
    ├── 4. 多轮对话循环
    │   │
    │   ├── 发送请求到DeepSeek API
    │   │
    │   ├── 判断响应类型
    │   │   ├── 如果是工具调用 → 执行工具
    │   │   │   └── client.CallToolAsync()
    │   │   │
    │   │   └── 如果是最终回复 → 结束循环
    │   │
    │   └── 将工具结果加入上下文，继续下一轮
    │
    └── 5. 返回最终翻译结果
```

## 关键方法详解

### ExecuteCommandAsync

**位置**：命令系统区域（约1350行后）

**功能**：解析并执行用户输入的命令

**命令处理流程**：
```csharp
private Task<CommandResult> ExecuteCommandAsync(string text)
{
    var parts = text.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
    var command = parts[0].ToLowerInvariant();
    var argument = parts.Length > 1 ? parts[1].Trim() : "";

    var result = command switch
    {
        "/now" or "/当前" => ExecuteNowCommand(),
        "/status" or "/状态" => ExecuteStatusCommand(),
        "/help" or "/帮助" => ExecuteHelpCommand(),
        "/switch" or "/切换" => ExecuteSwitchCommand(argument),
        _ => new CommandResult { IsCommand = true, Success = false, 
             Message = $"未知命令: {command}" }
    };

    return Task.FromResult(result);
}
```

**策略名称映射**：
```csharp
// 英文映射
["disabled"] = McpToolStrategy.Disabled
["blank"] = McpToolStrategy.Blank
["hybrid"] = McpToolStrategy.Hybrid
["toolfirst"] = McpToolStrategy.ToolFirst
["toolforced"] = McpToolStrategy.ToolForced

// 中文映射
["禁用"] = McpToolStrategy.Disabled
["空白"] = McpToolStrategy.Blank
["混合"] = McpToolStrategy.Hybrid
["优先"] = McpToolStrategy.ToolFirst
["强制"] = McpToolStrategy.ToolForced
```

### InitializeMcpAndGetSystemPrompt

**位置**：约1050行

**功能**：
- 创建所有MCP客户端
- 连接服务器
- 收集可用工具
- 生成系统提示词

**返回值**：
```csharp
(string systemPrompt, List<object> functionTools, List<(McpClient, string, McpTool)> enabledTools)
```

### LazyInitializeMcpAsync

**位置**：约1120行

**功能**：
- 延迟初始化（仅在AI决定调用工具时连接）
- 用于Blank和Hybrid策略

### GetSystemPromptByStrategy

**位置**：约1200行

**功能**：根据策略生成系统提示词

**策略提示词模板**：

| 策略 | 提示词特点 |
|------|-----------|
| `Blank` | 只列出可用工具，无额外说明 |
| `Hybrid` | 工具是可选的，让AI自行判断 |
| `ToolFirst` | 优先使用工具，无工具时回退 |
| `ToolForced` | 必须使用工具，否则报错 |

## 工具调用处理

### 工具链记录

```csharp
// 记录每个工具的调用结果
var toolChainList = new List<(string toolName, int count, bool? isSuccess)>();

// 成功调用 → ✅
// 失败调用 → ❌
// 进行中 → (无标记)
```

### 工具结果显示

```csharp
private string FormatToolChainItem((string toolName, int count, bool? isSuccess) item)
{
    var suffix = item.isSuccess switch
    {
        true => "✅",
        false => "❌",
        _ => ""
    };
    
    if (item.count > 1)
        return $"{item.toolName}×{item.count}{suffix}";
    return $"{item.toolName}{suffix}";
}
```

## 工具调用控制机制

### 已实现功能 ✅

#### 1. 总工具调用次数限制
- **实现**：`StrategyTotalToolCallsLimits` 字典
- **默认值**：15次（可在策略编辑对话框中调整）
- **行为**：达到上限后强制结束循环，返回当前结果

```csharp
// 检查是否达到总调用上限
if (toolCallCount >= maxToolCalls)
{
    result.Text = $"[警告] 达到最大工具调用次数限制 ({maxToolCalls})\n\n" + result.Text;
}
```

#### 2. 同一工具连续调用上限
- **实现**：`StrategyConsecutiveToolLimits` 字典
- **默认值**：5次（可在策略编辑对话框中调整）
- **行为**：达到上限后，为该工具添加虚拟失败响应，强制AI直接回答

```csharp
// 检查是否所有工具都达到连续调用上限
if (allToolsExceededLimit)
{
    // 为每个工具调用添加失败响应（标记为❎）
    allMessages.Add(new
    {
        role = "tool",
        content = $"[调用被截断] 工具 '{toolName}' 已达到连续调用上限...",
        tool_call_id = toolCallId
    });
    
    // 重置计数，继续下一轮让AI直接回答
    consecutiveToolCallCount[toolName] = 0;
    continue;
}
```

#### 3. 取消令牌传递
- **实现**：`cancellationToken` 已传递到所有 API 调用和工具调用
- **行为**：用户取消时立即终止操作

```csharp
while (!cancellationToken.IsCancellationRequested && toolCallCount < maxToolCalls)
{
    // 每次请求都传递 cancellationToken
    await Context.HttpService.StreamPostAsync(..., cancellationToken);
}
```

### 待实现优化
- [ ] **指数退避重试**：工具调用失败时的自动重试机制（任务 #4）
- [ ] **细粒度错误分类**：区分连接错误、参数错误、超时等（任务 #4）
- [ ] **独立超时控制**：每个工具调用的超时设置

## 错误处理

### 连接失败处理

```csharp
// ToolForced策略：报错
if (Settings.ToolStrategy == McpToolStrategy.ToolForced)
{
    result.Fail("没有可用的MCP服务器");
    return;
}

// 其他策略：回退到传统翻译
await TranslateWithTraditionalApi(request, result, cancellationToken);
```

### 工具调用失败

```csharp
try
{
    var toolResult = await client.CallToolAsync(toolName, arguments);
    UpdateToolChain(toolName, true);  // ✅
}
catch (Exception ex)
{
    UpdateToolChain(toolName, false); // ❌
    // 继续处理其他工具或回退
}
```

## 修改建议

### 添加新的翻译策略

1. 在 `Settings.cs` 的 `McpToolStrategy` 枚举添加新值
2. 在 `GetSystemPromptByStrategy()` 添加新分支
3. 更新 `InitializeMcpAndGetSystemPrompt()` 中的策略判断

### 修改工具链显示格式

1. 修改 `FormatToolChainItem()` 方法
2. 调整 `TranslateWithMcpTools()` 中的显示逻辑

### 添加新的MCP功能

1. 在 `McpClient.cs` 添加新方法
2. 在 `Main.cs` 的翻译流程中调用

## 调试技巧

### 启用详细日志

在设置界面选择"日志级别：详细"，将输出：
- MCP连接状态
- 工具调用详情
- API请求/响应

### 检查点

1. **MCP初始化**：检查 `_mcpClients` 是否创建成功
2. **工具列表**：检查 `allTools` 是否包含预期工具
3. **API响应**：检查 `finish_reason` 是否为 `tool_calls`
4. **工具调用**：检查 `toolCallCount` 是否达到预期

## 常见修改场景

### 场景1：添加新的命令

```csharp
// 在 ExecuteCommandAsync 中添加新命令分支
var result = command switch
{
    // ... 现有命令
    "/newcmd" or "/新命令" => ExecuteNewCommand(argument),
    _ => new CommandResult { ... }
};

// 实现命令处理函数
private CommandResult ExecuteNewCommand(string argument)
{
    // 实现命令逻辑
    return new CommandResult 
    { 
        IsCommand = true, 
        Success = true, 
        Message = "命令执行结果" 
    };
}
```

### 场景2：修改最大工具调用次数

```csharp
// 第500行附近
int maxToolCalls = enabledServers.FirstOrDefault()?.MaxToolCalls > 0 
    ? enabledServers.First().MaxToolCalls 
    : 10;  // 修改默认值
```

### 场景3：修改超时时间

```csharp
// McpClient.cs 中
await client.ConnectAsync(cancellationToken);
// 添加超时参数
```

### 场景4：添加工具结果缓存

```csharp
// 在 Main.cs 添加缓存字典
private readonly Dictionary<string, object> _toolResultCache = new();

// 在工具调用前检查缓存
if (_toolResultCache.TryGetValue(cacheKey, out var cachedResult))
{
    return cachedResult;
}
```