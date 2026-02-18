# DeepSeekMCP 翻译插件 - 维护文档

## 项目概述

**DeepSeekMCP** 是专为 **STranslate** 打造的翻译插件，支持 MCP（Model Context Protocol）服务器工具调用。插件在标准 DeepSeek 翻译基础上，允许 AI 在翻译过程中智能调用外部工具（如网络搜索、数据查询、代码执行等），实现更精准、更上下文感知的翻译体验。

## 文档结构

```
docs/
├── README.md                    # 本文档 - 入口和导航
  ├── modules/                     # 功能模块文档
  │   ├── project-structure.md     # 项目结构和文件组织
  │   ├── main-logic.md           # 主翻译逻辑（Main.cs）
  │   ├── settings-system.md      # 设置系统（Settings/ViewModel）
  │   ├── ui-layout.md            # UI布局（SettingsView.xaml）
  │   ├── mcp-client.md           # MCP客户端实现
  │   ├── tool-strategy.md        # 工具调用策略
  │   ├── mcp-tool-calling.md     # MCP工具调用详解
  │   └── command-system.md       # 命令系统
├── guides/                      # 开发和维护指南
│   ├── build-deploy.md         # 构建和部署
│   ├── troubleshooting.md      # 故障排除
│   └── adding-features.md      # 添加新功能指南
└── api/                         # API参考
    └── interfaces.md           # 接口定义
```

## 快速导航

### 按功能查找

| 功能 | 文档位置 |
|------|---------|
| 了解项目结构 | [项目结构](./modules/project-structure.md) |
| 修改翻译逻辑 | [主逻辑模块](./modules/main-logic.md) |
| 修改设置界面 | [设置系统](./modules/settings-system.md) + [UI布局](./modules/ui-layout.md) |
| 修改MCP功能 | [MCP客户端](./modules/mcp-client.md) + [工具策略](./modules/tool-strategy.md) |
| 修改命令系统 | [命令系统](./modules/command-system.md) + [主逻辑模块](./modules/main-logic.md) |
| 构建项目 | [构建部署指南](./guides/build-deploy.md) |
| 解决问题 | [故障排除](./guides/troubleshooting.md) |

### 按文件查找

| 文件 | 说明 | 文档 |
|------|------|------|
| `Main.cs` | 主翻译逻辑、命令系统 | [main-logic.md](./modules/main-logic.md) |
| `Settings.cs` | 数据模型 | [settings-system.md](./modules/settings-system.md) |
| `SettingsViewModel.cs` | 视图模型 | [settings-system.md](./modules/settings-system.md) |
| `SettingsView.xaml` | UI布局 | [ui-layout.md](./modules/ui-layout.md) |
| `McpClient.cs` | MCP客户端 | [mcp-client.md](./modules/mcp-client.md) |
| `StrategyEvents.cs` | 事件系统（命令与UI同步） | [命令系统](./modules/command-system.md) |

## 核心功能模块

### 1. 翻译引擎（Main.cs）
- 位置：`Main.cs`
- 功能：处理翻译请求，管理MCP工具调用，实现命令系统
- 关键方法：
  - `TranslateAsync()` - 主翻译入口
  - `TranslateWithMcpTools()` - MCP翻译流程
  - `TranslateWithTraditionalApi()` - 传统API翻译
  - `ExecuteCommandAsync()` - 命令系统执行

### 2. 设置系统
- 数据模型：`Settings.cs`
- 视图模型：`ViewModel/SettingsViewModel.cs`
- 视图：`View/SettingsView.xaml`
- 功能：管理插件配置、MCP服务器配置、命令系统开关

### 3. MCP集成
- 客户端：`McpClient.cs`
- 功能：连接MCP服务器，发现和调用工具
- 策略：`McpToolStrategy` 枚举定义5种策略（提示词级绑定，无全局策略）

