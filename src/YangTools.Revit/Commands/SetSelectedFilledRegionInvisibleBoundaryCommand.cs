using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("模型修改区", "填充隐形边界", "将选中填充区域的全部边界线切换为 <Invisible lines>；不重新创建填充，支持一次撤销", "Icons/override_cleaner_32.png", "Icons/override_cleaner_16.png")]
    public sealed class SetSelectedFilledRegionInvisibleBoundaryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            try
            {
                var selected = uidoc.Selection.GetElementIds().Select(doc.GetElement)
                    .OfType<FilledRegion>().Where(x => x.IsValidObject && !x.Pinned &&
                        x.GroupId.GetIdValue() == ElementId.InvalidElementId.GetIdValue() &&
                        x.OwnerViewId.GetIdValue() == view.Id.GetIdValue()).ToList();
                if (selected.Count == 0)
                {
                    TaskDialog.Show("填充隐形边界", "请先选中当前视图中的填充区域（不处理锁定或组内填充）。");
                    return Result.Cancelled;
                }
                IList<ElementId> validStyles = FilledRegion.GetValidLineStyleIdsForFilledRegion(doc);
                var choices = validStyles.Select(id => new { Id = id, Style = doc.GetElement(id) })
                    .Where(x => x.Style != null &&
                        (string.Equals(x.Style.Name.Trim(), "<Invisible lines>", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Style.Name.Trim(), "<Invisible Lines>", StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (choices.Count != 1)
                {
                    TaskDialog.Show("填充隐形边界", "无法唯一匹配有效边界线样式 <Invisible lines>。请检查当前项目线型名称。未进行修改。");
                    return Result.Cancelled;
                }
                var dialog = new TaskDialog("填充隐形边界") {
                    MainInstruction = $"将 {selected.Count} 个填充区域的所有边界设为 <Invisible lines>？",
                    MainContent = "仅修改所选填充边界线型，不修改填充图案、边界几何或填充类型。可一次 Ctrl+Z 撤销。",
                    CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel
                };
                if (dialog.Show() != TaskDialogResult.Ok) return Result.Cancelled;
                if (data.Application.ActiveUIDocument?.Document == null ||
                    !doc.Equals(data.Application.ActiveUIDocument.Document) ||
                    doc.ActiveView.Id.GetIdValue() != view.Id.GetIdValue() || doc.IsReadOnly)
                    return Result.Cancelled;
                using (var tx = new Transaction(doc, "所选填充区域设置隐形边界"))
                {
                    if (tx.Start() != TransactionStatus.Started) return Result.Failed;
                    try
                    {
                        foreach (FilledRegion filled in selected)
                        {
                            if (!filled.IsValidObject) throw new InvalidOperationException("选中的填充已失效。");
                            filled.SetLineStyleId(choices[0].Id);
                        }
                        if (tx.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("填充边界事务未提交。");
                    }
                    catch { if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); throw; }
                }
                TaskDialog.Show("填充隐形边界", $"已处理 {selected.Count} 个填充区域，可一次 Ctrl+Z 撤销。");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                DiagnosticLog.WriteError("填充隐形边界", ex);
                return Result.Failed;
            }
        }
    }
}
