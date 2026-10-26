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
        panelName: "钢筋工具区", buttonText: "渐变钢筋",
        tooltip: "按首末控制钢筋生成渐变 Free Form 钢筋集。",
        largeIcon: "Icons/rebar_gradient_set_32.png", smallIcon: "Icons/rebar_gradient_set_16.png",
        usage: "使用：点击按钮，在窗口选取起始与终止钢筋及路径/分区间距，检查方向与长度后执行。\n注意：Revit 2021 目前有已知 API 限制，不能因编译通过认定可用。",
        groupName: "创建钢筋", order: 100,
        groupLargeIcon: "Icons/rebar_gradient_set_32.png", groupSmallIcon: "Icons/rebar_gradient_set_16.png")]
    public sealed class RebarGradientSetCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                if (uiapp.ActiveUIDocument == null)
                {
                    TaskDialog.Show("渐变钢筋集", "请先打开一个项目文档。");
                    return Result.Cancelled;
                }

                ModelessWindowRegistry.ShowOrActivate(
                    uiapp.ActiveUIDocument.Document,
                    uiapp.MainWindowHandle,
                    () => new RebarGradientSetWindow(uiapp));

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("打开渐变钢筋集窗口", ex);
                message = ex.Message;
                TaskDialog.Show("渐变钢筋集", "打开窗口失败：\n" + ex.Message + "\n\n诊断日志：" + logPath);
                return Result.Failed;
            }
        }
    }
}
