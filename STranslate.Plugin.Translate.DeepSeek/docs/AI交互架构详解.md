# DeepSeek MCP 插件 - AI交互架构详解

## 目录

1. [架构概览](#架构概览)
2. [核心组件](#核心组件)
3. [MCP工具调用流程](#mcp工具调用流程)
4. [AI交互完整流程](#ai交互完整流程)
5. [数据流转详解](#数据流转详解)
6. [错误处理机制](#错误处理机制)
7. [性能优化策略](#性能优化策略)
8. [配置与扩展](#配置与扩展)

---

## 架构概览

### 1.1 整体架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                        STranslate 主程序                         │
│                                                                  │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐     │
│  │   用户界面    │    │  翻译请求    │    │  结果展示    │     │
│  └──────┬───────┘    └──────┬───────┘    └──────┬───────┘     │
│         │                   │                   │              │
└─────────┼───────────────────┼───────────────────┼──────────────┘
          │                   │                   │
          ▼                   ▼                   ▼
┌─────────────────────────────────────────────────────────────────┐
│                    DeepSeek MCP 插件                             │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                    Main.cs (主控制器)                     │  │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐       │  │
│  │  │ 命令系统     │ │  翻译逻辑   │ │ 策略管理    │       │  │
│  │  └─────────────┘ └─────────────┘ └─────────────┘       │  │
│  └──────────────────────────────────────────────────────────┘  │
│                              │                                   │
│         ┌────────────────────┼────────────────────┐            │
│         ▼                    ▼                    ▼            │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐       │
│  │  连接管理     │   │  工具执行     │   │  内容构建    │       │
│  │ McpClientPool│   │ ToolExecutor │   │ThreeStage    │       │
│  └──────────────┘   └──────────────┘   └──────────────┘       │
│         │                   │                   │              │
│         └───────────────────┼───────────────────┘              │
│                             ▼                                  │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                     MCP 客户端层                         │  │
│  │        SdkMcpClient (官方SDK实现)                        │  │
│  └──────────────────────────────────────────────────────────┘  │
│                             │                                   │
│                             ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                     MCP 服务器                            │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 核心设计原则

1. **分层架构**：清晰的职责分离，便于维护和测试
2. **连接复用**：通过连接池减少连接开销
3. **并发控制**：限制并发请求防止资源耗尽
4. **错误容错**：完善的错误处理和重试机制
5. **策略灵活**：支持多种MCP使用策略
6. **配置迁移**：向后兼容，平滑升级

---

## 核心组件

### 2.1 Main.cs - 主控制器

**职责**：
- 翻译请求的入口和分发
- 命令系统的处理
- MCP客户端的生命周期管理
- 策略选择和执行

**核心方法**：

```csharp
public class Main : LlmTranslatePluginBase
{
    // 1. 翻译入口
    public override async Task TranslateAsync(...)
    
    // 2. 命令执行
    private Task<CommandResult> ExecuteCommandAsync(string text)
    
    // 3. MCP翻译（带工具调用）
    private async Task TranslateWithMcpTools(...)
    
    // 4. 传统API翻译（回退）
    private async Task TranslateWithTraditionalApi(...)
}
```

### 2.2 McpClientPool - 连接池

**职责**：
- 管理MCP客户端的生命周期
- 实现连接复用
- 空闲连接清理
- 连接重试

**核心机制**：

```csharp
public class McpClientPool : IDisposable
{
    // 客户端缓存
    private readonly ConcurrentDictionary<string, PooledMcpClient> _clients;
    
    // 获取客户端（复用或创建）
    public async Task<IMcpClient> GetClientAsync(...)
    {
        // 1. 尝试从缓存获取
        // 2. 检查有效性
        // 3. 创建新连接（带重试）
        // 4. 加入缓存
    }
    
    // 定期清理（每30秒）
    private void CleanupIdleClients()
    {
        // 清理超过5分钟未使用的连接
    }
}
```

### 2.3 SdkMcpClient - MCP客户端

**职责**：
- MCP协议实现（initialize、tools/list、tools/call）
- HTTP/2通信
- 工具列表缓存
- 资源释放管理

**协议流程**：

```
连接流程：
1. Send initialize request
2. Receive server capabilities
3. Send initialized notification
4. Connection established

工具调用流程：
1. Send tools/list request
2. Receive available tools
3. Send tools/call request
4. Receive tool result
```

### 2.4 ThreeStageContentBuilder - 三阶段内容构建器

**职责**：
- 实现三阶段流式显示
- 内联工具标记
- 工具结果展示

**三个阶段**：

1. **LLM前置流**：AI的初始回复（包含工具调用标记）
2. **MCP工具流**：工具执行过程和结果
3. **LLM后置流**：AI的最终回复

```
显示示例：
用户问：北京今天天气如何？

【阶段1 - AI分析】
我需要查询北京的天气信息。「weather_search✅」

【阶段2 - 工具执行】
┌─ 工具结果 ───────────────────┐
│ 天气：晴天                    │
│ 温度：25°C                    │
│ 湿度：45%                     │
└──────────────────────────────┘

【阶段3 - AI回复】
北京今天天气晴朗，气温25°C，湿度45%，适合外出活动。
```

### 2.5 ConcurrentToolExecutor - 并发工具执行器

**职责**：
- 并发执行多个工具调用
- 限制最大并发数（默认5个）
- 收集执行结果
- 错误处理

**执行流程**：

```csharp
public async Task<List<ToolCallResult>> ExecuteConcurrentAsync(...)
{
    // 1. 创建信号量限制并发
    var semaphore = new SemaphoreSlim(_maxConcurrent);
    
    // 2. 为每个工具创建任务
    var tasks = toolCalls.Select(async toolCall =>
    {
        await semaphore.WaitAsync();
        try
        {
            // 3. 执行工具调用
            return await CallToolWithRetryAsync(...);
        }
        finally
        {
            semaphore.Release();
        }
    });
    
    // 4. 等待所有完成
    return await Task.WhenAll(tasks);
}
```

---

## MCP工具调用流程

### 3.1 工具调用完整流程图

```
用户输入
    │
    ▼
┌──────────────────────────────────┐
│ 1. 策略判断                       │
│    - 根据提示词获取绑定策略        │
│    - 策略：Blank/Hybrid/ToolFirst/│
│      ToolForced/Disabled          │
└──────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────┐
│ 2. 初始化MCP                     │
│    - 从连接池获取客户端           │
│    - 复用现有连接或新建连接        │
│    - 获取工具列表（带缓存）        │
└──────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────┐
│ 3. 构建请求                       │
│    - 生成系统提示词（含工具描述）  │
│    - 构建消息历史                 │
│    - 添加用户消息                 │
└──────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────┐
│ 4. 多轮对话循环                   │
│    ┌───────────────────────────┐ │
│    │ while (需要更多工具调用)   │ │
│    │   4.1 发送流式请求        │ │
│    │   4.2 接收AI响应          │ │
│    │   4.3 判断是否调用工具    │ │
│    │   4.4 执行工具（并发）    │ │
│    │   4.5 添加工具结果到历史  │ │
│    └───────────────────────────┘ │
└──────────────────────────────────┘
    │
    ▼
┌──────────────────────────────────┐
│ 5. 返回最终结果                   │
│    - 构建最终回复                 │
│    - 添加工具链显示               │
│    - 格式化输出                   │
└──────────────────────────────────┘
```

### 3.2 工具调用详细步骤

#### 步骤1：策略判断

```csharp
private McpToolStrategy GetEffectiveStrategy()
{
    // 第1层：检查全局MCP开关
    if (!Settings.EnableMcp)
        return McpToolStrategy.Disabled;
    
    // 第2层：获取提示词绑定的策略
    var currentPrompt = SelectedPrompt;
    if (currentPrompt == null)
        return McpToolStrategy.Disabled;
    
    return Settings.PromptStrategyMap.TryGetValue(
        currentPrompt.Name, out var strategy) 
        ? strategy : McpToolStrategy.Disabled;
}
```

**策略说明**：

| 策略 | 说明 | 适用场景 |
|------|------|----------|
| **Disabled** | 禁用MCP，使用普通翻译 | 简单翻译，不需要工具 |
| **Blank** | 列出工具，AI自行决定 | 通用场景 |
| **Hybrid** | 工具可选，AI判断使用 | 复杂问题，可能需工具辅助 |
| **ToolFirst** | 优先使用工具 | 查询类任务 |
| **ToolForced** | 必须使用工具 | 强制工具调用场景 |

#### 步骤2：连接池获取客户端

```csharp
// 连接池自动复用或创建连接
var client = await _clientPool.GetClientAsync(serverConfig, cancellationToken);

// 内部逻辑：
// 1. 检查是否有可用连接
// 2. 检查连接是否有效（未断开）
// 3. 复用现有连接或创建新连接
// 4. 新连接自动执行initialize流程
// 5. 更新最后使用时间
```

#### 步骤3：工具列表获取（带缓存）

```csharp
// 第一次调用：从服务器获取
var tools = await client.ListToolsAsync(cancellationToken);
// 内部自动缓存5分钟

// 5分钟内再次调用：使用缓存
var tools = await client.ListToolsAsync(cancellationToken);
// 直接返回缓存结果，无网络请求

// 强制刷新：清除缓存
var tools = await client.RefreshToolsAsync(cancellationToken);
```

#### 步骤4：多轮对话循环

```csharp
while (needMoreToolCalls && toolCallCount < maxToolCalls)
{
    // 1. 发送请求到DeepSeek API
    var response = await SendStreamingRequestAsync(allMessages);
    
    // 2. 流式接收响应
    await foreach (var chunk in response)
    {
        // 实时显示AI回复
        threeStageBuilder.AppendContent(chunk.Content);
    }
    
    // 3. 检查是否包含工具调用
    if (response.HasToolCalls)
    {
        // 4. 并发执行工具
        var results = await ExecuteToolsConcurrentAsync(
            response.ToolCalls, 
            enabledServers);
        
        // 5. 添加工具结果到对话历史
        foreach (var result in results)
        {
            allMessages.Add(new
            {
                role = "tool",
                content = result.Content,
                tool_call_id = result.ToolCallId
            });
        }
        
        toolCallCount += results.Count;
    }
    else
    {
        // AI不再调用工具，结束循环
        needMoreToolCalls = false;
    }
}
```

#### 步骤5：工具执行（带重试）

```csharp
private async Task<string> CallToolWithRetryAsync(...)
{
    return await GlobalRetryPolicy.ToolCall.ExecuteAsync(
        async ct =>
        {
            // 调用MCP工具
            var result = await client.CallToolAsync(
                toolName, 
                arguments, 
                ct);
            
            // 检查是否成功
            if (string.IsNullOrEmpty(result))
                throw new McpToolCallException(
                    "工具返回空结果",
                    McpErrorType.Unknown,
                    serverName,
                    toolName);
            
            return result;
        },
        $"调用工具 {toolName}",
        serverName,
        toolName,
        cancellationToken);
}

// 重试策略：
// - 最大重试2次
// - 指数退避延迟（0.5s, 1s）
// - 总超时30秒
```

---

## AI交互完整流程

### 4.1 用户输入到结果返回

```
用户输入: "翻译这段文本：Hello World"
    │
    ▼
┌────────────────────────────────────────┐
│ TranslateAsync (翻译入口)               │
│ 1. 检查是否为命令（以/开头）            │
│ 2. 如果不是命令，继续翻译流程           │
└────────────────────────────────────────┘
    │
    ▼
┌────────────────────────────────────────┐
│ GetEffectiveStrategy (获取有效策略)     │
│ 1. 检查全局MCP开关                      │
│ 2. 获取当前提示词绑定的策略             │
│ 结果: Hybrid (混合判断)                 │
└────────────────────────────────────────┘
    │
    ▼
┌────────────────────────────────────────┐
│ TranslateWithMcpTools (MCP翻译)         │
│                                          │
│ 阶段1: 初始化                            │
│ - 从连接池获取MCP客户端                  │
│ - 获取工具列表（使用缓存）               │
│ - 构建系统提示词                         │
│                                          │
│ 阶段2: 第一轮对话                        │
│ - 发送用户消息到DeepSeek                 │
│ - AI分析：识别为简单翻译                 │
│ - AI决定：不调用工具，直接翻译           │
│                                          │
│ 阶段3: 返回结果                          │
│ - AI回复："你好，世界"                   │
│ - 无工具调用，直接返回                   │
└────────────────────────────────────────┘
    │
    ▼
用户看到结果: "你好，世界"
```

### 4.2 带工具调用的复杂示例

```
用户输入: "查询北京今天天气"
    │
    ▼
[策略判断] → ToolFirst (工具优先)
    │
    ▼
[初始化MCP]
- 从连接池获取客户端
- 获取工具列表（3个工具）
- 系统提示词包含工具描述
    │
    ▼
[第一轮对话]
AI分析：需要查询天气，决定调用工具
AI响应：包含工具调用「weather_search✅」
    │
    ▼
[工具执行]
- 并发执行 weather_search
- 参数：{ "city": "北京" }
- 返回：{ "weather": "晴", "temp": "25°C" }
    │
    ▼
[第二轮对话]
AI收到工具结果
AI响应："北京今天天气晴朗，气温25°C..."
    │
    ▼
[返回结果]
显示：北京今天天气晴朗，气温25°C
工具链：weather_search✅
```

---

## 数据流转详解

### 5.1 消息格式转换

**OpenAI API 格式**：

```json
{
  "model": "deepseek-chat",
  "messages": [
    {
      "role": "system",
      "content": "You are a helpful assistant..."
    },
    {
      "role": "user", 
      "content": "查询北京天气"
    }
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "weather_search",
        "description": "查询天气信息",
        "parameters": {...}
      }
    }
  ],
  "tool_choice": "auto",
  "stream": true
}
```

**MCP 协议格式**：

```json
// tools/list 请求
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/list",
  "params": {}
}

// tools/call 请求
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "weather_search",
    "arguments": {
      "city": "北京"
    }
  }
}
```

### 5.2 对话历史维护

```csharp
// 对话历史结构
var allMessages = new List<object>
{
    // 1. 系统提示词
    new { role = "system", content = systemPrompt },
    
    // 2. 用户消息
    new { role = "user", content = "查询北京天气" },
    
    // 3. AI回复（包含工具调用）
    new 
    { 
        role = "assistant", 
        content = "",
        tool_calls = new[]
        {
            new 
            {
                id = "call_1",
                type = "function",
                function = new 
                {
                    name = "weather_search",
                    arguments = "{\"city\": \"北京\"}"
                }
            }
        }
    },
    
    // 4. 工具结果
    new 
    {
        role = "tool",
        content = "{\"weather\": \"晴\", \"temp\": \"25°C\"}",
        tool_call_id = "call_1"
    },
    
    // 5. AI最终回复
    new { role = "assistant", content = "北京今天天气晴朗，气温25°C..." }
};
```

---

## 错误处理机制

### 6.1 异常分类体系

```
McpException (基类)
    ├── McpConnectionException      # 连接错误（可重试）
    ├── McpAuthenticationException  # 认证错误（不重试）
    ├── McpToolCallException        # 工具调用错误
    ├── McpTimeoutException         # 超时错误（可重试）
    └── McpRetryExhaustedException  # 重试耗尽错误
```

### 6.2 错误处理流程

```
工具调用失败
    │
    ▼
┌──────────────────────────────┐
│ 识别错误类型                   │
│ - 连接错误 → 可重试          │
│ - 认证错误 → 不重试          │
│ - 参数错误 → 不重试          │
│ - 超时 → 可重试              │
└──────────────────────────────┘
    │
    ▼
┌──────────────────────────────┐
│ 重试策略                       │
│ - 最大重试3次                │
│ - 指数退避延迟               │
│ - 超时30秒                   │
└──────────────────────────────┘
    │
    ▼
┌──────────────────────────────┐
│ 重试成功？                     │
│ 是 → 返回结果                │
│ 否 → 继续                    │
└──────────────────────────────┘
    │
    ▼
┌──────────────────────────────┐
│ 重试耗尽                       │
│ - 标记工具调用失败           │
│ - 返回错误信息给AI           │
│ - AI决定如何继续             │
└──────────────────────────────┘
```

### 6.3 降级策略

| 场景 | 处理策略 |
|------|----------|
| MCP服务禁用 | 使用传统API翻译 |
| 无可用工具 | 根据策略：报错或回退 |
| 所有工具失败 | 返回错误信息，让AI直接回答 |
| 达到调用上限 | 强制结束，返回当前结果 |

---

## 性能优化策略

### 7.1 连接池优化

```
优化前：每次翻译新建连接
优化后：连接池复用

效果：
- 连接建立时间：~500ms → ~0ms（复用）
- 并发连接数：N×翻译数 → N×服务器数
- 端口占用：高 → 低
```

### 7.2 工具列表缓存

```
缓存策略：
- TTL：5分钟
- 缓存粒度：每个服务器独立缓存
- 刷新方式：手动刷新或超时自动刷新

效果：
- 减少80%的工具列表请求
- 显著降低服务器负载
```

### 7.3 并发控制

```
限制：
- 最大并发翻译请求：5个
- 最大并发工具调用：5个
- 连接池大小：10个/服务器

效果：
- 防止资源耗尽
- 保护MCP服务器
- 避免触发API限流
```

### 7.4 流式响应

```
传统方式：等待完整响应 → 显示
流式方式：实时接收 chunks → 实时显示

优势：
- 首字节时间：大幅降低
- 用户体验：实时看到AI思考过程
- 内存占用：低（无需缓存完整响应）
```

---

## 配置与扩展

### 8.1 配置项说明

```csharp
public class Settings
{
    // 基础配置
    public string ApiKey { get; set; }           // DeepSeek API密钥
    public string Model { get; set; }            // 模型名称
    public int MaxTokens { get; set; }           // 最大token数
    public double Temperature { get; set; } }    // 温度参数
    
    // MCP配置
    public bool EnableMcp { get; set; }          // MCP总开关
    public int LogLevel { get; set; }            // 日志级别
    public List<McpServerConfig> McpServers { get; set; }  // 服务器列表
    
    // 策略配置
    public Dictionary<string, McpToolStrategy> PromptStrategyMap { get; set; }
    public Dictionary<McpToolStrategy, string> CustomStrategyPrompts { get; set; }
    public Dictionary<McpToolStrategy, int> StrategyConsecutiveToolLimits { get; set; }
    public Dictionary<McpToolStrategy, int> StrategyTotalToolCallsLimits { get; set; }
    public Dictionary<McpToolStrategy, ToolResultDisplayMode> StrategyToolResultDisplayModes { get; set; }
    public Dictionary<McpToolStrategy, bool> StrategyToolChainDisplay { get; set; }
}
```

### 8.2 扩展点

1. **添加新的MCP策略**：
   - 在 `McpToolStrategy` 枚举中添加新策略
   - 在 `GetSystemPromptByStrategy` 中实现提示词
   - 在策略编辑对话框中添加选项

2. **自定义重试策略**：
   - 修改 `GlobalRetryPolicy` 中的配置
   - 或创建新的 `RetryPolicy` 实例

3. **添加新的错误处理**：
   - 继承 `McpException` 创建新的异常类型
   - 在 `ClassifyHttpException` 中添加分类逻辑

---

## 总结

DeepSeek MCP插件采用了分层、可扩展的架构设计：

1. **清晰的职责分离**：连接管理、工具执行、内容构建各司其职
2. **高效的资源利用**：连接池、缓存、并发控制三重优化
3. **健壮的容错机制**：多级重试、错误分类、降级策略
4. **灵活的配置体系**：策略级配置、版本迁移、向后兼容

这种架构既保证了高性能和稳定性，又提供了良好的可扩展性，可以方便地添加新功能和适配新场景。

---

*文档版本：v1.0*  
*最后更新：2026年2月16日*
