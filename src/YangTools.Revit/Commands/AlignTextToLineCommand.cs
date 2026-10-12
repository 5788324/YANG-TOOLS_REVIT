using System;
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
    [RibbonButton("文本工具区", "对齐到线", "将文本旋转并可移动到指定基准线", "Icons/align_line_32.png", "Icons/align_line_16.png")]
    public class AlignTextToLineCommand : IExternalCommand
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
            var state = new AlignTextToLineState();

            try
            {
                while (true)
                {
                    var window = new AlignTextToLineWindow(state);
                    new System.Windows.Interop.WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
                    bool? dialogResult = window.ShowDialog();

                    if (window.Action == TextToolDialogAction.SelectPrimary)
                    {
                        try
                        {
                            Reference reference = uiDoc.Selection.PickObject(
                                ObjectType.Element,
                                new TextNoteSelectionFilter(),
                                "请选择要对齐的文本");
                            state.SelectedText = doc.GetElement(reference) as TextNote;
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                        }
                        continue;
                    }

                    if (window.Action == TextToolDialogAction.SelectSecondary)
                    {
                        try
                        {
                            XYZ start = uiDoc.Selection.PickPoint("请选择基准线起点");
                            XYZ end = uiDoc.Selection.PickPoint("请选择基准线终点");
                            if (start.DistanceTo(end) < 1e-9)
                                TaskDialog.Show("提示", "基准线起点和终点不能重合。 ");
                            else
                            {
                                state.LineStart = start;
                                state.LineEnd = end;
                            }
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                        {
                        }
                        continue;
                    }

                    if (window.Action != TextToolDialogAction.Execute || dialogResult != true)
                        return Result.Cancelled;

                    return ExecuteAlignment(uiDoc, state, ref message);
                }
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("对齐文本到线", ex);
                TaskDialog.Show("对齐到线失败",
                    $"操作失败。\n\n错误信息：{ex.Message}\n\n诊断日志：{logPath}");
                message = ex.ToString();
                return Result.Failed;
            }
        }

        private static Result ExecuteAlignment(UIDocument uiDoc, AlignTextToLineState state, ref string message)
        {
            Document doc = uiDoc.Document;
            TextNote? targetText = state.SelectedText;
            if (targetText == null || !targetText.Document.Equals(doc) || doc.GetElement(targetText.Id) == null)
            {
                TaskDialog.Show("提示", "选择的文本已失效，请重新选择。 ");
                return Result.Cancelled;
            }
            if (state.LineStart == null || state.LineEnd == null)
                return Result.Cancelled;

            XYZ lineStart = state.LineStart;
            XYZ lineEnd = state.LineEnd;
            XYZ planarDirection = new XYZ(lineEnd.X - lineStart.X, lineEnd.Y - lineStart.Y, 0.0);
            if (planarDirection.GetLength() < 1e-9)
            {
                TaskDialog.Show("提示", "基准线在当前视图平面内没有有效方向。 ");
                return Result.Cancelled;
            }

            using (var transaction = new Transaction(doc, "对齐文本到线"))
            {
                try
                {
                    transaction.Start();
                    XYZ lineDirection = planarDirection.Normalize();
                    double lineAngle = Math.Atan2(lineDirection.Y, lineDirection.X);
                    if (state.KeepUpright)
                    {
                        if (lineAngle > Math.PI / 2.0) lineAngle -= Math.PI;
                        else if (lineAngle < -Math.PI / 2.0) lineAngle += Math.PI;
                    }

                    XYZ textCoord = targetText.Coord;
                    double textAngle = TextNoteHelper.GetTextNoteAngle(targetText);
                    double rotationAngle = NormalizeAngle(lineAngle - textAngle);
                    if (Math.Abs(rotationAngle) > 1e-9)
                    {
                        Line rotationAxis = Line.CreateBound(textCoord, textCoord + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(doc, targetText.Id, rotationAxis, rotationAngle);
                    }

                    XYZ? targetPosition = null;
                    if (string.Equals(state.AlignBaseTag, "Start", StringComparison.OrdinalIgnoreCase))
                        targetPosition = lineStart;
                    else if (string.Equals(state.AlignBaseTag, "End", StringComparison.OrdinalIgnoreCase))
                        targetPosition = lineEnd;

                    if (targetPosition != null)
                    {
                        XYZ offset = targetPosition - targetText.Coord;
                        if (offset.GetLength() > 1e-9)
                            ElementTransformUtils.MoveElement(doc, targetText.Id, offset);
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

            uiDoc.RefreshActiveView();
            TaskDialog.Show("完成", "文本已对齐到基准线。 ");
            return Result.Succeeded;
        }

        private static double NormalizeAngle(double angle)
        {
            while (angle > Math.PI) angle -= 2.0 * Math.PI;
            while (angle <= -Math.PI) angle += 2.0 * Math.PI;
            return angle;
        }
    }
}
