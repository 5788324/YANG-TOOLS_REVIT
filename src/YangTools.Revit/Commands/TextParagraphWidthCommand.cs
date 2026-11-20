using System;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "文本工具区", buttonText: "文字宽度",
        tooltip: "读取各条文字的纸面段落宽度，或将选中文字统一设宽。",
        largeIcon: "Icons/text_paragraph_width_32.png", smallIcon: "Icons/text_paragraph_width_16.png",
        usage: "使用：选中文字，查看纸面宽度（毫米），或输入统一宽度并应用。\n注意：设宽会开启自动换行。")]
    public sealed class TextParagraphWidthCommand : IExternalCommand
    {
        private const double MillimetresPerFoot = 304.8;
        private const double WidthTolerance = 1e-8;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            UIDocument? uiDoc = app.ActiveUIDocument;
            if (uiDoc == null) return Result.Cancelled;
            Document doc = uiDoc.Document;
            ElementId viewId = uiDoc.ActiveView.Id;

            try
            {
                var notes = TextNoteHelper.SelectedInActiveView(uiDoc).ToList();
                if (notes.Count == 0)
                {
                    notes = uiDoc.Selection.PickObjects(ObjectType.Element, new TextNoteSelectionFilter(),
                            "选择要检查段落宽度的文字，点击完成")
                        .Select(reference => reference.ElementId).Distinct().Select(id => doc.GetElement(id))
                        .OfType<TextNote>().Where(note => note.OwnerViewId == viewId).ToList();
                }
                if (notes.Count == 0) return Result.Cancelled;

                double min = 0, max = double.PositiveInfinity;
                foreach (ElementId type in notes.Select(note => note.GetTypeId()).Distinct())
                {
                    min = Math.Max(min, TextNote.GetMinimumAllowedWidth(doc, type));
                    max = Math.Min(max, TextNote.GetMaximumAllowedWidth(doc, type));
                }
                string? readOnlyReason = null;
                if (doc.IsReadOnly)
                    readOnlyReason = "当前项目为只读，可查看宽度。";
                else if (notes.Any(note => note.Pinned || note.GroupId != ElementId.InvalidElementId))
                    readOnlyReason = "选区包含固定或组内文字。可查看宽度，修改时请选择未固定且不在组内的文字。";
                else if (min > max)
                    readOnlyReason = "选中文字类型没有共同允许的宽度。";

                var rows = notes.Select(note => new TextParagraphWidthWindow.Row(note.Text, note.Id.ToString(),
                    note.Width * MillimetresPerFoot, note.IsTextWrappingActive)).ToList();
                var window = new TextParagraphWidthWindow(rows, min * MillimetresPerFoot, max * MillimetresPerFoot, readOnlyReason);
                new WindowInteropHelper(window).Owner = app.MainWindowHandle;
                if (window.ShowDialog() != true) return Result.Cancelled;
                double width = Math.Max(min, Math.Min(max, window.WidthMm / MillimetresPerFoot));
                var changes = notes.Where(note => !note.IsTextWrappingActive || Math.Abs(note.Width - width) > WidthTolerance).ToList();
                if (changes.Count == 0) return Result.Succeeded;

                using var transaction = new Transaction(doc, "修改文字段落宽度");
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法开始修改文字宽度。");
                foreach (TextNote note in changes) note.Width = width;
                doc.Regenerate();
                if (changes.Any(note => !note.IsTextWrappingActive || Math.Abs(note.Width - width) > WidthTolerance))
                    throw new InvalidOperationException("Revit 返回的文字宽度与输入不一致。");
                if (transaction.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit 未提交文字宽度修改。");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = $"文字宽度操作失败：{ex.Message}";
                return Result.Failed;
            }
        }
    }
}
