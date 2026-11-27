using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using YangTools.Revit.Core;
using YangTools.Revit.UI.Controls;

namespace YangTools.Revit.UI
{
    public sealed class DwgLayerOptionItem : INotifyPropertyChanged
    {
        public string LayerName { get; set; } = "";
        public int LineCount { get; set; }
        public int PolyLineCount { get; set; }
        public int ThreeDCount { get; set; }
        public int TotalSegments { get; set; }
        public string ThreeDTag { get; set; } = "2D";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class DwgToPipeWindow : YangWindow
    {
        private readonly List<DwgLayerOptionItem> _allLayers;

        public DwgToPipeRequest? Request { get; private set; }

        public DwgToPipeWindow(
            string dwgName,
            IReadOnlyList<DwgLayerSummary> layers,
            IReadOnlyList<PipingSystemType> systemTypes,
            IReadOnlyList<PipeType> pipeTypes,
            IReadOnlyList<Level> levels,
            ElementId defaultLevelId)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);

            DwgInfoText.Text = $"DWG：{dwgName}（共 {layers.Count} 个图层）";

            _allLayers = layers.Select((l, index) => new DwgLayerOptionItem
            {
                LayerName = l.LayerName,
                LineCount = l.LineCount,
                PolyLineCount = l.PolyLineCount,
                ThreeDCount = l.ThreeDCount,
                TotalSegments = l.TotalSegments,
                ThreeDTag = l.ThreeDTag,
                IsSelected = layers.Count == 1 || index == 0
            }).ToList();

            LayerGrid.ItemsSource = _allLayers;

            SystemTypeCombo.ItemsSource = systemTypes;
            SystemTypeCombo.SelectedIndex = 0;

            PipeTypeCombo.ItemsSource = pipeTypes;
            PipeTypeCombo.SelectedIndex = 0;

            var presetDiameters = new[] { "25", "32", "40", "50", "65", "80", "100", "125", "150", "200", "250", "300" };
            DiameterCombo.ItemsSource = presetDiameters;
            DiameterCombo.Text = "100";

            LevelCombo.ItemsSource = levels;
            Level? defaultLevel = levels.FirstOrDefault(l => l.Id.Equals(defaultLevelId)) ?? levels.FirstOrDefault();
            LevelCombo.SelectedItem = defaultLevel;

            UpdateSelectionSummary();
        }

        private void LayerSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string kw = (LayerSearchBox.Text ?? string.Empty).Trim();
            LayerGrid.ItemsSource = string.IsNullOrEmpty(kw)
                ? _allLayers
                : _allLayers.Where(x => x.LayerName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        private IEnumerable<DwgLayerOptionItem> GetVisibleLayers()
        {
            return LayerGrid.ItemsSource as IEnumerable<DwgLayerOptionItem> ?? _allLayers;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (DwgLayerOptionItem item in GetVisibleLayers()) item.IsSelected = true;
            UpdateSelectionSummary();
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (DwgLayerOptionItem item in GetVisibleLayers()) item.IsSelected = false;
            UpdateSelectionSummary();
        }

        private void InvertSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (DwgLayerOptionItem item in GetVisibleLayers()) item.IsSelected = !item.IsSelected;
            UpdateSelectionSummary();
        }

        private void IgnoreDwgZ_Changed(object sender, RoutedEventArgs e)
        {
            if (OffsetLabel == null || ElevationHintText == null) return;
            bool ignoreZ = ChkIgnoreDwgZ.IsChecked == true;
            OffsetLabel.Text = ignoreZ ? "标高偏移 (mm)：" : "Z 附加偏移 (mm)：";
            ElevationHintText.Text = ignoreZ
                ? "已勾选：忽略 DWG 自身 Z 值，全部按「参照标高 + 标高偏移」拍平生成。"
                : "默认直接采用 DWG 图元的实际 Z 坐标（支持 3D 多段线坡度与立管）。";
        }

        private void LayerSelection_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            int selectedLayers = _allLayers.Count(x => x.IsSelected);
            int totalSegments = _allLayers.Where(x => x.IsSelected).Sum(x => x.TotalSegments);
            SelectionSummaryText.Text = selectedLayers == 0
                ? "请至少勾选 1 个图层。"
                : $"已选 {selectedLayers} 个图层，预计处理 {totalSegments} 段线（支持单次 Ctrl+Z 撤销）。";
            RunButton.IsEnabled = selectedLayers > 0;
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            var selected = _allLayers.Where(x => x.IsSelected).Select(x => x.LayerName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "请至少勾选一个 DWG 图层。", "DWG 转管道", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!(SystemTypeCombo.SelectedItem is PipingSystemType sysType) ||
                !(PipeTypeCombo.SelectedItem is PipeType pipeType) ||
                !(LevelCombo.SelectedItem is Level level))
            {
                MessageBox.Show(this, "请选择管道系统、管道类型和参照标高。", "DWG 转管道", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParsePositive(DiameterCombo.Text, out double diameterMm) || diameterMm < 5 || diameterMm > 5000)
            {
                MessageBox.Show(this, "请输入有效的管径数值（5 ~ 5000 mm）。", "DWG 转管道", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse((OffsetBox.Text ?? "0").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetMm))
            {
                MessageBox.Show(this, "请输入有效的中心偏移数值（mm）。", "DWG 转管道", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParsePositive(ToleranceBox.Text, out double toleranceMm)) toleranceMm = 20.0;
            if (!TryParsePositive(MinLengthBox.Text, out double minLengthMm)) minLengthMm = 50.0;

            Request = new DwgToPipeRequest
            {
                SelectedLayers = selected,
                PipingSystemTypeId = sysType.Id,
                PipeTypeId = pipeType.Id,
                LevelId = level.Id,
                DiameterMm = diameterMm,
                IgnoreDwgZ = ChkIgnoreDwgZ.IsChecked == true,
                OffsetMm = offsetMm,
                ConnectPolyLineCorners = ChkConnectPoly.IsChecked == true,
                ConnectSharedEndpoints = ChkConnectEndpoints.IsChecked == true,
                MergeCollinearSegments = ChkMergeCollinear.IsChecked == true,
                ConnectToleranceMm = toleranceMm,
                MinSegmentLengthMm = minLengthMm
            };

            DialogResult = true;
        }

        private static bool TryParsePositive(string? text, out double value)
        {
            return double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0;
        }
    }
}
