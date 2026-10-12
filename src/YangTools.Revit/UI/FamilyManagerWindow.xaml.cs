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

namespace YangTools.Revit.UI
{
    public partial class FamilyManagerWindow : Window
    {
        private UIApplication _uiapp;
        private Document _doc;
        private ExternalEvent _externalEvent;
        private RevitEventHandler _handler;
        private ExternalEvent _readExternalEvent;
        private RevitReadEventHandler _readHandler;

        public ObservableCollection<FamilyListItemViewModel> FamilyList { get; set; } = new ObservableCollection<FamilyListItemViewModel>();
        public ObservableCollection<TypeItemViewModel> TypeList { get; set; } = new ObservableCollection<TypeItemViewModel>();
        private ICollectionView _familyCollectionView;
        private ICollectionView _typeCollectionView;
        private FamilyUsageSnapshot _usageSnapshot = new FamilyUsageSnapshot();
        private List<StableParameterDescriptor> _availableTypeParameters = new List<StableParameterDescriptor>();

        private List<string> _parameterColumns = new List<string> { "", "" };
        private string _configPath;
        private ElementId _selectedFamilyId;

        public FamilyManagerWindow(UIApplication uiapp)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;

            _handler = new RevitEventHandler(_doc, wrapInTransactionGroup: false, showAutomaticSuccess: false);
            _externalEvent = ExternalEvent.Create(_handler);
            _readHandler = new RevitReadEventHandler(_doc);
            _readExternalEvent = ExternalEvent.Create(_readHandler);

            LoadFamilyList();
            PopulateCategoryFilter();
        }

        private void PopulateCategoryFilter()
        {
            var categories = FamilyList.Select(x => x.Category).Distinct().OrderBy(x => x).ToList();
            categories.Insert(0, "所有类别 (All)");
            CategoryFilterComboBox.ItemsSource = categories;
            CategoryFilterComboBox.SelectedIndex = 0;
        }

        private void LoadFamilyList()
        {
            ElementId previouslySelectedId = _selectedFamilyId;
            FamilyList.Clear();
            try
            {
                _usageSnapshot = FamilyUsageService.Build(_doc, _doc.ActiveView?.Id ?? ElementId.InvalidElementId);
                var families = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Where(f => f.IsEditable)
                    .OrderBy(f => f.FamilyCategory?.Name ?? string.Empty)
                    .ThenBy(f => f.Name)
                    .ToList();

                foreach (var f in families)
                {
                    var symbolIds = f.GetFamilySymbolIds();
                    int usedTypeCount = symbolIds.Count(id => _usageSnapshot.GetTypeCount(id) > 0);
                    var vm = new FamilyListItemViewModel
                    {
                        Category = f.FamilyCategory?.Name ?? "未知",
                        FamilyName = f.Name,
                        FamilyId = f.Id,
                        TypeCount = symbolIds.Count,
                        InstanceCount = _usageSnapshot.GetFamilyCount(f.Id),
                        CurrentViewInstanceCount = _usageSnapshot.GetCurrentViewFamilyCount(f.Id),
                        UsedTypeCount = usedTypeCount,
                        UnusedTypeCount = Math.Max(0, symbolIds.Count - usedTypeCount)
                    };
                    FamilyList.Add(vm);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[YangTools] FamilyManagerWindow.LoadFamilyList: {0}", ex.Message);
            }

            _familyCollectionView = CollectionViewSource.GetDefaultView(FamilyList);
            _familyCollectionView.GroupDescriptions.Clear();
            _familyCollectionView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            _familyCollectionView.Filter = FilterFamilyItem;
            FamilyListBox.ItemsSource = _familyCollectionView;
            FamilyListStatusText.Text = $"族：{FamilyList.Count} · 有实例：{FamilyList.Count(x => x.InstanceCount > 0)} · 有未使用类型：{FamilyList.Count(x => x.UnusedTypeCount > 0)}";

            if (previouslySelectedId != null && previouslySelectedId != ElementId.InvalidElementId)
            {
                var previous = FamilyList.FirstOrDefault(x => x.FamilyId == previouslySelectedId);
                if (previous != null) FamilyListBox.SelectedItem = previous;
            }
        }

        private bool FilterFamilyItem(object obj)
        {
            if (!(obj is FamilyListItemViewModel item)) return false;

            if (CategoryFilterComboBox.SelectedIndex > 0
                && !string.Equals(item.Category, CategoryFilterComboBox.SelectedItem?.ToString(), StringComparison.Ordinal))
                return false;

            string searchText = SearchBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(searchText)
                && item.FamilyName.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) < 0)
                return false;

            if (UnusedFamilyOnlyCheckBox.IsChecked == true && item.UnusedTypeCount <= 0)
                return false;

