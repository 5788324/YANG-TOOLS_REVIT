using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Newtonsoft.Json;
using YangTools.Revit.Core;
using YangTools.Revit.Core.Rebar;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [RibbonButton(
        "钢筋工具区",
        "钢筋诊断",
        "只读检查钢筋引擎、宿主、排布、约束、弯钩与中心线。",
        "Icons/rebar_inspector_32.png",
        "Icons/rebar_inspector_16.png",
        groupName: "钢筋测试",
        order: 390,
        groupLargeIcon: "Icons/rebar_test_32.png",
        groupSmallIcon: "Icons/rebar_test_16.png")]
    public sealed class RebarInspectorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show("钢筋诊断", "请先打开项目文档。");
                return Result.Cancelled;
            }

            try
            {
                IReadOnlyList<ElementId> ids = RebarInspectionService.GetSelectedRebarIds(uidoc);
                if (ids.Count == 0)
                {
                    IList<Reference> picked;
                    try
                    {
                        picked = uidoc.Selection.PickObjects(
                            ObjectType.Element,
                            new RebarSelectionFilter(),
                            "请选择要诊断的钢筋，完成后点击“完成”。");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        return Result.Cancelled;
                    }

                    ids = picked.Select(reference => reference.ElementId).ToList();
                }

                if (ids.Count == 0)
                {
                    TaskDialog.Show("钢筋诊断", "没有选择钢筋。");
                    return Result.Cancelled;
                }

                RebarInspectionReport report = RebarInspectionService.Inspect(uiapp, ids);
                string json = JsonConvert.SerializeObject(report, Formatting.Indented);
                string path = SaveReport(json);
                DiagnosticLog.WriteInfo("钢筋诊断", $"RebarCount={report.RebarCount}; Report={path}");

                int shapeDriven = report.Items.Count(item => item.EngineMode == "ShapeDriven");
                int freeForm = report.Items.Count(item => item.EngineMode == "FreeForm");
                int varying = report.Items.Count(item => item.HasVariableLengthBars == true);
                int constrained = report.Items.Count(item => (item.ConstrainedHandleCount ?? 0) > 0);
                int errors = report.Items.Count(item => !string.IsNullOrWhiteSpace(item.Error));

                var dialog = new TaskDialog("钢筋诊断")
                {
                    MainInstruction = $"已只读检查 {report.RebarCount} 个钢筋元素",
                    MainContent =
                        $"Shape Driven：{shapeDriven}\n" +
                        $"Free Form：{freeForm}\n" +
                        $"变长钢筋集：{varying}\n" +
                        $"存在外部约束：{constrained}\n" +
                        $"读取异常：{errors}\n\n" +
                        $"详细 JSON 已保存：\n{path}",
                    CommonButtons = TaskDialogCommonButtons.Ok
                };
                dialog.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("钢筋诊断", ex);
                message = ex.Message;
                TaskDialog.Show("钢筋诊断失败", $"{ex.Message}\n\n诊断日志：{logPath}");
                return Result.Failed;
            }
        }

        private static string SaveReport(string json)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YangTools",
                "Diagnostics",
                "Rebar");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"RebarInspection_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(path, json, new UTF8Encoding(false));
            return path;
        }
    }
}
