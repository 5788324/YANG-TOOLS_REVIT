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
        panelName: "2D修改", buttonText: "隐藏边界",
        tooltip: "隐藏所选填充区域的边界线，不改变填充图案与几何。",
        largeIcon: "Icons/filledregion_invisible_32.png", smallIcon: "Icons/filledregion_invisible_16.png",
        usage: "使用：可先选填充区域再点击，也可先点击按钮再拾取，确认后设置全部边界线型。\n限制：仅当前视图、未锁定、非组内填充；优先通过系统 OST_InvisibleLines 识别合法线型。重叠的独立详图线不会被隐藏。一次 Ctrl+Z 撤销。")]
    public sealed class SetSelectedFilledRegionInvisibleBoundaryCommand : IExternalCommand
    {
        private sealed class FilledFilter : ISelectionFilter
        {
            private readonly ElementId viewId;
            public FilledFilter(ElementId id) { viewId = id; }
            public bool AllowElement(Element element) => element is FilledRegion filled &&
                filled.OwnerViewId.GetIdValue() == viewId.GetIdValue() && !filled.Pinned &&
                filled.GroupId.GetIdValue() == ElementId.InvalidElementId.GetIdValue();
            public bool AllowReference(Reference r, XYZ p) => false;
        }
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            try
            {
                List<FilledRegion> selected = ReadSelected(uidoc, view, uidoc.Selection.GetElementIds());
                if (selected.Count == 0)
                {
                    var ids = uidoc.Selection.PickObjects(ObjectType.Element,
                        new FilledFilter(view.Id), "请选择要隐藏边界的填充区域，完成选择以预览")
                        .Select(r => r.ElementId).Distinct().ToList();
                    selected = ReadSelected(uidoc, view, ids);
                }
                if (selected.Count == 0)
                {
                    TaskDialog.Show("隐藏边界", "没有选中可修改的当前视图填充区域。");
                    return Result.Cancelled;
                }
                IList<ElementId> valid = FilledRegion.GetValidLineStyleIdsForFilledRegion(doc);
                long builtin = (long)BuiltInCategory.OST_InvisibleLines;
                List<ElementId> builtinStyles = valid.Where(id =>
                    (doc.GetElement(id) as GraphicsStyle)?.GraphicsStyleCategory?.Id.GetIdValue() == builtin).ToList();
                // Only if the category lookup fails, use a unique explicitly recognized name.
                List<ElementId> possible = builtinStyles;
                if (possible.Count == 0)
                {
                    possible = valid.Where(id =>
                    {
                        string name = (doc.GetElement(id) as GraphicsStyle)?.Name?.Trim() ?? "";
                        return string.Equals(name, "<Invisible lines>", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(name, "<不可见线>", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(name, "<隐形线>", StringComparison.OrdinalIgnoreCase);
                    }).ToList();
                }
                if (possible.Count != 1 || !FilledRegion.IsValidLineStyleIdForFilledRegion(doc, possible[0]))
                {
                    TaskDialog.Show("隐藏边界", $"未找到唯一有效的系统隐形线样式（匹配数 {possible.Count}）。未进行修改，请检查项目线型。");
                    return Result.Cancelled;
                }
                var preview = new TaskDialog("隐藏边界预览")
                {
                    MainInstruction = $"将 {selected.Count} 个填充区域的边界设为隐形线？",
                    MainContent = "使用 Revit 原生 FilledRegion.SetLineStyleId，不重建填充或更改填充图案。独立绘制在填充上的详图线不会自动隐藏。一次 Ctrl+Z 撤销。",
                    CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel
                };
                if (preview.Show() != TaskDialogResult.Ok) return Result.Cancelled;
                if (data.Application.ActiveUIDocument == null ||
                    !data.Application.ActiveUIDocument.Document.Equals(doc) ||
                    doc.ActiveView.Id.GetIdValue() != view.Id.GetIdValue() || doc.IsReadOnly) return Result.Cancelled;
                using (var tx = new Transaction(doc, "所选填充区域设置隐形边界"))
                {
                    if (tx.Start() != TransactionStatus.Started) return Result.Failed;
                    try
                    {
                        foreach (FilledRegion filled in selected)
                        {
                            if (!filled.IsValidObject || filled.Pinned ||
                                filled.GroupId.GetIdValue() != ElementId.InvalidElementId.GetIdValue())
                                throw new InvalidOperationException("有填充区域已失效或不允许修改，事务回滚。");
                            filled.SetLineStyleId(possible[0]);
                        }
                        if (tx.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("隐形边界事务提交失败。");
                    }
                    catch { if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); throw; }
                }
                TaskDialog.Show("隐藏边界", $"已向 {selected.Count} 个填充应用隐形边界线型。若仍能看到边线，请检查是否叠加独立详图线。单次 Ctrl+Z 撤销。");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                message = ex.ToString();
                DiagnosticLog.WriteError("隐藏边界", ex);
                TaskDialog.Show("隐藏边界失败", ex.Message + "\n如事务失败则已尝试回滚。");
                return Result.Failed;
            }
        }
        private static List<FilledRegion> ReadSelected(UIDocument uidoc, View view, IEnumerable<ElementId> ids) =>
            ids.Distinct().Select(uidoc.Document.GetElement).OfType<FilledRegion>()
                .Where(x => x.IsValidObject && !x.Pinned &&
                    x.GroupId.GetIdValue() == ElementId.InvalidElementId.GetIdValue() &&
                    x.OwnerViewId.GetIdValue() == view.Id.GetIdValue()).ToList();
    }
}
