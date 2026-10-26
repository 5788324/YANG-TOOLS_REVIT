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
        panelName: "项目管理区", buttonText: "族管理",
        tooltip: "管理项目载入族及编辑后的重新载入。",
        largeIcon: "Icons/family_manager_32.png", smallIcon: "Icons/family_manager_16.png",
        usage: "使用：点击按钮，选择需要查询、编辑或重载的族并检查项目中的结果。\n注意：族替换可能影响已有实例和参数。")]
    public class FamilyManagerCommand : IExternalCommand
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
                    () => new FamilyManagerWindow(uiapp));

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
