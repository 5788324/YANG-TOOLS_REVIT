using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.UI;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public enum MergeSortMode
    {
        SelectionOrder,
        XDesc,
        XAsc,
        YDesc,
        YAsc
    }

    public partial class MergeTextWindow : Window
    {
        public MergeTextState State { get; }
        public TextToolDialogAction Action { get; private set; } = TextToolDialogAction.Cancel;

        public MergeTextWindow(MergeTextState state)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            State = state ?? throw new ArgumentNullException(nameof(state));
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSelectionStatus();
            SelectSortMode(State.SortTag);
            ChkUseNewline.IsChecked = State.UseNewlineSeparator;
            TxtSeparator.Text = State.SeparatorText;
            UpdateSeparatorState();
        }

        private void BtnSelectTexts_Click(object sender, RoutedEventArgs e)
        {
            SaveOptions();
            Action = TextToolDialogAction.SelectPrimary;
            DialogResult = false;
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (State.SelectedTexts.Count < 2)
            {
                TaskDialog.Show("提示", "请至少选择两个文本进行合并。 ");
                return;
            }

            SaveOptions();
            Action = TextToolDialogAction.Execute;
            DialogResult = true;
        }

        private void SaveOptions()
        {
            RadioButton? selected = new[] { RbSelectionOrder, RbXDesc, RbXAsc, RbYDesc, RbYAsc }
                .FirstOrDefault(button => button.IsChecked == true);
            State.SortTag = selected?.Tag?.ToString() ?? "SelectionOrder";
            State.UseNewlineSeparator = ChkUseNewline.IsChecked == true;
            State.SeparatorText = TxtSeparator.Text ?? string.Empty;
        }

        private void SelectSortMode(string tag)
        {
            RadioButton selected = new[] { RbSelectionOrder, RbXDesc, RbXAsc, RbYDesc, RbYAsc }
                .FirstOrDefault(button => string.Equals(button.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                ?? RbSelectionOrder;
            selected.IsChecked = true;
        }

        private void UpdateSelectionStatus()
        {
            if (State.SelectedTexts.Count > 0)
            {
                TxtSelectionStatus.Text = $"已选择 {State.SelectedTexts.Count} 个文本";
                TxtSelectionStatus.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor");
            }
            else
            {
                TxtSelectionStatus.Text = "未选择文本";
                TxtSelectionStatus.SetResourceReference(TextBlock.ForegroundProperty, "DangerBg");
            }
        }

        private void ChkNewline_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSeparatorState();
        }

        private void UpdateSeparatorState()
        {
            if (SeparatorPanel != null)
                SeparatorPanel.IsEnabled = ChkUseNewline.IsChecked != true;
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
