# UI布局（SettingsView.xaml）

## 文件位置
`View/SettingsView.xaml`

## 整体布局结构

```
SettingsView (UserControl)
└── ikw:SimpleStackPanel (垂直布局容器)
    ├── API配置卡片 (SettingsCard)
    │   ├── API URL (TextBox)
    │   ├── API Key (PasswordBox)
    │   └── 模型选择 (ComboBox)
    │
    ├── 提示词配置卡片 (SettingsCard)
    │   ├── 提示词选择 (ComboBox)
    │   ├── 当前策略标签 (Border + TextBlock)
    │   ├── MCP策略标签
    │   ├── 策略选择 (ComboBox，受MCP开关统辖)
    │   └── 编辑按钮 (Button)
    │
    ├── MCP服务功能卡片 (SettingsCard，2行)
    │   ├── 启用/禁用开关 (ToggleSwitch)
    │   └── 日志级别 (ComboBox，受MCP开关统辖)
    │
    ├── 命令列表卡片 (SettingsCard)
    │   └── Grid (3列表格：中文命令、英文命令、功能描述)
    │       ├── Row 0: 表头 (中文命令 | 英文命令 | 功能描述)
    │       ├── Row 1: /当前 | /now | 查看当前提示词的策略和工具设置
    │       ├── Row 2: /切换[策略] | /switch[策略] | 切换当前提示词的MCP策略
    │       ├── Row 3: /状态 | /status | 查看MCP服务状态和服务器列表
    │       ├── Row 4: /工具链 | /chain | 切换当前策略的工具链显示
    │       ├── Row 5: /工具结果[模式] | /result[模式] | 查看/切换工具结果显示模式
    │       ├── Row 6: /mcp | /mcp | 开启或关闭MCP服务
    │       └── Row 7: /帮助 | /help | 显示命令帮助信息
    │
    └── MCP服务器配置卡片 (SettingsCard)
        └── Grid (7行布局)
            ├── Row 0: 服务器配置 (ComboBox + 按钮组 + 启用开关)
            ├── Row 1: 删除确认提示 (TextBlock)
            ├── Row 2: 服务器名称 (TextBox)
            ├── Row 3: 服务器地址 (TextBox)
            ├── Row 4: 请求体 (PasswordBox)
            ├── Row 5: 工具列表 (ComboBox)
            └── Row 6: 测试连接 (Button + 结果)
```

## 详细布局说明

### MCP服务功能卡片（2行）

```xml
<ui:SettingsCard Header="MCP服务功能">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="140"/>  <!-- 开关列 -->
            <ColumnDefinition Width="Auto"/> <!-- 文字列 -->
        </Grid.ColumnDefinitions>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>   <!-- 启用/禁用 -->
            <RowDefinition Height="Auto"/>   <!-- 日志级别 -->
        </Grid.RowDefinitions>
        
        <!-- 第1行：启用/禁用（总开关，不受限制） -->
        <ui:ToggleSwitch Grid.Row="0" Grid.Column="0" 
                         IsOn="{Binding EnableMcp}"
                         HorizontalAlignment="Right"/>
        <TextBlock Grid.Row="0" Grid.Column="1" 
                   Text="启用/禁用"
                   FontWeight="SemiBold"
                   VerticalAlignment="Center"
                   Margin="16,0,0,0"/>
        
        <!-- 第2行：日志级别（受MCP开关统辖） -->
        <ComboBox Grid.Row="1" Grid.Column="0"
                  Width="140"
                  HorizontalAlignment="Right"
                  Margin="0,8,0,0"
                  IsEnabled="{Binding EnableMcp}"
                  SelectedIndex="{Binding LogLevel}">
            <ComboBoxItem Content="粗略" />
            <ComboBoxItem Content="中等" />
            <ComboBoxItem Content="详细" />
        </ComboBox>
        <TextBlock Grid.Row="1" Grid.Column="1"
                   Text="日志级别"
                   VerticalAlignment="Center"
                   Margin="16,8,0,0"/>
    </Grid>
</ui:SettingsCard>
```

