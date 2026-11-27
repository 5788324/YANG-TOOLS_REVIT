using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using YangTools.Revit.UI.Controls;

namespace YangTools.Revit.UI
{
    public enum PipeElevationReference
    {
        Center,
        Invert,
        Bottom,
        InnerTop,
        OuterTop
    }

    public partial class PipeElevationReferenceWindow : YangWindow
    {
        private sealed class RefItem
        {
            public PipeElevationReference Value { get; set; }
            public string Label { get; set; } = "";
            public override string ToString() => Label;
        }

        private readonly bool _initialized;

        public PipeElevationReference SourceReference { get; private set; } = PipeElevationReference.Center;
        public PipeElevationReference TargetReference { get; private set; } = PipeElevationReference.Invert;
        public bool IncludeInsulation { get; private set; }
        public bool DeleteBlockingFittings { get; private set; } = true;

        public PipeElevationReferenceWindow(int adjustableCount, int diameterCount, int skippedCount)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);

            SourceRefCombo.ItemsSource = new List<RefItem>
            {
                new RefItem { Value = PipeElevationReference.Center, Label = "中心高度（默认）" },
                new RefItem { Value = PipeElevationReference.Invert, Label = "管内底" },
                new RefItem { Value = PipeElevationReference.Bottom, Label = "管外底" },
                new RefItem { Value = PipeElevationReference.InnerTop, Label = "管内顶" },
                new RefItem { Value = PipeElevationReference.OuterTop, Label = "管外顶" }
            };
            SourceRefCombo.SelectedIndex = 0;

            string summary = $"已选 {adjustableCount} 根可调整管道（含 {diameterCount} 种管径）";
            if (skippedCount > 0)
                summary += $"，将自动跳过 {skippedCount} 根立管/固定/组内管道";
            SummaryText.Text = summary;

            _initialized = true;
            UpdateHint();
        }

        private PipeElevationReference GetSelectedSource()
        {
            return (SourceRefCombo.SelectedItem as RefItem)?.Value ?? PipeElevationReference.Center;
        }

        private PipeElevationReference GetSelectedTarget()
        {
            if (RbCenter.IsChecked == true) return PipeElevationReference.Center;
            if (RbBottom.IsChecked == true) return PipeElevationReference.Bottom;
            if (RbInnerTop.IsChecked == true) return PipeElevationReference.InnerTop;
            if (RbOuterTop.IsChecked == true) return PipeElevationReference.OuterTop;
            return PipeElevationReference.Invert;
        }

        private static string GetLabel(PipeElevationReference r) => r switch
        {
            PipeElevationReference.Center => "中心高度",
            PipeElevationReference.Invert => "管内底",
            PipeElevationReference.Bottom => "管外底",
            PipeElevationReference.InnerTop => "管内顶",
            PipeElevationReference.OuterTop => "管外顶",
            _ => "中心高度"
        };

        private void Reference_Changed(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            UpdateHint();
        }

        private void UpdateHint()
        {
            PipeElevationReference src = GetSelectedSource();
            PipeElevationReference dst = GetSelectedTarget();
            if (src == dst)
            {
                HintText.Text = $"当前基准与目标基准均为「{GetLabel(src)}」，管道高度不会发生变化。";
                ApplyButton.IsEnabled = false;
            }
            else
            {
                HintText.Text = $"示例：保持原「{GetLabel(src)}」数值 = 1200 mm 不变，整体平移管道使新「{GetLabel(dst)}」= 1200 mm。支持单次 Ctrl+Z 撤销。";
                ApplyButton.IsEnabled = true;
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            SourceReference = GetSelectedSource();
            TargetReference = GetSelectedTarget();
            IncludeInsulation = ChkIncludeInsulation.IsChecked == true;
            DeleteBlockingFittings = ChkDeleteBlockingFittings.IsChecked == true;
            DialogResult = true;
        }
    }
}
