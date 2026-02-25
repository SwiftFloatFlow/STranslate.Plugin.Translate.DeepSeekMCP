using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using STranslate.Plugin.Translate.DeepSeek.View;

namespace STranslate.Plugin.Translate.DeepSeek.ViewModel;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly IPluginContext _context;
    private readonly Settings _settings;
    private bool _isUpdating = false;
    private DispatcherTimer? _autoSaveTimer;
    private DispatcherTimer? _resultClearTimer;
    public Main Main { get; }
    
    /// <summary>
    /// 获取提示词的策略显示文本（用于下拉框显示）
    /// </summary>
    public string GetPromptStrategyTag(string promptName)
    {
        if (_settings.PromptStrategyMap.TryGetValue(promptName, out var strategy))
        {
            return PromptStrategyHelper.GetStrategyDisplayText(strategy);
        }
        return "[禁用服务]";
    }

    public SettingsViewModel(IPluginContext context, Settings settings, Main main)
    {
        _context = context;
        _settings = settings;
        Main = main;

        // 基础设置
        Url = _settings.Url;
        ApiKey = _settings.ApiKey;
        Model = _settings.Model;
        Models = new ObservableCollection<string>(_settings.Models);
        Temperature = _settings.Temperature;

        // MCP全局设置
        EnableMcp = _settings.EnableMcp;
        LogLevel = _settings.LogLevel;
        
        // 命令系统设置（独立）
        EnableCommandSystem = _settings.EnableCommandSystem;

        // 初始化服务器列表
        McpServers = new ObservableCollection<McpServerConfig>(_settings.McpServers);
        if (McpServers.Count == 0)
        {
            // 添加一个示例服务器
            AddNewServer();
        }
        
        // 设置当前选中服务器
        CurrentServerIndex = _settings.CurrentServerIndex;
        if (CurrentServerIndex < 0 || CurrentServerIndex >= McpServers.Count)
        {
            CurrentServerIndex = 0;
        }
        
        // 初始化当前服务器属性
        LoadCurrentServer();
        
        // 初始化日志级别选项
        LogLevelOptions = new ObservableCollection<string> { "粗略", "中等", "详细" };

        // 初始化提示词策略选项（5种具体策略，默认为Disabled）
        PromptStrategyOptions = new List<PromptStrategyOption>
        {
            new PromptStrategyOption(McpToolStrategy.Disabled),
            new PromptStrategyOption(McpToolStrategy.Blank),
            new PromptStrategyOption(McpToolStrategy.Hybrid),
            new PromptStrategyOption(McpToolStrategy.ToolFirst),
            new PromptStrategyOption(McpToolStrategy.ToolForced)
        };

        // 初始化当前提示词策略
        UpdateSelectedPromptStrategy();

        PropertyChanged += OnPropertyChanged;
        Models.CollectionChanged += OnModelsCollectionChanged;
        
        // 设置自动保存防抖
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _autoSaveTimer.Tick += (s, e) =>
        {
            _autoSaveTimer?.Stop();
            SaveSettings();
        };
        
        // 设置测试结果自动消失定时器（5秒）
        _resultClearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _resultClearTimer.Tick += (s, e) =>
        {
            _resultClearTimer?.Stop();
            McpValidateResult = string.Empty;
        };

        // 订阅 Main 的 PropertyChanged 事件，监听 SelectedPrompt 变化
        Main.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Main.SelectedPrompt))
            {
                UpdateSelectedPromptStrategy();
            }
        };

        // 订阅策略变更事件（用于命令系统与UI同步）
        StrategyEvents.Subscribe(this, OnPromptStrategyChanged);
    }

    /// <summary>
    /// 处理策略变更事件（当通过命令切换策略时更新UI）
    /// </summary>
    private void OnPromptStrategyChanged(object? sender, PromptStrategyChangedEventArgs e)
    {
        // 只有当当前显示的提示词与被修改的提示词相同时才更新UI
        if (Main.SelectedPrompt?.Name == e.PromptName)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SelectedPromptStrategy = PromptStrategyOptions.FirstOrDefault(o => o.Strategy == e.NewStrategy) ?? PromptStrategyOptions.First();
                UpdateStrategyDisplayText();
            });
        }
    }

    private void LoadCurrentServer()
    {
        if (CurrentServerIndex >= 0 && CurrentServerIndex < McpServers.Count)
        {
            var server = McpServers[CurrentServerIndex];
            McpTools = new ObservableCollection<McpToolConfig>(server.Tools);
            McpTools.CollectionChanged += OnMcpToolsCollectionChanged;
            
            // 切换服务器时清除测试结果
            McpValidateResult = string.Empty;
            
            // 通知UI所有服务器相关属性已变更
            OnPropertyChanged(nameof(CurrentServer));
            OnPropertyChanged(nameof(CurrentServerName));
            OnPropertyChanged(nameof(CurrentServerUrl));
            OnPropertyChanged(nameof(CurrentServerApiKey));
            OnPropertyChanged(nameof(CurrentServerEnabled));
            OnPropertyChanged(nameof(CurrentServerMaxToolCalls));
            OnPropertyChanged(nameof(ToolListSummary));
        }
    }

    private void SaveCurrentServer()
    {
        if (CurrentServerIndex >= 0 && CurrentServerIndex < McpServers.Count)
        {
            var server = McpServers[CurrentServerIndex];
            server.Tools = [.. McpTools];
            // 其他属性通过桥接属性自动同步
        }
    }
    
    /// <summary>
    /// 当前选中的服务器（用于XAML直接绑定）
    /// </summary>
    public McpServerConfig? CurrentServer => 
        CurrentServerIndex >= 0 && CurrentServerIndex < McpServers.Count 
            ? McpServers[CurrentServerIndex] 
            : null;

    private void OnModelsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add or
                       NotifyCollectionChangedAction.Remove or
                       NotifyCollectionChangedAction.Replace)
        {
            _settings.Models = [.. Models];
            SaveSettings();
        }
    }

    private void OnMcpToolsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add or
                       NotifyCollectionChangedAction.Remove or
                       NotifyCollectionChangedAction.Replace)
        {
            SaveCurrentServer();
        }
        // 工具列表变化时更新统计摘要
        OnPropertyChanged(nameof(ToolListSummary));
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdating) return;

        switch (e.PropertyName)
        {
            // 基础设置
            case nameof(ApiKey):
                _settings.ApiKey = ApiKey;
                break;
            case nameof(Url):
                _settings.Url = Url;
                break;
            case nameof(Model):
                _settings.Model = Model ?? string.Empty;
                break;
            case nameof(Temperature):
                _settings.Temperature = Math.Round(Temperature, 1);
                break;
            
            // MCP全局设置
            case nameof(EnableMcp):
                _settings.EnableMcp = EnableMcp;
                Main.ReinitializeMcpClient();
                break;
            case nameof(LogLevel):
                _settings.LogLevel = LogLevel;
                break;
            case nameof(EnableCommandSystem):
                _settings.EnableCommandSystem = EnableCommandSystem;
                SaveSettings();
                break;
            
            // 切换服务器
            case nameof(CurrentServerIndex):
                _settings.CurrentServerIndex = CurrentServerIndex;
                LoadCurrentServer();
                break;
            
            // 当前服务器属性变更
            case nameof(CurrentServerName):
            case nameof(CurrentServerUrl):
            case nameof(CurrentServerApiKey):
            case nameof(CurrentServerEnabled):
            case nameof(CurrentServerMaxToolCalls):
                SaveCurrentServer();
                // 服务器属性变更后立即保存，不等待防抖
                SaveSettings();
                return;
                
            // 提示词策略变更
            case nameof(SelectedPromptStrategy):
                SavePromptStrategy();
                SaveSettings();
                return;

            // MCP测试结果 - 5秒后自动消失
            case nameof(McpValidateResult):
                if (!string.IsNullOrEmpty(McpValidateResult))
                {
                    // 有结果时启动5秒定时器
                    _resultClearTimer?.Stop();
                    _resultClearTimer?.Start();
                }
                else
                {
                    // 清空时停止定时器
                    _resultClearTimer?.Stop();
                }
                break;
            
            default:
                return;
        }
        
        // 触发防抖保存
        _autoSaveTimer?.Stop();
        _autoSaveTimer?.Start();
    }

    private void SaveSettings()
    {
        _settings.McpServers = [.. McpServers];
        _context.SaveSettingStorage<Settings>();
    }

    [RelayCommand]
    private void AddModel(string model)
    {
        if (_isUpdating || string.IsNullOrWhiteSpace(model) || Models.Contains(model))
            return;

        using var _ = new UpdateGuard(this);

        Models.Add(model);
        Model = model;
    }

    [RelayCommand]
    private void DeleteModel(string model)
    {
        if (_isUpdating || !Models.Contains(model))
            return;

        using var _ = new UpdateGuard(this);

        if (Model == model)
            Model = Models.Count > 1 ? Models.First(m => m != model) : string.Empty;

        Models.Remove(model);
    }

    [RelayCommand]
    private void EditPrompt()
    {
        var dialog = _context.GetPromptEditWindow(Main.Prompts);

        if (dialog.ShowDialog() == true)
        {
            _settings.Prompts = [.. Main.Prompts.Select(p => p.Clone())];
            _context.SaveSettingStorage<Settings>();
            Main.SelectedPrompt = Main.Prompts.FirstOrDefault(p => p.IsEnabled);
        }
    }

    /// <summary>
    /// 更新当前选中的提示词策略显示
    /// </summary>
    private void UpdateSelectedPromptStrategy()
    {
        if (Main.SelectedPrompt == null)
        {
            SelectedPromptStrategy = PromptStrategyOptions.First();
            SelectedPromptStrategyText = "";
            return;
        }

        // 从映射表中获取该提示词的策略（默认为 Disabled）
        var strategy = _settings.PromptStrategyMap.TryGetValue(Main.SelectedPrompt.Name, out var s) ? s : McpToolStrategy.Disabled;
        SelectedPromptStrategy = PromptStrategyOptions.FirstOrDefault(o => o.Strategy == strategy) ?? PromptStrategyOptions.First();
        
        // 更新策略显示文本
        UpdateStrategyDisplayText();
    }
    
    /// <summary>
    /// 更新策略显示文本
    /// </summary>
    private void UpdateStrategyDisplayText()
    {
        if (Main.SelectedPrompt == null)
        {
            SelectedPromptStrategyText = "";
            return;
        }
        
        var strategy = _settings.PromptStrategyMap.TryGetValue(Main.SelectedPrompt.Name, out var s) ? s : McpToolStrategy.Disabled;
        SelectedPromptStrategyText = PromptStrategyHelper.GetStrategyDisplayText(strategy);
    }

    /// <summary>
    /// 保存当前提示词的策略设置
    /// </summary>
    private void SavePromptStrategy()
    {
        if (Main.SelectedPrompt == null || SelectedPromptStrategy == null)
            return;

        var promptName = Main.SelectedPrompt.Name;
        var newStrategy = SelectedPromptStrategy.Strategy;
        
        // 更新映射表
        _settings.PromptStrategyMap[promptName] = newStrategy;
        _context.SaveSettingStorage<Settings>();
        
        // 立即更新显示文本（实时刷新）
        UpdateStrategyDisplayText();
    }

    [RelayCommand]
    public async Task ValidateAsync()
    {
        try
        {
            UriBuilder uriBuilder = new(_settings.Url);
            if (uriBuilder.Path == "/")
                uriBuilder.Path = "/chat/completions";

            var model = _settings.Model.Trim();
            model = string.IsNullOrEmpty(model) ? "deepseek-chat" : model;

            var prompt = (Main.Prompts.FirstOrDefault(x => x.IsEnabled) ?? throw new Exception("请先完善Prompt配置"));
            var messages = prompt.Clone().Items;
            foreach (var item in messages)
            {
                item.Content = item.Content
                    .Replace("$source", "en-US")
                    .Replace("$target", "zh-CN")
                    .Replace("$content", "Hello world");
            }

            var temperature = Math.Clamp(_settings.Temperature, 0, 2);

            var content = new
            {
                model,
                messages,
                temperature,
                max_tokens = _settings.MaxTokens,
                top_p = _settings.TopP,
                n = _settings.N,
                stream = _settings.Stream
            };

            var option = new Options
            {
                Headers = new Dictionary<string, string>
                {
                    { "Authorization", "Bearer " + _settings.ApiKey },
                    { "Content-Type", "application/json" },
                    { "Accept", "text/event-stream" }
                }
            };

            await _context.HttpService.StreamPostAsync(uriBuilder.Uri.ToString(), content, (x) => { }, option);

            ValidateResult = _context.GetTranslation("ValidationSuccess");
        }
        catch (Exception ex)
        {
            ValidateResult = _context.GetTranslation("ValidationFailure");
        }
    }

    #region MCP服务器管理

    [RelayCommand]
    public void AddNewServer()
    {
        var newServer = new McpServerConfig
        {
            Name = $"服务器{McpServers.Count + 1}",
            Url = string.Empty,
            ApiKey = string.Empty,
            Enabled = false,  // 新服务器默认关闭
            MaxToolCalls = 10,
            Tools = []
        };
        
        McpServers.Add(newServer);
        CurrentServerIndex = McpServers.Count - 1;
        SaveSettings();
    }

    private bool _isConfirmingDelete = false;

    [RelayCommand]
    public void DeleteCurrentServer()
    {
        if (!_isConfirmingDelete)
        {
            // 第一次点击，显示确认提示
            _isConfirmingDelete = true;
            DeleteConfirmText = "删除服务器？请再次点击删除！";
            return;
        }

        // 第二次点击，执行删除
        _isConfirmingDelete = false;
        DeleteConfirmText = string.Empty;
        
        if (CurrentServerIndex >= 0 && CurrentServerIndex < McpServers.Count)
        {
            // 先保存要跳转到的索引（删除前一个）
            int newIndex = CurrentServerIndex;
            if (newIndex >= McpServers.Count - 1)
            {
                newIndex = Math.Max(0, McpServers.Count - 2);
            }
            
            McpServers.RemoveAt(CurrentServerIndex);
            
            // 如果删除后没有服务器了，自动创建一个新的
            if (McpServers.Count == 0)
            {
                AddNewServer();
            }
            else
            {
                // 设置新的索引并加载服务器
                CurrentServerIndex = newIndex;
                LoadCurrentServer();
                SaveSettings();
            }
        }
    }

    [RelayCommand]
    public void DuplicateCurrentServer()
    {
        if (CurrentServerIndex < 0 || CurrentServerIndex >= McpServers.Count)
            return;

        var currentServer = McpServers[CurrentServerIndex];
        var newServer = new McpServerConfig
        {
            Name = $"{currentServer.Name}-副本",
            Url = currentServer.Url,
            ApiKey = currentServer.ApiKey,
            Enabled = currentServer.Enabled,
            MaxToolCalls = currentServer.MaxToolCalls,
            Tools = currentServer.Tools.Select(t => new McpToolConfig
            {
                Name = t.Name,
                Description = t.Description,
                Enabled = t.Enabled,
                InputSchema = t.InputSchema
            }).ToList()
        };

        McpServers.Add(newServer);
        CurrentServerIndex = McpServers.Count - 1;
        SaveSettings();
    }

    [RelayCommand]
    public async Task TestAndDiscoverToolsAsync()
    {
        if (CurrentServerIndex < 0 || CurrentServerIndex >= McpServers.Count)
            return;

        var server = McpServers[CurrentServerIndex];
        
        if (string.IsNullOrWhiteSpace(server.Url))
        {
            McpValidateResult = "MCP服务器地址不能为空";
            return;
        }

        try
        {
            // 测试前清空当前工具列表，避免显示旧数据
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                McpTools.Clear();
            });

            using var mcpClient = McpClientFactory.CreateClient(server, null, _settings.LogLevel);
            var isConnected = await mcpClient.ConnectAsync();

            if (!isConnected)
            {
                McpValidateResult = "✗ MCP服务器连接失败";
                
                // 连接失败时清空工具列表
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    McpTools.Clear();
                    SaveCurrentServer();
                    SaveSettings();
                });
                return;
            }

            // 自动发现工具
            var tools = await mcpClient.ListToolsAsync();
            var isSameServer = !string.IsNullOrEmpty(server.LastConnectedUrl) && 
                               server.LastConnectedUrl == server.Url;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var existingConfigs = isSameServer 
                    ? server.Tools.ToDictionary(t => t.Name, t => t.Enabled) 
                    : new Dictionary<string, bool>();

                McpTools.Clear();
                foreach (var tool in tools)
                {
                    bool isEnabled = isSameServer && existingConfigs.TryGetValue(tool.Name, out var enabled) 
                        ? enabled 
                        : true;

                    McpTools.Add(new McpToolConfig
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        Enabled = isEnabled,
                        InputSchema = tool.InputSchema
                    });
                }
                
                // 保存到当前服务器
                SaveCurrentServer();
                SaveSettings();
            });

            server.LastConnectedUrl = server.Url;
            McpValidateResult = $"✅ 连接成功，发现 {tools.Count} 个工具";

            // 更新工具列表统计摘要
            OnPropertyChanged(nameof(ToolListSummary));
            
            Main.ReinitializeMcpClient();
        }
        catch (Exception ex)
        {
            McpValidateResult = $"✗ 错误: {ex.Message}";
            
            // 测试失败时清空工具列表
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                McpTools.Clear();
                SaveCurrentServer();
                SaveSettings();
            });
        }
    }

    [RelayCommand]
    private void ToggleToolEnabled(McpToolConfig tool)
    {
        if (tool == null) return;

        tool.Enabled = !tool.Enabled;

        // 保存更改
        SaveCurrentServer();
        SaveSettings();

        // 刷新工具列表和统计摘要以更新UI
        OnPropertyChanged(nameof(McpTools));
        OnPropertyChanged(nameof(ToolListSummary));
    }

    /// <summary>
    /// 刷新工具统计摘要（供视图调用）
    /// </summary>
    public void RefreshToolSummary()
    {
        // 保存当前更改
        SaveCurrentServer();
        SaveSettings();
        
        // 刷新统计
        OnPropertyChanged(nameof(ToolListSummary));
    }

    #endregion

    #region 策略提示词编辑

    /// <summary>
    /// 打开策略提示词编辑对话框
    /// </summary>
    [RelayCommand]
    private void EditStrategyPrompt()
    {
        var dialog = new StrategyPromptDialog(_context);
        var viewModel = new StrategyPromptDialogViewModel(_settings, dialog);
        dialog.DataContext = viewModel;
        
        var result = dialog.ShowDialog();
        if (result == true)
        {
            // 保存设置
            SaveSettings();
        }
    }

    #endregion

    public void Dispose()
    {
        _autoSaveTimer?.Stop();
        // 配置文件的保存/删除由主软件 PluginContext 统一管理
        // Dispose 时不应再次保存，否则会导致已删除的配置文件被重新创建
        _autoSaveTimer = null;
        _resultClearTimer?.Stop();
        _resultClearTimer = null;
        PropertyChanged -= OnPropertyChanged;
        Models.CollectionChanged -= OnModelsCollectionChanged;
        
        // 取消订阅策略变更事件
        StrategyEvents.Unsubscribe(this, OnPromptStrategyChanged);
    }

    #region 属性定义

    // 基础设置
    [ObservableProperty] public partial string ValidateResult { get; set; } = string.Empty;
    [ObservableProperty] public partial string Url { get; set; }
    [ObservableProperty] public partial string ApiKey { get; set; }
    [ObservableProperty] public partial string? Model { get; set; }
    [ObservableProperty] public partial ObservableCollection<string> Models { get; set; }
    [ObservableProperty] public partial double Temperature { get; set; }

    // MCP全局设置
    [ObservableProperty] public partial bool EnableMcp { get; set; }
    [ObservableProperty] public partial int LogLevel { get; set; }
    [ObservableProperty] public partial ObservableCollection<string> LogLevelOptions { get; set; } = new ObservableCollection<string> { "粗略", "中等", "详细" };
    
    // 命令系统开关（独立于MCP）
    [ObservableProperty] public partial bool EnableCommandSystem { get; set; } = false;
    
    // 工具结果显示模式（默认=Mixed）
    // 提示词级策略
    public List<PromptStrategyOption> PromptStrategyOptions { get; set; } = new();
    [ObservableProperty] public partial PromptStrategyOption? SelectedPromptStrategy { get; set; }
    
    // 当前选中提示词的策略显示文本（用于UI标签显示）
    [ObservableProperty] public partial string SelectedPromptStrategyText { get; set; } = "";

    // 服务器列表管理
    [ObservableProperty] public partial ObservableCollection<McpServerConfig> McpServers { get; set; }
    [ObservableProperty] public partial int CurrentServerIndex { get; set; }
    [ObservableProperty] public partial string DeleteConfirmText { get; set; } = string.Empty;

    // 当前服务器属性 - 使用完整属性定义（见下方#region）
    [ObservableProperty] public partial ObservableCollection<McpToolConfig> McpTools { get; set; } = new ObservableCollection<McpToolConfig>();
    [ObservableProperty] public partial McpToolConfig? SelectedTool { get; set; }

    // 测试连接结果
    [ObservableProperty] public partial string McpValidateResult { get; set; } = string.Empty;

    /// <summary>
    /// 工具列表统计摘要，格式："工具已启用   启用数/总数"（中间有间隔）
    /// </summary>
    public string ToolListSummary
    {
        get
        {
            if (McpTools == null || McpTools.Count == 0)
                return "工具已启用   0/0";
            var enabledCount = McpTools.Count(t => t.Enabled);
            return $"工具已启用   {enabledCount}/{McpTools.Count}";
        }
    }

    #endregion

    #region 当前服务器属性（用于XAML直接绑定）
    
    /// <summary>
    /// 当前服务器名称（绑定到CurrentServer.Name的桥接属性）
    /// </summary>
    public string CurrentServerName
    {
        get => CurrentServer?.Name ?? string.Empty;
        set
        {
            if (CurrentServer != null && CurrentServer.Name != value)
            {
                CurrentServer.Name = value;
                OnPropertyChanged(nameof(CurrentServerName));
            }
        }
    }
    
    /// <summary>
    /// 当前服务器地址（绑定到CurrentServer.Url的桥接属性）
    /// </summary>
    public string CurrentServerUrl
    {
        get => CurrentServer?.Url ?? string.Empty;
        set
        {
            if (CurrentServer != null && CurrentServer.Url != value)
            {
                CurrentServer.Url = value;
                OnPropertyChanged(nameof(CurrentServerUrl));
                Main.ReinitializeMcpClient();
            }
        }
    }
    
    /// <summary>
    /// 当前服务器API密钥（绑定到CurrentServer.ApiKey的桥接属性）
    /// </summary>
    public string CurrentServerApiKey
    {
        get => CurrentServer?.ApiKey ?? string.Empty;
        set
        {
            if (CurrentServer != null && CurrentServer.ApiKey != value)
            {
                CurrentServer.ApiKey = value;
                OnPropertyChanged(nameof(CurrentServerApiKey));
                Main.ReinitializeMcpClient();
            }
        }
    }
    
    /// <summary>
    /// 当前服务器是否启用（绑定到CurrentServer.Enabled的桥接属性）
    /// 启用时会自动测试连接，连接失败则无法启用
    /// </summary>
    public bool CurrentServerEnabled
    {
        get => CurrentServer?.Enabled ?? false;
        set
        {
            if (CurrentServer != null && CurrentServer.Enabled != value)
            {
                // 如果是启用操作，先测试连接
                if (value && !CurrentServer.Enabled)
                {
                    _ = TestConnectionAndEnableAsync();
                    return;
                }
                
                // 禁用操作直接执行
                CurrentServer.Enabled = value;
                OnPropertyChanged(nameof(CurrentServerEnabled));
                SaveCurrentServer();
                SaveSettings();
            }
        }
    }
    
    /// <summary>
    /// 测试连接、发现工具并在成功后启用服务器
    /// </summary>
    private async Task TestConnectionAndEnableAsync()
    {
        if (CurrentServer == null) return;
        
        try
        {
            // 测试前清空当前工具列表，避免显示旧数据
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                McpTools.Clear();
            });
            
            using var mcpClient = McpClientFactory.CreateClient(CurrentServer, null, _settings.LogLevel);
            var isConnected = await mcpClient.ConnectAsync();
            
            if (!isConnected)
            {
                // 连接失败，不启用并显示错误
                McpValidateResult = $"✗ 服务器 '{CurrentServer.Name}' 连接失败，无法启用";
                
                // 强制刷新UI，确保开关显示为关闭状态
                OnPropertyChanged(nameof(CurrentServerEnabled));
                return;
            }
            
            // 连接成功，自动发现工具
            var tools = await mcpClient.ListToolsAsync();
            var isSameServer = !string.IsNullOrEmpty(CurrentServer.LastConnectedUrl) && 
                               CurrentServer.LastConnectedUrl == CurrentServer.Url;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var existingConfigs = isSameServer 
                    ? CurrentServer.Tools.ToDictionary(t => t.Name, t => t.Enabled) 
                    : new Dictionary<string, bool>();

                McpTools.Clear();
                foreach (var tool in tools)
                {
                    bool isEnabled = isSameServer && existingConfigs.TryGetValue(tool.Name, out var enabled) 
                        ? enabled 
                        : true;

                    McpTools.Add(new McpToolConfig
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        Enabled = isEnabled,
                        InputSchema = tool.InputSchema
                    });
                }
                
                // 启用服务器并保存
                CurrentServer.Enabled = true;
                CurrentServer.LastConnectedUrl = CurrentServer.Url;
                OnPropertyChanged(nameof(CurrentServerEnabled));
                SaveCurrentServer();
                SaveSettings();
            });
            
            McpValidateResult = $"✅ 服务器 '{CurrentServer.Name}' 连接成功，发现 {tools.Count} 个工具并已启用";
            
            // 更新工具列表统计摘要
            OnPropertyChanged(nameof(ToolListSummary));
            
            // 重新初始化MCP客户端
            Main.ReinitializeMcpClient();
        }
        catch (Exception ex)
        {
            // 连接异常，不启用并显示错误
            McpValidateResult = $"✗ 服务器 '{CurrentServer.Name}' 连接错误: {ex.Message}";
            
            // 强制刷新UI，确保开关显示为关闭状态
            OnPropertyChanged(nameof(CurrentServerEnabled));
        }
    }
    
    /// <summary>
    /// 当前服务器最大工具调用次数（绑定到CurrentServer.MaxToolCalls的桥接属性）
    /// </summary>
    public int CurrentServerMaxToolCalls
    {
        get => CurrentServer?.MaxToolCalls ?? 10;
        set
        {
            if (CurrentServer != null && CurrentServer.MaxToolCalls != value)
            {
                CurrentServer.MaxToolCalls = value;
                OnPropertyChanged(nameof(CurrentServerMaxToolCalls));
            }
        }
    }
    
    #endregion

    // 辅助类
    private readonly struct UpdateGuard : IDisposable
    {
        private readonly SettingsViewModel _viewModel;

        public UpdateGuard(SettingsViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel._isUpdating = true;
        }

        public void Dispose() => _viewModel._isUpdating = false;
    }
}