**布局要点**：
- 2行使用相同的Grid列定义（140px开关列 + Auto文字列）
- 第1行"启用/禁用"是总开关，始终可用
- 第2行依赖于MCP开关，使用 `IsEnabled="{Binding EnableMcp}"` 统辖
- ToggleSwitch在第一列右对齐，文字在第二列左对齐
- 第2行顶部间距8px，与第1行区分
- 提示词配置区域的策略选择下拉框也使用相同的统辖逻辑
- **注意**：工具链显示和工具结果显示模式已移至策略级设置，通过策略编辑对话框配置

### 命令列表卡片

```xml
<ui:SettingsCard Header="命令列表">
    <ui:SettingsCard.HeaderIcon>
        <ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Code_24_Regular}" />
    </ui:SettingsCard.HeaderIcon>

    <Grid IsEnabled="{Binding EnableMcp}">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="110"/>  <!-- 中文命令列 -->
            <ColumnDefinition Width="110"/>  <!-- 英文命令列 -->
            <ColumnDefinition Width="*"/>    <!-- 功能描述列（自适应） -->
        </Grid.ColumnDefinitions>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>   <!-- 表头行 -->
            <RowDefinition Height="Auto"/>   <!-- /当前 | /now -->
            <RowDefinition Height="Auto"/>   <!-- /切换 | /switch -->
            <RowDefinition Height="Auto"/>   <!-- /状态 | /status -->
            <RowDefinition Height="Auto"/>   <!-- /工具链 | /chain -->
            <RowDefinition Height="Auto"/>   <!-- /工具结果 | /result -->
            <RowDefinition Height="Auto"/>   <!-- /mcp -->
            <RowDefinition Height="Auto"/>   <!-- /帮助 | /help -->
        </Grid.RowDefinitions>
        
        <!-- 表头 -->
        <TextBlock Grid.Row="0" Grid.Column="0" Text="中文命令" FontWeight="Bold" 
                   Foreground="{DynamicResource TextFillColorSecondaryBrush}" Margin="0,0,0,8"/>
        <TextBlock Grid.Row="0" Grid.Column="1" Text="英文命令" FontWeight="Bold" 
                   Foreground="{DynamicResource TextFillColorSecondaryBrush}" Margin="0,0,0,8"/>
        <TextBlock Grid.Row="0" Grid.Column="2" Text="功能描述" FontWeight="Bold" 
                   Foreground="{DynamicResource TextFillColorSecondaryBrush}" Margin="0,0,0,8"/>
        
        <!-- 命令1：查看当前 -->
        <TextBlock Grid.Row="1" Grid.Column="0" Text="/当前" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}" Margin="0,0,0,6"/>
        <TextBlock Grid.Row="1" Grid.Column="1" Text="/now" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}" Margin="0,0,0,6"/>
        <TextBlock Grid.Row="1" Grid.Column="2" Text="查看当前提示词的策略和工具设置" 
                   Foreground="{DynamicResource TextFillColorPrimaryBrush}" Margin="0,0,0,6"/>
        
        <!-- 命令2：切换策略 -->
        <TextBlock Grid.Row="2" Grid.Column="0" Text="/切换 [策略]" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}" Margin="0,0,0,6"/>
        <TextBlock Grid.Row="2" Grid.Column="1" Text="/switch [策略]" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}" Margin="0,0,0,6"/>
        <TextBlock Grid.Row="2" Grid.Column="2" Text="切换当前提示词的MCP策略" 
                   Foreground="{DynamicResource TextFillColorPrimaryBrush}" Margin="0,0,0,6"/>
        
        <!-- 命令3：查看状态 -->
        <TextBlock Grid.Row="3" Grid.Column="0" Text="/状态" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}" Margin="0,0,0,6"/>
        <TextBlock Grid.Row="3" Grid.Column="1" Text="/status" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}" Margin="0,0,0,6"/>
        <TextBlock Grid.Row="3" Grid.Column="2" Text="查看MCP服务状态和服务器列表" 
                   Foreground="{DynamicResource TextFillColorPrimaryBrush}" Margin="0,0,0,6"/>
        
        <!-- 命令4：工具链 -->
        <TextBlock Grid.Row="4" Grid.Column="0" Text="/工具链" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}" Margin="0,0,0,6"/>
        <TextBlock Grid.Row="4" Grid.Column="1" Text="/chain" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}" Margin="0,0,0,6"/>
        <TextBlock Grid.Row="4" Grid.Column="2" Text="切换当前策略的工具链显示" 
                   Foreground="{DynamicResource TextFillColorPrimaryBrush}" Margin="0,0,0,6"/>
        
        <!-- 命令5：工具结果 -->
        <TextBlock Grid.Row="5" Grid.Column="0" Text="/工具结果 [模式]" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}" Margin="0,0,0,6"/>
        <TextBlock Grid.Row="5" Grid.Column="1" Text="/result [模式]" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}" Margin="0,0,0,6"/>
        <TextBlock Grid.Row="5" Grid.Column="2" Text="查看/切换当前策略的工具结果显示模式" 
                   Foreground="{DynamicResource TextFillColorPrimaryBrush}" Margin="0,0,0,6"/>
        
        <!-- 命令6：MCP开关 -->
        <TextBlock Grid.Row="6" Grid.Column="0" Text="/mcp" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}" Margin="0,0,0,6"/>
        <TextBlock Grid.Row="6" Grid.Column="1" Text="/mcp" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}" Margin="0,0,0,6"/>
        <TextBlock Grid.Row="6" Grid.Column="2" Text="开启或关闭MCP服务" 
                   Foreground="{DynamicResource TextFillColorPrimaryBrush}" Margin="0,0,0,6"/>
        
        <!-- 命令7：帮助 -->
        <TextBlock Grid.Row="7" Grid.Column="0" Text="/帮助" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}"/>
        <TextBlock Grid.Row="7" Grid.Column="1" Text="/help" FontWeight="SemiBold" 
                   Foreground="{DynamicResource SystemFillColorAttentionBrush}"/>
        <TextBlock Grid.Row="7" Grid.Column="2" Text="显示命令帮助信息" 
                   Foreground="{DynamicResource TextFillColorPrimaryBrush}"/>
    </Grid>
</ui:SettingsCard>
```

