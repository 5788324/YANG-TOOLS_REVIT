using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public partial class MicroCommandPickerWindow : Window
    {
        public Type? SelectedCommandType { get; private set; }

        public MicroCommandPickerWindow(IEnumerable<Type> commandTypes)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            CommandsListBox.ItemsSource = commandTypes
                .Select(type => new MicroCommandOption { CommandType = type })
                .ToList();
            CommandsListBox.SelectedIndex = 0;
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            CompleteSelection();
        }

        private void CommandsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            CompleteSelection();
        }

        private void CompleteSelection()
        {
            if (CommandsListBox.SelectedItem is not MicroCommandOption option)
                return;

            SelectedCommandType = option.CommandType;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
