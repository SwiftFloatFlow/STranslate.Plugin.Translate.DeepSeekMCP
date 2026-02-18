# MCP客户端（McpClient.cs）

## 文件位置
`McpClient.cs`

## 实现状态总览

### ✅ 已实现功能
- [x] HTTP/2 连接支持
- [x] 完整的 MCP 协议实现（initialize、tools/list、tools/call）
- [x] Bearer Token 认证
- [x] 异步操作和取消令牌支持
- [x] 基础错误处理和日志记录
- [x] `IDisposable` 接口（基础实现）

### 🚧 待实现优化（高优先级）
- [ ] **连接池**（任务 #1）：避免每次翻译重新连接
- [ ] **线程安全**（任务 #2）：确保并发调用安全
- [ ] **完善资源释放**（任务 #3）：正确释放 HttpClient
- [ ] **细化错误处理**（任务 #4）：错误分类和重试机制
- [ ] **工具列表缓存**（任务 #6）：带 TTL 的缓存

详见 [下一阶段实现任务](../下一阶段实现任务.md)

## 类概述

```csharp
public class McpClient : IDisposable
{
    private readonly string _serverUrl;
    private readonly string _apiKey;
    private readonly ILogger _logger;
    
    // 核心方法
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken);
    public async Task<List<McpTool>> ListToolsAsync(CancellationToken cancellationToken);
    public async Task<JsonNode> CallToolAsync(string toolName, JsonNode arguments, CancellationToken cancellationToken);
}
```

## 职责

- 管理单个MCP服务器的连接
- 发现和调用服务器上的工具
- 处理连接生命周期

## MCP协议说明

MCP（Model Context Protocol）是标准化的AI工具调用协议，基于JSON-RPC 2.0。

### 连接流程

```
1. 发送 initialize 请求
   → 接收服务器能力信息
   
2. 发送 initialized 通知
   → 连接建立完成
   
3. 发送 tools/list 请求
   → 接收可用工具列表
```

### 消息格式

**Initialize请求：**
```json
{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
        "protocolVersion": "2024-11-05",
        "capabilities": {},
        "clientInfo": {
            "name": "STranslate.DeepSeek",
            "version": "1.0.0"
        }
    }
}
```

**Tools/List响应：**
```json
{
    "jsonrpc": "2.0",
    "id": 2,
    "result": {
        "tools": [
            {
                "name": "search_tools",
                "description": "搜索可用工具",
                "inputSchema": {
                    "type": "object",
                    "properties": {
                        "query": {"type": "string"}
                    },
                    "required": ["query"]
                }
            }
        ]
    }
}
```

**Tools/Call请求：**
```json
{
    "jsonrpc": "2.0",
    "id": 3,
    "method": "tools/call",
    "params": {
        "name": "search_tools",
        "arguments": {
            "query": "天气查询"
        }
    }
}
```

## 核心方法详解

### ConnectAsync

**签名：**
```csharp
public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
```

**流程：**
```csharp
public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
{
    try
    {
        // 1. 发送initialize请求
        var initRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            // ...
        };
        
        var response = await PostAsync(initRequest, cancellationToken);
        
        // 2. 发送initialized通知
        var initNotify = new
        {
            jsonrpc = "2.0",
            method = "notifications/initialized"
        };
        await PostAsync(initNotify, cancellationToken);
        
        _isConnected = true;
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError($"连接MCP服务器失败: {ex.Message}");
        return false;
    }
}
```

### ListToolsAsync

**签名：**
```csharp
public async Task<List<McpTool>> ListToolsAsync(CancellationToken cancellationToken)
```

**功能：**
- 获取服务器上所有可用工具
- 包含工具名称、描述、输入参数模式

**返回：**
```csharp
public class McpTool
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string InputSchema { get; set; }  // JSON Schema
}
```

### CallToolAsync

**签名：**
```csharp
public async Task<JsonNode> CallToolAsync(
    string toolName, 
    JsonNode arguments, 
    CancellationToken cancellationToken)
```

**流程：**
```csharp
var request = new
{
    jsonrpc = "2.0",
    id = GetNextRequestId(),
    method = "tools/call",
    @params = new
    {
        name = toolName,
        arguments = arguments
    }
};

var response = await PostAsync(request, cancellationToken);
return response["result"];
```

