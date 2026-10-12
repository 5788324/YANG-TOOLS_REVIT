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
        "变长布筋",
        "在同一宿主表面创建沿面分布的 Shape Driven 钢筋集，并支持 Varying Length。",
        "Icons/rebar_surface_varying_32.png",
        "Icons/rebar_surface_varying_16.png",
        groupName: "钢筋测试",
        order: 105,
        groupLargeIcon: "Icons/rebar_test_32.png",
        groupSmallIcon: "Icons/rebar_test_16.png")]
    public sealed class RebarSurfaceVaryingCreateCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("变长布筋", "请先打开项目文档。");
                return Result.Cancelled;
            }

            try
            {
                var filter = new RebarPlanarFaceSelectionFilter(uidoc.Document);
                Reference coverRef = uidoc.Selection.PickObject(
                    ObjectType.Face,
                    filter,
                    "请选择钢筋所在的 Cover 平面。 ");
                Element? host = uidoc.Document.GetElement(coverRef.ElementId);
                if (host == null) return Result.Cancelled;

                Reference startRef = uidoc.Selection.PickObject(
                    ObjectType.Face,
                    filter,
                    "请选择钢筋起点边界 Cover 平面（必须与第一面属于同一宿主）。 ");
                Reference endRef = uidoc.Selection.PickObject(
                    ObjectType.Face,
                    filter,
                    "请选择钢筋终点边界 Cover 平面（必须与第一面属于同一宿主）。 ");

                XYZ seedStart = uidoc.Selection.PickPoint("在 Cover 面附近点选钢筋方向起点。 ");
                XYZ seedEnd = uidoc.Selection.PickPoint("在 Cover 面附近点选钢筋方向终点。 ");

                RebarBarType? barType = RebarShapeDrivenCreateService
                    .GetAvailableBarTypes(uidoc.Document)
                    .FirstOrDefault();
                if (barType == null)
                {
                    TaskDialog.Show("变长布筋", "当前项目中没有 RebarBarType。请先载入钢筋类型。 ");
                    return Result.Cancelled;
                }

                var choice = new TaskDialog("变长布筋")
                {
                    MainInstruction = "选择创建方式",
                    MainContent =
                        $"宿主：{host.Name} (Id {host.Id.GetIdValue()})\n" +
                        $"钢筋类型：{barType.Name}\n\n" +
                        "工具会把 Edge 约束到 Cover，并把 Start/End 约束到两个边界面；钢筋集分布方向位于 Cover 面内。",
                    CommonButtons = TaskDialogCommonButtons.Cancel
                };
                choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Dry Run", "只解析三面、Cover、中心线和沿面分布方向");
                choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "创建 Uniform 控制组", "5 根 @200mm，不启用 Varying");
                choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "创建 Varying", "5 根 @200mm，启用约束驱动 Varying");
                TaskDialogResult selected = choice.Show();
                if (selected == TaskDialogResult.Cancel)
                    return Result.Cancelled;

                bool dryRun = selected == TaskDialogResult.CommandLink1;
                bool varying = selected == TaskDialogResult.CommandLink3;
                var request = new SurfaceVaryingRebarCreateRequest
                {
                    HostId = host.Id.GetIdValue(),
                    CoverFaceStableReference = coverRef.ConvertToStableRepresentation(uidoc.Document),
                    StartBoundaryFaceStableReference = startRef.ConvertToStableRepresentation(uidoc.Document),
                    EndBoundaryFaceStableReference = endRef.ConvertToStableRepresentation(uidoc.Document),
                    BarTypeId = barType.Id.GetIdValue(),
                    SeedStartOnCoverFaceMm = ToMillimeterPoint(seedStart),
                    SeedEndOnCoverFaceMm = ToMillimeterPoint(seedEnd),
                    NumberOfBarPositions = 5,
                    SpacingMm = 200.0,
                    EnableVarying = varying,
                    DryRun = dryRun
                };

                SurfaceVaryingRebarCreateResult result = RebarSurfaceVaryingCreateService.Execute(uiapp, request);
                if (!result.Success)
                {
                    message = result.Error;
                    TaskDialog.Show("变长布筋", "执行失败：\n" + result.Error);
                    return Result.Failed;
                }

                string created = result.CreatedRebarId.HasValue
                    ? $"\n新钢筋 Id：{result.CreatedRebarId.Value}"
                    : string.Empty;
                TaskDialog.Show(
                    "变长布筋",
                    result.Message + created +
                    $"\nCover：{result.CoverDistanceMm:0.###} mm" +
                    $"\n初始单筋长度：{result.InitialBarLengthMm:0.###} mm" +
                    $"\nEdge/Start/End：{result.EdgeConstraintType}/{result.StartConstraintType}/{result.EndConstraintType}" +
                    $"\nVarying API：{result.VaryingApiMode}" +
                    $"\nHasVariableLengthBars：{result.HasVariableLengthBars}" +
                    $"\nChangedModel：{result.ChangedModel}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("变长布筋", ex);
                message = ex.Message;
                TaskDialog.Show("变长布筋", $"{ex.Message}\n\n诊断日志：{logPath}");
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
