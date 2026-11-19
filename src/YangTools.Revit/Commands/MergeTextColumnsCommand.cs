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
    [RibbonButton(
        panelName: "文本工具区", buttonText: "双列合并",
        tooltip: "将两列 TextNote 按行合并，可选单文本或双文本输出。",
        largeIcon: "Icons/merge_columns_32.png", smallIcon: "Icons/merge_columns_16.png",
        usage: "使用：选择当前视图至少四个 TextNote，点击按钮并选择输出为一个多行文本或两个分列多行文本。\n建议：比例字体（如 Arial）优先使用双文本方式；原文字将被替换。")]
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
                var filter = new TextSelectionFilter(view.Id);
                List<TextNote> notes = TextNoteHelper.SelectedInActiveView(uiDoc).Where(filter.AllowElement).ToList();
                if (notes.Count < 4)
                {
                    notes = uiDoc.Selection.PickObjects(ObjectType.Element, filter,
                        "请选择左右两列的文字，至少四个").Select(r => r.ElementId).Distinct()
                        .Select(doc.GetElement).OfType<TextNote>().ToList();
                }
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
                if (!TextLayoutMerger.CanMerge(notes, out string formatError))
                {
                    TaskDialog.Show("双列文字合并", formatError);
                    return Result.Cancelled;
                }
                if (notes.Any(note => note.HorizontalAlignment != HorizontalTextAlignment.Left
                    || note.VerticalAlignment != VerticalTextAlignment.Top))
                {
                    TaskDialog.Show("双列文字合并", "请选用左对齐、顶端对齐的文字，确保合并后的列位置正确。");
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
                    MainContent = "双文本保留两列位置和行对应，行距由文字类型决定。单文本以空格近似对齐。每个原文字内的换行和制表符会压为普通空格。替换可一次撤销。",
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
                var positions = rows.SelectMany(row => row.Items).ToList();
                TextNote leftAnchor = positions.Where(x => x.U < cut).OrderByDescending(x => x.V).First().Note;
                TextNote rightAnchor = positions.Where(x => x.U >= cut).OrderByDescending(x => x.V).First().Note;
                XYZ up = view.UpDirection.Normalize();
                double top = positions.Max(position => position.V);
                List<string> finalTexts;
                if (single)
                {
                    int width = Math.Min(160, leftRows.Max(x => x.Length) + 4);
                    finalTexts = new List<string> { string.Join("\r", leftRows.Zip(rightRows,
                        (l, r) => r.Length == 0 ? l : l.PadRight(Math.Max(width, l.Length + 1)) + r)) };
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
                            XYZ point = anchor.Coord + up * (top - anchor.Coord.DotProduct(up));
                            TextNote output = TextNote.Create(doc, view.Id, point, finalTexts[i], options);
                            if (output == null) throw new InvalidOperationException("无法创建合并后的 TextNote。 ");
                            created.Add(output.Id);
                        }
                        foreach (TextNote original in notes)
                        {
                            var deleted = doc.Delete(original.Id);
                            if (deleted.Count != 1 || !deleted.Contains(original.Id))
                                throw new InvalidOperationException("删除原文本会影响关联元素，整次合并取消。");
                        }
                        if (transaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("文本合并提交失败。 ");
                    }
                    catch { if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack(); throw; }
                }
                try { uiDoc.Selection.SetElementIds(created); }
                catch (Exception ex) { DiagnosticLog.WriteError("双列文本合并后选中", ex); }
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
            public bool AllowElement(Element e) => e is TextNote t && t.OwnerViewId == _view
                && !t.Pinned && t.GroupId == ElementId.InvalidElementId;
            public bool AllowReference(Reference r, XYZ p) => false;
        }
    }
}
