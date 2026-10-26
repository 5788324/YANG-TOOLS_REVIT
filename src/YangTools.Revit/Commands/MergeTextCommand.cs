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
            var state = new MergeTextState();

            try
            {
                while (true)
                {
                    var window = new MergeTextWindow(state);
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

                    return ExecuteMerge(uiDoc, state, ref message);
                }
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("文本合并", ex);
                TaskDialog.Show("文本合并失败",
                    $"操作失败。\n\n错误信息：{ex.Message}\n\n诊断日志：{logPath}");
                message = ex.ToString();
                return Result.Failed;
            }
        }

        private static Result ExecuteMerge(UIDocument uiDoc, MergeTextState state, ref string message)
        {
            Document doc = uiDoc.Document;
            List<TextNote> textNotes = state.SelectedTexts
                .Where(note => note != null && note.Document.Equals(doc) && doc.GetElement(note.Id) != null)
                .ToList();
            // Keep source selection in the active view and avoid altering pinned/group-bound objects.
            if (textNotes.Any(note => note.OwnerViewId.GetIdValue() != doc.ActiveView.Id.GetIdValue()
                || note.Pinned || note.GroupId.GetIdValue() != ElementId.InvalidElementId.GetIdValue()))
            {
                TaskDialog.Show("文本合并", "所选文本包含其他视图或被固定/编组的元素，已取消。");
                return Result.Cancelled;
            }
            if (textNotes.Count < 2)
            {
                TaskDialog.Show("提示", "选择的文本已失效，请重新选择。 ");
                return Result.Cancelled;
            }

            MergeSortMode sortMode = Enum.TryParse(state.SortTag, true, out MergeSortMode parsed)
                ? parsed
                : MergeSortMode.SelectionOrder;
            switch (sortMode)
            {
                case MergeSortMode.ReadingOrder:
                    textNotes = TextLayoutMerger.Rows(TextLayoutMerger.Capture(doc.ActiveView, textNotes))
                        .SelectMany(row => row.Items.Select(x => x.Note)).ToList();
                    break;
                case MergeSortMode.XAsc: textNotes = textNotes.OrderBy(note => note.Coord.X).ToList(); break;
                case MergeSortMode.XDesc: textNotes = textNotes.OrderByDescending(note => note.Coord.X).ToList(); break;
                case MergeSortMode.YAsc: textNotes = textNotes.OrderBy(note => note.Coord.Y).ToList(); break;
                case MergeSortMode.YDesc: textNotes = textNotes.OrderByDescending(note => note.Coord.Y).ToList(); break;
            }

            string separator = state.UseNewlineSeparator ? "\r" : state.SeparatorText;
            string mergedText = sortMode == MergeSortMode.ReadingOrder
                ? TextLayoutMerger.ReadingText(doc.ActiveView, textNotes, state.CleanWhitespace, separator)
                : string.Join(separator, textNotes.Select(note => TextLayoutMerger.Clean(note.Text, state.CleanWhitespace)));
            if (string.IsNullOrWhiteSpace(mergedText))
            {
                TaskDialog.Show("提示", "合并后内容为空，已取消。 ");
                return Result.Cancelled;
            }

            string previewText = mergedText.Length > 320 ? mergedText.Substring(0, 320) + "…" : mergedText;
            if (TaskDialog.Show("文本合并预览", $"按当前顺序合并 {textNotes.Count} 个文本为 1 个。\n\n{previewText}\n\n该操作删除其余文本，可一次 Ctrl+Z 撤销。", TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel) != TaskDialogResult.Ok)
                return Result.Cancelled;

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
                        if (!deleted.Contains(removeId) || doc.GetElement(removeId) != null)
                            throw new InvalidOperationException($"文本 {removeId} 未确认删除，整次合并取消。");
                    }

                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"事务提交失败：{status}");

                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                        transaction.RollBack();
                    message = ex.ToString();
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
            TaskDialog.Show("完成", $"已合并 {textNotes.Count} 个文本，并保留第一个文本。{uiWarning}");
            return Result.Succeeded;
        }
    }
}
