using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using STranslate.Plugin;

namespace STranslate.Plugin.Translate.DeepSeek.View;

/// <summary>
/// 策略提示词编辑对话框
/// </summary>
public partial class StrategyPromptDialog : Window
{
    private readonly IPluginContext _context;
    
    public StrategyPromptDialog(IPluginContext context)
    {
        InitializeComponent();
        _context = context;
        
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 应用主题（使用 Fork 版本 IPluginContext 的 ApplyTheme 方法）
        // 通过反射调用以兼容不同版本
        try
        {
            var applyThemeMethod = _context.GetType().GetMethod("ApplyTheme", new[] { typeof(Window) });
            if (applyThemeMethod != null)
            {
                applyThemeMethod.Invoke(_context, new object[] { this });
            }
        }
        catch
        {
            // 如果方法不存在或调用失败，使用默认主题
        }
        
        // 设置焦点到文本框
        if (PromptTextBox != null)
        {
            PromptTextBox.Focus();
        }
        
        // 确保滚动条在顶部（延迟执行，等待数据绑定完成）
        Dispatcher.BeginInvoke(() =>
        {
            RightPanelScrollViewer?.ScrollToTop();
        }, DispatcherPriority.Loaded);
    }
    
    /// <summary>
    /// 复制变量到剪贴板
    /// </summary>
    private void CopyVariable_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock textBlock && textBlock.Tag is string variable)
        {
            Clipboard.SetText(variable);
        }
    }
}
