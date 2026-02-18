# 命令系统

## 概述

命令系统是 DeepSeekMCP 插件提供的一项便捷功能，允许用户通过输入命令快速切换 MCP 策略，无需打开设置界面。

## 功能特点

- **快速切换**：输入 `/切换 工具强制` 即可将当前提示词设为"工具强制"策略
- **实时反馈**：命令执行后立即显示结果，无需等待翻译
- **语言支持**：支持中英文命令和参数
- **受控开关**：命令系统可在设置中启用/禁用（默认启用）
- **MCP统辖**：命令系统受 MCP 服务功能开关统辖，MCP 禁用后不可用

## 命令列表

### 设置界面命令列表卡片

在插件的设置界面中，提供了一个**命令列表卡片**，以表格形式清晰展示所有可用命令，便于新手快速查阅：

```
┌─ 命令列表 ──────────────────────────────────────────────────────────┐
│                                                                      │
│  中文命令       英文命令        功能描述                              │
│  ──────────────────────────────────────────────────────────────────  │
│  /当前          /now            查看当前提示词的策略和工具设置        │
│  /切换[策略]    /switch[策略]   切换当前提示词的MCP策略               │
│  /状态          /status         查看MCP服务状态、服务器列表和策略     │
│  /工具链        /chain          切换当前策略的工具链显示开关          │
│  /工具结果[模式]/result[模式]   查看/切换当前策略的工具结果显示模式   │
│  /mcp           /mcp            开启或关闭MCP服务功能                 │
│  /帮助          /help           显示命令帮助信息                      │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

**表格布局特点：**
- **三列布局**：中文命令（110px）、英文命令（110px）、功能描述（自适应）
- **表头清晰**：显示"中文命令"、"英文命令"、"功能描述"三列标题
- **分列对齐**：命令以蓝色高亮显示，描述以主文本颜色显示
- **受控状态**：命令列表卡片受命令系统开关统辖，禁用后整个表格灰色显示

### 完整命令列表

| 命令（英文） | 命令（中文） | 功能 | 参数 |
|-------------|-------------|------|------|
| `/now` | `/当前` | 查看当前提示词绑定的策略和工具设置 | 无 |
| `/switch [策略]` | `/切换 [策略]` | 切换当前提示词的 MCP 策略 | 必填（策略名） |
| `/status` | `/状态` | 查看 MCP 服务状态、服务器列表和策略 | 无 |
| `/chain` | `/工具链` | 切换当前策略的工具链显示开关 | 无 |
| `/result [模式]` | `/工具结果 [模式]` | 查看或切换当前策略的工具结果显示模式 | 可选（模式名） |
| `/mcp` | `/mcp` | 开启或关闭 MCP 服务功能 | 无 |
| `/help` | `/帮助` | 显示命令帮助信息 | 无 |

### 工具结果显示模式

| 模式 | 中文名称 | 说明 |
|------|----------|------|
| `Disabled` | `禁用` | 不显示工具结果和内联标记 |
| `Minimal` | `粗略` | 仅内联显示工具名 |
| `Mixed` | `混合` | 内联+截断显示结果 |
| `Detailed` | `详细` | 内联+完整显示结果 |

## 策略名称

### 英文名称

| 策略 | 英文名称 |
|------|---------|
| 禁用服务 | `disabled` |
| 空白策略 | `blank` |
| 混合判断 | `hybrid` |
| 工具优先 | `toolfirst` |
| 工具强制 | `toolforced` |

### 中文名称

| 策略 | 中文名称 |
|------|---------|
| 禁用服务 | `禁用` |
| 空白策略 | `空白` |
| 混合判断 | `混合` |
| 工具优先 | `优先` |
| 工具强制 | `强制` |

## 使用示例

### 查看当前策略
```
/当前
```
输出：
```
当前提示词: 翻译
绑定策略: 工具优先
```

### 切换策略（中文）
```
/切换 工具强制
```
输出：
```
✅ 已切换提示词 '翻译' 的策略
从: 工具优先 → 工具强制 (CN)
```

### 切换策略（英文）
```
/switch hybrid
```
输出：
```
✅ 已切换提示词 '翻译' 的策略
从: 工具强制 → 混合判断 (EN)
```

### 查看 MCP 状态
```
/状态
```
输出：
```
=== MCP 服务状态 ===
MCP服务: 已启用
当前策略工具结果: 禁用结果
当前策略工具链显示: 关闭

