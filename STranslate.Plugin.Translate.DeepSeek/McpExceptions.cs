using System.Net;
using System.Net.Http;

namespace STranslate.Plugin.Translate.DeepSeek;

/// <summary>
/// MCP 错误类型枚举
/// </summary>
public enum McpErrorType
{
    /// <summary>连接错误（可重试）</summary>
    ConnectionError,
    
    /// <summary>认证错误（不重试）</summary>
    AuthenticationError,
    
    /// <summary>工具不存在（不重试）</summary>
    ToolNotFound,
    
    /// <summary>参数错误（不重试）</summary>
    ParameterError,
    
    /// <summary>超时（可重试）</summary>
    Timeout,
    
    /// <summary>服务器内部错误（可重试）</summary>
    ServerError,
    
    /// <summary>请求被取消</summary>
    Cancelled,
    
    /// <summary>未知错误</summary>
    Unknown
}

/// <summary>
/// MCP 基础异常类
/// </summary>
public class McpException : Exception
{
    /// <summary>
    /// 错误类型
    /// </summary>
    public McpErrorType ErrorType { get; }
    
    /// <summary>
    /// 服务器名称
    /// </summary>
    public string? ServerName { get; }
    
    /// <summary>
    /// 工具名称
    /// </summary>
    public string? ToolName { get; }
    
    /// <summary>
    /// 是否可重试
    /// </summary>
    public bool IsRetryable => ErrorType is 
        McpErrorType.ConnectionError or 
        McpErrorType.Timeout or 
        McpErrorType.ServerError;
    
    public McpException(string message, McpErrorType errorType, string? serverName = null, string? toolName = null, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorType = errorType;
        ServerName = serverName;
        ToolName = toolName;
    }
}

/// <summary>
/// MCP 连接异常
/// </summary>
public class McpConnectionException : McpException
{
    public McpConnectionException(string message, string serverName, Exception? innerException = null)
        : base(message, McpErrorType.ConnectionError, serverName, null, innerException)
    {
    }
}

/// <summary>
/// MCP 认证异常
/// </summary>
public class McpAuthenticationException : McpException
{
    public McpAuthenticationException(string message, string serverName, Exception? innerException = null)
        : base(message, McpErrorType.AuthenticationError, serverName, null, innerException)
    {
    }
}

/// <summary>
/// MCP 工具调用异常
/// </summary>
public class McpToolCallException : McpException
{
    public McpToolCallException(string message, McpErrorType errorType, string serverName, string toolName, Exception? innerException = null)
        : base(message, errorType, serverName, toolName, innerException)
    {
    }
}

/// <summary>
/// MCP 超时异常
/// </summary>
public class McpTimeoutException : McpException
{
    public TimeSpan Timeout { get; }
    
    public McpTimeoutException(string message, string serverName, string? toolName = null, TimeSpan? timeout = null, Exception? innerException = null)
        : base(message, McpErrorType.Timeout, serverName, toolName, innerException)
    {
        Timeout = timeout ?? TimeSpan.FromSeconds(30);
    }
}

/// <summary>
/// MCP 重试耗尽异常
/// </summary>
public class McpRetryExhaustedException : McpException
{
    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; }
    
    /// <summary>
    /// 原始异常
    /// </summary>
    public Exception OriginalException { get; }
    
    public McpRetryExhaustedException(string message, int retryCount, Exception originalException, string? serverName = null, string? toolName = null)
        : base(message, McpErrorType.Unknown, serverName, toolName, originalException)
    {
        RetryCount = retryCount;
        OriginalException = originalException;
    }
}

