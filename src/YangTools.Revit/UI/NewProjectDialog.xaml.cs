using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace YangTools.Revit.UI
{
    public partial class NewProjectDialog : Window
    {
        private readonly string _temporaryProjectsRoot;

        public string ProjectName => ProjectNameBox.Text.Trim();
        public string FolderPath => FolderPathBox.Text.Trim();

        public NewProjectDialog()
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _temporaryProjectsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YangTools",
                "TemporaryProjects");
            FolderPathBox.Text = _temporaryProjectsRoot;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择临时项目插件文件夹";
                dialog.ShowNewFolderButton = true;
                if (Directory.Exists(FolderPath))
                    dialog.SelectedPath = FolderPath;

                if (dialog.ShowDialog() == Forms.DialogResult.OK)
                    FolderPathBox.Text = dialog.SelectedPath;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                MessageBox.Show("项目名称不能为空。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ProjectName.IndexOfAny(new[] { '\r', '\n', '|' }) >= 0)
            {
                MessageBox.Show("项目名称不能包含换行或竖线字符。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(FolderPath))
            {
                MessageBox.Show("插件目录不能为空。", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string fullPath = Path.GetFullPath(FolderPath);
                if (PathsEqual(fullPath, _temporaryProjectsRoot))
                    fullPath = Path.Combine(_temporaryProjectsRoot, SanitizeFolderName(ProjectName));
                Directory.CreateDirectory(fullPath);
                FolderPathBox.Text = fullPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"文件夹路径无效或无法创建：\n{ex.Message}", "临时插件箱",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DialogResult = true;
            Close();
        }

        private static string SanitizeFolderName(string name)
        {
            string safe = string.Join("_", (name ?? string.Empty).Split(Path.GetInvalidFileNameChars()));
            return string.IsNullOrWhiteSpace(safe) ? "TemporaryProject" : safe.Trim();
        }

        private static bool PathsEqual(string first, string second)
        {
            string a = Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string b = Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
