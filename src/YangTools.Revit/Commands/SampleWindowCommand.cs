using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands;

[Transaction(TransactionMode.Manual)]
[RibbonButton("系统管理区", "系统设置", "主题配色、Ribbon 面板自定义、插件信息", "Icons/sample_window_32.png", "Icons/sample_window_16.png")]
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
