# 全局提示词功能

## 功能概述

全局提示词是主软件创建的提示词集合，通过 `IPluginContext` 接口暴露给插件使用。与插件局部提示词的区别：

| 特性 | 插件局部提示词 | 全局提示词 |
|------|---------------|-----------|
| **存储位置** | 插件配置文件 | 主程序 Settings.json |
| **IsEnabled 语义** | 互斥选择（翻译时只用一个） | 是否暴露给插件（可多选） |
| **管理方式** | 插件自己管理 | 主程序管理，接口暴露 |
| **可删除限制** | 必须保留至少一个 | 可以删除到空 |
| **ID 属性** | 自动生成，用于识别 | 自动生成，用于同步识别 |

## 架构设计

```
┌─────────────────────────────────────────────────────────┐
│ 主程序 (Settings.cs)                                     │
│ ┌──────────────────────────────────────────────────┐    │
│ │ GlobalPrompts: ObservableCollection<Prompt>      │    │
│ │   - Prompt.Id = 唯一标识符（用于同步识别）       │    │
│ │   - Prompt.IsEnabled = 是否暴露给插件            │    │
│ │   - 通过 PromptEditWindow 编辑（不互斥）         │    │
│ └──────────────────────────────────────────────────┘    │
│                          │                               │
│                          ▼ 有效保存时手动触发             │
│              GlobalPromptsChanged 事件                   │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼ IPluginContext.GetGlobalPrompts()
┌─────────────────────────────────────────────────────────┐
│ 插件 (LlmTranslatePluginBase)                           │
│ ┌──────────────────────────────────────────────────┐    │
│ │ Prompts: ObservableCollection<Prompt>            │    │
│ │   - 插件局部提示词（IsEnabled 互斥选择）         │    │
│ │   + 可选：通过 ID 识别并添加全局提示词           │    │
│ └──────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
```

## 相关文件

| 文件 | 用途 |
|------|------|
| `STranslate.Plugin/Prompt.cs` | Prompt 数据模型，包含 Id 属性 |
| `STranslate/Core/Settings.cs` | GlobalPrompts 属性、GlobalPromptsChanged 事件 |
| `STranslate.Plugin/IPluginContext.cs` | 全局提示词相关接口定义 |
| `STranslate/Core/PluginContext.cs` | 接口实现、IsEnabled 重置逻辑 |
| `STranslate/ViewModels/PromptEditViewModel.cs` | isMutualExclusion 参数、HasChanges() 检测 |
| `STranslate/Views/PromptEditWindow.xaml.cs` | HasValidSave 属性、构造函数互斥参数 |
| `STranslate/ViewModels/Pages/TranslateViewModel.cs` | EditGlobalPromptsCommand、有效保存通知 |
| `STranslate/Views/Pages/TranslatePage.xaml` | 全局提示词入口 UI |

## Prompt.ID 属性

每个 Prompt 都有唯一标识符 `Id`（Guid 类型），用于同步识别：

```csharp
public partial class Prompt : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("id")]
    public partial Guid Id { get; set; } = Guid.NewGuid();
    
    // Clone() 会保持原 ID
    public Prompt Clone()
    {
        return new Prompt(Name, Items.Select(p => p.Clone()), IsEnabled) { Id = Id };
    }
}
```

**兼容性**：
- 旧插件无需关心 ID，完全无感知
- 旧数据反序列化时 ID 为 `Guid.Empty`
- 新创建的 Prompt 自动生成唯一 ID

## 接口定义

### IPluginContext 新增接口

```csharp
/// <summary>
/// 获取启用的全局提示词（IsEnabled=true 的）
/// </summary>
IReadOnlyList<Prompt> GetGlobalPrompts();

/// <summary>
/// 获取全局提示词编辑窗口
/// </summary>
Window GetGlobalPromptEditWindow();

/// <summary>
/// 注册全局提示词变更回调
/// </summary>
/// <param name="callback">回调函数，参数为启用的全局提示词列表</param>
/// <param name="delayMs">延时注销毫秒数，默认100ms</param>
/// <returns>可释放对象，用于注销回调</returns>
IDisposable RegisterGlobalPromptsChangedCallback(
    Action<IReadOnlyList<Prompt>> callback, 
    int delayMs = 100);
```

## IsEnabled 语义重置

**重要**：`GetGlobalPrompts()` 和 `RegisterGlobalPromptsChangedCallback()` 返回的 Prompt 会将 `IsEnabled` 重置为 `false`。

| 位置 | IsEnabled 含义 |
|------|---------------|
| 主软件全局提示词 | 是否暴露给插件 |
| 返回给插件的克隆 | 固定为 `false`（未选中状态） |