**布局要点**：
- **三列布局**：中文命令（110px）、英文命令（110px）、功能描述（自适应）
- **表头清晰**：第0行显示三列表头，使用灰色次要文本颜色
- **分列对齐**：命令文本以蓝色高亮（SystemFillColorAttentionBrush），描述以主文本颜色
- **统辖控制**：整个Grid使用 `IsEnabled="{Binding EnableMcp}"`，MCP服务禁用后表格灰色显示
- **行间距**：表头与内容之间8px间距，命令行之间6px间距
- **字体样式**：表头加粗（Bold），命令半粗（SemiBold），普通文本常规粗细

### 提示词配置卡片

```xml
<ui:SettingsCard Header="提示词配置">
    <ikw:SimpleStackPanel Orientation="Horizontal" Spacing="8">
        <!-- 提示词选择下拉框 -->
        <ComboBox x:Name="PromptComboBox"
                  MinWidth="120"
                  DisplayMemberPath="Name"
                  ItemsSource="{Binding Main.Prompts}"
                  SelectedItem="{Binding Main.SelectedPrompt}"/>
        
        <!-- 当前策略显示标签 -->
        <Border Background="{DynamicResource SystemFillColorAttentionBackgroundBrush}"
                CornerRadius="4"
                Padding="6,2">
            <TextBlock Text="{Binding SelectedPromptStrategyText}"
                       FontSize="12"
                       Foreground="{DynamicResource SystemFillColorAttentionBrush}"/>
        </Border>
        
        <!-- MCP策略标签 -->
        <TextBlock Text="MCP策略:" 
                   Foreground="{DynamicResource TextFillColorSecondaryBrush}"/>
        
        <!-- 策略选择下拉框（受MCP开关统辖） -->
        <ComboBox MinWidth="120"
                  ItemsSource="{Binding PromptStrategyOptions}"
                  SelectedItem="{Binding SelectedPromptStrategy}"
                  IsEnabled="{Binding EnableMcp}"
                  DisplayMemberPath="Name"/>
        
        <!-- 编辑按钮 -->
        <Button Command="{Binding EditPromptCommand}">
            <ui:IconAndText Content="编辑" Icon="..."/>
        </Button>
    </ikw:SimpleStackPanel>
</ui:SettingsCard>
```

