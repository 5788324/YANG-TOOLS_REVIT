using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.UI;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public partial class AlignTextToLineWindow : Window
    {
        public AlignTextToLineState State { get; }
        public TextToolDialogAction Action { get; private set; } = TextToolDialogAction.Cancel;

        public AlignTextToLineWindow(AlignTextToLineState state)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            State = state ?? throw new ArgumentNullException(nameof(state));
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateStatus();
            ChkKeepUpright.IsChecked = State.KeepUpright;
            SelectComboValue(CmbAlignBase, State.AlignBaseTag, "Retain");
        }

        private void BtnSelectText_Click(object sender, RoutedEventArgs e)
        {
            SaveOptions();
            Action = TextToolDialogAction.SelectPrimary;
            DialogResult = false;
        }

        private void BtnSelectLine_Click(object sender, RoutedEventArgs e)
        {
            SaveOptions();
            Action = TextToolDialogAction.SelectSecondary;
            DialogResult = false;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (State.SelectedText == null)
            {
                TaskDialog.Show("提示", "请先选择一个文本。 ");
                return;
            }
            if (State.LineStart == null || State.LineEnd == null || State.LineStart.DistanceTo(State.LineEnd) < 1e-9)
            {
                TaskDialog.Show("提示", "请先绘制一条有效基准线。 ");
                return;
            }

            SaveOptions();
            Action = TextToolDialogAction.Execute;
            DialogResult = true;
        }

        private void SaveOptions()
        {
            State.KeepUpright = ChkKeepUpright.IsChecked == true;
            State.AlignBaseTag = (CmbAlignBase.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Retain";
        }

        private void UpdateStatus()
        {
            if (State.SelectedText != null)
            {
                TxtTextStatus.Text = $"已选择文本 ID：{State.SelectedText.Id}";
                TxtTextStatus.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor");
            }
            else
            {
                TxtTextStatus.Text = "未选择文本";
                TxtTextStatus.SetResourceReference(TextBlock.ForegroundProperty, "DangerBg");
            }

            if (State.LineStart != null && State.LineEnd != null && State.LineStart.DistanceTo(State.LineEnd) > 1e-9)
            {
                TxtLineStatus.Text = "已绘制基准线";
                TxtLineStatus.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor");
            }
            else
            {
                TxtLineStatus.Text = "未绘制基准线";
                TxtLineStatus.SetResourceReference(TextBlock.ForegroundProperty, "DangerBg");
            }
        }

        private static void SelectComboValue(System.Windows.Controls.ComboBox comboBox, string value, string fallback)
        {
            ComboBoxItem? item = comboBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => string.Equals(i.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                ?? comboBox.Items.OfType<ComboBoxItem>()
                    .FirstOrDefault(i => string.Equals(i.Tag?.ToString(), fallback, StringComparison.OrdinalIgnoreCase));
            comboBox.SelectedItem = item;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Action = TextToolDialogAction.Cancel;
            DialogResult = false;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
