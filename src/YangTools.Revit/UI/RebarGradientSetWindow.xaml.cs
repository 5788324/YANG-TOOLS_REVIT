using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Commands;
using YangTools.Revit.Core;
using YangTools.Revit.Core.Rebar;
using YangTools.Revit.Models;
using RevitRebar = Autodesk.Revit.DB.Structure.Rebar;

namespace YangTools.Revit.UI
{
    /// <summary>
    /// GR01 V1 中文 GUI。窗口层只负责拾取、编辑与结果显示；
    /// Station / spacing / 插值 / Create 统一调用已通过 Runtime 的 RebarGradientSetService。
    /// </summary>
    public partial class RebarGradientSetWindow : Window
    {
        private readonly UIApplication _uiapp;
        private readonly Document _document;
        private readonly RevitEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private readonly RebarGradientSetViewModel _viewModel = new RebarGradientSetViewModel();

        private long? _startControlId;
        private string _startControlUniqueId = string.Empty;
        private long? _endControlId;
        private string _endControlUniqueId = string.Empty;

        private string _pathKind = "ZAxis";
        private long? _pathElementId;
        private string _pathStableReference = string.Empty;
        private long? _pathOwnerId;
        private double? _pathReferenceLengthMm;

        private string _lastPlanFingerprint = string.Empty;
        private double? _lastKnownDistributionLengthMm;
        private bool _updatingZones;
        private bool _initialized;

        public RebarGradientSetWindow(UIApplication uiapp)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (uiapp.ActiveUIDocument == null) throw new InvalidOperationException("请先打开一个 Revit 项目文档。");

            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);

            _uiapp = uiapp;
            _document = uiapp.ActiveUIDocument.Document;
            _handler = new RevitEventHandler(_document, wrapInTransactionGroup: false, showAutomaticSuccess: false);
            _externalEvent = ExternalEvent.Create(_handler);

            DataContext = _viewModel;
            AddZone(new RebarGradientZoneRowViewModel
            {
                Label = "普通",
                IsAutoLength = true,
                LengthMm = 0.0,
                MaxSpacingMm = 200.0
            });

            UpdateZoneTotal();
            SetBusy(false);
            _initialized = true;
            RefreshPathControls();
            RefreshPathDisplay();
            UpdateInputValidation();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosed(EventArgs e)
        {
            try { _externalEvent.Dispose(); } catch { }
            base.OnClosed(e);
        }

        private void PickStartControl_Click(object sender, RoutedEventArgs e)
            => PickControl(isStart: true);

        private void PickEndControl_Click(object sender, RoutedEventArgs e)
            => PickControl(isStart: false);

        private void SwapControls_Click(object sender, RoutedEventArgs e)
        {
            if (!_startControlId.HasValue || !_endControlId.HasValue)
            {
                ShowInformation("请先拾取起始控制筋和结束控制筋，再交换首末。");
                return;
            }

            long? startId = _startControlId;
            string startUid = _startControlUniqueId;
            string startText = _viewModel.StartControlText;
            _startControlId = _endControlId;
            _startControlUniqueId = _endControlUniqueId;
            _viewModel.StartControlText = _viewModel.EndControlText;
            _endControlId = startId;
            _endControlUniqueId = startUid;
            _viewModel.EndControlText = startText;

            RefreshPathDisplay();
            InvalidatePreview("已交换首末控制筋；布置方向已反转，Zone[0] 仍从新的起始控制筋一侧开始。", clearDistributionLength: false);
        }

