# 全局提示词功能

## 概述

全局提示词功能允许插件从主软件（STranslate）加载和管理全局提示词。全局提示词在主软件中定义，可以被所有插件共享使用。

## 功能特性

### 1. 全局提示词标识

全局提示词在下拉菜单中显示时带有 `★` 前缀标识：
- 全局提示词：`★全局提示词名称`
- 局部提示词：`提示词名称`

### 2. 实时同步机制

通过主软件提供的回调机制实现实时同步：

```csharp
// 注册回调
Context.RegisterGlobalPromptsChangedCallback(OnGlobalPromptsChanged);

// 回调处理
private void OnGlobalPromptsChanged(IReadOnlyList<GlobalPrompt> globalPrompts)
{
    // 同步更新全局提示词列表
}
```

**同步内容**：
- ✅ 名称变更（重命名）
- ✅ Prompt内容变更（Items更新）
- ✅ 新增全局提示词
- ✅ 删除全局提示词

### 3. ID绑定策略

全局提示词和局部提示词都使用唯一的ID绑定MCP策略：

- 策略映射键：`PromptStrategyMap[PromptId]`
- 避免名称变更导致策略丢失
- 全局和局部提示词使用相同的ID机制

### 4. 编辑限制

全局提示词在插件端**不可编辑**：
- 编辑按钮自动禁用
- 只能通过主软件修改
- 防止插件端修改影响其他插件

## 实现细节

### 数据模型

```csharp
public class GlobalPrompt
{
    public string Id { get; set; }              // 唯一标识（GUID）
    public string Name { get; set; }            // 显示名称
    public bool IsEnabled { get; set; }
    public ObservableCollection<PromptItem> Items { get; set; }
}
```

### 同步流程

```
主软件修改全局提示词
    ↓
触发全局提示词变更回调
    ↓
插件收到通知
    ↓
在UI线程上执行刷新
    ↓
更新名称（如果有变化）
更新Items内容（如果有变化）
    ↓
触发PropertyChanged通知UI刷新
```

### 关键代码

**RefreshGlobalPromptsFromList** 方法处理同步：

```csharp
private void RefreshGlobalPromptsFromList(IReadOnlyList<GlobalPrompt> globalPrompts)
{
    // 1. 移除已删除的全局提示词
    // 2. 更新或添加全局提示词
    // 3. 更新名称（如果有变化）
    // 4. 更新Items内容（如果有变化）
    // 5. 清理同名局部提示词
}
```

**UpdatePromptItems** 方法更新内容：

```csharp
private bool UpdatePromptItems(Prompt prompt, ObservableCollection<PromptItem> newItems)
{
    // 比较现有的Items和新的Items
    // 如果不同，清空并重新添加
    // 返回是否有更新
}
```

## UI显示

### 下拉菜单显示

- **固定宽度**：120px
- **超长截断**：超过显示长度自动显示省略号
- **悬停提示**：鼠标悬停显示完整名称

### 布局特点

- 提示词下拉框和策略下拉框宽度一致（120px）
- 不会随提示词名称长度变化而影响其他组件位置
- 当前选中的提示词名称变化时立即刷新

## 使用场景

### 场景1：主软件创建全局提示词

1. 用户在主软件创建全局提示词"商务翻译"
2. 插件立即收到通知，在下拉菜单显示：`★商务翻译`
3. 用户可以在插件中选择该提示词进行翻译

### 场景2：修改全局提示词内容

1. 用户在主软件修改"商务翻译"的prompt内容
2. 插件收到通知，立即更新Items内容
3. 如果当前正在使用"商务翻译"，下次翻译会使用更新后的内容

### 场景3：重命名全局提示词

1. 用户在主软件将"商务翻译"重命名为"正式商务翻译"
2. 插件收到通知，更新显示名称
3. 策略绑定保持不变（因为使用ID绑定）

## 注意事项

1. **全局提示词不可编辑**：插件端编辑按钮对全局提示词自动禁用
2. **保存时过滤**：保存配置时只保存局部提示词，全局提示词不会被保存到插件配置
3. **实时同步**：依赖主软件的回调机制，如果主软件不支持则无法实时同步
4. **内存管理**：插件卸载时记得注销回调，避免内存泄漏

## 相关文件

| 文件 | 功能 |
|------|------|
| `Main.cs` | 全局提示词加载、同步、刷新 |
| `SettingsViewModel.cs` | 编辑逻辑、刷新逻辑 |
| `SettingsView.xaml` | 下拉框UI显示 |
| `PromptDisplayNameConverter.cs` | 显示名称转换（添加★前缀） |

## 版本历史

- **v4.0+** - 添加全局提示词功能
  - 支持从主软件加载全局提示词
  - 实现名称和内容的实时同步
  - 使用ID绑定策略
  - 添加★前缀标识
