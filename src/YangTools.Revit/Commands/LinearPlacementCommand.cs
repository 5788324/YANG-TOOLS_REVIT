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
        panelName: "模型修改区", buttonText: "沿线布置",
        tooltip: "沿选定路径重复布置指定族实例。",
        largeIcon: "Icons/linear_placement_32.png", smallIcon: "Icons/linear_placement_16.png",
        usage: "使用：点击按钮，在窗口选择路径、族类型、间距或数量，以及标高等参数后执行。\n注意：核对路径方向、标高基准与数量。")]
    public class LinearPlacementCommand : IExternalCommand
    {
        private static LinearPlacementWindow? _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiapp = commandData.Application;

                if (_window == null)
                {
                    _window = new LinearPlacementWindow(uiapp);
                    _window.Closed += (s, e) => _window = null;
                    
                    var handle = uiapp.MainWindowHandle;
                    var helper = new System.Windows.Interop.WindowInteropHelper(_window);
                    helper.Owner = handle;

                    _window.Show();
                }
                else
                {
                    _window.Activate();
                }

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
