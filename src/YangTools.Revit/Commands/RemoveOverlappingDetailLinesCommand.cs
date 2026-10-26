using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "2D修改", buttonText: "重叠清理",
        tooltip: "清理有实际重叠区间的同线型详图直线，保留覆盖范围。",
        largeIcon: "Icons/overlap_lines_32.png", smallIcon: "Icons/overlap_lines_16.png",
        usage: "使用：先选至少两条详图直线，或点击按钮后选线，再确认预览。\n限制：仅当前视图、共线同线型且有正长度重叠；只接触端点、交叉、模型线、圆弧、锁定、组内及有关联标注的线组跳过。优先保留原 ElementId，可单次撤销。")]
    public sealed class RemoveOverlappingDetailLinesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            try
            {
                List<DetailLineCleanup.Segment> lines = DetailLineCleanup.GetSegments(doc, view, uidoc.Selection.GetElementIds());
                if (lines.Count < 2)
                {
                    ICollection<ElementId> ids = uidoc.Selection.PickObjects(ObjectType.Element,
                        new DetailLineCleanup.SelectionFilter(view.Id), "选择当前视图至少两根重叠详图直线，完成选择以预览")
                        .Select(r => r.ElementId).Distinct().ToList();
                    lines = DetailLineCleanup.GetSegments(doc, view, ids);
                }
                if (lines.Count < 2 || lines.Count > 1500)
                {
                    TaskDialog.Show("重叠清理", "请选择 2–1500 根有效详图直线。其他类别、锁定或组内对象会被跳过。");
                    return Result.Cancelled;
                }
                List<DetailLineCleanup.Plan> plans = DetailLineCleanup.OverlapPlans(lines, out int ambiguous);
                if (plans.Count == 0)
                {
                    TaskDialog.Show("重叠清理", $"未发现正长度重叠的安全线组；歧义线段 {ambiguous} 条，未修改。");
                    return Result.Cancelled;
                }
                HashSet<long> protectedIds = DetailLineCleanup.ProtectedLineIds(doc, plans);
                int protectedGroups = plans.Count(p => p.Members.Any(s => protectedIds.Contains(s.Element.Id.GetIdValue())));
                plans = plans.Where(p => p.Members.All(s => !protectedIds.Contains(s.Element.Id.GetIdValue()))).ToList();
                if (plans.Count == 0)
                {
                    TaskDialog.Show("重叠清理", $"候选线组存在尺寸标注或依赖对象，已安全跳过 {protectedGroups} 组，未修改。");
                    return Result.Cancelled;
                }
                int count = plans.Sum(p => p.Members.Count);
                var confirm = new TaskDialog("重叠清理预览")
                {
                    MainInstruction = $"将 {count} 根重叠线整理为 {plans.Count} 根？",
                    MainContent = $"优先保留一条原 ElementId，其余重叠线删除。\n跳过：受保护 {protectedGroups} 组、歧义 {ambiguous} 条。\n仅选定且安全的线组改变，保留几何覆盖范围，可一次 Ctrl+Z 撤销。",
                    CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel
                };
                if (confirm.Show() != TaskDialogResult.Ok) return Result.Cancelled;
                if (data.Application.ActiveUIDocument == null ||
                    !data.Application.ActiveUIDocument.Document.Equals(doc) ||
                    doc.ActiveView.Id.GetIdValue() != view.Id.GetIdValue() || doc.IsReadOnly) return Result.Cancelled;
                // Clear the UI preselection before changing/deleting its members.
                // Revit may otherwise return additional ephemeral selection-dependent IDs
                // from Document.Delete. Do NOT relax the dependency/rollback guard in Apply.
                List<ElementId> selectionBefore = uidoc.Selection.GetElementIds().ToList();
                List<ElementId> retained;
                try
                {
                    uidoc.Selection.SetElementIds(new List<ElementId>());
                    retained = DetailLineCleanup.Apply(doc, plans, "清理重叠详图直线");
                }
                catch
                {
                    // If the model transaction rolled back, restore only still-valid IDs.
                    try { uidoc.Selection.SetElementIds(selectionBefore.Where(id => doc.GetElement(id) != null).ToList()); }
                    catch (Exception restoreEx) { DiagnosticLog.WriteError("还原选中对象", restoreEx); }
                    throw;
                }
                try { uidoc.Selection.SetElementIds(retained); }
                catch (Exception ex) { DiagnosticLog.WriteError("重叠清理选择结果", ex); }
                TaskDialog.Show("重叠清理", $"已整理 {count} 根重叠线，保留 {retained.Count} 个原 ElementId。跳过受保护 {protectedGroups} 组、歧义 {ambiguous} 条；单次 Ctrl+Z 撤销。");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                message = ex.ToString();
                DiagnosticLog.WriteError("重叠清理", ex);
                TaskDialog.Show("重叠清理失败", ex.Message + "\n如事务失败则已尝试回滚。");
                return Result.Failed;
            }
        }
    }
}
