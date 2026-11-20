using System.Windows;

namespace YangTools.Revit.UI
{
    public partial class ViewScaleLayoutWindow : Window
    {
        public int OriginalScale { get; private set; }
        public int TargetScale { get; private set; }
        public bool PickAnchor => PickAnchorBox.IsChecked == true;

        public ViewScaleLayoutWindow(int currentScale)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            CurrentScaleText.Text = $"当前视图比例 1:{currentScale}";
            OriginalScaleBox.Text = TargetScaleBox.Text = currentScale.ToString();
            Loaded += (_, _) =>
            {
                TargetScaleBox.Focus();
                TargetScaleBox.SelectAll();
            };
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(OriginalScaleBox.Text, out int original) || original <= 0 ||
                !int.TryParse(TargetScaleBox.Text, out int target) || target <= 0)
            {
                ValidationText.Text = "请填写正整数，例如原比例 1000，目标比例 200。";
                return;
            }
            OriginalScale = original;
            TargetScale = target;
            DialogResult = true;
        }
    }
}
