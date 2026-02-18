using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace STranslate.Plugin.Translate.DeepSeek;

/// <summary>
/// MCP连接管理器（单例）
/// 管理HTTP/2连接池和Session复用
/// </summary>
public class McpConnectionManager : IDisposable
{
    private static McpConnectionManager? _instance;
    private static readonly object _lock = new();
    
    private readonly ConcurrentDictionary<string, IMcpClient> _clients = new();
    private readonly ILogger? _logger;
    private readonly int _logLevel;
    private readonly SocketsHttpHandler _httpHandler;
    private bool _disposed;

    public static McpConnectionManager Instance(ILogger? logger = null, int logLevel = 1)
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                _instance ??= new McpConnectionManager(logger, logLevel);
            }
        }
        return _instance;
    }

    private McpConnectionManager(ILogger? logger = null, int logLevel = 1)
    {
        _logger = logger;
        _logLevel = logLevel;
        
        // 配置HTTP/2连接池
        _httpHandler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            MaxConnectionsPerServer = 10
        };
        
        if (ShouldLog(1))
            _logger?.LogInformation("[MCP] 连接管理器初始化完成，HTTP/2连接池已启用");
    }

    /// <summary>
    /// 获取或创建客户端
    /// </summary>
    public async Task<IMcpClient> GetOrCreateClientAsync(McpServerConfig config, CancellationToken cancellationToken = default)
    {
        var key = GetClientKey(config);
        
        // 检查现有连接
        if (_clients.TryGetValue(key, out var existingClient))
        {
            if (existingClient.IsConnected)
            {
                if (ShouldLog(2))
                    _logger?.LogDebug("[MCP] 复用现有连接: {ServerName}", config.Name);
                return existingClient;
            }
            
            // 连接已断开，移除旧连接
            if (ShouldLog(1))
                _logger?.LogInformation("[MCP] 连接已断开，重新创建: {ServerName}", config.Name);
            _clients.TryRemove(key, out _);
            existingClient.Dispose();
        }
        
        // 创建新连接
        var client = McpClientFactory.CreateClient(config, _logger, _logLevel);
        var connected = await client.ConnectAsync(cancellationToken);
        
        if (!connected)
        {
            throw new InvalidOperationException($"无法连接到MCP服务器: {config.Name}");
        }
        
        _clients[key] = client;
        return client;
    }

    /// <summary>
    /// 移除指定服务器的连接
    /// </summary>
    public void RemoveConnection(string serverName, string serverUrl)
    {
        var key = $"{serverName}:{serverUrl}";
        if (_clients.TryRemove(key, out var client))
        {
            client.Dispose();
            if (ShouldLog(1))
                _logger?.LogInformation("[MCP] 已移除连接: {ServerName}", serverName);
        }
    }

    /// <summary>
    /// 清除所有连接
    /// </summary>
    public void ClearAllConnections()
    {
        foreach (var (key, client) in _clients)
        {
            client.Dispose();
            if (ShouldLog(2))
                _logger?.LogDebug("[MCP] 已清理连接: {Key}", key);
        }
        _clients.Clear();
        
        if (ShouldLog(1))
            _logger?.LogInformation("[MCP] 所有连接已清理");
    }

    /// <summary>
    /// 获取HTTP处理器（供SDK客户端使用）
    /// </summary>
    public HttpMessageHandler GetHttpHandler() => _httpHandler;

    private static string GetClientKey(McpServerConfig config) => $"{config.Name}:{config.Url}";

    private bool ShouldLog(int requiredLevel) => _logLevel >= requiredLevel;

    public void Dispose()
    {
        if (_disposed) return;
        
        ClearAllConnections();
        _httpHandler.Dispose();
        _disposed = true;
        
        if (ShouldLog(1))
            _logger?.LogInformation("[MCP] 连接管理器已释放");
    }
}

/// <summary>
/// MCP工具缓存服务（简化版，不依赖MemoryCache包）
/// </summary>
public class McpToolCache
{
    private readonly ConcurrentDictionary<string, (List<McpTool> Tools, DateTime Expiry)> _cache = new();
    private readonly ILogger? _logger;
    private readonly int _logLevel;
    private readonly TimeSpan _defaultTtl;

    public McpToolCache(int cacheMinutes = 5, ILogger? logger = null, int logLevel = 1)
    {
        _logger = logger;
        _logLevel = logLevel;
        _defaultTtl = TimeSpan.FromMinutes(cacheMinutes);
        
        if (ShouldLog(1))
            _logger?.LogInformation("[MCP] 工具缓存初始化完成，默认TTL: {Minutes}分钟", cacheMinutes);
    }

    /// <summary>
    /// 获取缓存的工具列表
    /// </summary>
    public List<McpTool>? GetCachedTools(string serverId)
    {
        var key = GetCacheKey(serverId);
        if (_cache.TryGetValue(key, out var cached))
        {
            if (cached.Expiry > DateTime.Now)
            {
                if (ShouldLog(2))
                    _logger?.LogDebug("[MCP] 从缓存获取工具列表: {ServerId}", serverId);
                return cached.Tools;
            }
            else
            {
                // 缓存已过期，移除
                _cache.TryRemove(key, out _);
                if (ShouldLog(2))
                    _logger?.LogDebug("[MCP] 缓存已过期，已移除: {ServerId}", serverId);
            }
        }
        return null;
    }

    /// <summary>
    /// 缓存工具列表
    /// </summary>
    public void SetCachedTools(string serverId, List<McpTool> tools)
    {
        var key = GetCacheKey(serverId);
        var expiry = DateTime.Now.Add(_defaultTtl);
        
        _cache[key] = (tools, expiry);
        
        if (ShouldLog(1))
            _logger?.LogInformation("[MCP] 已缓存工具列表: {ServerId}, 共 {Count} 个工具", serverId, tools.Count);
    }

    /// <summary>
    /// 清除指定服务器的缓存
    /// </summary>
    public void InvalidateCache(string serverId)
    {
        var key = GetCacheKey(serverId);
        _cache.TryRemove(key, out _);
        
        if (ShouldLog(1))
            _logger?.LogInformation("[MCP] 已清除工具缓存: {ServerId}", serverId);
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public void ClearAllCache()
    {
        _cache.Clear();
        
        if (ShouldLog(1))
            _logger?.LogInformation("[MCP] 所有工具缓存已清除");
    }

    private static string GetCacheKey(string serverId) => $"mcp:tools:{serverId}";

    private bool ShouldLog(int requiredLevel) => _logLevel >= requiredLevel;
}
