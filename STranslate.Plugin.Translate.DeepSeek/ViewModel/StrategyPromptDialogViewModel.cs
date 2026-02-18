using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using static STranslate.Plugin.Translate.DeepSeek.Main;

namespace STranslate.Plugin.Translate.DeepSeek.ViewModel;

/// <summary>
/// 策略提示词编辑对话框视图模型
/// </summary>
public partial class StrategyPromptDialogViewModel : ObservableObject
{
    private readonly Settings _settings;
    private readonly Window _dialog;
    
    [ObservableProperty]
    private ObservableCollection<StrategyOption> _strategyOptions = new();
    
    [ObservableProperty]
    private StrategyOption? _selectedStrategy;
    
    [ObservableProperty]
    private string _currentPrompt = "";
    
    [ObservableProperty]
    private bool _showValidationWarning = false;
    
    [ObservableProperty]
    private bool _confirmWithoutVariable = false;
    
    [ObservableProperty]
    private int _consecutiveToolLimit = StrategyConsecutiveLimitHelper.DEFAULT_LIMIT;
    
    [ObservableProperty]
    private int _totalToolCallsLimit = StrategyTotalToolCallsHelper.DEFAULT_LIMIT;
    
    [ObservableProperty]
    private ToolResultDisplayMode _selectedToolResultDisplayMode = ToolResultDisplayMode.Disabled;
    
    [ObservableProperty]
    private bool _enableToolChainDisplay = false;
    
    [ObservableProperty]
    private string _previewText = "";
    
    [ObservableProperty]
    private bool _showPreview = false;
    
    public List<ToolResultDisplayModeOption> ToolResultDisplayModeOptions { get; } = new()
    {
        new ToolResultDisplayModeOption(ToolResultDisplayMode.Disabled, "禁用结果"),
        new ToolResultDisplayModeOption(ToolResultDisplayMode.Minimal, "粗略结果"),
        new ToolResultDisplayModeOption(ToolResultDisplayMode.Mixed, "混合显示"),
        new ToolResultDisplayModeOption(ToolResultDisplayMode.Detailed, "详细结果")
    };

