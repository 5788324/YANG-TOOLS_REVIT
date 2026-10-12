using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Microsoft.Win32;
using YangTools.Revit.Core;
using YangTools.Revit.Core.CadToRevit;

namespace YangTools.Revit.UI
{
    public partial class CadToRevitWindow : Window
    {
        private readonly Document _document;
        private readonly View _view;
        private readonly CadToRevitTypeCatalog _catalog;
        private CadRevitExchangeDocument? _exchange;

        public ObservableCollection<CadToRevitMappingRow> TextMappings { get; } = new ObservableCollection<CadToRevitMappingRow>();
        public ObservableCollection<CadToRevitMappingRow> LineMappings { get; } = new ObservableCollection<CadToRevitMappingRow>();
        public ObservableCollection<CadToRevitMappingRow> FillMappings { get; } = new ObservableCollection<CadToRevitMappingRow>();
        public ObservableCollection<CadToRevitMappingRow> DimensionMappings { get; } = new ObservableCollection<CadToRevitMappingRow>();

        public CadToRevitRebuildRequest? Request { get; private set; }

        public CadToRevitWindow(Document document, View view)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _catalog = new CadToRevitTypeCatalog(_document);

            TxtView.Text = $"当前视图：{_view.Name}";
            TextGrid.ItemsSource = TextMappings;
            LineGrid.ItemsSource = LineMappings;
            FillGrid.ItemsSource = FillMappings;
            DimensionGrid.ItemsSource = DimensionMappings;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择 YangCAD RVT Exchange JSON",
                Filter = "YangCAD / JSON (*.json;*.rvt.json)|*.json;*.rvt.json|所有文件 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true) return;
            TxtSourcePath.Text = dialog.FileName;
            Scan(dialog.FileName);
        }

