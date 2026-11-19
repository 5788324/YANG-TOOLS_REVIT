using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands;

[Transaction(TransactionMode.Manual)]
[RibbonButton(
        panelName: "系统管理区", buttonText: "系统设置",
        tooltip: "打开主题、Ribbon 布局与插件选项。",
        largeIcon: "Icons/sample_window_32.png", smallIcon: "Icons/sample_window_16.png",
        usage: "使用：点击按钮，按需要修改主题或 Ribbon 显隐并保存设置。\n注意：部分 Ribbon 布局可能需要重新加载插件才能完全生效。")]
public class SampleWindowCommand : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		try
		{
			SystemSettingsWindow settingsWindow = new SystemSettingsWindow();
			IntPtr mainWindowHandle = commandData.Application.MainWindowHandle;
			new WindowInteropHelper(settingsWindow).Owner = mainWindowHandle;
			settingsWindow.ShowDialog();
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
		message = "执行界面程序出错：" + ex.Message;
		return Result.Failed;
		}
	}
}
