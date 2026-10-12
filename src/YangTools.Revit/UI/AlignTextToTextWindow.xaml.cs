using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.UI;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public partial class AlignTextToTextWindow : Window
    {
        public AlignTextToTextState State { get; }
        public TextToolDialogAction Action { get; private set; } = TextToolDialogAction.Cancel;

        public AlignTextToTextWindow(AlignTextToTextState state)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            State = state ?? throw new ArgumentNullException(nameof(state));
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ChkAlignX.IsChecked = State.AlignX;
            ChkAlignY.IsChecked = State.AlignY;
            ChkAlignRotation.IsChecked = State.AlignRotation;
            ChkMatchParagraphWidth.IsChecked = State.MatchParagraphWidth;
            UpdateStatus();
        }

        private void BtnSelectSource_Click(object sender, RoutedEventArgs e)
        {
            SaveOptions();
            Action = TextToolDialogAction.SelectPrimary;
            DialogResult = false;
        }

        private void BtnSelectTargets_Click(object sender, RoutedEventArgs e)
        {
            SaveOptions();
            Action = TextToolDialogAction.SelectSecondary;
            DialogResult = false;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (State.SourceText == null)
            {
                TaskDialog.Show("提示", "请先选择源文本。 ");
                return;
            }
            if (State.TargetTexts.Count == 0)
            {
                TaskDialog.Show("提示", "请至少选择一个目标文本。 ");
                return;
            }

            SaveOptions();
            if (!State.AlignX && !State.AlignY && !State.AlignRotation && !State.MatchParagraphWidth)
            {
                TaskDialog.Show("提示", "请至少勾选一种对齐内容。 ");
                return;
            }

            Action = TextToolDialogAction.Execute;
            DialogResult = true;
        }

        private void SaveOptions()
        {
            State.AlignX = ChkAlignX.IsChecked == true;
            State.AlignY = ChkAlignY.IsChecked == true;
            State.AlignRotation = ChkAlignRotation.IsChecked == true;
            State.MatchParagraphWidth = ChkMatchParagraphWidth.IsChecked == true;
        }

        private void UpdateStatus()
        {
            if (State.SourceText != null)
            {
                TxtSourceStatus.Text = $"已选择源文本 ID：{State.SourceText.Id}";
                TxtSourceStatus.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor");
            }
            else
            {
                TxtSourceStatus.Text = "未选择源文本";
                TxtSourceStatus.SetResourceReference(TextBlock.ForegroundProperty, "DangerBg");
            }

            if (State.TargetTexts.Count > 0)
            {
                TxtTargetStatus.Text = $"已选择 {State.TargetTexts.Count} 个目标文本";
                TxtTargetStatus.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor");
            }
            else
            {
                TxtTargetStatus.Text = "未选择目标文本";
                TxtTargetStatus.SetResourceReference(TextBlock.ForegroundProperty, "DangerBg");
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            FeatureHelpWindow.Show(this, "文本基准对齐 - 功能说明",
                "功能逻辑\r\n" +
                "先选择一个源文本，再选择一个或多个目标文本。源文本保持不动，目标文本根据勾选项移动、旋转或调整段落宽度。\r\n\r\n" +
                "对齐 X\r\n" +
                "令目标 TextNote.Coord.X 与源文本相同，因此目标插入点会落在同一条竖直线上。\r\n\r\n" +
                "对齐 Y\r\n" +
                "令目标 TextNote.Coord.Y 与源文本相同，因此目标插入点会落在同一条水平线上。\r\n\r\n" +
                "对齐旋转\r\n" +
                "目标文本围绕各自插入点旋转到与源文本相同的角度。\r\n\r\n" +
                "段落宽度相同\r\n" +
                "将每个目标文本的 TextNote.Width 设置为源文本的 Width。只复制文字段落框宽度，不复制文字内容、文字类型、字号或格式；目标文本可能因此重新换行。\r\n\r\n" +
                "注意\r\n" +
                "X/Y 对齐的是文字插入点，不是文字可见边框。不同文字对齐方式或引线仍可能造成视觉边缘不一致。源宽度超出目标文字类型允许范围，或目标被锁定、成组、受约束时，程序会回滚该目标并报告跳过。");
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
