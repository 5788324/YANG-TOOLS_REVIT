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
        panelName: "模型修改区", buttonText: "面基转换",
        tooltip: "将可处理的基于面族实例转换为非面基族。",
        largeIcon: "Icons/face_based_32.png", smallIcon: "Icons/face_based_16.png",
        usage: "使用：点击按钮，在窗口选择待转换族实例及转换选项，核对处理结果。\n限制：部分宿主依赖或参数无法安全保留的实例会被拒绝。")]
    public class FaceBasedConverterCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiapp = commandData.Application;

                var window = new FaceBasedConverterWindow(uiapp);
                var helper = new System.Windows.Interop.WindowInteropHelper(window);
                helper.Owner = uiapp.MainWindowHandle;

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
