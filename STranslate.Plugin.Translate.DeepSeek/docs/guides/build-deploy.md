# 构建和部署指南

## 环境要求

### 开发环境

- **.NET SDK**: 8.0 或更高版本
- **IDE**: Visual Studio 2022 / VS Code / Rider
- **操作系统**: Windows 10/11

### 检查环境

```bash
# 检查.NET版本
dotnet --version

# 应该输出 8.0.x 或更高
```

## 构建步骤

### 1. 克隆/打开项目

```bash
# 进入项目目录
cd STranslate.Plugin.Translate.DeepSeek

# 还原依赖
dotnet restore
```

### 2. Debug构建（开发调试）

```bash
# 构建Debug版本
dotnet build --configuration Debug

# 输出位置
# artifacts/Debug/STranslate.Plugin.Translate.DeepSeek.dll
```

### 3. Release构建（正式发布）

```bash
# 清理旧构建
dotnet clean

# 构建Release版本
dotnet build --configuration Release

# 输出位置
# artifacts/Release/STranslate.Plugin.Translate.DeepSeek.dll
```

### 4. 完整构建脚本

```bash
#!/bin/bash
# build.sh

echo "开始构建 DeepSeek 翻译插件..."

# 清理
dotnet clean

# 还原包
dotnet restore

# 构建Release
dotnet build --configuration Release --no-restore

# 检查构建结果
if [ -f "artifacts/Release/STranslate.Plugin.Translate.DeepSeek.dll" ]; then
    echo "构建成功！"
    echo "输出文件: artifacts/Release/STranslate.Plugin.Translate.DeepSeek.dll"
else
    echo "构建失败！"
    exit 1
fi
```

## 部署步骤

### 方法1：手动部署（推荐开发测试）

1. **找到STranslate插件目录**
   ```
   %AppData%/STranslate/plugins/DeepSeek/
   ```

2. **复制构建文件**
   ```
   复制 artifacts/Debug/* 到 %AppData%/STranslate/plugins/DeepSeek/
   ```

3. **必需文件清单**
   ```
   DeepSeek/
   ├── STranslate.Plugin.Translate.DeepSeek.dll    # 主程序
   ├── STranslate.Plugin.Translate.DeepSeek.pdb    # 调试符号（可选）
   ├── Languages/                                   # 语言包
   │   ├── en.xaml
   │   ├── zh-cn.xaml
 │   └── zh-tw.xaml
   ├── icon.png                                     # 插件图标
   └── plugin.json                                  # 插件元数据
   ```

4. **重启STranslate**
   - 完全退出STranslate
   - 重新启动
   - 在设置中查看DeepSeek插件

### 方法2：自动部署脚本

```powershell
# deploy.ps1

$sourcePath = "artifacts/Debug"
$targetPath = "$env:APPDATA/STranslate/plugins/DeepSeek"

Write-Host "部署 DeepSeek 翻译插件..."

# 确保目标目录存在
if (!(Test-Path $targetPath)) {
    New-Item -ItemType Directory -Path $targetPath -Force
}

# 停止STranslate进程
$process = Get-Process "STranslate" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "停止 STranslate..."
    $process.Kill()
    Start-Sleep -Seconds 2
}

# 复制文件
Write-Host "复制文件..."
Copy-Item "$sourcePath/*" $targetPath -Recurse -Force

# 启动STranslate
Write-Host "启动 STranslate..."
Start-Process "STranslate"

Write-Host "部署完成！"
```

### 方法3：VS一键调试

在Visual Studio中配置：

```json
// launchSettings.json
{
  "profiles": {
    "Deploy to STranslate": {
      "commandName": "Executable",
      "executablePath": "$(AppData)/STranslate/STranslate.exe",
      "workingDirectory": "$(AppData)/STranslate"
    }
  }
}
```

## 构建配置说明

### 项目文件结构

```xml
<!-- STranslate.Plugin.Translate.DeepSeek.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <OutputPath>artifacts/$(Configuration)</OutputPath>
  </PropertyGroup>
  
  <ItemGroup>
    <!-- 包引用 -->
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="iNKORE.UI.WPF.Modern" Version="1.2.6" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

### Debug vs Release

| 配置 | 优化 | 调试符号 | 用途 |
|------|------|---------|------|
| **Debug** | 无 | 完整 | 开发调试 |
| **Release** | 完全优化 | 无 | 正式发布 |

## 常见问题

### Q: 构建失败 "找不到SDK"

**解决：**
```bash
# 安装.NET 8 SDK
winget install Microsoft.DotNet.SDK.8

# 或从官网下载
# https://dotnet.microsoft.com/download/dotnet/8.0
```

### Q: 缺少依赖项

**解决：**
```bash
# 强制还原包
dotnet restore --force

# 清除NuGet缓存
dotnet nuget locals all --clear
dotnet restore
```

### Q: XAML编译错误

**解决：**
```bash
# 清理并重建
dotnet clean
dotnet build --no-incremental
```

### Q: 部署后插件不显示

**检查清单：**
1. [ ] 文件复制到正确目录
2. [ ] plugin.json存在且格式正确
3. [ ] STranslate完全重启（不是最小化到托盘）
4. [ ] 检查STranslate版本兼容性

### Q: 如何调试已部署的插件

**方法：**
1. 使用Debug构建部署
2. 在代码中添加日志：`Context.Logger.LogInformation()`
3. 在STranslate设置中查看日志
4. 或使用VS附加到进程调试

## 版本管理

### 版本号规范

```
主版本.次版本.修订号
例如：1.2.3
```

**更新位置：**
1. `plugin.json` 中的 `Version`
2. `AssemblyInfo.cs` 中的版本属性
3. Git标签（发布时）

### 发布检查清单

- [ ] 版本号已更新
- [ ] Release构建成功
- [ ] 所有功能测试通过
- [ ] 文档已更新
- [ ] 发布说明已编写

## CI/CD 配置（GitHub Actions示例）

```yaml
# .github/workflows/build.yml
name: Build and Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Package
      run: |
        cd artifacts/Release
        7z a ../../DeepSeek-Plugin-${GITHUB_REF#refs/tags/}.zip *
    
    - name: Create Release
      uses: softprops/action-gh-release@v1
      with:
        files: DeepSeek-Plugin-*.zip
```

## 性能优化

### 构建优化

```bash
# 启用并行构建
dotnet build -m:4

# 跳过还原（如果已还原）
dotnet build --no-restore
```

### 部署优化

- 发布时删除 `.pdb` 文件（减少体积）
- 压缩语言包（如果有大量资源）
- 使用Release配置（性能更好）

## 相关链接

- [.NET 构建文档](https://docs.microsoft.com/dotnet/core/tools/dotnet-build)
- [WPF 部署指南](https://docs.microsoft.com/dotnet/desktop/wpf/app-development/)