**原因**：避免与插件的互斥选择机制冲突。插件收到全局提示词后，可以自由选择启用哪一个，不影响原有的互斥逻辑。

## 变更通知触发机制

全局提示词的变更通知**仅在有效保存时触发**，而非实时监听：

### 触发条件

| 用户操作 | 是否触发通知 |
|---------|-------------|
| 打开窗口 → 修改 → 点击保存（有变化） | ✅ 触发 |
| 打开窗口 → 修改 → 点击保存（无变化） | ❌ 不触发 |
| 打开窗口 → 不修改 → 点击保存 | ❌ 不触发 |
| 打开窗口 → 修改 → 点击取消 | ❌ 不触发 |
| 打开窗口 → 修改 → 点击×关闭 | ❌ 不触发 |

### 有效变更检测

使用序列化比较方式检测是否有实际变化：

```csharp
private bool HasChanges()
{
    var options = new JsonSerializerOptions { IncludeFields = true };
    var originalJson = JsonSerializer.Serialize(_originalPrompts, options);
    var currentJson = JsonSerializer.Serialize(Prompts, options);
    return originalJson != currentJson;
}
```

### 手动触发通知

`Settings.NotifyGlobalPromptsChanged()` 仅在全局提示词窗口有效保存后由 `TranslateViewModel` 调用：

```csharp
// TranslateViewModel.EditGlobalPrompts()
var result = window.ShowDialog();

if (result == true && window.HasValidSave)
{
    settings.NotifyGlobalPromptsChanged();
}
```

## 插件使用示例

### 基本使用

```csharp
public class MyPlugin : LlmTranslatePluginBase
{
    public override void Init(IPluginContext context)
    {
        // 获取启用的全局提示词（已克隆，IsEnabled 已重置为 false）
        var globalPrompts = context.GetGlobalPrompts();
        
        // 直接添加到插件本地（无需再次克隆）
        foreach (var prompt in globalPrompts)
        {
            Prompts.Add(prompt);
        }
    }
}
```

### 通过 ID 识别全局提示词

```csharp
public class MyPlugin : LlmTranslatePluginBase
{
    private HashSet<Guid> _globalPromptIds = [];
    
    public override void Init(IPluginContext context)
    {
        var globalPrompts = context.GetGlobalPrompts();
        
        foreach (var prompt in globalPrompts)
        {
            _globalPromptIds.Add(prompt.Id);
            Prompts.Add(prompt);
        }
    }
    
    private bool IsGlobalPrompt(Prompt prompt)
    {
        return _globalPromptIds.Contains(prompt.Id);
    }
}
```

### 监听变更

```csharp
public class MyPlugin : LlmTranslatePluginBase, IDisposable
{
    private IDisposable? _globalPromptsCallback;

    public override void Init(IPluginContext context)
    {
        // 注册变更回调
        _globalPromptsCallback = context.RegisterGlobalPromptsChangedCallback(
            OnGlobalPromptsChanged,
            delayMs: 100);
    }

    private void OnGlobalPromptsChanged(IReadOnlyList<Prompt> prompts)
    {
        // 更新插件的提示词列表
        // 注意：此方法可能在后台线程调用，需要 Dispatcher 切换到 UI 线程
        // prompts 已经是克隆且 IsEnabled 已重置为 false
    }

    public override void Dispose()
    {
        _globalPromptsCallback?.Dispose();
        base.Dispose();
    }
}
```

## 互斥机制说明

`PromptEditViewModel` 的 `isMutualExclusion` 参数控制提示词选择行为：

- **`isMutualExclusion: true`**（默认）：插件局部提示词，IsEnabled 互斥选择
- **`isMutualExclusion: false`**：全局提示词，IsEnabled 表示是否暴露给插件（可多选）

```csharp
// 插件局部提示词（默认互斥）
var window = new PromptEditWindow(prompts, roles, isMutualExclusion: true);

// 全局提示词（不互斥）
var window = new PromptEditWindow(settings.GlobalPrompts, roles: null, isMutualExclusion: false);
```

## UI 入口

全局提示词入口位于「设置 → 文本翻译」页面右上角：

```
全局提示词：[编辑] | 图片翻译：百度翻译 | 替换翻译：百度翻译
```

## 对插件的影响

| 插件类型 | 影响 |
|---------|------|
| 不使用全局提示词 | **零影响**，代码完全不变 |
| 使用全局提示词的新插件 | 低影响，调用 `GetGlobalPrompts()` 获取 |
| 使用全局提示词 + 监听变更 | 中影响，需注册回调 |
