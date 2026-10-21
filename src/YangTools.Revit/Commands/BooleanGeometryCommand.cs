using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Models;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(panelName: "模型修改区", buttonText: "布尔几何", tooltip: "对两个族实例进行布尔运算(连接/剪切/融合)", largeIcon: "Icons/boolean_geometry_32.png", smallIcon: "Icons/boolean_geometry_16.png")]
    public class BooleanGeometryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiapp = commandData.Application;
                var uidoc = uiapp.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show("提示", "请先打开一个项目文档。");
                    return Result.Cancelled;
                }
                var doc = uidoc.Document;

                UserSettings settings = UserSettings.Load();
                var state = new Models.BooleanGeometryState
                {
                    DeleteTargetInst = settings.BooleanDeleteTargetInst,
                    DeleteTargetFam = settings.BooleanDeleteTargetFam
                };

                while (true)
                {
                    var win = new UI.BooleanGeometryWindow(uiapp, state);
                    var helper = new System.Windows.Interop.WindowInteropHelper(win)
                    {
                        Owner = uiapp.MainWindowHandle
                    };
                    win.ShowDialog();

                    state = win.State;
                    if (uiapp.ActiveUIDocument == null ||
                        !DocumentIdentityGuard.IsSameActiveDocument(uiapp, doc))
                        throw new InvalidOperationException("操作期间项目文档已切换，选择和写入均已停止。");

                    if (win.ActionToPerform == "PickMain")
                    {
                        try
                        {
                            var r = uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element, "请选择主体实例");
                            if (doc.GetElement(r) is FamilyInstance fi)
                            {
                                state.MainInst.Clear();
                                state.MainInst.Add(fi);
                            }
                            else TaskDialog.Show("提示", "所选对象不是载入族实例，已保留之前的选择。");
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                    }
                    else if (win.ActionToPerform == "PickUnion")
                    {
                        try
                        {
                            var rs = uidoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, "请选择融合实例(可多选)");
                            var selected = rs.Select(r => doc.GetElement(r)).OfType<FamilyInstance>().ToList();
                            if (selected.Count != rs.Count)
                                TaskDialog.Show("提示", "选择中包含非族实例，已保留之前的选择，请重新选择。");
                            else
                            {
                                state.UnionInsts.Clear();
                                state.UnionInsts.AddRange(selected);
                            }
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                    }
                    else if (win.ActionToPerform == "PickCut")
                    {
                        try
                        {
                            var rs = uidoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, "请选择剪切实例(可多选)");
                            var selected = rs.Select(r => doc.GetElement(r)).OfType<FamilyInstance>().ToList();
                            if (selected.Count != rs.Count)
                                TaskDialog.Show("提示", "选择中包含非族实例，已保留之前的选择，请重新选择。");
                            else
                            {
                                state.CutInsts.Clear();
                                state.CutInsts.AddRange(selected);
                            }
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                    }
                    else if (win.ActionToPerform == "PickJoin")
                    {
                        try
                        {
                            var rs = uidoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element, "请选择连接实例(可多选)");
                            var selected = rs.Select(r => doc.GetElement(r)).OfType<FamilyInstance>().ToList();
                            if (selected.Count != rs.Count)
                                TaskDialog.Show("提示", "选择中包含非族实例，已保留之前的选择，请重新选择。");
                            else
                            {
                                state.JoinInsts.Clear();
                                state.JoinInsts.AddRange(selected);
                            }
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                    }
                    else if (win.IsExecute)
                    {
                        break; // Proceed to transaction
                    }
                    else
                    {
                        // User closed or cancelled
                        return Result.Cancelled;
                    }
                }

                if (uiapp.ActiveUIDocument == null ||
                    !DocumentIdentityGuard.IsSameActiveDocument(uiapp, doc))
                    throw new InvalidOperationException("当前 Revit 项目已切换，禁止对原文档继续执行布尔操作。");

                if (state.MainInst.Count == 0)
                {
                    TaskDialog.Show("提示", "请先选择主体实例。");
                    return Result.Cancelled;
                }

                var mainFi = state.MainInst[0];
                var allTargets = state.UnionInsts.Concat(state.CutInsts).Concat(state.JoinInsts).ToList();
                if (allTargets.Count == 0)
                {
                    TaskDialog.Show("提示", "请至少选择一个融合、剪切或连接目标实例。");
                    return Result.Cancelled;
                }

                if (allTargets.Any(t => t.Id == mainFi.Id))
                {
                    TaskDialog.Show("提示", "主体实例不能同时作为融合、剪切或连接目标。");
                    return Result.Cancelled;
                }

                var duplicateTargetIds = allTargets
                    .GroupBy(t => t.Id)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key.ToString())
                    .ToList();
                if (duplicateTargetIds.Count > 0)
                {
                    TaskDialog.Show("提示", "同一目标实例不能同时参与多种布尔操作。重复 ID：\n" + string.Join(", ", duplicateTargetIds));
                    return Result.Cancelled;
                }

                var allSelected = allTargets.Concat(new[] { mainFi }).ToList();
                foreach (FamilyInstance instance in allSelected)
                {
                    if (!doc.Equals(instance.Document) ||
                        !(doc.GetElement(instance.Id) is FamilyInstance current) ||
                        current.Symbol == null || current.Symbol.Family == null)
                        throw new InvalidOperationException($"选择中存在跨项目、已删除或无效族实例（ID：{instance.Id}），请重新选择。");
                }

                // 删除 Family 定义会级联删除项目中所有相同族实例，不能仅检查已选图元。
                var targetFamilyIds = allTargets.Select(t => t.Symbol.Family.Id).Distinct().ToList();
                if (state.DeleteTargetFam)
                {
                    if (targetFamilyIds.Contains(mainFi.Symbol.Family.Id))
                        throw new InvalidOperationException("目标族与主体族相同，禁止删除主体族定义。");
                    var selectedIds = new HashSet<ElementId>(allTargets.Select(t => t.Id));
                    var otherInstances = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                        .Where(fi => targetFamilyIds.Contains(fi.Symbol.Family.Id) && !selectedIds.Contains(fi.Id))
                        .Select(fi => fi.Id.ToString()).ToList();
                    if (otherInstances.Count > 0)
                        throw new InvalidOperationException(
                            "目标族仍存在未选中的项目实例，彻底删除族会级联删除它们，本次操作被阻止。实例 ID："
                            + string.Join(", ", otherInstances.Take(12)));
                }

                var warning = new TaskDialog("布尔几何确认")
                {
                    MainInstruction = "该操作会重写主体族的几何。",
                    MainContent = "主体族的所有同族实例及类型都可能受到影响，参数化几何会被转换为固定实体。"
                        + "\n计算顺序：融合 → 剪切 → 连接。剪切刀具默认保留。"
                        + (state.DeleteTargetInst || state.DeleteTargetFam ? "\n已启用删除：原图元/目标族可能被永久删除并影响标签与依赖，请先保存项目副本。" : "\n当前不会删除目标实例。")
                        + "\n建议先保存项目副本。",
                    CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel,
                    DefaultButton = TaskDialogResult.Cancel
                };
                if (warning.Show() != TaskDialogResult.Ok)
                    return Result.Cancelled;

                Document? famDoc = null;
                string? tempFile = null;

                try
                {
                    if (doc.IsModifiable || doc.IsReadOnly)
                        throw new InvalidOperationException("项目当前不可编辑族；请结束其他事务后重试。");

                    famDoc = doc.EditFamily(mainFi.Symbol.Family);
                    if (famDoc == null) throw new InvalidOperationException("无法编辑主体实例的族。");

                    Transform mainInv = mainFi.GetTransform().Inverse;

                    List<Solid> cutSolids = BooleanGeometryEngine.GetLocalSolids(state.CutInsts, mainInv);
                    List<Solid> unionSolids = BooleanGeometryEngine.GetLocalSolids(state.UnionInsts, mainInv);
                    List<Solid> joinSolids = BooleanGeometryEngine.GetLocalSolids(state.JoinInsts, mainInv);

                    using (Transaction tFam = new Transaction(famDoc, "布尔运算"))
                    {
                        try
                        {
                            if (tFam.Start() != TransactionStatus.Started)
                                throw new InvalidOperationException("无法启动族文档布尔事务。");

                            var resultingSolids = BooleanGeometryEngine.ExecuteBooleanOperations(
                                famDoc, cutSolids, unionSolids, joinSolids);
                            if (resultingSolids.Count == 0)
                                throw new InvalidOperationException("布尔运算未生成有效实体。");
                            foreach (var solid in resultingSolids)
                            {
                                if (solid.Volume <= 0)
                                    throw new InvalidOperationException("布尔运算返回了非正体积实体。");
                                if (FreeFormElement.Create(famDoc, solid) == null)
                                    throw new InvalidOperationException("FreeFormElement 创建失败，必须回滚族事务。");
                            }

                            TransactionStatus familyStatus = tFam.Commit();
                            if (familyStatus != TransactionStatus.Committed)
                                throw new InvalidOperationException($"族文档布尔事务提交失败，状态：{familyStatus}。");
                        }
                        catch (Exception writeError)
                        {
                            if (tFam.GetStatus() == TransactionStatus.Started)
                            {
                                try
                                {
                                    if (tFam.RollBack() != TransactionStatus.RolledBack)
                                        throw new InvalidOperationException("族事务回滚未确认。");
                                }
                                catch (Exception rollbackError)
                                {
                                    throw new InvalidOperationException("族事务回滚状态未知，禁止声明原模型未变化。",
                                        new AggregateException(writeError, rollbackError));
                                }
                            }
                            throw;
                        }
                    }

                    tempFile = FamilyFileHelper.CreateTempFamilyPath(mainFi.Symbol.Family.Name, "YangTools_BooleanGeometry");
                    famDoc.SaveAs(tempFile);

                    famDoc.Close(false);
                    famDoc = null;


                    // Revit Document.EditFamily 不允许项目存在活动写事务。
                    // 独立族文档修改并保存后，项目 TransactionGroup 只覆盖载入与可选删除。
                    using (TransactionGroup tg = new TransactionGroup(doc, "布尔运算"))
                    {
                        if (tg.Start() != TransactionStatus.Started)
                            throw new InvalidOperationException("无法启动项目布尔事务组。");
                        try
                        {
                            Family loadedFamily;
                            using (Transaction tLoad = new Transaction(doc, "载入族"))
                            {
                                try
                                {
                                    if (tLoad.Start() != TransactionStatus.Started)
                                        throw new InvalidOperationException("无法启动重新载入族事务。");

                                    bool loaded = doc.LoadFamily(tempFile, new FamilyOption(), out loadedFamily);
                                    if (!loaded || loadedFamily == null)
                                        throw new InvalidOperationException("修改后的主体族未能重新载入项目。");

                                    if (state.DeleteTargetInst || state.DeleteTargetFam)
                                    {
                                        foreach (var target in allTargets)
                                        {
                                            ICollection<ElementId> deleted = doc.Delete(target.Id);
                                            if (deleted == null || !deleted.Contains(target.Id) || doc.GetElement(target.Id) != null)
                                                throw new InvalidOperationException($"目标实例 {target.Id} 删除失败，必须回滚整批操作。");
                                        }
                                    }
                                    if (state.DeleteTargetFam)
                                    {
                                        foreach (ElementId familyId in targetFamilyIds)
                                        {
                                            ICollection<ElementId> deleted = doc.Delete(familyId);
                                            if (deleted == null || !deleted.Contains(familyId) || doc.GetElement(familyId) != null)
                                                throw new InvalidOperationException($"目标族 {familyId} 删除未确认，必须回滚整批操作。");
                                        }
                                    }

                                    doc.Regenerate();
                                    if (!(doc.GetElement(mainFi.Id) is FamilyInstance updatedMain) ||
                                        updatedMain.Symbol == null || updatedMain.Symbol.Family.Id != loadedFamily.Id)
                                        throw new InvalidOperationException("重新载入后主体实例/族未确认，阻止提交项目事务。");
                                    var updatedGeometry = updatedMain.get_Geometry(new Options { ComputeReferences = false });
                                    var updatedSolids = new List<Solid>();
                                    if (updatedGeometry != null)
                                        BooleanGeometryEngine.ExtractSolids(updatedGeometry, updatedSolids);
                                    if (!updatedSolids.Any(solid => solid.Volume > 0))
                                        throw new InvalidOperationException("重新载入后的主体实例没有可读的正体积 Solid，已停止提交。");
                                    if ((state.DeleteTargetInst || state.DeleteTargetFam) &&
                                        allTargets.Any(t => doc.GetElement(t.Id) != null))
                                        throw new InvalidOperationException("部分目标实例删除后仍存在，阻止提交项目事务。");
                                    TransactionStatus loadStatus = tLoad.Commit();
                                    if (loadStatus != TransactionStatus.Committed)
                                        throw new InvalidOperationException($"重新载入族事务提交失败，状态：{loadStatus}。");
                                }
                                catch (Exception writeError)
                                {
                                    if (tLoad.GetStatus() == TransactionStatus.Started)
                                    {
                                        try
                                        {
                                            if (tLoad.RollBack() != TransactionStatus.RolledBack)
                                                throw new InvalidOperationException("项目载入事务回滚未确认。");
                                        }
                                        catch (Exception rollbackError)
                                        {
                                            throw new InvalidOperationException("项目载入事务回滚状态未知，请检查模型。",
                                                new AggregateException(writeError, rollbackError));
                                        }
                                    }
                                    throw;
                                }
                            }

                                TransactionStatus groupStatus = tg.Assimilate();
                                if (groupStatus != TransactionStatus.Committed)
                                    throw new InvalidOperationException($"布尔几何事务组合并失败，状态：{groupStatus}。");
                            }
                        catch (Exception writeError)
                        {
                            if (tg.GetStatus() == TransactionStatus.Started)
                            {
                                try
                                {
                                    if (tg.RollBack() != TransactionStatus.RolledBack)
                                        throw new InvalidOperationException("项目事务组回滚未确认。");
                                }
                                catch (Exception rollbackError)
                                {
                                    throw new InvalidOperationException("项目布尔事务组回滚状态未知，不能声明已全部撤销。",
                                        new AggregateException(writeError, rollbackError));
                                }
                            }
                            throw;
                        }
                    }
                }
                finally
                {
                    if (famDoc != null)
                    {
                        try { famDoc.Close(false); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[YangTools] 关闭族文档失败: {ex.Message}"); }
                    }
                    FamilyFileHelper.Cleanup(tempFile);
                }

                TaskDialog.Show("完成", "布尔几何处理完成。\n如结果不符合预期，可直接使用 Revit 撤销。");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", $"操作失败。\n错误信息: {ex.Message}");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
