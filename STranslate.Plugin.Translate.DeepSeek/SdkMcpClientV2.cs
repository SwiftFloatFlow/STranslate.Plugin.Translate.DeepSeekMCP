using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace STranslate.Plugin.Translate.DeepSeek;

/// <summary>
/// 基于官方 ModelContextProtocol SDK v1.0 的 MCP 客户端
/// </summary>
public class SdkMcpClientV2 : IMcpClient, IDisposable
{
    private readonly McpServerConfig _config;
    private readonly ILogger? _logger;
    private readonly int _logLevel;
    private IMcpClient? _mcpClient;
    private HttpClient? _httpClient;
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
            _logger?.LogInformation("[MCP V2] SDK客户端初始化完成，服务器: {ServerName}, HTTP/2: 已启用", config.Name);
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SdkMcpClientV2), "MCP客户端已释放，无法连接");
            
        try
        {
            if (ShouldLog(1))
                _logger?.LogInformation("[MCP V2] 开始连接服务器: {ServerName}", _config.Name);

            var authHeaders = GetAuthenticationHeaders();
            
            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(_config.Url),
                AdditionalHeaders = authHeaders
            });

            var options = new McpClientOptions
            {
                ClientInfo = new Implementation
                {
                    Name = "DeepSeekMCP",
                    Version = "2.0.0"
                }
            };

            _mcpClient = (IMcpClient)await McpClient.CreateAsync(
                transport,
                options,
                null,
                cancellationToken);

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

    private Dictionary<string, string>? GetAuthenticationHeaders()
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
            return null;

        var apiKey = _config.ApiKey.Trim();
        string authValue;

        if (apiKey.StartsWith("Authorization=", StringComparison.OrdinalIgnoreCase))
        {
            var value = apiKey.Substring("Authorization=".Length).Trim();
            authValue = value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value : $"Bearer {value}";
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
            
        if (!_isConnected || _mcpClient == null)
            throw new InvalidOperationException("MCP客户端未连接");

        if (!forceRefresh && _cachedTools != null && 
            DateTime.UtcNow - _toolsCacheTime < _toolsCacheTtl)
        {
            if (ShouldLog(2))
                _logger?.LogDebug("[MCP V2] 使用缓存的工具列表: {ServerName}", _config.Name);
            return new List<McpTool>(_cachedTools);
        }

        try
        {
            if (ShouldLog(2))
                _logger?.LogDebug("[MCP V2] 从服务器获取工具列表: {ServerName}", _config.Name);

            var tools = await _mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
            
            var result = tools.Select(t => new McpTool
            {
                Name = t.Name,
                Description = t.Description ?? string.Empty,
                InputSchema = t.InputSchema?.ToString() ?? "{}"
            }).ToList();

            _cachedTools = result;
            _toolsCacheTime = DateTime.UtcNow;

            if (ShouldLog(1))
                _logger?.LogInformation("[MCP V2] 获取到 {Count} 个工具并缓存", result.Count);

            return result;
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
            
        if (!_isConnected || _mcpClient == null)
            throw new InvalidOperationException("MCP客户端未连接");

        try
        {
            if (ShouldLog(1))
                _logger?.LogInformation("[MCP V2] 调用工具: {ToolName}", toolName);

            Dictionary<string, object?> dict;
            if (arguments is Dictionary<string, object?> d)
                dict = d;
            else if (arguments is System.Collections.IDictionary id)
                dict = id.Cast<System.Collections.DictionaryEntry>().ToDictionary(k => k.Key.ToString()!, v => v.Value);
            else
                dict = [];

            var result = await _mcpClient.CallToolAsync(toolName, dict, cancellationToken: cancellationToken);
            
            var textResult = result ?? string.Empty;

            if (ShouldLog(2))
                _logger?.LogDebug("[MCP V2] 工具调用结果: {Result}", 
                    textResult.Length > 100 ? textResult[..100] + "..." : textResult);

            return textResult;
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
                    if (_mcpClient is IAsyncDisposable asyncDisposable)
                        asyncDisposable.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("[MCP V2] 释放McpClient时发生异常: {Message}", ex.Message);
                }
                
                try
                {
                    _httpClient?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("[MCP V2] 释放HttpClient时发生异常: {Message}", ex.Message);
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
