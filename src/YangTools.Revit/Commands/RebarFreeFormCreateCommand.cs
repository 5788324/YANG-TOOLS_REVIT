using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.Core.Rebar;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        "钢筋工具区",
        "自由形状",
        "创建 unconstrained Free Form 钢筋；复杂多段路径可通过 MCP 使用同一服务。",
        "Icons/rebar_freeform_create_32.png",
        "Icons/rebar_freeform_create_16.png",
        groupName: "钢筋测试",
        order: 120,
        groupLargeIcon: "Icons/rebar_test_32.png",
        groupSmallIcon: "Icons/rebar_test_16.png")]
    public sealed class RebarFreeFormCreateCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            try
            {
                Reference hostRef = uidoc.Selection.PickObject(ObjectType.Element, new RebarHostSelectionFilter(), "选择 Rebar Host");
                XYZ a = uidoc.Selection.PickPoint("选择 Free Form 钢筋起点");
                XYZ b = uidoc.Selection.PickPoint("选择 Free Form 钢筋终点");
                var req = new FreeFormRebarCreateRequest
                {
                    HostId = hostRef.ElementId.GetIdValue(),
                    DryRun = false,
                    Bars = new List<FreeFormBarPathInput>
                    {
                        new FreeFormBarPathInput
                        {
                            PointsMm = new List<RebarPointMm> { ToMm(a), ToMm(b) }
                        }
                    }
                };
                FreeFormRebarCreateResult result = RebarFreeFormCreateService.Execute(uiapp, req);
                if (!result.Success)
                {
                    message = result.Error;
                    TaskDialog.Show("自由形状", "执行失败：\n" + result.Error);
                    return Result.Failed;
                }
                TaskDialog.Show("自由形状", result.Message + $"\nRebarId：{result.CreatedRebarId}\nValidation：{result.FreeFormValidationResult}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("自由形状", ex);
                message = ex.Message;
                TaskDialog.Show("自由形状", ex.Message + "\n\n诊断日志：" + logPath);
                return Result.Failed;
            }
        }

        private static RebarPointMm ToMm(XYZ p) => new RebarPointMm
        {
            X = UnitUtils.ConvertFromInternalUnits(p.X, UnitTypeId.Millimeters),
            Y = UnitUtils.ConvertFromInternalUnits(p.Y, UnitTypeId.Millimeters),
            Z = UnitUtils.ConvertFromInternalUnits(p.Z, UnitTypeId.Millimeters)
        };
    }
}
