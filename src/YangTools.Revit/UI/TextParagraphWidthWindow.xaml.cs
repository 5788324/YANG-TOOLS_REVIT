using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace YangTools.Revit.UI
{
    public partial class TextParagraphWidthWindow : Window
    {
        private readonly double _minimum;
        private readonly double _maximum;
        public double WidthMm { get; private set; }

        public TextParagraphWidthWindow(IReadOnlyList<Row> rows, double minimum, double maximum, string? readOnlyReason)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _minimum = minimum;
            _maximum = maximum;
            NotesGrid.ItemsSource = rows;
            if (rows.Count == 1)
                WidthBox.Text = rows[0].WidthMm.ToString("G", CultureInfo.CurrentCulture);
            HintText.Text = readOnlyReason ?? $"设宽会启用自动换行。允许范围 {_minimum:0.###}–{_maximum:0.###} mm。";
            if (readOnlyReason != null)
                EditPanel.Visibility = ApplyButton.Visibility = Visibility.Collapsed;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(WidthBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double width) ||
                double.IsNaN(width) || width < _minimum || width > _maximum)
            {
                HintText.Text = $"请输入 {_minimum:0.###}–{_maximum:0.###} mm 范围内的宽度。";
                return;
            }
            WidthMm = width;
            DialogResult = true;
        }

        public sealed class Row
        {
            public Row(string text, string id, double widthMm, bool wrapping)
            {
                Text = text.Replace('\r', ' ').Replace('\n', ' ');
                Id = id;
                WidthMm = widthMm;
                Wrapping = wrapping ? "自动" : "无";
            }

            public string Text { get; }
            public string Id { get; }
            public double WidthMm { get; }
            public string Wrapping { get; }
        }
    }
}
