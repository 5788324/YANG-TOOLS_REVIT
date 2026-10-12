using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public partial class PasteCadWindow : Window
    {
        private readonly UserSettings _settings;
        private readonly bool _supportsThisViewOnly;
        private IReadOnlyList<CadClipboardFileInfo> _candidates = Array.Empty<CadClipboardFileInfo>();

        public string? SelectedDwgPath { get; private set; }
        public ImportUnit SelectedUnit { get; private set; }
        public ImportColorMode SelectedColorMode { get; private set; }
        public ImportPlacement SelectedPlacement { get; private set; }
        public bool SelectedThisViewOnly { get; private set; }

        public PasteCadWindow(string activeViewName, bool supportsThisViewOnly)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);

            _settings = UserSettings.Load();
            _supportsThisViewOnly = supportsThisViewOnly;
            TxtActiveView.Text = $"目标视图：{activeViewName}";

            CboUnits.SelectedIndex = _settings.PasteCadUnitIndex;
            CboColors.SelectedIndex = _settings.PasteCadColorIndex;
            CboPlacement.SelectedIndex = _settings.PasteCadPlacementIndex;
            UpdatePlacementHint();
            ChkThisViewOnly.IsChecked = supportsThisViewOnly && _settings.PasteCadThisViewOnly;
            ChkThisViewOnly.IsEnabled = supportsThisViewOnly;
            TxtViewOnlyHint.Text = supportsThisViewOnly
                ? "勾选后创建视图专用导入；取消勾选后作为模型级 CAD 导入。"
                : "当前为三维视图，Revit 不支持视图专用 CAD 导入，已自动使用模型级导入。";

            try
            {
                CadClipboardFileHelper.CleanupYangToolsCache(false, TimeSpan.FromDays(1));
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("自动清理从 CAD 粘贴缓存", ex);
            }
            DetectCadClipboard();
        }

        private void DetectCadClipboard()
        {
            try
            {
                _candidates = CadClipboardFileHelper.FindCandidates();
                CboCadFiles.ItemsSource = _candidates;
                if (_candidates.Count == 0)
                {
                    SelectedDwgPath = null;
                    TxtSelectedFile.Text = "（未检测到 AutoCAD 临时剪贴板文件）";
                    BtnConfirm.IsEnabled = false;
                    SetStatus(
                        false,
                        "未检测到 AutoCAD 复制数据",
                        "请先在 AutoCAD 中选择图元并按 Ctrl+C，然后回到 Revit 点击“重新检测”。也可以手动选择 DWG 文件。",
                        "⚠");
                    return;
                }

                CboCadFiles.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                SelectedDwgPath = null;
                BtnConfirm.IsEnabled = false;
                string logPath = DiagnosticLog.WriteError("检测 AutoCAD 剪贴板", ex);
                SetStatus(
                    false,
                    "检测 AutoCAD 剪贴板失败",
                    ex.Message + (string.IsNullOrWhiteSpace(logPath) ? string.Empty : $"\n诊断日志：{logPath}"),
                    "✕");
            }
        }

        private void CboCadFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboCadFiles.SelectedItem is CadClipboardFileInfo selected)
                SetFileSelected(selected);
        }

        private void SetFileSelected(CadClipboardFileInfo fileInfo)
        {
            SelectedDwgPath = fileInfo.FullPath;
            TxtSelectedFile.Text = fileInfo.FullPath;
            BtnConfirm.IsEnabled = File.Exists(fileInfo.FullPath) && fileInfo.SizeBytes > 0;

            if (fileInfo.IsStale)
            {
                SetStatus(
                    false,
                    "已选择较早的 DWG 临时文件",
                    $"该文件修改于 {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}。请确认它确实对应本次 AutoCAD 复制内容。",
                    "⚠");
            }
            else
            {
                SetStatus(
                    true,
                    "已检测到 AutoCAD 剪贴板文件",
                    $"{fileInfo.FileName}　{FormatSize(fileInfo.SizeBytes)}　{fileInfo.LastWriteTime:MM-dd HH:mm:ss}",
                    "✓");
            }
        }

        private void SetStatus(bool isSuccess, string title, string description, string icon)
        {
            TxtStatusIcon.Text = icon;
            TxtStatusTitle.Text = title;
            TxtStatusDesc.Text = description;

            string borderResource = isSuccess ? "SuccessColor" : "DangerBg";
            BdrStatus.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, borderResource);
            BdrStatus.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "PanelBg");
            TxtStatusIcon.SetResourceReference(TextBlock.ForegroundProperty, borderResource);
            TxtStatusTitle.SetResourceReference(TextBlock.ForegroundProperty, borderResource);
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择 DWG 文件",
                Filter = "DWG 文件 (*.dwg)|*.dwg|所有文件 (*.*)|*.*",
                InitialDirectory = Path.GetTempPath(),
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                CadClipboardFileInfo selected = CadClipboardFileHelper.CreateInfo(dialog.FileName);
                var updated = _candidates
                    .Where(item => !string.Equals(item.FullPath, selected.FullPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                updated.Insert(0, selected);
                _candidates = updated;
                CboCadFiles.ItemsSource = _candidates;
                CboCadFiles.SelectedItem = selected;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "从 CAD 粘贴", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


        private void BtnCleanupCache_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult choice = MessageBox.Show(
                "选择“是”：清理 YangTools 工作副本，并清理 24 小时以前且未被占用的 AutoCAD A$C 临时 DWG。\n\n" +
                "选择“否”：只清理 YangTools 自己创建的工作副本。\n\n" +
                "正在使用或 24 小时以内的 AutoCAD 临时文件不会删除。",
                "清理 CAD 缓存",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel)
                return;

            try
            {
                CadCacheCleanupResult result = CadClipboardFileHelper.CleanupYangToolsCache(true, TimeSpan.Zero);
                if (choice == MessageBoxResult.Yes)
                    result.Add(CadClipboardFileHelper.CleanupOldClipboardFiles(TimeSpan.FromHours(24)));

                string size = FormatSize(result.RemovedBytes);
                MessageBox.Show(
                    $"清理完成。\n\n删除目录：{result.RemovedDirectories}\n删除文件：{result.RemovedFiles}\n释放空间：{size}\n跳过占用/无权限项：{result.SkippedFiles}",
                    "清理 CAD 缓存",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                DetectCadClipboard();
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("清理 CAD 缓存", ex);
                MessageBox.Show(
                    $"清理失败：\n{ex.Message}" +
                    (string.IsNullOrWhiteSpace(logPath) ? string.Empty : $"\n\n诊断日志：{logPath}"),
                    "清理 CAD 缓存",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedDwgPath) || !File.Exists(SelectedDwgPath))
            {
                MessageBox.Show("所选 DWG 已被移动、清理或不可访问，请重新检测或手动选择。",
                    "从 CAD 粘贴", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedUnit = CboUnits.SelectedIndex switch
            {
                0 => ImportUnit.Default,
                1 => ImportUnit.Millimeter,
                2 => ImportUnit.Centimeter,
                3 => ImportUnit.Decimeter,
                4 => ImportUnit.Meter,
                5 => ImportUnit.Inch,
                6 => ImportUnit.Foot,
                _ => ImportUnit.Millimeter
            };

            SelectedColorMode = CboColors.SelectedIndex switch
            {
                0 => ImportColorMode.Preserved,
                1 => ImportColorMode.BlackAndWhite,
                2 => ImportColorMode.Inverted,
                _ => ImportColorMode.Preserved
            };

            SelectedPlacement = CboPlacement.SelectedIndex switch
            {
                0 => ImportPlacement.Origin,
                1 => ImportPlacement.Centered,
                2 => ImportPlacement.Shared,
                _ => ImportPlacement.Origin
            };

            SelectedThisViewOnly = _supportsThisViewOnly && ChkThisViewOnly.IsChecked == true;

            _settings.PasteCadUnitIndex = Math.Max(0, CboUnits.SelectedIndex);
            _settings.PasteCadColorIndex = Math.Max(0, CboColors.SelectedIndex);
            _settings.PasteCadPlacementIndex = Math.Max(0, CboPlacement.SelectedIndex);
            if (_supportsThisViewOnly)
                _settings.PasteCadThisViewOnly = ChkThisViewOnly.IsChecked == true;
            _settings.Save();

            DialogResult = true;
            Close();
        }


        private void CboPlacement_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePlacementHint();
        }

        private void UpdatePlacementHint()
        {
            if (TxtPlacementHint == null || CboPlacement == null)
                return;

            TxtPlacementHint.Text = CboPlacement.SelectedIndex switch
            {
                1 => "中心到中心依赖当前视图范围，适合只需快速目测对齐的临时 CAD。",
                2 => "按 Revit 项目的共享坐标放置。项目必须已建立有效共享坐标；YangTools 不会自动获取/发布坐标、不会验证 DWG 是否与项目采用同一坐标基准，也不会失败后静默改用原点到原点。",
                _ => "按 DWG 原点与 Revit 内部原点对齐。"
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnRedetect_Click(object sender, RoutedEventArgs e)
        {
            DetectCadClipboard();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private static string FormatSize(long value)
        {
            if (value >= 1024L * 1024L)
                return $"{value / 1024d / 1024d:F1} MB";
            if (value >= 1024L)
                return $"{value / 1024d:F1} KB";
            return $"{value} B";
        }
    }
}
