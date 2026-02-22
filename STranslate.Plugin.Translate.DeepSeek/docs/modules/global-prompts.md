# 全局提示词功能（插件端）

## 概述

本插件支持从 STranslate 主软件加载全局提示词。全局提示词在主软件中统一管理，插件端**只读使用**，通过 `IPluginContext` 提供的接口获取和监听变更。

## 插件端接入方式

### 1. 获取全局提示词

```csharp
// 通过 IPluginContext 获取全局提示词列表
var globalPrompts = Context.GetGlobalPrompts();
```

**返回值说明**：
- 返回 `IReadOnlyList<Prompt>` 类型
- 提示词已克隆，插件端可安全使用
- `IsEnabled` 属性已被重置为 `false`

### 2. 监听全局提示词变更

```csharp
// 注册变更回调
_globalPromptsCallback = Context.RegisterGlobalPromptsChangedCallback(
    OnGlobalPromptsChanged, 
    delayMs: 100);

// 回调处理
private void OnGlobalPromptsChanged(IReadOnlyList<Prompt> newGlobalPrompts)
{
    // 在 UI 线程上执行刷新
    Application.Current?.Dispatcher.Invoke(() =>
    {
        // 同步更新提示词列表
    });
}

// 插件卸载时取消注册
_globalPromptsCallback?.Dispose();
```

### 3. 识别全局提示词

使用 `Prompt.Id` 属性识别全局提示词：

```csharp
private HashSet<Guid> _globalPromptIds = [];

// 加载时记录全局提示词ID
foreach (var p in globalPrompts)
{
    _globalPromptIds.Add(p.Id);
    Prompts.Add(p);
}

// 判断是否为全局提示词
public bool IsGlobalPrompt(Prompt? prompt) => 
    prompt != null && _globalPromptIds.Contains(prompt.Id);
```

## 插件端实现要点

### 初始化时加载

```csharp
public override void Init(IPluginContext context)
{
    // 1. 先获取全局提示词ID集合
    var globalPrompts = Context.GetGlobalPrompts();
    _globalPromptIds = globalPrompts.Select(p => p.Id).ToHashSet();
    
    // 2. 加载局部提示词（过滤掉可能是全局提示词的项）
    foreach (var p in Settings.Prompts)
    {
        if (!_globalPromptIds.Contains(p.Id))
        {
            Prompts.Add(p);
        }
    }
    
    // 3. 添加全局提示词
    foreach (var p in globalPrompts)
    {
        Prompts.Add(p);
    }
    
    // 4. 注册变更回调
    _globalPromptsCallback = Context.RegisterGlobalPromptsChangedCallback(
        OnGlobalPromptsChanged, delayMs: 100);
}
```

### 保存时过滤

```csharp
public override void SelectPrompt(Prompt? prompt)
{
    base.SelectPrompt(prompt);
    // 只保存局部提示词，不保存全局提示词
    Settings.Prompts = Prompts
        .Where(p => !_globalPromptIds.Contains(p.Id))
        .Select(p => p.Clone())
        .ToList();
    Context.SaveSettingStorage<Settings>();
}
```

### 编辑按钮禁用

```csharp
// ViewModel 中
public bool IsSelectedPromptGlobal => Main.IsGlobalPrompt(Main.SelectedPrompt);
public bool CanEditSelectedPrompt => Main.SelectedPrompt != null && !IsSelectedPromptGlobal;
```

```xml
<!-- XAML 中 -->
<Button IsEnabled="{Binding CanEditSelectedPrompt}" ... />
```

### 编辑窗口只显示局部提示词

```csharp
private void EditPrompt()
{
    // 过滤掉全局提示词，只传递局部提示词
    var localPrompts = new ObservableCollection<Prompt>(
        Main.Prompts.Where(p => !Main.IsGlobalPrompt(p)).Select(p => p.Clone()));
    
    var dialog = _context.GetPromptEditWindow(localPrompts);
    // ...
}
```

## UI 显示规范

### 下拉菜单

- **固定宽度**：150px
- **全局提示词标识**：名称后显示 🌐 图标
- **全局提示词不可编辑**：选中时编辑按钮禁用

### 布局要求

- 提示词下拉框和 MCP 策略下拉框宽度一致
- 不会因提示词名称长度变化而影响其他组件位置

## 策略绑定

全局提示词和局部提示词使用相同的 ID 绑定 MCP 策略：

```csharp
// 策略映射键：使用 Prompt.Id
var strategyKey = prompt.Id.ToString("N");
if (Settings.PromptStrategyMap.TryGetValue(strategyKey, out var strategy))
{
    // 使用绑定的策略
}
```

## 同步流程

```
主软件修改全局提示词
       ↓
触发 RegisterGlobalPromptsChangedCallback 回调
       ↓
插件收到新提示词列表
       ↓
在 UI 线程执行：
  1. 移除旧的全局提示词
  2. 添加新的全局提示词
  3. 恢复选中状态
       ↓
UI 自动刷新
```

## 注意事项

1. **全局提示词只读**：插件端不能修改全局提示词
2. **保存时过滤**：配置文件只保存局部提示词
3. **ID 识别**：使用 `Prompt.Id` 识别，不依赖名称
4. **回调注销**：插件卸载时必须注销回调，避免内存泄漏
5. **UI 线程**：回调处理必须在 UI 线程执行

## 相关文件

| 文件 | 功能 |
|------|------|
| `Main.cs` | 全局提示词加载、ID识别、回调处理 |
| `SettingsViewModel.cs` | `IsGlobalPrompt`、`CanEditSelectedPrompt`、编辑逻辑 |
| `SettingsView.xaml` | 下拉框 UI、编辑按钮绑定 |

## 版本要求

- STranslate.Plugin >= 1.0.8
- 主软件需支持 `GetGlobalPrompts()` 和 `RegisterGlobalPromptsChangedCallback()` 接口
