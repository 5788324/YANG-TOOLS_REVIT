using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Models;
using YangTools.Revit.Core.FamilyInstances;

namespace YangTools.Revit.UI
{
    public partial class FamilyInstanceManagerWindow : Window
    {
        private UIApplication _uiapp;
        private Document _doc;
        private ExternalEvent _externalEvent;
        private RevitEventHandler _handler;
        private ExternalEvent _readExternalEvent;
        private RevitReadEventHandler _readHandler;

        public ObservableCollection<InstanceItemViewModel> AllItems { get; set; } = new ObservableCollection<InstanceItemViewModel>();
        private ICollectionView _collectionView;

        public ObservableCollection<InstanceItemViewModel> SystemAllItems { get; set; } = new ObservableCollection<InstanceItemViewModel>();
        private ICollectionView _systemCollectionView;
        private BuiltInCategory _selectedSystemCategory = BuiltInCategory.INVALID;
        private InstanceScopeMode _scopeMode = InstanceScopeMode.Project;
        private ICollection<ElementId> _selectedElementIds = new List<ElementId>();
        private List<StableParameterDescriptor> _availableInstanceParameters = new List<StableParameterDescriptor>();
        private bool _suppressQuickFilterEvents;
        private long _readRequestVersion;
        private bool _isWindowClosed;
        private long _cellEditSequence;
        private long _batchParameterSequence;
        private long _batchTypeSequence;
        private readonly Dictionary<string, long> _pendingCellEdits = new Dictionary<string, long>();

        private List<string> _parameterColumns = new List<string> { "", "" };
        private string _configPath;

        private ElementId _selectedFamilyId;

        public FamilyInstanceManagerWindow(UIApplication uiapp, ElementId familyId = null)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;

            _handler = new RevitEventHandler(_doc, wrapInTransactionGroup: false, showAutomaticSuccess: false);
            _externalEvent = ExternalEvent.Create(_handler);
            _readHandler = new RevitReadEventHandler(_doc);
            _readExternalEvent = ExternalEvent.Create(_readHandler);

            InitializeV21Controls();
            PopulateFamilyFilter();
            PopulateSystemCategoryFilter();

