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
        usage: "使用：先选详图直线并点击，或点击后框选/多选，再确认预览。\n限制：仅当前视图、未锁定、非组内详图直线；跳过重叠、分支、闭环和带标注/依赖关系的线组。优先保留原 ElementId，单次撤销；0.1 mm 是模型几何容差。")]
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
                    ICollection<ElementId> ids = uidoc.Selection.PickObjects(ObjectType.Element,
                        new DetailLineCleanup.SelectionFilter(view.Id), "选择当前视图至少两根详图直线，完成选择以预览")
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
                int inputCount = plans.Sum(p => p.Members.Count);
                var confirm = new TaskDialog("直线合并预览")
                {
                    MainInstruction = $"将 {inputCount} 根线合并为 {plans.Count} 根？",
                    MainContent = $"保留每组一条原 ElementId，调整其线段并删除其余线。\n跳过：含引用 {protectedGroups} 组；歧义 {ambiguous} 条。\n仅在无标注/依赖关系的线组上执行，一次 Ctrl+Z 撤销。",
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
                TaskDialog.Show("直线合并", $"已处理 {inputCount} 根线，保留 {retained.Count} 个原 ElementId。跳过受保护 {protectedGroups} 组、歧义 {ambiguous} 条；单次 Ctrl+Z 撤销。");
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
