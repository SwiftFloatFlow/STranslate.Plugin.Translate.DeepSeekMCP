# 设置系统

## 文件位置

- **数据模型**：`Settings.cs`
- **视图模型**：`ViewModel/SettingsViewModel.cs`

## Settings.cs（数据模型）

### 类结构

```csharp
public class Settings : ObservableObject
{
    // MCP全局设置
    public bool EnableMcp { get; set; }                    // MCP总开关
    public int LogLevel { get; set; }                      // 日志级别（0粗略/1中等/2详细）
    
    // DeepSeek API设置
    public string Url { get; set; }                        // API地址
    public string ApiKey { get; set; }                     // API密钥
    public string Model { get; set; }                      // 模型名称
    
    // MCP服务器列表
    public List<McpServerConfig> McpServers { get; set; }  // 服务器配置
    public int CurrentServerIndex { get; set; }            // 当前选中服务器
    
    // 提示词级 MCP 策略映射表（Key: 提示词ID, Value: 策略，默认为Disabled）
    public Dictionary<string, McpToolStrategy> PromptStrategyMap { get; set; } = new();
    
    // 提示词ID映射表（Key: 提示词名称, Value: 提示词ID）- 用于局部提示词
    public Dictionary<string, string> PromptIdMap { get; set; } = new();
    
    // 策略级设置（Key: 策略类型）
    public Dictionary<McpToolStrategy, string> CustomStrategyPrompts { get; set; } = new();        // 自定义提示词
    public Dictionary<McpToolStrategy, int> StrategyConsecutiveToolLimits { get; set; } = new();  // 连续调用上限
    public Dictionary<McpToolStrategy, int> StrategyTotalToolCallsLimits { get; set; } = new();   // 总工具调用上限
    public Dictionary<McpToolStrategy, ToolResultDisplayMode> StrategyToolResultDisplayModes { get; set; } = new();  // 工具结果显示模式
    public Dictionary<McpToolStrategy, bool> StrategyToolChainDisplay { get; set; } = new();       // 工具链显示开关
}
```

### 重要变更（v4.0+）

**提示词ID绑定**：策略映射现在使用提示词ID作为键，不再使用名称。这确保了即使提示词被重命名，策略绑定仍然有效。
- `PromptIdMap` - 存储局部提示词名称到ID的映射
- `PromptStrategyMap` - 使用ID作为键存储策略

**全局提示词实时同步**：通过回调机制实现全局提示词的实时同步。
- 插件注册 `RegisterGlobalPromptsChangedCallback` 回调
- 主软件修改全局提示词时主动通知插件
- 插件在UI线程上刷新提示词列表

**UI优化**：
- 提示词下拉框固定宽度120px
- 全局提示词显示★前缀标识
- 超长名称自动截断显示省略号
- 鼠标悬停显示完整名称

### 重要变更（v3.0+）

**策略级设置**：工具链显示和工具结果显示模式现在按策略设置，不再是全局设置。新增以下字典属性：
- `StrategyToolChainDisplay` - 每个策略独立的工具链显示开关
- `StrategyToolResultDisplayModes` - 每个策略独立的工具结果显示模式（禁用/粗略/混合/详细）
- `StrategyConsecutiveToolLimits` - 每个策略的同一工具连续调用上限
- `StrategyTotalToolCallsLimits` - 每个策略的总工具调用上限
- `CustomStrategyPrompts` - 每个策略的自定义系统提示词

### 重要变更（v2.0+）

**移除全局策略**：早期版本包含"全局策略"设置，现已被移除。每个提示词独立绑定策略，新提示词默认使用"禁用服务"策略。

**命令系统**：命令系统直接受 MCP 服务功能开关统辖，不再拥有独立开关。

### McpToolStrategy 枚举

```csharp
public enum McpToolStrategy
{
    Disabled,      // 禁用MCP
    Blank,         // 空白策略 - 只列出工具，AI自行判断
    Hybrid,        // 混合策略 - 工具可选
    ToolFirst,     // 工具优先 - 优先使用工具
    ToolForced     // 工具强制 - 必须使用工具
}
```