            // If a family ID was passed (from Family Manager), pre-select it
            if (familyId != null && familyId != ElementId.InvalidElementId)
            {
                for (int i = 0; i < FamilyFilterComboBox.Items.Count; i++)
                {
                    var item = FamilyFilterComboBox.Items[i] as FamilyFilterItem;
                    if (item != null && item.FamilyId == familyId)
                    {
                        FamilyFilterComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void InitializeV21Controls()
        {
            _suppressQuickFilterEvents = true;
            ScopeComboBox.ItemsSource = new List<InstanceScopeItem>
            {
                new InstanceScopeItem("全项目", InstanceScopeMode.Project),
                new InstanceScopeItem("当前视图", InstanceScopeMode.ActiveView),
                new InstanceScopeItem("当前选择", InstanceScopeMode.Selection)
            };
            ScopeComboBox.DisplayMemberPath = "Name";
            ScopeComboBox.SelectedValuePath = "Mode";
            ScopeComboBox.SelectedValue = InstanceScopeMode.Project;

            TypeFilterComboBox.ItemsSource = new List<string> { "所有类型" };
            TypeFilterComboBox.SelectedIndex = 0;
            LevelFilterComboBox.ItemsSource = new List<string> { "所有标高" };
            LevelFilterComboBox.SelectedIndex = 0;
            _suppressQuickFilterEvents = false;
        }

        public void SelectFamily(ElementId familyId)
        {
            if (familyId == null || familyId == ElementId.InvalidElementId) return;
            MainTabControl.SelectedIndex = 0;
            for (int i = 0; i < FamilyFilterComboBox.Items.Count; i++)
            {
                if (FamilyFilterComboBox.Items[i] is FamilyFilterItem item && item.FamilyId == familyId)
                {
                    FamilyFilterComboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        private void PopulateFamilyFilter()
        {
            try
            {
                // Only collect families that have at least one instance in the project
                var familiesWithInstances = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Select(i => i.Symbol?.Family)
                    .Where(f => f != null)
                    .GroupBy(f => f.Id)
                    .Select(g => g.First())
                    .Select(f => new FamilyFilterItem
                    {
                        FamilyName = f.Name,
                        FamilyId = f.Id,
                        CategoryName = f.FamilyCategory?.Name ?? "未分类"
                    })
                    .OrderBy(f => f.CategoryName)
                    .ThenBy(f => f.FamilyName)
                    .ToList();

                FamilyFilterComboBox.ItemsSource = familiesWithInstances;
                FamilyFilterComboBox.DisplayMemberPath = "DisplayName";
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] FamilyInstanceManagerWindow.xaml.cs: {0}", ex.Message); }
        }

        private void PopulateSystemCategoryFilter()
        {
            var categories = new Dictionary<long, SystemCategoryItem>();
            try
            {
                foreach (Element element in new FilteredElementCollector(_doc).WhereElementIsNotElementType())
                {
                    try
                    {
                        if (element is FamilyInstance) continue;
                        if (element.Category == null || element.Category.CategoryType != CategoryType.Model) continue;
                        if (element.GetTypeId() == ElementId.InvalidElementId) continue;
                        if (!BuiltInCategoryHelper.TryFromCategory(element.Category, out BuiltInCategory bic)) continue;
                        long key = element.Category.Id.GetIdValue();
                        if (!categories.ContainsKey(key))
                            categories[key] = new SystemCategoryItem(element.Category.Name, bic);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[YangTools] PopulateSystemCategoryFilter: {0}", ex.Message);
            }

            var items = categories.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            SystemCategoryComboBox.ItemsSource = items;
            SystemCategoryComboBox.DisplayMemberPath = "Name";
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(e.Source is TabControl) || _readHandler == null) return;
            InvalidateReadRequests();

            if (MainTabControl.SelectedIndex == 1)
            {
                var selected = SystemCategoryComboBox.SelectedItem as SystemCategoryItem;
                if (selected == null)
                {
                    TargetTypeComboBox.ItemsSource = null;
                    BatchParameterComboBox.ItemsSource = null;
                    UpdateStatusText();
                    return;
                }
                _selectedSystemCategory = selected.Category;
                _configPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YangTools", $"SystemInstanceConfig_{_selectedSystemCategory}.txt");
                LoadConfig();
                RequestRead(() =>
                {
                    CreateSystemDynamicColumns();
                    LoadSystemData();
                    RefreshBatchOptions();
                }, "切换到系统实例");
            }
            else
            {
                var selected = FamilyFilterComboBox.SelectedItem as FamilyFilterItem;
                if (selected == null)
                {
                    TargetTypeComboBox.ItemsSource = null;
                    BatchParameterComboBox.ItemsSource = null;
                    UpdateStatusText();
                    return;
                }
                _selectedFamilyId = selected.FamilyId;
                string safeName = string.Join("_", selected.FamilyName.Split(System.IO.Path.GetInvalidFileNameChars()));
                _configPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YangTools", $"InstanceConfig_{safeName}.txt");
                LoadConfig();
                RequestRead(() =>
                {
                    CreateDynamicColumns();
                    LoadData();
                    RefreshBatchOptions();
                }, "切换到族实例");
            }
        }

        private void FamilyFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = FamilyFilterComboBox.SelectedItem as FamilyFilterItem;
            if (selected == null)
            {
                InvalidateReadRequests();
                AllItems.Clear();
                _selectedFamilyId = null;
                EmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                InstanceDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                TargetTypeComboBox.ItemsSource = null;
                BatchParameterComboBox.ItemsSource = null;
                UpdateStatusText();
                return;
            }

            _selectedFamilyId = selected.FamilyId;
            string safeName = string.Join("_", selected.FamilyName.Split(System.IO.Path.GetInvalidFileNameChars()));
            _configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YangTools", $"InstanceConfig_{safeName}.txt");

            LoadConfig();
            RequestRead(() =>
            {
                CreateDynamicColumns();
                LoadData();
                RefreshBatchOptions();
            }, "加载族实例");

            EmptyStatePanel.Visibility = System.Windows.Visibility.Collapsed;
            InstanceDataGrid.Visibility = System.Windows.Visibility.Visible;
        }

        private void SystemCategoryChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = SystemCategoryComboBox.SelectedItem as SystemCategoryItem;
            if (selected == null)
            {
                InvalidateReadRequests();
                SystemAllItems.Clear();
                _selectedSystemCategory = BuiltInCategory.INVALID;
                SystemEmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                SystemDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                TargetTypeComboBox.ItemsSource = null;
                BatchParameterComboBox.ItemsSource = null;
                UpdateStatusText();
                return;
            }

            _selectedSystemCategory = selected.Category;
            _configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YangTools", $"SystemInstanceConfig_{_selectedSystemCategory}.txt");

            LoadConfig();
            RequestRead(() =>
            {
                CreateSystemDynamicColumns();
                LoadSystemData();
                RefreshBatchOptions();
            }, "加载系统实例");

            SystemEmptyStatePanel.Visibility = System.Windows.Visibility.Collapsed;
            SystemDataGrid.Visibility = System.Windows.Visibility.Visible;
        }

        private void LoadConfig()
        {
            if (string.IsNullOrEmpty(_configPath))
            {
                _parameterColumns = new List<string> { "", "" };
                return;
            }

            if (System.IO.File.Exists(_configPath))
            {
                try
                {
                    string text = System.IO.File.ReadAllText(_configPath);
                    _parameterColumns = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (_parameterColumns.Count == 0) _parameterColumns = new List<string> { "", "" };
                }
                catch { _parameterColumns = new List<string> { "", "" }; }
            }
            else
            {
                _parameterColumns = new List<string> { "", "" };
            }
        }

        private void SaveConfig()
        {
            if (string.IsNullOrEmpty(_configPath)) return;
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_configPath));
                var validParams = _parameterColumns.Where(p => !string.IsNullOrEmpty(p)).ToList();
                string text = string.Join("\n", validParams);
                System.IO.File.WriteAllText(_configPath, text);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] FamilyInstanceManagerWindow.xaml.cs: {0}", ex.Message); }
        }

        private void CreateDynamicColumns()
        {
            CreateDynamicColumnsForGrid(InstanceDataGrid, false);
        }

        private void CreateSystemDynamicColumns()
        {
            CreateDynamicColumnsForGrid(SystemDataGrid, true);
        }

        private void CreateDynamicColumnsForGrid(DataGrid grid, bool isSystem)
        {
            var colsToRemove = grid.Columns.Where(c => c.Header is System.Windows.Controls.ComboBox || c.Header is System.Windows.Controls.Button).ToList();
            foreach (var column in colsToRemove) grid.Columns.Remove(column);

            var discoveryElements = CollectParameterDiscoveryElements(isSystem);
            _availableInstanceParameters = StableParameterKey.Collect(discoveryElements);

            bool migrated = false;
            for (int i = 0; i < _parameterColumns.Count; i++)
            {
                string normalized = StableParameterKey.NormalizeConfiguredKey(_parameterColumns[i], _availableInstanceParameters);
                if (!string.Equals(normalized, _parameterColumns[i], StringComparison.Ordinal))
                {
                    _parameterColumns[i] = normalized;
                    migrated = true;
                }
            }
            if (migrated) SaveConfig();

            var availableParams = _availableInstanceParameters.Select(x => new AvailableParamItem
            {
                Key = x.Key,
                Name = x.Name,
                DisplayName = x.DisplayName,
                IsReadOnly = x.IsReadOnly
            }).ToList();

            for (int colIndex = 0; colIndex < _parameterColumns.Count; colIndex++)
            {
                string pKey = _parameterColumns[colIndex];
                int capturedIndex = colIndex;
                var col = new DataGridTextColumn
                {
                    Width = new DataGridLength(160),
                    MinWidth = 140,
                    MaxWidth = 280
                };

                var combo = new System.Windows.Controls.ComboBox
                {
                    ItemsSource = availableParams,
                    SelectedValuePath = "Key",
                    DisplayMemberPath = "DisplayName",
                    Width = 170,
                    MinWidth = 140,
                    MaxWidth = 220,
                    Margin = new Thickness(0, 0, 4, 0),
                    ToolTip = "按稳定 ParameterKey 保存；重复同名参数会显示身份后缀。"
                };
                if (!string.IsNullOrEmpty(pKey)) combo.SelectedValue = pKey;

                combo.SelectionChanged += (sender, args) =>
                {
                    try
                    {
                        string newKey = combo.SelectedValue as string ?? string.Empty;
                        if (string.Equals(_parameterColumns[capturedIndex], newKey, StringComparison.Ordinal)) return;
                        _parameterColumns[capturedIndex] = newKey;
                        SaveConfig();
                        RequestRead(() =>
                        {
                            CreateDynamicColumnsForGrid(grid, isSystem);
                            if (isSystem) LoadSystemData(); else LoadData();
                            RefreshBatchOptions();
                        }, isSystem ? "刷新系统实例参数" : "刷新族实例参数");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[YangTools] Instance parameter header: {0}", ex.Message);
                    }
                };
                col.Header = combo;

                if (!string.IsNullOrEmpty(pKey))
                {
                    col.Binding = new System.Windows.Data.Binding($"Parameters[{pKey}].Value") { UpdateSourceTrigger = UpdateSourceTrigger.Explicit };

                    var elementStyle = new Style(typeof(TextBlock));
                    var readOnlyText = new DataTrigger { Binding = new System.Windows.Data.Binding($"Parameters[{pKey}].IsReadOnly"), Value = true };
                    readOnlyText.Setters.Add(new Setter(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray));
                    elementStyle.Triggers.Add(readOnlyText);
                    col.ElementStyle = elementStyle;

                    var editingStyle = new Style(typeof(System.Windows.Controls.TextBox));
                    editingStyle.Setters.Add(new Setter(System.Windows.Controls.TextBox.IsReadOnlyProperty, new System.Windows.Data.Binding($"Parameters[{pKey}].IsReadOnly")));
                    var readOnlyEdit = new DataTrigger { Binding = new System.Windows.Data.Binding($"Parameters[{pKey}].IsReadOnly"), Value = true };
                    readOnlyEdit.Setters.Add(new Setter(System.Windows.Controls.TextBox.ForegroundProperty, System.Windows.Media.Brushes.Gray));
                    editingStyle.Triggers.Add(readOnlyEdit);
                    col.EditingElementStyle = editingStyle;
                }
                else
                {
                    col.IsReadOnly = true;
                }

                grid.Columns.Add(col);
            }

            var addCol = new DataGridTextColumn
            {
                IsReadOnly = true,
                Width = new DataGridLength(44),
                MinWidth = 44,
                MaxWidth = 44
            };
            var addBtn = new System.Windows.Controls.Button
            {
                Content = "➕",
                ToolTip = "添加参数列",
                Width = 34,
                Height = 26,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(3, 1, 3, 1)
            };
            addBtn.Click += (sender, args) =>
            {
                _parameterColumns.Add(string.Empty);
                RequestRead(() => CreateDynamicColumnsForGrid(grid, isSystem), isSystem ? "添加系统实例参数列" : "添加族实例参数列");
            };
            addCol.Header = addBtn;
            grid.Columns.Add(addCol);

            BatchParameterComboBox.ItemsSource = _availableInstanceParameters;
            BatchParameterComboBox.DisplayMemberPath = "DisplayName";
            BatchParameterComboBox.SelectedValuePath = "Key";
        }

        private List<Element> CollectParameterDiscoveryElements(bool isSystem)
        {
            try
            {
                if (isSystem)
                {
                    if (_selectedSystemCategory == BuiltInCategory.INVALID) return new List<Element>();
                    return ApplySelectionScope(
                            BuildScopedCollector(_selectedSystemCategory)
                                .WhereElementIsNotElementType()
                                .ToList())
                        .ToList();
                }

                if (_selectedFamilyId == null || _selectedFamilyId == ElementId.InvalidElementId) return new List<Element>();
                return BuildScopedFamilyInstances()
                    .Cast<Element>()
                    .ToList();
            }
            catch
            {
                return new List<Element>();
            }
        }

        private string GetParameterValueSafely(Parameter param) => ParameterValueHelper.GetDisplayValue(param);

        // Invalidate old ExternalEvent reads when the user changes the displayed context.
        // The queue remains owned by RevitReadEventHandler on the Revit API thread.
        private void InvalidateReadRequests() => ++_readRequestVersion;

        private void RequestRead(Action action, string operationName)
        {
            if (_isWindowClosed || _readHandler == null || _readExternalEvent == null) return;
            long requestVersion = ++_readRequestVersion;
            _readHandler.SetAction(_ =>
            {
                if (_isWindowClosed || requestVersion != _readRequestVersion) return;
                action();
            }, operationName);
            _readExternalEvent.Raise();
        }

        private void RestoreAfterSelection()
        {
            Show();
            Activate();
        }

        protected override void OnClosed(EventArgs e)
        {
            _isWindowClosed = true;
            _pendingCellEdits.Clear();
            InvalidateReadRequests();
            _readExternalEvent?.Dispose();
            _externalEvent?.Dispose();
            base.OnClosed(e);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LoadData()
        {
            AllItems.Clear();
            if (_selectedFamilyId == null || _selectedFamilyId == ElementId.InvalidElementId)
            {
                UpdateQuickFilterChoices(AllItems);
                UpdateStatusText();
                return;
            }

            try
            {
                foreach (FamilyInstance inst in BuildScopedFamilyInstances())
                {
                    var vm = CreateInstanceViewModel(inst, inst.Symbol?.Family?.Name ?? string.Empty, inst.Symbol?.Name ?? string.Empty);
                    FillConfiguredParameters(inst, vm);
                    AllItems.Add(vm);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[YangTools] FamilyInstanceManagerWindow.LoadData: {0}", ex.Message);
            }

            _collectionView = CollectionViewSource.GetDefaultView(AllItems);
            _collectionView.GroupDescriptions.Clear();
            _collectionView.GroupDescriptions.Add(new PropertyGroupDescription("TypeName"));
            _collectionView.Filter = FilterItem;
            InstanceDataGrid.ItemsSource = _collectionView;
            UpdateQuickFilterChoices(AllItems);
            UpdateStatusText();
        }

        private void LoadSystemData()
        {
            SystemAllItems.Clear();
            if (_selectedSystemCategory == BuiltInCategory.INVALID)
            {
                UpdateQuickFilterChoices(SystemAllItems);
                UpdateStatusText();
                return;
            }

            try
            {
                foreach (Element inst in ApplySelectionScope(
                    BuildScopedCollector(_selectedSystemCategory).WhereElementIsNotElementType().ToList()))
                {
                    string typeName = ResolveTypeName(inst);
                    var vm = CreateInstanceViewModel(inst, "系统族", typeName);
                    FillConfiguredParameters(inst, vm);
                    SystemAllItems.Add(vm);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[YangTools] FamilyInstanceManagerWindow.LoadSystemData: {0}", ex.Message);
            }

            _systemCollectionView = CollectionViewSource.GetDefaultView(SystemAllItems);
            _systemCollectionView.GroupDescriptions.Clear();
            _systemCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("TypeName"));
            _systemCollectionView.Filter = FilterSystemItem;
            SystemDataGrid.ItemsSource = _systemCollectionView;
            UpdateQuickFilterChoices(SystemAllItems);
            UpdateStatusText();
        }

        private IEnumerable<FamilyInstance> BuildScopedFamilyInstances()
        {
            FilteredElementCollector collector = _scopeMode == InstanceScopeMode.ActiveView && _doc.ActiveView != null
                ? new FilteredElementCollector(_doc, _doc.ActiveView.Id)
                : new FilteredElementCollector(_doc);

            IEnumerable<FamilyInstance> instances = collector
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(inst =>
                {
                    try { return inst.Symbol?.Family?.Id == _selectedFamilyId; }
                    catch { return false; }
                });

            if (_scopeMode == InstanceScopeMode.Selection)
            {
                var selectedValues = new HashSet<long>((_selectedElementIds ?? Array.Empty<ElementId>()).Select(x => x.GetIdValue()));
                instances = instances.Where(x => selectedValues.Contains(x.Id.GetIdValue()));
            }
            return instances.ToList();
        }

        private FilteredElementCollector BuildScopedCollector(BuiltInCategory category)
        {
            FilteredElementCollector collector = _scopeMode == InstanceScopeMode.ActiveView && _doc.ActiveView != null
                ? new FilteredElementCollector(_doc, _doc.ActiveView.Id)
                : new FilteredElementCollector(_doc);

            collector = collector.OfCategory(category);
            if (_scopeMode != InstanceScopeMode.Selection) return collector;

            // FilteredElementCollector has no mutable Id filter after construction that works identically across all target years.
            // Selection filtering is therefore applied by callers after ToElements; this collector remains category-scoped.
            return collector;
        }

        private IEnumerable<Element> ApplySelectionScope(IEnumerable<Element> elements)
        {
            if (_scopeMode != InstanceScopeMode.Selection) return elements;
            var selectedValues = new HashSet<long>((_selectedElementIds ?? Array.Empty<ElementId>()).Select(x => x.GetIdValue()));
            return elements.Where(x => selectedValues.Contains(x.Id.GetIdValue()));
        }

        private InstanceItemViewModel CreateInstanceViewModel(Element element, string familyName, string typeName)
        {
            return new InstanceItemViewModel
            {
                Category = element.Category?.Name ?? string.Empty,
                FamilyName = familyName,
                TypeName = typeName,
                InstanceId = element.Id,
                LevelName = ResolveLevelName(element),
                HostName = ResolveHostName(element),
                IsPinned = element.Pinned
            };
        }

        private void FillConfiguredParameters(Element element, InstanceItemViewModel vm)
        {
            foreach (string pKey in _parameterColumns)
            {
                if (string.IsNullOrEmpty(pKey)) continue;
                var pvm = new ParameterViewModel { Value = string.Empty, IsReadOnly = true };
                try
                {
                    Parameter parameter = StableParameterKey.Resolve(element, pKey);
                    if (parameter != null)
                    {
                        pvm.Value = GetParameterValueSafely(parameter);
                        pvm.IsReadOnly = !ParameterValueHelper.CanEditAsText(parameter);
                    }
                }
                catch { }
                vm.Parameters[pKey] = pvm;
            }
        }

        private string ResolveTypeName(Element element)
        {
            try
            {
                ElementId typeId = element.GetTypeId();
                Element type = typeId != ElementId.InvalidElementId ? _doc.GetElement(typeId) : null;
                return type?.Name ?? element.Name ?? string.Empty;
            }
            catch { return element.Name ?? string.Empty; }
        }

        private string ResolveLevelName(Element element)
        {
            try
            {
                ElementId levelId = element.LevelId;
                if (levelId != null && levelId != ElementId.InvalidElementId && _doc.GetElement(levelId) is Level directLevel)
                    return directLevel.Name;
            }
            catch { }

            BuiltInParameter[] candidates =
            {
                BuiltInParameter.FAMILY_LEVEL_PARAM,
                BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
                BuiltInParameter.SCHEDULE_LEVEL_PARAM
            };
            foreach (BuiltInParameter bip in candidates)
            {
                try
                {
                    Parameter p = element.get_Parameter(bip);
                    if (p == null || p.StorageType != StorageType.ElementId) continue;
                    ElementId id = p.AsElementId();
                    if (id != null && id != ElementId.InvalidElementId && _doc.GetElement(id) is Level level)
                        return level.Name;
                }
                catch { }
            }
            return "—";
        }

        private string ResolveHostName(Element element)
        {
            try
            {
                if (element is FamilyInstance fi && fi.Host != null)
                {
                    Element host = fi.Host;
                    return $"{host.Category?.Name ?? host.GetType().Name}: {host.Name} [{host.Id.GetIdValue()}]";
                }
            }
            catch { }
            return "—";
        }

        private void UpdateQuickFilterChoices(IEnumerable<InstanceItemViewModel> items)
        {
            _suppressQuickFilterEvents = true;
            string oldType = TypeFilterComboBox.SelectedItem as string;
            string oldLevel = LevelFilterComboBox.SelectedItem as string;

            var types = new List<string> { "所有类型" };
            types.AddRange(items.Select(x => x.TypeName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase));
            TypeFilterComboBox.ItemsSource = types;
            TypeFilterComboBox.SelectedItem = oldType != null && types.Contains(oldType) ? oldType : "所有类型";

            var levels = new List<string> { "所有标高" };
            levels.AddRange(items.Select(x => x.LevelName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase));
            LevelFilterComboBox.ItemsSource = levels;
            LevelFilterComboBox.SelectedItem = oldLevel != null && levels.Contains(oldLevel) ? oldLevel : "所有标高";
            _suppressQuickFilterEvents = false;
        }

        private bool FilterSystemItem(object obj)
        {
            if (!(obj is InstanceItemViewModel item)) return false;
            return MatchesQuickFilters(item, SystemSearchBox.Text);
        }

        private bool FilterItem(object obj)
        {
            if (!(obj is InstanceItemViewModel item)) return false;
            return MatchesQuickFilters(item, SearchBox.Text);
        }

        private bool MatchesQuickFilters(InstanceItemViewModel item, string searchSource)
        {
            string searchText = searchSource?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(searchText))
            {
                bool match = (item.TypeName ?? string.Empty).IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0
                    || item.InstanceIdValue.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                    || (item.LevelName ?? string.Empty).IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0
                    || (item.HostName ?? string.Empty).IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0;
                if (!match) return false;
            }

            string selectedType = TypeFilterComboBox.SelectedItem as string;
            if (!string.IsNullOrEmpty(selectedType) && selectedType != "所有类型"
                && !string.Equals(item.TypeName, selectedType, StringComparison.CurrentCultureIgnoreCase))
                return false;

            string selectedLevel = LevelFilterComboBox.SelectedItem as string;
            if (!string.IsNullOrEmpty(selectedLevel) && selectedLevel != "所有标高"
                && !string.Equals(item.LevelName, selectedLevel, StringComparison.CurrentCultureIgnoreCase))
                return false;

            return true;
        }

        public void SetSelectionFilter(ICollection<ElementId> selectedIds)
        {
            if (selectedIds == null || selectedIds.Count == 0)
            {
                TaskDialog.Show("提示", "当前选择为空。请先在 Revit 中选择至少一个可管理实例。");
                return;
            }

            RequestRead(() =>
            {
                var ids = selectedIds.Where(x => x != null && x != ElementId.InvalidElementId).ToList();
                FamilyInstance firstInstance = ids
                    .Select(id => _doc.GetElement(id) as FamilyInstance)
                    .FirstOrDefault(x => x?.Symbol?.Family != null);

                if (firstInstance == null)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("提示", "当前选择中没有可管理的载入族实例。系统族请切换到“系统族”页后使用当前选择范围。"));
                    return;
                }

                ElementId familyId = firstInstance.Symbol.Family.Id;
                Dispatcher.Invoke(() => ApplySelectionScopeToFamily(ids, familyId));
            }, "应用当前选择范围");
        }

        private void ApplySelectionScopeToFamily(ICollection<ElementId> selectedIds, ElementId familyId)
        {
            _selectedElementIds = selectedIds?.ToList() ?? new List<ElementId>();
            _scopeMode = InstanceScopeMode.Selection;

            _suppressQuickFilterEvents = true;
            ScopeComboBox.SelectedValue = InstanceScopeMode.Selection;
            _suppressQuickFilterEvents = false;

            MainTabControl.SelectedIndex = 0;
            for (int i = 0; i < FamilyFilterComboBox.Items.Count; i++)
            {
                if (FamilyFilterComboBox.Items[i] is FamilyFilterItem item && item.FamilyId == familyId)
                {
                    FamilyFilterComboBox.SelectedIndex = i;
                    break;
                }
            }

            RequestRead(() =>
            {
                CreateDynamicColumns();
                LoadData();
                RefreshBatchOptions();
            }, "刷新当前选择实例");
        }

        private void ScopeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressQuickFilterEvents || ScopeComboBox == null || _readHandler == null) return;
            if (!(ScopeComboBox.SelectedValue is InstanceScopeMode mode)) return;

