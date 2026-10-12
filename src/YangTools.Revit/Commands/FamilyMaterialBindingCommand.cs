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
        "项目管理区",
        "批量绑定材质",
        "批量给一个或多个载入族中的可绑定几何关联统一材质族参数，并重新载入项目",
        "Icons/family_manager_32.png",
        "Icons/family_manager_16.png")]
    public sealed class FamilyMaterialBindingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                TaskDialog.Show("批量绑定材质", "请先打开一个 Revit 项目文档。");
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            if (doc.IsFamilyDocument)
            {
                TaskDialog.Show("批量绑定材质", "请在 Revit 项目文档中运行此功能，以便批量编辑载入族并自动回载项目。");
                return Result.Cancelled;
            }

            try
            {
                var window = new FamilyMaterialBindingWindow(doc);
                new WindowInteropHelper(window).Owner = uiapp.MainWindowHandle;
                if (window.ShowDialog() != true || window.Request == null)
                    return Result.Cancelled;

                FamilyMaterialBindingRequest request = window.Request;
                string sampleNames = string.Join("、", request.FamilyIds
                    .Select(id => doc.GetElement(id) as Family)
                    .Where(family => family != null)
                    .Select(family => family.Name)
                    .Take(6));
                string suffix = request.FamilyIds.Count > 6 ? $" 等 {request.FamilyIds.Count} 个族" : string.Empty;
                string scope = request.IsInstanceParameter ? "实例参数" : "类型参数";
                string overwrite = request.RebindExistingAssociations ? "会重新绑定已有的其它材质参数关联" : "已有其它材质参数关联将跳过";

                TaskDialog confirm = new TaskDialog("确认批量绑定材质")
                {
                    MainInstruction = $"给所选族绑定材质参数“{request.ParameterName}”？",
                    MainContent = $"族：{sampleNames}{suffix}\n参数方式：{scope}\n绑定策略：{overwrite}\n\nYangTools 会打开每个族文档，只处理具有可关联 Material 参数的几何元素，然后重新载入原项目。不会自动指定具体材质值。",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No
                };
                if (confirm.Show() != TaskDialogResult.Yes)
                    return Result.Cancelled;

                FamilyMaterialBindingResult result = FamilyMaterialBindingService.Execute(doc, request);
                TaskDialog.Show("批量绑定材质完成", result.BuildSummary());
                return result.FailedCount == result.RequestedCount ? Result.Failed : Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                string log = DiagnosticLog.WriteError("绑定材质", ex);
                TaskDialog.Show("批量绑定材质失败", $"{ex.Message}\n\n诊断日志：{log}");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
