using System.Windows;
using YangTools.Revit.UI.Controls;

namespace YangTools.Revit.UI
{
    public enum PipeElevationTargetReference
    {
        Invert,
        Bottom,
        Top
    }

    public partial class PipeElevationReferenceWindow : YangWindow
    {
        public PipeElevationTargetReference TargetReference { get; private set; } = PipeElevationTargetReference.Invert;
        public bool IncludeInsulation { get; private set; }
        public bool DeleteBlockingFittings { get; private set; } = true;

        public PipeElevationReferenceWindow(int adjustableCount, int diameterCount, int skippedCount)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);

            string summary = $"已选 {adjustableCount} 根可调整管道（含 {diameterCount} 种管径）";
            if (skippedCount > 0)
                summary += $"，将自动跳过 {skippedCount} 根立管/固定/组内管道";
            SummaryText.Text = summary;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            TargetReference = RbBottom.IsChecked == true
                ? PipeElevationTargetReference.Bottom
                : RbTop.IsChecked == true
                    ? PipeElevationTargetReference.Top
                    : PipeElevationTargetReference.Invert;
            IncludeInsulation = ChkIncludeInsulation.IsChecked == true;
            DeleteBlockingFittings = ChkDeleteBlockingFittings.IsChecked == true;
            DialogResult = true;
        }
    }
}
