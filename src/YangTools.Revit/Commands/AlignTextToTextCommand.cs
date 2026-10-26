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
        panelName: "文本工具区", buttonText: "基准对齐",
        tooltip: "按参考文字对齐目标文字的位置、角度或宽度。",
        largeIcon: "Icons/align_text_32.png", smallIcon: "Icons/align_text_16.png",
        usage: "使用：点击按钮，按提示拾取参考 TextNote 和待调整 TextNote，并在设置窗口指定对齐项目。\n注意：可选择 X/Y、角度或文本框宽度，执行前确认参考和目标。")]
    public class AlignTextToTextCommand : IExternalCommand
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
            var state = new AlignTextToTextState();

            try
            {
                while (true)
                {
                    var window = new AlignTextToTextWindow(state);
                    new System.Windows.Interop.WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
                    bool? dialogResult = window.ShowDialog();

                    if (window.Action == TextToolDialogAction.SelectPrimary)
                    {
                        try
                        {
                            Reference reference = uiDoc.Selection.PickObject(
                                ObjectType.Element,
                                new TextNoteSelectionFilter(),
                                "请选择源文本（对齐基准）");
                            TextNote? selected = doc.GetElement(reference) as TextNote;
                            if (selected != null)
                            {
                                state.SourceText = selected;
                                state.TargetTexts.RemoveAll(note => note.Id == selected.Id);
                            }
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
                            var references = uiDoc.Selection.PickObjects(
                                ObjectType.Element,
                                new TextNoteSelectionFilter(),
                                "请选择要对齐的目标文本（可多选）");
                            ElementId sourceId = state.SourceText?.Id ?? ElementId.InvalidElementId;
                            state.TargetTexts = references
                                .Select(reference => doc.GetElement(reference) as TextNote)
                                .Where(note => note != null && note.Id != sourceId)
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

                    return ExecuteAlignment(uiDoc, state, ref message);
                }
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("文本对齐到文本", ex);
                TaskDialog.Show("对齐文本失败",
                    $"操作失败。\n\n错误信息：{ex.Message}\n\n诊断日志：{logPath}");
                message = ex.ToString();
                return Result.Failed;
            }
        }

        private static Result ExecuteAlignment(UIDocument uiDoc, AlignTextToTextState state, ref string message)
        {
            Document doc = uiDoc.Document;
            TextNote? sourceText = state.SourceText;
            if (sourceText == null || !sourceText.Document.Equals(doc) || doc.GetElement(sourceText.Id) == null)
            {
                TaskDialog.Show("提示", "源文本已失效，请重新选择。 ");
                return Result.Cancelled;
            }

            var targets = state.TargetTexts
                .Where(note => note != null && note.Id != sourceText.Id && note.Document.Equals(doc) && doc.GetElement(note.Id) != null)
                .ToList();
            if (targets.Count == 0)
            {
                TaskDialog.Show("提示", "目标文本已失效，请重新选择。 ");
                return Result.Cancelled;
            }

            using (var transaction = new Transaction(doc, "对齐文本到文本"))
            {
                try
                {
                    transaction.Start();
                    XYZ sourceCoord = sourceText.Coord;
                    double sourceAngle = TextNoteHelper.GetTextNoteAngle(sourceText);
                    double sourceWidth = sourceText.Width;

                    int succeeded = 0;
                    int skipped = 0;
                    state.LastSkipDetails.Clear();
                    foreach (TextNote target in targets)
                    {
                        using (var subTransaction = new SubTransaction(doc))
                        {
                            subTransaction.Start();
                            try
                            {
                                if (state.AlignRotation)
                                {
                                    double targetAngle = TextNoteHelper.GetTextNoteAngle(target);
                                    double angleDiff = NormalizeAngle(sourceAngle - targetAngle);
                                    if (Math.Abs(angleDiff) > 1e-9)
                                    {
                                        Line rotationAxis = Line.CreateBound(target.Coord, target.Coord + XYZ.BasisZ);
                                        ElementTransformUtils.RotateElement(doc, target.Id, rotationAxis, angleDiff);
                                    }
                                }

                                if (state.AlignX || state.AlignY)
                                {
                                    XYZ targetCoord = target.Coord;
                                    XYZ offset = new XYZ(
                                        state.AlignX ? sourceCoord.X - targetCoord.X : 0.0,
                                        state.AlignY ? sourceCoord.Y - targetCoord.Y : 0.0,
                                        0.0);
                                    if (offset.GetLength() > 1e-9)
                                        ElementTransformUtils.MoveElement(doc, target.Id, offset);
                                }

                                if (state.MatchParagraphWidth)
                                    SetParagraphWidthExact(doc, target, sourceWidth);

                                if (subTransaction.Commit() == TransactionStatus.Committed)
                                    succeeded++;
                                else
                                {
                                    TransactionStatus subStatus = subTransaction.GetStatus();
                                    if (subStatus == TransactionStatus.Started)
                                        subTransaction.RollBack();
                                    skipped++;
                                    RecordSkipDetail(state, target.Id, $"子事务提交状态：{subStatus}");
                                }
                            }
                            catch (Exception ex)
                            {
                                if (subTransaction.GetStatus() == TransactionStatus.Started)
                                    subTransaction.RollBack();
                                skipped++;
                                RecordSkipDetail(state, target.Id, ex.Message);
                            }
                        }
                    }

                    if (succeeded == 0)
                    {
                        string details = state.LastSkipDetails.Count > 0
                            ? "\n\n部分原因：\n" + string.Join("\n", state.LastSkipDetails)
                            : string.Empty;
                        throw new InvalidOperationException(
                            "所有目标文本均无法按所选项目处理，可能已锁定、成组、受约束，或源宽度超出目标文字类型允许范围。" + details);
                    }

                    TransactionStatus status = transaction.Commit();
                    state.LastSucceededCount = succeeded;
                    state.LastSkippedCount = skipped;
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
            string resultText = $"已对齐 {state.LastSucceededCount} 个目标文本。";
            if (state.LastSkippedCount > 0)
            {
                resultText += $"\n跳过 {state.LastSkippedCount} 个无法移动、旋转或设置段落宽度的目标。";
                if (state.LastSkipDetails.Count > 0)
                    resultText += "\n\n部分原因：\n" + string.Join("\n", state.LastSkipDetails);
            }
            TaskDialog.Show("完成", resultText);
            return Result.Succeeded;
        }


        private static void RecordSkipDetail(AlignTextToTextState state, ElementId targetId, string reason)
        {
            if (state.LastSkipDetails.Count >= 8)
                return;

            string safeReason = string.IsNullOrWhiteSpace(reason) ? "未知原因" : reason.Trim();
            state.LastSkipDetails.Add($"- 文本 ID {targetId}：{safeReason}");
        }

        private static void SetParagraphWidthExact(Document doc, TextNote target, double sourceWidth)
        {
            const double tolerance = 1e-9;
            ElementId targetTypeId = target.GetTypeId();
            double minWidth = TextNote.GetMinimumAllowedWidth(doc, targetTypeId);
            double maxWidth = TextNote.GetMaximumAllowedWidth(doc, targetTypeId);

            if (sourceWidth < minWidth - tolerance || sourceWidth > maxWidth + tolerance)
            {
                throw new InvalidOperationException(
                    $"源文本宽度 {sourceWidth:G17} ft 超出目标文本类型允许范围 [{minWidth:G17}, {maxWidth:G17}] ft。");
            }

            double assignableWidth = Math.Max(minWidth, Math.Min(maxWidth, sourceWidth));
            target.Width = assignableWidth;
            if (Math.Abs(target.Width - sourceWidth) > tolerance)
            {
                throw new InvalidOperationException(
                    $"目标文本宽度设置后与源文本不一致：源={sourceWidth:G17} ft，目标={target.Width:G17} ft。");
            }
        }

        private static double NormalizeAngle(double angle)
        {
            while (angle > Math.PI) angle -= 2.0 * Math.PI;
            while (angle <= -Math.PI) angle += 2.0 * Math.PI;
            return angle;
        }
    }
}
