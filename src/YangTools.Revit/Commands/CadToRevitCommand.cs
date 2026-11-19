using System;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Core.CadToRevit;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "2D修改", buttonText: "CAD转图",
        tooltip: "按 YangCAD Exchange 映射，将 CAD 图纸内容重建为 Revit 详图对象。",
        largeIcon: "Icons/cad_to_revit_32.png", smallIcon: "Icons/cad_to_revit_16.png",
        usage: "使用：先由 YangCAD 生成 RVT Exchange；打开目标详图视图，点击按钮，在窗口选取映射文件并检查转换范围后执行。\n限制：短边不会被静默删除；空白文字和不支持对象会记录原因。转换后检查本地逐源追踪报告。")]
    public sealed class CadToRevitCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc?.Document == null)
            {
                TaskDialog.Show("CAD 转 Revit 图纸", "请先打开一个 Revit 项目。 ");
                return Result.Cancelled;
            }

            Document document = uidoc.Document;
            if (document.IsFamilyDocument)
            {
                TaskDialog.Show("CAD 转 Revit 图纸", "V1 仅支持 Revit 项目文档中的二维视图，不在族编辑器中创建。 ");
                return Result.Cancelled;
            }

            View view = document.ActiveView;
            if (!IsSupportedView(view))
            {
                TaskDialog.Show("CAD 转 Revit 图纸",
                    "当前视图不适合创建 Revit 原生二维详图对象。\n\n" +
                    "请切换到平面、立面、剖面、详图或绘图视图后再执行；不支持三维视图、图纸、明细表和视图样板。 ");
                return Result.Cancelled;
            }
            if (document.IsReadOnly)
            {
                TaskDialog.Show("CAD 转 Revit 图纸", "当前文档为只读状态。 ");
                return Result.Cancelled;
            }

            try
            {
                var window = new CadToRevitWindow(document, view);
                new WindowInteropHelper(window).Owner = uiapp.MainWindowHandle;
                if (window.ShowDialog() != true || window.Request == null)
                    return Result.Cancelled;

                // ShowDialog 返回后检查原项目与视图：禁止旧窗口请求写入另一个上下文。
                if (!DocumentIdentityGuard.IsSameActiveDocument(uiapp, document) ||
                    document.ActiveView == null || document.ActiveView.Id.GetIdValue() != view.Id.GetIdValue() || document.IsReadOnly)
                {
                    TaskDialog.Show("CAD 转 Revit 图纸", "当前项目或活动视图已改变，已取消转换，请在目标视图重新执行。");
                    return Result.Cancelled;
                }

                CadToRevitRebuildRequest request = window.Request;
                CadToRevitRebuildResult result = CadToRevitRebuildService.Execute(document, view, request);
                int remaining = result.SourceOutcomes.Count(row => row.Status != "created");
                if (remaining > 0 || result.Failures.Count > 0 || result.AuditWarnings.Count > 0 ||
                    string.IsNullOrWhiteSpace(result.ReportPath))
                {
                    string report = string.IsNullOrWhiteSpace(result.ReportPath)
                        ? "转换报告未保存，请查看诊断日志。"
                        : $"转换报告：{result.ReportPath}";
                    string title = remaining > 0 || result.Failures.Count > 0
                        ? "CAD 转 Revit 图纸 · 部分完成"
                        : "CAD 转 Revit 图纸 · 提醒";
                    TaskDialog.Show(title, $"已创建 {result.CreatedTotal} 个图元。跳过或未完成的源对象 {remaining} 项，失败 {result.Failures.Count} 项。\n{report}");
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("CAD 转 Revit 图纸", ex);
                message = ex.Message;
                TaskDialog.Show("CAD 转 Revit 图纸失败", $"{ex.Message}\n\n诊断日志：{logPath}");
                return Result.Failed;
            }
        }

        private static bool IsSupportedView(View view)
        {
            if (view == null || view.IsTemplate) return false;
            switch (view.ViewType)
            {
                case ViewType.ThreeD:
                case ViewType.DrawingSheet:
                case ViewType.Schedule:
                    return false;
                default:
                    return true;
            }
        }
    }
}
