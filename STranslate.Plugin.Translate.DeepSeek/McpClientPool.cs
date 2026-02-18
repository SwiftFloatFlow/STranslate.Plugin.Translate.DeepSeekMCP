using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace STranslate.Plugin.Translate.DeepSeek;

/// <summary>
/// MCP 客户端连接池
/// 管理多个 MCP 服务器连接的复用和生命周期
/// </summary>
public class McpClientPool : IDisposable
{
    private readonly ConcurrentDictionary<string, PooledMcpClient> _clients = new();
    private readonly ILogger? _logger;
    private readonly int _logLevel;
    private readonly TimeSpan _idleTimeout;
    private readonly Timer _cleanupTimer;
    private bool _disposed;
    private readonly object _disposeLock = new();

    /// <summary>
    /// 创建 MCP 客户端连接池
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="logLevel">日志级别</param>
    /// <param name="idleTimeout">空闲超时时间（默认5分钟）</param>
    public McpClientPool(ILogger? logger = null, int logLevel = 1, TimeSpan? idleTimeout = null)
    {
        _logger = logger;
        _logLevel = logLevel;
        _idleTimeout = idleTimeout ?? TimeSpan.FromMinutes(5);
        
        // 创建定期清理定时器（每30秒检查一次）
        _cleanupTimer = new Timer(CleanupIdleClients, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        if (ShouldLog(1))
            _logger?.LogInformation("[MCP] 连接池初始化完成，空闲超时: {IdleTimeout}s", _idleTimeout.TotalSeconds);
    }

    /// <summary>
    /// 获取或创建 MCP 客户端（带重试机制）
    /// </summary>
    /// <param name="config">服务器配置</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>MCP 客户端</returns>
    public async Task<IMcpClient> GetClientAsync(McpServerConfig config, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(McpClientPool), "连接池已释放");

        var key = GetClientKey(config);
        
        // 尝试获取现有客户端
        if (_clients.TryGetValue(key, out var existingClient))
        {
            // 检查客户端是否仍然有效
            if (existingClient.IsValid)
            {
                existingClient.LastUsedTime = DateTime.UtcNow;
                existingClient.UseCount++;
                
                if (ShouldLog(2))
                    _logger?.LogDebug("[MCP] 复用连接池中的客户端: {ServerName}, 使用次数: {UseCount}", 
                        config.Name, existingClient.UseCount);
                
                return existingClient.Client;
            }
            else
            {
                // 客户端已失效，移除
                if (_clients.TryRemove(key, out var oldClient))
                {
                    oldClient.Client.Dispose();
                    if (ShouldLog(2))
                        _logger?.LogDebug("[MCP] 移除失效的客户端: {ServerName}", config.Name);
                }
            }
        }
        
        // 创建新客户端
        var newClient = new PooledMcpClient
        {
            Client = new SdkMcpClient(config, _logger, _logLevel),
            Config = config,
            CreatedTime = DateTime.UtcNow,
            LastUsedTime = DateTime.UtcNow,
            UseCount = 1
        };
        
        // 尝试添加到池（如果已存在则使用现有）
        var pooledClient = _clients.GetOrAdd(key, newClient);
        
        // 如果是新添加的，需要连接
        if (pooledClient == newClient)
        {
            if (ShouldLog(1))
                _logger?.LogInformation("[MCP] 创建新的客户端并加入连接池: {ServerName}", config.Name);
            
            try
            {
                // 使用重试策略连接服务器
                await GlobalRetryPolicy.Connection.ExecuteAsync<bool>(
                    async ct =>
                    {
                        var connected = await pooledClient.Client.ConnectAsync(ct);
                        if (!connected)
                        {
                            throw new McpConnectionException(
                                $"连接到服务器 {config.Name} 失败",
                                config.Name);
                        }
                        return true;
                    },
                    "连接MCP服务器",
                    config.Name,
                    null,
                    cancellationToken);
            }
            catch (Exception)
            {
                // 连接失败，移除客户端
                _clients.TryRemove(key, out _);
                pooledClient.Client.Dispose();
                throw;
            }
        }
        else
        {
            // 使用了已存在的客户端，释放新创建的
            newClient.Client.Dispose();
            pooledClient.LastUsedTime = DateTime.UtcNow;
            pooledClient.UseCount++;
        }
        
        return pooledClient.Client;
    }

