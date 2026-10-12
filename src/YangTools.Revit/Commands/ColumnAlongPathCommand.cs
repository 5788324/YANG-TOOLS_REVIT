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
        "模型修改区",
        "沿线布柱",
        "选择连续模型线，按固定间距布置柱，沿路线切线旋转并设置底部/顶部标高与偏移",
        "Icons/column_along_path_32.png",
        "Icons/column_along_path_16.png")]
    public sealed class ColumnAlongPathCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                if (uiapp.ActiveUIDocument == null)
                {
                    TaskDialog.Show("提示", "请先打开一个项目文档。");
                    return Result.Cancelled;
                }

                ModelessWindowRegistry.ShowOrActivate(
                    uiapp.ActiveUIDocument.Document,
                    uiapp.MainWindowHandle,
                    () => new ColumnAlongPathWindow(uiapp));

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("打开沿线布柱窗口", ex);
                TaskDialog.Show("错误", $"打开沿线布柱窗口失败。\n错误信息：{ex.Message}");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
