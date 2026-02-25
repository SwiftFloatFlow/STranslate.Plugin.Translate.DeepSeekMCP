using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace STranslate.Plugin.Translate.DeepSeek;

/// <summary>
/// 基于官方 ModelContextProtocol SDK v1.0 的 MCP 客户端
/// </summary>
public class SdkMcpClientV2 : IMcpClient, IDisposable
{
    private readonly McpServerConfig _config;
    private readonly ILogger? _logger;
    private readonly int _logLevel;
    private McpClient? _mcpClient;
    private bool _isConnected;
    
    private bool _disposed;
    private readonly object _disposeLock = new object();
    
    private List<McpTool>? _cachedTools;
    private DateTime _toolsCacheTime;
    private readonly TimeSpan _toolsCacheTtl = TimeSpan.FromMinutes(5);

    public McpServerConfig ServerConfig => _config;
    public bool IsConnected => _isConnected;

    public SdkMcpClientV2(McpServerConfig config, ILogger? logger = null, int logLevel = 1)
    {
        _config = config;
        _logger = logger;
        _logLevel = logLevel;
        
        if (ShouldLog(1))
            _logger?.LogInformation("[MCP V2] SDK客户端初始化完成，服务器: {ServerName}", config.Name);
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SdkMcpClientV2), "MCP客户端已释放，无法连接");
            
        try
        {
            if (ShouldLog(1))
                _logger?.LogInformation("[MCP V2] 开始连接服务器: {ServerName}", _config.Name);

            var authHeaders = GetAuthHeaders();
            
            var options = new HttpClientTransportOptions
            {
                Endpoint = new Uri(_config.Url),
                Name = _config.Name,
                AdditionalHeaders = authHeaders
            };

            var transport = new HttpClientTransport(options);
            
            var clientOptions = new McpClientOptions
            {
                ClientInfo = new ModelContextProtocol.Protocol.Implementation { Name = "DeepSeekMCP", Version = "2.0.0" }
            };

            _mcpClient = await McpClient.CreateAsync(transport, clientOptions, null, cancellationToken);

            _isConnected = true;
            
            if (ShouldLog(1))
            {
                _logger?.LogInformation("[MCP V2] 服务器连接成功: {ServerName}", _config.Name);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            if (ShouldLog(0))
                _logger?.LogError(ex, "[MCP V2] 连接服务器失败: {ServerName}", _config.Name);
            _isConnected = false;
            return false;
        }
    }

    private Dictionary<string, string>? GetAuthHeaders()
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
            return null;
            
        var apiKey = _config.ApiKey.Trim();
        string authValue;
        
        if (apiKey.StartsWith("Authorization=", StringComparison.OrdinalIgnoreCase))
        {
            authValue = apiKey.Substring("Authorization=".Length).Trim();
        }
        else if (apiKey.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            authValue = apiKey;
        }
        else if (apiKey.Contains(':'))
        {
            var idx = apiKey.IndexOf(':');
            var parts = new[] { apiKey[..idx], apiKey[(idx + 1)..] };
            if (parts.Length == 2)
            {
                return new Dictionary<string, string>
                {
                    [parts[0].Trim()] = parts[1].Trim()
                };
            }
            authValue = $"Bearer {apiKey}";
        }
        else
        {
            authValue = $"Bearer {apiKey}";
        }

        if (ShouldLog(2))
            _logger?.LogDebug("[MCP V2] 已配置认证头");

        return new Dictionary<string, string>
        {
            ["Authorization"] = authValue
        };
    }

    public async Task<List<McpTool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        return await ListToolsInternalAsync(cancellationToken, false);
    }
    
    private async Task<List<McpTool>> ListToolsInternalAsync(CancellationToken cancellationToken, bool forceRefresh)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SdkMcpClientV2), "MCP客户端已释放");
            
        if (_mcpClient == null)
        {
            throw new InvalidOperationException("MCP客户端未连接");
        }

        if (!forceRefresh && _cachedTools != null && 
            DateTime.UtcNow - _toolsCacheTime < _toolsCacheTtl)
        {
            if (ShouldLog(2))
                _logger?.LogDebug("[MCP V2] 使用缓存的工具列表: {ServerName}, 缓存时间: {CacheAge}s",
                    _config.Name, (DateTime.UtcNow - _toolsCacheTime).TotalSeconds);
            return new List<McpTool>(_cachedTools);
        }

        try
        {
            if (ShouldLog(2))
                _logger?.LogDebug("[MCP V2] 从服务器获取工具列表: {ServerName}", _config.Name);

            var tools = new List<McpTool>();
            var mcpTools = await _mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
            
            foreach (var tool in mcpTools)
            {
                tools.Add(new McpTool
                {
                    Name = tool.Name,
                    Description = tool.Description ?? string.Empty,
                    InputSchema = tool.JsonSchema.ToString()
                });
            }

            _cachedTools = new List<McpTool>(tools);
            _toolsCacheTime = DateTime.UtcNow;

            if (ShouldLog(1))
                _logger?.LogInformation("[MCP V2] 获取到 {Count} 个工具并缓存", tools.Count);

            return tools;
        }
        catch (Exception ex)
        {
            if (ShouldLog(0))
                _logger?.LogError(ex, "[MCP V2] 获取工具列表失败");
            throw;
        }
    }
    
    public async Task<List<McpTool>> RefreshToolsAsync(CancellationToken cancellationToken = default)
    {
        return await ListToolsInternalAsync(cancellationToken, true);
    }
    
    public void InvalidateToolsCache()
    {
        _cachedTools = null;
        if (ShouldLog(2))
            _logger?.LogDebug("[MCP V2] 工具列表缓存已失效: {ServerName}", _config.Name);
    }

    public async Task<string> CallToolAsync(string toolName, object arguments, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SdkMcpClientV2), "MCP客户端已释放");
            
        if (_mcpClient == null)
        {
            throw new InvalidOperationException("MCP客户端未连接");
        }

        try
        {
            if (ShouldLog(1))
                _logger?.LogInformation("[MCP V2] 调用工具: {ToolName}", toolName);

            var dictArgs = arguments as Dictionary<string, object?> ?? new Dictionary<string, object?>();
            var result = await _mcpClient.CallToolAsync(toolName, dictArgs, cancellationToken: cancellationToken);

            var resultText = "";
            if (result.Content != null)
            {
                foreach (var content in result.Content)
                {
                    if (content is ModelContextProtocol.Protocol.TextContentBlock textContent && !string.IsNullOrEmpty(textContent.Text))
                    {
                        resultText += textContent.Text;
                    }
                }
            }
                
            if (ShouldLog(2))
                _logger?.LogDebug("[MCP V2] 工具调用结果: {Result}", 
                    resultText.Length > 100 ? resultText[..100] + "..." : resultText);

            return resultText;
        }
        catch (Exception ex)
        {
            if (ShouldLog(0))
                _logger?.LogError(ex, "[MCP V2] 调用工具失败: {ToolName}", toolName);
            throw;
        }
    }

    public async Task<string> CallToolAsync(string toolName, object arguments, IProgress<double> progress, CancellationToken cancellationToken = default)
    {
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        lock (_disposeLock)
        {
            if (_disposed)
                return;
                
            if (disposing)
            {
                try
                {
                    (_mcpClient as IDisposable)?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("[MCP V2] 释放资源时发生异常: {Message}", ex.Message);
                }
            }
            
            _isConnected = false;
            _mcpClient = null;
            _disposed = true;
            
            if (ShouldLog(2))
                _logger?.LogDebug("[MCP V2] 客户端已释放: {ServerName}", _config.Name);
        }
    }
    
    ~SdkMcpClientV2()
    {
        Dispose(false);
    }

    private bool ShouldLog(int requiredLevel) => _logLevel >= requiredLevel;
}
