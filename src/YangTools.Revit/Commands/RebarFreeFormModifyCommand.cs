using System;
using System.Collections.Generic;
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
        "自由形状修改",
        "原位修改 unconstrained Free Form 钢筋曲线；复杂多杆 set 可通过 MCP 使用同一服务。",
        "Icons/rebar_freeform_modify_32.png",
        "Icons/rebar_freeform_modify_16.png",
        groupName: "钢筋测试",
        order: 220,
        groupLargeIcon: "Icons/rebar_test_32.png",
        groupSmallIcon: "Icons/rebar_test_16.png")]
    public sealed class RebarFreeFormModifyCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            try
            {
                Reference rebarRef = uidoc.Selection.PickObject(ObjectType.Element, new RebarSelectionFilter(), "选择 unconstrained Free Form Rebar");
                if (uidoc.Document.GetElement(rebarRef.ElementId) is not Autodesk.Revit.DB.Structure.Rebar rebar || !rebar.IsRebarFreeForm())
                {
                    TaskDialog.Show("自由形状修改", "请选择 Free Form Rebar。 ");
                    return Result.Cancelled;
                }
                if (rebar.NumberOfBarPositions != 1)
                {
                    TaskDialog.Show("自由形状修改", "UI 最小版只修改单根 Free Form；多杆 set 请使用 MCP modify_free_form_rebar。 ");
                    return Result.Cancelled;
                }

                XYZ a = uidoc.Selection.PickPoint("选择新的起点");
                XYZ b = uidoc.Selection.PickPoint("选择新的终点");
                var request = new FreeFormRebarModifyRequest
                {
                    RebarId = rebar.Id.GetIdValue(),
                    ExpectedUniqueId = rebar.UniqueId ?? string.Empty,
                    DryRun = false,
                    Bars = new List<FreeFormBarPathInput>
                    {
                        new FreeFormBarPathInput
                        {
                            PointsMm = new List<RebarPointMm> { ToMm(a), ToMm(b) }
                        }
                    }
                };

                FreeFormRebarModifyResult result = RebarFreeFormModifyService.Execute(uiapp, request);
                if (!result.Success)
                {
                    message = result.Error;
                    TaskDialog.Show("自由形状修改", "执行失败：\n" + result.Error);
                    return Result.Failed;
                }

                TaskDialog.Show("自由形状修改", result.Message + $"\nRebarId：{result.RebarId}\nBars：{result.BeforeBarCount} → {result.AfterBarCount}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("自由形状修改", ex);
                message = ex.Message;
                TaskDialog.Show("自由形状修改", ex.Message + "\n\n诊断日志：" + logPath);
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
