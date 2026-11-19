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
        panelName: "模型修改区", buttonText: "沿线布柱",
        tooltip: "沿连续模型线按固定间距放置柱。",
        largeIcon: "Icons/column_along_path_32.png", smallIcon: "Icons/column_along_path_16.png",
        usage: "使用：点击按钮，在窗口选取连续模型线、柱类型、间距、起终点及上下标高偏移，确认后生成。\n注意：模型线方向影响起点、末端及柱旋转。")]
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
