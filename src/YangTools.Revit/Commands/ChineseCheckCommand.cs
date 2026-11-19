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
        panelName: "项目管理区", buttonText: "中文检查",
        tooltip: "检查项目中含中文的可处理内容，支持定位与批量处理。",
        largeIcon: "Icons/cn_check_32.png", smallIcon: "Icons/cn_check_16.png",
        usage: "使用：点击按钮，在列表按类型筛选中文内容；可定位、选择、重命名或按界面确认安全清理。\n注意：删除或清空前确认影响范围。")]
public class ChineseCheckCommand : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		try
		{
			UIDocument activeUIDocument = commandData.Application.ActiveUIDocument;
			if (activeUIDocument == null)
			{
			TaskDialog.Show("YangTools", "未检测到任何处于打开或活动状态的项目文档！");
			return Result.Cancelled;
			}
			ChineseCheckWindow chineseCheckWindow = new ChineseCheckWindow(activeUIDocument.Document, activeUIDocument);
			IntPtr mainWindowHandle = commandData.Application.MainWindowHandle;
			new WindowInteropHelper(chineseCheckWindow).Owner = mainWindowHandle;
		chineseCheckWindow.ShowDialog();
		return Result.Succeeded;
		}
		catch (Exception ex)
		{
			TaskDialog.Show("错误", $"操作失败。\n错误信息: {ex.Message}");
		message = "中文检查执行出错：" + ex.Message;
		return Result.Failed;
		}
	}
}
