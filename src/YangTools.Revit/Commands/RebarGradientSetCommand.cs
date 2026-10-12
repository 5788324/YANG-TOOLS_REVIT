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
        "钢筋工具区",
        "渐变钢筋集",
        "以两根控制钢筋定义首末形状，默认沿全局 Z 轴自动计算长度，也可沿直线 ModelLine 或模型 Edge 按分区最大间距创建一个完整 Free Form 渐变钢筋集。",
        "Icons/rebar_gradient_set_32.png",
        "Icons/rebar_gradient_set_16.png",
        groupName: "创建钢筋",
        order: 100,
        groupLargeIcon: "Icons/rebar_gradient_set_32.png",
        groupSmallIcon: "Icons/rebar_gradient_set_16.png")]
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