### McpServerConfig 类

```csharp
public class McpServerConfig
{
    public string Name { get; set; }           // 服务器名称
    public string Url { get; set; }            // MCP服务器地址
    public string ApiKey { get; set; }         // API密钥（可选）
    public bool Enabled { get; set; }          // 是否启用此服务器
    public int MaxToolCalls { get; set; }      // 最大工具调用次数
    public List<McpToolConfig> Tools { get; set; }  // 工具列表
}
```

## SettingsViewModel.cs（视图模型）

### 职责

- 管理UI状态和命令
- 处理用户交互
- 自动保存设置
- 通过事件系统同步命令系统与UI

### 关键属性

```csharp
// 基础设置
public string Url { get; set; }
public string ApiKey { get; set; }

// MCP全局设置
public bool EnableMcp { get; set; }
public int LogLevel { get; set; }

// 服务器列表
public ObservableCollection<McpServerConfig> McpServers { get; }
public int CurrentServerIndex { get; set; }

// 当前选中服务器的属性（自动映射）
public string CurrentServerName { get; set; }
public string CurrentServerUrl { get; set; }
public string CurrentServerApiKey { get; set; }
public bool CurrentServerEnabled { get; set; }

// 工具列表
public ObservableCollection<McpToolConfig> McpTools { get; }
public string ToolListSummary { get; }  // "工具已启用 3/3"

// 提示词级策略绑定
public List<PromptStrategyOption> PromptStrategyOptions { get; }  // 策略选项列表（不含"跟随全局"）
public PromptStrategyOption SelectedPromptStrategy { get; set; }   // 当前选中的策略
public string SelectedPromptStrategyText { get; set; }            // 策略显示文本（如"[工具优先]"）
```

### 事件订阅（命令系统同步）

ViewModel 订阅 `StrategyEvents.PromptStrategyChanged` 事件，当用户通过命令系统切换策略时，UI会自动刷新：

```csharp
// 构造函数中订阅事件
StrategyEvents.Subscribe(this, OnPromptStrategyChanged);

// 事件处理器
private void OnPromptStrategyChanged(object? sender, PromptStrategyChangedEventArgs e)
{
    // 仅当当前显示的提示词与变更的提示词匹配时才更新UI
    if (Main.SelectedPrompt?.Name == e.PromptName)
    {
        SelectedPromptStrategy = PromptStrategyOptions.First(o => o.Strategy == e.NewStrategy);
        UpdateStrategyDisplayText();
    }
}
```

### 提示词策略绑定机制

```csharp
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
    var strategy = _settings.PromptStrategyMap.TryGetValue(Main.SelectedPrompt.Name, out var s) 
        ? s : McpToolStrategy.Disabled;
    SelectedPromptStrategy = PromptStrategyOptions.FirstOrDefault(o => o.Strategy == strategy) 
        ?? PromptStrategyOptions.First();
    
    // 更新策略显示文本
    UpdateStrategyDisplayText();
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
    
    // 触发事件通知命令系统（如需要）
    StrategyEvents.RaisePromptStrategyChanged(promptName, newStrategy);
    
    // 立即更新显示文本（实时刷新）
    UpdateStrategyDisplayText();
}
```

**注意**：策略选项列表不再包含"跟随全局"选项。新提示词默认使用"禁用服务"策略。

### 关键命令

```csharp
// 添加新服务器
[RelayCommand]
private void AddNewServer()

// 复制当前服务器
[RelayCommand]
private void DuplicateCurrentServer()

// 删除当前服务器
[RelayCommand]
private void DeleteCurrentServer()

// 测试连接并发现工具
[RelayCommand]
private async Task TestAndDiscoverToolsAsync()
```

### 自动保存机制

```csharp
// 防抖定时器（300ms）
private DispatcherTimer _autoSaveTimer;

// 属性变更时触发保存
partial void OnPropertyChanged(PropertyChangedEventArgs args)
{
    _autoSaveTimer?.Stop();
    _autoSaveTimer?.Start();  // 300ms后自动保存
}

public void SaveSettings()
{
    // 保存到STranslate配置系统
    Context.SaveSettings(Settings);
}
```

