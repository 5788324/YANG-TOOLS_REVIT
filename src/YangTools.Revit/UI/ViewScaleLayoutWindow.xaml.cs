using System.Windows;

namespace YangTools.Revit.UI
{
    public partial class ViewScaleLayoutWindow : Window
    {
        private readonly int _currentScale;

        public int OriginalScale { get; private set; }
        public int TargetScale { get; private set; }
        public bool PickAnchor => PickAnchorBox.IsChecked == true;

        public ViewScaleLayoutWindow(int currentScale, string selectionSummary, bool affectsLegend, bool hasDetailComponents)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _currentScale = currentScale;
            SelectionSummary.Text = selectionSummary;
            LegendHint.Visibility = affectsLegend ? Visibility.Visible : Visibility.Collapsed;
            DetailHint.Visibility = hasDetailComponents ? Visibility.Visible : Visibility.Collapsed;
            CurrentScaleText.Text = $"当前视图比例 1:{currentScale}";
            OriginalScaleBox.Text = TargetScaleBox.Text = currentScale.ToString();
            Loaded += (_, _) =>
            {
                TargetScaleBox.Focus();
                TargetScaleBox.SelectAll();
            };
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(OriginalScaleBox.Text, out int original) || original <= 0 ||
                !int.TryParse(TargetScaleBox.Text, out int target) || target <= 0)
            {
                ValidationText.Text = "请填写正整数，例如原比例 1000，目标比例 200。";
                return;
            }
            if (original == target)
            {
                ValidationText.Text = "两个比例相同，无需调整。";
                return;
            }
            if (_currentScale != original && _currentScale != target)
            {
                ValidationText.Text = $"原排版比例或目标比例应填当前视图比例 1:{_currentScale}。";
                return;
            }
            OriginalScale = original;
            TargetScale = target;
            DialogResult = true;
        }
    }
}
