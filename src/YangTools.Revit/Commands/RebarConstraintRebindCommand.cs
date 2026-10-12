using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.Core.Rebar;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        "钢筋工具区",
        "约束修改",
        "将既有 Shape Driven Rebar 的 Edge 约束原位重绑到同一宿主的新 exposed PlanarFace；Start/End 精确重绑暂不在 GUI 开放。",
        "Icons/rebar_constraint_32.png",
        "Icons/rebar_constraint_16.png",
        groupName: "钢筋测试",
        order: 210,
        groupLargeIcon: "Icons/rebar_test_32.png",
        groupSmallIcon: "Icons/rebar_test_16.png")]
    public sealed class RebarConstraintRebindCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("约束修改", "请先打开项目文档。");
                return Result.Cancelled;
            }

            try
            {
                Reference pickedRebar = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RebarSelectionFilter(),
                    "请选择需要重绑约束的 Shape Driven Rebar。");
                Autodesk.Revit.DB.Structure.Rebar? rebar =
                    uidoc.Document.GetElement(pickedRebar) as Autodesk.Revit.DB.Structure.Rebar;
                if (rebar == null || rebar.IsRebarFreeForm())
                {
                    TaskDialog.Show("约束修改", "请选择 Shape Driven Rebar；Free Form 不支持此操作。");
                    return Result.Cancelled;
                }

                RebarInspectionItem? before = RebarInspectionService
                    .Inspect(uiapp, new[] { rebar.Id })
                    .Items.FirstOrDefault();
                if (before == null || before.HostId <= 0)
                {
                    TaskDialog.Show("约束修改", "无法读取当前钢筋宿主。");
                    return Result.Failed;
                }

                // R05 当前只有 Edge exact face rebind 已完成 Runtime 闭环。
                // Start/End 的精确面重绑仍保留在 Service/MCP 研究能力中，但不在正式 GUI 暴露，
                // 避免用户把尚未闭环的入口误认为可生产使用。
                Reference targetFace = uidoc.Selection.PickObject(
                    ObjectType.Face,
                    new RebarPlanarFaceSelectionFilter(uidoc.Document, before.HostId),
                    "请选择同一 Rebar Host 上新的 Cover 平面（当前 GUI 仅修改 Edge 约束）。");
                string stable = targetFace.ConvertToStableRepresentation(uidoc.Document);

                var request = new ShapeDrivenConstraintRebindRequest
                {
                    RebarId = rebar.Id.GetIdValue(),
                    ExpectedUniqueId = rebar.UniqueId,
                    EdgeFaceStableReference = stable,
                    DryRun = true
                };

                var action = new TaskDialog("约束修改")
                {
                    MainInstruction = "确认 Edge 约束修改",
                    MainContent =
                        $"Rebar Id：{rebar.Id.GetIdValue()}\nHost Id：{before.HostId}\n\n" +
                        "当前 GUI 只修改 Edge → ToCover；目标必须属于同一宿主，且必须存在 ToCover candidate。\n" +
                        "ElementId / UniqueId / Host / Varying / 未重绑约束必须保持。",
                    CommonButtons = TaskDialogCommonButtons.Cancel
                };
                action.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Dry Run", "只验证 target face 和 ToCover candidate");
                action.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "应用重绑", "原位切换 preferred constraint，并进行约束读回校验");
                TaskDialogResult actionChoice = action.Show();
                if (actionChoice == TaskDialogResult.Cancel) return Result.Cancelled;
                request.DryRun = actionChoice == TaskDialogResult.CommandLink1;

                ShapeDrivenConstraintRebindResult result =
                    RebarConstraintRebindService.Execute(uiapp, request);
                if (!result.Success)
                {
                    message = result.Error;
                    TaskDialog.Show("约束修改", "执行失败：\n" + result.Error);
                    return Result.Failed;
                }

                TaskDialog.Show(
                    "约束修改",
                    result.Message +
                    $"\nElementId保持：{result.SameElementId}" +
                    $"\nHost保持：{result.SameHost}" +
                    $"\nVarying保持：{result.VaryingModePreserved}" +
                    $"\n未重绑约束保持：{result.UnretargetedConstraintsPreserved}" +
                    $"\nChangedModel：{result.ChangedModel}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("约束修改", ex);
                message = ex.Message;
                TaskDialog.Show("约束修改", $"{ex.Message}\n\n诊断日志：{logPath}");
                return Result.Failed;
            }
        }
    }
}
