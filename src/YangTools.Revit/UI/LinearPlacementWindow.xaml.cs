using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public partial class LinearPlacementWindow : Window
    {
        private readonly UIApplication _uiapp;
        private LinearPlacementViewModel _viewModel;
        private readonly UserSettings _settings;
        private readonly Document _doc;
        private bool _isClosed;
        private long _placementVersion;
        private long _pathVersion;
        
        private RevitEventHandler _handler;
        private ExternalEvent _externalEvent;

        public LinearPlacementWindow(UIApplication uiapp)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument?.Document
                ?? throw new InvalidOperationException("没有活动 Revit 文档。");
            _settings = UserSettings.Load();

            _handler = new RevitEventHandler(_doc,
                wrapInTransactionGroup: false, showAutomaticSuccess: false);
            _externalEvent = ExternalEvent.Create(_handler);

            _viewModel = new LinearPlacementViewModel();
            ApplySavedSettings();
            DataContext = _viewModel;

            LoadFamilySymbols();
        }

        private void LoadFamilySymbols()
        {
            _handler.SetAction(app =>
            {
                if (_isClosed) return;
                var doc = _doc;
                var symbols = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .OrderBy(x => x.FamilyName)
                    .ThenBy(x => x.Name)
                    .ToList();

                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(x => x.ProjectElevation)
                    .ThenBy(x => x.Name)
                    .ToList();
                ElementId activeLevelId = app.ActiveUIDocument.ActiveView.GenLevel?.Id ?? ElementId.InvalidElementId;

                Dispatcher.Invoke(() =>
                {
                    if (_isClosed) return;
                    _viewModel.AvailableSymbols = symbols;
                    if (symbols.Any())
                    {
                        FamilySymbol? savedSymbol = symbols.FirstOrDefault(symbol =>
                            symbol.FamilyName.Equals(_settings.LinearFamilyName, StringComparison.OrdinalIgnoreCase) &&
                            symbol.Name.Equals(_settings.LinearTypeName, StringComparison.OrdinalIgnoreCase));
                        bool hasSavedSymbol = !string.IsNullOrWhiteSpace(_settings.LinearFamilyName) ||
                                              !string.IsNullOrWhiteSpace(_settings.LinearTypeName);
                        _viewModel.SelectedSymbol = hasSavedSymbol ? savedSymbol : symbols.FirstOrDefault();
                        if (hasSavedSymbol && savedSymbol == null)
                            _viewModel.PathInfoText = "之前保存的族类型已失效，请重新选择；不会自动换成其他族类型。";
                    }

                    _viewModel.AvailableLevels = levels;
                    if (levels.Any())
                    {
                        Level? savedLevel = levels.FirstOrDefault(level =>
                            level.Name.Equals(_settings.LinearLevelName, StringComparison.OrdinalIgnoreCase));
                        Level? activeLevel = activeLevelId != ElementId.InvalidElementId
                            ? levels.FirstOrDefault(level => level.Id == activeLevelId)
                            : null;
                        bool hasSavedLevel = !string.IsNullOrWhiteSpace(_settings.LinearLevelName);
                        _viewModel.SelectedLevel = hasSavedLevel
                            ? savedLevel
                            : activeLevel ?? levels.FirstOrDefault();
                        if (hasSavedLevel && savedLevel == null)
                            _viewModel.PathInfoText = "之前保存的标高已失效，请重新选择；不会自动换成其他标高。";
                    }
                });
                    }, "加载族类型", showSuccess: false);
            _externalEvent.Raise();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        private void ApplySavedSettings()
        {
            // Set mode flags first because their setters provide defaults; restore numeric values afterwards.
            _viewModel.IsByDistance = _settings.LinearIsByDistance;
            _viewModel.IsBySpacing = _settings.LinearIsBySpacing;
            _viewModel.StartValue = _settings.LinearStartValue;
            _viewModel.EndValue = _settings.LinearEndValue;
            _viewModel.StepValue = _settings.LinearStepValue;
            _viewModel.AlignToCurve = _settings.LinearAlignToCurve;
            _viewModel.KeepVertical = _settings.LinearKeepVertical;
            _viewModel.RotationOffset = _settings.LinearRotationOffset;
            _viewModel.ReversePath = _settings.LinearReversePath;
            _viewModel.IncludeRemainderEndpoint = _settings.LinearIncludeRemainderEndpoint;
            _viewModel.StartElevationMm = _settings.LinearStartElevationMm;
            _viewModel.EndElevationMm = _settings.LinearEndElevationMm;
        }

        private void SaveCurrentSettings()
        {
            _settings.LinearIsByDistance = _viewModel.IsByDistance;
            _settings.LinearIsBySpacing = _viewModel.IsBySpacing;
            _settings.LinearStartValue = _viewModel.StartValue;
            _settings.LinearEndValue = _viewModel.EndValue;
            _settings.LinearStepValue = _viewModel.StepValue;
            _settings.LinearAlignToCurve = _viewModel.AlignToCurve;
            _settings.LinearKeepVertical = _viewModel.KeepVertical;
            _settings.LinearRotationOffset = _viewModel.RotationOffset;
            _settings.LinearReversePath = _viewModel.ReversePath;
            _settings.LinearIncludeRemainderEndpoint = _viewModel.IncludeRemainderEndpoint;
            _settings.LinearStartElevationMm = _viewModel.StartElevationMm;
            _settings.LinearEndElevationMm = _viewModel.EndElevationMm;
            if (_viewModel.SelectedLevel != null)
                _settings.LinearLevelName = _viewModel.SelectedLevel.Name;

            if (_viewModel.SelectedSymbol != null)
            {
                _settings.LinearFamilyName = _viewModel.SelectedSymbol.FamilyName;
                _settings.LinearTypeName = _viewModel.SelectedSymbol.Name;
            }

            _settings.Save();
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            ++_placementVersion;
            ++_pathVersion;
            SaveCurrentSettings();
            base.OnClosed(e);
        }

        private static bool HasInvalidInput(DependencyObject parent)
        {
            if (parent is TextBox textbox && Validation.GetHasError(textbox)) return true;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
                if (HasInvalidInput(System.Windows.Media.VisualTreeHelper.GetChild(parent, i))) return true;
            return false;
        }

        private void PickPath_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosed) return;
            long version = ++_pathVersion;

            // The bound ExternalEvent may be skipped after a document switch.
            // Hide only after RevitEventHandler has validated the original document;
            // otherwise a rejected event could leave this modeless window hidden forever.
            _handler.SetAction(app =>
            {
                if (_isClosed || version != _pathVersion) return;
                try
                {
                    Hide();
                    var engine = new LinearPlacementEngine(app.ActiveUIDocument);
                    var curves = engine.PickAndExtractCurves();
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (_isClosed || version != _pathVersion) return;
                        if (curves != null && curves.Count > 0)
                        {
                            _viewModel.ChainedCurves = curves;
                            double totalLenMm = curves.Sum(c => c.Length) * 304.8;
                            _viewModel.TotalLengthDisplay = $"总长: {totalLenMm:F0} mm";
                            _viewModel.PathInfoText = $"已拾取 {curves.Count} 段曲线, 总长 {totalLenMm:F0} mm";
                        }
                    });
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    if (!_isClosed && version == _pathVersion)
                        Dispatcher.Invoke(() => TaskDialog.Show("错误", "拾取发生错误: " + ex.Message));
                }
                finally
                {
                    if (!_isClosed && version == _pathVersion)
                        Dispatcher.Invoke(() => { Show(); Activate(); });
                }
                }, "拾取路线", showSuccess: false);
            _externalEvent.Raise();
        }

        private void StartPlacement_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosed) return;
            if (HasInvalidInput(this))
            {
                TaskDialog.Show("提示", "输入框存在未通过验证的数值，请先修正后再布置。");
                return;
            }

            LinearPlacementRequest request;
            try
            {
                request = LinearPlacementRequest.Capture(_viewModel);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("提示", ex.Message);
                return;
            }

            // Freeze the click-time values and invalidate any previous queued placements.
            long version = ++_placementVersion;
            SaveCurrentSettings();
            _handler.SetAction(app =>
            {
                if (_isClosed || version != _placementVersion) return;
                try
                {
                    var engine = new LinearPlacementEngine(app.ActiveUIDocument);
                    LinearPlacementResult result = engine.ExecutePlacement(request);
                    // A committed transaction may not be undone by later UI or readback failure.
                    if (!_isClosed && version == _placementVersion)
                    {
                        string message = result.ReadbackConfirmed
                            ? $"已布置 {result.CreatedCount} 个实例，提交及类型回读成功。"
                            : $"已提交 {result.CreatedCount} 个实例，但回读无法确认：{result.ReadbackWarning}，请检查模型后再执行。";
                        try { TaskDialog.Show("线性布置结果", message); }
                        catch (Exception uiError)
                        {
                            DiagnosticLog.WriteInfo("线性布置", "模型已提交，但结果界面显示失败：" + uiError.Message);
                        }
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                catch (Exception ex)
                {
                    if (!_isClosed && version == _placementVersion)
                        TaskDialog.Show("线性布置结果", ex.Message);
                }
            }, "线性布置", showSuccess: false);
            _externalEvent.Raise();
        }
    }
}
