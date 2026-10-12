using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Core.BatchTasks;
using YangTools.Revit.Models.BatchTask;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace YangTools.Revit.UI
{
    public partial class BatchTaskWindow : Window
    {
        private readonly UIApplication _uiApp;
        private readonly BatchTaskViewModel _viewModel;
        private bool _isRunning;

        public BatchTaskWindow(UIApplication uiApp)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _uiApp = uiApp ?? throw new ArgumentNullException(nameof(uiApp));
            _viewModel = new BatchTaskViewModel(uiApp);
            DataContext = _viewModel;
        }

        private void BtnAddDocument_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Revit 项目 (*.rvt)|*.rvt",
                Multiselect = true,
                Title = "添加要导出的 Revit 模型（Desktop Connector 文件需已同步到本机）"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            foreach (string file in dialog.FileNames)
                _viewModel.AddDocument(file);
        }

        private void BtnAddOpenDocuments_Click(object sender, RoutedEventArgs e)
        {
            int added = _viewModel.AddOpenDocuments(_uiApp);
            MessageBox.Show(
                added > 0
                    ? $"已加入 {added} 个当前 Revit 会话中已打开的项目模型（包含可识别的云模型）。"
                    : "没有发现新的已打开项目模型。",
                "添加已打开模型",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnPreflight_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
                return;

            BatchRunRequest request = BatchRunRequest.Capture(_viewModel);
            if (request.Documents.Count == 0)
            {
                MessageBox.Show("请至少勾选一个目标模型。", "预检", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!request.Options.HasAnyExportEnabled)
            {
                MessageBox.Show("请至少启用一种导出/打印格式。", "预检", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusyState(true, "正在执行全批预检...");
            try
            {
                BatchTaskRunResult overall = RunFullExportPreflight(request);
                ShowPreflightResult(request.Documents.Count, overall);
            }
            finally
            {
                SetBusyState(false, null);
            }
        }

        /// <summary>
        /// 使用执行快照进行跨模型全批规划。预检按钮与执行入口都调用本方法，
        /// 确定性路径冲突必须在任何导出文件发布之前发现。
        /// </summary>
        private BatchTaskRunResult RunFullExportPreflight(BatchRunRequest request)
        {
            var overall = new BatchTaskRunResult();
            if (!request.Options.HasAnyExportEnabled)
                return overall;

            bool multipleDocuments = request.Documents.Count > 1;
            var plannedOutputReservations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < request.Documents.Count; index++)
            {
                BatchDocumentInfo info = request.Documents[index];
                UpdateProgress($"预检 [{index + 1}/{request.Documents.Count}]：{info.DisplayName}");
                try
                {
                    using (BatchDocumentHandle handle = BatchDocumentOpener.Open(_uiApp, info))
                    {
                        BatchTaskRunResult check = BatchTaskEngine.ValidateExports(
                            handle.Document,
                            request.Options,
                            multipleDocuments,
                            plannedOutputReservations);
                        overall.Merge(check, $"[{handle.Label}] ");
                    }
                }
                catch (Exception ex)
                {
                    overall.Errors.Add($"[{info.DisplayName}] 打开/预检失败：{ex.Message}");
                }
            }

            return overall;
        }

        private void ShowPreflightResult(int documentCount, BatchTaskRunResult overall)
        {
            var message = new StringBuilder();
            message.AppendLine($"模型：{documentCount}");
            message.AppendLine($"通过项：{overall.SuccessCount}");
            message.AppendLine($"提示：{overall.WarningCount}");
            message.AppendLine($"失败：{overall.ErrorCount}");
            if (overall.Errors.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("主要问题：");
                foreach (string error in overall.Errors.Take(12))
                    message.AppendLine("• " + error);
            }
            MessageBox.Show(
                message.ToString().TrimEnd(),
                overall.ErrorCount == 0 ? "预检通过" : "预检发现问题",
                MessageBoxButton.OK,
                overall.ErrorCount == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            TxtProgress.Text = overall.ErrorCount == 0 ? "预检通过" : $"预检：{overall.ErrorCount} 项失败";
        }

        private void SetBusyState(bool busy, string? progressText)
        {
            _isRunning = busy;
            BtnExecute.IsEnabled = !busy;
            RootLayout.IsHitTestVisible = !busy;
            if (!string.IsNullOrWhiteSpace(progressText))
                TxtProgress.Text = progressText;
            FlushUi();
        }

        private void BtnRemoveDocument_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is BatchDocumentInfo document)
                _viewModel.RemoveDocument(document);
        }

        private void BtnAddFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Revit 与 CAD (*.rvt;*.dwg;*.dxf)|*.rvt;*.dwg;*.dxf",
                Multiselect = true,
                Title = "添加要链接到当前文档的文件"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            foreach (string file in dialog.FileNames)
                _viewModel.AddLink(file);
        }

        private void BtnRemoveLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is LinkItem item)
                _viewModel.Links.Remove(item);
        }

        private void BtnClearLinks_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.Links.Count == 0)
                return;

            if (MessageBox.Show(
                    "清空链接任务列表？这不会删除 Revit 中已经存在的链接。",
                    "清空列表",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _viewModel.Links.Clear();
            }
        }

        private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择批处理输出目录",
                ShowNewFolderButton = true,
                SelectedPath = Directory.Exists(_viewModel.OutputFolder)
                    ? _viewModel.OutputFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            })
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
                    && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    _viewModel.OutputFolder = dialog.SelectedPath;
                }
            }
        }

        private void BtnOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = _viewModel.OutputFolder;
                if (string.IsNullOrWhiteSpace(folder))
                    return;

                Directory.CreateDirectory(folder);
                OpenFolder(folder);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开输出目录：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
                return;

            BatchRunRequest request = BatchRunRequest.Capture(_viewModel);
            if (request.Options.Links.Count == 0 && !request.Options.HasAnyExportEnabled)
            {
                MessageBox.Show("请至少添加一个链接文件，或开启一项导出/打印任务。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (request.Options.HasAnyExportEnabled && request.Documents.Count == 0)
            {
                MessageBox.Show("已开启导出/打印任务，但没有勾选任何目标模型。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 保存的是用户当前界面设置；真正执行只读取上面的不可变运行快照。
            _viewModel.SaveUserSettings();
            SetBusyState(true, "正在执行，请不要关闭 Revit...");

            string operationId = Guid.NewGuid().ToString("N");
            string batchRunId = Guid.NewGuid().ToString("N");
            DiagnosticLog.WriteProductOperation(
                "BatchPublisher",
                operationId,
                "START",
                _uiApp.ActiveUIDocument?.Document,
                $"Documents={request.Documents.Count}; Links={request.Options.Links.Count}; " +
                $"NWC={request.Options.EnableNwc}; IFC={request.Options.EnableIfc}; DWG={request.Options.EnableDwg}; " +
                $"PDF={request.Options.EnablePdf}; PrintPDF={request.Options.EnablePrintPdf}",
                batchRunId);

            var overall = new BatchTaskRunResult();
            string reportPath = string.Empty;
            string rootFolder = string.IsNullOrWhiteSpace(request.Options.OutputFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "YangTools_BatchExport")
                : request.Options.OutputFolder.Trim();

            try
            {
                // F02：执行按钮自身强制调用与“预检”按钮相同的全批规划。
                // 任一确定性冲突存在时，在处理链接和导出之前整体中止。
                if (request.Options.HasAnyExportEnabled)
                {
                    BatchTaskRunResult preflight = RunFullExportPreflight(request);
                    if (preflight.ErrorCount > 0)
                    {
                        ShowPreflightResult(request.Documents.Count, preflight);
                        DiagnosticLog.WriteProductOperation(
                            "BatchPublisher",
                            operationId,
                            "PREFLIGHT_FAILED",
                            _uiApp.ActiveUIDocument?.Document,
                            string.Join(" | ", preflight.Errors.Take(20)),
                            batchRunId);
                        return;
                    }
                    foreach (string warning in preflight.Warnings)
                        overall.Warnings.Add("[预检] " + warning);
                }

                try
                {
                    Directory.CreateDirectory(rootFolder);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"无法创建输出目录：{ex.Message}", ex);
                }

                if (request.Options.Links.Count > 0)
                {
                    Document currentDocument = _uiApp.ActiveUIDocument.Document;
                    UpdateProgress($"[{currentDocument.Title}] 正在处理链接...");
                    BatchTaskRunResult linkResult = BatchTaskEngine.RunLinks(
                        currentDocument,
                        request.Options,
                        message => UpdateProgress($"[{currentDocument.Title}] {message}"));
                    overall.Merge(linkResult, $"[{currentDocument.Title}] ");
                    DiagnosticLog.WriteProductOperation(
                        "BatchPublisher.Links",
                        operationId,
                        linkResult.ErrorCount == 0 ? "COMPLETE" : "FAILED",
                        currentDocument,
                        $"Success={linkResult.SuccessCount}; Warning={linkResult.WarningCount}; Error={linkResult.ErrorCount}; " +
                        string.Join(" | ", linkResult.Successes.Concat(linkResult.Errors).Take(20)),
                        batchRunId);
                }

                if (request.Options.HasAnyExportEnabled)
                {
                    bool multipleDocuments = request.Documents.Count > 1;
                    var batchOutputReservations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int index = 0; index < request.Documents.Count; index++)
                    {
                        BatchDocumentInfo documentInfo = request.Documents[index];
                        string initialLabel = documentInfo.IsCurrentDocument
                            ? _uiApp.ActiveUIDocument.Document.Title
                            : documentInfo.DisplayName;
                        UpdateProgress($"[{index + 1}/{request.Documents.Count}] 打开：{initialLabel}");

                        try
                        {
                            using (BatchDocumentHandle handle = BatchDocumentOpener.Open(_uiApp, documentInfo))
                            {
                                UpdateProgress($"[{index + 1}/{request.Documents.Count}] 输出：{handle.Label}");
                                BatchTaskRunResult documentResult = BatchTaskEngine.RunExports(
                                    handle.Document,
                                    request.Options,
                                    multipleDocuments,
                                    message => UpdateProgress($"[{handle.Label}] {message}"),
                                    batchOutputReservations);
                                overall.Merge(documentResult, $"[{handle.Label}] ");
                                DiagnosticLog.WriteProductOperation(
                                    "BatchPublisher.Export",
                                    operationId,
                                    documentResult.ErrorCount == 0 ? "COMPLETE" : "FAILED",
                                    handle.Document,
                                    $"Model={handle.Label}; Success={documentResult.SuccessCount}; Warning={documentResult.WarningCount}; Error={documentResult.ErrorCount}; " +
                                    string.Join(" | ", documentResult.Successes.Concat(documentResult.Errors).Take(30)),
                                    batchRunId);
                            }
                        }
                        catch (Exception ex)
                        {
                            DiagnosticLog.WriteError($"打开/处理批处理模型：{initialLabel}", ex);
                            DiagnosticLog.WriteProductOperation("BatchPublisher.Export", operationId, "EXCEPTION", null, $"Model={initialLabel}; {ex}", batchRunId);
                            overall.Errors.Add($"[{initialLabel}] 打开或处理失败：{ex.Message}");
                        }
                    }
                }

                reportPath = WriteReport(rootFolder, overall);
                ShowCompletion(overall, reportPath);

                DiagnosticLog.WriteProductOperation(
                    "BatchPublisher",
                    operationId,
                    overall.ErrorCount == 0 ? "COMPLETE" : "COMPLETE_WITH_ERRORS",
                    _uiApp.ActiveUIDocument?.Document,
                    $"Success={overall.SuccessCount}; Warning={overall.WarningCount}; Error={overall.ErrorCount}; Report={reportPath}",
                    batchRunId);

                if (request.Options.OpenOutputFolderWhenDone && Directory.Exists(rootFolder))
                    OpenFolder(rootFolder);
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("批处理与云链接", ex);
                DiagnosticLog.WriteProductOperation("BatchPublisher", operationId, "EXCEPTION", _uiApp.ActiveUIDocument?.Document, ex.ToString(), batchRunId);
                TxtProgress.Text = "执行中断";
                MessageBox.Show(
                    $"批处理执行中断：{ex.Message}" +
                    (string.IsNullOrEmpty(logPath) ? string.Empty : $"\n诊断日志：{logPath}"),
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusyState(false, null);
            }
        }

        private void ShowCompletion(BatchTaskRunResult result, string reportPath)
        {
            TxtProgress.Text = result.ErrorCount == 0
                ? $"完成：{result.SuccessCount} 项成功，{result.WarningCount} 项提示"
                : $"完成：{result.SuccessCount} 项成功，{result.ErrorCount} 项失败";

            var text = new StringBuilder();
            text.AppendLine($"成功：{result.SuccessCount}");
            text.AppendLine($"提示：{result.WarningCount}");
            text.AppendLine($"失败：{result.ErrorCount}");
            if (!string.IsNullOrEmpty(reportPath))
                text.AppendLine($"报告：{reportPath}");

            if (result.Errors.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("主要错误：");
                foreach (string error in result.Errors.Take(8))
                    text.AppendLine("• " + error);
                if (result.Errors.Count > 8)
                    text.AppendLine($"• 另有 {result.Errors.Count - 8} 项，请查看报告。");
            }

            MessageBox.Show(
                text.ToString().TrimEnd(),
                result.ErrorCount == 0 ? "批处理完成" : "批处理完成（存在失败）",
                MessageBoxButton.OK,
                result.ErrorCount == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private static string WriteReport(string rootFolder, BatchTaskRunResult result)
        {
            try
            {
                Directory.CreateDirectory(rootFolder);
                string path = Path.Combine(rootFolder, $"YangTools-批处理报告-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                var text = new StringBuilder();
                text.AppendLine("YangTools 批处理与链接报告");
                text.AppendLine($"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                text.AppendLine($"成功：{result.SuccessCount}");
                text.AppendLine($"提示：{result.WarningCount}");
                text.AppendLine($"失败：{result.ErrorCount}");
                text.AppendLine();

                text.AppendLine("【成功】");
                foreach (string value in result.Successes)
                    text.AppendLine("- " + value);
                text.AppendLine();

                text.AppendLine("【提示】");
                foreach (string value in result.Warnings)
                    text.AppendLine("- " + value);
                text.AppendLine();

                text.AppendLine("【失败】");
                foreach (string value in result.Errors)
                    text.AppendLine("- " + value);
                text.AppendLine();

                text.AppendLine("【输出】");
                foreach (string value in result.OutputFiles.Distinct(StringComparer.OrdinalIgnoreCase))
                    text.AppendLine("- " + value);

                File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
                DiagnosticLog.WriteInfo("批处理报告", path);
                return path;
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("写入批处理报告", ex);
                return string.Empty;
            }
        }

        private void UpdateProgress(string message)
        {
            TxtProgress.Text = message;
            FlushUi();
        }

        private void FlushUi()
        {
            Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private static void OpenFolder(string folder)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isRunning)
            {
                MessageBox.Show("批处理正在执行，暂时不能关闭窗口。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                e.Cancel = true;
                return;
            }

            _viewModel.SaveUserSettings();
        }
    }
}
