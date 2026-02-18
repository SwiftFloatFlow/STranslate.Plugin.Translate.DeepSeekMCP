using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace STranslate.Plugin.Translate.DeepSeek;

/// <summary>
/// 基于官方 ModelContextProtocol SDK 的 MCP 客户端
/// 支持 Streamable HTTP、HTTP/2、连接复用
/// </summary>
public class SdkMcpClient : IMcpClient, IDisposable
{
    private readonly McpServerConfig _config;
    private readonly ILogger? _logger;
    private readonly int _logLevel;
    private readonly HttpClient _httpClient;
    private bool _isConnected;
    private string? _sessionId;
    private const string ProtocolVersion = "2025-11-25";
    
    // 资源释放标志
    private bool _disposed;
    private readonly object _disposeLock = new object();
    
    // 工具列表缓存
    private List<McpTool>? _cachedTools;
    private DateTime _toolsCacheTime;
    private readonly TimeSpan _toolsCacheTtl = TimeSpan.FromMinutes(5);

    public McpServerConfig ServerConfig => _config;
    
    public bool IsConnected => _isConnected;

    public SdkMcpClient(McpServerConfig config, ILogger? logger = null, int logLevel = 1)
    {
        _config = config;
        _logger = logger;
        _logLevel = logLevel;
        
        // 配置HTTP/2连接池
        var handler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            MaxConnectionsPerServer = 10
        };
        
        _httpClient = new HttpClient(handler)
        {
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        
        if (ShouldLog(1))
            _logger?.LogInformation("[MCP] SDK客户端初始化完成，服务器: {ServerName}, HTTP/2: 已启用", config.Name);
    }

    /// <summary>
    /// 连接到MCP服务器
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        // 检查是否已释放
        if (_disposed)
            throw new ObjectDisposedException(nameof(SdkMcpClient), "MCP客户端已释放，无法连接");
            