        private void PickControl(bool isStart)
        {
            if (!EnsureBoundDocumentActive()) return;
            HideForPick();
            _handler.SetAction(app =>
            {
                try
                {
                    UIDocument uidoc = app.ActiveUIDocument;
                    Reference picked = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        new RebarSelectionFilter(),
                        isStart ? "选择起始控制钢筋" : "选择结束控制钢筋");

                    RevitRebar rebar = uidoc.Document.GetElement(picked.ElementId) as RevitRebar
                        ?? throw new InvalidOperationException("选择的对象不是 Rebar。");
                    string display = DescribeRebar(uidoc.Document, rebar);
                    long id = rebar.Id.GetIdValue();
                    string uid = rebar.UniqueId ?? string.Empty;

                    Dispatcher.Invoke(() =>
                    {
                        if (isStart)
                        {
                            _startControlId = id;
                            _startControlUniqueId = uid;
                            _viewModel.StartControlText = display;
                        }
                        else
                        {
                            _endControlId = id;
                            _endControlUniqueId = uid;
                            _viewModel.EndControlText = display;
                        }
                        RefreshPathDisplay();
                        InvalidatePreview("控制钢筋已更新，请重新计算预览。", clearDistributionLength: true);
                    });
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    Dispatcher.Invoke(() => _viewModel.StatusText = "已取消拾取，当前选择未改变。");
                }
                finally
                {
                    Dispatcher.Invoke(ShowAfterPick);
                }
            }, isStart ? "GR01 拾取起始控制筋" : "GR01 拾取结束控制筋", showSuccess: false);
            _externalEvent.Raise();
        }

        private void PathKindCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            string selected = GetSelectedPathKind();
            if (!string.Equals(selected, _pathKind, StringComparison.Ordinal))
            {
                _pathKind = selected;
                _pathElementId = null;
                _pathStableReference = string.Empty;
                _pathOwnerId = null;
                _pathReferenceLengthMm = null;
                _viewModel.PathKind = selected;
                _viewModel.PathText = string.Equals(selected, "ZAxis", StringComparison.OrdinalIgnoreCase)
                    ? "全局 Z 轴（无需拾取）"
                    : "未选择";
                RefreshPathControls();
                RefreshPathDisplay();
                InvalidatePreview(
                    string.Equals(selected, "ZAxis", StringComparison.OrdinalIgnoreCase)
                        ? "已切换为全局 Z 轴；无需拾取基准，完成首末控制筋后即可预览。"
                        : "分布基准类型已切换，请重新拾取基准。",
                    clearDistributionLength: true);
            }
        }

        private void PickPath_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureBoundDocumentActive()) return;
            string kind = GetSelectedPathKind();
            if (string.Equals(kind, "ZAxis", StringComparison.OrdinalIgnoreCase))
            {
                ShowInformation("全局 Z 轴模式无需拾取基准；有效长度会由首末控制筋沿全局 Z 方向自动计算。");
                return;
            }
            HideForPick();
            _handler.SetAction(app =>
            {
                try
                {
                    UIDocument uidoc = app.ActiveUIDocument;
                    Document doc = uidoc.Document;

                    if (string.Equals(kind, "Edge", StringComparison.OrdinalIgnoreCase))
                    {
                        var filter = new StraightEdgeSelectionFilter(doc);
                        Reference picked = uidoc.Selection.PickObject(ObjectType.Edge, filter, "选择一条直线模型 Edge");
                        Element owner = doc.GetElement(picked.ElementId)
                            ?? throw new InvalidOperationException("无法解析 Edge 所属元素。");
                        GeometryObject geometry = owner.GetGeometryObjectFromReference(picked)
                            ?? throw new InvalidOperationException("无法从 Reference 读取几何对象。");
                        if (geometry is not Edge edge || edge.AsCurve() is not Line line)
                            throw new InvalidOperationException("请选择真实的直线 Edge。");

                        string stable = picked.ConvertToStableRepresentation(doc);
                        double lengthMm = line.Length * 304.8;
                        Dispatcher.Invoke(() =>
                        {
                            _pathKind = "Edge";
                            _pathElementId = null;
                            _pathStableReference = stable;
                            _pathOwnerId = owner.Id.GetIdValue();
                            _pathReferenceLengthMm = lengthMm;
                            _viewModel.PathKind = "Edge";
                            _viewModel.PathText = $"模型 Edge · Owner {owner.Id.GetIdValue()} · 基准总长 {lengthMm:0.###} mm";
                            RefreshPathDisplay();
                            InvalidatePreview("分布基准已更新；业务方向仍由起始控制筋 → 结束控制筋决定。", clearDistributionLength: true);
                        });
                    }
                    else
                    {
                        Reference picked = uidoc.Selection.PickObject(
                            ObjectType.Element,
                            new StraightModelCurveSelectionFilter(),
                            "选择一条直线 ModelLine");
                        Element element = doc.GetElement(picked.ElementId)
                            ?? throw new InvalidOperationException("无法解析 ModelLine。");
                        if (element is not ModelCurve modelCurve || modelCurve.GeometryCurve is not Line line)
                            throw new InvalidOperationException("请选择直线 ModelLine。");

                        double lengthMm = line.Length * 304.8;
                        Dispatcher.Invoke(() =>
                        {
                            _pathKind = "ModelLine";
                            _pathElementId = modelCurve.Id.GetIdValue();
                            _pathStableReference = string.Empty;
                            _pathOwnerId = modelCurve.Id.GetIdValue();
                            _pathReferenceLengthMm = lengthMm;
                            _viewModel.PathKind = "ModelLine";
                            _viewModel.PathText = $"模型线 {modelCurve.Id.GetIdValue()} · 基准总长 {lengthMm:0.###} mm";
                            RefreshPathDisplay();
                            InvalidatePreview("分布基准已更新；业务方向仍由起始控制筋 → 结束控制筋决定。", clearDistributionLength: true);
                        });
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    Dispatcher.Invoke(() => _viewModel.StatusText = "已取消拾取，当前选择未改变。");
                }
                finally
                {
                    Dispatcher.Invoke(ShowAfterPick);
                }
            }, "GR01 拾取分布路径", showSuccess: false);
            _externalEvent.Raise();
        }

        private void AddNormalZone_Click(object sender, RoutedEventArgs e)
        {
            InsertZoneAfterSelection(new RebarGradientZoneRowViewModel
            {
                Label = "普通",
                LengthMm = 1000.0,
                MaxSpacingMm = 200.0
            });
            RecalculateAutoZoneLength();
            InvalidatePreview("区段已修改，请重新计算预览。");
        }

        private void AddDenseZone_Click(object sender, RoutedEventArgs e)
        {
            InsertZoneAfterSelection(new RebarGradientZoneRowViewModel
            {
                Label = "加密",
                LengthMm = 1000.0,
                MaxSpacingMm = 100.0
            });
            RecalculateAutoZoneLength();
            InvalidatePreview("区段已修改，请重新计算预览。");
        }

        private void RemoveZone_Click(object sender, RoutedEventArgs e)
        {
            CommitGridEdits();
            if (ZoneGrid.SelectedItem is not RebarGradientZoneRowViewModel row)
                return;
            row.InputChanged -= Zone_InputChanged;
            _viewModel.Zones.Remove(row);
            RecalculateAutoZoneLength();
            UpdateZoneTotal();
            InvalidatePreview("区段已删除，请重新计算预览。");
        }

        private void MoveZoneUp_Click(object sender, RoutedEventArgs e) => MoveSelectedZone(-1);
        private void MoveZoneDown_Click(object sender, RoutedEventArgs e) => MoveSelectedZone(1);

        private void MoveSelectedZone(int delta)
        {
            CommitGridEdits();
            if (ZoneGrid.SelectedItem is not RebarGradientZoneRowViewModel row) return;
            int oldIndex = _viewModel.Zones.IndexOf(row);
            int newIndex = oldIndex + delta;
            if (oldIndex < 0 || newIndex < 0 || newIndex >= _viewModel.Zones.Count) return;
            _viewModel.Zones.Move(oldIndex, newIndex);
            ZoneGrid.SelectedItem = row;
            ZoneGrid.ScrollIntoView(row);
            RecalculateAutoZoneLength();
            UpdateZoneTotal();
            InvalidatePreview("区段顺序已修改，请重新计算预览。");
        }

        private void ZoneGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Row.Item is not RebarGradientZoneRowViewModel row || !row.IsAutoLength) return;
            string header = e.Column.Header?.ToString() ?? string.Empty;
            if (string.Equals(header, "长度(mm)", StringComparison.Ordinal))
                e.Cancel = true;
        }

        private void ZoneGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateZoneTotal();
                InvalidatePreview("区段参数已修改，请重新计算预览。");
            }));
        }

        private void Zone_InputChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_updatingZones) return;
                if (sender is RebarGradientZoneRowViewModel changed && changed.IsAutoLength)
                {
                    _updatingZones = true;
                    try
                    {
                        foreach (RebarGradientZoneRowViewModel row in _viewModel.Zones)
                        {
                            if (!ReferenceEquals(row, changed) && row.IsAutoLength)
                                row.IsAutoLength = false;
                        }
                    }
                    finally
                    {
                        _updatingZones = false;
                    }
                }
                RecalculateAutoZoneLength();
                UpdateZoneTotal();
                InvalidatePreview("区段参数已修改，请重新计算预览。");
            }));
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            CommitGridEdits();
            if (!TryBuildRequest(dryRun: true, expectedFingerprint: string.Empty, out RebarGradientSetRequest request, out string validation))
            {
                ShowInformation(validation);
                return;
            }

            SetBusy(true, "正在计算预览…");
            _handler.SetAction(app =>
            {
                RebarGradientSetResult result = RebarGradientSetService.Execute(app, request);
                Dispatcher.Invoke(() => ApplyPreviewResult(result));
            }, "GR01 计算预览", showSuccess: false, operationId: request.OperationId);
            _externalEvent.Raise();
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            CommitGridEdits();
            if (string.IsNullOrWhiteSpace(_lastPlanFingerprint))
            {
                ShowInformation("请先执行“计算预览”，确认区段、数量和实际间距后再创建。");
                return;
            }
            if (!TryBuildRequest(dryRun: false, expectedFingerprint: _lastPlanFingerprint, out RebarGradientSetRequest request, out string validation))
            {
                ShowInformation(validation);
                return;
            }

            SetBusy(true, "正在创建渐变钢筋集…");
            _handler.SetAction(app =>
            {
                RebarGradientSetResult result = RebarGradientSetService.Execute(app, request);
                Dispatcher.Invoke(() => ApplyCreateResult(result));
            }, "GR01 创建渐变钢筋集", showSuccess: false, operationId: request.OperationId);
            _externalEvent.Raise();
        }

        private void ApplyPreviewResult(RebarGradientSetResult result)
        {
            SetBusy(false);
            ClearZonePreview();

            if (!result.Success)
            {
                _lastPlanFingerprint = string.Empty;
                _viewModel.CanCreate = false;
                RefreshActionButtons();
                _viewModel.PreviewSummary = "预览失败";
                _viewModel.StatusText = BuildFailureText(result);
                UpdateInputValidation();
                return;
            }

            _lastPlanFingerprint = result.PlanFingerprint ?? string.Empty;
            _lastKnownDistributionLengthMm = result.DistributionLengthMm;
            _viewModel.CanCreate = !string.IsNullOrWhiteSpace(_lastPlanFingerprint);
            RefreshActionButtons();
            _viewModel.PreviewSummary =
                $"有效路径 {result.DistributionLengthMm:0.###} mm · {result.IntervalCount} 个间隔 · {result.BarCount} 根钢筋";
            _viewModel.HostAndTypeText =
                $"Host {result.HostId} · {result.BarTypeName} (TypeId {result.BarTypeId}) · {result.RebarStyle} · {result.DistributionPathKind}";
            _viewModel.StatusText = "预览通过。确认下面各区段实际间距后，可创建 1 个完整 Free Form Rebar Set。";

            if (result.SpacingPlan?.Zones != null)
            {
                int count = Math.Min(_viewModel.Zones.Count, result.SpacingPlan.Zones.Count);
                for (int i = 0; i < count; i++)
                {
                    RebarGradientSpacingZonePlan plan = result.SpacingPlan.Zones[i];
                    RebarGradientZoneRowViewModel row = _viewModel.Zones[i];
                    if (row.IsAutoLength)
                        row.SetResolvedAutoLength(plan.LengthMm);
                    row.PreviewMainSpacingMm = plan.MainSpacingMm;
                    row.PreviewTailSpacingMm = plan.TailSpacingMm > 1e-6 ? plan.TailSpacingMm : null;
                    row.PreviewIntervalCount = plan.IntervalCount;
                    row.PreviewText = FormatZonePlan(plan);
                }
            }
            RefreshPathDisplay();
            UpdateZoneTotal();
            UpdateInputValidation();
        }

        private void ApplyCreateResult(RebarGradientSetResult result)
        {
            SetBusy(false);
            _lastPlanFingerprint = string.Empty;
            _viewModel.CanCreate = false;
            RefreshActionButtons();

            if (!result.Success)
            {
                _viewModel.StatusText = BuildFailureText(result);
                MessageBox.Show(this, BuildFailureText(result), "渐变钢筋集", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _viewModel.PreviewSummary =
                $"已创建 Rebar {result.CreatedRebarId} · {result.PhysicalBarCount ?? result.BarCount} 根 physical bars";
            _viewModel.StatusText = "创建成功。若要再次创建，请重新计算预览；一次 Revit Undo 可撤销本次结果集。";
            MessageBox.Show(
                this,
                result.Message +
                $"\n\nRebarId：{result.CreatedRebarId}" +
                $"\nPhysical bars：{result.PhysicalBarCount}" +
                $"\nValidation：{result.FreeFormValidationResult}" +
                $"\nReadback deviation：{result.MaxReadbackDeviationMm?.ToString("0.######", CultureInfo.InvariantCulture) ?? "-"} mm",
                "渐变钢筋集",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private bool TryBuildRequest(bool dryRun, string expectedFingerprint, out RebarGradientSetRequest request, out string error)
        {
            request = new RebarGradientSetRequest();
            error = string.Empty;

            if (!_startControlId.HasValue)
            {
                error = "请先拾取起始控制筋。";
                return false;
            }
            if (!_endControlId.HasValue)
            {
                error = "请先拾取结束控制筋。";
                return false;
            }
            if (string.Equals(_pathKind, "ZAxis", StringComparison.OrdinalIgnoreCase))
            {
                // 不需要额外路径几何；Core 会使用两根控制筋的 transformed centerline 几何中心自动计算 Z 向有效长度。
            }
            else if (string.Equals(_pathKind, "Edge", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(_pathStableReference))
                {
                    error = "请先拾取模型 Edge 分布路径。";
                    return false;
                }
            }
            else if (string.Equals(_pathKind, "ModelLine", StringComparison.OrdinalIgnoreCase))
            {
                if (!_pathElementId.HasValue)
                {
                    error = "请先拾取直线 ModelLine 分布路径。";
                    return false;
                }
            }
            else
            {
                error = "不支持的分布方式。";
                return false;
            }
            if (!ValidateZoneInputs(out error))
                return false;

            var zones = new List<RebarGradientSpacingZoneInput>();
            for (int i = 0; i < _viewModel.Zones.Count; i++)
            {
                RebarGradientZoneRowViewModel row = _viewModel.Zones[i];
                zones.Add(new RebarGradientSpacingZoneInput
                {
                    Label = row.Label ?? string.Empty,
                    AutoLength = row.IsAutoLength,
                    LengthMm = row.LengthMm,
                    MaxSpacingMm = row.MaxSpacingMm,
                    ManualActualSpacingMm = row.ManualActualSpacingMm
                });
            }

            request = new RebarGradientSetRequest
            {
                OperationId = Guid.NewGuid().ToString("N"),
                StartControlRebarId = _startControlId.Value,
                ExpectedStartControlUniqueId = _startControlUniqueId,
                EndControlRebarId = _endControlId.Value,
                ExpectedEndControlUniqueId = _endControlUniqueId,
                DistributionPath = new RebarGradientDistributionPathInput
                {
                    Kind = _pathKind,
                    ElementId = _pathElementId,
                    StableReference = _pathStableReference
                },
                Zones = zones,
                ExpectedPlanFingerprint = expectedFingerprint ?? string.Empty,
                DryRun = dryRun
            };
            return true;
        }

        private void AddZone(RebarGradientZoneRowViewModel row)
        {
            row.InputChanged += Zone_InputChanged;
            _viewModel.Zones.Add(row);
            ZoneGrid?.ScrollIntoView(row);
            RecalculateAutoZoneLength();
            UpdateZoneTotal();
        }

        private void InsertZoneAfterSelection(RebarGradientZoneRowViewModel row)
        {
            row.InputChanged += Zone_InputChanged;
            int index = _viewModel.Zones.Count;
            if (ZoneGrid?.SelectedItem is RebarGradientZoneRowViewModel selected)
            {
                int selectedIndex = _viewModel.Zones.IndexOf(selected);
                if (selectedIndex >= 0) index = selectedIndex + 1;
            }
            _viewModel.Zones.Insert(index, row);
            if (ZoneGrid != null)
            {
                ZoneGrid.SelectedItem = row;
                ZoneGrid.ScrollIntoView(row);
            }
            RecalculateAutoZoneLength();
            UpdateZoneTotal();
            UpdateInputValidation();
        }

        private void InvalidatePreview(string reason, bool clearDistributionLength = false)
        {
            _lastPlanFingerprint = string.Empty;
            if (clearDistributionLength)
            {
                _lastKnownDistributionLengthMm = null;
                foreach (RebarGradientZoneRowViewModel row in _viewModel.Zones.Where(x => x.IsAutoLength))
                    row.SetResolvedAutoLength(0.0);
                RefreshPathDisplay();
            }
            _viewModel.CanCreate = false;
            RefreshActionButtons();
            ClearZonePreview();
            _viewModel.PreviewSummary = "尚未计算有效预览";
            _viewModel.HostAndTypeText = "Host / 钢筋类型将在预览后显示";
            _viewModel.StatusText = reason;
            RefreshPathDisplay();
            UpdateZoneTotal();
            UpdateInputValidation();
        }

        private void ClearZonePreview()
        {
            foreach (RebarGradientZoneRowViewModel row in _viewModel.Zones)
                row.ClearPreview();
        }

        private void UpdateZoneTotal()
        {
            if (ZoneTotalText == null) return;
            double fixedTotal = _viewModel.Zones
                .Where(x => !x.IsAutoLength && IsFinite(x.LengthMm))
                .Sum(x => x.LengthMm);
            int autoCount = _viewModel.Zones.Count(x => x.IsAutoLength);

            if (_lastKnownDistributionLengthMm.HasValue)
            {
                double length = _lastKnownDistributionLengthMm.Value;
                if (autoCount == 1)
                {
                    double remaining = length - fixedTotal;
                    string suffix = remaining < 0.0 ? " · ⚠ 固定区段已超出路径" : string.Empty;
                    ZoneTotalText.Text = $"路径：{length:0.###} mm · 固定：{fixedTotal:0.###} mm · 自动剩余：{remaining:0.###} mm · {_viewModel.Zones.Count} 段{suffix}";
                }
                else
                {
                    double diff = length - fixedTotal;
                    ZoneTotalText.Text = $"路径：{length:0.###} mm · 固定区段合计：{fixedTotal:0.###} mm · 差值：{diff:0.###} mm · {_viewModel.Zones.Count} 段";
                }
                return;
            }

            ZoneTotalText.Text = autoCount == 1
                ? $"固定区段：{fixedTotal:0.###} mm · 自动剩余：待预览计算 · {_viewModel.Zones.Count} 段"
                : $"固定区段合计：{fixedTotal:0.###} mm · 路径长度待预览 · {_viewModel.Zones.Count} 段";
        }

        private void RecalculateAutoZoneLength()
        {
            if (!_lastKnownDistributionLengthMm.HasValue) return;
            RebarGradientZoneRowViewModel? auto = _viewModel.Zones.FirstOrDefault(x => x.IsAutoLength);
            if (auto == null) return;

            double fixedTotal = _viewModel.Zones
                .Where(x => !x.IsAutoLength && IsFinite(x.LengthMm))
                .Sum(x => x.LengthMm);
            auto.SetResolvedAutoLength(_lastKnownDistributionLengthMm.Value - fixedTotal);
        }

        private bool ValidateZoneInputs(out string error)
        {
            error = string.Empty;
            if (_viewModel.Zones.Count == 0)
            {
                error = "至少需要一个分布区段。";
                return false;
            }

            int autoCount = _viewModel.Zones.Count(x => x.IsAutoLength);
            if (autoCount > 1)
            {
                error = "最多只能设置一个“自动剩余”区段。";
                return false;
            }

            double fixedTotal = 0.0;
            for (int i = 0; i < _viewModel.Zones.Count; i++)
            {
                RebarGradientZoneRowViewModel row = _viewModel.Zones[i];
                if (!row.IsAutoLength)
                {
                    if (!IsFinitePositive(row.LengthMm))
                    {
                        error = $"第 {i + 1} 个区段长度必须大于 0。";
                        return false;
                    }
                    fixedTotal += row.LengthMm;
                }
                if (!IsFinitePositive(row.MaxSpacingMm))
                {
                    error = $"第 {i + 1} 个区段最大间距必须大于 0。";
                    return false;
                }
                if (row.ManualActualSpacingMm.HasValue &&
                    (!IsFinitePositive(row.ManualActualSpacingMm.Value) || row.ManualActualSpacingMm.Value > row.MaxSpacingMm))
                {
                    error = $"第 {i + 1} 个区段手动实际间距必须 >0 且 <= 最大间距。";
                    return false;
                }
            }

            if (_lastKnownDistributionLengthMm.HasValue && autoCount == 1)
            {
                double remaining = _lastKnownDistributionLengthMm.Value - fixedTotal;
                if (remaining < 0.0)
                {
                    error = $"固定区段合计 {fixedTotal:0.###} mm 已超过有效长度 {_lastKnownDistributionLengthMm.Value:0.###} mm。";
                    return false;
                }
                if (remaining > 0.0 && remaining < 1.0)
                {
                    error = $"自动剩余仅 {remaining:0.###} mm，小于 1 mm。";
                    return false;
                }
            }

            if (_lastKnownDistributionLengthMm.HasValue && autoCount == 0)
            {
                double total = _viewModel.Zones.Where(x => IsFinite(x.LengthMm)).Sum(x => x.LengthMm);
                if (Math.Abs(total - _lastKnownDistributionLengthMm.Value) > 0.5)
                {
                    error = $"固定区段合计 {total:0.###} mm 与有效长度 {_lastKnownDistributionLengthMm.Value:0.###} mm 不一致。";
                    return false;
                }
            }
            return true;
        }

        private bool IsPreviewInputReady(out string reason)
        {
            if (!_startControlId.HasValue)
            {
                reason = "请先拾取起始控制筋。";
                return false;
            }
            if (!_endControlId.HasValue)
            {
                reason = "请先拾取结束控制筋。";
                return false;
            }
            if (string.Equals(_pathKind, "Edge", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(_pathStableReference))
            {
                reason = "请先拾取模型 Edge。";
                return false;
            }
            if (string.Equals(_pathKind, "ModelLine", StringComparison.OrdinalIgnoreCase) && !_pathElementId.HasValue)
            {
                reason = "请先拾取模型线。";
                return false;
            }
            if (!ValidateZoneInputs(out reason)) return false;
            reason = string.Empty;
            return true;
        }

        private void UpdateInputValidation()
        {
            _viewModel.InputValidationText = ValidateZoneInputs(out string error) ? string.Empty : "⚠ " + error;
            RefreshActionButtons();
        }

        private void ZoneGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (MainScrollViewer == null || ZoneGrid == null) return;
            ScrollViewer? inner = ZoneGrid.Template?.FindName("DG_ScrollViewer", ZoneGrid) as ScrollViewer
                ?? FindVisualChild<ScrollViewer>(ZoneGrid);

            bool innerCanScroll = inner != null &&
                (e.Delta < 0 ? inner.VerticalOffset < inner.ScrollableHeight : inner.VerticalOffset > 0);
            if (innerCanScroll) return;

            MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;
                T? nested = FindVisualChild<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }

        private void CommitGridEdits()
        {
            try
            {
                ZoneGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                ZoneGrid.CommitEdit(DataGridEditingUnit.Row, true);
            }
            catch { }
        }

        private void SetBusy(bool busy, string? status = null)
        {
            _viewModel.IsBusy = busy;
            RefreshActionButtons();
            if (!string.IsNullOrWhiteSpace(status))
                _viewModel.StatusText = status;
        }


        private void RefreshActionButtons()
        {
            string reason = string.Empty;
            bool previewReady = !_viewModel.IsBusy && IsPreviewInputReady(out reason);
            if (PreviewButton != null)
            {
                PreviewButton.IsEnabled = previewReady;
                PreviewButton.ToolTip = previewReady ? "计算有效长度、区段实际间距和总根数" : reason;
            }
            if (CreateButton != null) CreateButton.IsEnabled = !_viewModel.IsBusy && _viewModel.CanCreate;
            RefreshPathControls();
        }

        private void RefreshPathControls()
        {
            if (PickPathButton != null)
                PickPathButton.IsEnabled = !_viewModel.IsBusy && !string.Equals(_pathKind, "ZAxis", StringComparison.OrdinalIgnoreCase);
        }

        private void RefreshPathDisplay()
        {
            _viewModel.DirectionText = _startControlId.HasValue && _endControlId.HasValue
                ? $"Rebar {_startControlId.Value} → Rebar {_endControlId.Value}（Zone[0] 从起始侧开始）"
                : "请先选择起始控制筋和结束控制筋";

            _viewModel.EffectiveLengthText = _lastKnownDistributionLengthMm.HasValue
                ? $"{_lastKnownDistributionLengthMm.Value:0.###} mm"
                : "待计算（预览后自动读取）";

            if (string.Equals(_pathKind, "ZAxis", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.PathText = "全局 Z 轴（无需拾取）";
                return;
            }

            if (string.Equals(_pathKind, "ModelLine", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.PathText = _pathElementId.HasValue
                    ? $"模型线 {_pathElementId.Value} · 基准总长 {FormatLength(_pathReferenceLengthMm)}"
                    : "未选择模型线";
                return;
            }

            if (string.Equals(_pathKind, "Edge", StringComparison.OrdinalIgnoreCase))
            {
                _viewModel.PathText = !string.IsNullOrWhiteSpace(_pathStableReference)
                    ? $"模型 Edge · Owner {_pathOwnerId?.ToString() ?? "?"} · 基准总长 {FormatLength(_pathReferenceLengthMm)}"
                    : "未选择模型 Edge";
            }
        }

        private static string FormatLength(double? value)
            => value.HasValue ? $"{value.Value:0.###} mm" : "待读取";


        private bool EnsureBoundDocumentActive()
        {
            Document? active = _uiapp.ActiveUIDocument?.Document;
            if (active != null && active.Equals(_document)) return true;
            ShowInformation($"此窗口绑定到“{_document.Title}”。请先切回该文档再拾取，或关闭窗口后在当前文档重新打开。");
            return false;
        }

        private void HideForPick()
        {
            SetBusy(true, "请在 Revit 视图中完成拾取；Esc 可取消。");
            Hide();
        }

        private void ShowAfterPick()
        {
            Show();
            Activate();
            SetBusy(false);
        }

        private string GetSelectedPathKind()
        {
            if (PathKindCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                return tag;
            return "ZAxis";
        }

        private static string DescribeRebar(Document doc, RevitRebar rebar)
        {
            string typeName = doc.GetElement(rebar.GetTypeId())?.Name ?? "?";
            Element? host = doc.GetElement(rebar.GetHostId());
            string hostText = host == null ? "Host ?" : $"Host {host.Id.GetIdValue()} {host.Name}";
            return $"Rebar {rebar.Id.GetIdValue()} · {typeName} · {hostText} · Positions {rebar.NumberOfBarPositions}";
        }

        private static string FormatZonePlan(RebarGradientSpacingZonePlan plan)
        {
            if (plan.IntervalCount <= 0) return "-";
            if (plan.TailSpacingMm > 1e-6)
                return $"{plan.MainSpacingMm:0.###} × {plan.FullIntervalCount} + {plan.TailSpacingMm:0.###}";
            return $"{plan.MainSpacingMm:0.###} × {plan.IntervalCount}";
        }

        private static string BuildFailureText(RebarGradientSetResult result)
        {
            string code = string.IsNullOrWhiteSpace(result.ErrorCode) ? "ERROR" : result.ErrorCode;
            string detail = string.IsNullOrWhiteSpace(result.Error) ? result.Message : result.Error;
            return $"{code}：{detail}";
        }

        private void ShowInformation(string text)
            => MessageBox.Show(this, text, "渐变钢筋集", MessageBoxButton.OK, MessageBoxImage.Information);

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool IsFinitePositive(double value) => IsFinite(value) && value > 0.0;

        private sealed class StraightModelCurveSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
                => elem is ModelCurve modelCurve && modelCurve.GeometryCurve is Line;

            public bool AllowReference(Reference reference, XYZ position) => true;
        }

        private sealed class StraightEdgeSelectionFilter : ISelectionFilter
        {
            private readonly Document _doc;
            public StraightEdgeSelectionFilter(Document doc) => _doc = doc;

            public bool AllowElement(Element elem) => elem != null;

            public bool AllowReference(Reference reference, XYZ position)
            {
                try
                {
                    Element? owner = _doc.GetElement(reference.ElementId);
                    GeometryObject? geometry = owner?.GetGeometryObjectFromReference(reference);
                    return geometry is Edge edge && edge.AsCurve() is Line;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
