# 项目结构

## 目录组织

```
STranslate.Plugin.Translate.DeepSeek/
├── Main.cs                          # 主翻译逻辑（核心）
├── Settings.cs                      # 数据模型和配置
├── McpClient.cs                     # MCP客户端实现
├── McpServerConfig.cs               # MCP服务器配置类
├── McpToolConfig.cs                 # MCP工具配置类
├── View/
│   └── SettingsView.xaml            # 设置界面UI
├── ViewModel/
│   └── SettingsViewModel.cs         # 设置界面逻辑
├── Converters/
│   └── StrategyConverters.cs        # 策略枚举转换器
├── Languages/
│   ├── en.json                      # 英文语言包（插件名称和描述）
│   ├── zh-cn.json                   # 简体中文语言包（插件名称和描述）
│   └── zh-tw.json                   # 繁体中文语言包（插件名称和描述）
├── docs/                            # 本文档目录
├── artifacts/                       # 构建输出
│   └── Debug/
│       └── STranslate.Plugin.Translate.DeepSeek.dll
└── plugin.json                      # 插件元数据
```

## 文件依赖关系

```
Main.cs
    ├── Settings.cs              (依赖)
    ├── McpClient.cs             (依赖)
    └── ViewModel/
        └── SettingsViewModel.cs (通过Context访问)

SettingsView.xaml
    ├── SettingsViewModel.cs     (DataContext)
    ├── Settings.cs              (数据类型)
    └── Converters/              (值转换器)

SettingsViewModel.cs
    ├── Settings.cs              (数据模型)
    └── Main.cs                  (业务逻辑)
```

## 核心类说明

### Main.cs
- **类名**：`Main`
- **职责**：实现 `ITranslator` 接口，处理翻译请求
- **关键方法**：
  - `TranslateAsync()` - 异步翻译入口
  - `TranslateWithMcpTools()` - MCP工具翻译流程
  - `TranslateWithTraditionalApi()` - 传统DeepSeek API翻译
- **状态管理**：`_mcpClients` - MCP客户端列表

### Settings.cs
- **类名**：`Settings`
- **职责**：定义所有配置数据模型
- **关键属性**：
  - `McpServers` - MCP服务器列表
  - `ToolStrategy` - 工具策略（Blank/Hybrid/ToolFirst/ToolForced）
  - `EnableMcp` - MCP总开关

### McpClient.cs
- **类名**：`McpClient`
- **职责**：管理单个MCP服务器连接
- **关键方法**：
  - `ConnectAsync()` - 连接服务器
  - `ListToolsAsync()` - 获取可用工具
  - `CallToolAsync()` - 调用工具

### SettingsViewModel.cs
- **类名**：`SettingsViewModel`
- **职责**：设置界面逻辑和命令
- **关键属性**：
  - `McpServers` - 服务器列表（ObservableCollection）
  - `CurrentServerIndex` - 当前选中服务器
  - `ToolStrategy` - 当前工具策略
- **关键命令**：
  - `TestAndDiscoverToolsCommand` - 测试连接并发现工具
  - `AddNewServerCommand` - 添加服务器

## 数据流

```
用户操作 → SettingsView.xaml 
    → SettingsViewModel.cs (命令处理)
        → Settings.cs (数据更新)
            → Main.cs (翻译时使用)
                → McpClient.cs (调用MCP工具)
```

## 构建输出

- **DLL文件**：`artifacts/Debug/STranslate.Plugin.Translate.DeepSeek.dll`
- **部署方式**：复制到 STranslate 的插件目录
- **依赖项**：
  - CommunityToolkit.Mvvm
  - iNKORE.UI.WPF.Modern
  - Newtonsoft.Json

## 配置文件

插件会自动在 STranslate 配置目录中创建配置文件：
- **位置**：`%AppData%/STranslate/plugins/`
- **格式**：JSON
- **内容**：Settings.cs 的序列化数据

## 语言文件说明

**重要**：语言文件采用 JSON 格式定义插件在 STranslate 界面中显示的名称和描述。

### 文件位置
```
Languages/
├── en.json     # 英文显示名称和描述
├── zh-cn.json  # 简体中文显示名称和描述
└── zh-tw.json  # 繁体中文显示名称和描述
```

### JSON 格式
```json
{
  "Name": "DeepSeekMCP",
  "Description": "DeepSeek translation plugin with MCP server tool support..."
}
```

### 关键字段
- **Name**: 插件在 STranslate 界面列表中显示的名称（**重要：不是 plugin.json 中的 Name**）
- **Description**: 插件的描述信息，鼠标悬停时显示

### 注意事项
- 修改语言文件后需要**重启 STranslate** 才能生效
- 确保所有语言文件保持一致，避免不同语言环境下显示不一致
- 描述应该简洁明了，突出 MCP 工具调用特色

## 修改建议

1. **修改UI布局**：编辑 `View/SettingsView.xaml`
2. **修改界面逻辑**：编辑 `ViewModel/SettingsViewModel.cs`
3. **修改翻译逻辑**：编辑 `Main.cs`
4. **添加新配置项**：
   - 在 `Settings.cs` 添加属性
   - 在 `SettingsViewModel.cs` 添加绑定属性
   - 在 `SettingsView.xaml` 添加UI元素