## 数据绑定

### ViewModel → View 绑定

```xml
<!-- 全局策略 -->
<ComboBox ItemsSource="{Binding ToolStrategies}"
          SelectedItem="{Binding ToolStrategy}" />

<!-- 服务器选择 -->
<ComboBox ItemsSource="{Binding McpServers}"
          SelectedIndex="{Binding CurrentServerIndex}" />

<!-- 当前服务器属性 -->
<TextBox Text="{Binding CurrentServerName, Mode=TwoWay}" />
<TextBox Text="{Binding CurrentServerUrl, Mode=TwoWay}" />
```

### 工具列表绑定

```xml
<ComboBox ItemsSource="{Binding McpTools}"
          ui:ControlHelper.PlaceholderText="{Binding ToolListSummary}">
    <ComboBox.ItemTemplate>
        <DataTemplate>
            <Grid>
                <ToggleSwitch IsOn="{Binding Enabled, Mode=TwoWay}" />
                <TextBlock Text="{Binding Name}" />
            </Grid>
        </DataTemplate>
    </ComboBox.ItemTemplate>
</ComboBox>
```

## 配置持久化

### 保存流程

```
用户修改 → ViewModel属性变更
    → OnPropertyChanged()
        → 防抖定时器(300ms)
            → SaveSettings()
                → Context.SaveSettings(Settings)
                    → STranslate保存到配置文件
```

### 配置文件位置

```
%AppData%/STranslate/plugins/DeepSeek/settings.json
```

## 修改建议

### 添加新设置项

1. **在Settings.cs添加属性**：
```csharp
public string NewSetting { get; set; } = "默认值";
```

2. **在ViewModel添加绑定属性**：
```csharp
public string NewSetting
{
    get => _settings.NewSetting;
    set
    {
        _settings.NewSetting = value;
        OnPropertyChanged();
        SaveSettings();
    }
}
```

3. **在SettingsView.xaml添加UI**：
```xml
<TextBlock Text="新设置:" />
<TextBox Text="{Binding NewSetting, Mode=TwoWay}" />
```

### 修改命令系统响应

命令系统的响应文本在 `Main.cs` 的 `ExecuteCommandAsync` 方法中定义。修改对应命令的执行方法即可自定义响应：

```csharp
private CommandResult ExecuteHelpCommand()
{
    // 自定义帮助信息
    var sb = new StringBuilder();
    sb.AppendLine("=== 自定义帮助标题 ===");
    // ... 添加自定义内容
    return new CommandResult { IsCommand = true, Success = true, Message = sb.ToString() };
}
```

### 修改自动保存间隔

```csharp
// SettingsViewModel.cs 构造函数中
_autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
// 改为500ms
```

### 添加设置验证

```csharp
partial void OnCurrentServerUrlChanged(string value)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out _))
    {
        // 显示错误提示
        McpValidateResult = "无效的URL格式";
    }
}
```

## 调试技巧

### 查看当前设置值

在代码中添加断点：
```csharp
// SettingsViewModel.cs
public void SaveSettings()
{
    var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
    Debug.WriteLine(json);  // 在此设置断点
    Context.SaveSettings(Settings);
}
```

### 检查绑定是否成功

在XAML中添加调试输出：
```xml
<TextBlock Text="{Binding CurrentServerName, FallbackValue='绑定失败'}" />
```

## 常见问题

### Q: 设置不保存？
A: 检查：
1. ViewModel属性是否正确实现INotifyPropertyChanged
2. OnPropertyChanged是否被调用
3. SaveSettings是否被触发

### Q: 下拉框不显示枚举名称？
A: 确保：
1. 使用StrategyToNameConverter转换器
2. 在App.xaml或资源字典中定义转换器

### Q: 工具列表不更新？
A: 检查：
1. McpTools集合是否为ObservableCollection
2. 切换服务器时是否正确刷新Tools属性