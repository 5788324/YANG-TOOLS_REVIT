using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.UI;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public enum SortMode { SelectionOrder, XAsc, XDesc, YAsc, YDesc }
    public enum DistributeDirection { None, X, Y }
    public enum AlignMode { None, AlignX, AlignY }

    public partial class DistributeTextWindow : Window
    {
        public DistributeTextState State { get; }
        public TextToolDialogAction Action { get; private set; } = TextToolDialogAction.Cancel;

        public DistributeTextWindow(DistributeTextState state)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            State = state ?? throw new ArgumentNullException(nameof(state));
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSelectionStatus();
            SelectComboValue(CmbSortBy, State.SortTag, "SelectionOrder");
            SelectComboValue(CmbOperationMode, GetModeTag(State.DirectionTag, State.AlignTag), "VerticalAlignedX");
            TxtSpacing.Text = State.SpacingMm.ToString("0.###", CultureInfo.CurrentCulture);
            UpdateModeExplanation();
        }

        private void BtnSelectTexts_Click(object sender, RoutedEventArgs e)
        {
            if (!TrySaveOptions(showValidation: false)) return;
            Action = TextToolDialogAction.SelectPrimary;
            DialogResult = false;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (State.SelectedTexts.Count < 2)
            {
                TaskDialog.Show("提示", "请至少选择两个文本。 ");
                return;
            }

            if (!TrySaveOptions(showValidation: true)) return;
            Action = TextToolDialogAction.Execute;
            DialogResult = true;
        }

        private bool TrySaveOptions(bool showValidation)
        {
            string mode = GetSelectedTag(CmbOperationMode, "VerticalAlignedX");
            bool needsSpacing = mode is "VerticalAlignedX" or "HorizontalAlignedY" or "VerticalKeepX" or "HorizontalKeepY";
            double spacing = State.SpacingMm;
            if (needsSpacing && !TryParsePositiveDouble(TxtSpacing.Text, out spacing))
            {
                if (showValidation) TaskDialog.Show("输入错误", "插入点间距必须是大于 0 的有效数字。 ");
                return false;
            }

            State.SortTag = GetSelectedTag(CmbSortBy, "SelectionOrder");
            ApplyMode(mode, State);
            State.SpacingMm = spacing;
            return true;
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

        private void CmbOperationMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateModeExplanation();
        }

        private void UpdateModeExplanation()
        {
            string mode = GetSelectedTag(CmbOperationMode, "VerticalAlignedX");
            bool needsSpacing = mode is "VerticalAlignedX" or "HorizontalAlignedY" or "VerticalKeepX" or "HorizontalKeepY";
            TxtSpacing.IsEnabled = needsSpacing;
            TxtModeExplanation.Text = mode switch
            {
                "VerticalAlignedX" => "所有文本插入点 X 相同，并按固定 Y 间距竖向排列。",
                "HorizontalAlignedY" => "所有文本插入点 Y 相同，并按固定 X 间距横向排列。",
                "VerticalKeepX" => "只重排每个文本的 Y 坐标；原 X 坐标保持不变。",
                "HorizontalKeepY" => "只重排每个文本的 X 坐标；原 Y 坐标保持不变。",
                "AlignXOnly" => "只令目标文本插入点 X 与第一个文本一致，不做等距分布。",
                "AlignYOnly" => "只令目标文本插入点 Y 与第一个文本一致，不做等距分布。",
                _ => "按选定模式处理文本插入点。"
            };
        }

        private static void ApplyMode(string mode, DistributeTextState state)
        {
            switch (mode)
            {
                case "HorizontalAlignedY":
                    state.DirectionTag = "X";
                    state.AlignTag = "AlignY";
                    break;
                case "VerticalKeepX":
                    state.DirectionTag = "Y";
                    state.AlignTag = "None";
                    break;
                case "HorizontalKeepY":
                    state.DirectionTag = "X";
                    state.AlignTag = "None";
                    break;
                case "AlignXOnly":
                    state.DirectionTag = "None";
                    state.AlignTag = "AlignX";
                    break;
                case "AlignYOnly":
                    state.DirectionTag = "None";
                    state.AlignTag = "AlignY";
                    break;
                default:
                    state.DirectionTag = "Y";
                    state.AlignTag = "AlignX";
                    break;
            }
        }

        private static string GetModeTag(string direction, string align)
        {
            if (direction == "X" && align == "AlignY") return "HorizontalAlignedY";
            if (direction == "Y" && align == "None") return "VerticalKeepX";
            if (direction == "X" && align == "None") return "HorizontalKeepY";
            if (direction == "None" && align == "AlignX") return "AlignXOnly";
            if (direction == "None" && align == "AlignY") return "AlignYOnly";
            return "VerticalAlignedX";
        }

        private static bool TryParsePositiveDouble(string text, out double value)
        {
            return (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                    || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                   && value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string GetSelectedTag(System.Windows.Controls.ComboBox comboBox, string fallback)
        {
            return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;
        }

        private static void SelectComboValue(System.Windows.Controls.ComboBox comboBox, string value, string fallback)
        {
            ComboBoxItem? selected = comboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                ?? comboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), fallback, StringComparison.OrdinalIgnoreCase));
            comboBox.SelectedItem = selected;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            FeatureHelpWindow.Show(this, "文本等距分布 - 功能说明",
                "功能逻辑\r\n" +
                "本工具移动的是 TextNote.Coord 插入点。排序后的第一个文本保持原位，其余文本相对它进行分布或对齐。\r\n\r\n" +
                "常用模式\r\n" +
                "1. 垂直等距并对齐 X：适合把多行说明整理成竖直列表。\r\n" +
                "2. 水平等距并对齐 Y：适合把多个文字横向排成一行。\r\n" +
                "3. 仅沿某轴等距：只改变分布轴，保留另一轴原位置。\r\n" +
                "4. 仅对齐 X/Y：不使用间距，只把插入点对齐。\r\n\r\n" +
                "排序\r\n" +
                "坐标排序最稳定；“拾取返回顺序”在逐个点选时通常符合点选顺序，但框选时不保证。\r\n\r\n" +
                "注意\r\n" +
                "插入点间距不是文字可见边框之间的净距。不同文本类型、宽度或对齐方式会使视觉间距不同。操作可通过 Revit 撤销。 ");
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