**布局要点**：
- 使用水平SimpleStackPanel
- 提示词选择后显示当前绑定的策略标签
- 策略标签使用动态背景色和前景色
- 策略下拉框支持5种策略选择（不含"跟随全局"）
- **重要**：策略下拉框使用 `IsEnabled="{Binding EnableMcp}"`，受MCP服务功能开关统辖
- 当MCP禁用时，策略下拉框变为灰色不可用状态
- **编辑按钮**：点击打开策略编辑对话框，可配置该策略的提示词、工具调用上限、工具结果显示模式、工具链显示等

## 详细布局说明

### 第0行：服务器配置行

```xml
<Grid Grid.Row="0" Margin="0,0,0,12">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="110" />  <!-- 标签列 -->
        <ColumnDefinition Width="*" />     <!-- 内容列 -->
        <ColumnDefinition Width="Auto" />  <!-- 启用开关列 -->
    </Grid.ColumnDefinitions>
    
    <!-- 标签：右对齐，右间距20px -->
    <TextBlock Grid.Column="0" 
               HorizontalAlignment="Right"
               Margin="0,0,20,0"
               Text="服务器配置:" />
    
    <!-- 服务器选择下拉框 -->
    <StackPanel Grid.Column="1" Orientation="Horizontal">
        <ComboBox Width="200" ... />           <!-- 服务器列表 -->
        <Button ... />                          <!-- 添加按钮 -->
        <Button ... />                          <!-- 复制按钮 -->
        <Button ... />                          <!-- 删除按钮 -->
    </StackPanel>
    
    <!-- 启用此服务器开关 -->
    <ToggleSwitch Grid.Column="2" 
                  HorizontalAlignment="Right"
                  Margin="0,0,15,0"
                  IsOn="{Binding CurrentServerEnabled}" />
</Grid>
```

**布局要点**：
- 第2列（ToggleSwitch）使用`Width="Auto"`，右对齐
- 右边距15px使开关与下方输入框右边界对齐

### 第2-4行：输入框行

```xml
<!-- 服务器名称行 -->
<Grid Grid.Row="2" Margin="0,0,0,8">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="110" />  <!-- 标签列 -->
        <ColumnDefinition Width="*" />     <!-- 输入框列（自动填充） -->
    </Grid.ColumnDefinitions>
    
    <TextBlock Grid.Column="0" ... Text="服务器名称:" />
    <TextBox Grid.Column="1" ... />   <!-- 宽度自动拉伸 -->
</Grid>

<!-- 服务器地址行 -->
<Grid Grid.Row="3" ...>
    <TextBlock ... Text="服务器地址:" />
    <TextBox Grid.Column="1" ... />
</Grid>

<!-- 请求体行 -->
<Grid Grid.Row="4" ...>
    <TextBlock ... Text="请求体:" />
    <PasswordBox Grid.Column="1" ... />
</Grid>
```

**对齐规则**：
- 所有标签列宽固定110px
- 标签右对齐，右间距20px
- 输入框列使用`*`，自动拉伸到右边界

### 第5行：工具列表行

```xml
<Grid Grid.Row="5" Margin="0,8,0,0">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="110" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    
    <TextBlock ... Text="工具列表:" />
    
    <ComboBox Grid.Column="1"
              HorizontalAlignment="Stretch"   <!-- 填满整个列宽 -->
              MinHeight="36"
              ItemsSource="{Binding McpTools}"
              ui:ControlHelper.PlaceholderText="{Binding ToolListSummary}">
        <!-- 下拉项模板：包含ToggleSwitch和工具名称 -->
        <ComboBox.ItemTemplate>
            <DataTemplate>
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="60"/>   <!-- 开关列 -->
                        <ColumnDefinition Width="*"/>    <!-- 名称列 -->
                    </Grid.ColumnDefinitions>
                    <ToggleSwitch Grid.Column="0" ... />
                    <TextBlock Grid.Column="1" ... 
                               HorizontalAlignment="Right"
                               TextTrimming="CharacterEllipsis" />
                </Grid>
            </DataTemplate>
        </ComboBox.ItemTemplate>
    </ComboBox>
</Grid>
```

