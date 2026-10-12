using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
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
        "机械连接",
        "使用 Revit 原生 RebarCoupler 创建两筋机械连接或单端封帽；支持 Dry Run / Apply / Undo。",
        "Icons/rebar_coupler_32.png",
        "Icons/rebar_coupler_16.png",
        groupName: "钢筋测试",
        order: 320,
        groupLargeIcon: "Icons/rebar_test_32.png",
        groupSmallIcon: "Icons/rebar_test_16.png")]
    public sealed class RebarCouplerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            try
            {
                Document doc = uidoc.Document;
                Reference firstRef = uidoc.Selection.PickObject(ObjectType.Element, new RebarSelectionFilter(), "选择第一根 Rebar");
                if (doc.GetElement(firstRef.ElementId) is not Autodesk.Revit.DB.Structure.Rebar first) return Result.Cancelled;

                var modeDialog = new TaskDialog("机械连接")
                {
                    MainInstruction = "选择 Coupler 模式",
                    CommonButtons = TaskDialogCommonButtons.Cancel
                };
                modeDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Two-Bar Coupler（连接两根钢筋）");
                modeDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Single-End Cap（单根钢筋端部封帽）");
                TaskDialogResult modeChoice = modeDialog.Show();
                if (modeChoice == TaskDialogResult.Cancel) return Result.Cancelled;

                Autodesk.Revit.DB.Structure.Rebar? second = null;
                if (modeChoice == TaskDialogResult.CommandLink1)
                {
                    Reference secondRef = uidoc.Selection.PickObject(ObjectType.Element, new RebarSelectionFilter(), "选择第二根 Rebar");
                    second = doc.GetElement(secondRef.ElementId) as Autodesk.Revit.DB.Structure.Rebar;
                    if (second == null || second.Id.GetIdValue() == first.Id.GetIdValue())
                    {
                        TaskDialog.Show("机械连接", "第二根必须是另一根 Rebar。 ");
                        return Result.Cancelled;
                    }
                }

                int? firstEnd = AskEnd("第一根 Rebar");
                if (!firstEnd.HasValue) return Result.Cancelled;
                int secondEnd = 0;
                if (second != null)
                {
                    int? selected = AskEnd("第二根 Rebar");
                    if (!selected.HasValue) return Result.Cancelled;
                    secondEnd = selected.Value;
                }

                var types = RebarCouplerService.GetAvailableTypes(doc).ToList();
                if (types.Count == 0)
                {
                    TaskDialog.Show("机械连接", "当前项目没有 Rebar Coupler Type。请先加载 Coupler Family。 ");
                    return Result.Cancelled;
                }
                string choices = string.Join("\n", types.Take(20).Select(x =>
                    $"{x.ElementId}  {x.FamilyName} : {x.TypeName}" + (x.IsDefault ? "  [Default]" : string.Empty)));
                long suggestedId = types.FirstOrDefault(x => x.IsDefault)?.ElementId ?? types[0].ElementId;
                var typeDialog = new SimpleInputDialog(
                    "机械连接",
                    "输入 Coupler Type ElementId；当前可用类型（最多显示 20 个）：\n\n" + choices,
                    suggestedId.ToString());
                if (typeDialog.ShowDialog() != true) return Result.Cancelled;
                if (!long.TryParse(typeDialog.InputText.Trim(), out long typeId) || typeId <= 0) return Result.Cancelled;

                var action = new TaskDialog("机械连接")
                {
                    MainInstruction = "执行方式",
                    MainContent = "Dry Run 不启动 Transaction；Apply 才调用 Revit 原生 RebarCoupler.Create。",
                    CommonButtons = TaskDialogCommonButtons.Cancel
                };
                action.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Dry Run（只预检）");
                action.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Apply（创建 Coupler）");
                TaskDialogResult actionChoice = action.Show();
                if (actionChoice == TaskDialogResult.Cancel) return Result.Cancelled;

                var request = new RebarCouplerRequest
                {
                    CouplerTypeId = typeId,
                    FirstRebarId = first.Id.GetIdValue(),
                    FirstEnd = firstEnd.Value,
                    ExpectedFirstUniqueId = first.UniqueId ?? string.Empty,
                    SecondRebarId = second?.Id.GetIdValue(),
                    SecondEnd = secondEnd,
                    ExpectedSecondUniqueId = second?.UniqueId ?? string.Empty,
                    DryRun = actionChoice == TaskDialogResult.CommandLink1
                };
                RebarCouplerResult result = RebarCouplerService.Execute(uiapp, request);
                if (!result.Success)
                {
                    message = result.Error;
                    TaskDialog.Show("机械连接", "执行失败：\n" + result.Error +
                        (string.IsNullOrWhiteSpace(result.NativeError) ? string.Empty : "\nNative：" + result.NativeError));
                    return Result.Failed;
                }

                TaskDialog.Show(
                    "机械连接",
                    result.Message +
                    $"\nMode：{result.Mode}" +
                    $"\nCouplerType：{result.CouplerTypeId}" +
                    $"\nCouplerId：{result.CouplerId?.ToString() ?? "-"}" +
                    $"\nNativeValidation：{result.NativeValidationPerformed}" +
                    $"\nNativeError：{(string.IsNullOrWhiteSpace(result.NativeError) ? "-" : result.NativeError)}" +
                    $"\nChangedModel：{result.ChangedModel}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("机械连接", ex);
                message = ex.Message;
                TaskDialog.Show("机械连接", ex.Message + "\n\n诊断日志：" + logPath);
                return Result.Failed;
            }
        }

        private static int? AskEnd(string label)
        {
            var dialog = new TaskDialog("机械连接")
            {
                MainInstruction = label + "：选择端点",
                MainContent = "Rebar API 端点编号为 0 / 1。",
                CommonButtons = TaskDialogCommonButtons.Cancel
            };
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "End 0");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "End 1");
            TaskDialogResult choice = dialog.Show();
            if (choice == TaskDialogResult.CommandLink1) return 0;
            if (choice == TaskDialogResult.CommandLink2) return 1;
            return null;
        }
    }
}
