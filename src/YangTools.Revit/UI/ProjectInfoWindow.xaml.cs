using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace YangTools.Revit.UI
{
    public partial class ProjectInfoWindow : Window
    {
        private readonly UIApplication _uiapp;
        private readonly Document _doc;
        private string? _estimateSourcePath;
        private string? _documentPath;
        private string? _centralPath;
        private bool _estimating;

        public ProjectInfoWindow(UIApplication uiapp)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;
            LoadInfo();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_Closing(object sender, CancelEventArgs e) => e.Cancel = _estimating;

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void LoadInfo()
        {
            bool cloud = _doc.IsModelInCloud;
            string path = _doc.PathName;
            TxtFileName.Text = _doc.Title;
            TxtRevitVersion.Text = _uiapp.Application.VersionName;
            _documentPath = string.IsNullOrWhiteSpace(path) ? null : path;

            if (cloud)
            {
                try { _documentPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(_doc.GetCloudModelPath()); }
                catch (Exception ex) { DiagnosticLog.WriteError("读取云模型路径", ex); }
            }

            TxtDocPath.Text = _documentPath ?? (cloud ? "云模型路径无法读取" : "文件未保存");
            BtnCopyPath.IsEnabled = !string.IsNullOrWhiteSpace(_documentPath);
            if (_doc.IsWorkshared && !cloud)
            {
                PanelCentralPath.Visibility = System.Windows.Visibility.Visible;
                try
                {
                    var centralModelPath = _doc.GetWorksharingCentralModelPath();
                    if (centralModelPath != null)
                        _centralPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralModelPath);
                }
                catch (Exception ex) { DiagnosticLog.WriteError("读取中心模型路径", ex); }
                TxtCentralPath.Text = _centralPath ?? "无法获取";
                BtnCopyCentralPath.IsEnabled = !string.IsNullOrWhiteSpace(_centralPath);
            }

            bool hasDiskFile = !cloud && File.Exists(path);
            if (hasDiskFile)
            {
                try
                {
                    var file = new FileInfo(path);
                    TxtFileSize.Text = TxtCurrentSizeCard2.Text = FormatSize(file.Length);
                    TxtLastModified.Text = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
                catch (IOException ex)
                {
                    TxtFileSize.Text = TxtCurrentSizeCard2.Text = "读取失败";
                    DiagnosticLog.WriteError("读取 RVT 文件信息", ex);
                }
            }

#if REVIT2024_OR_GREATER
            try { TxtUnusedCount.Text = $"{_doc.GetUnusedElements(new HashSet<ElementId>()).Count} 个"; }
            catch (Exception ex)
            {
                TxtUnusedCount.Text = "读取失败";
                DiagnosticLog.WriteError("读取可清理图元", ex);
            }

            if (cloud || _doc.IsWorkshared)
                TxtEstimateHint.Text = "体积预估适用于已保存的独立 RVT。";
            else if (!hasDiskFile)
                TxtEstimateHint.Text = "保存当前项目并重新打开此窗口后可预估。";
            else
            {
                _estimateSourcePath = path;
                TxtEstimateHint.Text = "以磁盘已保存版本为准，在临时副本上执行一轮清理。";
            }
#else
            TxtUnusedCount.Text = "需 Revit 2024 或更新版本";
            TxtEstimateHint.Text = "体积预估需 Revit 2024 或更新版本。";
#endif
            BtnCalculatePurge.IsEnabled = _estimateSourcePath != null;
        }

        private void BtnCalculatePurge_Click(object sender, RoutedEventArgs e)
        {
#if REVIT2024_OR_GREATER
            if (_estimating || _estimateSourcePath == null) return;
            _estimating = true;
            BtnCalculatePurge.IsEnabled = false;
            TxtPurgedSize.Text = TxtSavedSize.Text = "-";
            string folder = Path.Combine(Path.GetTempPath(), "YangTools-Purge-" + Guid.NewGuid().ToString("N"));
            Document? tempDoc = null;

            try
            {
                SetStatus("正在打开临时副本…");
                Directory.CreateDirectory(folder);
                string copy = Path.Combine(folder, "估算副本.rvt");
                File.Copy(_estimateSourcePath, copy);
                using (var sourceInfo = BasicFileInfo.Extract(copy))
                {
                    if (sourceInfo.IsWorkshared)
                        throw new InvalidOperationException("磁盘版本是工作共享模型，无法估算独立 RVT 的清理体积。");
                }
                long originalSize = new FileInfo(copy).Length;
                TxtCurrentSizeCard2.Text = FormatSize(originalSize);
                tempDoc = _uiapp.Application.OpenDocumentFile(copy);

                SetStatus("正在清理副本…");
                var unused = tempDoc.GetUnusedElements(new HashSet<ElementId>());
                if (unused.Count > 0)
                {
                    using var transaction = new Transaction(tempDoc, "清理估算副本");
                    if (transaction.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动副本清理事务。");
                    tempDoc.Delete(unused);
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("副本清理未成功提交。");
                    tempDoc.Save();
                }

                long purgedSize = new FileInfo(copy).Length;
                TxtPurgedSize.Text = FormatSize(purgedSize);
                TxtSavedSize.Text = purgedSize <= originalSize
                    ? FormatSize(originalSize - purgedSize)
                    : $"增加 {FormatSize(purgedSize - originalSize)}";
                TxtStatus.Text = "预估完成";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "预估失败";
                MessageBox.Show(this, ex.Message, "文件体积预估", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                try
                {
                    if (tempDoc != null && !tempDoc.Close(false))
                        throw new InvalidOperationException("Revit 未关闭临时副本。");
                    if (Directory.Exists(folder))
                        Directory.Delete(folder, recursive: true);
                }
                catch (Exception ex)
                {
                    TxtStatus.Text += " · 临时目录清理失败";
                    DiagnosticLog.WriteError($"清理体积预估目录：{folder}", ex);
                }
                finally
                {
                    _estimating = false;
                    BtnCalculatePurge.IsEnabled = true;
                }
            }
#endif
        }

        private void SetStatus(string message)
        {
            TxtStatus.Text = message;
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        }

        private static string FormatSize(long bytes) => $"{bytes / (1024.0 * 1024.0):F2} MB";

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_documentPath))
                Clipboard.SetText(_documentPath);
        }

        private void CopyCentralPath_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_centralPath))
                Clipboard.SetText(_centralPath);
        }
    }
}
