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
    /// <summary>Two-column text selection: single multiline note with space padding, or two aligned notes.</summary>
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("文本工具区", "双列文字合并", "识别左右两列及每行位置，可输出一个空格对齐的多行文字，或两个精准定位的多行文字", "Icons/merge_columns_32.png", "Icons/merge_columns_16.png")]
    public sealed class MergeTextColumnsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null) return Result.Cancelled;
            Document doc = uiDoc.Document;
            View view = doc.ActiveView;
            try
            {
                var ids = uiDoc.Selection.GetElementIds().ToList();
                if (ids.Count < 4)
                {
                    ids = uiDoc.Selection.PickObjects(ObjectType.Element, new TextSelectionFilter(view.Id),
                        "请选择左右两列所有文本，至少四个 TextNote").Select(r => r.ElementId).Distinct().ToList();
                }
                List<TextNote> notes = ids.Select(id => doc.GetElement(id) as TextNote).Where(x => x != null)
                    .Cast<TextNote>().Where(x => x.OwnerViewId.GetIdValue() == view.Id.GetIdValue() && !x.Pinned && x.GroupId.GetIdValue() == ElementId.InvalidElementId.GetIdValue())
                    .GroupBy(x => x.Id.GetIdValue()).Select(x => x.First()).ToList();
                if (notes.Count < 4 || notes.Count > 300)
                {
                    TaskDialog.Show("双列文字合并", "只处理本视图内 4–300 个未锁定、不在组内的 TextNote；请重新选择。");
                    return Result.Cancelled;
                }
                // Different rotations could not be preserved by one text box; do not silently alter them.
                XYZ xDir = view.RightDirection.Normalize();
                if (notes.Any(note => (note.BaseDirection.Normalize() - xDir).GetLength() > 0.01))
                {
                    TaskDialog.Show("双列文字合并", "包含旋转或反向文字。本版仅处理与当前视图横向一致的 TextNote，不改变原文本。");
                    return Result.Cancelled;
                }
                if (!TextLayoutMerger.TryColumns(view, notes, out var rows, out double cut, out string error))
                {
                    TaskDialog.Show("双列文字合并", error);
                    return Result.Cancelled;
                }
                var leftRows = new List<string>();
                var rightRows = new List<string>();
                foreach (var row in rows)
                {
                    leftRows.Add(string.Join(" ", row.Items.Where(x => x.U < cut)
                        .Select(x => TextLayoutMerger.Clean(x.Note.Text, true)).Where(x => x.Length > 0)));
                    rightRows.Add(string.Join(" ", row.Items.Where(x => x.U >= cut)
                        .Select(x => TextLayoutMerger.Clean(x.Note.Text, true)).Where(x => x.Length > 0)));
                }
                if (leftRows.All(string.IsNullOrWhiteSpace) || rightRows.All(string.IsNullOrWhiteSpace))
                {
                    TaskDialog.Show("双列文字合并", "未找到可用的左右列文本内容。"); return Result.Cancelled;
                }
                var dialog = new TaskDialog("双列文字合并")
                {
                    MainInstruction = $"已识别 {rows.Count} 行、共 {notes.Count} 个文字。选择输出方式",
                    MainContent = "单个多行文字：用空格补齐右列，比例字体下只能近似对齐；精准方式：创建左右两个多行 TextNote，保留两列位置和行对应。原文字将在一个事务中替换，Ctrl+Z 一次撤销。",
                    CommonButtons = TaskDialogCommonButtons.Cancel
                };
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "单个多行文字（右列空格近似对齐）");
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "两个多行文字（两列定位更精确，推荐）");
                TaskDialogResult selected = dialog.Show();
                bool single = selected == TaskDialogResult.CommandLink1;
                if (!single && selected != TaskDialogResult.CommandLink2) return Result.Cancelled;
                // Must remain in the source view/document after UI prompts.
                if (!commandData.Application.ActiveUIDocument.Document.Equals(doc)
                    || doc.ActiveView.Id.GetIdValue() != view.Id.GetIdValue() || doc.IsReadOnly)
                    return Result.Cancelled;
                var positions = TextLayoutMerger.Capture(view, notes);
                TextNote leftAnchor = positions.Where(x => x.U < cut).OrderByDescending(x => x.V).First().Note;
                TextNote rightAnchor = positions.Where(x => x.U >= cut).OrderByDescending(x => x.V).First().Note;
                // Preserve TextNote type only when consistent. Mixed styles would be lost in a merged note.
                if (notes.Any(x => x.GetTypeId().GetIdValue() != leftAnchor.GetTypeId().GetIdValue()))
                {
                    TaskDialog.Show("双列文字合并", "选中对象包含不同文字类型，合并会丢失格式。请先统一文字类型后再试。");
                    return Result.Cancelled;
                }
                List<string> finalTexts;
                if (single)
                {
                    int width = Math.Min(160, leftRows.Max(x => x.Length) + 4);
                    finalTexts = new List<string> { string.Join("\r", leftRows.Zip(rightRows,
                        (l, r) => (l.Length == 0 ? new string(' ', width) : l.PadRight(width)) + r).Select(x => x.TrimEnd())) };
                }
                else
                {
                    finalTexts = new List<string> { string.Join("\r", leftRows), string.Join("\r", rightRows) };
                }
                if (finalTexts.Any(string.IsNullOrWhiteSpace)) return Result.Cancelled;
                var created = new List<ElementId>();
                using (var transaction = new Transaction(doc, "双列文字合并"))
                {
                    if (transaction.Start() != TransactionStatus.Started) return Result.Failed;
                    try
                    {
                        for (int i = 0; i < finalTexts.Count; i++)
                        {
                            TextNote anchor = i == 0 ? leftAnchor : rightAnchor;
                            // Unwrapped TextNote lets explicit CR delimit each row. A fixed width may
                            // introduce automatic wrapping and silently break column alignment.
                            var options = new TextNoteOptions(anchor.GetTypeId());
                            TextNote output = TextNote.Create(doc, view.Id, anchor.Coord, finalTexts[i], options);
                            if (output == null) throw new InvalidOperationException("无法创建合并后的 TextNote。 ");
                            created.Add(output.Id);
                        }
                        foreach (TextNote original in notes)
                        {
                            var deleted = doc.Delete(original.Id);
                            if (!deleted.Contains(original.Id)) throw new InvalidOperationException("原 TextNote 删除失败，事务回滚。 ");
                        }
                        if (transaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("文本合并提交失败。 ");
                    }
                    catch { if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack(); throw; }
                }
                try { uiDoc.Selection.SetElementIds(created); }
                catch (Exception ex) { DiagnosticLog.WriteError("双列文本合并后选中", ex); }
                TaskDialog.Show("完成", $"{notes.Count} 个文本已变成 {created.Count} 个多行 TextNote。一次 Ctrl+Z 撤销。" +
                    (single ? "\n提示：比例字体下空格无法保证精确的双列边界。" : ""));
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("双列文字合并", ex);
                message = ex.ToString();
                TaskDialog.Show("双列文字合并失败", ex.Message + "\n模型事务已尝试回滚。");
                return Result.Failed;
            }
        }
        private sealed class TextSelectionFilter : ISelectionFilter
        {
            private readonly ElementId _view;
            public TextSelectionFilter(ElementId view) { _view = view; }
            public bool AllowElement(Element e) => e is TextNote t && t.OwnerViewId.GetIdValue() == _view.GetIdValue() && !t.Pinned;
            public bool AllowReference(Reference r, XYZ p) => false;
        }
    }
}
