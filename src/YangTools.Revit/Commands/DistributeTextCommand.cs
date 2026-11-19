using System;
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
        panelName: "文本工具区", buttonText: "等距分布",
        tooltip: "沿水平或垂直方向等距分布、对齐选中的文字。",
        largeIcon: "Icons/distribute_text_32.png", smallIcon: "Icons/distribute_text_16.png",
        usage: "使用：点击按钮，按提示拾取多个 TextNote，在窗口选择水平/垂直和间距或对齐方式。\n注意：依据 TextNote 插入点处理，效果不等同于视觉边界对齐。")]
    public class DistributeTextCommand : IExternalCommand
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
            var state = new DistributeTextState { SelectedTexts = TextNoteHelper.SelectedInActiveView(uiDoc) };
            if (state.SelectedTexts.Count > 1) state.SortTag = "YDesc";

            try
            {
                while (true)
                {
                    var window = new DistributeTextWindow(state);
                    new System.Windows.Interop.WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
                    bool? dialogResult = window.ShowDialog();

                    if (window.Action == TextToolDialogAction.SelectPrimary)
                    {
                        try
                        {
                            var references = uiDoc.Selection.PickObjects(
                                ObjectType.Element,
                                new TextNoteSelectionFilter(),
                                "请选择要分布或对齐的文本（至少两个）");

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
                            // 返回参数窗口，保留之前的输入和选择。
                        }
                        continue;
                    }

                    if (window.Action != TextToolDialogAction.Execute || dialogResult != true)
                        return Result.Cancelled;

                    return ExecuteDistribution(uiDoc, state, ref message);
                }
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("文本等距分布", ex);
                TaskDialog.Show("文本等距分布失败",
                    $"操作失败。\n\n错误信息：{ex.Message}\n\n诊断日志：{logPath}");
                message = ex.ToString();
                return Result.Failed;
            }
        }

        private static Result ExecuteDistribution(UIDocument uiDoc, DistributeTextState state, ref string message)
        {
            Document doc = uiDoc.Document;
            var textNotes = state.SelectedTexts
                .Where(note => note != null && note.Document.Equals(doc) && doc.GetElement(note.Id) != null)
                .ToList();

            if (textNotes.Count < 2)
            {
                TaskDialog.Show("提示", "选择的文本已失效，请重新选择。 ");
                return Result.Cancelled;
            }

            SortMode sortMode = ParseSortMode(state.SortTag);
            DistributeDirection direction = ParseDirection(state.DirectionTag);
            AlignMode alignMode = ParseAlignMode(state.AlignTag);

            XYZ right = doc.ActiveView.RightDirection.Normalize();
            XYZ up = doc.ActiveView.UpDirection.Normalize();
            double U(TextNote note) => note.Coord.DotProduct(right);
            double V(TextNote note) => note.Coord.DotProduct(up);
            switch (sortMode)
            {
                case SortMode.XAsc: textNotes = textNotes.OrderBy(U).ToList(); break;
                case SortMode.XDesc: textNotes = textNotes.OrderByDescending(U).ToList(); break;
                case SortMode.YAsc: textNotes = textNotes.OrderBy(V).ToList(); break;
                case SortMode.YDesc: textNotes = textNotes.OrderByDescending(V).ToList(); break;
            }

            using (var transaction = new Transaction(doc, "文本等距分布"))
            {
                try
                {
                    if (transaction.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法开始文本分布事务。");
                    double firstX = U(textNotes[0]);
                    double firstY = V(textNotes[0]);
                    double spacing = UnitUtils.ConvertToInternalUnits(state.SpacingMm, UnitTypeId.Millimeters);
                    int dirXSign = sortMode == SortMode.XDesc ? -1 : 1;
                    int dirYSign = sortMode == SortMode.YDesc ? -1 : 1;

                    if (sortMode == SortMode.SelectionOrder && textNotes.Count > 1)
                    {
                        dirXSign = U(textNotes[1]) < firstX ? -1 : 1;
                        dirYSign = V(textNotes[1]) < firstY ? -1 : 1;
                    }

                    for (int index = 0; index < textNotes.Count; index++)
                    {
                        TextNote note = textNotes[index];
                        double x = U(note);
                        double y = V(note);
                        double newX = x;
                        double newY = y;

                        if (alignMode == AlignMode.AlignX) newX = firstX;
                        else if (alignMode == AlignMode.AlignY) newY = firstY;

                        if (direction == DistributeDirection.X)
                            newX = firstX + dirXSign * spacing * index;
                        else if (direction == DistributeDirection.Y)
                            newY = firstY + dirYSign * spacing * index;

                        XYZ offset = right * (newX - x) + up * (newY - y);
                        if (offset.GetLength() > 1e-9)
                            ElementTransformUtils.MoveElement(doc, note.Id, offset);
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

            TextNoteHelper.RefreshAfterCommit(uiDoc, "文本分布后刷新");
            return Result.Succeeded;
        }

        private static SortMode ParseSortMode(string tag)
        {
            return Enum.TryParse(tag, true, out SortMode value) ? value : SortMode.SelectionOrder;
        }

        private static DistributeDirection ParseDirection(string tag)
        {
            return Enum.TryParse(tag, true, out DistributeDirection value) ? value : DistributeDirection.Y;
        }

        private static AlignMode ParseAlignMode(string tag)
        {
            return Enum.TryParse(tag, true, out AlignMode value) ? value : AlignMode.AlignX;
        }
    }
}