### 4. 命令系统
- 位置：`Main.cs`（命令处理） + `StrategyEvents.cs`（事件同步）
- 功能：允许用户通过命令快速切换MCP策略和工具设置
- 命令：`/当前`, `/切换`, `/状态`, `/工具链`, `/工具结果`, `/mcp`, `/帮助`
- 统辖：命令系统受MCP服务功能开关统辖

### 5. UI布局
- 主界面：`View/SettingsView.xaml`
- 包含：服务器配置、工具列表、测试连接、命令系统开关等

## 重要变更（v3.0+）

### 架构变更
- **新增策略级工具设置**：工具链显示和工具结果显示模式现在按策略设置，不再是全局设置
- **新增工具结果显示模式**：支持4种显示模式（禁用/粗略/混合/详细）
- **新增命令**：`/工具链`（切换工具链显示）、`/工具结果`（切换显示模式）、`/mcp`（开关MCP服务）
- **优化/状态命令**：移除"命令系统"状态显示（因为命令系统受MCP开关统辖）
- **工具调用控制**：新增策略级总工具调用上限和同一工具连续调用上限
- **设置保存防抖**：实现延迟保存机制，避免频繁写磁盘

### 文件变更
- **新增** `StrategyPromptDialog.xaml` - 策略提示词编辑对话框
- **新增** `StrategyPromptDialogViewModel.cs` - 对话框视图模型
- **新增** `ThreeStageContentBuilder` - 三阶段内容构建器（Main.cs 内）
- **新增** `StrategyEvents.cs` - 弱引用事件管理器（UI同步）
- **修改** `Main.cs` - 添加三阶段内容构建、工具调用控制、命令系统
- **修改** `Settings.cs` - 添加策略级设置字典（ToolChainDisplay、ToolResultDisplayModes、TotalToolCallsLimits、ConsecutiveToolLimits）
- **修改** `SettingsView.xaml` - 添加策略提示词编辑按钮、命令列表
- **修改** `SettingsViewModel.cs` - 添加策略编辑命令、防抖保存

## 重要变更（v2.0+）

### 架构变更
- **移除全局策略**：不再支持"全局策略"设置，改为纯提示词级策略绑定
- **新增命令系统**：支持通过命令快速切换策略（`/切换 [策略名]`）
- **MCP开关统辖**：命令系统和提示词策略选择都受MCP服务功能开关统辖
- **默认策略变更**：新提示词默认使用"禁用服务"策略（原"跟随全局"）

### 文件变更
- **新增** `StrategyEvents.cs` - 弱引用事件管理器（命令与UI同步）
- **修改** `Main.cs` - 添加命令系统处理逻辑
- **修改** `Settings.cs` - 移除 `ToolStrategy`，添加 `EnableCommandSystem`
- **修改** `SettingsView.xaml` - 合并MCP服务功能卡片（4行）

## 开发注意事项

1. **不要直接修改生成的文件** - 只修改源代码文件
2. **构建前保存所有更改** - 特别是XAML文件
3. **测试多种策略** - 修改后测试所有5种MCP策略
4. **测试命令系统** - 验证所有命令和错误处理
5. **保持向后兼容** - 注意 `PromptStrategyMap` 值类型从 `McpToolStrategy?` 改为 `McpToolStrategy`
6. **MCP开关统辖** - 添加新的MCP相关控件时，使用 `IsEnabled="{Binding EnableMcp}"` 统辖

## 相关链接

- STranslate 主项目：[GitHub](https://github.com/ZGGSONG/STranslate)
- 本项目基于 [STranslate.Plugin.Translate.DeepSeek](https://github.com/monbed/STranslate.Plugin.Translate.DeepSeek) 修改而来，遵循 MIT 许可证。
- DeepSeek API 文档：https://platform.deepseek.com/
- MCP 协议规范：https://modelcontextprotocol.io/

---

*最后更新：2026年2月16日*  
*文档版本：v3.0*