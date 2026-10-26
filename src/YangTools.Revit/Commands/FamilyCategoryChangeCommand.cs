using System;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "项目管理区", buttonText: "族类别",
        tooltip: "修改可编辑载入族的族类别。",
        largeIcon: "Icons/family_category_32.png", smallIcon: "Icons/family_category_16.png",
        usage: "使用：点击按钮，在窗口选定可编辑载入族及目标类别，核对映射后执行。\n注意：类别更改可能影响可见性、明细表和族参数。")]
    public sealed class FamilyCategoryChangeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                TaskDialog.Show("族类别修改", "请先打开一个 Revit 项目文档。");
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            if (doc.IsFamilyDocument)
            {
                TaskDialog.Show("族类别修改", "请在项目文档中运行此功能；族文档请直接使用“族类别和族参数”。");
                return Result.Cancelled;
            }

            try
            {
                var window = new FamilyCategoryChangeWindow(doc);
                new WindowInteropHelper(window).Owner = uiapp.MainWindowHandle;
                if (window.ShowDialog() != true || window.Request == null)
                    return Result.Cancelled;

                FamilyCategoryChangeRequest request = window.Request;
                string sampleNames = string.Join("、", request.FamilyIds
                    .Select(id => doc.GetElement(id) as Family)
                    .Where(family => family != null)
                    .Select(family => family.Name)
                    .Take(6));
                string suffix = request.FamilyIds.Count > 6 ? $" 等 {request.FamilyIds.Count} 个族" : string.Empty;

                TaskDialog confirm = new TaskDialog("确认修改族类别")
                {
                    MainInstruction = $"将所选族修改为“{request.TargetCategoryName}”？",
                    MainContent = $"族：{sampleNames}{suffix}\n\n这会改变 Revit Family Category，可能影响可见性、明细表/过滤器、类别专用参数和宿主行为。\n\nYangTools 会在回载后复核族/类型/实例 ElementId 与正 ID 自定义参数内部值；发现异常会回滚整批。",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No
                };
                if (confirm.Show() != TaskDialogResult.Yes)
                    return Result.Cancelled;

                FamilyCategoryChangeResult result = FamilyCategoryChangeService.Execute(doc, request);
                TaskDialog.Show("族类别修改完成", result.BuildSummary());
                return result.FailedCount == result.RequestedCount ? Result.Failed : Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                string log = DiagnosticLog.WriteError("族类别修改", ex);
                TaskDialog.Show("族类别修改失败", $"{ex.Message}\n\n诊断日志：{log}");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
