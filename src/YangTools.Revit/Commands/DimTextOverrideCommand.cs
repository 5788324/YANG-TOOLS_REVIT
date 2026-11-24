using System;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands;

[Transaction(TransactionMode.Manual)]
[RibbonButton(
        panelName: "标注工具区", buttonText: "标注替换",
        tooltip: "替换标注显示文字，不修改测量几何尺寸。",
        largeIcon: "Icons/dim_override_32.png", smallIcon: "Icons/dim_override_16.png",
        usage: "使用：点击按钮，拾取目标标注，在窗口输入需要显示的文字并确认。\n注意：这是文字覆盖，不能替代真实尺寸修改。")]
public class DimTextOverrideCommand : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		try
		{
			UIDocument activeUIDocument = commandData.Application.ActiveUIDocument;
			if (activeUIDocument == null)
			{
				TaskDialog.Show("YangTools 提示", "未找到当前正处于活动状态的 Revit 文档！");
				return Result.Cancelled;
			}
			Document document = activeUIDocument.Document;
			var filter = new DimensionSelectionFilter();
			var list = activeUIDocument.Selection.GetElementIds()
				.Where(id => filter.AllowElement(document.GetElement(id))).ToList();
			if (list.Count == 0)
			{
				list = activeUIDocument.Selection.PickObjects(ObjectType.Element, filter, "请选择要修改的标注")
					.Select(reference => reference.ElementId).Distinct().ToList();
			}
			if (list.Count == 0)
			{
				TaskDialog.Show("YangTools 提示", "未选择任何标注图元！");
				return Result.Cancelled;
			}
			DimTextOverrideWindow dimTextOverrideWindow = new DimTextOverrideWindow(list.Count);
			IntPtr mainWindowHandle = commandData.Application.MainWindowHandle;
			new WindowInteropHelper(dimTextOverrideWindow).Owner = mainWindowHandle;
			if (dimTextOverrideWindow.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			string text = (dimTextOverrideWindow.IsClear ? "" : dimTextOverrideWindow.OverrideText);
			int num = 0;
			
			using (Transaction val2 = new Transaction(document, "标注文本替换"))
			{
				try
				{
					if (val2.Start() != TransactionStatus.Started)
						throw new InvalidOperationException("无法启动标注文本替换事务。");
					foreach (ElementId item in list)
					{
						Element element = document.GetElement(item);
						Dimension val3 = element as Dimension;
						if (val3 == null || !val3.IsValidObject)
							throw new InvalidOperationException("所选标注已失效或不是标注，整批操作取消。");
						if (val3.Segments.Size > 0)
						{
							foreach (DimensionSegment segment in val3.Segments)
							{
								segment.ValueOverride = text;
							}
						}
						else
						{
							val3.ValueOverride = text;
						}
						num++;
					}
					if (num != list.Count)
						throw new InvalidOperationException("标注处理数量与选择数量不一致。");
					TransactionStatus commitStatus = val2.Commit();
					if (commitStatus != TransactionStatus.Committed)
					{
						message = $"事务未成功提交，状态: {commitStatus}";
						return Result.Failed;
					}
				}
				catch (Exception ex)
				{
					if (val2.GetStatus() == TransactionStatus.Started) val2.RollBack();
					message = ex.Message;
					return Result.Failed;
				}
			}
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}
}