MCP服务器: 1/3 已启用
  - MCP-智能路由（已启用）: 3/3个工具
    search_tools✅
    describe_tool✅
    call_tool✅
---
  - MCP-time（已关闭）: 0/3个工具
    mcp_rand_uuid❎
    mcp_rand_roll_dice❎
    mcp_rand_draw_cards❎
---
  - MCP-rand（已关闭）: 0/0个工具
```

### 显示帮助
```
/帮助
```

## 使用规则

### 1. 触发条件
- 输入的第一个字符必须是 `/`
- MCP 服务必须已启用（命令系统受 MCP 开关统辖）

### 2. 语言一致性
- 命令和参数必须使用同一种语言
- ✅ 正确：`/切换 工具强制`（中文命令 + 中文参数）
- ✅ 正确：`/switch toolforced`（英文命令 + 英文参数）
- ❌ 错误：`/切换 toolforced`（中文命令 + 英文参数）

### 3. 错误处理
- **未知命令**：返回可用命令列表
- **缺少参数**：返回命令用法说明
- **未选择提示词**：提示先选择提示词（`/工具链`和`/工具结果`需要选择提示词）
- **未知策略**：返回可用策略列表
- **MCP 禁用**：命令系统不可用，输入以 `/` 开头的文本将按普通文本处理

### 4. 策略级工具设置

工具链显示和工具结果显示模式是**按策略设置**的，不是全局设置：

- 不同策略可以有不同的工具结果显示模式
- 不同策略可以独立开启或关闭工具链显示
- 切换策略后，工具设置会跟随策略自动变化
- 使用 `/工具链` 和 `/工具结果` 命令修改的是当前策略的设置

## 实现机制

### 流程图

```
用户输入命令
    ↓
TranslateAsync()
    ↓
检查 MCP 服务是否启用且以"/"开头
    ↓
ExecuteCommandAsync()
    ↓
解析命令和参数
    ↓
匹配命令类型（switch语句）
    ↓
执行对应命令处理函数
    ↓
    ├─ /now → 查询当前提示词策略和工具设置
    ├─ /switch → 验证参数并更新策略
    ├─ /status → 收集MCP状态信息
    ├─ /chain → 切换当前策略的工具链显示
    ├─ /result → 查看或切换工具结果显示模式
    ├─ /mcp → 切换MCP服务功能开关
    └─ /help → 返回帮助文本
    ↓
更新 Settings（PromptStrategyMap / StrategyToolChainDisplay / StrategyToolResultDisplayModes）
    ↓
触发 StrategyEvents（通知UI更新）
    ↓
保存设置到存储
    ↓
