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
        panelName: "项目工具区", buttonText: "重跑微工具",
        tooltip: "直接重新运行最近一次执行过的临时插件，无需再次打开插件箱（可在 Revit 快捷键 KS 中绑定专属快捷键，执行一次后也可直接按 Enter 重复运行）。",
        largeIcon: "Icons/micro_tool_repeat_32.png", smallIcon: "Icons/micro_tool_repeat_16.png",
        usage: "使用：在插件箱中运行任意临时插件后，点击本按钮（或为其绑定 Revit 快捷键如 MT）即可直接执行该插件，之后按 Enter 也会直接重复执行该插件。\n注意：若尚未运行过任何临时插件或原 DLL 已被移动，将自动回退打开插件箱供重新选择。")]
    public sealed class MicroToolRepeatCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                string? lastDll = MicroToolEngine.GetLastExecutedDllPath();
                if (string.IsNullOrWhiteSpace(lastDll))
                {
                    var window = new MicroToolWindow();
                    new System.Windows.Interop.WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
                    if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.SelectedDllPath))
                        return Result.Cancelled;
                    lastDll = window.SelectedDllPath;
                }

                return MicroToolEngine.ExecuteMicroTool(lastDll!, commandData, ref message, elements);
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("重跑微工具入口", ex);
                message = ex.Message;
                TaskDialog.Show(
                    "重跑微工具",
                    $"运行最近临时插件失败：\n{ex.Message}" +
                    (string.IsNullOrWhiteSpace(logPath) ? string.Empty : $"\n\n诊断日志：{logPath}"));
                return Result.Failed;
            }
        }
    }
}
