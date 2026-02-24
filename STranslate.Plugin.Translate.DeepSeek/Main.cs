using STranslate.Plugin.Translate.DeepSeek.View;
using STranslate.Plugin.Translate.DeepSeek.ViewModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;

namespace STranslate.Plugin.Translate.DeepSeek;

public class Main : LlmTranslatePluginBase
{
    private Control? _settingUi;
    private SettingsViewModel? _viewModel;
    private Settings Settings { get; set; } = null!;
    private IPluginContext Context { get; set; } = null!;
    private List<IMcpClient> _mcpClients = [];
    private McpConnectionManager? _connectionManager;
    private McpToolCache? _toolCache;
    private ConcurrentToolExecutor? _toolExecutor;
    private McpClientPool? _clientPool;
    private readonly SemaphoreSlim _translationSemaphore = new(5, 5);
    
    // [全局提示词] 识别全局提示词的ID集合
    private HashSet<Guid> _globalPromptIds = [];
    private IDisposable? _globalPromptsCallback;
    
    /// <summary>
    /// 判断指定提示词是否为全局提示词
    /// </summary>
    public bool IsGlobalPrompt(Prompt? prompt) => prompt != null && _globalPromptIds.Contains(prompt.Id);

    /// <summary>
    /// 根据日志级别判断是否允许记录日志
    /// </summary>
    private bool ShouldLog(int requiredLevel)
    {
        // 0=粗略, 1=中等, 2=详细
        return Settings.LogLevel >= requiredLevel;
    }

    /// <summary>
    /// 获取当前实际使用的 MCP 策略（两层优先级架构）
    /// 第1层：全局 MCP 总开关（EnableMcp）
    /// 第2层：提示词级策略（PromptStrategyMap，默认为 Disabled）
    /// </summary>
    private McpToolStrategy GetEffectiveStrategy()
    {
        // 第1层：检查全局 MCP 总开关
        if (!Settings.EnableMcp)
        {
            if (ShouldLog(1))
                Context.Logger.LogInformation("[MCP策略] 全局 MCP 功能已禁用，使用 Disabled 策略");
            return McpToolStrategy.Disabled;
        }

        // 获取当前选中的提示词
        var currentPrompt = SelectedPrompt;
        if (currentPrompt == null)
        {
            // 没有选中提示词，使用默认策略
            if (ShouldLog(1))
                Context.Logger.LogInformation("[MCP策略] 未选择提示词，使用默认策略: Disabled");
            return McpToolStrategy.Disabled;
        }

        // 第2层：检查提示词是否有绑定的策略，默认为 Disabled
        if (Settings.PromptStrategyMap.TryGetValue(currentPrompt.Name, out var promptStrategy))
        {
            if (ShouldLog(1))
                Context.Logger.LogInformation($"[MCP策略] 提示词 '{currentPrompt.Name}' 使用绑定策略: {promptStrategy}");
            return promptStrategy;
        }

        // 默认使用 Disabled
        if (ShouldLog(1))
            Context.Logger.LogInformation($"[MCP策略] 提示词 '{currentPrompt.Name}' 未绑定策略，使用默认策略: Disabled");
        return McpToolStrategy.Disabled;
    }

    public override void SelectPrompt(Prompt? prompt)
    {
        base.SelectPrompt(prompt);
        // 只保存局部提示词（过滤掉全局提示词）
        Settings.Prompts = [.. Prompts.Where(p => !_globalPromptIds.Contains(p.Id)).Select(p => p.Clone())];
        Context.SaveSettingStorage<Settings>();
    }

    public override Control GetSettingUI()
    {
        // [全局提示词] 如果全局提示词尚未加载（Init时可能主程序还没准备好），现在再尝试加载
        if (_globalPromptIds.Count == 0)
        {
            LoadGlobalPrompts();
        }
        
        _viewModel ??= new SettingsViewModel(Context, Settings, this);
        _settingUi ??= new SettingsView { DataContext = _viewModel };
        return _settingUi;
    }

