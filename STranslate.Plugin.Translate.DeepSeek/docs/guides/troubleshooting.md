# 故障排除指南

## 快速诊断

### 问题分类

| 症状 | 可能原因 | 查看文档 |
|------|---------|---------|
| 翻译失败 | API配置/网络/MCP连接 | [API问题](#api问题) |
| 工具不调用 | 策略设置/MCP配置 | [MCP问题](#mcp问题) |
| UI显示异常 | XAML绑定/布局 | [UI问题](#ui问题) |
| 设置不保存 | ViewModel/持久化 | [设置问题](#设置问题) |
| 构建失败 | 环境/依赖 | [构建问题](#构建问题) |

## API问题

### 症状：翻译返回错误

**检查清单：**
1. API密钥是否正确配置
2. API地址是否可访问
3. 网络连接是否正常
4. DeepSeek服务状态

**调试步骤：**
```csharp
// 在Main.cs中添加日志
public async Task TranslateAsync(...)
{
    _logger.LogInformation($"使用模型: {Settings.Model}");
    _logger.LogInformation($"API地址: {Settings.Url}");
    
    try
    {
        // 翻译逻辑
    }
    catch (Exception ex)
    {
        _logger.LogError($"翻译失败: {ex}");
        throw;
    }
}
```

### 症状：API响应超时

**解决：**
```csharp
// 在HttpClient中添加超时设置
_httpClient.Timeout = TimeSpan.FromSeconds(60);  // 增加到60秒
```

### 症状：返回乱码

**检查：**
- 请求编码是否为UTF-8
- 响应内容类型是否正确

## MCP问题

### 症状：工具链不显示

**可能原因：**
1. MCP未启用
2. 当前策略的工具链显示开关未开启
3. 所有服务器都未启用
4. 服务器连接失败

**调试：**
```csharp
// 检查当前策略的工具链显示设置
var strategy = Settings.PromptStrategyMap.TryGetValue(promptName, out var s) ? s : McpToolStrategy.Disabled;
var toolChainEnabled = Settings.StrategyToolChainDisplay.TryGetValue(strategy, out var tc) ? tc : false;
_logger.LogInformation($"策略 {strategy} 的工具链显示: {toolChainEnabled}");

// 检查MCP客户端
if (_mcpClients.Count == 0)
{
    _logger.LogWarning("没有MCP客户端");
}

foreach (var client in _mcpClients)
{
    _logger.LogInformation($"客户端状态: {client.IsConnected}");
}
```

**解决方案：**
1. 使用 `/工具链` 命令切换当前策略的工具链显示
2. 或打开策略编辑对话框，开启"显示工具链"开关

### 症状：MCP连接失败

**检查清单：**
1. 服务器URL是否正确
2. 服务器是否运行
3. 防火墙是否阻挡
4. API密钥是否正确（如果需要）

**测试连接：**
```bash
# 使用curl测试MCP服务器
curl http://your-mcp-server:3000/mcp \
  -X POST \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
```

### 症状：工具调用失败

**常见错误：**

| 错误码 | 含义 | 解决 |
|--------|------|------|
| -32601 | 方法不存在 | 检查工具名称拼写 |
| -32602 | 参数错误 | 检查参数格式和类型 |
| -32603 | 内部错误 | 查看MCP服务器日志 |
| -32700 | 解析错误 | 检查JSON格式 |

### 症状：ToolForced策略报错

**检查：**
1. 至少有一个服务器启用
2. 该服务器至少有一个工具启用
3. 服务器连接成功

### 症状：提示词策略绑定不生效

**可能原因：**
1. MCP服务功能未开启
2. 提示词策略设置为"跟随全局"但全局策略是"禁用服务"
3. 策略绑定未正确保存

**调试步骤：**
```csharp
// 检查三层优先级
_logger.LogInformation($"MCP总开关: {Settings.EnableMcp}");
_logger.LogInformation($"当前提示词: {Main.SelectedPrompt?.Name}");
_logger.LogInformation($"提示词绑定策略: {Settings.PromptStrategyMap.GetValueOrDefault(Main.SelectedPrompt?.Name, null)}");
_logger.LogInformation($"全局策略: {Settings.ToolStrategy}");

// 计算实际生效的策略
var effectiveStrategy = GetEffectiveStrategy();
_logger.LogInformation($"实际生效策略: {effectiveStrategy}");
```

**解决：**
1. 确保"MCP服务功能"开关已开启
2. 检查提示词配置区域的策略标签显示
3. 重新选择策略并观察是否保存成功

### 症状：策略优先级混乱

**三层优先级检查：**

```
第1层：MCP服务功能（总开关）
└─ 如果关闭，无视所有策略绑定

第2层：提示词级策略绑定
└─ 检查 Settings.PromptStrategyMap[提示词名称]
└─ null 表示跟随全局

第3层：全局策略
└─ Settings.ToolStrategy
```

**常见配置错误：**
| 现象 | 原因 | 解决 |
|------|------|------|
| 绑定策略后仍使用传统翻译 | MCP总开关关闭 | 开启"MCP服务功能" |
| 提示词显示[跟随全局]但行为不一致 | 全局策略被修改 | 检查全局策略设置 |
| 某些提示词无法绑定策略 | 提示词名称包含特殊字符 | 使用简单名称 |

## UI问题

### 症状：绑定不生效

**调试方法：**
```xml
<!-- 在XAML中添加FallbackValue -->
<TextBlock Text="{Binding CurrentServerName, FallbackValue='绑定失败'}" />

<!-- 检查DataContext -->
<Grid DataContext="{Binding}">
    <TextBlock Text="{Binding DataContext.GetType().Name}" />
</Grid>
```

### 症状：布局错乱

**排查：**
1. 检查Grid的Row/Column定义
2. 检查Margin和Padding
3. 检查Horizontal/VerticalAlignment
4. 使用Snoop工具检查可视化树

**临时调试：**
```xml
<Grid Background="Red">  <!-- 显示边界 -->
    <Border BorderBrush="Blue" BorderThickness="1">  <!-- 显示边框 -->
        <TextBlock ... />
    </Border>
</Grid>
```

### 症状：下拉框不显示

**检查：**
1. ItemsSource是否绑定正确
2. ItemTemplate是否正确
3. 是否有足够的高度（MinHeight）
4. 是否被其他元素遮挡（Z-Index）

## 设置问题

### 症状：设置不保存

**调试步骤：**

1. **检查属性变更通知**
```csharp
// 在ViewModel中添加日志
partial void OnCurrentServerNameChanged(string value)
{
    _logger.LogInformation($"服务器名称变更: {value}");
    SaveSettings();
}
```

2. **检查保存调用**
```csharp
public void SaveSettings()
{
    _logger.LogInformation("调用SaveSettings");
    Context.SaveSettings(Settings);
}
```

3. **检查配置文件权限**
```
%AppData%/STranslate/plugins/DeepSeek/
确保有写入权限
```

### 症状：设置丢失

**可能原因：**
1. 配置文件被删除
2. 反序列化失败（版本不兼容）
3. 权限问题

**解决：**
```csharp
// 添加配置版本控制
public class Settings
{
    public int ConfigVersion { get; set; } = 1;  // 配置版本
    
    // 迁移逻辑
    public void Migrate()
    {
        if (ConfigVersion < 2)
        {
            // 执行迁移
            ConfigVersion = 2;
        }
    }
}
```

## 构建问题

### 症状：缺少命名空间

**解决：**
```bash
# 还原包
dotnet restore

# 清理并重建
dotnet clean
dotnet build --no-incremental
```

### 症状：XAML编译错误

**常见错误：**
1. `MC3072: 属性不存在` - 检查拼写和命名空间
2. `MC3029: 元素不存在` - 检查程序集引用
3. `MC6000: 项目文件问题` - 检查.csproj文件

### 症状：运行时缺少DLL

**检查：**
```bash
# 查看依赖树
dotnet list package --include-transitive

# 确保所有依赖项都复制到输出目录
```

## 性能问题

### 症状：翻译速度慢

**优化建议：**
1. 减少MCP工具调用次数
2. 使用更小的模型（如 deepseek-chat 而非 deepseek-coder）
3. 减少max_tokens参数
4. 检查网络延迟

**监控代码：**
```csharp
var stopwatch = Stopwatch.StartNew();
await TranslateAsync(...);
_logger.LogInformation($"翻译耗时: {stopwatch.ElapsedMilliseconds}ms");
```

### 症状：内存占用高

**检查点：**
1. 是否及时释放McpClient（Dispose）
2. 是否缓存了过多数据
3. 日志级别是否设置为详细（产生大量日志）

## 日志分析

### 启用详细日志

在设置界面选择"日志级别：详细"

### 关键日志标记

```
[MCP] 连接服务器: ...          # MCP连接
[MCP] 获取到 X 个工具          # 工具发现
[MCP] AI决定调用工具           # 工具调用决策
[MCP] 调用工具: tool_name      # 工具执行
[API] 请求: ...                # API请求
[API] 响应: ...                # API响应
```

### 日志文件位置

```
%AppData%/STranslate/logs/
```

## 调试技巧

### 使用Visual Studio调试

1. **附加到进程**
   - 启动STranslate
   - VS: 调试 → 附加到进程 → 选择STranslate.exe
   - 设置断点

2. **设置启动项目**
   ```json
   // launchSettings.json
   {
     "profiles": {
       "Debug with STranslate": {
         "commandName": "Executable",
         "executablePath": "$(AppData)/STranslate/STranslate.exe"
       }
     }
   }
   ```

### 使用日志调试

```csharp
// 不同级别的日志
_logger.LogDebug("调试信息");     // 详细级别
_logger.LogInformation("普通信息"); // 中等级别
_logger.LogWarning("警告信息");     // 粗略级别
_logger.LogError("错误信息");       // 始终显示
```

### 使用单元测试

```csharp
[Test]
public async Task TestTranslate()
{
    var main = new Main(context, settings);
    var request = new TranslateRequest { Text = "Hello", ... };
    var result = new TranslateResult();
    
    await main.TranslateAsync(request, result, CancellationToken.None);
    
    Assert.IsFalse(result.IsError);
    Assert.IsNotEmpty(result.Text);
}
```

## 提交Issue

如果以上方法无法解决问题，请提交Issue并提供：

1. **环境信息**
   - Windows版本
   - .NET版本
   - STranslate版本
   - 插件版本

2. **复现步骤**
   - 详细操作步骤
   - 预期结果
   - 实际结果

3. **日志文件**
   - `%AppData%/STranslate/logs/` 下的相关日志

4. **配置文件**
   - `%AppData%/STranslate/plugins/DeepSeek/` 下的配置文件（删除敏感信息）

## 常用命令速查

```bash
# 构建
dotnet build
dotnet build --configuration Release

# 清理
dotnet clean

# 还原
dotnet restore

# 运行测试
dotnet test

# 发布
dotnet publish --configuration Release

# 检查依赖
dotnet list package

# 查看信息
dotnet --info
```

---

*遇到未列出的问题？请提交Issue或查看项目Wiki。*