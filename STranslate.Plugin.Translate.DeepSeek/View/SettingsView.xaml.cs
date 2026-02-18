using System.Windows;
using System.Windows.Controls;
using STranslate.Plugin.Translate.DeepSeek.ViewModel;

namespace STranslate.Plugin.Translate.DeepSeek.View;

public partial class SettingsView
{
    public SettingsView() => InitializeComponent();

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