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
        panelName: "项目管理区", buttonText: "参数去重",
        tooltip: "扫描重复项目参数并尝试保留绑定与取值。",
        largeIcon: "Icons/parameter_cleanup_32.png", smallIcon: "Icons/parameter_cleanup_16.png",
        usage: "使用：点击按钮先扫描重复项，核对预览、来源与保留目标，再选择需要处理的组并执行。\n注意：清理项目参数可能影响明细表/标签；重要项目先在非生产副本验证。")]
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
