using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using YangTools.Revit.Core.ParameterCleanup;
using YangTools.Revit.Models.ParameterCleanup;

namespace YangTools.Revit.UI
{
    public partial class DuplicateProjectParameterCleanupWindow : Window
    {
        private readonly Document _document;
        private readonly ParameterScanService _scanService;
        private readonly ParameterMergeService _mergeService;
        private readonly ParameterDeleteService _deleteService;
        private readonly CsvReportWriter _reportWriter;
        private readonly ParameterCleanupVerificationService _verificationService;
        private ScanResult? _scanResult;
        private bool _busy;
        private DateTime _lastUiPumpUtc = DateTime.MinValue;

        public DuplicateProjectParameterCleanupWindow(Document document)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);

            _document = document ?? throw new ArgumentNullException(nameof(document));
            _scanService = new ParameterScanService(_document);
            _mergeService = new ParameterMergeService(_document);
            _deleteService = new ParameterDeleteService(_document);
            _reportWriter = new CsvReportWriter(_document);
            _verificationService = new ParameterCleanupVerificationService(_document);
        }

        private MergeOptions CurrentOptions => new MergeOptions
        {
            AllowDeleteSharedParameters = AllowDeleteSharedCheckBox.IsChecked == true,
            DeleteSourcesWithConflicts = DeleteConflictsCheckBox.IsChecked == true
        };

        private void ScanButton_Click(object sender, RoutedEventArgs e) => RunSafely(Scan);
        private void MergeButton_Click(object sender, RoutedEventArgs e) => RunSafely(Merge);
        private void QuickButton_Click(object sender, RoutedEventArgs e) => RunSafely(QuickProcess);
        private void DeleteButton_Click(object sender, RoutedEventArgs e) => RunSafely(DeleteSafe);
        private void ReportButton_Click(object sender, RoutedEventArgs e) => OpenReportFolder();
        private void SelectAllButton_Click(object sender, RoutedEventArgs e) => ApplySelection(SelectionMode.AllCompatible);
        private void SelectNoneButton_Click(object sender, RoutedEventArgs e) => ApplySelection(SelectionMode.None);
        private void InvertSelectionButton_Click(object sender, RoutedEventArgs e) => ApplySelection(SelectionMode.InvertCompatible);
        private void SelectFirstTenButton_Click(object sender, RoutedEventArgs e) => ApplySelection(SelectionMode.FirstTenSources);
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_busy) Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            const string help =
                "功能用途\n" +
                "扫描当前项目中同名但拥有不同 ParameterElement ID 的项目参数，并将其合并为一个保留参数。\n\n" +
                "处理方式\n" +
                "1. 保留一个现有原参数，不采用全部删除重建。\n" +
                "2. 合并同名参数的绑定类别。\n" +
                "3. 清理前只做一次实例/类型扫描，同时建立值快照和参数值索引；复制阶段只访问真正有来源值的元素。\n" +
                "4. Double/Integer/String/ElementId 按内部存储类型直接复制，不使用显示字符串转换。\n" +
                "5. 删除来源参数时，先读取 Revit GetDependentElements；参数自身的依赖子对象可随参数删除，未预期对象才回滚定位。\n" +
                "6. 正式提交前复核：清理前每个不同非空内部值仍存在，已完整清理的参数组只剩一个 ParameterElement；失败自动回滚。\n\n" +
                "按钮说明\n" +
                "扫描：只读分析并生成预览报告。\n" +
                "仅合并：合并绑定和值，但不删除来源参数。\n" +
                "快速处理：合并、复制值、删除可删除来源参数，并自动重新扫描复核。\n" +
                "仅删除：用于已经完成合并后的来源参数。\n\n" +
                "建议先另存 RVT 副本。完成后可使用 Revit 撤销。";

            System.Windows.MessageBox.Show(
                this,
                help,
                "重复项目参数清理 - 功能说明",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void Scan()
        {
            AppendLog("开始扫描：" + _document.Title);
            _scanResult = _scanService.Scan(AppendLog);
            PopulateGrid();
            WriteReports(_scanResult);

            MergeButton.IsEnabled = _scanResult.Groups.Any(x => x.IsCompatible);
            QuickButton.IsEnabled = _scanResult.Groups.Any(x => x.IsCompatible);
            DeleteButton.IsEnabled = false;
            ReportButton.IsEnabled = true;

            if (_scanResult.FilterReferenceScanIncomplete)
            {
                AppendLog("提示：部分视图过滤器规则无法完整解析；不会阻断全部参数。已识别引用仍受保护，未预期级联对象会回滚定位。");
            }

            AppendLog(string.Empty);
            AppendLog(ParameterCleanupPreviewBuilder.BuildScanPreview(_scanResult, CurrentOptions));
            UpdateSummary();
        }

        private void Merge()
        {
            if (_scanResult == null) return;
            CommitGridEdits();

            int selected = _scanResult.Groups.Count(x => x.Selected && x.IsCompatible);
            int selectedSources = _scanResult.Groups
                .Where(x => x.Selected && x.IsCompatible)
                .Sum(x => x.SourceCount);
            if (selected == 0)
            {
                ShowInfo("没有选中可处理的参数组。");
                return;
            }

            MessageBoxResult confirmation = System.Windows.MessageBox.Show(
                this,
                $"将修改当前模型中的项目参数绑定和参数值。\n\n选中参数组：{selected}；来源参数：{selectedSources}。继续吗？",
                "确认合并",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes) return;

            AppendLog("开始合并参数绑定和值……");
            _mergeService.MergeSelected(_scanResult, CurrentOptions, AppendLog);
            RefreshGrid();
            WriteReports(_scanResult);
            DeleteButton.IsEnabled = _scanResult.Groups.Any(x => x.ValuesMerged);
            UpdateSummary();
        }

        private void QuickProcess()
        {
            if (_scanResult == null) return;
            CommitGridEdits();

            int selected = _scanResult.Groups.Count(x => x.Selected && x.IsCompatible);
            int selectedSources = _scanResult.Groups
                .Where(x => x.Selected && x.IsCompatible)
                .Sum(x => x.SourceCount);
            if (selected == 0)
            {
                ShowInfo("没有选中可处理的参数组。");
                return;
            }

            MessageBoxResult confirmation = System.Windows.MessageBox.Show(
                this,
                "将一次完成：合并绑定、复制参数值、删除符合当前选项的来源参数。\n\n" +
                "提交前会复核：清理前所有不同非空内部值仍然存在，且已完整删除的参数组只剩一个项目参数。\n\n" +
                $"选中参数组：{selected}；来源参数：{selectedSources}。继续吗？",
                "快速处理",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes) return;

            var totalTimer = Stopwatch.StartNew();
            ScanResult before = _scanResult;

            var phaseTimer = Stopwatch.StartNew();
            ParameterCleanupSnapshot snapshot = _verificationService.CaptureBefore(before, AppendLog);
            AppendPerformance("建立清理前快照", phaseTimer);

            ParameterCleanupVerificationResult? verification = null;
            ScanResult? verifiedAfter = null;
            int deletedCount = 0;
            AppendLog("快速处理开始：使用已验证的值索引引擎合并绑定和值。大型项目通常需要几十秒，窗口会按阶段刷新，请勿重复点击。 ");

            using (var group = new TransactionGroup(_document, "重复项目参数：合并并清理"))
            {
                if (group.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动快速处理事务组。");

                try
                {
                    phaseTimer.Restart();
                    _mergeService.MergeSelected(before, CurrentOptions, AppendLog, snapshot.ValueIndex);
                    AppendPerformance("合并绑定与参数值", phaseTimer);

                    int candidates = before.Groups
                        .Where(x => x.Selected && x.ValuesMerged)
                        .SelectMany(x => x.Sources)
                        .Count(x => string.IsNullOrEmpty(x.DeleteBlockReason) && !x.Deleted);

                    phaseTimer.Restart();
                    if (candidates > 0)
                    {
                        AppendLog($"准备快速删除 {candidates} 个候选参数……");
                        deletedCount = _deleteService.DeleteSafeSources(before, AppendLog);
                    }
                    else
                    {
                        AppendLog("合并完成，但没有满足当前删除条件的来源参数。");
                    }
                    AppendPerformance("删除来源参数", phaseTimer);

                    AppendLog("提交前正在复核参数唯一性和元素参数值完整性……");
                    phaseTimer.Restart();
                    verifiedAfter = _scanService.Scan(AppendLog);
                    verifiedAfter.ReportFolder = before.ReportFolder;
                    AppendPerformance("提交前项目参数复扫", phaseTimer);

                    phaseTimer.Restart();
                    verification = _verificationService.VerifyAfter(snapshot, before, verifiedAfter, AppendLog);
                    AppendPerformance("参数值与唯一性复核", phaseTimer);

                    if (!verification.CanCommit)
                    {
                        throw new InvalidOperationException(
                            "数据完整性复核失败：发现原有参数值缺失，或已报告完整删除的参数组仍不是唯一参数。" +
                            "本轮模型修改已取消。详细结果将在回滚后写入复核 CSV。");
                    }

                    phaseTimer.Restart();
                    TransactionStatus groupStatus = group.Assimilate();
                    AppendPerformance("TransactionGroup 提交", phaseTimer);
                    if (groupStatus != TransactionStatus.Committed)
                        throw new InvalidOperationException("快速处理事务组未成功提交：" + groupStatus);

                    AppendLog("完整性复核通过。快速处理已合并为一个 Revit 撤销项：重复项目参数：合并并清理。");
                }
                catch
                {
                    if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
                    AppendLog("快速处理未通过提交条件，事务组已回滚。正在刷新回滚后的模型状态……");

                    if (verifiedAfter != null && verification != null)
                    {
                        try { _reportWriter.WriteIntegrityVerification(before, verifiedAfter, verification); }
                        catch (Exception reportEx) { AppendLog("复核失败报告写入失败：" + reportEx.Message); }
                    }

                    _scanResult = _scanService.Scan(AppendLog);
                    PopulateGrid();
                    UpdateSummary();
                    throw;
                }
            }

            if (verifiedAfter == null || verification == null)
                throw new InvalidOperationException("清理完成，但未生成提交前复核结果。");

            phaseTimer.Restart();
            WriteReports(before);
            _reportWriter.WriteVerification(before, verifiedAfter, deletedCount);
            _reportWriter.WriteIntegrityVerification(before, verifiedAfter, verification);
            AppendPerformance("CSV 报告写入", phaseTimer);

            AppendLog(string.Empty);
            AppendLog(ParameterCleanupPreviewBuilder.BuildPostCleanupReport(before, verifiedAfter, deletedCount));
            AppendLog(string.Empty);
            AppendLog(ParameterCleanupVerificationService.BuildSummary(verification));

            _scanResult = verifiedAfter;
            phaseTimer.Restart();
            PopulateGrid();
            MergeButton.IsEnabled = verifiedAfter.Groups.Any(x => x.IsCompatible);
            QuickButton.IsEnabled = verifiedAfter.Groups.Any(x => x.IsCompatible);
            DeleteButton.IsEnabled = false;
            ReportButton.IsEnabled = true;
            UpdateSummary();
            AppendPerformance("界面刷新", phaseTimer);
            AppendLog($"[性能] 快速处理全链路：{totalTimer.Elapsed.TotalMilliseconds:0.0} ms");
        }

        private void DeleteSafe()
        {
            if (_scanResult == null) return;
            CommitGridEdits();

            int candidates = _scanResult.Groups
                .Where(x => x.Selected && x.ValuesMerged)
                .SelectMany(x => x.Sources)
                .Count(x => string.IsNullOrEmpty(x.DeleteBlockReason) && !x.Deleted);

            if (candidates == 0)
            {
                ShowInfo("当前没有可删除的重复参数。受保护原因已写入 CSV 报告。");
                return;
            }

            MessageBoxResult confirmation = System.Windows.MessageBox.Show(
                this,
                $"即将删除 {candidates} 个项目参数。\n\n" +
                "删除后会自动复核元素参数值和参数唯一性；失败则回滚整个删除事务组。\n" +
                "完成后仍可使用 Revit 撤销。确认继续吗？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes) return;

            var totalTimer = Stopwatch.StartNew();
            ScanResult before = _scanResult;
            var phaseTimer = Stopwatch.StartNew();
            ParameterCleanupSnapshot snapshot = _verificationService.CaptureBefore(before, AppendLog);
            AppendPerformance("建立删除前快照", phaseTimer);

            ParameterCleanupVerificationResult? verification = null;
            ScanResult? verifiedAfter = null;
            int deletedCount = 0;

            using (var group = new TransactionGroup(_document, "重复项目参数：仅删除并复核"))
            {
                if (group.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动删除复核事务组。");

                try
                {
                    phaseTimer.Restart();
                    deletedCount = _deleteService.DeleteSafeSources(before, AppendLog);
                    AppendPerformance("删除来源参数", phaseTimer);

                    AppendLog("提交前正在复核参数唯一性和元素参数值完整性……");
                    phaseTimer.Restart();
                    verifiedAfter = _scanService.Scan(AppendLog);
                    verifiedAfter.ReportFolder = before.ReportFolder;
                    AppendPerformance("提交前项目参数复扫", phaseTimer);

                    phaseTimer.Restart();
                    verification = _verificationService.VerifyAfter(snapshot, before, verifiedAfter, AppendLog);
                    AppendPerformance("参数值与唯一性复核", phaseTimer);

                    if (!verification.CanCommit)
                        throw new InvalidOperationException("删除后数据完整性复核失败。本轮删除已取消。");

                    phaseTimer.Restart();
                    TransactionStatus groupStatus = group.Assimilate();
                    AppendPerformance("TransactionGroup 提交", phaseTimer);
                    if (groupStatus != TransactionStatus.Committed)
                        throw new InvalidOperationException("删除复核事务组未成功提交：" + groupStatus);
                }
                catch
                {
                    if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
                    AppendLog("删除复核失败，事务组已回滚。正在刷新回滚后的模型状态……");

                    if (verifiedAfter != null && verification != null)
                    {
                        try { _reportWriter.WriteIntegrityVerification(before, verifiedAfter, verification); }
                        catch (Exception reportEx) { AppendLog("复核失败报告写入失败：" + reportEx.Message); }
                    }

                    _scanResult = _scanService.Scan(AppendLog);
                    PopulateGrid();
                    UpdateSummary();
                    throw;
                }
            }

            if (verifiedAfter == null || verification == null)
                throw new InvalidOperationException("删除完成，但未生成提交前复核结果。");

            phaseTimer.Restart();
            WriteReports(before);
            _reportWriter.WriteVerification(before, verifiedAfter, deletedCount);
            _reportWriter.WriteIntegrityVerification(before, verifiedAfter, verification);
            AppendPerformance("CSV 报告写入", phaseTimer);

            AppendLog(string.Empty);
            AppendLog(ParameterCleanupPreviewBuilder.BuildPostCleanupReport(before, verifiedAfter, deletedCount));
            AppendLog(string.Empty);
            AppendLog(ParameterCleanupVerificationService.BuildSummary(verification));

            _scanResult = verifiedAfter;
            phaseTimer.Restart();
            PopulateGrid();
            MergeButton.IsEnabled = verifiedAfter.Groups.Any(x => x.IsCompatible);
            QuickButton.IsEnabled = verifiedAfter.Groups.Any(x => x.IsCompatible);
            DeleteButton.IsEnabled = false;
            ReportButton.IsEnabled = true;
            UpdateSummary();
            AppendPerformance("界面刷新", phaseTimer);
            AppendLog($"[性能] 仅删除全链路：{totalTimer.Elapsed.TotalMilliseconds:0.0} ms");
        }

        private enum SelectionMode
        {
            AllCompatible,
            None,
            InvertCompatible,
            FirstTenSources
        }

        private void ApplySelection(SelectionMode mode)
        {
            if (_scanResult == null || _busy) return;
            CommitGridEdits();

            DuplicateGroupPlan[] compatible = _scanResult.Groups
                .Where(x => x.IsCompatible)
                .ToArray();

            switch (mode)
            {
                case SelectionMode.AllCompatible:
                    foreach (DuplicateGroupPlan plan in _scanResult.Groups)
                        plan.Selected = plan.IsCompatible;
                    break;
                case SelectionMode.None:
                    foreach (DuplicateGroupPlan plan in _scanResult.Groups)
                        plan.Selected = false;
                    break;
                case SelectionMode.InvertCompatible:
                    foreach (DuplicateGroupPlan plan in _scanResult.Groups)
                        plan.Selected = plan.IsCompatible && !plan.Selected;
                    break;
                case SelectionMode.FirstTenSources:
                    foreach (DuplicateGroupPlan plan in _scanResult.Groups)
                        plan.Selected = false;
                    int remainingSources = 10;
                    foreach (DuplicateGroupPlan plan in compatible)
                    {
                        if (plan.SourceCount <= 0 || plan.SourceCount > remainingSources) continue;
                        plan.Selected = true;
                        remainingSources -= plan.SourceCount;
                        if (remainingSources == 0) break;
                    }
                    break;
            }

            RefreshGrid();
            UpdateSummary();
            int selectedGroups = compatible.Count(x => x.Selected);
            int selectedSources = compatible.Where(x => x.Selected).Sum(x => x.SourceCount);
            AppendLog($"选择已更新：{selectedGroups} 组，涉及 {selectedSources} 个来源参数。" +
                      (mode == SelectionMode.FirstTenSources
                          ? " 测试选择不会超过 10 个来源参数；若组大小无法刚好凑满，实际数量可能少于 10。"
                          : string.Empty));
            AppendLog(string.Empty);
            AppendLog(ParameterCleanupPreviewBuilder.BuildScanPreview(_scanResult, CurrentOptions));
        }

        private void GroupSelectionCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_scanResult == null || _busy) return;
            Dispatcher.BeginInvoke(new Action(UpdateSummary));
        }

        private void PopulateGrid()
        {
            ResultGrid.ItemsSource = null;
            ResultGrid.ItemsSource = _scanResult?.Groups;
            UpdateSelectionSummary();
        }

        private void RefreshGrid()
        {
            ResultGrid.Items.Refresh();
        }

        private void CommitGridEdits()
        {
            ResultGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ResultGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private void WriteReports(ScanResult result)
        {
            string folder = _reportWriter.WriteAll(result);
            AppendLog("CSV 报告已更新：" + folder);
        }

        private void OpenReportFolder()
        {
            if (_scanResult == null) return;
            if (string.IsNullOrWhiteSpace(_scanResult.ReportFolder)) WriteReports(_scanResult);
            CsvReportWriter.OpenFolder(_scanResult.ReportFolder);
        }

        private void UpdateSummary()
        {
            if (_scanResult == null)
            {
                SummaryText.Text = "尚未扫描。";
                UpdateSelectionSummary();
                return;
            }

            int selected = _scanResult.Groups.Count(x => x.Selected && x.IsCompatible);
            int deleted = _scanResult.Groups.SelectMany(x => x.Records).Count(x => x.Deleted);
            int blocked = _scanResult.Groups
                .SelectMany(x => x.Sources)
                .Count(x => !x.Deleted && !string.IsNullOrEmpty(x.DeleteBlockReason));

            SummaryText.Text =
                $"项目参数 {_scanResult.AllParameters.Count}；唯一名称 {_scanResult.DistinctNameCount}；" +
                $"不重复 {_scanResult.NonDuplicateParameterCount}；重复组 {_scanResult.Groups.Count}；" +
                $"可处理 {_scanResult.CompatibleGroupCount}；已选择 {selected}；冲突 {_scanResult.TotalConflictCount}；" +
                $"错误 {_scanResult.Errors.Count}；已删除 {deleted}；受保护/失败 {blocked}";
            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            if (_scanResult == null)
            {
                SelectionSummaryText.Text = "未选择参数组";
                return;
            }

            int selectedGroups = _scanResult.Groups.Count(x => x.Selected && x.IsCompatible);
            int selectedSources = _scanResult.Groups
                .Where(x => x.Selected && x.IsCompatible)
                .Sum(x => x.SourceCount);
            SelectionSummaryText.Text = $"已选 {selectedGroups} 组 / {selectedSources} 个来源参数";
        }

        private void AppendPerformance(string stage, Stopwatch timer)
        {
            AppendLog($"[性能] {stage}：{timer.Elapsed.TotalMilliseconds:0.0} ms");
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                LogTextBox.AppendText(Environment.NewLine);
            }
            else
            {
                string[] lines = message.Replace("\r\n", "\n").Split('\n');
                foreach (string line in lines)
                {
                    if (line.Length == 0)
                    {
                        LogTextBox.AppendText(Environment.NewLine);
                    }
                    else
                    {
                        LogTextBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + line + Environment.NewLine);
                    }
                }
            }
            LogTextBox.ScrollToEnd();
            PumpUi();
        }

        private void PumpUi(bool force = false)
        {
            // Revit API 只能在 Revit 主线程执行，不能用 Task.Run 直接访问模型。
            // 这里只在安全日志检查点泵送 WPF 渲染；所有按钮在运行期间均已禁用，避免重入模型操作。
            DateTime now = DateTime.UtcNow;
            if (!force && (now - _lastUiPumpUtc).TotalMilliseconds < 200) return;
            _lastUiPumpUtc = now;

            try
            {
                LogTextBox.UpdateLayout();
                Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => { }));
            }
            catch
            {
                // UI 刷新失败不能中断模型处理。
            }
        }

        private void RunSafely(Action action)
        {
            SetBusy(true);
            try
            {
                action();
            }
            catch (Exception ex)
            {
                string logPath = Core.DiagnosticLog.WriteError("重复项目参数清理", ex);
                AppendLog("失败：" + ex.Message);
                System.Windows.MessageBox.Show(
                    this,
                    ex.Message + "\n\n诊断日志：" + logPath,
                    "重复项目参数清理失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            Cursor = busy ? Cursors.Wait : Cursors.Arrow;
            ScanButton.IsEnabled = !busy;
            MergeButton.IsEnabled = !busy && _scanResult != null && _scanResult.Groups.Any(x => x.IsCompatible);
            QuickButton.IsEnabled = !busy && _scanResult != null && _scanResult.Groups.Any(x => x.IsCompatible);
            DeleteButton.IsEnabled = !busy && _scanResult != null && _scanResult.Groups.Any(x => x.ValuesMerged);
            ReportButton.IsEnabled = !busy && _scanResult != null;
            CloseButton.IsEnabled = !busy;
            ResultGrid.IsEnabled = !busy;
            SelectAllButton.IsEnabled = !busy && _scanResult != null;
            SelectNoneButton.IsEnabled = !busy && _scanResult != null;
            InvertSelectionButton.IsEnabled = !busy && _scanResult != null;
            SelectFirstTenButton.IsEnabled = !busy && _scanResult != null;
            AllowDeleteSharedCheckBox.IsEnabled = !busy;
            DeleteConflictsCheckBox.IsEnabled = !busy;
            PumpUi(force: true);
        }

        private void ShowInfo(string message)
        {
            System.Windows.MessageBox.Show(
                this,
                message,
                "重复项目参数清理",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
