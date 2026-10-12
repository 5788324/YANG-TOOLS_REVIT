using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("总控中心", "批处理与云链接", tooltip: "批量处理本地/已打开/云模型：导出 NWC、IFC、DWG、PDF，Revit 2021+ 可通过虚拟打印机打印 PDF；支持预检与可控命名", largeIcon: "Icons/batch_task_32.png", smallIcon: "Icons/batch_task_16.png")]
    public class BatchTaskCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var window = new BatchTaskWindow(commandData.Application);
                new System.Windows.Interop.WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("打开批处理与云链接", ex);
                TaskDialog.Show("错误", $"操作失败。\n错误信息：{ex.Message}" +
                    (string.IsNullOrEmpty(logPath) ? string.Empty : $"\n诊断日志：{logPath}"));
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
