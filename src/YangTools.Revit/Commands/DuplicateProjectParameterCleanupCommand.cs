using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        "检查工具区",
        "重复项目参数清理",
        "扫描并原位合并同名重复项目参数的绑定和值，删除不会级联影响其他对象的来源参数",
        "Icons/parameter_cleanup_32.png",
        "Icons/parameter_cleanup_16.png")]
    public sealed class DuplicateProjectParameterCleanupCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIDocument? uiDocument = commandData.Application.ActiveUIDocument;
                if (uiDocument == null)
                {
                    TaskDialog.Show("YangTools", "请先打开一个 Revit 项目文档。");
                    return Result.Cancelled;
                }

                var window = new DuplicateProjectParameterCleanupWindow(uiDocument.Document);
                new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("打开重复项目参数清理", ex);
                message = ex.Message;
                TaskDialog.Show(
                    "重复项目参数清理",
                    $"打开功能失败：\n{ex.Message}\n\n诊断日志：{logPath}");
                return Result.Failed;
            }
        }
    }
}
