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
        tooltip: "批量导出或处理本地及云端关联模型。",
        largeIcon: "Icons/batch_task_32.png", smallIcon: "Icons/batch_task_16.png",
        usage: "使用：点击按钮，在窗口选择文件/视图集、导出类型和输出目录，先预检后执行。\n注意：云端模型权限和 PDF 输出方式依 Revit 版本与环境而异。")]
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
