using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public partial class FamilyMaterialBindingWindow : Window
    {
        private readonly Document _document;
        private readonly ObservableCollection<FamilyMaterialBindingRow> _rows = new ObservableCollection<FamilyMaterialBindingRow>();
        private readonly ICollectionView _view;

        internal FamilyMaterialBindingRequest Request { get; private set; }

        public FamilyMaterialBindingWindow(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);

            LoadFamilies();
            _view = CollectionViewSource.GetDefaultView(_rows);
            _view.Filter = FilterRow;
            FamilyGrid.ItemsSource = _view;
            UpdateSelectionStatus();
        }

        private void LoadFamilies()
        {
            var instanceCountByFamily = new Dictionary<long, int>();
            foreach (FamilyInstance instance in new FilteredElementCollector(_document)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>())
            {
                Family family = instance.Symbol?.Family;
                if (family == null) continue;
                long id = family.Id.GetIdValue();
                instanceCountByFamily[id] = instanceCountByFamily.TryGetValue(id, out int count) ? count + 1 : 1;
            }

            foreach (Family family in new FilteredElementCollector(_document)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                bool editable = family.IsEditable && !family.IsInPlace;
                var row = new FamilyMaterialBindingRow
                {
                    FamilyId = family.Id,
                    FamilyName = family.Name,
                    CurrentCategory = family.FamilyCategory?.Name ?? "未分类",
                    TypeCount = family.GetFamilySymbolIds().Count,
                    InstanceCount = instanceCountByFamily.TryGetValue(family.Id.GetIdValue(), out int count) ? count : 0,
                    IsEditable = editable,
                    StatusText = editable ? "可处理" : (family.IsInPlace ? "就地族" : "不可编辑")
                };
                row.PropertyChanged += Row_PropertyChanged;
                _rows.Add(row);
            }
        }

        private bool FilterRow(object item)
        {
            if (!(item is FamilyMaterialBindingRow row)) return false;
            string search = (SearchBox?.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(search)) return true;

            return row.FamilyName.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                   row.CurrentCategory.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private IEnumerable<FamilyMaterialBindingRow> VisibleRows()
        {
            return _view.Cast<FamilyMaterialBindingRow>();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _view?.Refresh();
            UpdateSelectionStatus();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (FamilyMaterialBindingRow row in VisibleRows().Where(row => row.IsEditable))
                row.IsSelected = true;
            UpdateSelectionStatus();
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (FamilyMaterialBindingRow row in VisibleRows())
                row.IsSelected = false;
            UpdateSelectionStatus();
        }

        private void Invert_Click(object sender, RoutedEventArgs e)
        {
            foreach (FamilyMaterialBindingRow row in VisibleRows().Where(row => row.IsEditable))
                row.IsSelected = !row.IsSelected;
            UpdateSelectionStatus();
        }

        private void Row_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FamilyMaterialBindingRow.IsSelected))
                UpdateSelectionStatus();
        }

        private void UpdateSelectionStatus()
        {
            int selected = _rows.Count(row => row.IsSelected && row.IsEditable);
            int editable = _rows.Count(row => row.IsEditable);
            if (SelectionStatus != null)
                SelectionStatus.Text = $"已选择 {selected} / 可处理 {editable}";
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
                MessageBox.Show("请至少选择一个可编辑的载入族。", "批量绑定材质", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string parameterName = (ParameterNameBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                MessageBox.Show("请输入材质参数名称。", "批量绑定材质", MessageBoxButton.OK, MessageBoxImage.Information);
                ParameterNameBox.Focus();
                return;
            }

            Request = new FamilyMaterialBindingRequest
            {
                FamilyIds = selected.Select(row => row.FamilyId).ToList(),
                ParameterName = parameterName,
                IsInstanceParameter = InstanceParameterRadio.IsChecked == true,
                RebindExistingAssociations = RebindExistingCheckBox.IsChecked == true
            };
            DialogResult = true;
        }
    }

    internal sealed class FamilyMaterialBindingRow : INotifyPropertyChanged
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
}
