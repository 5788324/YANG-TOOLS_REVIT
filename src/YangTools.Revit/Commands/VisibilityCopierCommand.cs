using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "视图修改区", buttonText: "可见性复制",
        tooltip: "将源视图的图形可见性设置复制至其他视图。",
        largeIcon: "Icons/visibility_copier_32.png", smallIcon: "Icons/visibility_copier_16.png",
        usage: "使用：点击按钮，在窗口选择源视图、目标视图与复制项目，再执行。\n注意：检查视图模板及目标视图覆盖情况。")]
    public class VisibilityCopierCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (commandData.Application.ActiveUIDocument == null)
                {
                    TaskDialog.Show("提示", "请先打开一个项目文档。");
                    return Result.Cancelled;
                }

                var uiApp = commandData.Application;
                var doc = uiApp.ActiveUIDocument.Document;

                var window = new VisibilityCopierWindow(doc);
                var helper = new System.Windows.Interop.WindowInteropHelper(window)
                {
                    Owner = uiApp.MainWindowHandle
                };

                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", $"操作失败。\n错误信息: {ex.Message}");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