            return true;
        }

        private void UnusedFamilyOnlyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _familyCollectionView?.Refresh();
        }

        private void UnusedTypesOnlyCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            _typeCollectionView?.Refresh();
        }

        private void FilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_familyCollectionView != null) _familyCollectionView.Refresh();
        }
        
        private void FilterChanged(object sender, TextChangedEventArgs e)
        {
            if (_familyCollectionView != null) _familyCollectionView.Refresh();
        }

        private void FamilyListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = FamilyListBox.SelectedItem as FamilyListItemViewModel;
            if (selected == null)
            {
                TypeList.Clear();
                EmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                TypeDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                SelectedFamilyTitle.Text = "请在左侧选择一个族";
                SelectedFamilyStatsText.Text = "实例 0 · 使用类型 0 · 未使用类型 0 · 当前视图 0";
                RenameFamilyBtn.Visibility = System.Windows.Visibility.Collapsed;
                ManageInstancesBtn.IsEnabled = false;
                _selectedFamilyId = null;
                return;
            }

            _selectedFamilyId = selected.FamilyId;
            SelectedFamilyTitle.Text = $"族：{selected.FamilyName} ({selected.Category})";
            SelectedFamilyStatsText.Text = $"实例 {selected.InstanceCount} · 使用类型 {selected.UsedTypeCount}/{selected.TypeCount} · 未使用类型 {selected.UnusedTypeCount} · 当前视图 {selected.CurrentViewInstanceCount}";
            RenameFamilyBtn.Visibility = System.Windows.Visibility.Visible;
            ManageInstancesBtn.IsEnabled = selected.InstanceCount > 0;

            string safeName = string.Join("_", selected.FamilyName.Split(System.IO.Path.GetInvalidFileNameChars()));
            _configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YangTools", $"FamilyConfig_{safeName}.txt");

            LoadConfig();
            RequestRead(() =>
            {
                CreateDynamicColumns();
                LoadTypeData();
            }, "加载族类型");

            EmptyStatePanel.Visibility = System.Windows.Visibility.Collapsed;
            TypeDataGrid.Visibility = System.Windows.Visibility.Visible;
        }

        private void LoadConfig()
        {
            if (string.IsNullOrEmpty(_configPath)) return;
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
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] FamilyManagerWindow.xaml.cs: {0}", ex.Message); }
        }

        private void CreateDynamicColumns()
        {
            var colsToRemove = TypeDataGrid.Columns.Where(c =>
            {
                var h = c.Header;
                return h is System.Windows.Controls.ComboBox || h is System.Windows.Controls.Button;
            }).ToList();
            foreach (var c in colsToRemove) TypeDataGrid.Columns.Remove(c);

            _availableTypeParameters = new List<StableParameterDescriptor>();
            try
            {
                if (_selectedFamilyId != null && _selectedFamilyId != ElementId.InvalidElementId)
                {
                    var family = _doc.GetElement(_selectedFamilyId) as Family;
                    if (family != null)
                    {
                        var elements = family.GetFamilySymbolIds()
                            .Select(id => _doc.GetElement(id))
                            .Where(x => x != null)
                            .Cast<Element>()
                            .ToList();
                        _availableTypeParameters = StableParameterKey.Collect(elements);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[YangTools] FamilyManagerWindow.CreateDynamicColumns: {0}", ex.Message);
            }

            bool migrated = false;
            for (int i = 0; i < _parameterColumns.Count; i++)
            {
                string normalized = StableParameterKey.NormalizeConfiguredKey(_parameterColumns[i], _availableTypeParameters);
                if (!string.Equals(normalized, _parameterColumns[i], StringComparison.Ordinal))
                {
                    _parameterColumns[i] = normalized;
                    migrated = true;
                }
            }
            if (migrated) SaveConfig();

            var availableParams = _availableTypeParameters.Select(x => new AvailableParamItem
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
                    ToolTip = "按稳定 ParameterKey 保存。重复同名参数会显示身份后缀。"
                };
                if (!string.IsNullOrEmpty(pKey)) combo.SelectedValue = pKey;

                combo.SelectionChanged += (s, ev) =>
                {
                    try
                    {
                        string newKey = combo.SelectedValue as string ?? string.Empty;
                        if (string.Equals(_parameterColumns[capturedIndex], newKey, StringComparison.Ordinal)) return;

                        _parameterColumns[capturedIndex] = newKey;
                        SaveConfig();
                        RequestRead(() =>
                        {
                            CreateDynamicColumns();
                            LoadTypeData();
                        }, "刷新族类型参数");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[YangTools] FamilyManagerWindow parameter header: {0}", ex.Message);
                    }
                };

                col.Header = combo;

                if (!string.IsNullOrEmpty(pKey))
                {
                    col.Binding = new System.Windows.Data.Binding($"Parameters[{pKey}].Value") { UpdateSourceTrigger = UpdateSourceTrigger.Explicit };

                    var elStyle = new Style(typeof(TextBlock));
                    var t1 = new DataTrigger { Binding = new System.Windows.Data.Binding($"Parameters[{pKey}].IsReadOnly"), Value = true };
                    t1.Setters.Add(new Setter(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray));
                    elStyle.Triggers.Add(t1);
                    col.ElementStyle = elStyle;

                    var editStyle = new Style(typeof(System.Windows.Controls.TextBox));
                    editStyle.Setters.Add(new Setter(System.Windows.Controls.TextBox.IsReadOnlyProperty, new System.Windows.Data.Binding($"Parameters[{pKey}].IsReadOnly")));
                    var t2 = new DataTrigger { Binding = new System.Windows.Data.Binding($"Parameters[{pKey}].IsReadOnly"), Value = true };
                    t2.Setters.Add(new Setter(System.Windows.Controls.TextBox.ForegroundProperty, System.Windows.Media.Brushes.Gray));
                    editStyle.Triggers.Add(t2);
                    col.EditingElementStyle = editStyle;
                }
                else
                {
                    col.IsReadOnly = true;
                }

                TypeDataGrid.Columns.Add(col);
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
            addBtn.Click += (s, ev) =>
            {
                _parameterColumns.Add(string.Empty);
                RequestRead(CreateDynamicColumns, "添加族类型参数列");
            };
            addCol.Header = addBtn;
            TypeDataGrid.Columns.Add(addCol);
        }

        private void LoadTypeData()
        {
            TypeList.Clear();
            if (_selectedFamilyId == null || _selectedFamilyId == ElementId.InvalidElementId) return;

            try
            {
                var family = _doc.GetElement(_selectedFamilyId) as Family;
                if (family == null) return;

                foreach (ElementId symId in family.GetFamilySymbolIds())
                {
                    var sym = _doc.GetElement(symId) as FamilySymbol;
                    if (sym == null) continue;

                    int usageCount = _usageSnapshot.GetTypeCount(sym.Id);
                    var vm = new TypeItemViewModel
                    {
                        TypeName = sym.Name,
                        TypeId = sym.Id,
                        FamilyId = family.Id,
                        UsageCount = usageCount
                    };

                    foreach (string pKey in _parameterColumns)
                    {
                        if (string.IsNullOrEmpty(pKey)) continue;
                        try
                        {
                            var pvm = new ParameterViewModel { Value = string.Empty, IsReadOnly = true };
                            Parameter param = StableParameterKey.Resolve(sym, pKey);
                            if (param != null)
                            {
                                pvm.Value = GetParameterValueSafely(param);
                                pvm.IsReadOnly = !ParameterValueHelper.CanEditAsText(param);
                            }
                            vm.Parameters[pKey] = pvm;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("[YangTools] FamilyManagerWindow.LoadTypeData parameter: {0}", ex.Message);
                        }
                    }
                    TypeList.Add(vm);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[YangTools] FamilyManagerWindow.LoadTypeData: {0}", ex.Message);
            }

            _typeCollectionView = CollectionViewSource.GetDefaultView(TypeList);
            _typeCollectionView.Filter = obj => !(obj is TypeItemViewModel item)
                || UnusedTypesOnlyCheckBox.IsChecked != true
                || item.UsageCount == 0;
            TypeDataGrid.ItemsSource = _typeCollectionView;
        }

        private string GetParameterValueSafely(Parameter param) => ParameterValueHelper.GetDisplayValue(param);

        private void RequestRead(Action action, string operationName)
        {
            _readHandler.SetAction(_ => action(), operationName);
            _readExternalEvent.Raise();
        }

        protected override void OnClosed(EventArgs e)
        {
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

        private void TypeDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            try
            {
                var vm = e.Row.Item as TypeItemViewModel;
                var el = e.EditingElement as System.Windows.Controls.TextBox;
                if (vm == null || el == null) return;
                var newValue = el.Text;

                var bindingExpr = el.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                var bindingPath = (bindingExpr?.ParentBinding as System.Windows.Data.Binding)?.Path?.Path;
                if (string.IsNullOrEmpty(bindingPath)) return;

                if (bindingPath == "TypeName")
                {
                    var oldTypeName = vm.TypeName;
                    if (newValue == oldTypeName) return;
                    if (vm.TypeId == ElementId.InvalidElementId) return;

                    _handler.SetAction(app =>
                    {
                        try
                        {
                            var doc = app.ActiveUIDocument.Document;
                            var type = doc.GetElement(vm.TypeId) as ElementType;
                            if (type == null) return;
                            bool success = false;
                            try
                            {
                                using (Transaction t = new Transaction(doc, "Rename Type"))
                                {
                                    t.Start();
                                    type.Name = newValue;
                                    if (t.Commit() != TransactionStatus.Committed)
                                        throw new InvalidOperationException("事务提交未完成。");
                                    success = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not rename: " + ex.Message));
                            }
                            Dispatcher.Invoke(() =>
                            {
                                if (success) vm.TypeName = newValue;
                                else vm.TypeName = oldTypeName;
                            });
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] FamilyManagerWindow.xaml.cs: {0}", ex.Message); }
                    });
                    _externalEvent.Raise();
                }
                else if (bindingPath.StartsWith("Parameters[") && bindingPath.EndsWith("].Value") && bindingPath.Length > 19)
                {
                    string pKey = bindingPath.Substring(11, bindingPath.Length - 18);
                    if (string.IsNullOrEmpty(pKey)) return;
                    if (!vm.Parameters.ContainsKey(pKey)) return;
                    var pvm = vm.Parameters[pKey];
                    var oldValue = pvm.Value;
                    if (newValue == oldValue || pvm.IsReadOnly) return;

                    _handler.SetAction(app =>
                    {
                        try
                        {
                            var doc = app.ActiveUIDocument.Document;
                            var type = doc.GetElement(vm.TypeId) as ElementType;
                            if (type == null) return;
                            var param = StableParameterKey.Resolve(type, pKey);
                            if (param == null || param.IsReadOnly) return;

                            bool success = false;
                            try
                            {
                                using (Transaction t = new Transaction(doc, "Set Parameter"))
                                {
                                    t.Start();
                                    if (!ParameterValueHelper.TrySetFromDisplayValue(param, newValue, out string setError))
                                    {
                                        throw new InvalidOperationException(setError);
                                    }
                                    if (t.Commit() != TransactionStatus.Committed)
                                        throw new InvalidOperationException("事务提交未完成。");
                                    success = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not set parameter: " + ex.Message));
                            }
                            string readBackValue = oldValue;
                            if (success)
                            {
                                try
                                {
                                    var refreshed = doc.GetElement(vm.TypeId) as ElementType;
                                    var refreshedParam = refreshed == null ? null : StableParameterKey.Resolve(refreshed, pKey);
                                    if (refreshedParam == null)
                                        success = false;
                                    else
                                        readBackValue = ParameterValueHelper.GetDisplayValue(refreshedParam);
                                }
                                catch
                                {
                                    success = false;
                                }
                            }
                            Dispatcher.Invoke(() =>
                            {
                                pvm.Value = success ? readBackValue : oldValue;
                            });
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] FamilyManagerWindow.xaml.cs: {0}", ex.Message); }
                    });
                    _externalEvent.Raise();
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] FamilyManagerWindow.xaml.cs: {0}", ex.Message); }
        }

        private void RefreshFamilyManager_Click(object sender, RoutedEventArgs e)
        {
            RequestRead(() =>
            {
                LoadFamilyList();
                PopulateCategoryFilter();
                if (_selectedFamilyId != null && _selectedFamilyId != ElementId.InvalidElementId)
                {
                    CreateDynamicColumns();
                    LoadTypeData();
                }
            }, "刷新族管理数据");
        }

        private void ResetColumns_Click(object sender, RoutedEventArgs e)
        {
            _parameterColumns = new List<string> { "", "" };
            SaveConfig();
            RequestRead(() =>
            {
                CreateDynamicColumns();
                if (_selectedFamilyId != null) LoadTypeData();
            }, "重置族类型参数列");
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (TypeList.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Excel File (*.xlsx)|*.xlsx", FileName = "FamilyTypes.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var data = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>();
                    foreach (var item in TypeList)
                    {
                        var row = new System.Collections.Generic.Dictionary<string, object>();
                        row["类型名称 (Type)"] = item.TypeName;
                        row["图元ID"] = item.TypeId.GetIdValue().ToString();
                        
                        foreach (var col in _parameterColumns)
                        {
                            if (string.IsNullOrEmpty(col)) continue;
                            string header = StableParameterKey.GetDisplayName(col, _availableTypeParameters);
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

                    using (TransactionGroup group = new TransactionGroup(doc, "批量导入族类型参数"))
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
                                    failures.Add($"第 {rowIndex + 2} 行：找不到族类型 {idValue}。");
                                    continue;
                                }

                                using (Transaction transaction = new Transaction(doc, $"导入族类型 {idValue}"))
                                {
                                    if (transaction.Start() != TransactionStatus.Started)
                                    {
                                        skippedCount++;
                                        failures.Add($"族类型 {idValue}：无法启动事务。");
                                        continue;
                                    }

                                    try
                                    {
                                        if (row.TryGetValue("类型名称 (Type)", out object typeNameObject) && typeNameObject != null)
                                        {
                                            string newName = typeNameObject.ToString()?.Trim();
                                            if (!string.IsNullOrWhiteSpace(newName) && element.Name != newName)
                                            {
                                                element.Name = newName;
                                            }
                                        }

                                        foreach (var item in row)
                                        {
                                            if (item.Key == "图元ID" || item.Key == "类型名称 (Type)" || item.Value == null)
                                            {
                                                continue;
                                            }

                                            Parameter parameter = StableParameterKey.ResolveByDisplayLabel(element, item.Key);
                                            if (parameter == null)
                                            {
                                                throw new InvalidOperationException($"不存在唯一可识别的参数“{item.Key}”。若存在重复同名参数，请使用本工具重新导出的 Excel 表头。");
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
                                        failures.Add($"族类型 {idValue}：{rowException.Message}");
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
                            "族类型参数导入结果",
                            $"成功：{successCount} 行\n跳过/失败：{skippedCount} 行{detail}");
                        if (_selectedFamilyId != null) RequestRead(LoadTypeData, "刷新族类型数据");
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("错误", "导入失败：" + ex.Message));
                }
            });
            _externalEvent.Raise();
        }

        private void BatchRename_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Cast<TypeItemViewModel>(TypeDataGrid.SelectedItems));
            if (selectedItems.Count == 0)
            {
                TaskDialog.Show("提示", "请先在右侧列表中选择要重命名的族类型。");
                return;
            }
            
            var renameWin = new BatchRenameWindow { Owner = this };
            if (renameWin.ShowDialog() == true)
            {
                string prefix = renameWin.PrefixText;
                string suffix = renameWin.SuffixText;
                string find = renameWin.FindText;
                string replace = renameWin.ReplaceText;
                
                var ids = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(selectedItems, x => x.TypeId != ElementId.InvalidElementId), x => x.TypeId));
                
                _handler.SetAction(app =>
                {
                    try
                    {
                        var doc = app.ActiveUIDocument.Document;
                        int count = 0;
                        using (Transaction t = new Transaction(doc, "批量重命名族类型"))
                        {
                            t.Start();
                            foreach(var id in ids)
                            {
                                var elem = doc.GetElement(id);
                                if(elem != null)
                                {
                                    try {
                                        string oldName = elem.Name;
                                        string newName = oldName;
                                        
                                        if(!string.IsNullOrEmpty(find))
                                            newName = newName.Replace(find, replace ?? "");
                                        
                                        newName = prefix + newName + suffix;
                                        
                                        if(newName != oldName && !string.IsNullOrWhiteSpace(newName))
                                        {
                                            elem.Name = newName;
                                            count++;
                                        }
                                    } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] FamilyManagerWindow.xaml.cs: {0}", ex.Message); }
                                }
                            }
                            
                            if (renameWin.ApplyToFamily)
                            {
                                var families = ids.Select(id => (doc.GetElement(id) as FamilySymbol)?.Family)
                                                  .Where(f => f != null)
                                                  .GroupBy(f => f.Id).Select(g => g.First()).ToList();
                                foreach (var fam in families)
                                {
                                    try
                                    {
                                        string oldName = fam.Name;
                                        string newName = oldName;
                                        if(!string.IsNullOrEmpty(find))
                                            newName = newName.Replace(find, replace ?? "");
                                        newName = prefix + newName + suffix;
                                        if(newName != oldName && !string.IsNullOrWhiteSpace(newName))
                                        {
                                            fam.Name = newName;
                                        }
                                    }
                                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] FamilyManagerWindow.xaml.cs: {0}", ex.Message); }
                                }
                            }
                            
                            if (t.Commit() != TransactionStatus.Committed)
                                throw new InvalidOperationException("批量重命名事务未成功提交。");
                        }
                        
                        Dispatcher.Invoke(() =>
                        {
                            TaskDialog.Show("提示", $"成功重命名 {count} 个族类型！");
                            if (_selectedFamilyId != null) RequestRead(LoadTypeData, "刷新族类型数据");
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("错误", "Batch rename failed: " + ex.Message));
                    }
                });
                _externalEvent.Raise();
            }
        }

        private void RenameFamily_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFamilyId == null || _selectedFamilyId == ElementId.InvalidElementId) return;
            ElementId selectedFamilyId = _selectedFamilyId;

            var selected = FamilyListBox.SelectedItem as FamilyListItemViewModel;
            if (selected == null) return;

            var inputDialog = new SimpleInputDialog("重命名族", "请输入新的族名称：", selected.FamilyName) { Owner = this };
            if (inputDialog.ShowDialog() == true)
            {
                string newName = inputDialog.InputText;
                if (string.IsNullOrWhiteSpace(newName) || newName == selected.FamilyName) return;

                _handler.SetAction(app =>
                {
                    var doc = app.ActiveUIDocument.Document;
                    try
                    {
                        var fam = doc.GetElement(selectedFamilyId) as Family;
                        if (fam == null) return;

                        bool success = false;
                        using (Transaction t = new Transaction(doc, "重命名族"))
                        {
                            t.Start();
                            fam.Name = newName;
                            if (t.Commit() != TransactionStatus.Committed)
                                throw new InvalidOperationException("重命名族事务未成功提交。");
                            success = true;
                        }

                        if (success)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (_selectedFamilyId == selectedFamilyId)
                                {
                                    TypeList.Clear();
                                    TypeDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                                    EmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                                    SelectedFamilyTitle.Text = "请在左侧选择一个族";
                                    SelectedFamilyStatsText.Text = "实例 0 · 使用类型 0 · 未使用类型 0 · 当前视图 0";
                                    RenameFamilyBtn.Visibility = System.Windows.Visibility.Collapsed;
                                    ManageInstancesBtn.IsEnabled = false;
                                    _selectedFamilyId = null;
                                }
                                RequestRead(() =>
                                {
                                    LoadFamilyList();
                                    PopulateCategoryFilter();
                                }, "刷新族列表");
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("Error", "重命名失败: " + ex.Message));
                    }
                });
                _externalEvent.Raise();
            }
        }

        private void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = TypeDataGrid.SelectedItems.Cast<TypeItemViewModel>().ToList();
            if (selectedItems.Count != 1)
            {
                TaskDialog.Show("提示", "请选择且仅选择一个类型进行复制。 (Please select exactly one type to duplicate.)");
                return;
            }

            var vm = selectedItems[0];
            if (vm.TypeId == ElementId.InvalidElementId) return;
            ElementId sourceTypeId = vm.TypeId;
            ElementId sourceFamilyId = vm.FamilyId;
            string sourceTypeName = vm.TypeName;

            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var type = doc.GetElement(sourceTypeId) as ElementType;
                if (type == null) return;

                try
                {
                    ElementType newType = null;
                    var existingNames = new HashSet<string>(
                        new FilteredElementCollector(doc)
                            .WhereElementIsElementType()
                            .Select(element => element.Name),
                        StringComparer.OrdinalIgnoreCase);
                    string newName = GetUniqueName(existingNames, sourceTypeName + "_Copy");
                    using (Transaction t = new Transaction(doc, "复制族类型"))
                    {
                        t.Start();
                        newType = type.Duplicate(newName);
                        if (t.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("复制族类型事务未成功提交。");
                    }

                    if (newType != null)
                    {
                        Family family = newType is FamilySymbol symbol ? symbol.Family : doc.GetElement(sourceFamilyId) as Family;
                        FamilyUsageSnapshot freshUsage = FamilyUsageService.Build(doc, doc.ActiveView?.Id ?? ElementId.InvalidElementId);
                        int typeCount = family?.GetFamilySymbolIds().Count ?? 0;
                        int usedTypeCount = family?.GetFamilySymbolIds().Count(id => freshUsage.GetTypeCount(id) > 0) ?? 0;
                        int instanceCount = freshUsage.GetFamilyCount(sourceFamilyId);
                        int currentViewCount = freshUsage.GetCurrentViewFamilyCount(sourceFamilyId);
                        Dispatcher.Invoke(() =>
                        {
                            var currentFamily = FamilyList.FirstOrDefault(x => x.FamilyId == sourceFamilyId);
                            if (currentFamily != null)
                            {
                                currentFamily.TypeCount = typeCount;
                                currentFamily.InstanceCount = instanceCount;
                                currentFamily.CurrentViewInstanceCount = currentViewCount;
                                currentFamily.UsedTypeCount = usedTypeCount;
                                currentFamily.UnusedTypeCount = Math.Max(0, typeCount - usedTypeCount);
                                if (_selectedFamilyId == sourceFamilyId)
                                    SelectedFamilyStatsText.Text = $"实例 {instanceCount} · 使用类型 {usedTypeCount}/{typeCount} · 未使用类型 {Math.Max(0, typeCount - usedTypeCount)} · 当前视图 {currentViewCount}";
                            }
                            _familyCollectionView?.Refresh();
                            if (_selectedFamilyId == sourceFamilyId) RequestRead(LoadTypeData, "刷新复制后的族类型");
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not duplicate: " + ex.Message));
                }
            });

            _externalEvent.Raise();
        }

        private void ChangeCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFamilyId == null || _selectedFamilyId == ElementId.InvalidElementId) return;
            ElementId selectedFamilyId = _selectedFamilyId;

            RequestRead(() =>
            {
                var categories = new List<CategoryItem>();
                foreach (Category cat in _doc.Settings.Categories)
                {
                    if ((cat.CategoryType == CategoryType.Model || cat.CategoryType == CategoryType.Annotation)
                        && cat.CanAddSubcategory
                        && BuiltInCategoryHelper.TryFromCategory(cat, out _))
                    {
                        categories.Add(new CategoryItem { Name = cat.Name, Id = cat.Id });
                    }
                }

                categories = categories.OrderBy(c => c.Name).ToList();
                var selectWindow = new ParameterSelectWindow(categories.Select(c => c.Name).ToList())
                {
                    Title = "选择新类别",
                    Owner = this
                };

                if (selectWindow.ShowDialog() != true || selectWindow.SelectedParameters.Count == 0) return;

                string targetCatName = selectWindow.SelectedParameters[0];
                CategoryItem targetCatItem = categories.FirstOrDefault(c => c.Name == targetCatName);
                if (targetCatItem == null) return;

                ElementId catItemId = targetCatItem.Id;
                _handler.SetAction(app =>
                {
                    var doc = app.ActiveUIDocument.Document;
                    var fam = doc.GetElement(selectedFamilyId) as Family;
                    if (fam == null || !fam.IsEditable)
                    {
                        throw new InvalidOperationException("当前族不可编辑或已经不存在。");
                    }

                    if (!BuiltInCategoryHelper.TryFromCategoryId(catItemId, out BuiltInCategory bic))
                    {
                        throw new InvalidOperationException("目标类别不是可用于族文档的内置类别。");
                    }

                    Document familyDoc = doc.EditFamily(fam);
                    if (familyDoc == null)
                    {
                        throw new InvalidOperationException("无法打开族文档。");
                    }

                    try
                    {
                        using (Transaction t = new Transaction(familyDoc, "修改族类别"))
                        {
                            t.Start();
                            Category newCat = familyDoc.Settings.Categories.get_Item(bic);
                            if (newCat == null)
                            {
                                throw new InvalidOperationException("族模板不支持所选类别。");
                            }
                            familyDoc.OwnerFamily.FamilyCategory = newCat;
                            if (t.Commit() != TransactionStatus.Committed)
                                throw new InvalidOperationException("修改族类别事务未成功提交。");
                        }

                        // Revit API 要求目标项目文档此时不可处于可修改状态；
                        // 因此这里不能额外包项目 Transaction。
                        Family reloadedFamily = familyDoc.LoadFamily(doc, new FamilyLoadOptions());
                        if (reloadedFamily == null)
                            throw new InvalidOperationException("修改后的族未能重新载入项目。");
                    }
                    finally
                    {
                        familyDoc.Close(false);
                    }

                    LoadFamilyList();
                    PopulateCategoryFilter();
                    if (_selectedFamilyId == selectedFamilyId)
                    {
                        TypeList.Clear();
                        TypeDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                        EmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                        SelectedFamilyTitle.Text = "请在左侧选择一个族";
                        _selectedFamilyId = null;
                    }
                    TaskDialog.Show("完成", "族类别已修改并重新载入项目。");
                }, operationName: "修改族类别", showSuccess: false);
                _externalEvent.Raise();
            }, "读取可用族类别");
        }

        private void ManageInstances_Click(object sender, RoutedEventArgs e)
        {
            var selected = FamilyListBox.SelectedItem as FamilyListItemViewModel;
            if (selected == null || selected.FamilyId == ElementId.InvalidElementId) return;
            ElementId familyId = selected.FamilyId;

            ModelessWindowRegistry.ShowOrActivate(
                _doc,
                _uiapp.MainWindowHandle,
                () => new FamilyInstanceManagerWindow(_uiapp, familyId),
                existing => existing.SelectFamily(familyId));
        }

        private void ExportSelectedRfa_Click(object sender, RoutedEventArgs e)
        {
            var selectedFamilies = FamilyListBox.SelectedItems.Cast<FamilyListItemViewModel>().ToList();
            if (selectedFamilies.Count == 0 && FamilyListBox.SelectedItem is FamilyListItemViewModel current)
                selectedFamilies.Add(current);
            if (selectedFamilies.Count == 0)
            {
                TaskDialog.Show("提示", "请先选择至少一个族。按 Ctrl/Shift 可在左侧多选。 ");
                return;
            }

            string folder;
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择批量 RFA 导出文件夹";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                folder = dialog.SelectedPath;
            }

            var ids = selectedFamilies.Select(x => x.FamilyId).Where(x => x != ElementId.InvalidElementId).ToList();
            var existingFiles = selectedFamilies
                .Select(x => System.IO.Path.Combine(folder, SanitizeFileName(x.FamilyName) + ".rfa"))
                .Where(System.IO.File.Exists)
                .ToList();
            if (existingFiles.Count > 0)
            {
                TaskDialogResult overwrite = TaskDialog.Show(
                    "覆盖已有族文件",
                    $"目标目录中已有 {existingFiles.Count} 个同名 RFA。\n\n继续将覆盖这些文件，是否继续？",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (overwrite != TaskDialogResult.Yes) return;
            }

            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                int success = 0;
                var failures = new List<string>();

                foreach (ElementId familyId in ids)
                {
                    var family = doc.GetElement(familyId) as Family;
                    if (family == null || !family.IsEditable)
                    {
                        failures.Add($"{familyId.GetIdValue()}：族不存在或不可编辑。");
                        continue;
                    }

                    string savePath = System.IO.Path.Combine(folder, SanitizeFileName(family.Name) + ".rfa");
                    Document familyDoc = null;
                    try
                    {
                        familyDoc = doc.EditFamily(family);
                        if (familyDoc == null) throw new InvalidOperationException("无法打开族文档。");
                        familyDoc.SaveAs(savePath, new SaveAsOptions { OverwriteExistingFile = true });
                        success++;
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{family.Name}：{ex.Message}");
                    }
                    finally
                    {
                        try { familyDoc?.Close(false); } catch { }
                    }
                }

                string detail = failures.Count == 0 ? string.Empty :
                    "\n\n失败明细（最多 8 条）：\n" + string.Join("\n", failures.Take(8));
                TaskDialog.Show("批量导出 RFA", $"成功：{success}\n失败：{failures.Count}\n目录：{folder}{detail}");
            }, operationName: "批量导出 RFA", showSuccess: false);
            _externalEvent.Raise();
        }

        private void CleanupUnusedTypes_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFamilyId == null || _selectedFamilyId == ElementId.InvalidElementId)
            {
                TaskDialog.Show("提示", "请先选择一个族。");
                return;
            }

            ElementId selectedFamilyId = _selectedFamilyId;
            string familyName = (FamilyListBox.SelectedItem as FamilyListItemViewModel)?.FamilyName ?? "当前族";
            string operationId = Guid.NewGuid().ToString("N");

            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var family = doc.GetElement(selectedFamilyId) as Family;
                if (family == null) throw new InvalidOperationException("当前族已经不存在。");

                FamilyUsageSnapshot freshUsage = FamilyUsageService.Build(doc, doc.ActiveView?.Id ?? ElementId.InvalidElementId);
                var unusedIds = family.GetFamilySymbolIds().Where(id => freshUsage.GetTypeCount(id) == 0).ToList();
                if (unusedIds.Count == 0)
                {
                    TaskDialog.Show("清理未使用类型", "当前族没有未使用类型。");
                    return;
                }

                var extraIds = new List<ElementId>();
                using (Transaction trial = new Transaction(doc, "Trial Delete Unused Family Types"))
                {
                    if (trial.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动未使用类型删除预检事务。");
                    ICollection<ElementId> deleted = doc.Delete(unusedIds);
                    if (deleted != null)
                        extraIds = deleted.Where(id => !unusedIds.Contains(id)).ToList();
                    if (trial.RollBack() != TransactionStatus.RolledBack)
                        throw new InvalidOperationException("未使用类型删除预检未能回滚。");
                }

                if (extraIds.Count > 0)
                {
                    DiagnosticLog.WriteProductOperation(
                        "FamilyManager.CleanupUnusedTypes",
                        operationId,
                        "BLOCKED_CASCADE",
                        doc,
                        $"FamilyId={selectedFamilyId.GetIdValue()}; Unused={unusedIds.Count}; Cascade={extraIds.Count}; CascadeIds={string.Join(",", extraIds.Select(x => x.GetIdValue()).Take(20))}");
                    var details = new List<string>();
                    foreach (ElementId id in extraIds.Take(12))
                    {
                        Element element = doc.GetElement(id);
                        details.Add(element == null
                            ? $"- ElementId {id.GetIdValue()}"
                            : $"- [{element.Category?.Name ?? element.GetType().Name}] {element.Name} ({id.GetIdValue()})");
                    }
                    TaskDialog.Show(
                        "安全清理已阻止",
                        $"发现 {unusedIds.Count} 个未使用类型，但 Trial Delete 还会连带删除 {extraIds.Count} 个其它图元。\n\nV2.1 安全清理不会自动执行级联删除。\n\n" + string.Join("\n", details));
                    return;
                }

                string typeNames = string.Join("\n", unusedIds.Take(12).Select(id => "- " + (doc.GetElement(id)?.Name ?? id.GetIdValue().ToString())));
                if (unusedIds.Count > 12) typeNames += $"\n... 及其他 {unusedIds.Count - 12} 个";
                TaskDialogResult confirm = TaskDialog.Show(
                    "清理未使用类型",
                    $"族“{familyName}”有 {unusedIds.Count} 个使用数为 0 的类型。\n\n{typeNames}\n\n确认删除？一次 Ctrl+Z 可撤销本次清理。",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (confirm != TaskDialogResult.Yes) return;

                using (Transaction tx = new Transaction(doc, "清理未使用族类型"))
                {
                    if (tx.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动清理事务。");
                    doc.Delete(unusedIds);
                    if (tx.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("清理事务提交失败。");
                }

                DiagnosticLog.WriteProductOperation(
                    "FamilyManager.CleanupUnusedTypes",
                    operationId,
                    "COMMITTED",
                    doc,
                    $"FamilyId={selectedFamilyId.GetIdValue()}; DeletedTypeIds={string.Join(",", unusedIds.Select(x => x.GetIdValue()))}; Count={unusedIds.Count}");

                _usageSnapshot = FamilyUsageService.Build(doc, doc.ActiveView?.Id ?? ElementId.InvalidElementId);
                int remainingTypes = family.GetFamilySymbolIds().Count;
                int usedTypes = family.GetFamilySymbolIds().Count(id => _usageSnapshot.GetTypeCount(id) > 0);
                int instanceCount = _usageSnapshot.GetFamilyCount(selectedFamilyId);
                int currentViewCount = _usageSnapshot.GetCurrentViewFamilyCount(selectedFamilyId);
                Dispatcher.Invoke(() =>
                {
                    FamilyListItemViewModel item = FamilyList.FirstOrDefault(x => x.FamilyId == selectedFamilyId);
                    if (item != null)
                    {
                        item.TypeCount = remainingTypes;
                        item.InstanceCount = instanceCount;
                        item.CurrentViewInstanceCount = currentViewCount;
                        item.UsedTypeCount = usedTypes;
                        item.UnusedTypeCount = Math.Max(0, remainingTypes - usedTypes);
                        SelectedFamilyStatsText.Text = $"实例 {instanceCount} · 使用类型 {usedTypes}/{remainingTypes} · 未使用类型 {Math.Max(0, remainingTypes - usedTypes)} · 当前视图 {currentViewCount}";
                    }
                    _familyCollectionView?.Refresh();
                    RequestRead(LoadTypeData, "刷新清理后的族类型");
                    TaskDialog.Show("清理完成", $"已删除 {unusedIds.Count} 个未使用类型。\n一次 Ctrl+Z 可撤销。");
                });
            }, operationName: "清理未使用族类型", showSuccess: false, operationId: operationId);
            _externalEvent.Raise();
        }

        private void ExportRfa_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFamilyId == null || _selectedFamilyId == ElementId.InvalidElementId) return;
            ElementId selectedFamilyId = _selectedFamilyId;

            string folder;
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择 RFA 导出文件夹";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;
                folder = dialog.SelectedPath;
            }

            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument.Document;
                var family = doc.GetElement(selectedFamilyId) as Family;
                if (family == null || !family.IsEditable)
                    throw new InvalidOperationException("当前族不可编辑或已经不存在。");

                string fileName = SanitizeFileName(family.Name) + ".rfa";
                string savePath = System.IO.Path.Combine(folder, fileName);
                if (System.IO.File.Exists(savePath))
                {
                    TaskDialogResult overwrite = TaskDialog.Show(
                        "覆盖已有族文件",
                        $"文件已经存在：\n{savePath}\n\n是否覆盖？",
                        TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                    if (overwrite != TaskDialogResult.Yes)
                        return;
                }

                Document familyDoc = doc.EditFamily(family);
                if (familyDoc == null)
                    throw new InvalidOperationException("无法打开族文档。");

                try
                {
                    familyDoc.SaveAs(savePath, new SaveAsOptions { OverwriteExistingFile = true });
                }
                finally
                {
                    familyDoc.Close(false);
                }

                TaskDialog.Show("导出完成", $"族文件已导出：\n{savePath}");
            }, operationName: "导出 RFA", showSuccess: false);
            _externalEvent.Raise();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = TypeDataGrid.SelectedItems.Cast<TypeItemViewModel>().ToList();
            if (selectedItems.Count == 0)
            {
                // If no type selected, ask if they want to delete the whole family
                var resultFam = TaskDialog.Show("确认", "没有选中类型，是否删除当前选中的整个族？", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
                if (resultFam == TaskDialogResult.Yes && _selectedFamilyId != null)
                {
                    ElementId familyIdToDelete = _selectedFamilyId;
                    _handler.SetAction(app =>
                    {
                        var doc = app.ActiveUIDocument.Document;
                        try
                        {
                            using (Transaction t = new Transaction(doc, "Delete Family"))
                            {
                                t.Start();
                                doc.Delete(familyIdToDelete);
                                if (t.Commit() != TransactionStatus.Committed)
                                    throw new InvalidOperationException("删除族事务未成功提交。");
                            }
                            Dispatcher.Invoke(() => {
                                if (_selectedFamilyId == familyIdToDelete)
                                {
                                    TypeList.Clear();
                                    TypeDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                                    EmptyStatePanel.Visibility = System.Windows.Visibility.Visible;
                                    SelectedFamilyTitle.Text = "请在左侧选择一个族";
                                    SelectedFamilyStatsText.Text = "实例 0 · 使用类型 0 · 未使用类型 0 · 当前视图 0";
                                    ManageInstancesBtn.IsEnabled = false;
                                    _selectedFamilyId = null;
                                }
                                RequestRead(() => { LoadFamilyList(); PopulateCategoryFilter(); }, "刷新删除后的族列表");
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() => TaskDialog.Show("错误", "Could not delete: " + ex.Message));
                        }
                    });
                    _externalEvent.Raise();
                }
                return;
            }

            var typeIds = selectedItems.Where(x => x.TypeId != ElementId.InvalidElementId).Select(x => x.TypeId).ToList();
            ElementId familyId = selectedItems[0].FamilyId;

            var result = TaskDialog.Show("确认", $"确定要删除选中的 {selectedItems.Count} 个类型吗？", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);
            if (result == TaskDialogResult.Yes)
            {
                _handler.SetAction(app =>
                {
                    var doc = app.ActiveUIDocument.Document;
                    bool success = false;
                    int remainingTypeCount = 0;
                    try
                    {
                        using (Transaction t = new Transaction(doc, "Delete Types"))
                        {
                            t.Start();
                            foreach (var id in typeIds)
                            {
                                if (doc.GetElement(id) != null)
                                    doc.Delete(id);
                            }
                            if (t.Commit() != TransactionStatus.Committed)
                                throw new InvalidOperationException("删除族类型事务未成功提交。");
                            success = true;
                            var family = doc.GetElement(familyId) as Family;
                            remainingTypeCount = family?.GetFamilySymbolIds().Count ?? 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("错误", "Could not delete: " + ex.Message));
                    }

                    if (success)
                    {
                        FamilyUsageSnapshot freshUsage = FamilyUsageService.Build(doc, doc.ActiveView?.Id ?? ElementId.InvalidElementId);
                        Family remainingFamily = doc.GetElement(familyId) as Family;
                        int usedTypeCount = remainingFamily?.GetFamilySymbolIds().Count(id => freshUsage.GetTypeCount(id) > 0) ?? 0;
                        int instanceCount = freshUsage.GetFamilyCount(familyId);
                        int currentViewCount = freshUsage.GetCurrentViewFamilyCount(familyId);
                        Dispatcher.Invoke(() =>
                        {
                            var currentFamily = FamilyList.FirstOrDefault(x => x.FamilyId == familyId);
                            if (currentFamily != null)
                            {
                                currentFamily.TypeCount = remainingTypeCount;
                                currentFamily.InstanceCount = instanceCount;
                                currentFamily.CurrentViewInstanceCount = currentViewCount;
                                currentFamily.UsedTypeCount = usedTypeCount;
                                currentFamily.UnusedTypeCount = Math.Max(0, remainingTypeCount - usedTypeCount);
                                if (_selectedFamilyId == familyId)
                                    SelectedFamilyStatsText.Text = $"实例 {instanceCount} · 使用类型 {usedTypeCount}/{remainingTypeCount} · 未使用类型 {Math.Max(0, remainingTypeCount - usedTypeCount)} · 当前视图 {currentViewCount}";
                            }
                            _familyCollectionView?.Refresh();
                            if (_selectedFamilyId == familyId) RequestRead(LoadTypeData, "刷新删除后的族类型");
                        });
                    }
                });
                _externalEvent.Raise();
            }
        }

        private static string GetUniqueName(ISet<string> existingNames, string baseName)
        {
            string candidate = baseName;
            int index = 2;
            while (existingNames.Contains(candidate))
                candidate = baseName + "_" + index++;
            return candidate;
        }

        private static string SanitizeFileName(string name)
        {
            string safe = string.Join("_", (name ?? string.Empty).Split(System.IO.Path.GetInvalidFileNameChars()));
            return string.IsNullOrWhiteSpace(safe) ? "Family" : safe.Trim();
        }

    }

    public class FamilyListItemViewModel : INotifyPropertyChanged
    {
        public string Category { get; set; }
        public string FamilyName { get; set; }
        public ElementId FamilyId { get; set; }

        private int _typeCount;
        public int TypeCount { get => _typeCount; set { _typeCount = value; OnPropertyChanged(nameof(TypeCount)); } }
        private int _instanceCount;
        public int InstanceCount { get => _instanceCount; set { _instanceCount = value; OnPropertyChanged(nameof(InstanceCount)); } }
        private int _currentViewInstanceCount;
        public int CurrentViewInstanceCount { get => _currentViewInstanceCount; set { _currentViewInstanceCount = value; OnPropertyChanged(nameof(CurrentViewInstanceCount)); } }
        private int _usedTypeCount;
        public int UsedTypeCount { get => _usedTypeCount; set { _usedTypeCount = value; OnPropertyChanged(nameof(UsedTypeCount)); } }
        private int _unusedTypeCount;
        public int UnusedTypeCount { get => _unusedTypeCount; set { _unusedTypeCount = value; OnPropertyChanged(nameof(UnusedTypeCount)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class TypeItemViewModel : INotifyPropertyChanged
    {
        private string _typeName;
        public string TypeName { get => _typeName; set { _typeName = value; OnPropertyChanged(nameof(TypeName)); } }
        public ElementId FamilyId { get; set; }
        public ElementId TypeId { get; set; }

        private int _usageCount;
        public int UsageCount { get => _usageCount; set { _usageCount = value; OnPropertyChanged(nameof(UsageCount)); OnPropertyChanged(nameof(UsageStatus)); } }
        public string UsageStatus => UsageCount == 0 ? "未使用" : "使用中";

        public Dictionary<string, ParameterViewModel> Parameters { get; set; } = new Dictionary<string, ParameterViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class CategoryItem
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public class FamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }

    }
}