**特殊样式**：
- ComboBox使用`HorizontalAlignment="Stretch"`填满列宽
- 下拉项中工具名称右对齐，超长显示省略号

### 第6行：测试连接行

```xml
<Grid Grid.Row="6" Margin="0,8,0,0">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="110" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    
    <TextBlock ... Text="测试连接:" />
    
    <StackPanel Grid.Column="1" Orientation="Horizontal">
        <Button Command="{Binding TestAndDiscoverToolsCommand}"
                Style="{StaticResource AccentButtonStyle}"
                HorizontalAlignment="Stretch"
                Width="Auto">
            <ui:IconAndText Content="测试连接" Icon="..." />
        </Button>
        
        <!-- 测试结果提示 -->
        <Border Margin="12,0,0,0">
            <TextBlock Text="{Binding McpValidateResult}" ... />
        </Border>
    </StackPanel>
</Grid>
```

## 布局对齐原则

### 水平对齐

| 元素 | 对齐方式 | 说明 |
|------|---------|------|
| 标签（服务器配置:等） | 右对齐 | `HorizontalAlignment="Right"` |
| 输入框 | 拉伸填充 | `Width="*"` 或 `HorizontalAlignment="Stretch"` |
| 第一行开关 | 右对齐 | 与下方输入框右边界对齐 |

### 间距

```
标签右间距：20px
第一行开关右间距：15px
按钮之间间距：4px
测试结果左间距：12px
```

## 样式资源

### 使用的系统样式

```xml
<!-- 强调按钮 -->
<Button Style="{StaticResource AccentButtonStyle}" ... />

<!-- 现代切换开关 -->
<ui:ToggleSwitch ... />

<!-- 字体图标 -->
<ui:FontIcon Icon="{x:Static ui:FluentSystemIcons.Add_24_Regular}" />
<ui:IconAndText Content="测试连接" Icon="..." />
```

### 动态资源

```xml
<!-- 主题色 -->
Foreground="{DynamicResource TextFillColorPrimaryBrush}"
Background="{DynamicResource SystemFillColorAttentionBackgroundBrush}"
```

## 修改建议

### 修改标签列宽

```xml
<!-- 将所有Grid的第一列宽度统一修改 -->
<ColumnDefinition Width="110" />  <!-- 改为需要的值，如120 -->
```

### 修改输入框高度

```xml
<TextBox MinHeight="32" ... />  <!-- 所有输入框保持一致 -->
```

### 修改间距

```xml
<!-- 标签右边距 -->
<TextBlock Margin="0,0,20,0" ... />  <!-- 修改第3个值 -->

<!-- 行间距 -->
<Grid Margin="0,0,0,8">  <!-- 修改第4个值 -->
```

### 添加新行

1. 在RowDefinitions中添加新行
2. 设置Grid.Row属性
3. 保持列定义与其他行一致

```xml
<Grid.RowDefinitions>
    ...
    <RowDefinition Height="Auto" />  <!-- 新行 -->
</Grid.RowDefinitions>

<Grid Grid.Row="7" ...>  <!-- 新行内容 -->
```

## 布局调试技巧

### 显示布局边界

在开发时添加背景色查看边界：
```xml
<Grid Background="Red">  <!-- 临时添加 -->
```

### 检查对齐

使用不同颜色的边框：
```xml
<Border BorderBrush="Blue" BorderThickness="1">
    <TextBlock ... />
</Border>
```

## 常见问题

### Q: 元素超出边界？
A: 检查：
1. 父容器是否设置了固定宽度
2. 子元素是否设置了过大的MinWidth
3. Margin总和是否过大

### Q: 对齐不精确？
A: 确保：
1. 所有相关元素使用相同的列宽定义
2. HorizontalAlignment设置正确
3. Margin值统一

### Q: 下拉菜单被截断？
A: 检查：
1. 父容器是否设置了ClipToBounds="True"
2. 是否有上层元素遮挡
3. Popup的Placement属性