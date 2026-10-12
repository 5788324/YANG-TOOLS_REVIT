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
        "按面布筋",
        "选择宿主平面，按保护层创建 Shape Driven 钢筋，并建立 ToCover 约束。",
        "Icons/rebar_face_32.png",
        "Icons/rebar_face_16.png",
        groupName: "钢筋测试",
        order: 100,
        groupLargeIcon: "Icons/rebar_test_32.png",
        groupSmallIcon: "Icons/rebar_test_16.png")]
    public sealed class RebarFaceDrivenCreateCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("按面布筋", "请先打开项目文档。");
                return Result.Cancelled;
            }

            try
            {
                Reference faceReference = uidoc.Selection.PickObject(
                    ObjectType.Face,
                    new RebarPlanarFaceSelectionFilter(uidoc.Document),
                    "请选择有效 Rebar Host 的 exposed 平面。 ");
                Element? host = uidoc.Document.GetElement(faceReference.ElementId);
                if (host == null) return Result.Cancelled;

                XYZ start = uidoc.Selection.PickPoint("请选择钢筋起点附近位置；工具会投影到所选宿主面。 ");
                XYZ end = uidoc.Selection.PickPoint("请选择钢筋终点附近位置；工具会投影到所选宿主面。 ");

                RebarBarType? barType = RebarShapeDrivenCreateService
                    .GetAvailableBarTypes(uidoc.Document)
                    .FirstOrDefault();
                if (barType == null)
                {
                    TaskDialog.Show("按面布筋", "当前项目中没有 RebarBarType。请先载入钢筋类型。 ");
                    return Result.Cancelled;
                }

                string stableReference = faceReference.ConvertToStableRepresentation(uidoc.Document);
                var choice = new TaskDialog("按面布筋")
                {
                    MainInstruction = "选择创建方式",
                    MainContent =
                        $"宿主：{host.Name} (Id {host.Id.GetIdValue()})\n" +
                        $"钢筋类型：{barType.Name}\n\n" +
                        "仅处理 exposed PlanarFace，并验证 RebarPlane → ToCover 首选约束。",
                    CommonButtons = TaskDialogCommonButtons.Cancel
                };
                choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Dry Run", "解析面、Cover、钢筋半径与中心线，不修改模型");
                choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "创建单根", "一个 Transaction；创建后校验 ToCover 约束");
                choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "创建 3 根 @200mm", "NumberWithSpacing；仍使用同一 Face/Cover 逻辑");
                TaskDialogResult selected = choice.Show();
                if (selected == TaskDialogResult.Cancel)
                    return Result.Cancelled;

                bool dryRun = selected == TaskDialogResult.CommandLink1;
                bool set = selected == TaskDialogResult.CommandLink3;
                var request = new FaceDrivenRebarCreateRequest
                {
                    HostId = host.Id.GetIdValue(),
                    FaceStableReference = stableReference,
                    BarTypeId = barType.Id.GetIdValue(),
                    StartOnFaceMm = ToMillimeterPoint(start),
                    EndOnFaceMm = ToMillimeterPoint(end),
                    LayoutMode = set ? "NumberWithSpacing" : "Single",
                    NumberOfBarPositions = set ? 3 : 1,
                    SpacingMm = 200.0,
                    DryRun = dryRun
                };

                FaceDrivenRebarCreateResult result = RebarFaceDrivenCreateService.Execute(uiapp, request);
                if (!result.Success)
                {
                    message = result.Error;
                    TaskDialog.Show("按面布筋", "执行失败：\n" + result.Error);
                    return Result.Failed;
                }

                string created = result.CreatedRebarId.HasValue
                    ? $"\n新钢筋 Id：{result.CreatedRebarId.Value}"
                    : string.Empty;
                TaskDialog.Show(
                    "按面布筋",
                    result.Message + created +
                    $"\nCover：{result.CoverDistanceMm:0.###} mm" +
                    $"\n预期中心线距面：{result.ExpectedCenterlineOffsetMm:0.###} mm" +
                    $"\n实际中心线距面：{result.ActualCenterlineOffsetMm:0.###} mm" +
                    $"\nPreferred Constraint：{result.PreferredConstraintType}" +
                    $"\nChangedModel：{result.ChangedModel}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("按面布筋", ex);
                message = ex.Message;
                TaskDialog.Show("按面布筋", $"{ex.Message}\n\n诊断日志：{logPath}");
                return Result.Failed;
            }
        }

        private static RebarPointMm ToMillimeterPoint(XYZ value)
        {
            return new RebarPointMm
            {
                X = UnitUtils.ConvertFromInternalUnits(value.X, UnitTypeId.Millimeters),
                Y = UnitUtils.ConvertFromInternalUnits(value.Y, UnitTypeId.Millimeters),
                Z = UnitUtils.ConvertFromInternalUnits(value.Z, UnitTypeId.Millimeters)
            };
        }
    }
}