            if (mode == InstanceScopeMode.Selection)
            {
                RequestRead(() =>
                {
                    var uidoc = _uiapp?.ActiveUIDocument;
                    var ids = uidoc?.Selection?.GetElementIds()?.ToList() ?? new List<ElementId>();
                    if (ids.Count == 0)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _suppressQuickFilterEvents = true;
                            ScopeComboBox.SelectedValue = _scopeMode;
                            _suppressQuickFilterEvents = false;
                            TaskDialog.Show("提示", "当前 Revit 选择为空。先选择实例，再切换到“当前选择”。");
                        });
                        return;
                    }

                    _selectedElementIds = ids;
                    _scopeMode = InstanceScopeMode.Selection;
                    Dispatcher.Invoke(() => ReloadCurrentTab());
                }, "读取当前选择范围");
                return;
            }

            _scopeMode = mode;
            _selectedElementIds = new List<ElementId>();
            RequestRead(ReloadCurrentTab, mode == InstanceScopeMode.ActiveView ? "切换当前视图范围" : "切换全项目范围");
        }

        private void QuickFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressQuickFilterEvents) return;
            _collectionView?.Refresh();
            _systemCollectionView?.Refresh();
            UpdateStatusText();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RequestRead(ReloadCurrentTab, "刷新族实例管理");
        }

        private void ReloadCurrentTab()
        {
            if (MainTabControl.SelectedIndex == 1)
            {
                CreateSystemDynamicColumns();
                LoadSystemData();
            }
            else
            {
                CreateDynamicColumns();
                LoadData();
            }
            RefreshBatchOptions();
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            bool isSystem = MainTabControl?.SelectedIndex == 1;
            DataGrid grid = isSystem ? SystemDataGrid : InstanceDataGrid;
            ICollectionView view = isSystem ? _systemCollectionView : _collectionView;
            int resultCount = 0;
            try { resultCount = view?.Cast<object>().Count() ?? 0; } catch { }
            int selectedCount = grid?.SelectedItems?.Count ?? 0;
            string scope = _scopeMode == InstanceScopeMode.Project ? "全项目"
                : _scopeMode == InstanceScopeMode.ActiveView ? "当前视图" : "当前选择";
            if (InstanceStatusText != null)
                InstanceStatusText.Text = $"{scope} · 结果 {resultCount} · 选中 {selectedCount}";
        }

        private void RefreshBatchOptions()
        {
            if (_isWindowClosed || TargetTypeComboBox == null) return;
            try
            {
                var request = new FamilyInstanceTypeOptionsRequest(
                    _doc,
                    MainTabControl.SelectedIndex == 1,
                    _selectedFamilyId,
                    _selectedSystemCategory);
                var result = FamilyInstanceTypeOptionsService.Read(request);
                TargetTypeComboBox.ItemsSource = result.Options
                    .Select(x => new TypeTargetItem(x.TypeId, x.DisplayName))
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[YangTools] RefreshBatchOptions: {0}", ex.Message);
                TargetTypeComboBox.ItemsSource = new List<TypeTargetItem>();
            }
            TargetTypeComboBox.SelectedIndex = -1;
            UpdateStatusText();
        }

        private List<InstanceItemViewModel> GetSelectedRows()
        {
            DataGrid grid = MainTabControl.SelectedIndex == 1 ? SystemDataGrid : InstanceDataGrid;
            return grid.SelectedItems.Cast<InstanceItemViewModel>()
                .Where(x => x.InstanceId != null && x.InstanceId != ElementId.InvalidElementId)
                .ToList();
        }

        private void BatchChangeType_Click(object sender, RoutedEventArgs e)
        {
            var rows = GetSelectedRows();
            var target = TargetTypeComboBox.SelectedItem as TypeTargetItem;
            if (rows.Count == 0)
            {
                TaskDialog.Show("提示", "请先在表格中选择至少一个实例。");
                return;
            }
            if (target == null)
            {
                TaskDialog.Show("提示", "请选择目标类型。");
                return;
            }

            var request = new FamilyInstanceBatchTypeRequest
            {
                InstanceIds = rows.Select(x => x.InstanceId).ToArray(),
                TargetTypeId = target.TypeId
            };
            int tabAtRequest = MainTabControl.SelectedIndex;
            long readVersionAtRequest = _readRequestVersion;
            long familyIdAtRequest = _selectedFamilyId?.GetIdValue() ?? -1;
            BuiltInCategory categoryAtRequest = _selectedSystemCategory;
            InstanceScopeMode scopeAtRequest = _scopeMode;
            object typeFilterAtRequest = TypeFilterComboBox.SelectedItem;
            object levelFilterAtRequest = LevelFilterComboBox.SelectedItem;
            long batchVersionAtRequest = ++_batchTypeSequence;
            string operationId = Guid.NewGuid().ToString("N");
            bool IsCurrentRequest()
            {
                if (_isWindowClosed || tabAtRequest != MainTabControl.SelectedIndex ||
                    readVersionAtRequest != _readRequestVersion || batchVersionAtRequest != _batchTypeSequence ||
                    !ReferenceEquals(TargetTypeComboBox.SelectedItem, target) ||
                    familyIdAtRequest != (_selectedFamilyId?.GetIdValue() ?? -1) ||
                    categoryAtRequest != _selectedSystemCategory || scopeAtRequest != _scopeMode ||
                    !Equals(typeFilterAtRequest, TypeFilterComboBox.SelectedItem) ||
                    !Equals(levelFilterAtRequest, LevelFilterComboBox.SelectedItem))
                    return false;
                var grid = tabAtRequest == 1 ? SystemDataGrid : InstanceDataGrid;
                var activeRows = tabAtRequest == 1 ? SystemAllItems : AllItems;
                return rows.All(row => activeRows.Contains(row) && grid.SelectedItems.Contains(row));
            }

            _handler.SetAction(app =>
            {
                if (!IsCurrentRequest())
                {
                    DiagnosticLog.WriteProductOperation("FamilyInstance.BatchChangeType", operationId,
                        "SKIPPED_STALE_UI", _doc, "Window, tab, filter, rows, selection or target type changed.");
                    return;
                }
                // RevitEventHandler is bound to _doc; do not use a new active document.
                var result = FamilyInstanceBatchTypeService.Execute(_doc, request);
                DiagnosticLog.WriteProductOperation(
                    "FamilyInstance.BatchChangeType", operationId,
                    result.Committed ? "COMMITTED" : "NO_CHANGE", _doc,
                    $"TargetTypeId={request.TargetTypeId.GetIdValue()}; " +
                    $"InputIds={string.Join(",", request.InstanceIds.Select(x => x.GetIdValue()))}; " +
                    $"Success={result.ChangedCount}; Skipped={result.SkippedCount}; " +
                    $"ReadbackConfirmed={result.ReadbackConfirmed}; " +
                    $"Replacements={string.Join(",", result.Replacements.Select(x => x.OriginalId.GetIdValue() + ":" + x.EffectiveId.GetIdValue()))}");
                if (!IsCurrentRequest()) return;
                try
                {
                    if (!result.ReadbackConfirmed)
                        TaskDialog.Show("批量换型已提交", result.ReadbackWarning);
                    else
                        TaskDialog.Show("批量换型结果", $"成功：{result.ChangedCount}\n跳过：{result.SkippedCount}\n失败：0");
                    RequestRead(ReloadCurrentTab, "刷新换型结果");
                }
                catch (Exception uiError)
                {
                    TaskDialog.Show("批量换型界面刷新失败",
                        (result.Committed ? "模型换型已提交" : "模型没有发生修改") +
                        "，但界面更新失败：" + uiError.Message + "。请重新打开窗口核查。");
                }
            }, operationName: "批量更换实例类型", showSuccess: false, operationId: operationId);
            _externalEvent.Raise();
        }

        private void BatchSetParameter_Click(object sender, RoutedEventArgs e)
        {
            var rows = GetSelectedRows();
            var descriptor = BatchParameterComboBox.SelectedItem as StableParameterDescriptor;
            string newValue = BatchParameterValueBox.Text ?? string.Empty;
            bool onlyEmpty = OnlyEmptyCheckBox.IsChecked == true;
            if (rows.Count == 0)
            {
                TaskDialog.Show("提示", "请先在表格中选择至少一个实例。");
                return;
            }
            if (descriptor == null)
            {
                TaskDialog.Show("提示", "请选择要批量写入的参数。");
                return;
            }

            var request = new FamilyInstanceBatchParameterRequest
            {
                InstanceIds = rows.Select(x => x.InstanceId).ToArray(),
                ParameterKey = descriptor.Key,
                NewValue = newValue,
                OnlyEmpty = onlyEmpty
            };
            int tabAtRequest = MainTabControl.SelectedIndex;
            long readVersionAtRequest = _readRequestVersion;
            long batchVersionAtRequest = ++_batchParameterSequence;
            string operationId = Guid.NewGuid().ToString("N");
            bool IsCurrentRequest()
            {
                if (_isWindowClosed || tabAtRequest != MainTabControl.SelectedIndex ||
                    readVersionAtRequest != _readRequestVersion || batchVersionAtRequest != _batchParameterSequence)
                    return false;
                var grid = tabAtRequest == 1 ? SystemDataGrid : InstanceDataGrid;
                return rows.All(row => (tabAtRequest == 1 ? SystemAllItems : AllItems).Contains(row) &&
                    grid.SelectedItems.Contains(row));
            }

            _handler.SetAction(app =>
            {
                if (!IsCurrentRequest())
                {
                    DiagnosticLog.WriteProductOperation("FamilyInstance.BatchSetParameter", operationId,
                        "SKIPPED_STALE_UI", _doc, "Window, tab, filter, row or selected items changed before queued write.");
                    return;
                }
                var result = FamilyInstanceBatchParameterService.Execute(_doc, request);
                DiagnosticLog.WriteProductOperation(
                    "FamilyInstance.BatchSetParameter", operationId,
                    result.Committed ? "COMMITTED" : "NO_CHANGE", _doc,
                    $"ParameterKey={request.ParameterKey}; OnlyEmpty={request.OnlyEmpty}; " +
                    $"InputIds={string.Join(",", request.InstanceIds.Select(x => x.GetIdValue()))}; " +
                    $"Success={result.ChangedCount}; Skipped={result.SkippedCount}; ReadbackConfirmed={result.ReadbackConfirmed}");
                if (!IsCurrentRequest()) return;
                try
                {
                    if (!result.ReadbackConfirmed)
                        TaskDialog.Show("批量参数已提交", result.ReadbackWarning);
                    else
                        TaskDialog.Show("批量参数结果", $"成功：{result.ChangedCount}\n跳过：{result.SkippedCount}\n失败：0");
                    RequestRead(ReloadCurrentTab, "刷新批量参数结果");
                }
                catch (Exception uiError)
                {
                    TaskDialog.Show("批量参数界面刷新失败",
                        (result.Committed ? "模型修改已提交" : "模型没有发生修改") +
                        "，但界面更新失败：" + uiError.Message + "。请重新打开窗口核查。");
                }
            }, operationName: "批量写入实例参数", showSuccess: false, operationId: operationId);
            _externalEvent.Raise();
        }

        private void SystemSearchChanged(object sender, TextChangedEventArgs e)
        {
            if (_systemCollectionView != null) _systemCollectionView.Refresh();
            UpdateStatusText();
        }

        private void SelectElements_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            _handler.SetAction(app =>
            {
                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    var sel = app.ActiveUIDocument.Selection;
                    var picked = sel.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, "请选择要管理的族实例 (Select instances)");
                    
                    if (picked != null && picked.Count > 0)
                    {
                        var ids = picked.Select(p => p.ElementId).ToList();
                        FamilyInstance firstInstance = ids
                            .Select(id => doc.GetElement(id) as FamilyInstance)
                            .FirstOrDefault(x => x?.Symbol?.Family != null);
                        Dispatcher.Invoke(() =>
                        {
                            if (firstInstance == null)
                                TaskDialog.Show("提示", "选择中没有可管理的载入族实例。");
                            else
                                ApplySelectionScopeToFamily(ids, firstInstance.Symbol.Family.Id);
                            RestoreAfterSelection();
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(RestoreAfterSelection);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User canceled selection
                    Dispatcher.Invoke(RestoreAfterSelection);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => {
                        TaskDialog.Show("Error", ex.Message);
                        RestoreAfterSelection();
                    });
                }
            });
            _externalEvent.Raise();
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            bool isSystem = MainTabControl.SelectedIndex == 1;
            var activeView = isSystem ? _systemCollectionView : _collectionView;
            var activeItems = activeView?.Cast<InstanceItemViewModel>().ToList() ?? new List<InstanceItemViewModel>();

            if (activeItems.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel File (*.xlsx)|*.xlsx", FileName = "Instances.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
                    foreach (var item in activeItems)
                    {
                        var row = new System.Collections.Generic.Dictionary<string, object>();
                        row["图元ID"] = item.InstanceId.GetIdValue().ToString();
                        row["类型 (Type)"] = item.TypeName;
                        row["标高"] = item.LevelName;
                        row["主体"] = item.HostName;
                        row["固定"] = item.IsPinned ? "是" : "否";

                        foreach (var col in _parameterColumns)
                        {
                            if (string.IsNullOrEmpty(col)) continue;
                            string header = StableParameterKey.GetDisplayName(col, _availableInstanceParameters);
                            row[header] = item.Parameters.ContainsKey(col) ? item.Parameters[col].Value : "";
                        }
                        data.Add(row);
                    }
                    MiniExcelLibs.MiniExcel.SaveAs(dialog.FileName, data);
                    TaskDialog.Show("提示", "导出成功 (Export Successful)");
                }
                catch (Exception ex)
                {
                    TaskDialog.Show("错误", "导出失败: " + ex.Message);
                }
            }
        }

        private void ImportExcel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Excel File (*.xlsx)|*.xlsx" };
            if (dialog.ShowDialog() != true) return;

            string importPath = dialog.FileName;
            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var failures = new List<string>();
                int successCount = 0;
                int skippedCount = 0;

                try
                {
                    var rows = MiniExcelLibs.MiniExcel.Query(importPath, useHeaderRow: true)
                        .Cast<IDictionary<string, object>>()
                        .ToList();

                    using (TransactionGroup group = new TransactionGroup(doc, "批量导入实例参数"))
                    {
                        if (group.Start() != TransactionStatus.Started)
                        {
                            throw new InvalidOperationException("无法启动批量导入事务组。");
                        }

                        try
                        {
                            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                            {
                                var row = rows[rowIndex];
                                if (!row.TryGetValue("图元ID", out object idObject) ||
                                    idObject == null ||
                                    !long.TryParse(idObject.ToString(), out long idValue))
                                {
                                    skippedCount++;
                                    failures.Add($"第 {rowIndex + 2} 行：缺少有效的图元ID。");
                                    continue;
                                }

                                Element element = doc.GetElement(ElementIdExtensions.CreateId(idValue));
                                if (element == null)
                                {
                                    skippedCount++;
                                    failures.Add($"第 {rowIndex + 2} 行：找不到图元 {idValue}。");
                                    continue;
                                }

                                using (Transaction transaction = new Transaction(doc, $"导入实例参数 {idValue}"))
                                {
                                    if (transaction.Start() != TransactionStatus.Started)
                                    {
                                        skippedCount++;
                                        failures.Add($"图元 {idValue}：无法启动事务。");
                                        continue;
                                    }

                                    try
                                    {
                                        foreach (var item in row)
                                        {
                                            if (item.Key == "图元ID" || item.Key == "类型 (Type)" || item.Key == "标高" || item.Key == "主体" || item.Key == "固定" || item.Value == null)
                                            {
                                                continue;
                                            }

                                            Parameter parameter = StableParameterKey.ResolveByDisplayLabel(element, item.Key);
                                            if (parameter == null)
                                            {
                                                throw new InvalidOperationException($"不存在参数“{item.Key}”，或存在重复同名参数无法唯一定位。");
                                            }
                                            if (parameter.IsReadOnly)
                                            {
                                                throw new InvalidOperationException($"参数“{item.Key}”为只读。");
                                            }
                                            if (!ParameterValueHelper.TrySetFromDisplayValue(parameter, item.Value.ToString(), out string setError))
                                            {
                                                throw new InvalidOperationException($"参数“{item.Key}”写入失败：{setError}");
                                            }
                                        }

                                        if (transaction.Commit() != TransactionStatus.Committed)
                                        {
                                            throw new InvalidOperationException("事务提交失败。");
                                        }
                                        successCount++;
                                    }
                                    catch (Exception rowException)
                                    {
                                        if (transaction.GetStatus() == TransactionStatus.Started)
                                        {
                                            transaction.RollBack();
                                        }
                                        skippedCount++;
                                        failures.Add($"图元 {idValue}：{rowException.Message}");
                                    }
                                }
                            }

                            TransactionStatus groupStatus = successCount > 0
                                ? group.Assimilate()
                                : group.RollBack();
                            if (successCount > 0 && groupStatus != TransactionStatus.Committed)
                            {
                                throw new InvalidOperationException("批量导入事务组合并失败。");
                            }
                        }
                        catch
                        {
                            if (group.GetStatus() == TransactionStatus.Started)
                            {
                                group.RollBack();
                            }
                            throw;
                        }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        string detail = failures.Count == 0
                            ? string.Empty
                            : "\n\n失败明细（最多 8 条）：\n" + string.Join("\n", failures.Take(8));
                        TaskDialog.Show(
                            "实例参数导入结果",
                            $"成功：{successCount} 行\n跳过/失败：{skippedCount} 行{detail}");
                        if (MainTabControl.SelectedIndex == 1) LoadSystemData();
                        else LoadData();
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("错误", "导入失败：" + ex.Message));
                }
            });
            _externalEvent.Raise();
        }

        private void SearchChanged(object sender, TextChangedEventArgs e)
        {
            if (_collectionView != null) _collectionView.Refresh();
            UpdateStatusText();
        }



        private void InstanceDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit || _isWindowClosed) return;
            try
            {
                var grid = sender as DataGrid;
                bool isSystemTab = MainTabControl.SelectedIndex == 1;
                if (grid != (isSystemTab ? SystemDataGrid : InstanceDataGrid)) return;
                var row = e.Row.Item as InstanceItemViewModel;
                var editor = e.EditingElement as TextBox;
                if (row == null || editor == null || row.InstanceId == null) return;
                var binding = editor.GetBindingExpression(TextBox.TextProperty);
                var bindingPath = (binding?.ParentBinding as Binding)?.Path?.Path;
                if (string.IsNullOrEmpty(bindingPath) ||
                    !bindingPath.StartsWith("Parameters[", StringComparison.Ordinal) ||
                    !bindingPath.EndsWith("].Value", StringComparison.Ordinal)) return;
                string parameterKey = bindingPath.Substring(11, bindingPath.Length - 18);
                if (string.IsNullOrWhiteSpace(parameterKey) || !row.Parameters.TryGetValue(parameterKey, out var parameterVm)) return;

                string before = parameterVm.Value ?? string.Empty;
                string requestedValue = editor.Text ?? string.Empty;
                if (string.Equals(requestedValue, before, StringComparison.Ordinal)) return;
                if (parameterVm.IsReadOnly)
                {
                    parameterVm.Value = before;
                    TaskDialog.Show("参数不可编辑", "所选参数为只读或不支持文本写入。");
                    return;
                }

                // Capture identities and the UI context, not a mutable TextBox/active document.
                int tabAtRequest = MainTabControl.SelectedIndex;
                long readVersionAtRequest = _readRequestVersion;
                ElementId familyAtRequest = _selectedFamilyId;
                BuiltInCategory categoryAtRequest = _selectedSystemCategory;
                var request = new FamilyInstanceCellEditRequest
                {
                    InstanceId = row.InstanceId,
                    ParameterKey = parameterKey,
                    NewValue = requestedValue
                };
                string cellKey = request.InstanceId.GetIdValue() + ":" + parameterKey;
                long sequenceAtRequest = ++_cellEditSequence;
                _pendingCellEdits[cellKey] = sequenceAtRequest;
                bool IsRequestCurrent()
                {
                    return !_isWindowClosed && tabAtRequest == MainTabControl.SelectedIndex &&
                        readVersionAtRequest == _readRequestVersion &&
                        (isSystemTab
                            ? categoryAtRequest == _selectedSystemCategory && SystemAllItems.Contains(row)
                            : Equals(familyAtRequest, _selectedFamilyId) && AllItems.Contains(row)) &&
                        row.Parameters.TryGetValue(parameterKey, out var currentVm) &&
                        ReferenceEquals(currentVm, parameterVm) &&
                        _pendingCellEdits.TryGetValue(cellKey, out long latest) && latest == sequenceAtRequest;
                }

                _handler.SetAction(app =>
                {
                    if (!IsRequestCurrent()) return;
                    FamilyInstanceCellEditResult result;
                    try
                    {
                        result = FamilyInstanceCellEditService.Execute(_doc, request);
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (!IsRequestCurrent()) return;
                            _pendingCellEdits.Remove(cellKey);
                            parameterVm.Value = before;
                            TaskDialog.Show("实例参数修改失败", ex.Message);
                        });
                        return;
                    }
                    Dispatcher.Invoke(() =>
                    {
                        if (!IsRequestCurrent()) return;
                        _pendingCellEdits.Remove(cellKey);
                        if (!result.ReadbackConfirmed)
                        {
                            TaskDialog.Show("实例参数已提交", result.ReadbackWarning);
                            return;
                        }
                        try
                        {
                            parameterVm.Value = result.DisplayValue;
                        }
                        catch (Exception uiEx)
                        {
                            TaskDialog.Show("实例参数已提交", "模型修改已提交，但界面更新失败：" + uiEx.Message);
                        }
                    });
                }, "编辑族实例参数", showSuccess: false);
                _externalEvent.Raise();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("实例参数编辑失败", ex.Message);
            }
        }

        private void SystemDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            InstanceDataGrid_CellEditEnding(sender, e);
        }

        private void ResetColumns_Click(object sender, RoutedEventArgs e)
        {
            _parameterColumns = new List<string> { "", "" };
            SaveConfig();
            
            RequestRead(() =>
            {
                if (MainTabControl.SelectedIndex == 1)
                {
                    CreateSystemDynamicColumns();
                    LoadSystemData();
                }
                else
                {
                    CreateDynamicColumns();
                    if (_selectedFamilyId != null) LoadData();
                }
            }, "重置实例参数列");
        }

        private void SelectInView_Click(object sender, RoutedEventArgs e)
        {
            bool isSystem = MainTabControl.SelectedIndex == 1;
            var activeDataGrid = isSystem ? SystemDataGrid : InstanceDataGrid;
            
            var selectedItems = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<InstanceItemViewModel>(activeDataGrid.SelectedItems));
            if (selectedItems.Count == 0) return;
            
            var ids = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(selectedItems, x => x.InstanceId != ElementId.InvalidElementId), x => x.InstanceId));
            _handler.SetAction(app =>
            {
                var uidoc = app.ActiveUIDocument;
                uidoc.Selection.SetElementIds(ids);
                uidoc.ShowElements(ids);
            });
            _externalEvent.Raise();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            bool isSystem = MainTabControl.SelectedIndex == 1;
            var activeDataGrid = isSystem ? SystemDataGrid : InstanceDataGrid;

            var selectedItems = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<InstanceItemViewModel>(activeDataGrid.SelectedItems));
            if (selectedItems.Count == 0) return;

            var ids = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(selectedItems, x => x.InstanceId));
            ExecuteDelete(ids, "实例");
        }

        private void ExecuteDelete(System.Collections.Generic.List<ElementId> ids, string sourceDesc)
        {
            _handler.SetAction(app =>
            {
                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    int actualDeleteCount = 0;
                    bool proceed = true;

                    using (TransactionGroup tg = new TransactionGroup(doc, "级联删除(YANG TOOLS)"))
                    {
                        if (tg.Start() != TransactionStatus.Started)
                            throw new InvalidOperationException("无法启动实例删除事务组。");
                        System.Collections.Generic.List<ElementId> extraIds = new System.Collections.Generic.List<ElementId>();
                        using (Transaction t1 = new Transaction(doc, "Trial Delete"))
                        {
                            if (t1.Start() != TransactionStatus.Started)
                                throw new InvalidOperationException("无法启动实例删除预检事务。");
                            var deleted = doc.Delete(ids);
                            actualDeleteCount = deleted?.Count ?? 0;
                            if (deleted != null) extraIds = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(deleted, id => !ids.Contains(id)));
                            if (t1.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("实例删除预检事务未能回滚。");
                        }

                        var confirmDialog = new TaskDialog(extraIds.Count > 0 ? "警告" : "确认删除");
                        if (extraIds.Count > 0)
                        {
                            System.Text.StringBuilder extraDetails = new System.Text.StringBuilder();
                            int count = 0;
                            foreach (var eId in extraIds)
                            {
                                if (count++ >= 15) break;
                                try
                                {
                                    var el = doc.GetElement(eId);
                                    if (el != null)
                                    {
                                        string catName = el.Category?.Name ?? el.GetType().Name;
                                        extraDetails.AppendLine($"- [{catName}] {el.Name}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine("[YangTools] FamilyInstanceManagerWindow.xaml.cs: {0}", ex.Message);
                                }
                            }
                            if (extraIds.Count > 15)
                                extraDetails.AppendLine($"... 及其他 {extraIds.Count - 15} 个图元");

                            confirmDialog.MainInstruction = $"您选择的 {ids.Count} 个{sourceDesc}将连带删除 {extraIds.Count} 个图元！";
                            confirmDialog.MainContent = "这通常是因为删除了主体，导致依附于其上的标记或其他图元也被连带删除。\n\n连带删除的图元如下：\n\n" + extraDetails;
                        }
                        else
                        {
                            confirmDialog.MainInstruction = $"确定删除选中的 {ids.Count} 个{sourceDesc}吗？";
                            confirmDialog.MainContent = "删除操作会写入 Revit 撤销记录，执行后仍可使用 Ctrl+Z 撤销。";
                        }
                        confirmDialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                        confirmDialog.DefaultButton = TaskDialogResult.No;
                        proceed = confirmDialog.Show() == TaskDialogResult.Yes;

                        if (proceed)
                        {
                            using (Transaction t2 = new Transaction(doc, "Execute Delete"))
                            {
                                if (t2.Start() != TransactionStatus.Started)
                                    throw new InvalidOperationException("无法启动实例删除事务。");
                                doc.Delete(ids);
                                if (t2.Commit() != TransactionStatus.Committed)
                                    throw new InvalidOperationException("实例删除事务未能提交。");
                            }
                            TransactionStatus groupStatus = tg.Assimilate();
                            if (groupStatus != TransactionStatus.Committed)
                            {
                                if (tg.GetStatus() == TransactionStatus.Started) tg.RollBack();
                                throw new InvalidOperationException("实例删除事务组合并失败。");
                            }
                            Dispatcher.Invoke(() => 
                            {
                                if (MainTabControl.SelectedIndex == 1) LoadSystemData();
                                else LoadData();
                            });
                        }
                        else
                        {
                            tg.RollBack();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("错误", "删除失败: " + ex.Message));
                }
            });
            _externalEvent.Raise();
        }
    }

    public enum InstanceScopeMode
    {
        Project,
        ActiveView,
        Selection
    }

    public sealed class InstanceScopeItem
    {
        public string Name { get; }
        public InstanceScopeMode Mode { get; }

        public InstanceScopeItem(string name, InstanceScopeMode mode)
        {
            Name = name;
            Mode = mode;
        }
    }

    public sealed class TypeTargetItem
    {
        public ElementId TypeId { get; }
        public string DisplayName { get; }

        public TypeTargetItem(ElementId typeId, string displayName)
        {
            TypeId = typeId;
            DisplayName = displayName ?? string.Empty;
        }
    }

    public class FamilyFilterItem
    {
        public string FamilyName { get; set; }
        public string CategoryName { get; set; }
        public ElementId FamilyId { get; set; }
        public string DisplayName => $"{CategoryName} - {FamilyName}";
    }

    public class SystemCategoryItem
    {
        public string Name { get; set; }
        public BuiltInCategory Category { get; set; }

        public SystemCategoryItem(string name, BuiltInCategory category)
        {
            Name = name;
            Category = category;
        }
    }

    public class InstanceItemViewModel : INotifyPropertyChanged
    {
        public string Category { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string FamilyAndType => $"{FamilyName} - {TypeName}";
        public ElementId InstanceId { get; set; }
        public long InstanceIdValue => InstanceId?.GetIdValue() ?? -1;
        public string LevelName { get; set; } = "—";
        public string HostName { get; set; } = "—";
        public bool IsPinned { get; set; }

        public Dictionary<string, ParameterViewModel> Parameters { get; set; } = new Dictionary<string, ParameterViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
