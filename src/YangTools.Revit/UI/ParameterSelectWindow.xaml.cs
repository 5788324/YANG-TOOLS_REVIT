using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YangTools.Revit.UI
{
    public partial class ParameterSelectWindow : Window
    {
        public List<string> SelectedParameters { get; private set; } = new List<string>();

        public ParameterSelectWindow(List<string> parameterNames, bool allowMultiple = true)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);

            OptionsList.SelectionMode = allowMultiple ? SelectionMode.Multiple : SelectionMode.Single;
            OptionsList.ItemsSource = parameterNames;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void OptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConfirmButton.IsEnabled = OptionsList.SelectedItems.Count > 0;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            SelectedParameters = OptionsList.SelectedItems.Cast<string>().ToList();
            DialogResult = true;
        }
    }
}
