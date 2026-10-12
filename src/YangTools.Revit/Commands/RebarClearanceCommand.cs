using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.Core.Rebar;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [RibbonButton(
        "钢筋工具区",
        "碰撞与净距",
        "只读筛查钢筋之间的硬碰撞与净距不足；直线精确，圆弧/其他曲线为离散筛查。",
        "Icons/rebar_clearance_32.png",
        "Icons/rebar_clearance_16.png",
        groupName: "钢筋测试",
        order: 400,
        groupLargeIcon: "Icons/rebar_test_32.png",
        groupSmallIcon: "Icons/rebar_test_16.png")]
    public sealed class RebarClearanceCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;

            try
            {
                List<ElementId> ids = uidoc.Selection.GetElementIds()
                    .Where(id => uidoc.Document.GetElement(id) is Autodesk.Revit.DB.Structure.Rebar)
                    .ToList();
                if (ids.Count < 2)
                {
                    IList<Reference> refs;
                    try
                    {
                        refs = uidoc.Selection.PickObjects(
                            ObjectType.Element,
                            new RebarSelectionFilter(),
                            "选择至少两组要筛查的 Rebar，完成后点击“完成”。");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        return Result.Cancelled;
                    }
                    ids = refs.Select(reference => reference.ElementId).Distinct().ToList();
                }

                if (ids.Count < 2)
                {
                    TaskDialog.Show("碰撞与净距", "至少需要选择两个 Rebar 元素。\n同一 set 内部检查请使用 MCP 的 include_same_element=true。");
                    return Result.Cancelled;
                }

                var request = new RebarClearanceRequest
                {
                    RebarIds = ids.Select(id => id.GetIdValue()).ToList(),
                    MinimumClearanceMm = 25.0,
                    IncludeSameElement = false,
                    MaxResults = 20
                };
                RebarClearanceReport report = RebarClearanceService.Analyze(uiapp, request);
                if (!report.Success)
                {
                    TaskDialog.Show("碰撞与净距", "分析失败：\n" + report.Error);
                    return Result.Failed;
                }

                string preview = string.Join("\n", report.Conflicts.Take(8).Select(item =>
                    $"{item.Status}: {item.RebarAId}[{item.BarAPosition}] ↔ {item.RebarBId}[{item.BarBPosition}], gap={item.SurfaceGapMm:0.###} mm"));
                var dialog = new TaskDialog("碰撞与净距")
                {
                    MainInstruction = $"硬碰撞 {report.ClashCount}；净距不足 {report.ClearanceViolationCount}",
                    MainContent =
                        $"物理钢筋：{report.PhysicalBarCount}\n" +
                        $"Broad-phase 候选：{report.BroadPhaseCandidatePairs}\n" +
                        $"详细检查：{report.DetailedPairChecks}\n" +
                        $"阈值：{report.MinimumClearanceMm:0.###} mm\n\n" +
                        (string.IsNullOrWhiteSpace(preview) ? "未发现小于阈值的钢筋对。" : preview) +
                        "\n\nUI 试用版固定阈值 25 mm；完整明细和自定义阈值请用 MCP analyze_rebar_clearance。",
                    CommonButtons = TaskDialogCommonButtons.Ok
                };
                dialog.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("碰撞与净距", ex);
                message = ex.Message;
                TaskDialog.Show("碰撞与净距", ex.Message + "\n\n诊断日志：" + logPath);
                return Result.Failed;
            }
        }
    }
}
