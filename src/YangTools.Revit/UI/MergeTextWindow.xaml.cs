using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public enum MergeSortMode
    {
        SelectionOrder,
        ReadingOrder,
        XDesc,
        XAsc,
        YDesc,
        YAsc
    }

    public partial class MergeTextWindow : Window
    {
        private readonly Func<(List<TextNote> Notes, string Text)> _prepareMerge;
        internal (List<TextNote> Notes, string Text) PreparedMerge { get; private set; }

        public MergeTextState State { get; }
        public TextToolDialogAction Action { get; private set; } = TextToolDialogAction.Cancel;

        internal MergeTextWindow(MergeTextState state, Func<(List<TextNote> Notes, string Text)> prepareMerge)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            State = state ?? throw new ArgumentNullException(nameof(state));
            _prepareMerge = prepareMerge;
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSelectionStatus();
            SelectSortMode(State.SortTag);
            ChkUseNewline.IsChecked = State.UseNewlineSeparator;
            ChkCleanWhitespace.IsChecked = State.CleanWhitespace;
            TxtSeparator.Text = State.SeparatorText;
            UpdateSeparatorState();
            foreach (RadioButton option in new[] { RbReadingOrder, RbSelectionOrder, RbXDesc, RbXAsc, RbYDesc, RbYAsc })
                option.Checked += (_, _) => UpdatePreview();
            ChkUseNewline.Checked += (_, _) => UpdatePreview();
            ChkUseNewline.Unchecked += (_, _) => UpdatePreview();
            ChkCleanWhitespace.Checked += (_, _) => UpdatePreview();
            ChkCleanWhitespace.Unchecked += (_, _) => UpdatePreview();
            TxtSeparator.TextChanged += (_, _) => UpdatePreview();
            UpdatePreview();
        }

        private void BtnSelectTexts_Click(object sender, RoutedEventArgs e)
        {
            SaveOptions();
            Action = TextToolDialogAction.SelectPrimary;
            DialogResult = false;
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (!UpdatePreview()) return;
            Action = TextToolDialogAction.Execute;
            DialogResult = true;
        }

        private bool UpdatePreview()
        {
            SaveOptions();
            try
            {
                PreparedMerge = _prepareMerge();
                TxtPreview.Text = PreparedMerge.Text;
                TxtPreviewStatus.Text = $"{PreparedMerge.Notes.Count} 个文本合并为 1 个，其余文本将删除。可用 Ctrl+Z 撤销。";
                BtnExecute.IsEnabled = true;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                TxtPreview.Text = string.Empty;
                TxtPreviewStatus.Text = ex.Message;
                BtnExecute.IsEnabled = false;
                return false;
            }
        }

        private void SaveOptions()
        {
            RadioButton? selected = new[] { RbReadingOrder, RbSelectionOrder, RbXDesc, RbXAsc, RbYDesc, RbYAsc }
                .FirstOrDefault(button => button.IsChecked == true);
            State.SortTag = selected?.Tag?.ToString() ?? "ReadingOrder";
            State.CleanWhitespace = ChkCleanWhitespace.IsChecked == true;
            State.UseNewlineSeparator = ChkUseNewline.IsChecked == true;
            State.SeparatorText = TxtSeparator.Text ?? string.Empty;
        }

        private void SelectSortMode(string tag)
        {
            RadioButton selected = new[] { RbReadingOrder, RbSelectionOrder, RbXDesc, RbXAsc, RbYDesc, RbYAsc }
                .FirstOrDefault(button => string.Equals(button.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                ?? RbReadingOrder;
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
