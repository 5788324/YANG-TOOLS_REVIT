using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "项目管理区", buttonText: "批量任务",
        tooltip: "批量导出本地/已打开/云模型。",
        largeIcon: "Icons/batch_task_32.png", smallIcon: "Icons/batch_task_16.png",
        usage: "使用：点击按钮，在窗口选择文件/视图集、导出类型和输出目录，先预检核对文件名再执行。\n注意：Revit 2021 使用虚拟打印机打印 PDF。")]
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
