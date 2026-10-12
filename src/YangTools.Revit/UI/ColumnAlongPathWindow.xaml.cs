using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public partial class ColumnAlongPathWindow : Window
    {
        private readonly UIApplication _uiapp;
        private readonly Document _doc;
        private readonly UserSettings _settings;
        private readonly ColumnAlongPathViewModel _viewModel;
        private readonly RevitEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private bool _isClosed;
        private long _requestVersion;

        public ColumnAlongPathWindow(UIApplication uiapp)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);

            _uiapp = uiapp ?? throw new ArgumentNullException(nameof(uiapp));
            _doc = uiapp.ActiveUIDocument?.Document
                ?? throw new InvalidOperationException("请先打开一个项目文档。");
            _settings = UserSettings.Load();
            _viewModel = new ColumnAlongPathViewModel();
            ApplySavedSettings();
            DataContext = _viewModel;

            _handler = new RevitEventHandler(
                _doc,
                wrapInTransactionGroup: false,
                showAutomaticSuccess: false);
            _externalEvent = ExternalEvent.Create(_handler);

            LoadColumnTypesAndLevels();
        }

        private void LoadColumnTypesAndLevels()
        {
            _handler.SetAction(app =>
            {
                if (_isClosed) return;
                Document doc = app.ActiveUIDocument.Document;

                var columnTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(IsSupportedColumnSymbol)
                    .Select(symbol => new ColumnTypeOption
                    {
                        ElementIdValue = symbol.Id.GetIdValue(),
                        FamilyName = symbol.FamilyName,
                        TypeName = symbol.Name,
                        IsStructural = symbol.Category != null
                            && symbol.Category.Id.GetIdValue() == (long)BuiltInCategory.OST_StructuralColumns
                    })
                    .OrderBy(option => option.IsStructural ? 0 : 1)
                    .ThenBy(option => option.FamilyName)
                    .ThenBy(option => option.TypeName)
                    .ToList();

                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(level => level.ProjectElevation)
                    .Select(level => new LevelOption
                    {
                        ElementIdValue = level.Id.GetIdValue(),
                        Name = level.Name,
                        ProjectElevation = level.ProjectElevation
                    })
                    .ToList();

                long? activeLevelId = app.ActiveUIDocument.ActiveView.GenLevel?.Id.GetIdValue();

                Dispatcher.Invoke(() =>
                {
                    if (_isClosed) return;
                    _viewModel.AvailableColumnTypes = columnTypes;
                    _viewModel.AvailableLevels = levels;

                    ColumnTypeOption? savedColumnType = columnTypes.FirstOrDefault(option =>
                        option.FamilyName.Equals(_settings.ColumnAlongPathFamilyName, StringComparison.OrdinalIgnoreCase)
                        && option.TypeName.Equals(_settings.ColumnAlongPathTypeName, StringComparison.OrdinalIgnoreCase));
                    bool hasSavedColumnType = !string.IsNullOrWhiteSpace(_settings.ColumnAlongPathFamilyName)
                        || !string.IsNullOrWhiteSpace(_settings.ColumnAlongPathTypeName);
                    _viewModel.SelectedColumnType = hasSavedColumnType
                        ? savedColumnType
                        : columnTypes.FirstOrDefault();
                    if (hasSavedColumnType && savedColumnType == null)
                        _viewModel.PathInfoText = "保存的柱类型已失效，请重新选择；不会自动更换柱类型。";

                    bool hasSavedBaseLevel = !string.IsNullOrWhiteSpace(_settings.ColumnAlongPathBaseLevelName);
                    LevelOption? baseLevel = hasSavedBaseLevel
                        ? levels.FirstOrDefault(level => level.Name.Equals(
                            _settings.ColumnAlongPathBaseLevelName, StringComparison.OrdinalIgnoreCase))
                        : null;
                    if (!hasSavedBaseLevel && baseLevel == null && activeLevelId.HasValue)
                        baseLevel = levels.FirstOrDefault(level => level.ElementIdValue == activeLevelId.Value);
                    if (!hasSavedBaseLevel)
                        baseLevel ??= levels.FirstOrDefault();
                    _viewModel.SelectedBaseLevel = baseLevel;
                    if (hasSavedBaseLevel && baseLevel == null)
                        _viewModel.PathInfoText = "保存的 Base Level 已失效，请重新选择。";

                    bool hasSavedTopLevel = !string.IsNullOrWhiteSpace(_settings.ColumnAlongPathTopLevelName);
                    LevelOption? topLevel = hasSavedTopLevel
                        ? levels.FirstOrDefault(level => level.Name.Equals(
                            _settings.ColumnAlongPathTopLevelName, StringComparison.OrdinalIgnoreCase))
                        : null;
                    if (!hasSavedTopLevel && topLevel == null && baseLevel != null)
                        topLevel = levels.FirstOrDefault(level => level.ProjectElevation > baseLevel.ProjectElevation + 1e-6);
                    if (!hasSavedTopLevel)
                        topLevel ??= levels.LastOrDefault();
                    _viewModel.SelectedTopLevel = topLevel;
                    if (hasSavedTopLevel && topLevel == null)
                        _viewModel.PathInfoText = "保存的 Top Level 已失效，请重新选择。";
                });
            }, "加载柱类型与标高", showSuccess: false);

            _externalEvent.Raise();
        }

        private static bool IsSupportedColumnSymbol(FamilySymbol symbol)
        {
            if (symbol.Category == null)
                return false;

            long categoryId = symbol.Category.Id.GetIdValue();
            if (categoryId != (long)BuiltInCategory.OST_StructuralColumns
                && categoryId != (long)BuiltInCategory.OST_Columns)
            {
                return false;
            }

            FamilyPlacementType placementType = symbol.Family.FamilyPlacementType;
            return placementType == FamilyPlacementType.OneLevelBased
                || placementType == FamilyPlacementType.TwoLevelsBased;
        }

        private void ApplySavedSettings()
        {
            _viewModel.SpacingMm = _settings.ColumnAlongPathSpacingMm;
            _viewModel.StartOffsetMm = _settings.ColumnAlongPathStartOffsetMm;
            _viewModel.RouteStartMileageM = _settings.ColumnAlongPathStartMileageM;
            _viewModel.RotationOffsetDegrees = _settings.ColumnAlongPathRotationOffsetDegrees;
            _viewModel.BaseOffsetMm = _settings.ColumnAlongPathBaseOffsetMm;
            _viewModel.TopOffsetMm = _settings.ColumnAlongPathTopOffsetMm;
            _viewModel.ReversePath = _settings.ColumnAlongPathReversePath;
            _viewModel.IncludeEndColumn = _settings.ColumnAlongPathIncludeEndColumn;
        }

        private void SaveCurrentSettings()
        {
            _settings.ColumnAlongPathSpacingMm = _viewModel.SpacingMm;
            _settings.ColumnAlongPathStartOffsetMm = _viewModel.StartOffsetMm;
            _settings.ColumnAlongPathStartMileageM = _viewModel.RouteStartMileageM;
            _settings.ColumnAlongPathRotationOffsetDegrees = _viewModel.RotationOffsetDegrees;
            _settings.ColumnAlongPathBaseOffsetMm = _viewModel.BaseOffsetMm;
            _settings.ColumnAlongPathTopOffsetMm = _viewModel.TopOffsetMm;
            _settings.ColumnAlongPathReversePath = _viewModel.ReversePath;
            _settings.ColumnAlongPathIncludeEndColumn = _viewModel.IncludeEndColumn;
            // Keep a saved (but now unresolved) selection until the user explicitly chooses a new one.
            if (_viewModel.SelectedColumnType != null)
            {
                _settings.ColumnAlongPathFamilyName = _viewModel.SelectedColumnType.FamilyName;
                _settings.ColumnAlongPathTypeName = _viewModel.SelectedColumnType.TypeName;
            }
            if (_viewModel.SelectedBaseLevel != null)
                _settings.ColumnAlongPathBaseLevelName = _viewModel.SelectedBaseLevel.Name;
            if (_viewModel.SelectedTopLevel != null)
                _settings.ColumnAlongPathTopLevelName = _viewModel.SelectedTopLevel.Name;
            _settings.Save();
        }

        private void PickRoute_Click(object sender, RoutedEventArgs e)
        {
            long requestVersion = ++_requestVersion;
            _handler.SetAction(app =>
            {
                if (_isClosed || requestVersion != _requestVersion) return;
                Dispatcher.Invoke(Hide);
                try
                {
                    var engine = new ColumnAlongPathEngine(app.ActiveUIDocument);
                    List<Curve>? curves = engine.PickContinuousModelLines();
                    if (curves != null && curves.Count > 0)
                    {
                        double totalLengthMm = curves.Sum(curve => curve.Length) * 304.8;
                        Dispatcher.Invoke(() =>
                        {
                            if (_isClosed || requestVersion != _requestVersion) return;
                            _viewModel.ChainedCurves = curves;
                            _viewModel.TotalLengthDisplay = $"总长：{totalLengthMm:F0} mm";
                            _viewModel.PathInfoText = $"已连接 {curves.Count} 段模型线。默认首端依据首条所选模型线确定，可用“反转路线首尾”切换。";
                        });
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User cancelled selection.
                }
                catch (Exception ex)
                {
                    DiagnosticLog.WriteError("选择沿线布柱路线", ex);
                    Dispatcher.Invoke(() =>
                    {
                        if (!_isClosed) TaskDialog.Show("路线无效", ex.Message);
                    });
                }
                finally
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_isClosed) return;
                        Show();
                        Activate();
                    });
                }
            }, "选择沿线布柱路线", showSuccess: false);

            _externalEvent.Raise();
        }

        private void StartPlacement_Click(object sender, RoutedEventArgs e)
        {
            ColumnAlongPathPlacementRequest request;
            try
            {
                if (HasInvalidInput(this))
                    throw new InvalidOperationException("输入中有无效数值，请修正红框中的数值后再布置。");
                request = ColumnAlongPathPlacementRequest.Capture(_viewModel);
                SaveCurrentSettings();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("沿线布柱", ex.Message);
                return;
            }

            long requestVersion = ++_requestVersion;
            _handler.SetAction(app =>
            {
                if (_isClosed || requestVersion != _requestVersion) return;
                var engine = new ColumnAlongPathEngine(app.ActiveUIDocument);
                // A committed result is returned before any potentially failing UI operation.
                ColumnAlongPathPlacementResult result = engine.ExecutePlacement(request);
                try
                {
                    app.ActiveUIDocument.Selection.SetElementIds(result.CreatedIds.ToList());
                    app.ActiveUIDocument.RefreshActiveView();
                    if (!_isClosed && !result.ReadbackConfirmed)
                        TaskDialog.Show("沿线布柱已提交", result.ReadbackWarning);
                    else if (!_isClosed)
                    {
                        TaskDialog.Show("沿线布置柱",
                            $"已布置 {result.CreatedCount} 根柱。\n" +
                            $"间距：{result.SpacingMm:F0} mm\n" +
                            $"旋转偏移：{result.RotationOffsetDegrees:F2}°\n" +
                            $"首柱里程：{ColumnAlongPathEngine.FormatStation(result.FirstStationM)}\n" +
                            $"末柱里程：{ColumnAlongPathEngine.FormatStation(result.LastStationM)}\n" +
                            "本次操作可通过一次 Ctrl+Z 撤销。");
                    }
                }
                catch (Exception uiError)
                {
                    DiagnosticLog.WriteError("沿线布柱已提交但 UI 更新失败", uiError);
                    if (!_isClosed)
                        TaskDialog.Show("沿线布柱已提交",
                            $"{result.CreatedCount} 根柱已提交到模型，但选择或刷新界面失败：{uiError.Message}。请检查模型，不要直接重复布置。");
                }
            }, "沿线布置柱", showSuccess: false);
            _externalEvent.Raise();
        }

        // Invalid WPF double bindings retain the previous ViewModel value. Reject these
        // instead of silently placing columns with stale numeric settings.
        private static bool HasInvalidInput(DependencyObject node)
        {
            if (node is TextBox textbox && Validation.GetHasError(textbox))
                return true;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                if (HasInvalidInput(VisualTreeHelper.GetChild(node, i)))
                    return true;
            }
            return false;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSettings();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            ++_requestVersion;
            SaveCurrentSettings();
            base.OnClosed(e);
        }
    }
}
