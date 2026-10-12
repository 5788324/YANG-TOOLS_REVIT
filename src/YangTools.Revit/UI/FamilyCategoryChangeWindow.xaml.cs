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
            foreach (FamilyCategoryRow row in VisibleRows().Where(row => row.IsEditable))
                row.IsSelected = true;
            UpdateSelectionStatus();
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (FamilyCategoryRow row in VisibleRows())
                row.IsSelected = false;
            UpdateSelectionStatus();
        }

        private void Invert_Click(object sender, RoutedEventArgs e)
        {
            foreach (FamilyCategoryRow row in VisibleRows().Where(row => row.IsEditable))
                row.IsSelected = !row.IsSelected;
            UpdateSelectionStatus();
        }

        private void Row_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FamilyCategoryRow.IsSelected))
                UpdateSelectionStatus();
        }

        private void UpdateSelectionStatus()
        {
            int selected = _rows.Count(row => row.IsSelected && row.IsEditable);
            int editable = _rows.Count(row => row.IsEditable);
            if (SelectionStatus != null)
                SelectionStatus.Text = $"已选择 {selected} / 可修改 {editable}";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var selected = _rows.Where(row => row.IsSelected && row.IsEditable).ToList();
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

            Request = new FamilyCategoryChangeRequest
            {
                FamilyIds = selected.Select(row => row.FamilyId).ToList(),
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
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
