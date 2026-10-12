using System;
using System.Linq;
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
        "弯钩",
        "原位设置或移除 Shape Driven Rebar 的 Start/End Hook Type；无效组合由 Revit 回滚。",
        "Icons/rebar_hook_32.png",
        "Icons/rebar_hook_16.png",
        groupName: "钢筋测试",
        order: 300,
        groupLargeIcon: "Icons/rebar_test_32.png",
        groupSmallIcon: "Icons/rebar_test_16.png")]
    public sealed class RebarHookModifyCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            try
            {
                Reference picked = uidoc.Selection.PickObject(ObjectType.Element, new RebarSelectionFilter(), "选择需要修改 Hook 的 Shape Driven Rebar");
                var rebar = uidoc.Document.GetElement(picked) as Autodesk.Revit.DB.Structure.Rebar;
                if (rebar == null || rebar.IsRebarFreeForm()) return Result.Cancelled;

                var hookTypes = new FilteredElementCollector(uidoc.Document)
                    .OfClass(typeof(RebarHookType)).Cast<RebarHookType>().OrderBy(x => x.Name).Take(20).ToList();
                string choices = hookTypes.Count == 0
                    ? "当前文档没有 RebarHookType。"
                    : string.Join("\n", hookTypes.Select(x => $"{x.Id.GetIdValue()}  {x.Name}  {Math.Round(x.HookAngle * 180.0 / Math.PI, 1)}°"));

                var endDialog = new TaskDialog("弯钩") { MainInstruction = "修改哪一端？", MainContent = choices, CommonButtons = TaskDialogCommonButtons.Cancel };
                endDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Start Hook");
                endDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "End Hook");
                TaskDialogResult endChoice = endDialog.Show();
                if (endChoice == TaskDialogResult.Cancel) return Result.Cancelled;

                var idDialog = new SimpleInputDialog("弯钩", "输入 RebarHookType ElementId；输入 -1 移除 Hook。", hookTypes.FirstOrDefault()?.Id.GetIdValue().ToString() ?? "-1");
                if (idDialog.ShowDialog() != true) return Result.Cancelled;
                if (!long.TryParse(idDialog.InputText.Trim(), out long hookTypeId)) return Result.Cancelled;

                var action = new TaskDialog("弯钩") { MainInstruction = "执行方式", CommonButtons = TaskDialogCommonButtons.Cancel };
                action.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Dry Run");
                action.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "应用 Hook 修改");
                TaskDialogResult actionChoice = action.Show();
                if (actionChoice == TaskDialogResult.Cancel) return Result.Cancelled;

                var request = new RebarHookModifyRequest
                {
                    RebarId = rebar.Id.GetIdValue(),
                    ExpectedUniqueId = rebar.UniqueId,
                    StartHookTypeId = endChoice == TaskDialogResult.CommandLink1 ? hookTypeId : (long?)null,
                    EndHookTypeId = endChoice == TaskDialogResult.CommandLink2 ? hookTypeId : (long?)null,
                    DryRun = actionChoice == TaskDialogResult.CommandLink1
                };
                RebarHookModifyResult result = RebarHookModifyService.Execute(uiapp, request);
                if (!result.Success)
                {
                    message = result.Error;
                    TaskDialog.Show("弯钩", "执行失败：\n" + result.Error);
                    return Result.Failed;
                }
                TaskDialog.Show("弯钩", result.Message + $"\nStartHookId：{result.AfterStartHookTypeId}\nEndHookId：{result.AfterEndHookTypeId}\nChangedModel：{result.ChangedModel}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("弯钩", ex);
                message = ex.Message;
                TaskDialog.Show("弯钩", ex.Message + "\n\n诊断日志：" + logPath);
                return Result.Failed;
            }
        }
    }
}
