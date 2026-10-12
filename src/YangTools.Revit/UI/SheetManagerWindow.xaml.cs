using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public partial class SheetManagerWindow : Window, INotifyPropertyChanged
    {
        private UIApplication _uiapp;
        private Document _doc;
        private ExternalEvent _externalEvent;
        private RevitEventHandler _handler;
        private ExternalEvent _readExternalEvent;
        private RevitReadEventHandler _readHandler;
        private bool _isInitialized;
        
        public ObservableCollection<SheetItemViewModel> Sheets { get; set; } = new ObservableCollection<SheetItemViewModel>();
        public ObservableCollection<TitleblockItemViewModel> Titleblocks { get; set; } = new ObservableCollection<TitleblockItemViewModel>();
        
        private List<TitleblockTypeOption> _availableTitleblocks = new List<TitleblockTypeOption>();
        public List<TitleblockTypeOption> AvailableTitleblocks
        {
            get => _availableTitleblocks;
            set { _availableTitleblocks = value; OnPropertyChanged(nameof(AvailableTitleblocks)); }
        }

        private List<string> _addedSheetParameters = new List<string>();
        private List<string> _titleblockParameterColumns = new List<string>();
        private readonly HashSet<long> _pendingTitleblockTypeChanges = new HashSet<long>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public SheetManagerWindow(UIApplication uiapp)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            this.DataContext = this;
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;

            _handler = new RevitEventHandler(_doc, wrapInTransactionGroup: false, showAutomaticSuccess: false);
            _externalEvent = ExternalEvent.Create(_handler);
            _readHandler = new RevitReadEventHandler(_doc);
            _readExternalEvent = ExternalEvent.Create(_readHandler);
            
            SheetsDataGrid.ItemsSource = Sheets;
            TitleblocksDataGrid.ItemsSource = Titleblocks;

            _isInitialized = true;
            LoadSheets();
            LoadAvailableTitleblocks();
        }

        private void RequestRead(Action action, string operationName)
        {
            if (!_isInitialized || _readHandler == null || _readExternalEvent == null)
                return;

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
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || _readHandler == null || _readExternalEvent == null)
                return;

            if (e.Source is TabControl && e.AddedItems.Count > 0)
            {
                var tab = e.AddedItems[0] as TabItem;
                if (tab != null)
                {
                    if (tab.Header.ToString().Contains("图框"))
                    {
                        RequestRead(LoadTitleblocks, "加载图框");
                    }
                    else if (tab.Header.ToString().Contains("图纸"))
                    {
                        RequestRead(LoadSheets, "加载图纸");
                    }
                }
            }
        }

        #region 图纸管理 (Sheet Management)

        private void LoadSheets()
        {
            _revisionCache.Clear();
            Sheets.Clear();
            var sheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .OrderBy(s => s.SheetNumber)
                .ToList();

            int index = 1;
            foreach (var s in sheets)
            {
                var vm = new SheetItemViewModel
                {
                    Index = index++,
                    SheetId = s.Id,
                    SheetNumber = s.SheetNumber,
                    SheetName = s.Name,
                    Revisions = GetRevisionNames(s)
                };

                vm.OnSheetInfoChanged = (prop, newVal) =>
                {
                    var sheetId = vm.SheetId;
                    _handler.SetAction(app =>
                    {
                        var doc = app.ActiveUIDocument.Document;
                        var sheet = doc.GetElement(sheetId) as ViewSheet;
                        if (sheet == null)
                            throw new InvalidOperationException("图纸已经不存在，请刷新列表。");

                        string oldValue = prop == "SheetNumber" ? sheet.SheetNumber : sheet.Name;
                        try
                        {
                            using (Transaction t = new Transaction(doc, "修改图纸信息"))
                            {
                                t.Start();
                                if (prop == "SheetNumber" && sheet.SheetNumber != newVal) sheet.SheetNumber = newVal;
                                else if (prop == "SheetName" && sheet.Name != newVal) sheet.Name = newVal;
                                t.Commit();
                            }
                        }
                        catch
                        {
                            Dispatcher.Invoke(() => vm.SetSheetInfoSilently(prop, oldValue));
                            throw;
                        }
                    }, operationName: "修改图纸信息", showSuccess: false);
                    _externalEvent.Raise();
                };

                foreach (var paramName in _addedSheetParameters)
                {
                    var p = s.LookupParameter(paramName);
                    string val = ParameterValueHelper.GetDisplayValue(p);
                    var pItem = new ParameterItem { Name = paramName, Value = val };
                    pItem.OnValueChanged = (pName, newVal) =>
                    {
                        var sheetId = vm.SheetId;
                        _handler.SetAction(app =>
                        {
                            var doc = app.ActiveUIDocument.Document;
                            var sheet = doc.GetElement(sheetId) as ViewSheet;
                            var param = sheet?.LookupParameter(pName);
                            if (sheet == null || param == null || param.IsReadOnly)
                                throw new InvalidOperationException($"图纸参数“{pName}”已经不存在或不可写。");

                            string oldValue = ParameterValueHelper.GetDisplayValue(param);
                            try
                            {
                                using (Transaction t = new Transaction(doc, "修改图纸参数"))
                                {
                                    t.Start();
                                    if (!ParameterValueHelper.TrySetFromDisplayValue(param, newVal, out string setError))
                                        throw new InvalidOperationException(setError);
                                    t.Commit();
                                }
                            }
                            catch
                            {
                                Dispatcher.Invoke(() => pItem.SetValueSilently(oldValue));
                                throw;
                            }
                        }, operationName: "修改图纸参数", showSuccess: false);
                        _externalEvent.Raise();
                    };
                    vm.Parameters[paramName] = pItem;
                }

                Sheets.Add(vm);
            }
            SheetStatusBarText.Text = $"共计 {Sheets.Count} 张图纸";
        }

        private void AddParameterColumn_Click(object sender, RoutedEventArgs e)
        {
            RequestRead(() =>
            {
                var sheet = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .WhereElementIsNotElementType()
                    .FirstElement() as ViewSheet;
                if (sheet == null)
                {
                    TaskDialog.Show("提示", "当前项目中没有图纸。");
                    return;
                }

                var paramNames = new List<string>();
                foreach (Parameter parameter in sheet.Parameters)
                {
                    string name = parameter.Definition?.Name;
                    if (!string.IsNullOrWhiteSpace(name)) paramNames.Add(name);
                }

                var dialog = new ParameterSelectWindow(paramNames.Distinct().OrderBy(x => x).ToList())
                {
                    Owner = this
                };

                if (dialog.ShowDialog() != true) return;

                foreach (string selectedName in dialog.SelectedParameters)
                {
                    if (_addedSheetParameters.Contains(selectedName)) continue;

                    _addedSheetParameters.Add(selectedName);
                    SheetsDataGrid.Columns.Add(new DataGridTextColumn
                    {
                        Header = selectedName,
                        Binding = new System.Windows.Data.Binding($"Parameters[{selectedName}].Value")
                        {
                            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                        },
                        Width = DataGridLength.Auto
                    });
                }

                LoadSheets();
            }, "读取图纸参数");
        }

        // CellEditEnding removed to follow MVVM

        private Dictionary<ElementId, string> _revisionCache = new Dictionary<ElementId, string>();

        private string GetRevisionNames(ViewSheet sheet)
        {
            var revIds = sheet.GetAllRevisionIds();
            var names = new List<string>();
            foreach (var id in revIds)
            {
                if (!_revisionCache.ContainsKey(id))
                {
                    if (_doc.GetElement(id) is Revision rev)
                    {
                        _revisionCache[id] = rev.RevisionDate + " " + rev.Description;
                    }
                    else
                    {
                        _revisionCache[id] = "";
                    }
                }
                
                string name = _revisionCache[id];
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }
            return string.Join("; ", names);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            foreach (var col in SheetsDataGrid.Columns)
            {
                col.Visibility = System.Windows.Visibility.Visible;
            }
            RequestRead(LoadSheets, "刷新图纸");
        }

        private void DeleteSheets_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = SheetsDataGrid.SelectedItems.Cast<SheetItemViewModel>().ToList();
            if (selectedItems.Count == 0) return;

            var ids = selectedItems.Select(x => x.SheetId).ToList();
            
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
                            throw new InvalidOperationException("无法启动图纸删除事务组。");
                        List<ElementId> extraIds = new List<ElementId>();
                        using (Transaction t1 = new Transaction(doc, "Trial Delete"))
                        {
                            if (t1.Start() != TransactionStatus.Started)
                                throw new InvalidOperationException("无法启动图纸删除预检事务。");
                            var deleted = doc.Delete(ids);
                            actualDeleteCount = deleted?.Count ?? 0;
                            if (deleted != null) extraIds = deleted.Where(id => !ids.Contains(id)).ToList();
                            if (t1.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("图纸删除预检事务未能回滚。");
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
                                    System.Diagnostics.Debug.WriteLine("[YangTools] SheetManagerWindow.xaml.cs: {0}", ex.Message);
                                }
                            }
                            if (extraIds.Count > 15)
                                extraDetails.AppendLine($"... 及其他 {extraIds.Count - 15} 个图元");

                            confirmDialog.MainInstruction = $"您选择的 {ids.Count} 张图纸将连带删除 {extraIds.Count} 个图元！";
                            confirmDialog.MainContent = "连带删除的通常是图纸上的视图、明细表、图框等图元。\n\n图元如下：\n\n" + extraDetails;
                        }
                        else
                        {
                            confirmDialog.MainInstruction = $"确定删除选中的 {ids.Count} 张图纸吗？";
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
                                    throw new InvalidOperationException("无法启动图纸删除事务。");
                                doc.Delete(ids);
                                if (t2.Commit() != TransactionStatus.Committed)
                                    throw new InvalidOperationException("图纸删除事务未能提交。");
                            }
                            TransactionStatus groupStatus = tg.Assimilate();
                            if (groupStatus != TransactionStatus.Committed)
                            {
                                if (tg.GetStatus() == TransactionStatus.Started) tg.RollBack();
                                throw new InvalidOperationException("图纸删除事务组合并失败。");
                            }
                            Dispatcher.Invoke(() => LoadSheets());
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
            }, operationName: "删除图纸", showSuccess: true);
            _externalEvent.Raise();
        }

        private void EditRevisions_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var viewModel = button?.Tag as SheetItemViewModel;
            if (viewModel == null) return;

            RequestRead(() =>
            {
                var sheet = _doc.GetElement(viewModel.SheetId) as ViewSheet;
                if (sheet == null)
                {
                    TaskDialog.Show("提示", "所选图纸已经不存在，请刷新列表。");
                    return;
                }

                var revisionWindow = new RevisionsWindow(_doc, sheet) { Owner = this };
                if (revisionWindow.ShowDialog() != true) return;

                var selectedRevisionIds = revisionWindow.SelectedRevisionIds.ToList();
                ElementId sheetId = viewModel.SheetId;

                _handler.SetAction(app =>
                {
                    var doc = app.ActiveUIDocument.Document;
                    var targetSheet = doc.GetElement(sheetId) as ViewSheet;
                    if (targetSheet == null)
                    {
                        throw new InvalidOperationException("所选图纸已经不存在。");
                    }

                    using (Transaction transaction = new Transaction(doc, "更新图纸修订"))
                    {
                        transaction.Start();
                        targetSheet.SetAdditionalRevisionIds(selectedRevisionIds);
                        transaction.Commit();
                    }

                    _revisionCache.Clear();
                    viewModel.Revisions = GetRevisionNames(targetSheet);
                }, operationName: "更新图纸修订", showSuccess: false);
                _externalEvent.Raise();
            }, "读取图纸修订");
        }

        private void ExportSheetExcel_Click(object sender, RoutedEventArgs e)
        {
            if (Sheets.Count == 0) return;
            var filePath = ExcelService.ShowSaveDialog("Sheets.xlsx");
            if (filePath == null) return;

            var data = new List<Dictionary<string, object>>();
            foreach (var item in Sheets)
            {
                var row = new Dictionary<string, object>();
                row["图号 (Sheet Number)"] = item.SheetNumber;
                row["图名 (Sheet Name)"] = item.SheetName;

                foreach (var col in _addedSheetParameters)
                {
                    if (string.IsNullOrEmpty(col)) continue;
                    row[col] = item.Parameters.ContainsKey(col) ? item.Parameters[col].Value : "";
                }
                data.Add(row);
            }
            ExcelService.ExportToExcel(filePath, data);
        }

        private void ImportSheetExcel_Click(object sender, RoutedEventArgs e)
        {
            var filePath = ExcelService.ShowOpenDialog();
            if (filePath == null) return;

            _handler.SetAction(app =>
            {
                try
                {
                    List<IDictionary<string, object>> rows = ExcelService.ReadExcel(filePath).ToList();
                    var doc = app.ActiveUIDocument.Document;
                    int successCount = 0;
                    int createdCount = 0;
                    int updatedCount = 0;
                    int skippedCount = 0;
                    var failures = new List<string>();

                    ElementId defaultTitleblockId = ElementId.InvalidElementId;
                    FamilySymbol defaultTitleblock = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_TitleBlocks)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .FirstOrDefault();
                    if (defaultTitleblock != null)
                        defaultTitleblockId = defaultTitleblock.Id;

                    Dictionary<string, List<ViewSheet>> existingSheetsCache = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheet))
                        .Cast<ViewSheet>()
                        .GroupBy(sheet => sheet.SheetNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList(), StringComparer.OrdinalIgnoreCase);

                    using (TransactionGroup group = new TransactionGroup(doc, "批量导入建图/更新"))
                    {
                        if (group.Start() != TransactionStatus.Started)
                            throw new InvalidOperationException("无法启动图纸导入事务组。");

                        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                        {
                            IDictionary<string, object> row = rows[rowIndex];
                            if (!row.TryGetValue("图号 (Sheet Number)", out object numberValue)
                                || string.IsNullOrWhiteSpace(numberValue?.ToString()))
                            {
                                skippedCount++;
                                continue;
                            }

                            string sheetNumber = numberValue.ToString().Trim();
                            string sheetName = row.TryGetValue("图名 (Sheet Name)", out object nameValue)
                                ? nameValue?.ToString()?.Trim() ?? string.Empty
                                : string.Empty;

                            existingSheetsCache.TryGetValue(sheetNumber, out List<ViewSheet> matchingSheets);
                            matchingSheets = matchingSheets ?? new List<ViewSheet>();
                            if (matchingSheets.Count > 1)
                            {
                                failures.Add($"第 {rowIndex + 2} 行，图号“{sheetNumber}”在项目中对应 {matchingSheets.Count} 张图纸，无法确定更新目标。");
                                continue;
                            }

                            bool created = false;
                            ViewSheet targetSheet = matchingSheets.FirstOrDefault();
                            using (Transaction rowTransaction = new Transaction(doc, $"导入图纸：{sheetNumber}"))
                            {
                                try
                                {
                                    if (rowTransaction.Start() != TransactionStatus.Started)
                                        throw new InvalidOperationException("无法启动该行事务。");

                                    if (targetSheet == null)
                                    {
                                        targetSheet = ViewSheet.Create(doc, defaultTitleblockId);
                                        if (targetSheet == null)
                                            throw new InvalidOperationException("Revit 未能创建新图纸。");

                                        targetSheet.SheetNumber = sheetNumber;
                                        targetSheet.Name = string.IsNullOrWhiteSpace(sheetName) ? "未命名图纸" : sheetName;
                                        created = true;
                                    }
                                    else if (!string.IsNullOrWhiteSpace(sheetName) && targetSheet.Name != sheetName)
                                    {
                                        targetSheet.Name = sheetName;
                                    }

                                    foreach (KeyValuePair<string, object> kvp in row)
                                    {
                                        if (kvp.Key == "图号 (Sheet Number)"
                                            || kvp.Key == "图名 (Sheet Name)"
                                            || kvp.Value == null)
                                        {
                                            continue;
                                        }

                                        Parameter parameter = targetSheet.LookupParameter(kvp.Key);
                                        if (parameter == null || parameter.IsReadOnly)
                                            continue;

                                        if (!ParameterValueHelper.TrySetFromDisplayValue(
                                                parameter,
                                                kvp.Value.ToString(),
                                                out string setError))
                                        {
                                            throw new InvalidOperationException($"参数“{kvp.Key}”写入失败：{setError}");
                                        }
                                    }

                                    TransactionStatus rowStatus = rowTransaction.Commit();
                                    if (rowStatus != TransactionStatus.Committed)
                                        throw new InvalidOperationException($"该行事务未提交，状态：{rowStatus}。");

                                    if (created)
                                    {
                                        existingSheetsCache[sheetNumber] = new List<ViewSheet> { targetSheet };
                                        createdCount++;
                                    }
                                    else
                                    {
                                        updatedCount++;
                                    }
                                    successCount++;
                                }
                                catch (Exception ex)
                                {
                                    try
                                    {
                                        if (rowTransaction.GetStatus() == TransactionStatus.Started)
                                            rowTransaction.RollBack();
                                    }
                                    catch { }

                                    failures.Add($"第 {rowIndex + 2} 行，图号“{sheetNumber}”：{ex.Message}");
                                }
                            }
                        }

                        if (successCount > 0)
                        {
                            TransactionStatus groupStatus = group.Assimilate();
                            if (groupStatus != TransactionStatus.Committed)
                                throw new InvalidOperationException($"图纸导入事务组合并失败，状态：{groupStatus}。");
                        }
                        else if (group.GetStatus() == TransactionStatus.Started)
                        {
                            group.RollBack();
                        }
                    }

                    Dispatcher.Invoke(() =>
                    {
                        LoadSheets();
                        var summary = new System.Text.StringBuilder();
                        summary.AppendLine("图纸导入完成。");
                        summary.AppendLine($"成功：{successCount} 行（新建 {createdCount}，更新 {updatedCount}）");
                        summary.AppendLine($"跳过空行：{skippedCount}");
                        summary.AppendLine($"失败：{failures.Count}");
                        if (failures.Count > 0)
                        {
                            summary.AppendLine();
                            summary.AppendLine("主要失败项：");
                            foreach (string failure in failures.Take(8))
                                summary.AppendLine("• " + failure);
                            if (failures.Count > 8)
                                summary.AppendLine($"• 另有 {failures.Count - 8} 项，请查看诊断日志。");
                        }
                        TaskDialog.Show(failures.Count == 0 ? "导入完成" : "导入完成（存在失败）", summary.ToString().TrimEnd());
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("错误", "导入失败：" + ex.Message));
                }
            }, operationName: "导入图纸参数", showSuccess: false);
            _externalEvent.Raise();
        }

        #endregion

        #region 图框管理 (Titleblock Management)

        private void LoadAvailableTitleblocks()
        {
            AvailableTitleblocks = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .OrderBy(symbol => symbol.FamilyName)
                .ThenBy(symbol => symbol.Name)
                .Select(symbol => new TitleblockTypeOption
                {
                    Id = symbol.Id,
                    Name = $"{symbol.FamilyName} : {symbol.Name}"
                })
                .ToList();
        }

        private void LoadTitleblocks()
        {
            Titleblocks.Clear();
            
            // 找到所有的图框实例
            var titleblockInstances = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            // 提取参数列 (过滤掉空值或无关紧要的)
            var paramSet = new HashSet<string>();

            foreach (var inst in titleblockInstances)
            {
                var sheet = _doc.GetElement(inst.OwnerViewId) as ViewSheet;
                if (sheet == null) continue;

                var vm = new TitleblockItemViewModel
                {
                    InstanceId = inst.Id,
                    InstanceIdValue = inst.Id.GetIdValue(),
                    SheetId = sheet.Id,
                    SheetNumber = sheet.SheetNumber,
                    SheetName = sheet.Name,
                    TypeId = inst.Symbol.Id,
                    TypeName = inst.Symbol.Name
                };

                foreach (Parameter p in inst.Parameters)
                {
                    if (p.IsReadOnly || string.IsNullOrEmpty(p.Definition.Name)) continue;
                    if (p.StorageType == StorageType.ElementId || p.StorageType == StorageType.None) continue;
                    
                    string pName = p.Definition.Name;
                    paramSet.Add(pName);
                    var parameterItem = new ParameterItem
                    {
                        Name = pName,
                        Value = ParameterValueHelper.GetDisplayValue(p)
                    };
                    var instanceId = inst.Id;
                    parameterItem.OnValueChanged = (parameterName, newValue) =>
                    {
                        _handler.SetAction(app =>
                        {
                            var doc = app.ActiveUIDocument.Document;
                            var titleblock = doc.GetElement(instanceId);
                            var parameter = titleblock?.LookupParameter(parameterName);
                            if (parameter == null || parameter.IsReadOnly)
                                throw new InvalidOperationException($"图框参数“{parameterName}”已经不存在或不可写。");

                            string oldValue = ParameterValueHelper.GetDisplayValue(parameter);
                            try
                            {
                                using (Transaction transaction = new Transaction(doc, "修改图框参数"))
                                {
                                    transaction.Start();
                                    if (!ParameterValueHelper.TrySetFromDisplayValue(parameter, newValue, out string setError))
                                        throw new InvalidOperationException(setError);
                                    transaction.Commit();
                                }
                            }
                            catch
                            {
                                Dispatcher.Invoke(() => parameterItem.SetValueSilently(oldValue));
                                throw;
                            }
                        }, operationName: "修改图框参数", showSuccess: false);
                        _externalEvent.Raise();
                    };
                    vm.Parameters[pName] = parameterItem;
                }

                Titleblocks.Add(vm);
            }

            // 清理上一轮的动态列，避免切换项目内容后重复累积。
            var previousDynamicColumns = new HashSet<string>(_titleblockParameterColumns);
            var colsToRemove = TitleblocksDataGrid.Columns
                .Where(c => c.Header is string header && previousDynamicColumns.Contains(header))
                .ToList();
            foreach (var c in colsToRemove) TitleblocksDataGrid.Columns.Remove(c);

            _titleblockParameterColumns = paramSet.OrderBy(x => x).ToList();

            // 重新添加动态列
            foreach (var colName in _titleblockParameterColumns)
            {
                var col = new DataGridTextColumn
                {
                    Header = colName,
                    Binding = new System.Windows.Data.Binding($"Parameters[{colName}].Value") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                    Width = DataGridLength.Auto
                };
                TitleblocksDataGrid.Columns.Add(col);
            }

            TitleblockStatusBarText.Text = $"共计 {Titleblocks.Count} 个图框实例";
        }

        private void TitleblockComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(e.AddedItems.Count > 0 && e.AddedItems[0] is TitleblockTypeOption selectedType)) return;

            var comboBox = sender as System.Windows.Controls.ComboBox;
            var viewModel = comboBox?.DataContext as TitleblockItemViewModel;
            if (viewModel == null || viewModel.TypeId == selectedType.Id) return;

            ElementId instanceId = viewModel.InstanceId;
            long instanceKey = instanceId.GetIdValue();
            if (!_pendingTitleblockTypeChanges.Add(instanceKey))
            {
                // The previous type change for this instance is still in the ExternalEvent queue.
                // Revert the visual selection instead of queuing a second stale request.
                comboBox.SelectedValue = viewModel.TypeId;
                return;
            }

            ElementId newTypeId = selectedType.Id;
            ElementId oldTypeId = viewModel.TypeId;
            string oldTypeName = viewModel.TypeName;
            _handler.SetAction(app =>
            {
                try
                {
                    var doc = app.ActiveUIDocument.Document;
                    var instance = doc.GetElement(instanceId) as FamilyInstance;
                    var newSymbol = doc.GetElement(newTypeId) as FamilySymbol;
                    if (instance == null || newSymbol == null)
                        throw new InvalidOperationException("图框实例或目标类型已经不存在，请刷新列表。");

                    using (Transaction transaction = new Transaction(doc, "更换图框类型"))
                    {
                        if (transaction.Start() != TransactionStatus.Started)
                            throw new InvalidOperationException("无法启动图框换型事务。");
                        if (!newSymbol.IsActive) newSymbol.Activate();
                        instance.Symbol = newSymbol;
                        TransactionStatus status = transaction.Commit();
                        if (status != TransactionStatus.Committed)
                            throw new InvalidOperationException($"图框换型事务未提交，状态：{status}。");
                    }

                    Dispatcher.Invoke(() =>
                    {
                        viewModel.TypeId = newSymbol.Id;
                        viewModel.TypeName = newSymbol.Name;
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        viewModel.TypeId = oldTypeId;
                        viewModel.TypeName = oldTypeName;
                        TaskDialog.Show("错误", "图框换型失败：" + ex.Message);
                    });
                }
                finally
                {
                    Dispatcher.Invoke(() =>
                    {
                        _pendingTitleblockTypeChanges.Remove(instanceKey);
                        comboBox.SelectedValue = viewModel.TypeId;
                    });
                }
            }, operationName: "更换图框类型", showSuccess: false);
            _externalEvent.Raise();
        }

        private void ExportTitleblockExcel_Click(object sender, RoutedEventArgs e)
        {
            if (Titleblocks.Count == 0) return;
            var filePath = ExcelService.ShowSaveDialog("Titleblocks.xlsx");
            if (filePath == null) return;

            var data = new List<Dictionary<string, object>>();
            foreach (var item in Titleblocks)
            {
                var row = new Dictionary<string, object>();
                row["图元ID"] = item.InstanceIdValue.ToString();
                row["图纸号"] = item.SheetNumber;
                row["图纸名"] = item.SheetName;
                row["图框类型"] = item.TypeName;

                foreach (var col in _titleblockParameterColumns)
                {
                    row[col] = item.Parameters.ContainsKey(col) ? item.Parameters[col].Value : "";
                }
                data.Add(row);
            }
            ExcelService.ExportToExcel(filePath, data);
        }

        private void ImportTitleblockExcel_Click(object sender, RoutedEventArgs e)
        {
            var filePath = ExcelService.ShowOpenDialog();
            if (filePath == null) return;

            _handler.SetAction(app =>
                {
                    try
                    {
                        var rows = ExcelService.ReadExcel(filePath);
                        var doc = app.ActiveUIDocument.Document;
                        int successCount = 0;

                        using (Transaction t = new Transaction(doc, "批量导入图框参数"))
                        {
                            t.Start();
                            foreach (var row in rows)
                            {
                                if (!row.ContainsKey("图元ID") || string.IsNullOrEmpty(row["图元ID"]?.ToString())) continue;
                                
                                if (long.TryParse(row["图元ID"].ToString(), out long idVal))
                                {
                                    ElementId eId = YangTools.Revit.Core.ElementIdExtensions.CreateId(idVal);
                                    var elem = doc.GetElement(eId);
                                    if (elem == null) continue;

                                    foreach (var kvp in row)
                                    {
                                        if (kvp.Key == "图元ID" || kvp.Key == "图纸号" || kvp.Key == "图纸名" || kvp.Key == "图框类型" || kvp.Value == null) continue;
                                        
                                        var p = elem.LookupParameter(kvp.Key);
                                        if (p != null && !p.IsReadOnly)
                                        {
                                            try
                                            {
                                                if (!ParameterValueHelper.TrySetFromDisplayValue(p, kvp.Value.ToString(), out string setError))
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"[YangTools] 参数 {kvp.Key} 导入失败: {setError}");
                                                }
                                            } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] SheetManagerWindow.xaml.cs: {0}", ex.Message); }
                                        }
                                    }
                                    successCount++;
                                }
                            }
                            t.Commit();
                        }

                        Dispatcher.Invoke(() =>
                        {
                            LoadTitleblocks();
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => TaskDialog.Show("错误", "Import failed: " + ex.Message));
                    }
                }, operationName: "导入图框参数", showSuccess: true);
                _externalEvent.Raise();
            }

        private void SelectTitleblockInView_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = TitleblocksDataGrid.SelectedItems.Cast<TitleblockItemViewModel>().ToList();
            if (selectedItems.Count == 0) return;
            
            var ids = selectedItems.Where(x => x.InstanceId != ElementId.InvalidElementId).Select(x => x.InstanceId).ToList();
            _handler.SetAction(app =>
            {
                var uidoc = app.ActiveUIDocument;
                uidoc.Selection.SetElementIds(ids);
                uidoc.ShowElements(ids);
            }, operationName: "在视图中定位图框", showSuccess: true);
            _externalEvent.Raise();
        }

        private void RefreshTitleblock_Click(object sender, RoutedEventArgs e)
        {
            foreach (var col in TitleblocksDataGrid.Columns)
            {
                col.Visibility = System.Windows.Visibility.Visible;
            }
            RequestRead(LoadTitleblocks, "刷新图框");
        }

        private void HideColumn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu)
            {
                var header = contextMenu.PlacementTarget as System.Windows.Controls.Primitives.DataGridColumnHeader;
                if (header != null && header.Column != null)
                {
                    // 保护核心列：禁止隐藏 序号/图号/图名/图框类型
                    string headerText = header.Column.Header?.ToString() ?? "";
                    if (headerText == "#" || headerText.Contains("序号") || headerText.Contains("图号") || headerText.Contains("图名") || headerText.Contains("图框"))
                        return;
                    header.Column.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }

        #endregion
    }

}
