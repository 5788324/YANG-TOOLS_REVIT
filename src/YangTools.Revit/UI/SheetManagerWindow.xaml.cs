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
using YangTools.Revit.Core.Sheets;
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

        private void ResetPendingTitleblockChange(long key, System.Windows.Controls.ComboBox comboBox,
            TitleblockItemViewModel model, string reason = null)
        {
            if (!_isInitialized) return;
            Dispatcher.Invoke(() =>
            {
                if (!_isInitialized) return;
                _pendingTitleblockTypeChanges.Remove(key);
                if (comboBox != null && comboBox.IsLoaded)
                    comboBox.SelectedValue = model.TypeId;
                if (!string.IsNullOrEmpty(reason))
                    TitleblockStatusBarText.Text = "图框换型未执行：" + reason;
            });
        }

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
            _isInitialized = false;
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
                        var doc = app.ActiveUIDocument?.Document;
                        if (doc == null || !doc.Equals(_doc) || !doc.IsValidObject || doc.IsReadOnly)
                            throw new InvalidOperationException("当前项目已切换、关闭或只读，请重新打开图纸管理器。");
                        var sheet = doc.GetElement(sheetId) as ViewSheet;
                        if (sheet == null)
                            throw new InvalidOperationException("图纸已经不存在，请刷新列表。");

                        if (prop != "SheetNumber" && prop != "SheetName")
                            throw new InvalidOperationException("不支持修改该图纸字段：" + prop);
                        string oldValue = prop == "SheetNumber" ? sheet.SheetNumber : sheet.Name;
                        if (string.Equals(oldValue, newVal, StringComparison.Ordinal)) return;
                        try
                        {
                            using (Transaction t = new Transaction(doc, "修改图纸信息"))
                            {
                                if (t.Start() != TransactionStatus.Started)
                                    throw new InvalidOperationException("无法启动图纸信息修改事务。");
                                if (prop == "SheetNumber") sheet.SheetNumber = newVal;
                                else sheet.Name = newVal;
                                if (t.Commit() != TransactionStatus.Committed)
                                    throw new InvalidOperationException("图纸信息修改事务未成功提交，模型状态需要复核。");
                            }
                        }
                        catch
                        {
                            Dispatcher.Invoke(() => vm.SetSheetInfoSilently(prop, oldValue));
                            throw;
                        }
                        // A Revit failure processor may alter the value during commit.
                        // Do not show the new value as confirmed until the model agrees.
                        string actualValue = prop == "SheetNumber" ? sheet.SheetNumber : sheet.Name;
                        if (!string.Equals(actualValue, newVal, StringComparison.Ordinal))
                        {
                            Dispatcher.Invoke(() => vm.SetSheetInfoSilently(prop, actualValue));
                            throw new InvalidOperationException("图纸修改已提交，但结果与请求值不一致；已按模型实际值恢复显示。");
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
                            var doc = app.ActiveUIDocument?.Document;
                            if (doc == null || !doc.Equals(_doc) || !doc.IsValidObject || doc.IsReadOnly)
                                throw new InvalidOperationException("项目已切换、关闭或只读，图纸参数未修改。");
                            var sheet = doc.GetElement(sheetId) as ViewSheet;
                            var param = sheet?.LookupParameter(pName);
                            if (sheet == null || param == null || param.IsReadOnly)
                                throw new InvalidOperationException($"图纸参数“{pName}”已经不存在或不可写。");

                            string oldValue = ParameterValueHelper.GetDisplayValue(param);
                            try
                            {
                                using (Transaction t = new Transaction(doc, "修改图纸参数"))
                                {
                                    if (t.Start() != TransactionStatus.Started)
                                        throw new InvalidOperationException("无法启动图纸参数修改事务。");
                                    if (!ParameterValueHelper.TrySetFromDisplayValue(param, newVal, out string setError))
                                        throw new InvalidOperationException(setError);
                                    if (t.Commit() != TransactionStatus.Committed)
                                        throw new InvalidOperationException("图纸参数修改未能提交，模型状态需要复核。");
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
                    var doc = app.ActiveUIDocument?.Document;
                    if (doc == null || !doc.Equals(_doc) || !doc.IsValidObject || doc.IsReadOnly)
                        throw new InvalidOperationException("项目已切换、关闭或只读，删除未执行。请重新打开管理器。");
                    if (ids.Count == 0 || ids.Distinct().Count() != ids.Count ||
                        ids.Any(id => id == null || doc.GetElement(id) == null))
                        throw new InvalidOperationException("所选图元已失效或存在重复，请刷新列表后重试。");
                    var previewDeletedIds = new HashSet<ElementId>();
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
                            if (deleted != null) previewDeletedIds.UnionWith(deleted);
                            if (!ids.All(previewDeletedIds.Contains))
                                throw new InvalidOperationException("预检未覆盖所有选中的图纸，操作已取消。");
                            extraIds = previewDeletedIds.Where(id => !ids.Contains(id)).ToList();
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
                                ICollection<ElementId> actuallyDeleted = doc.Delete(ids);
                                if (actuallyDeleted == null ||
                                    !previewDeletedIds.SetEquals(actuallyDeleted) ||
                                    actuallyDeleted.Count != actualDeleteCount)
                                    throw new InvalidOperationException("实际级联删除范围与预检不一致，已拒绝提交并回滚。");
                                if (t2.Commit() != TransactionStatus.Committed)
                                    throw new InvalidOperationException("图纸删除事务未能提交。");
                            }
                            // Still inside the transaction group: a failed post-commit
                            // readback can undo the real deletion before one Undo is recorded.
                            if (ids.Any(id => doc.GetElement(id) != null))
                            {
                                tg.RollBack();
                                throw new InvalidOperationException("删除后读回发现仍有目标图元，已回滚事务组。");
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
            }, operationName: "删除图纸", showSuccess: false);
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
                    var doc = app.ActiveUIDocument?.Document;
                    if (doc == null || !doc.Equals(_doc) || !doc.IsValidObject || doc.IsReadOnly)
                        throw new InvalidOperationException("项目已切换、关闭或只读，修订未修改。");
                    var targetSheet = doc.GetElement(sheetId) as ViewSheet;
                    if (targetSheet == null)
                    {
                        throw new InvalidOperationException("所选图纸已经不存在。");
                    }

                    using (Transaction transaction = new Transaction(doc, "更新图纸修订"))
                    {
                        if (transaction.Start() != TransactionStatus.Started)
                            throw new InvalidOperationException("无法启动图纸修订事务。");
                        targetSheet.SetAdditionalRevisionIds(selectedRevisionIds);
                        if (transaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("图纸修订事务未成功提交。");
                    }

                    // When the failure processor changes the revision selection,
                    // never leave the UI showing a false requested state.
                    _revisionCache.Clear();
                    viewModel.Revisions = GetRevisionNames(targetSheet);
                    if (!new HashSet<ElementId>(targetSheet.GetAdditionalRevisionIds()).SetEquals(selectedRevisionIds))
                        throw new InvalidOperationException("图纸修订已提交，但读回的附加修订与所选不一致，请复核。");
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
            string filePath = ExcelService.ShowOpenDialog();
            if (filePath == null) return;
            // Read the spreadsheet outside the write transaction. Any read failure
            // is reported BEFORE starting the Revit ExternalEvent.
            List<IDictionary<string, object>> rows;
            try
            {
                rows = ExcelService.ReadExcel(filePath)
                    .Select(row => (IDictionary<string, object>)new Dictionary<string, object>(row))
                    .ToList();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("图纸导入失败", "读取 Excel 失败，模型没有修改：" + ex.Message);
                return;
            }
            var request = new SheetExcelImportRequest { Rows = rows };
            _handler.SetAction(app =>
            {
                // Prevent stale ExternalEvents from writing another open document.
                if (!_isInitialized || !_doc.IsValidObject ||
                    app.ActiveUIDocument == null || !app.ActiveUIDocument.Document.Equals(_doc)) return;

                SheetExcelImportResult result;
                try
                {
                    result = SheetExcelImportService.Execute(_doc, request);
                }
                catch (Exception ex)
                {
                    if (_isInitialized)
                        TaskDialog.Show("图纸导入失败", "整批导入未成功：" + ex.Message +
                            "\n如提示回滚/提交状态未知，请先核对实际图纸，不要重复导入。");
                    return;
                }

                // WPF refresh failure does not invalidate a completed Revit commit.
                if (!_isInitialized) return;
                try
                {
                    LoadSheets();
                    string summary = result.Committed
                        ? $"图纸已提交：新建 {result.CreatedCount}，更新 {result.UpdatedCount}，跳过空行 {result.SkippedBlankRows}。\n整批写入可使用一次 Ctrl+Z 撤销。"
                        : "Excel 不包含需要导入的图纸行；模型没有修改。";
                    if (result.Committed && !result.SheetIdentityReadbackConfirmed)
                        summary += "\n警告：提交后图纸身份回读不完整，请检查实际图纸，不要重复导入。";
                    TaskDialog.Show(result.Committed ? "图纸导入完成" : "没有可导入的图纸", summary);
                }
                catch (Exception refreshError)
                {
                    TaskDialog.Show("图纸已提交，界面更新失败",
                        "模型操作已完成，但界面刷新或提示失败：" + refreshError.Message +
                        "。请重新打开管理窗口，不要重复导入。");
                }
            }, operationName: "批量导入图纸", showSuccess: false);
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
                            var doc = app.ActiveUIDocument?.Document;
                            if (doc == null || !doc.Equals(_doc) || !doc.IsValidObject || doc.IsReadOnly)
                                throw new InvalidOperationException("项目已切换、关闭或只读，图框参数未修改。");
                            var titleblock = doc.GetElement(instanceId);
                            var parameter = titleblock?.LookupParameter(parameterName);
                            if (parameter == null || parameter.IsReadOnly)
                                throw new InvalidOperationException($"图框参数“{parameterName}”已经不存在或不可写。");

                            string oldValue = ParameterValueHelper.GetDisplayValue(parameter);
                            try
                            {
                                using (Transaction transaction = new Transaction(doc, "修改图框参数"))
                                {
                                    if (transaction.Start() != TransactionStatus.Started)
                                        throw new InvalidOperationException("无法启动图框参数修改事务。");
                                    if (!ParameterValueHelper.TrySetFromDisplayValue(parameter, newValue, out string setError))
                                        throw new InvalidOperationException(setError);
                                    if (transaction.Commit() != TransactionStatus.Committed)
                                        throw new InvalidOperationException("图框参数修改未能提交，模型状态需要复核。");
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
            string operationId = Guid.NewGuid().ToString("N");
            _handler.SetAction(app =>
            {
                bool committed = false;
                try
                {
                    var doc = app.ActiveUIDocument?.Document;
                    if (doc == null || !doc.IsValidObject || !doc.Equals(_doc) || doc.IsReadOnly)
                        throw new InvalidOperationException("项目已切换、关闭或只读，图框换型未执行。");
                    var instance = doc.GetElement(instanceId) as FamilyInstance;
                    var newSymbol = doc.GetElement(newTypeId) as FamilySymbol;
                    if (instance == null || newSymbol == null ||
                        instance.Category?.Id.GetIdValue() != (long)BuiltInCategory.OST_TitleBlocks ||
                        newSymbol.Category?.Id.GetIdValue() != (long)BuiltInCategory.OST_TitleBlocks)
                        throw new InvalidOperationException("图框实例或目标类型已失效，请刷新列表。");

                    using (Transaction transaction = new Transaction(doc, "更换图框类型"))
                    {
                        if (transaction.Start() != TransactionStatus.Started)
                            throw new InvalidOperationException("无法启动图框换型事务。");
                        if (!newSymbol.IsActive) newSymbol.Activate();
                        instance.Symbol = newSymbol;
                        doc.Regenerate();
                        if (instance.Symbol?.Id.GetIdValue() != newTypeId.GetIdValue())
                            throw new InvalidOperationException("换型结果与目标类型不一致，已取消事务。");
                        TransactionStatus status = transaction.Commit();
                        if (status != TransactionStatus.Committed)
                            throw new InvalidOperationException($"图框换型事务未提交，状态：{status}。");
                        committed = true;
                    }
                    // Committed state must be distinguished from a subsequent UI/readback failure.
                    var saved = doc.GetElement(instanceId) as FamilyInstance;
                    if (saved?.Symbol?.Id.GetIdValue() != newTypeId.GetIdValue())
                        throw new InvalidOperationException("事务已提交，但图框类型读回不一致，请刷新核对模型。");
                    Dispatcher.Invoke(() =>
                    {
                        if (!_isInitialized) return;
                        viewModel.TypeId = newTypeId;
                        viewModel.TypeName = newSymbol.Name;
                        TitleblockStatusBarText.Text = "图框换型已提交并验证。";
                    });
                }
                catch (Exception ex)
                {
                    DiagnosticLog.WriteError("图框换型", ex);
                    Dispatcher.Invoke(() =>
                    {
                        if (!_isInitialized) return;
                        if (!committed)
                        {
                            viewModel.TypeId = oldTypeId;
                            viewModel.TypeName = oldTypeName;
                        }
                        else
                        {
                            var currentInstance = _doc.IsValidObject ? _doc.GetElement(instanceId) as FamilyInstance : null;
                            if (currentInstance?.Symbol != null)
                            {
                                viewModel.TypeId = currentInstance.Symbol.Id;
                                viewModel.TypeName = currentInstance.Symbol.Name;
                            }
                        }
                        TaskDialog.Show(committed ? "需要复核" : "图框换型失败",
                            (committed ? "事务已提交，不能视为未执行。" : "事务未确认提交。") + ex.Message);
                    });
                }
                finally
                {
                    ResetPendingTitleblockChange(instanceKey, comboBox, viewModel);
                }
            }, operationName: "更换图框类型", showSuccess: false,
               operationId: operationId,
               onSkipped: reason => ResetPendingTitleblockChange(instanceKey, comboBox, viewModel, reason));
            try
            {
                // Pending refers to an earlier signal, whose dispatcher drains the same
                // action queue. Denied/TimedOut MUST cancel this queued item before retry.
                ExternalEventRequest result = _externalEvent.Raise();
                if (result != ExternalEventRequest.Accepted && result != ExternalEventRequest.Pending)
                {
                    _handler.CancelPendingAction(operationId);
                    ResetPendingTitleblockChange(instanceKey, comboBox, viewModel,
                        "Revit 未接受本次事件（" + result + "），请重试。");
                }
            }
            catch (Exception raiseError)
            {
                _handler.CancelPendingAction(operationId);
                ResetPendingTitleblockChange(instanceKey, comboBox, viewModel,
                    "Revit 事件无法入队：" + raiseError.Message);
                DiagnosticLog.WriteError("图框换型入队", raiseError);
            }
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

            // Read/parse outside the Revit callback; hold a stable input snapshot.
            List<IDictionary<string, object>> rows;
            try
            {
                rows = ExcelService.ReadExcel(filePath)
                    .Select(row => (IDictionary<string, object>)new Dictionary<string, object>(row))
                    .ToList();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("图框参数导入失败", "Excel 读取失败：" + ex.Message);
                return;
            }

            _handler.SetAction(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null || !doc.Equals(_doc))
                    throw new InvalidOperationException("当前项目已切换，图框参数未导入。请重新打开管理器。");
                var outcome = TitleblockExcelImportService.Execute(doc, rows);
                string message = outcome.Committed
                    ? $"图框参数整批提交：更新 {outcome.ChangedElements} 个实例、{outcome.WrittenParameters} 个参数。\n可使用一次 Ctrl+Z 撤销。"
                    : "所有指定图框参数均无需修改；没有创建撤销记录。";
                if (!outcome.ReadbackConfirmed)
                    message += "\n警告：" + outcome.ReadbackWarning;
                try
                {
                    LoadTitleblocks();
                }
                catch (Exception refreshError)
                {
                    message += "\n模型操作已完成，但刷新表格失败：" + refreshError.Message;
                }
                Dispatcher.Invoke(() => TaskDialog.Show("图框参数 Excel 导入结果", message));
            }, operationName: "图框参数 Excel 整批导入", showSuccess: false);
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
