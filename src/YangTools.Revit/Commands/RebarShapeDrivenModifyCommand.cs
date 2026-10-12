using System;
using System.Globalization;
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
        "排布修改",
        "原位修改 Shape Driven 钢筋排布，保持 ElementId、宿主和既有约束。",
        "Icons/rebar_shape_modify_32.png",
        "Icons/rebar_shape_modify_16.png",
        groupName: "钢筋测试",
        order: 200,
        groupLargeIcon: "Icons/rebar_test_32.png",
        groupSmallIcon: "Icons/rebar_test_16.png")]
    public sealed class RebarShapeDrivenModifyCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("排布修改", "请先打开项目文档。");
                return Result.Cancelled;
            }

            try
            {
                Reference picked = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RebarSelectionFilter(),
                    "请选择需要原位修改的 Shape Driven Rebar。");
                Autodesk.Revit.DB.Structure.Rebar? rebar = uidoc.Document.GetElement(picked) as Autodesk.Revit.DB.Structure.Rebar;
                if (rebar == null || rebar.IsRebarFreeForm())
                {
                    TaskDialog.Show("排布修改", "请选择 Shape Driven Rebar；Free Form 请使用“自由形状修改”。 ");
                    return Result.Cancelled;
                }

                RebarInspectionReport beforeReport = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
                RebarInspectionItem? before = beforeReport.Items.FirstOrDefault();
                if (before == null)
                {
                    TaskDialog.Show("排布修改", "无法读取当前钢筋。 ");
                    return Result.Failed;
                }

                int defaultCount = Math.Max(2, before.NumberOfBarPositions > 0 ? before.NumberOfBarPositions : 5);
                double defaultSpacing = before.MaxSpacingMm ?? 200.0;

                var countDialog = new SimpleInputDialog(
                    "排布修改",
                    "目标钢筋数量（NumberWithSpacing）：",
                    defaultCount.ToString(CultureInfo.InvariantCulture));
                if (countDialog.ShowDialog() != true) return Result.Cancelled;
                if (!int.TryParse(countDialog.InputText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                {
                    TaskDialog.Show("排布修改", "钢筋数量必须是整数。 ");
                    return Result.Cancelled;
                }

                var spacingDialog = new SimpleInputDialog(
                    "排布修改",
                    "目标间距 mm：",
                    defaultSpacing.ToString("0.###", CultureInfo.InvariantCulture));
                if (spacingDialog.ShowDialog() != true) return Result.Cancelled;
                if (!double.TryParse(spacingDialog.InputText, NumberStyles.Float, CultureInfo.InvariantCulture, out double spacing))
                {
                    TaskDialog.Show("排布修改", "间距必须是有效数字。 ");
                    return Result.Cancelled;
                }

                var choice = new TaskDialog("排布修改")
                {
                    MainInstruction = $"目标：{count} 根 @ {spacing:0.###} mm",
                    MainContent =
                        $"Rebar Id：{rebar.Id.GetIdValue()}\n" +
                        $"当前：{before.LayoutRule}, {before.NumberOfBarPositions} 根, {before.MaxSpacingMm:0.###} mm\n\n" +
                        "修改后要求 ElementId、UniqueId、Host 和既有 Constraint 目标保持不变。",
                    CommonButtons = TaskDialogCommonButtons.Cancel
                };
                choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Dry Run", "只验证修改计划，不启动 Transaction");
                choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "应用修改", "原位更新 NumberWithSpacing，并在提交前验证约束不变量");
                TaskDialogResult selected = choice.Show();
                if (selected == TaskDialogResult.Cancel) return Result.Cancelled;

                var request = new ShapeDrivenRebarModifyRequest
                {
                    RebarId = rebar.Id.GetIdValue(),
                    ExpectedUniqueId = rebar.UniqueId,
                    NumberOfBarPositions = count,
                    SpacingMm = spacing,
                    BarsOnNormalSide = before.BarsOnNormalSide ?? true,
                    IncludeFirstBar = true,
                    IncludeLastBar = true,
                    RequireConstraintPreservation = true,
                    DryRun = selected == TaskDialogResult.CommandLink1
                };

                ShapeDrivenRebarModifyResult result = RebarShapeDrivenModifyService.Execute(uiapp, request);
                if (!result.Success)
                {
                    message = result.Error;
                    TaskDialog.Show("排布修改", "执行失败：\n" + result.Error);
                    return Result.Failed;
                }

                TaskDialog.Show(
                    "排布修改",
                    result.Message +
                    $"\nElementId保持：{result.SameElementId}" +
                    $"\nHost保持：{result.SameHost}" +
                    $"\nConstraint保持：{result.ConstraintsPreserved}" +
                    $"\n布局：{result.BeforeNumberOfBarPositions}@{result.BeforeSpacingMm:0.###} → {result.AfterNumberOfBarPositions}@{result.AfterSpacingMm:0.###}" +
                    $"\nChangedModel：{result.ChangedModel}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("排布修改", ex);
                message = ex.Message;
                TaskDialog.Show("排布修改", $"{ex.Message}\n\n诊断日志：{logPath}");
                return Result.Failed;
            }
        }
    }
}
