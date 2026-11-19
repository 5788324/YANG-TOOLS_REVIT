using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands;

[Transaction(TransactionMode.Manual)]
[RibbonButton(
        panelName: "视图修改区", buttonText: "覆盖清理",
        tooltip: "清理视图中针对单个元素设置的图形覆盖。",
        largeIcon: "Icons/override_cleaner_32.png", smallIcon: "Icons/override_cleaner_16.png",
        usage: "使用：点击按钮，在窗口选择视图与清理范围，确认后执行。\n注意：仅清理按元素的覆盖，不等同于重置全部可见性或视图模板。")]
	public class ViewGraphicCleanerCommand : IExternalCommand
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

				UIApplication application = commandData.Application;
				ViewGraphicCleanerWindow viewGraphicCleanerWindow = new ViewGraphicCleanerWindow(application.ActiveUIDocument.Document);
				new WindowInteropHelper(viewGraphicCleanerWindow).Owner = application.MainWindowHandle;
				viewGraphicCleanerWindow.ShowDialog();
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
