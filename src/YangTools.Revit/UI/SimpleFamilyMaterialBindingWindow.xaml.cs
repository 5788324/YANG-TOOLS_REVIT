using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public partial class SimpleFamilyMaterialBindingWindow : Window
    {
        private readonly Document _document;
        private readonly IList<ElementId> _instanceIds;
        private readonly IList<MaterialChoice> _materials;

        internal SimpleFamilyMaterialBindingRequest Request { get; private set; }
        internal bool ReselectRequested { get; private set; }

        public SimpleFamilyMaterialBindingWindow(Document document, IList<ElementId> instanceIds)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _instanceIds = instanceIds ?? new List<ElementId>();
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);

            _materials = new FilteredElementCollector(_document)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .Where(material => material != null)
                .OrderBy(material => material.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(material => new MaterialChoice { Id = material.Id, Name = material.Name })
                .ToList();
            MaterialBox.ItemsSource = _materials;
            if (_materials.Count > 0)
                MaterialBox.SelectedIndex = 0;

            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            List<FamilyInstance> instances = _instanceIds
                .Select(id => _document.GetElement(id) as FamilyInstance)
                .Where(instance => instance != null)
                .ToList();
            int families = instances
                .Select(instance => instance.Symbol?.Family?.Id?.GetIdValue() ?? -1)
                .Where(id => id >= 0)
                .Distinct()
                .Count();
            int symbols = instances
                .Select(instance => instance.Symbol?.Id?.GetIdValue() ?? -1)
                .Where(id => id >= 0)
                .Distinct()
                .Count();
            string familyNames = string.Join("、", instances
                .Select(instance => instance.Symbol?.Family?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Take(5));
            string suffix = families > 5 ? "…" : string.Empty;
            SelectionSummaryText.Text = $"已选择 {instances.Count} 个实例 / {families} 个族 / {symbols} 个族类型" +
                                        (string.IsNullOrWhiteSpace(familyNames) ? string.Empty : $"\n族：{familyNames}{suffix}");
        }

        private void Reselect_Click(object sender, RoutedEventArgs e)
        {
            ReselectRequested = true;
            DialogResult = false;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ReselectRequested = false;
            DialogResult = false;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            string parameterName = (ParameterNameBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                MessageBox.Show("请输入材质参数名称。", "绑定材质", MessageBoxButton.OK, MessageBoxImage.Information);
                ParameterNameBox.Focus();
                return;
            }

            MaterialChoice material = MaterialBox.SelectedItem as MaterialChoice;
            string typedName = (MaterialBox.Text ?? string.Empty).Trim();
            if (material == null || (!string.IsNullOrWhiteSpace(typedName) &&
                !string.Equals(material.Name, typedName, StringComparison.CurrentCultureIgnoreCase)))
            {
                material = _materials.FirstOrDefault(item =>
                    string.Equals(item.Name, typedName, StringComparison.CurrentCultureIgnoreCase));
            }
            if (material == null)
            {
                MessageBox.Show("请选择当前项目中已经存在的材质。可以输入材质名称搜索，但必须选择/输入完整的现有材质名称。",
                    "绑定材质", MessageBoxButton.OK, MessageBoxImage.Information);
                MaterialBox.Focus();
                return;
            }

            Request = new SimpleFamilyMaterialBindingRequest
            {
                InstanceIds = _instanceIds.ToList(),
                ParameterName = parameterName,
                IsInstanceParameter = InstanceParameterRadio.IsChecked == true,
                RebindExistingAssociations = RebindExistingCheckBox.IsChecked == true,
                MaterialId = material.Id,
                MaterialName = material.Name
            };
            ReselectRequested = false;
            DialogResult = true;
        }

        private sealed class MaterialChoice
        {
            public ElementId Id { get; set; } = ElementId.InvalidElementId;
            public string Name { get; set; } = string.Empty;
        }
    }
}
