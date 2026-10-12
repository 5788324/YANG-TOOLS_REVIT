using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.Core.Rebar;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        "钢筋工具区",
        "钢筋约束",
        "使用 Revit 原生 ToOtherRebar candidate，在两根 Shape Driven Rebar 之间建立可读回的约束关系。",
        "Icons/rebar_relationship_32.png",
        "Icons/rebar_relationship_16.png",
        groupName: "钢筋测试",
        order: 310,
        groupLargeIcon: "Icons/rebar_test_32.png",
        groupSmallIcon: "Icons/rebar_test_16.png")]
    public sealed class RebarRelationshipCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            try
            {
                Reference sourceRef = uidoc.Selection.PickObject(ObjectType.Element, new RebarSelectionFilter(), "选择 Source Shape Driven Rebar");
                Reference targetRef = uidoc.Selection.PickObject(ObjectType.Element, new RebarSelectionFilter(), "选择 Target Shape Driven Rebar（例如箍筋）");
                var source = uidoc.Document.GetElement(sourceRef) as Autodesk.Revit.DB.Structure.Rebar;
                var target = uidoc.Document.GetElement(targetRef) as Autodesk.Revit.DB.Structure.Rebar;
                if (source == null || target == null || source.IsRebarFreeForm() || target.IsRebarFreeForm())
                {
                    TaskDialog.Show("钢筋约束", "当前只支持两根 Shape Driven Rebar。");
                    return Result.Cancelled;
                }
                if (source.Id == target.Id)
                {
                    TaskDialog.Show("钢筋约束", "Source 和 Target 不能是同一根钢筋。");
                    return Result.Cancelled;
                }

                var handleDialog = new TaskDialog("钢筋约束") { MainInstruction = "选择 Source Handle", CommonButtons = TaskDialogCommonButtons.Cancel };
                handleDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Edge");
                handleDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "StartOfBar");
                handleDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "EndOfBar");
                TaskDialogResult handleChoice = handleDialog.Show();
                if (handleChoice == TaskDialogResult.Cancel) return Result.Cancelled;
                string handleType = handleChoice == TaskDialogResult.CommandLink2 ? "StartOfBar" : handleChoice == TaskDialogResult.CommandLink3 ? "EndOfBar" : "Edge";

                var ordinalDialog = new SimpleInputDialog("钢筋约束", "SourceHandleOrdinal（从 0 开始）。普通直筋 Edge 通常填 0；多段筋按 dryRun 结果调整。", "0");
                if (ordinalDialog.ShowDialog() != true) return Result.Cancelled;
                if (!int.TryParse(ordinalDialog.InputText.Trim(), out int handleOrdinal) || handleOrdinal < 0)
                {
                    TaskDialog.Show("钢筋约束", "SourceHandleOrdinal 必须是 >=0 的整数。");
                    return Result.Cancelled;
                }

                var kindDialog = new TaskDialog("钢筋约束") { MainInstruction = "Target Rebar 几何类型", CommonButtons = TaskDialogCommonButtons.Cancel };
                kindDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Any", "先查看 Revit 提供的全部 ToOtherRebar candidate");
                kindDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Edge");
                kindDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "BarBend");
                kindDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "HookBend");
                TaskDialogResult kindChoice = kindDialog.Show();
                if (kindChoice == TaskDialogResult.Cancel) return Result.Cancelled;
                string targetKind = kindChoice == TaskDialogResult.CommandLink2 ? "Edge" : kindChoice == TaskDialogResult.CommandLink3 ? "BarBend" : kindChoice == TaskDialogResult.CommandLink4 ? "HookBend" : "Any";

                int? targetIndex = null;
                if (!string.Equals(targetKind, "Any", StringComparison.OrdinalIgnoreCase))
                {
                    var indexDialog = new SimpleInputDialog("钢筋约束", "TargetIndex（Edge/Bend 编号，HookBend 为 0/1；留空表示不过滤）", "");
                    if (indexDialog.ShowDialog() != true) return Result.Cancelled;
                    string text = indexDialog.InputText.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (!int.TryParse(text, out int parsed) || parsed < 0) return Result.Cancelled;
                        targetIndex = parsed;
                    }
                }

                var action = new TaskDialog("钢筋约束") { MainInstruction = "执行方式", MainContent = "默认建议先 Dry Run，查看 candidate 数量与 target 类型。", CommonButtons = TaskDialogCommonButtons.Cancel };
                action.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Dry Run");
                action.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "应用 ToOtherRebar");
                TaskDialogResult actionChoice = action.Show();
                if (actionChoice == TaskDialogResult.Cancel) return Result.Cancelled;

                var request = new RebarToRebarConstraintRequest
                {
                    SourceRebarId = source.Id.GetIdValue(),
                    ExpectedSourceUniqueId = source.UniqueId,
                    TargetRebarId = target.Id.GetIdValue(),
                    ExpectedTargetUniqueId = target.UniqueId,
                    SourceHandleType = handleType,
                    SourceHandleOrdinal = handleOrdinal,
                    TargetKind = targetKind,
                    TargetIndex = targetIndex,
                    DryRun = actionChoice == TaskDialogResult.CommandLink1
                };
                RebarToRebarConstraintResult result = RebarRelationshipService.ConstrainToOtherRebar(uiapp, request);
                if (!result.Success)
                {
                    message = result.Error;
                    TaskDialog.Show("钢筋约束", "执行失败：\n" + result.Error);
                    return Result.Failed;
                }
                TaskDialog.Show("钢筋约束", result.Message + $"\nCandidateCount：{result.CandidateCount}\nChangedModel：{result.ChangedModel}\nExactTargetReadback：{result.ExactTargetRebarReadback}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("钢筋约束", ex);
                message = ex.Message;
                TaskDialog.Show("钢筋约束", ex.Message + "\n\n诊断日志：" + logPath);
                return Result.Failed;
            }
        }
    }
}