        private void Rescan_Click(object sender, RoutedEventArgs e)
        {
            string path = TxtSourcePath.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                MessageBox.Show("请先选择有效的 YangCAD 交换文件。", "CAD 转 Revit 图纸", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Scan(path);
        }

        private void Scan(string path)
        {
            try
            {
                _exchange = CadRevitExchangeReader.Read(path);
                BuildMappings();
                TxtSummary.Text = $"识别：文字 {_exchange.Texts.Count} / 线条 {_exchange.Curves.Count} / 填充 {_exchange.Fills.Count} / 标注 {_exchange.Dimensions.Count}";
                TxtSourceInfo.Text = $"单位：{_exchange.Units}；Schema：{(string.IsNullOrWhiteSpace(_exchange.SchemaVersion) ? "未声明" : _exchange.SchemaVersion)}；基点：({_exchange.Origin.X:0.###}, {_exchange.Origin.Y:0.###})" +
                                     (_exchange.Warnings.Count > 0 ? $"；解析警告 {_exchange.Warnings.Count} 条" : string.Empty);
                UpdateMappingStatus();
            }
            catch (Exception ex)
            {
                _exchange = null;
                TextMappings.Clear(); LineMappings.Clear(); FillMappings.Clear(); DimensionMappings.Clear();
                BtnConvert.IsEnabled = false;
                TxtSummary.Text = "扫描失败";
                TxtSourceInfo.Text = ex.Message;
                MessageBox.Show(ex.Message, "CAD 转 Revit 图纸", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BuildMappings()
        {
            if (_exchange == null) return;
            CadToRevitMappingFile saved = CadToRevitMappingStore.Load();
            TextMappings.ReplaceWith(BuildTextRows(_exchange, saved));
            LineMappings.ReplaceWith(BuildLineRows(_exchange, saved));
            FillMappings.ReplaceWith(BuildFillRows(_exchange, saved));
            DimensionMappings.ReplaceWith(BuildDimensionRows(_exchange, saved));
        }

        private IEnumerable<CadToRevitMappingRow> BuildTextRows(CadRevitExchangeDocument exchange, CadToRevitMappingFile saved)
        {
            foreach (IGrouping<string, CadTextItem> group in exchange.Texts.GroupBy(x => x.StyleKey, StringComparer.CurrentCultureIgnoreCase).OrderBy(x => x.Key))
            {
                CadTextItem sample = group.First();
                string info = string.Join(" · ", new[]
                {
                    sample.Height > 0 ? $"H={sample.Height:0.###}{exchange.Units}" : string.Empty,
                    sample.FontName
                }.Where(x => !string.IsNullOrWhiteSpace(x)));
                yield return CreateRow(CadRevitObjectKind.Text, group.Key, group.Count(), info, saved);
            }
        }

        private IEnumerable<CadToRevitMappingRow> BuildLineRows(CadRevitExchangeDocument exchange, CadToRevitMappingFile saved)
        {
            foreach (IGrouping<string, CadCurveItem> group in exchange.Curves.GroupBy(x => x.StyleKey, StringComparer.CurrentCultureIgnoreCase).OrderBy(x => x.Key))
            {
                CadCurveItem sample = group.First();
                string info = (sample.LineWeightMm > 0 ? $"{sample.LineWeightMm:0.##}mm" : "线宽? ") +
                              (string.IsNullOrWhiteSpace(sample.LineTypeName) ? string.Empty : " · " + sample.LineTypeName);
                yield return CreateRow(CadRevitObjectKind.Curve, group.Key, group.Count(), info, saved);
            }
        }

        private IEnumerable<CadToRevitMappingRow> BuildFillRows(CadRevitExchangeDocument exchange, CadToRevitMappingFile saved)
        {
            foreach (IGrouping<string, CadFillItem> group in exchange.Fills.GroupBy(x => x.StyleKey, StringComparer.CurrentCultureIgnoreCase).OrderBy(x => x.Key))
                yield return CreateRow(CadRevitObjectKind.Fill, group.Key, group.Count(), group.First().PatternName, saved);
        }

        private IEnumerable<CadToRevitMappingRow> BuildDimensionRows(CadRevitExchangeDocument exchange, CadToRevitMappingFile saved)
        {
            foreach (IGrouping<string, CadDimensionItem> group in exchange.Dimensions.GroupBy(x => x.StyleKey, StringComparer.CurrentCultureIgnoreCase).OrderBy(x => x.Key))
            {
                int valid = group.Count(x => x.DimensionLine != null && x.ReferenceLines.Count >= 2);
                yield return CreateRow(CadRevitObjectKind.Dimension, group.Key, group.Count(), $"可直接重建 {valid}/{group.Count()}", saved);
            }
        }

        private CadToRevitMappingRow CreateRow(CadRevitObjectKind kind, string sourceKey, int count, string extraInfo, CadToRevitMappingFile saved)
        {
            List<string> targets = _catalog.GetNames(kind);
            string suggested = _catalog.SuggestTarget(kind, sourceKey);
            var row = new CadToRevitMappingRow
            {
                Kind = kind,
                SourceKey = sourceKey,
                Count = count,
                ExtraInfo = extraInfo,
                TargetOptions = targets,
                SuggestedTargetName = suggested,
                NewTypeName = CadToRevitTypeCatalog.DefaultNewTypeName(kind, sourceKey)
            };

            if (CadToRevitMappingStore.TryGet(saved, kind, sourceKey, out CadToRevitMappingSnapshot snapshot))
            {
                row.Action = snapshot.Action;
                row.TargetName = snapshot.TargetName;
                if (!string.IsNullOrWhiteSpace(snapshot.NewTypeName)) row.NewTypeName = snapshot.NewTypeName;
            }
            else if (!string.IsNullOrWhiteSpace(suggested))
            {
                row.Action = CadToRevitMappingActions.UseExisting;
                row.TargetName = suggested;
            }
            else if (kind == CadRevitObjectKind.Curve)
            {
                row.Action = CadToRevitMappingActions.AutoCreate;
                row.TargetName = targets.FirstOrDefault() ?? string.Empty;
            }
            else
            {
                // 文字、填充和标注标准可能与 CAD 差异很大，不猜；用户可明确选择类型/复制/新建/跳过。
                row.Action = CadToRevitMappingActions.Skip;
                row.TargetName = targets.FirstOrDefault() ?? string.Empty;
            }
            return row;
        }

        private void Mapping_Changed(object sender, SelectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(UpdateMappingStatus));
        }


        private void MappingGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(UpdateMappingStatus));
        }

        private void UpdateMappingStatus()
        {
            List<CadToRevitMappingRow> all = GetAllRows().ToList();
            int invalid = all.Count(IsIncomplete);
            int skipped = all.Count(x => x.Action == CadToRevitMappingActions.Skip);
            TxtMappingStatus.Text = invalid == 0
                ? $"映射完成 {all.Count - skipped}/{all.Count}（跳过 {skipped}）"
                : $"待完善 {invalid} 项 · 跳过 {skipped} 项";
            TxtMappingStatus.Foreground = FindResource(invalid == 0 ? "SuccessColor" : "WarningColor") as System.Windows.Media.Brush;
            BtnConvert.IsEnabled = _exchange != null && invalid == 0 && all.Any(x => x.Action != CadToRevitMappingActions.Skip);
        }

        private static bool IsIncomplete(CadToRevitMappingRow row)
        {
            if (row.Action == CadToRevitMappingActions.Skip) return false;
            if (row.Action == CadToRevitMappingActions.UseExisting || row.Action == CadToRevitMappingActions.CadExplodedReference)
                return string.IsNullOrWhiteSpace(row.TargetName) || !row.TargetOptions.Any(x => string.Equals(x, row.TargetName, StringComparison.CurrentCultureIgnoreCase));
            if (row.Action == CadToRevitMappingActions.CopyExisting)
                return string.IsNullOrWhiteSpace(row.TargetName) || string.IsNullOrWhiteSpace(row.NewTypeName) ||
                       !row.TargetOptions.Any(x => string.Equals(x, row.TargetName, StringComparison.CurrentCultureIgnoreCase));
            if (row.Action == CadToRevitMappingActions.AutoCreate)
            {
                if (row.Kind == CadRevitObjectKind.Curve)
                    return string.IsNullOrWhiteSpace(row.NewTypeName);
                return string.IsNullOrWhiteSpace(row.TargetName) || string.IsNullOrWhiteSpace(row.NewTypeName) ||
                       !row.TargetOptions.Any(x => string.Equals(x, row.TargetName, StringComparison.CurrentCultureIgnoreCase));
            }
            return true;
        }

        private IEnumerable<CadToRevitMappingRow> GetAllRows()
        {
            return TextMappings.Concat(LineMappings).Concat(FillMappings).Concat(DimensionMappings);
        }

        private void SaveMappings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CommitPendingGridEdits();
                CadToRevitMappingStore.Save(GetAllRows());
                MessageBox.Show($"映射已保存到：\n{CadToRevitMappingStore.MappingPath}", "CAD 转 Revit 图纸", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存映射失败：" + ex.Message, "CAD 转 Revit 图纸", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Convert_Click(object sender, RoutedEventArgs e)
        {
            CommitPendingGridEdits();
            UpdateMappingStatus();
            if (_exchange == null) return;
            CadToRevitMappingRow? invalid = GetAllRows().FirstOrDefault(IsIncomplete);
            if (invalid != null)
            {
                MessageBox.Show($"请完善“{invalid.SourceKey}”的 Revit 映射，或明确选择“跳过”。", "CAD 转 Revit 图纸", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string dimLineStyle = (TxtDimensionReferenceStyle.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(dimLineStyle))
            {
                MessageBox.Show("请输入尺寸界线专用 Revit 线样式名称。", "CAD 转 Revit 图纸", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try { CadToRevitMappingStore.Save(GetAllRows()); }
            catch (Exception ex) { DiagnosticLog.WriteError("保存 CAD 转 Revit 映射", ex); }

            Request = new CadToRevitRebuildRequest
            {
                Exchange = _exchange,
                TextMappings = TextMappings.ToList(),
                LineMappings = LineMappings.ToList(),
                FillMappings = FillMappings.ToList(),
                DimensionMappings = DimensionMappings.ToList(),
                DimensionReferenceLineStyleName = dimLineStyle
            };
            DialogResult = true;
        }

        private void CommitPendingGridEdits()
        {
            foreach (DataGrid grid in new[] { TextGrid, LineGrid, FillGrid, DimensionGrid })
            {
                grid.CommitEdit(DataGridEditingUnit.Cell, true);
                grid.CommitEdit(DataGridEditingUnit.Row, true);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    internal static class ObservableCollectionExtensions
    {
        public static void ReplaceWith<T>(this ObservableCollection<T> collection, IEnumerable<T> values)
        {
            collection.Clear();
            foreach (T value in values) collection.Add(value);
        }
    }
}
