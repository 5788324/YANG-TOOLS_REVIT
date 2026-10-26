using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "项目信息区", buttonText: "文件信息",
        tooltip: "查看当前项目文件信息和清理预估。",
        largeIcon: "Icons/project_info_32.png", smallIcon: "Icons/project_info_16.png",
        usage: "使用：打开项目后点击按钮，在信息窗口查看体积、项目状态和可清理内容。\n注意：预估信息不代表已经实施清理。")]
    public class ProjectInfoCommand : IExternalCommand
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

                UIApplication uiapp = commandData.Application;
                Document doc = uiapp.ActiveUIDocument.Document;

                if (doc.IsFamilyDocument)
                {
                    TaskDialog.Show("提示", "此功能主要用于项目文件。");
                    return Result.Cancelled;
                }

                ProjectInfoWindow window = new ProjectInfoWindow(uiapp);
                new System.Windows.Interop.WindowInteropHelper(window).Owner = uiapp.MainWindowHandle;
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
