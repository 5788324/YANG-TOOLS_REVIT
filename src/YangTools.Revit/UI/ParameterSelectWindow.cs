using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;

namespace YangTools.Revit.UI
{
    public class ParameterSelectWindow : Window
    {
        private ListBox _listBox;
        public List<string> SelectedParameters { get; private set; } = new List<string>();

        public ParameterSelectWindow(List<string> parameterNames)
        {
            ThemeHelper.PrepareForInitialize(this);

            Title = "选择参数 (Select Parameters)";
            Width = 380;
            Height = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = MediaBrushes.Transparent;

            var border = new Border
            {
                Background = (MediaBrush)(TryFindResource("WindowBg") ?? new SolidColorBrush(MediaColor.FromRgb(250, 246, 240))),
                BorderBrush = (MediaBrush)(TryFindResource("WindowBorder") ?? new SolidColorBrush(MediaColor.FromRgb(213, 200, 184))),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });

            var titleBar = new Border
            {
                Background = (MediaBrush)(TryFindResource("TitleBarStart") ?? new SolidColorBrush(MediaColor.FromRgb(139, 109, 76))),
                CornerRadius = new CornerRadius(4, 4, 0, 0)
            };
            var titleText = new TextBlock
            {
                Text = "选择参数 (Select Parameters)",
                Foreground = (MediaBrush)(TryFindResource("TitleText") ?? MediaBrushes.White),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            titleBar.Child = titleText;
            grid.Children.Add(titleBar);

            _listBox = new ListBox
            {
                Margin = new Thickness(10),
                SelectionMode = SelectionMode.Multiple,
                Background = (MediaBrush)(TryFindResource("PanelBg") ?? new SolidColorBrush(MediaColor.FromRgb(245, 240, 232))),
                BorderBrush = (MediaBrush)(TryFindResource("BtnBorder") ?? new SolidColorBrush(MediaColor.FromRgb(200, 184, 160)))
            };

            foreach (var name in parameterNames)
            {
                var cb = new CheckBox
                {
                    Content = name,
                    Margin = new Thickness(4, 2, 4, 2),
                    Foreground = (MediaBrush)(TryFindResource("PrimaryText") ?? new SolidColorBrush(MediaColor.FromRgb(58, 48, 40)))
                };
                _listBox.Items.Add(cb);
            }

            Grid.SetRow(_listBox, 1);
            grid.Children.Add(_listBox);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 15, 0)
            };
            Grid.SetRow(btnPanel, 2);

            var okBtn = new Button
            {
                Content = "确定 (OK)",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                Background = (MediaBrush)(TryFindResource("BtnBg") ?? new SolidColorBrush(MediaColor.FromRgb(232, 223, 208))),
                BorderBrush = (MediaBrush)(TryFindResource("BtnBorder") ?? new SolidColorBrush(MediaColor.FromRgb(200, 184, 160)))
            };
            okBtn.Click += (s, ev) =>
            {
                foreach (var item in _listBox.Items)
                {
                    if (item is CheckBox cb && cb.IsChecked == true)
                    {
                        SelectedParameters.Add(cb.Content.ToString());
                    }
                }
                DialogResult = SelectedParameters.Count > 0;
                Close();
            };
            okBtn.IsDefault = true;
            btnPanel.Children.Add(okBtn);

            var cancelBtn = new Button
            {
                Content = "取消 (Cancel)",
                Width = 80,
                Background = (MediaBrush)(TryFindResource("BtnBg") ?? new SolidColorBrush(MediaColor.FromRgb(232, 223, 208))),
                BorderBrush = (MediaBrush)(TryFindResource("BtnBorder") ?? new SolidColorBrush(MediaColor.FromRgb(200, 184, 160)))
            };
            cancelBtn.Click += (s, ev) => { DialogResult = false; Close(); };
            cancelBtn.IsCancel = true;
            btnPanel.Children.Add(cancelBtn);
            grid.Children.Add(btnPanel);
            border.Child = grid;
            Content = border;

            titleBar.MouseLeftButtonDown += (s, ev) => { if (ev.ChangedButton == MouseButton.Left) this.DragMove(); };
        }
    }
}
