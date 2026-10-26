using System;
using System.Collections.Generic;
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
        panelName: "文本工具区", buttonText: "文本编辑",
        tooltip: "查找、替换及格式化项目中的文本。",
        largeIcon: "Icons/text_modifier_32.png", smallIcon: "Icons/text_modifier_16.png",
        usage: "使用：点击按钮，在窗口选择检查范围、输入查找与替换规则，核对匹配内容后执行。\n注意：批量替换前确认筛选范围和格式规则。")]
    public class TextModifierCommand : IExternalCommand
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

                var window = new TextModifierWindow(commandData);
                new System.Windows.Interop.WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
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