    public override string? GetSourceLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "Requires you to identify automatically",
        LangEnum.ChineseSimplified => "Simplified Chinese",
        LangEnum.ChineseTraditional => "Traditional Chinese",
        LangEnum.Cantonese => "Cantonese",
        LangEnum.English => "English",
        LangEnum.Japanese => "Japanese",
        LangEnum.Korean => "Korean",
        LangEnum.French => "French",
        LangEnum.Spanish => "Spanish",
        LangEnum.Russian => "Russian",
        LangEnum.German => "German",
        LangEnum.Italian => "Italian",
        LangEnum.Turkish => "Turkish",
        LangEnum.PortuguesePortugal => "Portuguese",
        LangEnum.PortugueseBrazil => "Portuguese",
        LangEnum.Vietnamese => "Vietnamese",
        LangEnum.Indonesian => "Indonesian",
        LangEnum.Thai => "Thai",
        LangEnum.Malay => "Malay",
        LangEnum.Arabic => "Arabic",
        LangEnum.Hindi => "Hindi",
        LangEnum.MongolianCyrillic => "Mongolian",
        LangEnum.MongolianTraditional => "Mongolian",
        LangEnum.Khmer => "Central Khmer",
        LangEnum.NorwegianBokmal => "Norwegian Bokmål",
        LangEnum.NorwegianNynorsk => "Norwegian Nynorsk",
        LangEnum.Persian => "Persian",
        LangEnum.Swedish => "Swedish",
        LangEnum.Polish => "Polish",
        LangEnum.Dutch => "Dutch",
        LangEnum.Ukrainian => "Ukrainian",
        _ => "Requires you to identify automatically"
    };

    public override string? GetTargetLanguage(LangEnum langEnum) => langEnum switch
    {
        LangEnum.Auto => "Requires you to identify automatically",
        LangEnum.ChineseSimplified => "Simplified Chinese",
        LangEnum.ChineseTraditional => "Traditional Chinese",
        LangEnum.Cantonese => "Cantonese",
        LangEnum.English => "English",
        LangEnum.Japanese => "Japanese",
        LangEnum.Korean => "Korean",
        LangEnum.French => "French",
        LangEnum.Spanish => "Spanish",
        LangEnum.Russian => "Russian",
        LangEnum.German => "German",
        LangEnum.Italian => "Italian",
        LangEnum.Turkish => "Turkish",
        LangEnum.PortuguesePortugal => "Portuguese",
        LangEnum.PortugueseBrazil => "Portuguese",
        LangEnum.Vietnamese => "Vietnamese",
        LangEnum.Indonesian => "Indonesian",
        LangEnum.Thai => "Thai",
        LangEnum.Malay => "Malay",
        LangEnum.Arabic => "Arabic",
        LangEnum.Hindi => "Hindi",
        LangEnum.MongolianCyrillic => "Mongolian",
        LangEnum.MongolianTraditional => "Mongolian",
        LangEnum.Khmer => "Central Khmer",
        LangEnum.NorwegianBokmal => "Norwegian Bokmål",
        LangEnum.NorwegianNynorsk => "Norwegian Nynorsk",
        LangEnum.Persian => "Persian",
        LangEnum.Swedish => "Swedish",
        LangEnum.Polish => "Polish",
        LangEnum.Dutch => "Dutch",
        LangEnum.Ukrainian => "Ukrainian",
        _ => "Requires you to identify automatically"
    };

    public override void Init(IPluginContext context)
    {
        Context = context;
        Settings = context.LoadSettingStorage<Settings>();
        
        // 执行配置迁移
        Settings.Migrate();
        
        // [全局提示词] 先加载全局提示词，获取全局提示词ID集合
        var globalPrompts = Context.GetGlobalPrompts();
        _globalPromptIds = globalPrompts.Select(p => p.Id).ToHashSet();
        
        // 加载局部提示词（过滤掉可能是全局提示词的项）
        foreach (var p in Settings.Prompts)
        {
            // 如果这个提示词ID在全局提示词中存在，跳过（不作为局部提示词加载）
            if (_globalPromptIds.Contains(p.Id))
            {
                continue;
            }
            Prompts.Add(p);
        }
        
        // 添加全局提示词到 Prompts
        foreach (var p in globalPrompts)
        {
            Prompts.Add(p);
        }
        
        // [全局提示词] 注册变更回调
        _globalPromptsCallback = Context.RegisterGlobalPromptsChangedCallback(
            OnGlobalPromptsChanged, delayMs: 100);

        if (Settings.EnableMcp && Settings.McpServers.Count > 0)
        {
            InitializeMcpClients();
        }
    }
    
    /// <summary>
    /// [全局提示词] 加载全局提示词到 Prompts 集合（供回调使用）
    /// </summary>
    private void LoadGlobalPrompts()
    {
        var globalPrompts = Context.GetGlobalPrompts();
        
        // 更新全局提示词ID集合
        var newGlobalIds = globalPrompts.Select(p => p.Id).ToHashSet();
        
        // 移除已经不在全局提示词列表中的旧全局提示词
        for (int i = Prompts.Count - 1; i >= 0; i--)
        {
            if (_globalPromptIds.Contains(Prompts[i].Id) && !newGlobalIds.Contains(Prompts[i].Id))
            {
                Prompts.RemoveAt(i);
            }
        }
        
        // 更新ID集合
        _globalPromptIds = newGlobalIds;
        
        // 移除已加载的局部提示词中实际上是全局提示词的项（ID匹配）
        for (int i = Prompts.Count - 1; i >= 0; i--)
        {
            if (_globalPromptIds.Contains(Prompts[i].Id))
            {
                Prompts.RemoveAt(i);
            }
        }
        
        // 添加全局提示词
        foreach (var p in globalPrompts)
        {
            if (!Prompts.Any(x => x.Id == p.Id))
            {
                Prompts.Add(p);
            }
        }
    }
    
    /// <summary>
    /// [全局提示词] 变更回调
    /// </summary>
    private void OnGlobalPromptsChanged(IReadOnlyList<Prompt> newGlobalPrompts)
    {
        try
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // 保存当前选中的提示词ID
                var previouslySelectedId = SelectedPrompt?.Id ?? Guid.Empty;
                
                // 移除旧的全局提示词
                for (int i = Prompts.Count - 1; i >= 0; i--)
                {
                    if (_globalPromptIds.Contains(Prompts[i].Id))
                    {
                        Prompts.RemoveAt(i);
                    }
                }
                
                // 清空并重建 ID 集合
                _globalPromptIds.Clear();
                
                // 添加新的全局提示词
                foreach (var p in newGlobalPrompts)
                {
                    if (!Prompts.Any(x => x.Id == p.Id))
                    {
                        _globalPromptIds.Add(p.Id);
                        Prompts.Add(p);
                    }
                }
                
                // 恢复选中状态
                if (previouslySelectedId != Guid.Empty)
                {
                    SelectedPrompt = Prompts.FirstOrDefault(x => x.Id == previouslySelectedId);
                }
            });
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "[全局提示词] 同步失败");
        }
    }

    private void InitializeMcpClients()
    {
        try
        {
            // 清理现有客户端
            foreach (var client in _mcpClients)
            {
                client.Dispose();
            }
            _mcpClients.Clear();

            // 初始化连接管理器
            _connectionManager = McpConnectionManager.Instance(Context.Logger, Settings.LogLevel);
            
            // 初始化工具缓存
            _toolCache = new McpToolCache(Settings.McpToolCacheMinutes, Context.Logger, Settings.LogLevel);
            
            // 初始化并发工具执行器
            _toolExecutor = new ConcurrentToolExecutor(Settings.MaxConcurrentTools, Context.Logger, Settings.LogLevel);

            // 初始化连接池（如果不存在）
            _clientPool ??= new McpClientPool(Context.Logger, Settings.LogLevel);

            // 为每个启用的服务器创建客户端
            foreach (var server in Settings.McpServers.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Url)))
            {
                try
                {
                    var client = McpClientFactory.CreateClient(server, Context.Logger, Settings.LogLevel);
                    _mcpClients.Add(client);
                    if (ShouldLog(1))
                        Context.Logger.LogInformation("[MCP] 客户端初始化成功: {ServerName}", server.Name);
                }
                catch (Exception ex)
                {
                    if (ShouldLog(0))
                        Context.Logger.LogError("[MCP] 客户端初始化失败 {ServerName}: {Message}", server.Name, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            if (ShouldLog(0))
                Context.Logger.LogError("[MCP] 初始化MCP客户端失败: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 重新初始化MCP客户端（在设置变更后调用）
    /// </summary>
    public void ReinitializeMcpClient()
    {
        if (Settings.EnableMcp)
        {
            InitializeMcpClients();
        }
        else
        {
            foreach (var client in _mcpClients)
            {
                client.Dispose();
            }
            _mcpClients.Clear();
        }
    }

    public override void Dispose()
    {
        _viewModel?.Dispose();
        
        // [全局提示词] 取消注册回调
        _globalPromptsCallback?.Dispose();
        _globalPromptsCallback = null;
        
        // 释放连接池
        _clientPool?.Dispose();
        _clientPool = null;
        
        foreach (var client in _mcpClients)
        {
            client.Dispose();
        }
        _mcpClients.Clear();
        
        // 释放并发控制信号量
        _translationSemaphore?.Dispose();
    }

    public override async Task TranslateAsync(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken = default)
    {
        // 限制并发翻译请求数量
        await _translationSemaphore.WaitAsync(cancellationToken);
        
        try
        {
            await TranslateInternalAsync(request, result, cancellationToken);
        }
        finally
        {
            _translationSemaphore.Release();
        }
    }

    private async Task TranslateInternalAsync(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken)
    {
        // 检查是否为命令（以/开头且命令系统已启用）
        if (Settings.EnableCommandSystem && !string.IsNullOrWhiteSpace(request.Text) && request.Text.TrimStart().StartsWith("/"))
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

        if (GetSourceLanguage(request.SourceLang) is not string sourceStr)
        {
            result.Fail(Context.GetTranslation("UnsupportedSourceLang"));
            return;
        }
        if (GetTargetLanguage(request.TargetLang) is not string targetStr)
        {
            result.Fail(Context.GetTranslation("UnsupportedTargetLang"));
            return;
        }

        // 使用两层优先级获取实际策略
        var effectiveStrategy = GetEffectiveStrategy();
        
        // 根据有效策略决定翻译方式
        if (effectiveStrategy != McpToolStrategy.Disabled && Settings.EnableMcp && _mcpClients.Count > 0)
        {
            await TranslateWithMcpTools(request, result, effectiveStrategy, cancellationToken);
        }
        else
        {
            if (ShouldLog(1) && effectiveStrategy != McpToolStrategy.Disabled && (!Settings.EnableMcp || _mcpClients.Count == 0))
            {
                Context.Logger.LogInformation("[MCP策略] 策略非禁用但 MCP 未启用或无可用客户端，回退到传统 API");
            }
            await TranslateWithTraditionalApi(request, result, cancellationToken);
        }
    }

    private async Task TranslateWithMcpTools(TranslateRequest request, TranslateResult result, McpToolStrategy strategy, CancellationToken cancellationToken)
    {
        try
        {
            // 获取启用的服务器配置
            var enabledServers = Settings.McpServers.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Url)).ToList();
            
            if (enabledServers.Count == 0)
            {
                // 根据策略决定如何处理
                if (strategy == McpToolStrategy.ToolForced)
                {
                    result.Fail("没有可用的MCP服务器");
                    return;
                }
                // 其他策略：回退到普通API翻译
                if (ShouldLog(1))
                    Context.Logger.LogInformation("[MCP] 没有可用的MCP服务器，根据策略回退到普通翻译");
                await TranslateWithTraditionalApi(request, result, cancellationToken);
                return;
            }

            // 1. 构建请求消息
            var messages = (Prompts.FirstOrDefault(x => x.IsEnabled) ?? throw new Exception("请先完善Prompt配置"))
                .Clone()
                .Items;
            messages.ToList().ForEach(item =>
                item.Content = item.Content
                    .Replace("$source", request.SourceLang.ToString())
                    .Replace("$target", request.TargetLang.ToString())
                    .Replace("$content", request.Text)
            );

            var model = string.IsNullOrEmpty(Settings.Model) ? "deepseek-chat" : Settings.Model.Trim();
            var temperature = Math.Clamp(Settings.Temperature, 0, 2);
            
            // 使用策略级别的总工具调用上限（-1=无限，0=禁用，>0=允许次数）
            int maxToolCalls = Settings.StrategyConfigs.TryGetValue(strategy, out var config) 
                ? config.TotalToolCallsLimit 
                : StrategyTotalToolCallsHelper.DEFAULT_LIMIT;
            
            // -1 代表无上限
            if (maxToolCalls < 0) maxToolCalls = int.MaxValue;
            
            if (ShouldLog(1))
                Context.Logger.LogInformation("[MCP策略] 当前策略总工具调用上限: {Limit}", 
                    maxToolCalls == 0 ? "无限" : $"{maxToolCalls}次");

            UriBuilder uriBuilder = new(Settings.Url);
            if (uriBuilder.Path == "/")
                uriBuilder.Path = "/chat/completions";

            var option = new Options
            {
                Headers = new Dictionary<string, string>
                {
                    { "Authorization", "Bearer " + Settings.ApiKey },
                    { "Content-Type", "application/json" }
                }
            };

            // 记录所有工具调用（工具名称、状态）
            var toolChainList = new List<(string toolName, bool isSuccess)>();
            
            // 防止无限循环的计数器（按策略设置）
            var consecutiveToolCallCount = new Dictionary<string, int>();
            int maxConsecutiveToolCalls = Settings.StrategyConfigs.TryGetValue(strategy, out var config2) 
                ? config2.ConsecutiveToolLimit 
                : StrategyConsecutiveLimitHelper.DEFAULT_LIMIT;
            // -1 代表无上限
            if (maxConsecutiveToolCalls < 0) maxConsecutiveToolCalls = int.MaxValue;
            bool enableConsecutiveLimit = maxConsecutiveToolCalls >= 0;

            // 2. 初始化MCP并准备工具（所有策略都立即连接）
            var allMessages = new List<object>();
            var (systemPrompt, functionTools, enabledTools) = await InitializeMcpAndGetSystemPrompt(enabledServers, strategy, cancellationToken);
            
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                allMessages.Add(new { role = "system", content = systemPrompt });
            }
            foreach (var msg in messages)
            {
                allMessages.Add(new { role = msg.Role, content = msg.Content });
            }
            
            // 检查是否有可用工具
            if (functionTools.Count == 0)
            {
                if (strategy == McpToolStrategy.ToolForced)
                {
                    result.Fail("没有启用的MCP工具");
                    return;
                }
                // 其他策略：回退到普通API翻译
                if (ShouldLog(1))
                    Context.Logger.LogInformation("[MCP] 没有启用的MCP工具，根据策略回退到普通翻译");
                await TranslateWithTraditionalApi(request, result, cancellationToken);
                return;
            }

            // 3. 三段式流架构：LLM前置流 → MCP工具流 → LLM后置流
            var threeStageBuilder = new ThreeStageContentBuilder(result, Settings, strategy);
            int toolCallCount = 0;
            bool needMoreToolCalls = true;
            
            while (needMoreToolCalls && toolCallCount < maxToolCalls && !cancellationToken.IsCancellationRequested)
            {
                // 发送流式请求
                var requestBody = new
                {
                    model,
                    messages = allMessages,
                    temperature,
                    max_tokens = Settings.MaxTokens,
                    top_p = Settings.TopP,
                    n = Settings.N,
                    stream = true,
                    tools = functionTools.Count > 0 ? functionTools : null,
                    tool_choice = functionTools.Count > 0 ? "auto" : null
                };

                if (ShouldLog(1))
                    Context.Logger.LogInformation("[MCP] 流式对话请求 #{CallCount}，消息数: {MessageCount}", toolCallCount + 1, allMessages.Count);

                // 流式响应收集器
                var streamCollector = new StreamResponseCollector();
                
                // 流式收集AI响应并实时显示
                await Context.HttpService.StreamPostAsync(uriBuilder.Uri.ToString(), requestBody, chunk =>
                {
                    if (string.IsNullOrEmpty(chunk?.Trim()))
                        return;

                    var preprocessString = chunk.Replace("data:", "").Trim();
                    if (preprocessString.Equals("[DONE]"))
                        return;

                    try
                    {
                        var parsedData = JsonNode.Parse(preprocessString);
                        if (parsedData is null)
                            return;

                        var delta = parsedData["choices"]?[0]?["delta"];
                        var finishReason = parsedData["choices"]?[0]?["finish_reason"]?.ToString();
                        
                        // 实时显示AI回复内容（合并reasoning和content）
                        var reasoningChunk = delta?["reasoning_content"]?.ToString();
                        if (!string.IsNullOrEmpty(reasoningChunk))
                        {
                            streamCollector.AppendReasoning(reasoningChunk);
                            threeStageBuilder.AppendAIContent(reasoningChunk);
                        }
                        
                        // 同时收集content字段（如果有）
                        var contentChunk = delta?["content"]?.ToString();
                        if (!string.IsNullOrEmpty(contentChunk))
                        {
                            streamCollector.AppendContent(contentChunk);
                            threeStageBuilder.AppendAIContent(contentChunk);
                        }
                        
                        // 收集工具调用信息
                        if (delta?["tool_calls"] is JsonArray toolCallsDelta)
                        {
                            streamCollector.AccumulateToolCalls(toolCallsDelta);
                        }
                        
                        // 检测完成状态
                        if (finishReason == "tool_calls")
                        {
                            streamCollector.SetHasToolCalls(true);
                        }
                        else if (!string.IsNullOrEmpty(finishReason) && finishReason != "null")
                        {
                            streamCollector.SetFinishReason(finishReason);
                        }
                    }
                    catch
                    {
                        // 解析失败时忽略
                    }
                }, option, cancellationToken: cancellationToken);

                // 流式响应完成后处理
                var finishReason = streamCollector.GetFinishReason();
                var hasToolCalls = streamCollector.HasToolCalls();
                var accumulatedToolCalls = streamCollector.GetToolCalls();
                var reasoningContent = streamCollector.GetReasoningContent();
                var assistantMsgContent = streamCollector.GetContent();
                
                if (ShouldLog(1))
                {
                    Context.Logger.LogInformation("[MCP] 流式响应完成，finish_reason: {FinishReason}, hasToolCalls: {HasToolCalls}", 
                        finishReason, hasToolCalls);
                }

                // 检查是否需要调用工具
                if (hasToolCalls && accumulatedToolCalls.Count > 0)
                {
                    toolCallCount++;
                    if (ShouldLog(1))
                        Context.Logger.LogInformation("[MCP] AI决定调用工具，共 {Count} 个", accumulatedToolCalls.Count);

                    // 过滤掉DSML XML代码
                    if (assistantMsgContent.Contains("<｜DSML｜") || assistantMsgContent.Contains("function_calls"))
                    {
                        assistantMsgContent = "";
                    }
                    
                    // 将AI思考内容添加到消息历史
                    var toolCallsList = new List<object>();
                    foreach (var tc in accumulatedToolCalls)
                    {
                        var tcId = tc["id"]?.ToString() ?? "";
                        var tcType = tc["type"]?.ToString() ?? "function";
                        var tcFunc = tc["function"];
                        var tcFuncName = tcFunc?["name"]?.ToString() ?? "";
                        var tcFuncArgs = tcFunc?["arguments"]?.ToString() ?? "{}";
                        
                        toolCallsList.Add(new
                        {
                            id = tcId,
                            type = tcType,
                            function = new
                            {
                                name = tcFuncName,
                                arguments = tcFuncArgs
                            }
                        });
                    }
                    
                    allMessages.Add(new
                    {
                        role = "assistant",
                        content = assistantMsgContent,
                        reasoning_content = reasoningContent,
                        tool_calls = toolCallsList
                    });

                    // 准备工具调用任务
                    var currentToolNames = new HashSet<string>();
                    
                    foreach (var tc in accumulatedToolCalls)
                    {
                        var tcObj = tc!;
                        var functionToken = tcObj["function"];
                        var nameToken = functionToken?["name"];
                        var tn = nameToken?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(tn))
                        {
                            currentToolNames.Add(tn);
                            
                            // 检查是否是连续调用
                            if (!consecutiveToolCallCount.ContainsKey(tn))
                            {
                                consecutiveToolCallCount[tn] = 0;
                            }
                        }
                    }
                    
                    // 检查是否所有工具都达到了连续调用上限
                    bool allToolsExceededLimit = true;
                    foreach (var toolName in currentToolNames)
                    {
                        if (!enableConsecutiveLimit || 
                            !consecutiveToolCallCount.ContainsKey(toolName) || 
                            consecutiveToolCallCount[toolName] < maxConsecutiveToolCalls)
                        {
                            allToolsExceededLimit = false;
                            break;
                        }
                    }
                    
                    // 如果所有工具都达到了连续调用上限，强制退出并让AI直接回答
                    if (allToolsExceededLimit && currentToolNames.Count > 0)
                    {
                        if (ShouldLog(1))
                            Context.Logger.LogInformation("[MCP] 所有工具达到连续调用上限({Max}次)，强制退出工具循环", maxConsecutiveToolCalls);
                        
                        // 为每个工具调用添加失败响应（标记为❎）
                        foreach (var tc in accumulatedToolCalls)
                        {
                            var tcObj = tc!;
                            var tcId = tcObj["id"];
                            var toolCallId = tcId?.ToString() ?? string.Empty;
                            var tcFuncName = tcObj["function"]?["name"];
                            var toolName = tcFuncName?.ToString() ?? "unknown";
                            
                            if (!string.IsNullOrEmpty(toolCallId))
                            {
                                toolChainList.Add((toolName, false));
                                allMessages.Add(new
                                {
                                    role = "tool",
                                    content = $"[调用被截断] 工具 '{toolName}' 已达到连续调用上限({maxConsecutiveToolCalls}次)，请直接回答用户问题。",
                                    tool_call_id = toolCallId
                                });
                            }
                        }
                        
                        // 重置所有工具的连续调用计数
                        foreach (var toolName in currentToolNames)
                        {
                            consecutiveToolCallCount[toolName] = 0;
                        }
                        
                        // 继续下一轮（这一轮会让AI直接回答，不再调用工具）
                        continue;
                    }

                    // 准备并发执行工具调用
                    var toolCallTasks = new List<ToolCallTask>();
                    var taskIndex = 0;
                    
                    foreach (var toolCall in accumulatedToolCalls)
                    {
                        var toolCallObj = toolCall!;
                        var tcFunction = toolCallObj["function"];
                        var tcFuncName = tcFunction?["name"];
                        var toolName = tcFuncName?.ToString() ?? string.Empty;
                        var tcFuncArgs = tcFunction?["arguments"];
                        var toolArguments = tcFuncArgs?.ToString() ?? string.Empty;
                        var tcId = toolCallObj["id"];
                        var toolCallId = tcId?.ToString() ?? string.Empty;

                        if (string.IsNullOrEmpty(toolName) || string.IsNullOrEmpty(toolCallId))
                            continue;

                        // 检查工具是否被启用
                        var toolConfig = enabledServers
                            .SelectMany(s => s.Tools)
                            .FirstOrDefault(t => t.Name == toolName);
                            
                        if (toolConfig != null && !toolConfig.Enabled)
                        {
                            if (ShouldLog(0))
                                Context.Logger.LogWarning("[MCP] 工具 {ToolName} 已被禁用", toolName);
                            
                            toolChainList.Add((toolName, false));
                            allMessages.Add(new
                            {
                                role = "tool",
                                content = $"Error: Tool '{toolName}' is disabled by user configuration.",
                                tool_call_id = toolCallId
                            });
                            continue;
                        }

                        // 检查连续调用次数
                        if (enableConsecutiveLimit)
                        {
                            if (!consecutiveToolCallCount.ContainsKey(toolName))
                                consecutiveToolCallCount[toolName] = 0;
                                
                            consecutiveToolCallCount[toolName]++;
                            
                            if (consecutiveToolCallCount[toolName] > maxConsecutiveToolCalls)
                            {
                                if (ShouldLog(0))
                                    Context.Logger.LogWarning("[MCP] 工具 {ToolName} 连续调用超过{Max}次上限", toolName, maxConsecutiveToolCalls);
                                
                                toolChainList.Add((toolName, false));
                                allMessages.Add(new
                                {
                                    role = "tool",
                                    content = $"Error: Tool '{toolName}' has been called consecutively {maxConsecutiveToolCalls} times.",
                                    tool_call_id = toolCallId
                                });
                                continue;
                            }
                        }

                        // 解析参数
                        object? arguments = null;
                        if (!string.IsNullOrEmpty(toolArguments))
                        {
                            try
                            {
                                arguments = JsonSerializer.Deserialize<object>(toolArguments);
                            }
                            catch
                            {
                                arguments = toolArguments;
                            }
                        }

                        // 找到对应的服务器客户端
                        var serverClient = enabledTools.FirstOrDefault(t => t.Tool.Name == toolName).Client;
                        
                        // 添加到任务列表
                        toolCallTasks.Add(new ToolCallTask
                        {
                            ToolName = toolName,
                            ToolCallId = toolCallId,
                            Arguments = arguments,
                            Client = serverClient,
                            Index = taskIndex++
                        });
                    }

                    // 三段式流：开始工具调用，在AI回复中内联显示工具名
                    foreach (var task in toolCallTasks)
                    {
                        threeStageBuilder.StartToolCall(task.ToolName);
                    }

                    // 并发执行所有工具调用
                    var toolResults = await ExecuteToolsConcurrentAsync(
                        toolCallTasks, 
                        enabledServers, 
                        toolCallCount,
                        consecutiveToolCallCount,
                        enableConsecutiveLimit,
                        maxConsecutiveToolCalls);

                    // 处理工具结果并更新三段式显示
                    foreach (var toolResult in toolResults)
                    {
                        // 记录工具调用结果
                        toolChainList.Add((toolResult.ToolName, toolResult.IsSuccess));
                        
                        // 更新三段式流中的工具执行结果
                        threeStageBuilder.FinalizeToolResult(toolResult.IsSuccess, 
                            toolResult.Result.Length > 100 ? toolResult.Result[..100] + "..." : toolResult.Result);

                        // 添加工具结果到对话历史
                        allMessages.Add(new
                        {
                            role = "tool",
                            content = toolResult.Result,
                            tool_call_id = toolResult.ToolCallId
                        });

                        if (ShouldLog(1))
                            Context.Logger.LogInformation("[MCP] 工具 {ToolName} 执行{Status}", 
                                toolResult.ToolName, toolResult.IsSuccess ? "成功" : "失败");
                    }
                    
                    // 继续下一轮对话（AI会继续输出回复，工具名已内联显示）
                    continue;
                }
                else
                {
                    // AI不再调用工具，准备返回最终结果
                    needMoreToolCalls = false;

                    // 【工具强制策略检查】如果策略是ToolForced但AI没有调用任何工具，报错
                    if (strategy == McpToolStrategy.ToolForced && toolCallCount == 0)
                    {
                        Context.Logger.LogError("[MCP] 工具强制策略下AI未调用任何工具，这可能是由于：1)所有工具被禁用 2)AI忽略了强制提示");
                        result.Fail("❎ 工具强制策略执行失败\n\nAI未调用任何工具。可能原因：\n" +
                            "1. 所有MCP工具已被禁用 - 请在设置中启用至少一个工具\n" +
                            "2. AI忽略了强制提示 - 这是模型行为问题，建议重试\n\n" +
                            "建议：切换到[工具优先]或[混合判断]策略，让AI自行决定是否使用工具。");
                        return;
                    }

                    // 添加AI的最后一次回复到对话历史（如果有实际内容，不是XML代码）
                    var assistantContent = assistantMsgContent;
                    // 如果内容包含DSML function_calls标记，清空它，因为已经有tool_calls了
                    if (assistantContent.Contains("function_calls") || assistantContent.Contains("DSML"))
                    {
                        assistantContent = "";
                    }
                    
                    // 【工具强制策略】检查AI是否报告没有合适的工具
                    if (strategy == McpToolStrategy.ToolForced && 
                        (assistantContent.Contains("[NO_SUITABLE_TOOL]") || assistantContent.Contains("没有合适的工具")))
                    {
                        Context.Logger.LogError("[MCP] 工具强制策略下AI报告没有合适的工具");
                        result.Fail("❎ 工具强制策略执行失败\n\nAI判断当前可用的工具都无法回答您的问题。\n" +
                            "可用的工具与问题不匹配，因此无法继续。\n\n" +
                            "建议：\n" +
                            "1. 切换到[工具优先]或[混合判断]策略，允许AI直接回答\n" +
                            "2. 添加能够处理此类问题的MCP工具\n" +
                            "3. 修改问题，使其与现有工具的能力匹配");
                        return;
                    }
                    
                    allMessages.Add(new
                    {
                        role = "assistant",
                        content = assistantContent
                    });
                    
                    // AI流式传输结束，无需额外添加工具链（已在ThreeStageContentBuilder中实时显示）
                    if (ShouldLog(1))
                        Context.Logger.LogInformation("[MCP] 流式对话完成，共调用 {Count} 次工具", toolCallCount);
                }
            }

            // 如果达到最大调用次数限制
            if (toolCallCount >= maxToolCalls)
            {
                if (ShouldLog(0)) // 粗略及以上级别（Warning默认记录）
                    Context.Logger.LogWarning("[MCP] 达到最大工具调用次数限制 ({Max})", maxToolCalls);
                result.Text = $"[警告] 达到最大工具调用次数限制 ({maxToolCalls})\n\n" + result.Text;
            }
        }
        catch (Exception ex)
        {
            Context.Logger.LogError(ex, "[MCP] 翻译过程发生异常");
            result.Fail($"MCP翻译失败: {ex.Message}");
        }
    }

    private string ProcessThinkContent(string content)
    {
        // 移除<think>标签内容
        var startIndex = content.IndexOf("<think>");
        var endIndex = content.IndexOf("</think>");

        if (startIndex >= 0 && endIndex > startIndex)
        {
            return content.Substring(0, startIndex) + content.Substring(endIndex + 8).Trim();
        }

        return content;
    }

    /// <summary>
    /// 初始化MCP并获取系统提示词（使用连接池复用客户端）
    /// </summary>
    private async Task<(string systemPrompt, List<object> functionTools, List<(IMcpClient Client, string ServerName, McpTool Tool)> enabledTools)>
        InitializeMcpAndGetSystemPrompt(List<McpServerConfig> enabledServers, McpToolStrategy strategy, CancellationToken cancellationToken)
    {
        // 确保连接池已初始化
        _clientPool ??= new McpClientPool(Context.Logger, Settings.LogLevel);
        
        // 清理现有客户端列表（使用连接池后，这里只存储引用，不拥有所有权）
        _mcpClients.Clear();
        
        // 从连接池获取客户端
        foreach (var server in enabledServers)
        {
            try
            {
                var client = await _clientPool.GetClientAsync(server, cancellationToken);
                _mcpClients.Add(client);
                if (ShouldLog(1))
                    Context.Logger.LogInformation("[MCP] 从连接池获取客户端: {ServerName}", server.Name);
            }
            catch (Exception ex)
            {
                if (ShouldLog(0))
                    Context.Logger.LogError("[MCP] 获取客户端失败 {ServerName}: {Message}", server.Name, ex.Message);
            }
        }
        
        // 收集工具（客户端已从连接池获取并连接）
        var allTools = new List<(IMcpClient Client, string ServerName, McpTool Tool)>();
        
        foreach (var client in _mcpClients)
        {
            IMcpClient currentClient = client;
            try
            {
                // 从连接池获取的客户端已连接，直接获取工具列表
                var tools = await currentClient.ListToolsAsync(cancellationToken);
                var serverName = enabledServers.FirstOrDefault(s => _mcpClients.Any(c => c == currentClient))?.Name ?? "Unknown";
                
                if (ShouldLog(1))
                    Context.Logger.LogInformation("[MCP] 服务器 {ServerName} 获取到 {Count} 个工具", serverName, tools.Count);
                
                foreach (var tool in tools)
                {
                    allTools.Add((currentClient, serverName, tool));
                }
            }
            catch (Exception ex)
            {
                if (ShouldLog(0))
                    Context.Logger.LogError("[MCP] 获取工具列表失败: {Message}", ex.Message);
            }
        }
        
        // 根据用户配置过滤启用的工具
        var enabledTools = allTools.Where(item => 
        {
            var serverConfig = enabledServers.FirstOrDefault(s => s.Name == item.ServerName);
            if (serverConfig == null) return true;
            
            var config = serverConfig.Tools.FirstOrDefault(t => t.Name == item.Tool.Name);
            return config?.Enabled ?? true;
        }).ToList();
        
        if (ShouldLog(1))
            Context.Logger.LogInformation("[MCP] 过滤后启用 {Count} 个工具，禁用 {DisabledCount} 个工具", 
                enabledTools.Count, allTools.Count - enabledTools.Count);

        // 转换为OpenAI Function Calling格式
        var functionTools = enabledTools.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Tool.Name,
                description = t.Tool.Description,
                parameters = JsonNode.Parse(t.Tool.InputSchema)
            }
        }).Cast<object>().ToList();

        // 获取系统提示词
        string systemPrompt = GetSystemPromptByStrategy(strategy, enabledTools);
        
        return (systemPrompt, functionTools, enabledTools);
    }

    /// <summary>
    /// 根据策略获取系统提示词（支持自定义提示词）
    /// </summary>
    private string GetSystemPromptByStrategy(McpToolStrategy strategy, List<(IMcpClient Client, string ServerName, McpTool Tool)> enabledTools)
    {
        // 如果策略是禁用，直接返回空
        if (strategy == McpToolStrategy.Disabled)
            return string.Empty;
        
        // 生成两种格式的工具描述
        // rough: 工具名称 + 参数定义
        var descriptionRough = enabledTools.Any()
            ? string.Join("\n\n", enabledTools.Select(t => FormatToolWithParameters(t.Tool.Name, t.Tool.InputSchema)))
            : "（当前没有启用的MCP工具）";
        
        // detailed: 工具名称 + 参数定义 + 描述
        var descriptionDetailed = enabledTools.Any()
            ? string.Join("\n\n", enabledTools.Select(t => FormatToolWithParameters(t.Tool.Name, t.Tool.InputSchema, t.Tool.Description)))
            : "（当前没有启用的MCP工具）";

        // 获取自定义提示词或默认提示词
        string prompt;
        if (Settings.StrategyConfigs.TryGetValue(strategy, out var config) && !string.IsNullOrWhiteSpace(config.CustomPrompt))
        {
            prompt = config.CustomPrompt;
        }
        else
        {
            prompt = DefaultStrategyPrompts.GetDefaultPrompt(strategy);
        }

        // 替换变量占位符
        return prompt
            .Replace("$description_rough", descriptionRough)
            .Replace("$description_detailed", descriptionDetailed);
    }
    
    /// <summary>
    /// 格式化工具及其参数信息
    /// </summary>
    private string FormatToolWithParameters(string toolName, string inputSchema, string? description = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"- {toolName}");
        
        // 解析参数定义
        if (!string.IsNullOrEmpty(inputSchema) && inputSchema != "{}")
        {
            try
            {
                var schema = JsonNode.Parse(inputSchema);
                if (schema?["properties"] is JsonObject properties)
                {
                    sb.AppendLine("  参数:");
                    foreach (var prop in properties)
                    {
                        var paramName = prop.Key;
                        var paramDesc = prop.Value?["description"]?.ToString() ?? "";
                        var paramType = prop.Value?["type"]?.ToString() ?? "any";
                        sb.AppendLine($"    - {paramName} ({paramType}): {paramDesc}");
                    }
                }
                
                // 显示必需参数
                if (schema?["required"] is JsonArray required && required.Count > 0)
                {
                    sb.AppendLine($"  必需参数: {string.Join(", ", required.Select(r => r?.ToString()))}");
                }
            }
            catch
            {
                // 如果解析失败，显示原始 schema
                sb.AppendLine($"  参数定义: {inputSchema}");
            }
        }
        
        // 如果是 detailed 模式，添加描述
        if (!string.IsNullOrEmpty(description))
        {
            sb.AppendLine($"  描述: {description}");
        }
        
        return sb.ToString().TrimEnd();
    }

    private async Task TranslateWithTraditionalApi(TranslateRequest request, TranslateResult result, CancellationToken cancellationToken)
    {
        UriBuilder uriBuilder = new(Settings.Url);
        if (uriBuilder.Path == "/")
            uriBuilder.Path = "/chat/completions";

        var model = string.IsNullOrEmpty(Settings.Model) ? "deepseek-chat" : Settings.Model.Trim();

        var messages = (Prompts.FirstOrDefault(x => x.IsEnabled) ?? throw new Exception("请先完善Prompt配置"))
            .Clone()
            .Items;
        messages.ToList().ForEach(item =>
            item.Content = item.Content
                .Replace("$source", request.SourceLang.ToString())
                .Replace("$target", request.TargetLang.ToString())
                .Replace("$content", request.Text)
        );

        var temperature = Math.Clamp(Settings.Temperature, 0, 2);

        var content = new
        {
            model,
            messages,
            temperature,
            max_tokens = Settings.MaxTokens,
            top_p = Settings.TopP,
            n = Settings.N,
            stream = Settings.Stream
        };

        var option = new Options
        {
            Headers = new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + Settings.ApiKey },
                { "Content-Type", "application/json" },
                { "Accept", "text/event-stream" }
            }
        };

        StringBuilder sb = new();
        var isThink = false;

        await Context.HttpService.StreamPostAsync(uriBuilder.Uri.ToString(), content, msg =>
        {
            if (string.IsNullOrEmpty(msg?.Trim()))
                return;

            var preprocessString = msg.Replace("data:", "").Trim();

            if (preprocessString.Equals("[DONE]"))
                return;

            try
            {
                var parsedData = JsonNode.Parse(preprocessString);

                if (parsedData is null)
                    return;

                var contentValue = parsedData["choices"]?[0]?["delta"]?["content"]?.ToString();

                if (string.IsNullOrEmpty(contentValue))
                    return;

                if (contentValue.Trim() == "<think>")
                    isThink = true;
                if (contentValue.Trim() == "</think>")
                {
                    isThink = false;
                    return;
                }

                if (isThink)
                    return;

                if (string.IsNullOrWhiteSpace(sb.ToString()) && string.IsNullOrWhiteSpace(contentValue))
                    return;

                sb.Append(contentValue);
                result.Text = sb.ToString();
            }
            catch
            {
            }
        }, option, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 流式响应收集器 - 用于收集流式响应中的思考内容和工具调用信息
    /// </summary>
    private class StreamResponseCollector
    {
        private readonly StringBuilder _reasoningBuilder = new();
        private readonly StringBuilder _contentBuilder = new();
        private readonly List<JsonNode> _toolCallDeltas = new();
        private bool _hasToolCalls = false;
        private string _finishReason = string.Empty;
        
        /// <summary>
        /// 追加思考内容
        /// </summary>
        public void AppendReasoning(string reasoning)
        {
            _reasoningBuilder.Append(reasoning);
        }
        
        /// <summary>
        /// 追加内容
        /// </summary>
        public void AppendContent(string content)
        {
            _contentBuilder.Append(content);
        }
        
        /// <summary>
        /// 累积工具调用信息
        /// </summary>
        public void AccumulateToolCalls(JsonArray toolCallsDelta)
        {
            foreach (var toolCall in toolCallsDelta)
            {
                if (toolCall != null)
                {
                    _toolCallDeltas.Add(toolCall.DeepClone());
                }
            }
        }
        
        /// <summary>
        /// 设置是否有工具调用
        /// </summary>
        public void SetHasToolCalls(bool hasToolCalls)
        {
            _hasToolCalls = hasToolCalls;
        }
        
        /// <summary>
        /// 设置完成原因
        /// </summary>
        public void SetFinishReason(string finishReason)
        {
            _finishReason = finishReason;
        }
        
        /// <summary>
        /// 获取思考内容
        /// </summary>
        public string GetReasoningContent() => _reasoningBuilder.ToString();
        
        /// <summary>
        /// 获取内容
        /// </summary>
        public string GetContent() => _contentBuilder.ToString();
        
        /// <summary>
        /// 是否有工具调用
        /// </summary>
        public bool HasToolCalls() => _hasToolCalls;
        
        /// <summary>
        /// 获取完成原因
        /// </summary>
        public string GetFinishReason() => _finishReason;
        
        /// <summary>
        /// 获取合并后的工具调用列表
        /// </summary>
        public List<JsonNode> GetToolCalls()
        {
            // 合并同一工具调用的增量数据
            var mergedToolCalls = new Dictionary<int, JsonNode>();
            
            foreach (var delta in _toolCallDeltas)
            {
                var index = delta["index"]?.GetValue<int>() ?? 0;
                
                if (!mergedToolCalls.ContainsKey(index))
                {
                    mergedToolCalls[index] = new JsonObject
                    {
                        ["id"] = delta["id"]?.DeepClone(),
                        ["type"] = delta["type"]?.DeepClone() ?? "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = string.Empty,
                            ["arguments"] = string.Empty
                        }
                    };
                }
                
                var existing = mergedToolCalls[index];
                var deltaFunction = delta["function"];
                
                if (deltaFunction != null)
                {
                    var existingFunction = existing["function"] as JsonObject;
                    if (existingFunction != null)
                    {
                        var nameChunk = deltaFunction["name"]?.ToString();
                        var argsChunk = deltaFunction["arguments"]?.ToString();
                        
                        if (!string.IsNullOrEmpty(nameChunk))
                        {
                            var currentName = existingFunction["name"]?.ToString() ?? string.Empty;
                            existingFunction["name"] = currentName + nameChunk;
                        }
                        
                        if (!string.IsNullOrEmpty(argsChunk))
                        {
                            var currentArgs = existingFunction["arguments"]?.ToString() ?? string.Empty;
                            existingFunction["arguments"] = currentArgs + argsChunk;
                        }
                    }
                }
            }
            
            return mergedToolCalls.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
        }
    }

    /// <summary>
    /// 内容构建器 - 支持4种工具结果显示模式
    /// 模式：Disabled/Minimal/Mixed/Detailed
    /// </summary>
    private class ThreeStageContentBuilder
    {
        private readonly TranslateResult _result;
        private readonly Settings _settings;
        private readonly McpToolStrategy _strategy;
        private readonly StringBuilder _aiContent = new();          // AI回复内容
        private readonly List<ToolCallInfo> _toolCalls = new();      // 工具调用信息
        private string _lastDisplayText = "";                       // 用于防止闪烁
        private bool _hasPendingToolTag = false;                    // 是否有待插入的工具标记
        private string _pendingToolName = "";                       // 待插入的工具名
        
        private class ToolCallInfo
        {
            public string ToolName { get; set; } = "";
            public string Status { get; set; } = "⏳";  // ⏳/✅/❎
            public string Result { get; set; } = "";
        }
        
        public ThreeStageContentBuilder(TranslateResult result, Settings settings, McpToolStrategy strategy)
        {
            _result = result;
            _settings = settings;
            _strategy = strategy;
        }
        
        /// <summary>
        /// 获取当前策略的工具结果显示模式
        /// </summary>
        private ToolResultDisplayMode GetToolResultDisplayMode()
        {
            if (_settings.StrategyConfigs.TryGetValue(_strategy, out var config))
            {
                return config.ToolResultDisplayMode;
            }
            return ToolResultDisplayMode.Disabled; // 默认禁用结果
        }
        
        /// <summary>
        /// 获取当前策略的工具链显示开关
        /// </summary>
        private bool GetToolChainDisplay()
        {
            if (_settings.StrategyConfigs.TryGetValue(_strategy, out var config))
            {
                return config.ToolChainDisplay;
            }
            return false; // 默认关闭
        }
        
        /// <summary>
        /// 追加AI回复内容
        /// </summary>
        public void AppendAIContent(string text)
        {
            var mode = GetToolResultDisplayMode();
            
            // 如果不是Disabled模式，显示内联工具名
            if (mode != ToolResultDisplayMode.Disabled)
            {
                // 如果有待插入的工具标记，先插入它
                if (_hasPendingToolTag && !string.IsNullOrEmpty(_pendingToolName))
                {
                    _aiContent.Append($"「{_pendingToolName}✅」");
                    _hasPendingToolTag = false;
                    _pendingToolName = "";
                }
            }
            
            _aiContent.Append(text);
            RefreshDisplay();
        }
        
        /// <summary>
        /// 开始工具调用（在AI回复中标记位置）
        /// </summary>
        public void StartToolCall(string toolName)
        {
            var mode = GetToolResultDisplayMode();
            
            // 记录工具调用
            _toolCalls.Add(new ToolCallInfo
            {
                ToolName = toolName,
                Status = "⏳"
            });
            
            // 只有在非Disabled模式下才标记插入工具名
            if (mode != ToolResultDisplayMode.Disabled)
            {
                // 标记需要在下次AppendAIContent时插入工具名
                _hasPendingToolTag = true;
                _pendingToolName = toolName;
            }
            
            RefreshDisplay();
        }
        
        /// <summary>
        /// 完成工具执行
        /// </summary>
        public void FinalizeToolResult(bool isSuccess, string result)
        {
            if (_toolCalls.Count > 0)
            {
                var tool = _toolCalls[^1];
                tool.Status = isSuccess ? "✅" : "❎";
                tool.Result = result;
                RefreshDisplay();
            }
        }
        
        /// <summary>
        /// 刷新整个显示（带闪烁保护）
        /// </summary>
        private void RefreshDisplay()
        {
            var newDisplay = BuildDisplayText();
            
            // 只有内容真正变化时才更新UI，防止闪烁
            if (newDisplay != _lastDisplayText)
            {
                _result.Text = newDisplay;
                _lastDisplayText = newDisplay;
            }
        }
        
        /// <summary>
        /// 构建显示文本
        /// </summary>
        private string BuildDisplayText()
        {
            var display = new StringBuilder();
            var mode = GetToolResultDisplayMode();
            
            // AI回复内容（包含内联工具名）
            if (_aiContent.Length > 0)
            {
                display.Append(_aiContent.ToString().TrimEnd());
            }
            
            // 如果有待插入的工具标记且AI内容已结束，直接追加
            if (mode != ToolResultDisplayMode.Disabled && 
                _hasPendingToolTag && !string.IsNullOrEmpty(_pendingToolName))
            {
                display.Append($"「{_pendingToolName}✅」");
            }
            
            // 工具链和工具结果显示（仅在非Disabled和非Minimal模式下显示模块）
            if (_toolCalls.Count > 0 && mode != ToolResultDisplayMode.Disabled && mode != ToolResultDisplayMode.Minimal)
            {
                if (display.Length > 0) display.AppendLine();
                display.AppendLine();
                display.AppendLine("---");
                
                // 获取策略级别的工具链显示设置
                var enableToolChain = GetToolChainDisplay();
                
                // 工具链显示（单行，用➡️连接）
                if (enableToolChain)
                {
                    var chainParts = new List<string>();
                    foreach (var tool in _toolCalls)
                    {
                        chainParts.Add($"「{tool.ToolName}」{tool.Status}");
                    }
                    display.AppendLine($"> ⛓️ 工具链：{string.Join("➡️", chainParts)}");
                }
                
                // 工具结果显示
                if (enableToolChain) display.AppendLine();
                display.AppendLine("> 🔧 工具结果：");
                
                foreach (var tool in _toolCalls)
                {
                    if (mode == ToolResultDisplayMode.Mixed)
                    {
                        // 混合显示：部分结果，省略号截断（中等详细）
                        var result = tool.Result;
                        if (result.Length > 150)
                        {
                            result = result[..150] + "...";
                        }
                        display.AppendLine($">   [{tool.Status}] {tool.ToolName}: {result}");
                    }
                    else if (mode == ToolResultDisplayMode.Detailed)
                    {
                        // 详细显示：完整结果
                        display.AppendLine($">   [{tool.Status}] {tool.ToolName}:");
                        // 处理多行结果
                        var lines = tool.Result.Split('\n');
                        foreach (var line in lines)
                        {
                            display.AppendLine($">     {line}");
                        }
                    }
                }
                
                display.AppendLine("---");
            }
            
            return display.ToString().TrimEnd();
        }
        
        /// <summary>
        /// 获取完整内容
        /// </summary>
        public string GetFullContent()
        {
            return BuildDisplayText();
        }
    }

    #region Default Strategy Prompts

    /// <summary>
    /// 默认策略提示词（用于重置和默认值）
    /// </summary>
    public static class DefaultStrategyPrompts
    {
        /// <summary>
        /// 获取默认提示词
        /// </summary>
        public static string GetDefaultPrompt(McpToolStrategy strategy) => strategy switch
        {
            McpToolStrategy.Blank => Blank,
            McpToolStrategy.Hybrid => Hybrid,
            McpToolStrategy.ToolFirst => ToolFirst,
            McpToolStrategy.ToolForced => ToolForced,
            _ => string.Empty
        };

        /// <summary>
        /// 空白策略 - 仅列出工具
        /// </summary>
        public const string Blank = @"Available MCP tools:
$description_rough";

        /// <summary>
        /// 混合判断 - AI自行决定是否使用工具
        /// </summary>
        public const string Hybrid = @"You are a helpful assistant with access to optional MCP tools.

Available tools:
$description_detailed

Instructions:
- Tools are OPTIONAL - use them only when they can genuinely help answer the question
- For general knowledge, common sense, or simple questions - answer directly WITHOUT tools
- If no tool fits the request, answer directly with your own knowledge
- IMPORTANT: If a tool returns empty results, errors, or no useful data, you MUST fall back to answering with your own knowledge completely and accurately. Do not mention the tool failure or the attempt to use tools unless specifically asked.";

        /// <summary>
        /// 工具优先 - 优先使用工具
        /// </summary>
        public const string ToolFirst = @"You are a helpful assistant with access to MCP tools.

Available tools:
$description_detailed

Instructions:
- PRIORITIZE using tools when they can provide better or more accurate information
- If no suitable tool is available, answer directly using your own knowledge
- If a tool returns empty results, errors, or no useful data, you MUST use your own knowledge to answer the question completely and accurately. You MUST explicitly mention that the tool returned empty/error results and that you are using your own knowledge instead.";

        /// <summary>
        /// 工具强制 - 必须使用工具
        /// </summary>
        public const string ToolForced = @"You are a helpful assistant that MUST use available MCP tools.

Available tools:
$description_detailed

CRITICAL INSTRUCTIONS:
- You MUST use tools to answer the question
- If no suitable tool exists, reply: 'No suitable tool available to answer this question.'
- Do not answer from your own knowledge unless explicitly instructed";
    }

    #endregion

    #region Command System

    /// <summary>
    /// 命令执行结果
    /// </summary>
    private class CommandResult
    {
        public bool IsCommand { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// 策略名称映射（中英文对照）
    /// </summary>
    private static readonly Dictionary<string, McpToolStrategy> EnglishStrategyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["disabled"] = McpToolStrategy.Disabled,
        ["blank"] = McpToolStrategy.Blank,
        ["hybrid"] = McpToolStrategy.Hybrid,
        ["toolfirst"] = McpToolStrategy.ToolFirst,
        ["toolforced"] = McpToolStrategy.ToolForced
    };

    private static readonly Dictionary<string, McpToolStrategy> ChineseStrategyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["禁用服务"] = McpToolStrategy.Disabled,
        ["空白策略"] = McpToolStrategy.Blank,
        ["混合判断"] = McpToolStrategy.Hybrid,
        ["工具优先"] = McpToolStrategy.ToolFirst,
        ["工具强制"] = McpToolStrategy.ToolForced
    };

    private static readonly string[] AllStrategiesEn = ["disabled", "blank", "hybrid", "toolfirst", "toolforced"];
    private static readonly string[] AllStrategiesZh = ["禁用服务", "空白策略", "混合判断", "工具优先", "工具强制"];

    /// <summary>
    /// 执行命令
    /// </summary>
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
            "/mcp" => ExecuteToggleMcpCommand(),
            "/chain" or "/工具链" => ExecuteToggleToolChainCommand(),
            "/result" or "/工具结果" => ExecuteToolResultCommand(argument),
            _ => new CommandResult { IsCommand = true, Success = false, Message = $"❎ 未知命令: \"{command}\"\n可用命令: /当前, /切换, /状态, /mcp, /工具链, /工具结果, /帮助" }
        };

        return Task.FromResult(result);
    }

    /// <summary>
    /// /now /当前 - 查看当前策略
    /// </summary>
    private CommandResult ExecuteNowCommand()
    {
        var currentPrompt = SelectedPrompt;
        if (currentPrompt == null)
        {
            return new CommandResult { IsCommand = true, Success = true, Message = "当前未选择提示词\n默认策略: 禁用服务" };
        }

        var strategy = Settings.PromptStrategyMap.TryGetValue(currentPrompt.Name, out var s) ? s : McpToolStrategy.Disabled;
        var strategyName = PromptStrategyHelper.GetStrategyName(strategy);
        
        // 获取当前策略的工具结果模式和工具链显示状态
        var toolResultMode = Settings.StrategyConfigs.TryGetValue(strategy, out var config) ? config.ToolResultDisplayMode : ToolResultDisplayMode.Disabled;
        var toolChainEnabled = Settings.StrategyConfigs.TryGetValue(strategy, out var config2) && config2.ToolChainDisplay;
        
        return new CommandResult 
        { 
            IsCommand = true, 
            Success = true, 
            Message = $"当前提示词: {currentPrompt.Name}\n绑定策略: {strategyName}\n工具结果: {GetToolResultDisplayModeName(toolResultMode)}\n工具链显示: {(toolChainEnabled ? "✅ 开启" : "❎ 关闭")}" 
        };
    }

    /// <summary>
    /// 获取工具结果显示模式的中文名称
    /// </summary>
    private string GetToolResultDisplayModeName(ToolResultDisplayMode mode) => mode switch
    {
        ToolResultDisplayMode.Disabled => "禁用结果",
        ToolResultDisplayMode.Minimal => "粗略结果",
        ToolResultDisplayMode.Mixed => "混合显示",
        ToolResultDisplayMode.Detailed => "详细结果",
        _ => "禁用结果"
    };

    /// <summary>
    /// /status /状态 - 查看MCP状态
    /// </summary>
    private CommandResult ExecuteStatusCommand()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== MCP 服务状态 ===");
        sb.AppendLine($"MCP服务: {(Settings.EnableMcp ? "已启用" : "已禁用")}");
        
        // 显示当前提示词的工具结果显示模式和工具链状态
        var currentPrompt = SelectedPrompt;
        if (currentPrompt != null)
        {
            var strategy = Settings.PromptStrategyMap.TryGetValue(currentPrompt.Name, out var s) ? s : McpToolStrategy.Disabled;
            var config = Settings.StrategyConfigs.TryGetValue(strategy, out var c) ? c : null;
            var mode = config?.ToolResultDisplayMode ?? ToolResultDisplayMode.Disabled;
            var toolChainEnabled = config?.ToolChainDisplay ?? false;
            sb.AppendLine($"当前策略工具结果: {GetToolResultDisplayModeName(mode)}");
            sb.AppendLine($"当前策略工具链显示: {(toolChainEnabled ? "开启" : "关闭")}");
        }
        sb.AppendLine();
        
        // 统计启用/禁用的服务器数量
        var enabledServerCount = Settings.McpServers.Count(s => s.Enabled);
        sb.AppendLine($"MCP服务器: {enabledServerCount}/{Settings.McpServers.Count} 已启用");
        
        // 显示所有服务器（包括禁用的），保持配置顺序
        var isFirstServer = true;
        foreach (var server in Settings.McpServers)
        {
            if (!isFirstServer)
            {
                sb.AppendLine("---");
            }
            isFirstServer = false;
            
            // 服务器状态文本：已启用或已关闭
            var serverStatusText = server.Enabled ? "已启用" : "已关闭";
            
            // 服务器禁用时，所有工具都显示为禁用状态
            var enabledToolsCount = server.Enabled ? server.Tools.Count(t => t.Enabled) : 0;
            
            sb.AppendLine($"  - {server.Name}（{serverStatusText}）: {enabledToolsCount}/{server.Tools.Count}个工具");
            
            // 按工具列表顺序显示所有工具
            // 服务器禁用时，所有工具都显示为❎
            foreach (var tool in server.Tools)
            {
                var toolEnabled = server.Enabled && tool.Enabled;
                sb.AppendLine($"    {tool.Name}{(toolEnabled ? "✅" : "❎")}");
            }
        }

        return new CommandResult { IsCommand = true, Success = true, Message = sb.ToString() };
    }

    /// <summary>
    /// /mcp - 切换MCP服务功能开关
    /// </summary>
    private CommandResult ExecuteToggleMcpCommand()
    {
        Settings.EnableMcp = !Settings.EnableMcp;
        Context.SaveSettingStorage<Settings>();
        var status = Settings.EnableMcp ? "✅ 已启用" : "❎ 已禁用";
        return new CommandResult 
        { 
            IsCommand = true, 
            Success = true, 
            Message = $"MCP服务: {status}\n提示: 当前提示词的策略仍会继续生效" 
        };
    }

    /// <summary>
    /// /chain /工具链 - 切换当前策略的工具链显示
    /// </summary>
    private CommandResult ExecuteToggleToolChainCommand()
    {
        var currentPrompt = SelectedPrompt;
        if (currentPrompt == null)
        {
            return new CommandResult { IsCommand = true, Success = false, Message = "❎ 请先选择一个提示词" };
        }

        var strategy = Settings.PromptStrategyMap.TryGetValue(currentPrompt.Name, out var s) ? s : McpToolStrategy.Disabled;
        
        // 确保配置存在
        if (!Settings.StrategyConfigs.ContainsKey(strategy))
        {
            Settings.StrategyConfigs[strategy] = new McpStrategyConfig { Strategy = strategy };
        }
        
        var currentValue = Settings.StrategyConfigs[strategy].ToolChainDisplay;
        Settings.StrategyConfigs[strategy].ToolChainDisplay = !currentValue;
        Context.SaveSettingStorage<Settings>();
        
        var status = !currentValue ? "✅ 已启用" : "❎ 已禁用";
        return new CommandResult 
        { 
            IsCommand = true, 
            Success = true, 
            Message = $"当前策略工具链显示: {status}" 
        };
    }

    /// <summary>
    /// /result /工具结果 - 切换或查看当前策略的工具结果显示模式
    /// </summary>
    private CommandResult ExecuteToolResultCommand(string argument)
    {
        var currentPrompt = SelectedPrompt;
        if (currentPrompt == null)
        {
            return new CommandResult { IsCommand = true, Success = false, Message = "❎ 请先选择一个提示词" };
        }

        var strategy = Settings.PromptStrategyMap.TryGetValue(currentPrompt.Name, out var s) ? s : McpToolStrategy.Disabled;
        
        // 如果没有参数，显示当前模式
        if (string.IsNullOrWhiteSpace(argument))
        {
            var currentMode = Settings.StrategyConfigs.TryGetValue(strategy, out var config) 
                ? config.ToolResultDisplayMode 
                : ToolResultDisplayMode.Disabled;
            return new CommandResult 
            { 
                IsCommand = true, 
                Success = true, 
                Message = $"当前工具结果显示: {GetToolResultDisplayModeName(currentMode)}\n可用模式: 禁用, 粗略, 混合, 详细" 
            };
        }

        // 解析参数并切换模式
        var newMode = argument.ToLowerInvariant() switch
        {
            "禁用" or "disabled" => ToolResultDisplayMode.Disabled,
            "粗略" or "minimal" => ToolResultDisplayMode.Minimal,
            "混合" or "mixed" => ToolResultDisplayMode.Mixed,
            "详细" or "detailed" => ToolResultDisplayMode.Detailed,
            _ => (ToolResultDisplayMode?)null
        };

        if (newMode == null)
        {
            return new CommandResult 
            { 
                IsCommand = true, 
                Success = false, 
                Message = "❎ 无效的模式\n可用模式: 禁用, 粗略, 混合, 详细" 
            };
        }

        // 确保配置存在
        if (!Settings.StrategyConfigs.ContainsKey(strategy))
        {
            Settings.StrategyConfigs[strategy] = new McpStrategyConfig { Strategy = strategy };
        }
        
        Settings.StrategyConfigs[strategy].ToolResultDisplayMode = newMode.Value;
        Context.SaveSettingStorage<Settings>();
        
        return new CommandResult 
        { 
            IsCommand = true, 
            Success = true, 
            Message = $"✅ 工具结果显示已切换为: {GetToolResultDisplayModeName(newMode.Value)}" 
        };
    }

    /// <summary>
    /// /help /帮助 - 显示帮助信息
    /// </summary>
    private CommandResult ExecuteHelpCommand()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== MCP 命令系统 ===");
        sb.AppendLine();
        sb.AppendLine("【查看命令】");
        sb.AppendLine("  /now, /当前       - 查看当前提示词的策略、工具结果模式和工具链状态");
        sb.AppendLine("  /status, /状态    - 查看MCP服务状态和服务器列表");
        sb.AppendLine();
        sb.AppendLine("【切换策略】");
        sb.AppendLine("  /switch [策略], /切换 [策略]");
        sb.AppendLine("  英文: disabled, blank, hybrid, toolfirst, toolforced");
        sb.AppendLine("  中文: 禁用服务, 空白策略, 混合判断, 工具优先, 工具强制");
        sb.AppendLine("  示例: /switch hybrid  或  /切换 混合判断");
        sb.AppendLine();
        sb.AppendLine("【开关控制】");
        sb.AppendLine("  /mcp              - 开启或关闭MCP服务功能");
        sb.AppendLine("  /chain, /工具链   - 开启或关闭当前策略的工具链显示");
        sb.AppendLine();
        sb.AppendLine("【工具结果】");
        sb.AppendLine("  /result [模式], /工具结果 [模式]");
        sb.AppendLine("  模式: 禁用, 粗略, 混合, 详细");
        sb.AppendLine("  示例: /工具结果 混合");
        sb.AppendLine();
        sb.AppendLine("【帮助】");
        sb.AppendLine("  /help, /帮助      - 显示此帮助信息");
        sb.AppendLine();
        sb.AppendLine("注意: 命令必须以/开头，语言需一致（中文或英文）");

        return new CommandResult { IsCommand = true, Success = true, Message = sb.ToString() };
    }

    /// <summary>
    /// /switch /切换 [策略] - 切换当前提示词的MCP策略
    /// </summary>
    private CommandResult ExecuteSwitchCommand(string argument)
    {
        // 检查是否有参数
        if (string.IsNullOrWhiteSpace(argument))
        {
            return new CommandResult 
            { 
                IsCommand = true, 
                Success = false, 
                Message = "❎ 命令格式错误\n用法: /切换 [策略名]\n可用策略: 禁用服务, 空白策略, 混合判断, 工具优先, 工具强制" 
            };
        }

        // 检查当前提示词
        var currentPrompt = SelectedPrompt;
        if (currentPrompt == null)
        {
            return new CommandResult 
            { 
                IsCommand = true, 
                Success = false, 
                Message = "❎ 未选择提示词，无法切换策略" 
            };
        }

        // 解析策略名称
        McpToolStrategy? newStrategy = null;
        bool isEnglish = false;

        if (EnglishStrategyMap.TryGetValue(argument, out var enStrategy))
        {
            newStrategy = enStrategy;
            isEnglish = true;
        }
        else if (ChineseStrategyMap.TryGetValue(argument, out var zhStrategy))
        {
            newStrategy = zhStrategy;
            isEnglish = false;
        }

        if (!newStrategy.HasValue)
        {
            var availableStrategies = argument.All(c => c <= 127) 
                ? string.Join(", ", AllStrategiesEn)
                : string.Join(", ", AllStrategiesZh);
            
            return new CommandResult 
            { 
                IsCommand = true, 
                Success = false, 
                Message = $"❎ 未知策略: \"{argument}\"\n可用策略: {availableStrategies}" 
            };
        }

        // 保存旧策略用于比较
        var oldStrategy = Settings.PromptStrategyMap.TryGetValue(currentPrompt.Name, out var old) ? old : McpToolStrategy.Disabled;
        
        // 更新策略
        Settings.PromptStrategyMap[currentPrompt.Name] = newStrategy.Value;
        Context.SaveSettingStorage<Settings>();

        // 触发事件通知UI更新
        StrategyEvents.RaisePromptStrategyChanged(currentPrompt.Name, newStrategy.Value);

        var strategyName = PromptStrategyHelper.GetStrategyName(newStrategy.Value);
        var langIndicator = isEnglish ? "(EN)" : "(CN)";

        if (oldStrategy == newStrategy.Value)
        {
            return new CommandResult 
            { 
                IsCommand = true, 
                Success = true, 
                Message = $"提示词 '{currentPrompt.Name}' 当前已是\"{strategyName}\"策略 {langIndicator}" 
            };
        }

        return new CommandResult 
        { 
            IsCommand = true, 
            Success = true, 
            Message = $"✅ 已切换提示词 '{currentPrompt.Name}' 的策略\n从: {PromptStrategyHelper.GetStrategyName(oldStrategy)} → {strategyName} {langIndicator}" 
        };
    }

    #endregion

    #region 并发工具调用

    /// <summary>
    /// 工具调用任务信息
    /// </summary>
    private class ToolCallTask
    {
        public string ToolName { get; set; } = string.Empty;
        public string ToolCallId { get; set; } = string.Empty;
        public object? Arguments { get; set; }
        public IMcpClient? Client { get; set; }
        public int Index { get; set; }
    }

    /// <summary>
    /// 工具调用结果
    /// </summary>
    private class ToolCallResultInfo
    {
        public string ToolName { get; set; } = string.Empty;
        public string ToolCallId { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public int Index { get; set; }
    }

    /// <summary>
    /// 并发执行多个工具调用
    /// </summary>
    private async Task<List<ToolCallResultInfo>> ExecuteToolsConcurrentAsync(
        List<ToolCallTask> tasks,
        List<McpServerConfig> enabledServers,
        int toolCallCount,
        Dictionary<string, int> consecutiveToolCallCount,
        bool enableConsecutiveLimit,
        int maxConsecutiveToolCalls)
    {
        var results = new List<ToolCallResultInfo>(tasks.Count);
        var semaphore = new SemaphoreSlim(Settings.MaxConcurrentTools);
        var completionSource = new TaskCompletionSource<bool>();
        var completedCount = 0;

        if (ShouldLog(1))
            Context.Logger.LogInformation("[MCP] 开始并发执行 {Count} 个工具，最大并发数: {Max}", tasks.Count, Settings.MaxConcurrentTools);

        var executingTasks = tasks.Select(async task =>
        {
            await semaphore.WaitAsync();
            try
            {
                // 检查工具是否被禁用
                var toolConfig = enabledServers
                    .SelectMany(s => s.Tools)
                    .FirstOrDefault(t => t.Name == task.ToolName);
                    
                if (toolConfig != null && !toolConfig.Enabled)
                {
                    if (ShouldLog(0))
                        Context.Logger.LogWarning("[MCP] 工具 {ToolName} 已被禁用", task.ToolName);
                    
                    lock (results)
                    {
                        results.Add(new ToolCallResultInfo
                        {
                            ToolName = task.ToolName,
                            ToolCallId = task.ToolCallId,
                            Result = $"Error: Tool '{task.ToolName}' is disabled by user configuration.",
                            IsSuccess = false,
                            Index = task.Index
                        });
                    }
                    return;
                }

                // 检查连续调用次数
                if (enableConsecutiveLimit && consecutiveToolCallCount.ContainsKey(task.ToolName))
                {
                    if (consecutiveToolCallCount[task.ToolName] > maxConsecutiveToolCalls)
                    {
                        if (ShouldLog(0))
                            Context.Logger.LogWarning("[MCP] 工具 {ToolName} 连续调用超过{Max}次上限", task.ToolName, maxConsecutiveToolCalls);
                        
                        lock (results)
                        {
                            results.Add(new ToolCallResultInfo
                            {
                                ToolName = task.ToolName,
                                ToolCallId = task.ToolCallId,
                                Result = $"Error: Tool '{task.ToolName}' has been called consecutively {maxConsecutiveToolCalls} times.",
                                IsSuccess = false,
                                Index = task.Index
                            });
                        }
                        return;
                    }
                }

                // 检查客户端
                if (task.Client == null)
                {
                    if (ShouldLog(0))
                        Context.Logger.LogError("[MCP] 找不到工具 {ToolName} 对应的服务器客户端", task.ToolName);
                    
                    lock (results)
                    {
                        results.Add(new ToolCallResultInfo
                        {
                            ToolName = task.ToolName,
                            ToolCallId = task.ToolCallId,
                            Result = $"Error: Could not find server client for tool '{task.ToolName}'.",
                            IsSuccess = false,
                            Index = task.Index
                        });
                    }
                    return;
                }

                // 执行工具调用
                if (ShouldLog(1))
                    Context.Logger.LogInformation("[MCP] 第 {Round} 轮 - 执行工具: {ToolName}", toolCallCount, task.ToolName);

                try
                {
                    var toolResult = await task.Client.CallToolAsync(task.ToolName, task.Arguments ?? new { });
                    
                    if (ShouldLog(1))
                        Context.Logger.LogInformation("[MCP] 第 {Round} 轮 - 工具 {ToolName} 执行完成", toolCallCount, task.ToolName);

                    lock (results)
                    {
                        results.Add(new ToolCallResultInfo
                        {
                            ToolName = task.ToolName,
                            ToolCallId = task.ToolCallId,
                            Result = toolResult,
                            IsSuccess = true,
                            Index = task.Index
                        });
                    }
                }
                catch (Exception ex)
                {
                    if (ShouldLog(0))
                        Context.Logger.LogError("[MCP] 工具 {ToolName} 执行失败: {Error}", task.ToolName, ex.Message);
                    
                    lock (results)
                    {
                        results.Add(new ToolCallResultInfo
                        {
                            ToolName = task.ToolName,
                            ToolCallId = task.ToolCallId,
                            Result = $"Error: {ex.Message}",
                            IsSuccess = false,
                            Index = task.Index
                        });
                    }
                }
            }
            finally
            {
                semaphore.Release();
                
                // 检查是否所有任务都完成了
                Interlocked.Increment(ref completedCount);
                if (completedCount >= tasks.Count)
                {
                    completionSource.TrySetResult(true);
                }
            }
        }).ToArray();

        // 等待所有任务完成
        await Task.WhenAll(executingTasks);

        if (ShouldLog(1))
            Context.Logger.LogInformation("[MCP] 所有工具执行完成，成功: {Success}/{Total}", 
                results.Count(r => r.IsSuccess), results.Count);

        // 按原始顺序排序结果
        return results.OrderBy(r => r.Index).ToList();
    }

    #endregion
}