/// <summary>
/// 重试策略配置
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// 基础延迟时间
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// 最大延迟时间
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// 超时时间
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// 执行带重试的操作
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        string operationName,
        string? serverName = null,
        string? toolName = null,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;
        
        for (int i = 0; i <= MaxRetries; i++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(Timeout);
                
                return await action(cts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new McpException(
                    $"{operationName} 被取消",
                    McpErrorType.Cancelled,
                    serverName,
                    toolName);
            }
            catch (McpException ex) when (ex.IsRetryable && i < MaxRetries)
            {
                lastException = ex;
                var delay = CalculateDelay(i);
                
                await Task.Delay(delay, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                // 转换 HTTP 异常为 MCP 异常
                var errorType = ClassifyHttpException(ex);
                var isRetryable = IsRetryableErrorType(errorType);
                
                if (isRetryable && i < MaxRetries)
                {
                    lastException = ex;
                    var delay = CalculateDelay(i);
                    await Task.Delay(delay, cancellationToken);
                }
                else
                {
                    throw ConvertToMcpException(ex, serverName, toolName);
                }
            }
            catch (Exception ex)
            {
                // 其他异常，如果可以重试则重试
                if (i < MaxRetries)
                {
                    lastException = ex;
                    var delay = CalculateDelay(i);
                    await Task.Delay(delay, cancellationToken);
                }
                else
                {
                    throw new McpException(
                        $"{operationName} 失败: {ex.Message}",
                        McpErrorType.Unknown,
                        serverName,
                        toolName,
                        ex);
                }
            }
        }
        
        throw new McpRetryExhaustedException(
            $"{operationName} 在重试 {MaxRetries} 次后仍然失败",
            MaxRetries,
            lastException!,
            serverName,
            toolName);
    }
    
    /// <summary>
    /// 计算指数退避延迟
    /// </summary>
    private TimeSpan CalculateDelay(int retryAttempt)
    {
        // 指数退避: BaseDelay * 2^retryAttempt
        var delayMs = BaseDelay.TotalMilliseconds * Math.Pow(2, retryAttempt);
        // 添加随机抖动（±25%）
        var jitter = new Random().NextDouble() * 0.5 - 0.25;
        delayMs *= (1 + jitter);
        
        return TimeSpan.FromMilliseconds(Math.Min(delayMs, MaxDelay.TotalMilliseconds));
    }
    
    /// <summary>
    /// 分类 HTTP 异常
    /// </summary>
    private static McpErrorType ClassifyHttpException(HttpRequestException ex)
    {
        if (ex.StatusCode == HttpStatusCode.Unauthorized ||
            ex.StatusCode == HttpStatusCode.Forbidden)
        {
            return McpErrorType.AuthenticationError;
        }
        
        if (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return McpErrorType.ToolNotFound;
        }
        
        if (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return McpErrorType.ParameterError;
        }
        
        if (ex.StatusCode == null ||
            ex.StatusCode >= HttpStatusCode.InternalServerError)
        {
            return McpErrorType.ServerError;
        }
        
        return McpErrorType.ConnectionError;
    }
    
    /// <summary>
    /// 判断错误类型是否可重试
    /// </summary>
    private static bool IsRetryableErrorType(McpErrorType errorType)
    {
        return errorType is McpErrorType.ConnectionError or 
            McpErrorType.Timeout or 
            McpErrorType.ServerError;
    }
    
    /// <summary>
    /// 将异常转换为 MCP 异常
    /// </summary>
    private static McpException ConvertToMcpException(Exception ex, string? serverName, string? toolName)
    {
        if (ex is HttpRequestException httpEx)
        {
            var errorType = ClassifyHttpException(httpEx);
            return new McpException(
                httpEx.Message,
                errorType,
                serverName,
                toolName,
                httpEx);
        }
        
        if (ex is TaskCanceledException)
        {
            return new McpTimeoutException(
                "操作超时",
                serverName ?? "Unknown",
                toolName,
                TimeSpan.FromSeconds(30),
                ex);
        }
        
        return new McpException(
            ex.Message,
            McpErrorType.Unknown,
            serverName,
            toolName,
            ex);
    }
}

/// <summary>
/// 全局重试策略实例
/// </summary>
public static class GlobalRetryPolicy
{
    /// <summary>
    /// 默认重试策略
    /// </summary>
    public static RetryPolicy Default { get; } = new RetryPolicy
    {
        MaxRetries = 3,
        BaseDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(30),
        Timeout = TimeSpan.FromSeconds(30)
    };
    
    /// <summary>
    /// 连接重试策略（更长的超时）
    /// </summary>
    public static RetryPolicy Connection { get; } = new RetryPolicy
    {
        MaxRetries = 5,
        BaseDelay = TimeSpan.FromSeconds(2),
        MaxDelay = TimeSpan.FromSeconds(60),
        Timeout = TimeSpan.FromSeconds(10)
    };
    
    /// <summary>
    /// 工具调用重试策略
    /// </summary>
    public static RetryPolicy ToolCall { get; } = new RetryPolicy
    {
        MaxRetries = 2,
        BaseDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(10),
        Timeout = TimeSpan.FromSeconds(30)
    };
}
