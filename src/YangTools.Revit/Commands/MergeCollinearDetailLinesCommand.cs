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
        panelName: "2D修改", buttonText: "直线合并",
        tooltip: "合并相同线型、首尾连续且没有重叠或分支的详图直线。",
        largeIcon: "Icons/merge_lines_32.png", smallIcon: "Icons/merge_lines_16.png",
        usage: "使用：选中至少两根详图直线后点击按钮，或点击按钮后选线并点“完成”。\n限制：仅当前视图、未锁定、非组内详图直线；跳过重叠、分支、闭环和带标注/依赖关系的线组。优先保留原 ElementId，单次撤销；0.1 mm 是模型几何容差。")]
    public sealed class MergeCollinearDetailLinesCommand : IExternalCommand
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
                    var preselected = lines.Select(line => new Reference(line.Element)).ToList();
                    ICollection<ElementId> ids = uidoc.Selection.PickObjects(ObjectType.Element,
                        new DetailLineCleanup.SelectionFilter(view.Id), "选择至少两根详图直线，点“完成”即合并。", preselected)
                        .Select(r => r.ElementId).Distinct().ToList();
                    lines = DetailLineCleanup.GetSegments(doc, view, ids);
                }
                if (lines.Count < 2 || lines.Count > 600)
                {
                    TaskDialog.Show("直线合并", "请选择 2–600 根有效详图直线。模型线、圆弧、锁定及组内对象会被跳过。");
                    return Result.Cancelled;
                }
                List<DetailLineCleanup.Plan> plans = DetailLineCleanup.JoinPlans(lines, out int ambiguous);
                if (plans.Count == 0)
                {
                    TaskDialog.Show("直线合并", $"没有发现安全、无重叠的连续线链；跳过歧义线段 {ambiguous} 条，未修改。");
                    return Result.Cancelled;
                }
                HashSet<long> protectedIds = DetailLineCleanup.ProtectedLineIds(doc, plans);
                int protectedGroups = plans.Count(p => p.Members.Any(s => protectedIds.Contains(s.Element.Id.GetIdValue())));
                plans = plans.Where(p => p.Members.All(s => !protectedIds.Contains(s.Element.Id.GetIdValue()))).ToList();
                if (plans.Count == 0)
                {
                    TaskDialog.Show("直线合并", $"候选线组涉及标注引用或依赖元素，已安全跳过 {protectedGroups} 组，未修改。");
                    return Result.Cancelled;
                }
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
                    retained = DetailLineCleanup.Apply(doc, plans, "合并共线详图线");
                }
                catch
                {
                    // If the model transaction rolled back, restore only still-valid IDs.
                    try { uidoc.Selection.SetElementIds(selectionBefore.Where(id => doc.GetElement(id) != null).ToList()); }
                    catch (Exception restoreEx) { DiagnosticLog.WriteError("还原选中对象", restoreEx); }
                    throw;
                }
                try { uidoc.Selection.SetElementIds(retained); }
                catch (Exception ex) { DiagnosticLog.WriteError("直线合并选择结果", ex); }
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                message = ex.ToString();
                DiagnosticLog.WriteError("直线合并", ex);
                TaskDialog.Show("直线合并失败", ex.Message + "\n如事务失败则已尝试回滚。");
                return Result.Failed;
            }
        }
    }
}