        try
        {
            if (ShouldLog(1))
                _logger?.LogInformation("[MCP] 开始连接服务器: {ServerName}", _config.Name);

            // 设置请求头
            _httpClient.DefaultRequestHeaders.Add("MCP-Protocol-Version", ProtocolVersion);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
            
            // 配置认证 - 支持多种格式
            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                var apiKey = _config.ApiKey.Trim();
                
                // 格式1: "Authorization=Bearer xxx" 或 "Authorization=xxx" (UI提示的格式)
                if (apiKey.StartsWith("Authorization=", StringComparison.OrdinalIgnoreCase))
                {
                    var authValue = apiKey.Substring("Authorization=".Length).Trim();
                    
                    // 如果值以 Bearer 开头，保持完整格式
                    if (authValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authValue);
                        
                        if (ShouldLog(2))
                            _logger?.LogDebug("[MCP] 已配置Bearer认证 (Authorization=Bearer xxx 格式)");
                    }
                    else
                    {
                        // 如果不是Bearer开头，添加Bearer前缀
                        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {authValue}");
                        
                        if (ShouldLog(2))
                            _logger?.LogDebug("[MCP] 已配置Bearer认证 (Authorization=xxx 格式，自动添加Bearer)");
                    }
                }
                // 格式2: 纯 "Bearer xxx" 格式
                else if (apiKey.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    // 保持完整的 Bearer 格式
                    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", apiKey);
                    
                    if (ShouldLog(2))
                        _logger?.LogDebug("[MCP] 已配置Bearer认证 (Bearer xxx 格式)");
                }
                // 格式3: "HeaderName: Value" 格式（其他自定义认证头）
                else if (apiKey.Contains(':'))
                {
                    var parts = apiKey.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(parts[0].Trim(), parts[1].Trim());
                        
                        if (ShouldLog(2))
                            _logger?.LogDebug("[MCP] 已配置自定义认证头: {Header}", parts[0].Trim());
                    }
                }
                // 格式4: 纯 token，自动生成 Bearer
                else
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    
                    if (ShouldLog(2))
                    {
                        var displayToken = apiKey.Length > 10 ? apiKey[..10] + "..." : apiKey;
                        _logger?.LogDebug("[MCP] 已配置Bearer认证 (纯token，自动生成Bearer)，Token预览: {Token}", displayToken);
                    }
                }
            }

            // 发送初始化请求
            var initRequest = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = ProtocolVersion,
                    capabilities = new
                    {
                        tools = new { listChanged = true }
                    },
                    clientInfo = new
                    {
                        name = "DeepSeekMCP",
                        version = "2.0.0"
                    }
                }
            };

            var requestJson = JsonSerializer.Serialize(initRequest);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            if (ShouldLog(2))
            {
                _logger?.LogDebug("[MCP] 初始化请求: {Request}", requestJson);
                _logger?.LogDebug("[MCP] 请求URL: {Url}", _config.Url);
                // 记录请求头（隐藏敏感信息）
                foreach (var header in _httpClient.DefaultRequestHeaders)
                {
                    var value = header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) 
                        ? "[HIDDEN]" 
                        : string.Join(", ", header.Value);
                    _logger?.LogDebug("[MCP] 请求头 {Header}: {Value}", header.Key, value);
                }
            }

            var response = await _httpClient.PostAsync(_config.Url, content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (ShouldLog(0))
                    _logger?.LogError("[MCP] 初始化失败: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // 处理SSE格式响应
            var jsonContent = ExtractJsonFromSse(responseContent);
            var jsonNode = JsonNode.Parse(jsonContent);

            // 检查错误响应
            if (jsonNode?["error"] != null)
            {
                var errorMessage = jsonNode["error"]?["message"]?.ToString() ?? "未知错误";
                if (ShouldLog(0))
                    _logger?.LogError("[MCP] 初始化失败: {Error}", errorMessage);
                return false;
            }

            // 提取Session ID和服务器信息
            if (response.Headers.TryGetValues("MCP-Session-Id", out var sessionIds))
            {
                _sessionId = sessionIds.FirstOrDefault();
                if (ShouldLog(2))
                    _logger?.LogDebug("[MCP] Session ID: {SessionId}", _sessionId);
            }
            
            // 记录服务器信息（从响应中）
            var serverInfo = jsonNode?["result"]?["serverInfo"];
            if (serverInfo != null && ShouldLog(1))
            {
                var serverName = serverInfo["name"]?.ToString() ?? "未知";
                var serverVersion = serverInfo["version"]?.ToString() ?? "未知";
                _logger?.LogInformation("[MCP] 服务器信息 - Name: {Name}, Version: {Version}", serverName, serverVersion);
            }

            // 发送initialized通知
            await SendInitializedNotificationAsync(cancellationToken);

            _isConnected = true;
            if (ShouldLog(1))
                _logger?.LogInformation("[MCP] 服务器连接成功: {ServerName}", _config.Name);
            
            return true;
        }
        catch (Exception ex)
        {
            if (ShouldLog(0))
                _logger?.LogError(ex, "[MCP] 连接服务器失败: {ServerName}", _config.Name);
            _isConnected = false;
            return false;
        }
    }

    /// <summary>
    /// 发送initialized通知
    /// </summary>
    private async Task SendInitializedNotificationAsync(CancellationToken cancellationToken)
    {
        var notification = new
        {
            jsonrpc = "2.0",
            method = "notifications/initialized"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(notification),
            Encoding.UTF8,
            "application/json"
        );

        if (!string.IsNullOrEmpty(_sessionId))
        {
            content.Headers.Add("MCP-Session-Id", _sessionId);
            if (ShouldLog(2))
                _logger?.LogDebug("[MCP] 添加Session ID到initialized通知: {SessionId}", _sessionId);
        }

        if (ShouldLog(2))
            _logger?.LogDebug("[MCP] 发送initialized通知");
            
        var response = await _httpClient.PostAsync(_config.Url, content, cancellationToken);
        
        if (ShouldLog(2))
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                _logger?.LogDebug("[MCP] initialized通知已接受 (202 Accepted)");
            }
            else if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("[MCP] initialized通知返回非预期状态码: {StatusCode}", response.StatusCode);
            }
        }
    }

    /// <summary>
    /// 从SSE格式响应中提取JSON数据
    /// </summary>
    private string ExtractJsonFromSse(string responseContent)
    {
        var lines = responseContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(5).Trim();
            }
        }
        return responseContent;
    }

    /// <summary>
    /// 获取可用工具列表（带缓存）
    /// </summary>
    public async Task<List<McpTool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        return await ListToolsInternalAsync(cancellationToken, false);
    }
    
    /// <summary>
    /// 获取可用工具列表（内部实现）
    /// </summary>
    private async Task<List<McpTool>> ListToolsInternalAsync(CancellationToken cancellationToken, bool forceRefresh)
    {
        // 检查是否已释放
        if (_disposed)
            throw new ObjectDisposedException(nameof(SdkMcpClient), "MCP客户端已释放");
            
        if (!_isConnected)
        {
            throw new InvalidOperationException("MCP客户端未连接");
        }

        // 检查缓存是否有效
        if (!forceRefresh && _cachedTools != null && 
            DateTime.UtcNow - _toolsCacheTime < _toolsCacheTtl)
        {
            if (ShouldLog(2))
                _logger?.LogDebug("[MCP] 使用缓存的工具列表: {ServerName}, 缓存时间: {CacheAge}s",
                    _config.Name, (DateTime.UtcNow - _toolsCacheTime).TotalSeconds);
            return new List<McpTool>(_cachedTools);
        }

        try
        {
            if (ShouldLog(2))
                _logger?.LogDebug("[MCP] 从服务器获取工具列表: {ServerName}", _config.Name);

            var request = new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/list",
                @params = new { }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"
            );

            if (!string.IsNullOrEmpty(_sessionId))
            {
                content.Headers.Add("MCP-Session-Id", _sessionId);
            }

            var response = await _httpClient.PostAsync(_config.Url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonContent = ExtractJsonFromSse(responseContent);
            var jsonNode = JsonNode.Parse(jsonContent);

            var tools = new List<McpTool>();
            if (jsonNode?["result"]?["tools"] is JsonArray toolsArray)
            {
                foreach (var toolNode in toolsArray)
                {
                    if (toolNode != null)
                    {
                        tools.Add(new McpTool
                        {
                            Name = toolNode["name"]?.ToString() ?? string.Empty,
                            Description = toolNode["description"]?.ToString() ?? string.Empty,
                            InputSchema = toolNode["inputSchema"]?.ToString() ?? "{}"
                        });
                    }
                }
            }

            // 更新缓存
            _cachedTools = new List<McpTool>(tools);
            _toolsCacheTime = DateTime.UtcNow;

            if (ShouldLog(1))
                _logger?.LogInformation("[MCP] 获取到 {Count} 个工具并缓存", tools.Count);

            return tools;
        }
        catch (Exception ex)
        {
            if (ShouldLog(0))
                _logger?.LogError(ex, "[MCP] 获取工具列表失败");
            throw;
        }
    }
    
    /// <summary>
    /// 刷新工具列表缓存
    /// </summary>
    public async Task<List<McpTool>> RefreshToolsAsync(CancellationToken cancellationToken = default)
    {
        return await ListToolsInternalAsync(cancellationToken, true);
    }
    
    /// <summary>
    /// 使工具列表缓存失效
    /// </summary>
    public void InvalidateToolsCache()
    {
        _cachedTools = null;
        if (ShouldLog(2))
            _logger?.LogDebug("[MCP] 工具列表缓存已失效: {ServerName}", _config.Name);
    }

    /// <summary>
    /// 调用MCP工具
    /// </summary>
    public async Task<string> CallToolAsync(string toolName, object arguments, CancellationToken cancellationToken = default)
    {
        // 检查是否已释放
        if (_disposed)
            throw new ObjectDisposedException(nameof(SdkMcpClient), "MCP客户端已释放");
            
        if (!_isConnected)
        {
            throw new InvalidOperationException("MCP客户端未连接");
        }

        try
        {
            if (ShouldLog(1))
                _logger?.LogInformation("[MCP] 调用工具: {ToolName}", toolName);

            var request = new
            {
                jsonrpc = "2.0",
                id = 3,
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = arguments
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json"
            );

            if (!string.IsNullOrEmpty(_sessionId))
            {
                content.Headers.Add("MCP-Session-Id", _sessionId);
            }

            var response = await _httpClient.PostAsync(_config.Url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonContent = ExtractJsonFromSse(responseContent);
            var jsonNode = JsonNode.Parse(jsonContent);

            var result = jsonNode?["result"]?["content"]?.AsArray();
            if (result != null)
            {
                var sb = new StringBuilder();
                foreach (var contentNode in result)
                {
                    if (contentNode?["type"]?.ToString() == "text")
                    {
                        sb.AppendLine(contentNode["text"]?.ToString());
                    }
                }
                var resultText = sb.ToString().Trim();
                
                if (ShouldLog(2))
                    _logger?.LogDebug("[MCP] 工具调用结果: {Result}", 
                        resultText.Length > 100 ? resultText[..100] + "..." : resultText);

                return resultText;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            if (ShouldLog(0))
                _logger?.LogError(ex, "[MCP] 调用工具失败: {ToolName}", toolName);
            throw;
        }
    }

    /// <summary>
    /// 调用MCP工具（带进度回调）
    /// </summary>
    public async Task<string> CallToolAsync(string toolName, object arguments, IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        // SDK 目前不直接支持进度回调，模拟进度
        progress?.Report(0.1);
        
        try
        {
            var result = await CallToolAsync(toolName, arguments, cancellationToken);
            progress?.Report(1.0);
            return result;
        }
        catch
        {
            progress?.Report(0);
            throw;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// 释放资源的实际实现
    /// </summary>
    /// <param name="disposing">是否正在释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        lock (_disposeLock)
        {
            if (_disposed)
                return;
                
            if (disposing)
            {
                // 释放托管资源
                try
                {
                    _httpClient?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("[MCP] 释放HttpClient时发生异常: {Message}", ex.Message);
                }
            }
            
            // 释放非托管资源（如果有）
            _isConnected = false;
            _sessionId = null;
            _disposed = true;
            
            if (ShouldLog(2))
                _logger?.LogDebug("[MCP] SDK客户端已释放: {ServerName}", _config.Name);
        }
    }
    
    /// <summary>
    /// 析构函数（安全网）
    /// </summary>
    ~SdkMcpClient()
    {
        Dispose(false);
    }

    private bool ShouldLog(int requiredLevel) => _logLevel >= requiredLevel;
}
