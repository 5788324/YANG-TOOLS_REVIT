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
        "中心线布筋",
        "基于宿主与起终点创建 Shape Driven 直筋；支持 Dry Run 与基础排布。",
        "Icons/rebar_shape_create_32.png",
        "Icons/rebar_shape_create_16.png",
        groupName: "钢筋测试",
        order: 110,
        groupLargeIcon: "Icons/rebar_test_32.png",
        groupSmallIcon: "Icons/rebar_test_16.png")]
    public sealed class RebarShapeDrivenCreateCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("中心线布筋", "请先打开项目文档。");
                return Result.Cancelled;
            }

            try
            {
                Reference hostReference = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RebarHostSelectionFilter(),
                    "请选择一个可承载钢筋的混凝土宿主。");
                Element? host = uidoc.Document.GetElement(hostReference.ElementId);
                if (host == null)
                    return Result.Cancelled;

                XYZ start = uidoc.Selection.PickPoint("请选择直筋中心线起点。");
                XYZ end = uidoc.Selection.PickPoint("请选择直筋中心线终点。");
                XYZ normal = ResolveNormal(uidoc.ActiveView, start, end);

                RebarBarType? barType = RebarShapeDrivenCreateService
                    .GetAvailableBarTypes(uidoc.Document)
                    .FirstOrDefault();
                if (barType == null)
                {
                    TaskDialog.Show("中心线布筋", "当前项目中没有 RebarBarType。请先载入钢筋类型。");
                    return Result.Cancelled;
                }

                var choice = new TaskDialog("中心线布筋")
                {
                    MainInstruction = "选择创建方式",
                    MainContent =
                        $"宿主：{host.Name} (Id {host.Id.GetIdValue()})\n" +
                        $"钢筋类型：{barType.Name}\n\n" +
                        "当前按拾取的中心线创建，不自动按宿主面 + 保护层求中心线；需要保护层约束时请使用“按面布筋”。",
                    CommonButtons = TaskDialogCommonButtons.Cancel
                };
                choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Dry Run", "只验证参数，不修改模型");
                choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "创建单根", "一个 Transaction，可一次 Ctrl+Z 撤销");
                choice.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "创建 3 根 @200mm", "NumberWithSpacing 测试集，一个 Transaction");
                TaskDialogResult selected = choice.Show();
                if (selected == TaskDialogResult.Cancel)
                    return Result.Cancelled;

                bool dryRun = selected == TaskDialogResult.CommandLink1;
                bool set = selected == TaskDialogResult.CommandLink3;
                var request = new ShapeDrivenRebarCreateRequest
                {
                    HostId = host.Id.GetIdValue(),
                    BarTypeId = barType.Id.GetIdValue(),
                    StartMm = ToMillimeterPoint(start),
                    EndMm = ToMillimeterPoint(end),
                    Normal = new RebarVectorInput { X = normal.X, Y = normal.Y, Z = normal.Z },
                    LayoutMode = set ? "NumberWithSpacing" : "Single",
                    NumberOfBarPositions = set ? 3 : 1,
                    SpacingMm = 200.0,
                    DryRun = dryRun
                };

                ShapeDrivenRebarCreateResult result = RebarShapeDrivenCreateService.Execute(uiapp, request);
                if (!result.Success)
                {
                    message = result.Error;
                    TaskDialog.Show("中心线布筋", "执行失败：\n" + result.Error);
                    return Result.Failed;
                }

                string created = result.CreatedRebarId.HasValue
                    ? $"\n新钢筋 Id：{result.CreatedRebarId.Value}"
                    : string.Empty;
                TaskDialog.Show(
                    "中心线布筋",
                    result.Message + created +
                    $"\n长度：{result.LineLengthMm:0.###} mm" +
                    $"\nLayout：{result.LayoutMode}" +
                    $"\nChangedModel：{result.ChangedModel}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("中心线布筋", ex);
                message = ex.Message;
                TaskDialog.Show("中心线布筋", $"{ex.Message}\n\n诊断日志：{logPath}");
                return Result.Failed;
            }
        }

        private static XYZ ResolveNormal(View view, XYZ start, XYZ end)
        {
            XYZ direction = (end - start).Normalize();
            XYZ candidate = view.ViewDirection.Normalize();
            if (Math.Abs(candidate.DotProduct(direction)) < 1e-6)
                return candidate;

            foreach (XYZ axis in new[] { XYZ.BasisZ, XYZ.BasisY, XYZ.BasisX })
            {
                if (Math.Abs(axis.DotProduct(direction)) < 1e-6)
                    return axis;
            }

            XYZ fallback = direction.CrossProduct(XYZ.BasisZ);
            if (fallback.GetLength() < 1e-9)
                fallback = direction.CrossProduct(XYZ.BasisX);
            return fallback.Normalize();
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
