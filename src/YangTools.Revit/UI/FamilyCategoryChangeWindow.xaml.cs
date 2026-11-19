using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public partial class FamilyCategoryChangeWindow : Window
    {
        private readonly ObservableCollection<FamilyCategoryRow> _rows;
        private readonly ICollectionView _view;
        private bool _updatingSelection;

        internal FamilyCategoryChangeRequest Request { get; private set; }

        public FamilyCategoryChangeWindow(Document document)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);

            _rows = new ObservableCollection<FamilyCategoryRow>(BuildRows(document));
            FamilyGrid.ItemsSource = _rows;
            _view = CollectionViewSource.GetDefaultView(_rows);
            _view.Filter = FilterRow;

            TargetCategoryBox.ItemsSource = BuildTargetCategories(document);
            SelectDefaultTarget();

            foreach (FamilyCategoryRow row in _rows)
                row.PropertyChanged += Row_PropertyChanged;
            UpdateSelectionStatus();
        }

        private static IEnumerable<FamilyCategoryRow> BuildRows(Document document)
        {
            var instanceCounts = new Dictionary<long, int>();
            foreach (FamilyInstance instance in new FilteredElementCollector(document)
                         .OfClass(typeof(FamilyInstance))
                         .Cast<FamilyInstance>())
            {
                Family family = instance.Symbol?.Family;
                if (family == null) continue;
                long key = family.Id.GetIdValue();
                instanceCounts.TryGetValue(key, out int count);
                instanceCounts[key] = count + 1;
            }

            return new FilteredElementCollector(document)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(family =>
                {
                    bool editable = family.IsEditable && !family.IsInPlace;
                    return new FamilyCategoryRow
                    {
                        FamilyId = family.Id,
                        FamilyName = family.Name,
                        CurrentCategory = family.FamilyCategory?.Name ?? "未分类",
                        CurrentCategoryId = family.FamilyCategory?.Id ?? ElementId.InvalidElementId,
                        TypeCount = family.GetFamilySymbolIds().Count,
                        InstanceCount = instanceCounts.TryGetValue(family.Id.GetIdValue(), out int count) ? count : 0,
                        IsEditable = editable,
                        IsSelected = false,
                        StatusText = editable ? "可修改" : (family.IsInPlace ? "内建族" : "不可编辑")
                    };
                })
                .ToList();
        }

        private static IList<FamilyCategoryTargetItem> BuildTargetCategories(Document document)
        {
            var result = new List<FamilyCategoryTargetItem>();
            foreach (Category category in document.Settings.Categories)
            {
                if (category == null || category.Parent != null || string.IsNullOrWhiteSpace(category.Name))
                    continue;
                if (category.CategoryType != CategoryType.Model && category.CategoryType != CategoryType.Annotation)
                    continue;
                if (!BuiltInCategoryHelper.TryFromCategory(category, out BuiltInCategory builtIn) || builtIn == BuiltInCategory.INVALID)
                    continue;

                result.Add(new FamilyCategoryTargetItem
                {
                    BuiltInCategory = builtIn,
                    CategoryId = category.Id,
                    Name = category.Name,
                    DisplayName = $"{(category.CategoryType == CategoryType.Model ? "模型" : "注释")}｜{category.Name}"
                });
            }

            return result
                .GroupBy(item => Convert.ToInt64(item.BuiltInCategory))
                .Select(group => group.First())
                .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private void SelectDefaultTarget()
        {
            if (!(TargetCategoryBox.ItemsSource is IEnumerable<FamilyCategoryTargetItem> items))
                return;

            FamilyCategoryTargetItem generic = items.FirstOrDefault(item =>
                item.BuiltInCategory == BuiltInCategory.OST_GenericModel);
            TargetCategoryBox.SelectedItem = generic ?? items.FirstOrDefault();
        }

        private bool FilterRow(object item)
        {
            if (!(item is FamilyCategoryRow row)) return false;
            string search = (SearchBox?.Text ?? string.Empty).Trim();
            if (search.Length == 0) return true;
            return row.FamilyName.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                   row.CurrentCategory.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private IEnumerable<FamilyCategoryRow> VisibleRows()
        {
            return _view.Cast<FamilyCategoryRow>();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _view?.Refresh();
            UpdateSelectionStatus();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            var target = TargetCategoryBox.SelectedItem as FamilyCategoryTargetItem;
            SetSelection(VisibleRows(), row => row.IsEditable && row.CurrentCategoryId != target?.CategoryId);
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            SetSelection(_rows, _ => false);
        }

        private void Invert_Click(object sender, RoutedEventArgs e)
        {
            var target = TargetCategoryBox.SelectedItem as FamilyCategoryTargetItem;
            SetSelection(VisibleRows(), row => row.IsEditable && row.CurrentCategoryId != target?.CategoryId && !row.IsSelected);
        }

        private void SetSelection(IEnumerable<FamilyCategoryRow> rows, Func<FamilyCategoryRow, bool> isSelected)
        {
            _updatingSelection = true;
            try
            {
                foreach (FamilyCategoryRow row in rows)
                    row.IsSelected = isSelected(row);
            }
            finally
            {
                _updatingSelection = false;
            }
            UpdateSelectionStatus();
        }

        private void Row_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!_updatingSelection && e.PropertyName == nameof(FamilyCategoryRow.IsSelected))
                UpdateSelectionStatus();
        }

        private void TargetCategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectionStatus();
        }

        private void UpdateSelectionStatus()
        {
            if (_view == null || SelectionStatus == null) return;
            var target = TargetCategoryBox.SelectedItem as FamilyCategoryTargetItem;
            var checkedRows = VisibleRows().Where(row => row.IsSelected && row.IsEditable).ToList();
            int unchanged = checkedRows.Count(row => row.CurrentCategoryId == target?.CategoryId);
            int hidden = _rows.Count(row => row.IsSelected && row.IsEditable) - checkedRows.Count;
            SelectionStatus.Text = $"待修改 {checkedRows.Count - unchanged} 个";
            if (unchanged > 0) SelectionStatus.Text += $" · 已是目标 {unchanged} 个";
            if (hidden > 0) SelectionStatus.Text += $" · 已隐藏 {hidden} 个";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var selected = VisibleRows().Where(row => row.IsSelected && row.IsEditable).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("请至少选择一个可编辑的载入族。", "族类别修改", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!(TargetCategoryBox.SelectedItem is FamilyCategoryTargetItem target))
            {
                MessageBox.Show("请选择目标族类别。", "族类别修改", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var changedIds = selected.Where(row => row.CurrentCategoryId != target.CategoryId)
                .Select(row => row.FamilyId).ToList();
            if (changedIds.Count == 0)
            {
                MessageBox.Show("当前列表勾选的族已全部属于目标类别。", "族类别修改", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Request = new FamilyCategoryChangeRequest
            {
                FamilyIds = changedIds,
                TargetCategory = target.BuiltInCategory,
                TargetCategoryName = target.Name
            };
            DialogResult = true;
        }
    }

    internal sealed class FamilyCategoryRow : INotifyPropertyChanged
    {
        private bool _isSelected;
        public ElementId FamilyId { get; set; }
        public string FamilyName { get; set; } = string.Empty;
        public string CurrentCategory { get; set; } = string.Empty;
        public ElementId CurrentCategoryId { get; set; } = ElementId.InvalidElementId;
        public int TypeCount { get; set; }
        public int InstanceCount { get; set; }
        public bool IsEditable { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public long ElementIdValue => FamilyId?.GetIdValue() ?? -1;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    internal sealed class FamilyCategoryTargetItem
    {
        public BuiltInCategory BuiltInCategory { get; set; }
        public ElementId CategoryId { get; set; } = ElementId.InvalidElementId;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