    public StrategyPromptDialogViewModel(Settings settings, Window dialog)
    {
        _settings = settings;
        _dialog = dialog;
        
        // 初始化策略列表（固定4个，不允许增删）
        StrategyOptions.Add(new StrategyOption(McpToolStrategy.Blank, "空白策略"));
        StrategyOptions.Add(new StrategyOption(McpToolStrategy.Hybrid, "混合判断"));
        StrategyOptions.Add(new StrategyOption(McpToolStrategy.ToolFirst, "工具优先"));
        StrategyOptions.Add(new StrategyOption(McpToolStrategy.ToolForced, "工具强制"));
        
        // 默认选中第一个
        SelectedStrategy = StrategyOptions.First();
        
        // 监听选中项变化
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SelectedStrategy))
            {
                LoadStrategyPrompt();
                LoadTotalToolCallsLimit();
                LoadConsecutiveLimit();
                LoadToolResultDisplayMode();
                LoadToolChainDisplay();
            }
            else if (e.PropertyName == nameof(CurrentPrompt))
            {
                ValidatePrompt();
                UpdatePreview();
            }
        };
        
        LoadStrategyPrompt();
        LoadTotalToolCallsLimit();
        LoadConsecutiveLimit();
        LoadToolResultDisplayMode();
        LoadToolChainDisplay();
    }

    /// <summary>
    /// 加载选中策略的提示词
    /// </summary>
    private void LoadStrategyPrompt()
    {
        if (SelectedStrategy == null) return;
        
        if (_settings.CustomStrategyPrompts.TryGetValue(SelectedStrategy.Strategy, out var customPrompt))
        {
            CurrentPrompt = customPrompt;
        }
        else
        {
            CurrentPrompt = DefaultStrategyPrompts.GetDefaultPrompt(SelectedStrategy.Strategy);
        }
        
        ConfirmWithoutVariable = false;
        ShowValidationWarning = false;
    }

    /// <summary>
    /// 加载选中策略的连续调用上限
    /// </summary>
    private void LoadConsecutiveLimit()
    {
        if (SelectedStrategy == null) return;
        
        ConsecutiveToolLimit = StrategyConsecutiveLimitHelper.GetLimit(
            _settings.StrategyConsecutiveToolLimits, 
            SelectedStrategy.Strategy);
    }

    /// <summary>
    /// 加载选中策略的总工具调用上限
    /// </summary>
    private void LoadTotalToolCallsLimit()
    {
        if (SelectedStrategy == null) return;
        
        TotalToolCallsLimit = StrategyTotalToolCallsHelper.GetLimit(
            _settings.StrategyTotalToolCallsLimits,
            SelectedStrategy.Strategy);
    }

    /// <summary>
    /// 加载选中策略的工具结果显示模式
    /// </summary>
    private void LoadToolResultDisplayMode()
    {
        if (SelectedStrategy == null) return;
        
        if (_settings.StrategyToolResultDisplayModes.TryGetValue(SelectedStrategy.Strategy, out var mode))
        {
            SelectedToolResultDisplayMode = mode;
        }
        else
        {
            SelectedToolResultDisplayMode = ToolResultDisplayMode.Disabled; // 默认禁用结果
        }
    }

    /// <summary>
    /// 加载选中策略的工具链显示开关
    /// </summary>
    private void LoadToolChainDisplay()
    {
        if (SelectedStrategy == null) return;
        
        if (_settings.StrategyToolChainDisplay.TryGetValue(SelectedStrategy.Strategy, out var enabled))
        {
            EnableToolChainDisplay = enabled;
        }
        else
        {
            EnableToolChainDisplay = false; // 默认关闭
        }
    }

    /// <summary>
    /// 验证提示词（检查是否包含变量）
    /// </summary>
    private void ValidatePrompt()
    {
        if (string.IsNullOrWhiteSpace(CurrentPrompt))
        {
            ShowValidationWarning = false;
            return;
        }
        
        bool hasVariable = CurrentPrompt.Contains("$description_rough") || 
                          CurrentPrompt.Contains("$description_detailed");
        
        // 如果没有变量且未确认过，显示警告
        ShowValidationWarning = !hasVariable && !ConfirmWithoutVariable;
    }

    /// <summary>
    /// 重置为默认提示词
    /// </summary>
    [RelayCommand]
    private void ResetToDefault()
    {
        if (SelectedStrategy == null) return;
        
        var result = MessageBox.Show(
            $"确定要将【{SelectedStrategy.Name}】的提示词重置为默认吗？",
            "确认重置",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            CurrentPrompt = DefaultStrategyPrompts.GetDefaultPrompt(SelectedStrategy.Strategy);
            ConsecutiveToolLimit = StrategyConsecutiveLimitHelper.DEFAULT_LIMIT;
            TotalToolCallsLimit = StrategyTotalToolCallsHelper.DEFAULT_LIMIT;
            SelectedToolResultDisplayMode = ToolResultDisplayMode.Disabled;
            EnableToolChainDisplay = false;
            ConfirmWithoutVariable = false;
        }
    }

    /// <summary>
    /// 保存提示词
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        if (SelectedStrategy == null) return;
        
        // 检查是否包含变量
        bool hasVariable = CurrentPrompt.Contains("$description_rough") || 
                          CurrentPrompt.Contains("$description_detailed");
        
        // 如果没有变量且未确认，先显示警告
        if (!hasVariable && !ConfirmWithoutVariable)
        {
            ShowValidationWarning = true;
            ConfirmWithoutVariable = true; // 下次点击将确认保存
            return;
        }
        
        // 保存提示词、上限值、工具结果显示模式和工具链显示开关
        _settings.CustomStrategyPrompts[SelectedStrategy.Strategy] = CurrentPrompt;
        _settings.StrategyConsecutiveToolLimits[SelectedStrategy.Strategy] = ConsecutiveToolLimit;
        _settings.StrategyTotalToolCallsLimits[SelectedStrategy.Strategy] = TotalToolCallsLimit;
        _settings.StrategyToolResultDisplayModes[SelectedStrategy.Strategy] = SelectedToolResultDisplayMode;
        _settings.StrategyToolChainDisplay[SelectedStrategy.Strategy] = EnableToolChainDisplay;
        
        _dialog.DialogResult = true;
        _dialog.Close();
    }

    /// <summary>
    /// 取消编辑
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _dialog.DialogResult = false;
        _dialog.Close();
    }
    
    /// <summary>
    /// 切换预览显示
    /// </summary>
    [RelayCommand]
    private void TogglePreview()
    {
        ShowPreview = !ShowPreview;
        if (ShowPreview)
        {
            UpdatePreview();
        }
    }
    
    /// <summary>
    /// 更新预览文本
    /// </summary>
    private void UpdatePreview()
    {
        if (!ShowPreview) return;
        
        var prompt = CurrentPrompt;
        
        // 模拟替换变量占位符
        if (prompt.Contains("$description_rough"))
        {
            var roughDescription = "【简单工具列表示例】\n" +
                "• search_tools - 搜索可用工具\n" +
                "• web_search - 网页搜索\n" +
                "• calculator - 计算器\n" +
                "...(共N个工具)";
            prompt = prompt.Replace("$description_rough", roughDescription);
        }
        
        if (prompt.Contains("$description_detailed"))
        {
            var detailedDescription = "【详细工具列表示例】\n" +
                "• search_tools - 根据查询搜索最合适的可用工具\n" +
                "  参数: query (string, required)\n" +
                "  返回: 相关工具列表\n\n" +
                "• web_search - 使用网页搜索查询信息\n" +
                "  参数: query (string, required)\n" +
                "  返回: 搜索结果\n" +
                "...(共N个工具)";
            prompt = prompt.Replace("$description_detailed", detailedDescription);
        }
        
        PreviewText = $"【预览】\n{prompt}";
    }
}

/// <summary>
/// 策略选项
/// </summary>
public class StrategyOption
{
    public McpToolStrategy Strategy { get; }
    public string Name { get; }

    public StrategyOption(McpToolStrategy strategy, string name)
    {
        Strategy = strategy;
        Name = name;
    }
}
