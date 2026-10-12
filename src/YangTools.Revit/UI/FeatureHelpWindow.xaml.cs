using System;
using System.Windows;
using System.Windows.Input;

namespace YangTools.Revit.UI
{
    /// <summary>
    /// YangTools 通用、可选择复制的功能说明窗口。
    /// </summary>
    public partial class FeatureHelpWindow : Window
    {
        public FeatureHelpWindow(string title, string content, string? subtitle = null)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            Title = string.IsNullOrWhiteSpace(title) ? "功能说明" : title.Trim();
            TitleText.Text = Title;
            SubtitleText.Text = string.IsNullOrWhiteSpace(subtitle) ? "用途、操作步骤与注意事项" : subtitle.Trim();
            ContentTextBox.Text = content ?? string.Empty;
        }

        public static void Show(Window owner, string title, string content, string? subtitle = null)
        {
            var window = new FeatureHelpWindow(title, content, subtitle) { Owner = owner };
            window.ShowDialog();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ContentTextBox.Text ?? string.Empty);
            }
            catch
            {
                // 剪贴板暂时被其他程序占用时，不阻断关闭窗口。
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
