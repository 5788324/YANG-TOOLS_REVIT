using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "项目管理区", buttonText: "族实例",
        tooltip: "查询、批量编辑当前项目中的族实例。",
        largeIcon: "Icons/family_instance_32.png", smallIcon: "Icons/family_instance_16.png",
        usage: "使用：点击按钮，在窗口筛选族实例，选择参数编辑、换型或 Excel 操作并确认。\n注意：批量操作前检查族类型、数量和参数范围。")]
    public class FamilyInstanceManagerCommand : IExternalCommand
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

                var uiapp = commandData.Application;
                ModelessWindowRegistry.ShowOrActivate(
                    uiapp.ActiveUIDocument.Document,
                    uiapp.MainWindowHandle,
                    () => new FamilyInstanceManagerWindow(uiapp));
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
