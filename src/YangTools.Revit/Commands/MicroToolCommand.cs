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
        panelName: "项目工具区", buttonText: "插件箱",
        tooltip: "管理、加载及运行临时 Revit 插件。",
        largeIcon: "Icons/micro_tool_32.png", smallIcon: "Icons/micro_tool_16.png",
        usage: "使用：点击按钮，在插件箱中选择已登记插件，阅读说明后执行或管理收藏。\n注意：临时 DLL 会在 Revit 进程内执行，不是沙箱。")]
    public class MicroToolCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var window = new MicroToolWindow();
                new System.Windows.Interop.WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
                if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.SelectedDllPath))
                    return Result.Cancelled;

                return MicroToolEngine.ExecuteMicroTool(window.SelectedDllPath, commandData, ref message, elements);
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("临时插件箱入口", ex);
                message = ex.Message;
                TaskDialog.Show(
                    "临时插件箱",
                    $"打开或运行临时插件失败：\n{ex.Message}" +
                    (string.IsNullOrWhiteSpace(logPath) ? string.Empty : $"\n\n诊断日志：{logPath}"));
                return Result.Failed;
            }
        }
    }
}
