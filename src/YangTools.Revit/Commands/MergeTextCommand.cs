using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.Models;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "文本工具区", buttonText: "文本合并",
        tooltip: "将多个 TextNote 按指定分隔符合并。",
        largeIcon: "Icons/merge_text_32.png", smallIcon: "Icons/merge_text_16.png",
        usage: "使用：点击按钮并拾取多个 TextNote，在窗口设置合并顺序与分隔符，确认后执行。\n注意：合并会修改原文字对象；需保留独立段落时不要执行。")]
    public class MergeTextCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument? uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("提示", "请先打开一个项目文档。 ");
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;
            var state = new MergeTextState { SelectedTexts = TextNoteHelper.SelectedInActiveView(uiDoc) };

            try
            {
                while (true)
                {
                    var window = new MergeTextWindow(state, () => PrepareMerge(doc, state));
                    new System.Windows.Interop.WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
                    bool? dialogResult = window.ShowDialog();

                    if (window.Action == TextToolDialogAction.SelectPrimary)
                    {
                        try
                        {
                            var references = uiDoc.Selection.PickObjects(
                                ObjectType.Element,
                                new TextNoteSelectionFilter(),
                                "请选择要合并的文本（至少两个）");
                            state.SelectedTexts = references
                                .Select(reference => doc.GetElement(reference) as TextNote)
                                .Where(note => note != null)
                                .Cast<TextNote>()
                                .GroupBy(note => note.Id)
                                .Select(group => group.First())
                                .ToList();
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                        }
                        continue;
                    }

                    if (window.Action != TextToolDialogAction.Execute || dialogResult != true)
                        return Result.Cancelled;

                    return ExecuteMerge(uiDoc, window.PreparedMerge.Notes, window.PreparedMerge.Text);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("文本合并", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static (List<TextNote> Notes, string Text) PrepareMerge(Document doc, MergeTextState state)
        {
            List<TextNote> textNotes = state.SelectedTexts
                .Where(note => note.IsValidObject && note.Document.Equals(doc) && doc.GetElement(note.Id) != null)
                .ToList();
            // Keep source selection in the active view and avoid altering pinned/group-bound objects.
            if (textNotes.Any(note => note.OwnerViewId.GetIdValue() != doc.ActiveView.Id.GetIdValue()
                || note.Pinned || note.GroupId.GetIdValue() != ElementId.InvalidElementId.GetIdValue()))
                throw new InvalidOperationException("请选择当前视图内未固定、未编组的文本。");
            if (textNotes.Count < 2)
                throw new InvalidOperationException("请至少选择两个文本。");
            if (!TextLayoutMerger.CanMerge(textNotes, out string formatError))
                throw new InvalidOperationException(formatError);

            XYZ right = doc.ActiveView.RightDirection.Normalize();
            XYZ up = doc.ActiveView.UpDirection.Normalize();
            var rows = new List<TextLayoutMerger.ReadingRow>();
            MergeSortMode sortMode = Enum.TryParse(state.SortTag, true, out MergeSortMode parsed)
                ? parsed
                : MergeSortMode.SelectionOrder;
            switch (sortMode)
            {
                case MergeSortMode.ReadingOrder:
                    rows = TextLayoutMerger.Rows(TextLayoutMerger.Capture(doc.ActiveView, textNotes));
                    textNotes = rows.SelectMany(row => row.Items.Select(x => x.Note)).ToList();
                    break;
                case MergeSortMode.XAsc: textNotes = textNotes.OrderBy(note => note.Coord.DotProduct(right)).ToList(); break;
                case MergeSortMode.XDesc: textNotes = textNotes.OrderByDescending(note => note.Coord.DotProduct(right)).ToList(); break;
                case MergeSortMode.YAsc: textNotes = textNotes.OrderBy(note => note.Coord.DotProduct(up)).ToList(); break;
                case MergeSortMode.YDesc: textNotes = textNotes.OrderByDescending(note => note.Coord.DotProduct(up)).ToList(); break;
            }

            string separator = state.UseNewlineSeparator ? "\r" : state.SeparatorText;
            string mergedText = sortMode == MergeSortMode.ReadingOrder
                ? TextLayoutMerger.ReadingText(rows, state.CleanWhitespace, separator)
                : string.Join(separator, textNotes.Select(note => TextLayoutMerger.Clean(note.Text, state.CleanWhitespace)));
            if (string.IsNullOrWhiteSpace(mergedText))
                throw new InvalidOperationException("合并后的内容为空。");
            return (textNotes, mergedText);
        }

        private static Result ExecuteMerge(UIDocument uiDoc, List<TextNote> textNotes, string mergedText)
        {
            Document doc = uiDoc.Document;
            using (var transaction = new Transaction(doc, "文本合并"))
            {
                try
                {
                    if (transaction.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("文本合并事务无法开始。");
                    TextNote survivor = textNotes[0];
                    survivor.Text = mergedText;
                    for (int index = 1; index < textNotes.Count; index++)
                    {
                        ElementId removeId = textNotes[index].Id;
                        var deleted = doc.Delete(removeId);
                        if (deleted.Count != 1 || !deleted.Contains(removeId))
                            throw new InvalidOperationException("删除原文本会影响关联元素，整次合并取消。");
                    }

                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"事务提交失败：{status}");

                }
                catch
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                        transaction.RollBack();
                    throw;
                }
            }

            // Revit 事务已经提交，选择或刷新失败不能改写模型操作结果。
            string uiWarning = string.Empty;
            try
            {
                uiDoc.Selection.SetElementIds(new[] { textNotes[0].Id });
                uiDoc.RefreshActiveView();
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("文本合并提交后界面刷新", ex);
                uiWarning = "\n提示：合并已经提交，但选择或刷新视图失败。";
            }
            if (uiWarning.Length > 0) TaskDialog.Show("文本已合并", uiWarning.Trim());
            return Result.Succeeded;
        }
    }
}