返回 CommandResult
```

### 核心代码

#### 命令入口

```csharp
public override async Task TranslateAsync(TranslateRequest request, ...)
{
    // 检查是否为命令
    if (Settings.EnableCommandSystem && !string.IsNullOrWhiteSpace(request.Text) 
        && request.Text.TrimStart().StartsWith("/"))
    {
        var commandResult = await ExecuteCommandAsync(request.Text.Trim());
        if (commandResult.IsCommand)
        {
            result.Text = commandResult.Success 
                ? commandResult.Message 
                : $"❌ {commandResult.Message}";
            return;
        }
    }
    // ... 继续翻译流程
}
```

#### 命令执行器

```csharp
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
        "/chain" or "/工具链" => ExecuteToggleToolChainCommand(),
        "/result" or "/工具结果" => ExecuteToolResultCommand(argument),
        "/mcp" => ExecuteToggleMcpCommand(),
        _ => new CommandResult 
        { 
            IsCommand = true, 
            Success = false, 
            Message = $"❎ 未知命令: \"{command}\"\n可用命令: /当前, /切换, /状态, /工具链, /工具结果, /mcp, /帮助" 
        }
    };

    return Task.FromResult(result);
}
```

#### 策略切换

```csharp
private CommandResult ExecuteSwitchCommand(string argument)
{
    // 检查参数
    if (string.IsNullOrWhiteSpace(argument))
    {
        return new CommandResult 
        { 
            IsCommand = true, 
            Success = false, 
            Message = "命令格式错误\n用法: /切换 [策略名]\n可用策略: 禁用, 空白, 混合, 优先, 强制" 
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
            Message = "未选择提示词，无法切换策略" 
        };
    }

    // 解析策略
    McpToolStrategy? newStrategy = null;
    if (EnglishStrategyMap.TryGetValue(argument, out var enStrategy))
        newStrategy = enStrategy;
    else if (ChineseStrategyMap.TryGetValue(argument, out var zhStrategy))
        newStrategy = zhStrategy;

    if (!newStrategy.HasValue)
    {
        return new CommandResult 
        { 
            IsCommand = true, 
            Success = false, 
            Message = $"未知策略: \"{argument}\"" 
        };
    }

    // 更新策略
    Settings.PromptStrategyMap[currentPrompt.Name] = newStrategy.Value;
    Context.SaveSettingStorage<Settings>();

    // 触发事件通知UI
    StrategyEvents.RaisePromptStrategyChanged(currentPrompt.Name, newStrategy.Value);

    return new CommandResult 
    { 
        IsCommand = true, 
        Success = true, 
        Message = $"✅ 已切换提示词 '{currentPrompt.Name}' 的策略" 
    };
}
```

## UI 同步机制

命令系统与设置界面的同步通过 `StrategyEvents` 实现：

1. **命令触发变更**：用户在输入框执行切换命令
2. **更新数据模型**：`ExecuteSwitchCommand` 更新 `PromptStrategyMap`
3. **触发事件**：调用 `StrategyEvents.RaisePromptStrategyChanged()`
4. **UI 接收事件**：`SettingsViewModel` 订阅事件并检查提示词名称
5. **刷新显示**：匹配时更新 `SelectedPromptStrategy` 和显示文本

```csharp
// SettingsViewModel.cs
private void OnPromptStrategyChanged(object? sender, PromptStrategyChangedEventArgs e)
{
    // 仅当当前显示的提示词与变更的提示词匹配时才更新UI
    if (Main.SelectedPrompt?.Name == e.PromptName)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SelectedPromptStrategy = PromptStrategyOptions
                .FirstOrDefault(o => o.Strategy == e.NewStrategy) 
                ?? PromptStrategyOptions.First();
            UpdateStrategyDisplayText();
        });
    }
}
```

## 开关控制

命令系统开关位于 **MCP 服务功能**卡片中：

- **开关位置**：MCP 服务功能卡片第3行
- **默认状态**：启用（`true`）
- **统辖关系**：受 MCP 服务功能开关统辖
- **禁用影响**：
  - 以 `/` 开头的输入不再被视为命令
  - 输入将直接发送给 AI 进行翻译

## 故障排除

### 命令无响应
- 检查 MCP 服务功能是否已启用
- 确认输入以 `/` 开头且格式正确

### 策略切换后UI未更新
- 检查当前显示的提示词是否与命令目标提示词一致
- 查看日志确认事件是否触发
- 重新打开设置界面查看最新状态

### "未选择提示词"错误
- 在提示词配置区域选择一个提示词
- 或使用 `/当前` 查看当前选中的提示词

## 扩展开发

### 添加新命令

1. 在 `ExecuteCommandAsync` 中添加新分支：
```csharp
var result = command switch
{
    // ... 现有命令
    "/newcmd" or "/新命令" => ExecuteNewCommand(argument),
    _ => new CommandResult { ... }
};
```

2. 实现命令处理函数：
```csharp
private CommandResult ExecuteNewCommand(string argument)
{
    // 实现逻辑
    return new CommandResult 
    { 
        IsCommand = true, 
        Success = true, 
        Message = "执行结果" 
    };
}
```

3. 更新帮助信息：
```csharp
private CommandResult ExecuteHelpCommand()
{
    // 在帮助文本中添加新命令说明
}
```

### 修改策略名称

如需添加新的策略别名，修改策略名称映射字典：

```csharp
private static readonly Dictionary<string, McpToolStrategy> EnglishStrategyMap = new(StringComparer.OrdinalIgnoreCase)
{
    ["disabled"] = McpToolStrategy.Disabled,
    ["off"] = McpToolStrategy.Disabled,  // 新增别名
    // ...
};
```

## 相关文档

- [主翻译逻辑](./main-logic.md) - 命令系统执行流程
- [设置系统](./settings-system.md) - 命令系统开关设置
- [UI布局](./ui-layout.md) - MCP服务功能卡片布局
- [工具策略](./tool-strategy.md) - MCP策略详解
