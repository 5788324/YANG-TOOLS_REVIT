using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands;

[Transaction(TransactionMode.Manual)]
[RibbonButton(
        panelName: "模型修改区", buttonText: "标高修改",
        tooltip: "修改图元关联标高并尽量保持模型世界位置。",
        largeIcon: "Icons/level_modifier_32.png", smallIcon: "Icons/level_modifier_16.png",
        usage: "使用：预选目标图元或点击后拾取，再选择新标高。按原世界位置计算偏移。\n注意：宿主、镜像或复杂约束图元可能无法安全修改。")]
public class LevelModifierCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                TaskDialog.Show("提示", "请先打开一个项目文档。");
                return Result.Cancelled;
            }
            Document doc = uiDoc.Document;
            // 模态窗口显示前即冻结选择；拒绝预览过程中混入的新选择。
            ElementId[] elementIds = uiDoc.Selection.GetElementIds().ToArray();
            if (elementIds.Length == 0)
            {
                elementIds = uiDoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element,
                        "选择要修改标高的图元，然后点击完成。")
                    .Select(reference => reference.ElementId).ToArray();
                if (elementIds.Length == 0) return Result.Cancelled;
            }
            List<Level> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(level => level.ProjectElevation).ToList();
            if (levels.Count == 0)
            {
                TaskDialog.Show("错误", "项目中没有找到标高。");
                return Result.Failed;
            }
            LevelModifierWindow window = new LevelModifierWindow(levels, elementIds.Length);
            new System.Windows.Interop.WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.ShowDialog();
            if (!window.IsOk || window.SelectedLevel == null) return Result.Cancelled;
            if (!doc.IsValidObject || uiDoc.Document != doc)
            {
                message = "原项目文档已失效，未执行标高修改。";
                TaskDialog.Show("操作取消", message);
                return Result.Cancelled;
            }

            LevelModifierRequest request = LevelModifierRequest.Create(window.SelectedLevel.Id, elementIds);
            LevelModifierResult result = LevelModifierService.Execute(doc, request);
            if (result.NoChange)
            {
                TaskDialog.Show("提示", "所有所选图元已位于目标标高，无需修改；未创建 Undo。 ");
                return Result.Succeeded;
            }
            if (!result.Committed)
            {
                message = result.Error ?? "标高修改未提交。";
                TaskDialog.Show("标高修改未完成", message);
                return Result.Failed;
            }
            if (!result.ReadbackConfirmed)
            {
                TaskDialog.Show("需要核验", result.Error ?? "事务已提交但模型状态未确认，请勿立即重复执行。");
                return Result.Succeeded;
            }
            try
            {
                uiDoc.RefreshActiveView();
            }
            catch (Exception refreshEx)
            {
                TaskDialog.Show("已提交，界面刷新异常", "标高修改已提交，模型回读通过。界面刷新失败：" + refreshEx.Message);
                return Result.Succeeded;
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
            TaskDialog.Show("错误", "标高修改异常：" + ex.Message);
            return Result.Failed;
        }
    }
}
