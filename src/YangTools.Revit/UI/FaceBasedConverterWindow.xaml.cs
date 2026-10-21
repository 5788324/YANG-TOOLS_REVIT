using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public partial class FaceBasedConverterWindow : Window
    {
        private readonly UIApplication _uiapp;
        private readonly Document _doc;
        private readonly FaceBasedConverterEngine _engine;
        private List<ElementId> _lastScannedIds = new List<ElementId>();

        public ObservableCollection<FaceBasedTypeItem> TypeItems { get; set; } = new ObservableCollection<FaceBasedTypeItem>();

        public FaceBasedConverterWindow(UIApplication uiapp)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;
            _engine = new FaceBasedConverterEngine(uiapp);

            FamilyDataGrid.ItemsSource = TypeItems;

            ResolveTemplatePath();
            ScanFaceBasedFamilies();
        }

        private void ResolveTemplatePath()
        {
            string path = _engine.ResolveTemplatePath();
            if (path != null) TxtTemplatePath.Text = path;
        }

        private void ScanFaceBasedFamilies()
        {
            TypeItems.Clear();
            try
            {
                var instances = new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(fi =>
                    {
                        try { return _engine.IsFamilyFaceBased(fi.Symbol?.Family); }
                        catch { return false; }
                    })
                    .ToList();

                var grouped = instances
                    .GroupBy(fi => fi.Symbol?.Id ?? ElementId.InvalidElementId)
                    .Where(g => g.Key != ElementId.InvalidElementId)
                    .ToList();

                foreach (var group in grouped)
                {
                    var firstInst = group.First();
                    var symbol = firstInst.Symbol;
                    if (symbol == null) continue;

                    string hostCat = "";
                    try { hostCat = firstInst.Host?.Category?.Name ?? "未知"; }
                    catch { hostCat = "未知"; }

                    TypeItems.Add(new FaceBasedTypeItem
                    {
                        IsSelected = true,
                        FamilyName = symbol.Family?.Name ?? "未知",
                        TypeName = symbol.Name ?? "未知",
                        InstanceCount = group.Count(),
                        HostCategory = hostCat,
                        SymbolId = symbol.Id,
                        InstanceIds = group.Select(fi => fi.Id).ToList()
                    });
                }

                AttachSelectionTracking();
                TxtCount.Text = $"共 {TypeItems.Count} 个基于面族类型 ({instances.Count} 个实例)";
                TxtStatus.Text = $"扫描完成，发现 {TypeItems.Count} 个基于面的族类型";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "扫描失败: " + ex.Message;
            }
        }

        private void AttachSelectionTracking()
        {
            foreach (var row in TypeItems)
                row.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(FaceBasedTypeItem.IsSelected))
                        ChkSelectAll.IsChecked = TypeItems.Count > 0 && TypeItems.All(x => x.IsSelected);
                };
            ChkSelectAll.IsChecked = TypeItems.Count > 0 && TypeItems.All(x => x.IsSelected);
        }

        private void ScanFromSelection(ICollection<ElementId> selectedIds)
        {
            _lastScannedIds = selectedIds.ToList();
            RefreshUIList();
        }

        private void RefreshUIList()
        {
            TypeItems.Clear();
            try
            {
                var instances = _lastScannedIds
                    .Select(id => _doc.GetElement(id))
                    .OfType<FamilyInstance>()
                    .Where(fi =>
                    {
                        try { return _engine.IsFamilyFaceBased(fi.Symbol?.Family); }
                        catch { return false; }
                    })
                    .ToList();

                bool bakeRotation = cbBakeRotation.IsChecked == true;

                var groupedBySymbol = instances
                    .GroupBy(fi => fi.Symbol?.Id ?? ElementId.InvalidElementId)
                    .Where(g => g.Key != ElementId.InvalidElementId)
                    .ToList();

                foreach (var symbolGroup in groupedBySymbol)
                {
                    if (bakeRotation)
                    {
                        var orientationGroups = symbolGroup.GroupBy(fi => _engine.GetOrientationHash(fi)).ToList();
                        int index = 1;
                        bool multiple = orientationGroups.Count > 1;

                        foreach (var og in orientationGroups)
                        {
                            var firstInst = og.First();
                            var symbol = firstInst.Symbol;
                            string hostCat = "";
                            try { hostCat = firstInst.Host?.Category?.Name ?? "未知"; } catch { hostCat = "未知"; }

                            TypeItems.Add(new FaceBasedTypeItem
                            {
                                IsSelected = true,
                                FamilyName = symbol.Family?.Name ?? "未知",
                                TypeName = symbol.Name + (multiple ? $" (朝向 {index})" : ""),
                                ConvertedSuffix = multiple ? $"_{index}" : "",
                                InstanceCount = og.Count(),
                                HostCategory = hostCat,
                                SymbolId = symbol.Id,
                                InstanceIds = og.Select(i => i.Id).ToList()
                            });
                            index++;
                        }
                    }
                    else
                    {
                        var firstInst = symbolGroup.First();
                        var symbol = firstInst.Symbol;
                        string hostCat = "";
                        try { hostCat = firstInst.Host?.Category?.Name ?? "未知"; } catch { hostCat = "未知"; }

                        TypeItems.Add(new FaceBasedTypeItem
                        {
                            IsSelected = true,
                            FamilyName = symbol.Family?.Name ?? "未知",
                            TypeName = symbol.Name ?? "未知",
                            ConvertedSuffix = "",
                            HostCategory = hostCat,
                            InstanceCount = symbolGroup.Count(),
                            SymbolId = symbol.Id,
                            InstanceIds = symbolGroup.Select(i => i.Id).ToList()
                        });
                    }
                }

                AttachSelectionTracking();
                TxtCount.Text = $"共 {TypeItems.Count} 个基于面族类型 ({instances.Count} 个实例)";
                TxtStatus.Text = "列表已更新，等待选择转换类型。";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "扫描失败: " + ex.Message;
            }
        }

        private void StartConvert_Click(object sender, RoutedEventArgs e)
        {
            FaceBasedConversionRequest request;
            try
            {
                request = FaceBasedConversionRequest.Capture(TypeItems, TxtTemplatePath.Text,
                    ChkDeleteOld.IsChecked == true, cbBakeRotation.IsChecked == true);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("提示", ex.Message);
                TxtStatus.Text = ex.Message;
                return;
            }
            // 窗口是 IExternalCommand 中 ShowDialog 模态调用，仍在合法 Revit API 上下文。
            // 不应把此同步处理伪装成 ExternalEvent 队列。
            if (!_doc.IsValidObject || _uiapp.ActiveUIDocument?.Document != _doc)
            {
                TaskDialog.Show("错误", "当前 Revit 文档已切换，请重新打开转换工具。");
                return;
            }
            string templatePath = request.TemplatePath;
            bool deleteOld = request.DeleteOriginal;
            bool bakeRotation = request.BakeRotation;
            TxtStatus.Text = "正在转换...";
            IsEnabled = false;

            int totalConverted = 0;
            int totalFailed = 0;
            var failMessages = new List<string>();

            using (TransactionGroup group = new TransactionGroup(_doc, "基于面族批量转换"))
            {
                try
                {
                    if (group.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动基于面转换事务组。");

                    foreach (var typeItem in request.Items)
                    {
                        try
                        {
                            TxtStatus.Text = $"正在转换: {typeItem.FamilyName} - {typeItem.TypeName}...";

                            var repInstance = _doc.GetElement(typeItem.InstanceIds.First()) as FamilyInstance;
                            if (repInstance == null)
                                throw new InvalidOperationException("找不到用于提取几何的代表实例。");

                            Transform representativeTransform = repInstance.GetTransform();
                            var geom = _engine.ExtractGeometry(repInstance, bakeRotation);
                            var solids = geom.Item1;
                            var curves = geom.Item2;
                            if (solids.Count == 0 && curves.Count == 0)
                                throw new InvalidOperationException("无法提取任何几何体或线条。");

                            string rfaPath = _engine.CreateConvertedFamily(
                                typeItem.FamilyName,
                                typeItem.TypeName,
                                typeItem.Suffix,
                                solids,
                                curves,
                                templatePath);

                            Family? loadedFamily = null;
                            using (Transaction loadTransaction = new Transaction(_doc, "载入转换族"))
                            {
                                try
                                {
                                    if (loadTransaction.Start() != TransactionStatus.Started)
                                        throw new InvalidOperationException("无法启动载入转换族事务。");

                                    bool loaded = _doc.LoadFamily(rfaPath, new FamilyOption(), out loadedFamily);
                                    if (!loaded || loadedFamily == null)
                                        throw new InvalidOperationException("转换族未能载入当前项目。");

                                    TransactionStatus loadStatus = loadTransaction.Commit();
                                    if (loadStatus != TransactionStatus.Committed)
                                        throw new InvalidOperationException($"载入转换族事务提交失败，状态：{loadStatus}。");
                                }
                                catch
                                {
                                    try
                                    {
                                        if (loadTransaction.GetStatus() == TransactionStatus.Started)
                                            loadTransaction.RollBack();
                                    }
                                    catch { }
                                    throw;
                                }
                            }

                            ElementId newSymbolId = loadedFamily.GetFamilySymbolIds().FirstOrDefault();
                            if (newSymbolId == null || newSymbolId == ElementId.InvalidElementId)
                                throw new InvalidOperationException("无法获取新族类型。");

                            var newSymbol = _doc.GetElement(newSymbolId) as FamilySymbol;
                            if (newSymbol == null)
                                throw new InvalidOperationException("新族类型对象无效。");
                            if (newSymbol.Family?.FamilyPlacementType == FamilyPlacementType.WorkPlaneBased)
                                throw new InvalidOperationException("转换后的族仍是工作平面宿主族，不能作为常规模型替换。");

                            using (Transaction replaceTransaction = new Transaction(_doc, "替换实例"))
                            {
                                try
                                {
                                    if (replaceTransaction.Start() != TransactionStatus.Started)
                                        throw new InvalidOperationException("无法启动替换实例事务。");

                                    try
                                    {
                                        string newTypeName = typeItem.TypeName + "_Converted";
                                        if (newSymbol.Name != newTypeName)
                                            newSymbol.Name = newTypeName;
                                    }
                                    catch (Exception ex)
                                    {
                                        DiagnosticLog.WriteError("重命名转换族类型", ex);
                                    }

                                    if (!newSymbol.IsActive)
                                    {
                                        newSymbol.Activate();
                                        _doc.Regenerate();
                                    }

                                    foreach (ElementId oldId in typeItem.InstanceIds)
                                    {
                                        using (SubTransaction instanceTransaction = new SubTransaction(_doc))
                                        {
                                            try
                                            {
                                                if (instanceTransaction.Start() != TransactionStatus.Started)
                                                    throw new InvalidOperationException("无法启动实例替换子事务。");

                                                var oldInst = _doc.GetElement(oldId) as FamilyInstance;
                                                if (oldInst == null)
                                                    throw new InvalidOperationException("原实例已不存在。");
                                                if (oldInst.Symbol?.Id.GetIdValue() != typeItem.SymbolId.GetIdValue() ||
                                                    !_engine.IsFamilyFaceBased(oldInst.Symbol?.Family))
                                                    throw new InvalidOperationException("原实例的类型或放置方式已变化，请重新扫描后再转换。");

                                                Transform sourceTransform = oldInst.GetTransform();
                                                // 烘焙后的几何取自代表实例；近似朝向分组不足以证明姿态相同。
                                                // 角度超过容差必须失败，禁止把另一实例替换成错误的姿态。
                                                if (bakeRotation &&
                                                    (sourceTransform.BasisX.DistanceTo(representativeTransform.BasisX) > 1e-6 ||
                                                     sourceTransform.BasisY.DistanceTo(representativeTransform.BasisY) > 1e-6 ||
                                                     sourceTransform.BasisZ.DistanceTo(representativeTransform.BasisZ) > 1e-6))
                                                    throw new InvalidOperationException("同组实例的实际三维朝向不同，无法共用一个烘焙族；请分组后重试。");
                                                if (!bakeRotation &&
                                                    sourceTransform.BasisZ.DotProduct(XYZ.BasisZ) < 1.0 - 1e-6)
                                                    throw new InvalidOperationException("原实例存在非垂直三维朝向，请开启“烘焙三维姿态”后转换。");

                                                XYZ location;
                                                double rotation = 0;
                                                if (oldInst.Location is LocationPoint locationPoint)
                                                {
                                                    location = locationPoint.Point;
                                                    rotation = locationPoint.Rotation;
                                                }
                                                else
                                                    throw new InvalidOperationException("无法读取可验证的 LocationPoint；不能用包围盒中心代替原始族原点。");

                                                Level? level = null;
                                                ElementId levelId = oldInst.LevelId;
                                                if (levelId != ElementId.InvalidElementId)
                                                    level = _doc.GetElement(levelId) as Level;

                                                if (level == null)
                                                {
                                                    level = new FilteredElementCollector(_doc)
                                                        .OfClass(typeof(Level))
                                                        .Cast<Level>()
                                                        .OrderBy(item => Math.Abs(item.ProjectElevation - location.Z))
                                                        .FirstOrDefault();
                                                }

                                                if (level == null)
                                                    throw new InvalidOperationException("项目中没有可用于放置转换实例的标高。");

                                                FamilyInstance newInst = FamilyPlacementHelper.CreateAtWorldPoint(
                                                    _doc,
                                                    newSymbol,
                                                    level,
                                                    location,
                                                    "基于面转换");

                                                if (!bakeRotation && Math.Abs(rotation) > 1e-6)
                                                {
                                                    Line rotationAxis = Line.CreateBound(location, location + XYZ.BasisZ);
                                                    ElementTransformUtils.RotateElement(_doc, newInst.Id, rotationAxis, rotation);
                                                }

                                                // 删除是破坏性操作：必须确认原参数已保留，禁止静默丢失。
                                                _engine.CopyInstanceParameters(oldInst, newInst, deleteOld);
                                                if (deleteOld)
                                                {
                                                    if (oldInst.Host == null && oldInst.HostFace == null)
                                                        throw new InvalidOperationException("原实例没有可确认的面宿主，不能安全删除。");
                                                    _doc.Delete(oldId);
                                                    if (_doc.GetElement(oldId) != null)
                                                        throw new InvalidOperationException("原实例删除后仍存在，回滚整个批次。");
                                                }
                                                var actual = _doc.GetElement(newInst.Id) as FamilyInstance;
                                                if (actual == null || actual.Symbol.Id != newSymbol.Id)
                                                    throw new InvalidOperationException("新实例及其类型未能回读确认，回滚整个批次。");
                                                if (!(actual.Location is LocationPoint placedPoint) ||
                                                    placedPoint.Point.DistanceTo(location) > 1.0 / 304.8)
                                                    throw new InvalidOperationException("新实例世界坐标与原位置偏差超过 1 mm，回滚整个批次。");

                                                TransactionStatus instanceStatus = instanceTransaction.Commit();
                                                if (instanceStatus != TransactionStatus.Committed)
                                                    throw new InvalidOperationException($"实例替换子事务提交失败，状态：{instanceStatus}。");

                                                totalConverted++;
                                            }
                                            catch (Exception ex)
                                            {
                                                try
                                                {
                                                    if (instanceTransaction.GetStatus() == TransactionStatus.Started)
                                                        instanceTransaction.RollBack();
                                                }
                                                catch { }

                                                totalFailed++;
                                                failMessages.Add(
                                                    $"[{typeItem.FamilyName}-{typeItem.TypeName}] 实例 {oldId}: {ex.Message}");
                                                throw new InvalidOperationException(
                                                    $"实例 {oldId} 转换失败，已停止整批转换：{ex.Message}", ex);
                                            }
                                        }
                                    }

                                    TransactionStatus replaceStatus = replaceTransaction.Commit();
                                    if (replaceStatus != TransactionStatus.Committed)
                                        throw new InvalidOperationException($"替换实例事务提交失败，状态：{replaceStatus}。");
                                }
                                catch
                                {
                                    try
                                    {
                                        if (replaceTransaction.GetStatus() == TransactionStatus.Started)
                                            replaceTransaction.RollBack();
                                    }
                                    catch { }
                                    throw;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            failMessages.Add($"[{typeItem.FamilyName}-{typeItem.TypeName}]: {ex.Message}");
                            totalFailed++;
                            throw;
                        }
                    }

                    TransactionStatus groupStatus = group.Assimilate();
                    if (groupStatus != TransactionStatus.Committed)
                        throw new InvalidOperationException($"基于面转换事务组合并失败，状态：{groupStatus}。");
                }
                catch (Exception ex)
                {
                    bool rollbackConfirmed = group.GetStatus() == TransactionStatus.RolledBack;
                    try
                    {
                        if (group.GetStatus() == TransactionStatus.Started)
                            rollbackConfirmed = group.RollBack() == TransactionStatus.RolledBack;
                    }
                    catch (Exception rollbackError)
                    {
                        DiagnosticLog.WriteError("基于面转换回滚", rollbackError);
                    }
                    totalConverted = 0;
                    string outcome = rollbackConfirmed
                        ? "本次模型变更已回滚。"
                        : "无法确认事务组完整回滚，请检查当前模型，切勿直接重试删除。";
                    TaskDialog.Show("错误", "转换终止：" + ex.Message + "\n" + outcome);
                    TxtStatus.Text = rollbackConfirmed ? "转换失败，已回滚" : "转换失败，回滚状态未知";
                    return;
                }
                finally
                {
                    _engine.CleanupTempFiles();
                    IsEnabled = true;
                }
            }

            string resultMsg = $"转换完成！\n\n成功转换: {totalConverted} 个实例\n失败项: {totalFailed}";
            if (failMessages.Count > 0)
                resultMsg += "\n\n失败详情:\n" + string.Join("\n", failMessages);

            TaskDialog.Show("转换结果", resultMsg);
            TxtStatus.Text = $"完成: 成功 {totalConverted} 个, 失败 {totalFailed} 个";
            if (totalConverted > 0)
            {
                try { ScanFaceBasedFamilies(); }
                catch (Exception uiError)
                {
                    DiagnosticLog.WriteError("基于面转换成功后刷新", uiError);
                    TxtStatus.Text = "模型已提交，列表刷新失败；请手动刷新列表。";
                }
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RefreshList_Click(object sender, RoutedEventArgs e)
        {
            ScanFaceBasedFamilies();
        }

        private void SelectFromView_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                var sel = _uiapp.ActiveUIDocument.Selection;
                var picked = sel.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element,
                    "请选择要转换的基于面族实例 (按Enter确认，按ESC取消)");

                if (picked != null && picked.Count > 0)
                {
                    var ids = picked.Select(p => p.ElementId).ToList();
                    ScanFromSelection(ids);
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", ex.Message);
            }
            finally
            {
                this.Show();
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool selectAll = ChkSelectAll.IsChecked == true;
            foreach (var item in TypeItems)
                item.IsSelected = selectAll;
        }

        private void cbBakeRotation_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // InitializeComponent 加载控件时也可能触发 Checked/Unchecked，不能读取尚未赋值的 Engine。
            if (_engine == null || _doc == null) return;
            if (_lastScannedIds != null && _lastScannedIds.Count > 0)
                RefreshUIList();
            else
                ScanFaceBasedFamilies();
        }

        private void BrowseTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Revit Family Template (*.rft)|*.rft",
                Title = "选择常规模型族模板"
            };
            if (!string.IsNullOrEmpty(TxtTemplatePath.Text))
                dlg.InitialDirectory = Path.GetDirectoryName(TxtTemplatePath.Text);
            if (dlg.ShowDialog() == true)
                TxtTemplatePath.Text = dlg.FileName;
        }
    }
}