    /// <summary>
    /// 移除并释放指定服务器的客户端
    /// </summary>
    /// <param name="config">服务器配置</param>
    public void RemoveClient(McpServerConfig config)
    {
        var key = GetClientKey(config);
        
        if (_clients.TryRemove(key, out var pooledClient))
        {
            pooledClient.Client.Dispose();
            
            if (ShouldLog(1))
                _logger?.LogInformation("[MCP] 从连接池移除客户端: {ServerName}", config.Name);
        }
    }

    /// <summary>
    /// 获取连接池统计信息
    /// </summary>
    public ClientPoolStatistics GetStatistics()
    {
        var stats = new ClientPoolStatistics
        {
            TotalClients = _clients.Count,
            ActiveClients = _clients.Count(c => c.Value.IsValid),
            IdleClients = _clients.Count(c => c.Value.IsIdle),
            TotalUseCount = _clients.Sum(c => c.Value.UseCount)
        };
        
        return stats;
    }

    /// <summary>
    /// 清理空闲客户端
    /// </summary>
    private void CleanupIdleClients(object? state)
    {
        if (_disposed)
            return;

        try
        {
            var idleClients = _clients.Where(c => c.Value.IsIdle && 
                DateTime.UtcNow - c.Value.LastUsedTime > _idleTimeout).ToList();
            
            foreach (var kvp in idleClients)
            {
                if (_clients.TryRemove(kvp.Key, out var pooledClient))
                {
                    pooledClient.Client.Dispose();
                    
                    if (ShouldLog(2))
                        _logger?.LogDebug("[MCP] 清理空闲客户端: {ServerName}, 空闲时间: {IdleTime}s",
                            pooledClient.Config.Name,
                            (DateTime.UtcNow - pooledClient.LastUsedTime).TotalSeconds);
                }
            }
            
            if (idleClients.Count > 0 && ShouldLog(1))
                _logger?.LogInformation("[MCP] 连接池清理完成，移除了 {Count} 个空闲客户端，当前池大小: {PoolSize}",
                    idleClients.Count, _clients.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[MCP] 清理空闲客户端时发生异常");
        }
    }

    /// <summary>
    /// 生成客户端键
    /// </summary>
    private string GetClientKey(McpServerConfig config)
    {
        // 使用 URL + API Key 的哈希作为键
        var key = $"{config.Url}#{config.ApiKey}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key));
    }

    /// <summary>
    /// 释放连接池
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源的实际实现
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        lock (_disposeLock)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // 停止清理定时器
                _cleanupTimer?.Dispose();
                
                // 释放所有客户端
                foreach (var kvp in _clients)
                {
                    try
                    {
                        kvp.Value.Client.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning("[MCP] 释放客户端时发生异常: {Message}", ex.Message);
                    }
                }
                
                _clients.Clear();
            }

            _disposed = true;
            
            if (ShouldLog(1))
                _logger?.LogInformation("[MCP] 连接池已释放");
        }
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~McpClientPool()
    {
        Dispose(false);
    }

    private bool ShouldLog(int requiredLevel) => _logLevel >= requiredLevel;
}

/// <summary>
/// 池化的 MCP 客户端包装
/// </summary>
internal class PooledMcpClient
{
    /// <summary>
    /// MCP 客户端实例
    /// </summary>
    public required IMcpClient Client { get; set; }
    
    /// <summary>
    /// 服务器配置
    /// </summary>
    public required McpServerConfig Config { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; }
    
    /// <summary>
    /// 最后使用时间
    /// </summary>
    public DateTime LastUsedTime { get; set; }
    
    /// <summary>
    /// 使用次数
    /// </summary>
    public int UseCount { get; set; }
    
    /// <summary>
    /// 是否有效（未断开连接）
    /// </summary>
    public bool IsValid => Client.IsConnected;
    
    /// <summary>
    /// 是否空闲（超过30秒未使用）
    /// </summary>
    public bool IsIdle => DateTime.UtcNow - LastUsedTime > TimeSpan.FromSeconds(30);
}

/// <summary>
/// 连接池统计信息
/// </summary>
public class ClientPoolStatistics
{
    /// <summary>
    /// 总客户端数
    /// </summary>
    public int TotalClients { get; set; }
    
    /// <summary>
    /// 活跃客户端数
    /// </summary>
    public int ActiveClients { get; set; }
    
    /// <summary>
    /// 空闲客户端数
    /// </summary>
    public int IdleClients { get; set; }
    
    /// <summary>
    /// 总使用次数
    /// </summary>
    public long TotalUseCount { get; set; }
}
