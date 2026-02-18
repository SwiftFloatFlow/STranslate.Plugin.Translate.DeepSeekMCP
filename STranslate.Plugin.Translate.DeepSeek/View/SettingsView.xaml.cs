using System.Windows;
using System.Windows.Controls;
using STranslate.Plugin.Translate.DeepSeek.ViewModel;

namespace STranslate.Plugin.Translate.DeepSeek.View;

public partial class SettingsView
{
    public SettingsView()
    {
        InitializeComponent();
        
        // 订阅激活事件，用于刷新全局提示词
        Loaded += OnSettingsViewLoaded;
        IsVisibleChanged += OnVisibilityChanged;
    }
    
    /// <summary>
    /// 设置视图加载时刷新全局提示词
    /// </summary>
    private void OnSettingsViewLoaded(object sender, RoutedEventArgs e)
    {
        RefreshGlobalPrompts();
    }
    
    /// <summary>
    /// 可见性改变时刷新全局提示词（当从隐藏变为显示时）
    /// </summary>
    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 当界面变为可见时刷新全局提示词
        if (e.NewValue is bool isVisible && isVisible)
        {
            RefreshGlobalPrompts();
        }
    }
    
    /// <summary>
    /// 刷新全局提示词列表
    /// </summary>
    private void RefreshGlobalPrompts()
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.Main.RefreshGlobalPrompts();
        }
    }

    private void OnToolSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 清除选中，保持显示"工具列表"
        if (sender is ComboBox comboBox)
        {
            comboBox.SelectedItem = null;
        }
    }

    private void OnToolToggleChanged(object sender, RoutedEventArgs e)
    {
        // 获取ViewModel并通知更新统计
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.RefreshToolSummary();
        }
    }
}