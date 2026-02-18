using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace STranslate.Plugin.Translate.DeepSeek;

/// <summary>
/// MCP客户端接口
/// </summary>
public interface IMcpClient : IDisposable
{
    /// <summary>
    /// 连接到MCP服务器
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取可用工具列表
    /// </summary>
    Task<List<McpTool>> ListToolsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 调用MCP工具
    /// </summary>
    Task<string> CallToolAsync(string toolName, object arguments, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 调用MCP工具（带进度回调）
    /// </summary>
    Task<string> CallToolAsync(string toolName, object arguments, IProgress<double> progress, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检查连接状态
    /// </summary>
    bool IsConnected { get; }
    
    /// <summary>
    /// 服务器配置
    /// </summary>
    McpServerConfig ServerConfig { get; }
}

/// <summary>
/// MCP工具信息
/// </summary>
public class McpTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InputSchema { get; set; } = "{}";
}

/// <summary>
/// MCP客户端工厂
/// </summary>
public static class McpClientFactory
{
    /// <summary>
    /// 创建MCP客户端（始终使用官方SDK）
    /// </summary>
    public static IMcpClient CreateClient(McpServerConfig config, ILogger? logger = null, int logLevel = 1)
    {
        logger?.LogInformation("[MCP] 创建客户端连接服务器: {ServerName}", config.Name);
        return new SdkMcpClient(config, logger, logLevel);
    }
}

/// <summary>
/// 工具调用结果
/// </summary>
public class ToolCallResult
{
    public string ToolName { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// 并发工具调用执行器
/// </summary>
public class ConcurrentToolExecutor
{
    private readonly int _maxConcurrent;
    private readonly ILogger? _logger;
    private readonly int _logLevel;

    public ConcurrentToolExecutor(int maxConcurrent = 5, ILogger? logger = null, int logLevel = 1)
    {
        _maxConcurrent = maxConcurrent;
        _logger = logger;
        _logLevel = logLevel;
    }

    /// <summary>
    /// 并发执行多个工具调用
    /// </summary>
    public async Task<List<ToolCallResult>> ExecuteConcurrentAsync(
        List<(string ToolName, object Arguments)> toolCalls,
        Func<string, object, IProgress<double>?, CancellationToken, Task<string>> callToolFunc,
        CancellationToken cancellationToken = default)
    {
        if (toolCalls.Count == 0) return [];

        var results = new List<ToolCallResult>();
        var semaphore = new SemaphoreSlim(_maxConcurrent);
        
        if (ShouldLog(1))
            _logger?.LogInformation("[MCP] 开始并发执行 {Count} 个工具，最大并发数: {Max}", toolCalls.Count, _maxConcurrent);

        var tasks = toolCalls.Select(async (toolCall, index) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var progress = new Progress<double>(p =>
                {
                    if (ShouldLog(2))
                        _logger?.LogDebug("[MCP] 工具 {ToolName} 进度: {Percent:P0}", toolCall.ToolName, p);
                });

                try
                {
                    var result = await callToolFunc(toolCall.ToolName, toolCall.Arguments, progress, cancellationToken);
                    stopwatch.Stop();
                    
                    if (ShouldLog(1))
                        _logger?.LogInformation("[MCP] 工具 {ToolName} 执行完成，耗时: {Ms}ms", toolCall.ToolName, stopwatch.ElapsedMilliseconds);

                    return new ToolCallResult
                    {
                        ToolName = toolCall.ToolName,
                        Result = result,
                        IsSuccess = true,
                        ExecutionTime = stopwatch.Elapsed
                    };
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    if (ShouldLog(0))
                        _logger?.LogError("[MCP] 工具 {ToolName} 执行失败: {Error}", toolCall.ToolName, ex.Message);

                    return new ToolCallResult
                    {
                        ToolName = toolCall.ToolName,
                        Result = string.Empty,
                        IsSuccess = false,
                        ErrorMessage = ex.Message,
                        ExecutionTime = stopwatch.Elapsed
                    };
                }
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        var completedResults = await Task.WhenAll(tasks);
        results.AddRange(completedResults);

        if (ShouldLog(1))
            _logger?.LogInformation("[MCP] 所有工具执行完成，成功: {Success}/{Total}", 
                results.Count(r => r.IsSuccess), results.Count);

        return results;
    }

    private bool ShouldLog(int requiredLevel) => _logLevel >= requiredLevel;
}