## 错误处理

### 连接错误

```csharp
try
{
    var connected = await client.ConnectAsync(cancellationToken);
    if (!connected)
    {
        // 连接失败，记录日志
        _logger.LogWarning("MCP服务器连接失败，跳过此服务器");
        continue;  // 尝试下一个服务器
    }
}
catch (OperationCanceledException)
{
    throw;  // 用户取消，向上传播
}
catch (Exception ex)
{
    _logger.LogError($"连接异常: {ex.Message}");
    // 继续处理其他服务器
}
```

### 工具调用错误

```csharp
try
{
    var result = await client.CallToolAsync(toolName, args, cancellationToken);
}
catch (McpException ex) when (ex.ErrorCode == -32601)
{
    // 工具不存在
    _logger.LogError($"工具 {toolName} 不存在");
}
catch (McpException ex) when (ex.ErrorCode == -32602)
{
    // 参数错误
    _logger.LogError($"工具 {toolName} 参数错误: {ex.Message}");
}
```

## 生命周期管理

### 创建

```csharp
// 在Main.cs中创建
var client = new McpClient(server.Url, server.ApiKey, logger, logLevel);
_mcpClients.Add(client);
```

### 连接

```csharp
// 首次使用时连接
if (!await client.ConnectAsync(cancellationToken))
{
    // 连接失败处理
}
```

### 释放

```csharp
// 在Main.Dispose中释放
public void Dispose()
{
    foreach (var client in _mcpClients)
    {
        client.Dispose();
    }
    _mcpClients.Clear();
}
```

## 修改建议

### 添加连接池支持

```csharp
public class McpClientPool
{
    private readonly ConcurrentDictionary<string, McpClient> _clients = new();
    
    public McpClient GetClient(string serverUrl)
    {
        return _clients.GetOrAdd(serverUrl, url => 
            new McpClient(url, _apiKey, _logger, _logLevel));
    }
}
```

### 添加重试机制

```csharp
public async Task<JsonNode> CallToolWithRetryAsync(
    string toolName, 
    JsonNode arguments, 
    int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await CallToolAsync(toolName, arguments, CancellationToken.None);
        }
        catch (Exception ex) when (i < maxRetries - 1)
        {
            _logger.LogWarning($"工具调用失败，重试 {i + 1}/{maxRetries}: {ex.Message}");
            await Task.Delay(1000 * (i + 1));  // 指数退避
        }
    }
    throw new Exception("工具调用多次失败");
}
```

### 添加连接保活

```csharp
private Timer _keepAliveTimer;

public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
{
    var result = await ConnectInternalAsync(cancellationToken);
    
    if (result)
    {
        // 每30秒发送ping
        _keepAliveTimer = new Timer(async _ =>
        {
            try
            {
                await PingAsync();
            }
            catch
            {
                // 重连逻辑
            }
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }
    
    return result;
}
```

## 调试技巧

### 记录所有MCP通信

```csharp
private async Task<JsonNode> PostAsync(object request, CancellationToken cancellationToken)
{
    var json = JsonConvert.SerializeObject(request);
    _logger.LogDebug($"MCP请求: {json}");
    
    var response = await _httpClient.PostAsync(...);
    var responseJson = await response.Content.ReadAsStringAsync();
    _logger.LogDebug($"MCP响应: {responseJson}");
    
    return JsonNode.Parse(responseJson);
}
```

### 测试MCP服务器

使用curl测试：
```bash
# Initialize
curl -X POST http://localhost:3000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "2024-11-05",
      "capabilities": {},
      "clientInfo": {"name": "test", "version": "1.0"}
    }
  }'

# List tools
curl -X POST http://localhost:3000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list"
  }'
```

## 常见问题

### Q: 连接成功但获取不到工具？
A: 检查：
1. 服务器是否实现了tools/list方法
2. 响应格式是否符合MCP规范
3. 是否发送了initialized通知

### Q: 工具调用返回错误？
A: 检查：
1. 工具名称是否正确
2. 参数是否符合inputSchema
3. 参数类型是否匹配

### Q: 长时间运行后连接断开？
A: 建议：
1. 添加连接保活机制
2. 在断开时自动重连
3. 添加连接状态检查