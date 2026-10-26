using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "模型修改区", buttonText: "材质绑定",
        tooltip: "给选中族实例或相应族类型绑定材质参数并赋予材质。",
        largeIcon: "Icons/material_binding_32.png", smallIcon: "Icons/material_binding_16.png",
        usage: "使用：先选择族实例或点击后拾取，再在窗口选择材质、参数名与实例/类型作用范围，确认后执行。\n注意：选择类型级会影响同类型的其他实例。")]
    public sealed class SimpleFamilyMaterialBindingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null || uidoc.Document == null)
            {
                TaskDialog.Show("绑定材质", "请先打开一个 Revit 项目文档。");
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;
            if (doc.IsFamilyDocument)
            {
                TaskDialog.Show("绑定材质", "请在项目文档中选择族实例后运行此功能。");
                return Result.Cancelled;
            }

            try
            {
                List<ElementId> instanceIds = GetPreselectedInstances(uidoc, doc);
                if (instanceIds.Count == 0)
                    instanceIds = PickInstances(uidoc);
                if (instanceIds.Count == 0)
                    return Result.Cancelled;

                while (true)
                {
                    var window = new SimpleFamilyMaterialBindingWindow(doc, instanceIds);
                    new WindowInteropHelper(window).Owner = uiapp.MainWindowHandle;
                    bool? dialogResult = window.ShowDialog();

                    if (window.ReselectRequested)
                    {
                        List<ElementId> reselected = PickInstances(uidoc);
                        if (reselected.Count > 0)
                            instanceIds = reselected;
                        continue;
                    }

                    if (dialogResult != true || window.Request == null)
                        return Result.Cancelled;

                    SimpleFamilyMaterialBindingRequest request = window.Request;
                    int familyCount = request.InstanceIds
                        .Select(id => doc.GetElement(id) as FamilyInstance)
                        .Where(instance => instance?.Symbol?.Family != null)
                        .Select(instance => instance.Symbol.Family.Id.GetIdValue())
                        .Distinct()
                        .Count();
                    string scope = request.IsInstanceParameter ? "仅所选实例" : "所选实例对应的族类型（同类型其它实例也会变化）";

                    TaskDialog confirm = new TaskDialog("确认绑定并赋予材质")
                    {
                        MainInstruction = $"绑定参数“{request.ParameterName}”并赋予“{request.MaterialName}”？",
                        MainContent = $"所选实例：{request.InstanceIds.Count}\n涉及族：{familyCount}\n赋值范围：{scope}\n\n将自动打开相关载入族、绑定几何 Material 参数、回载项目，再设置选定材质。",
                        CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                        DefaultButton = TaskDialogResult.No
                    };
                    if (confirm.Show() != TaskDialogResult.Yes)
                        return Result.Cancelled;

                    SimpleFamilyMaterialBindingResult result = SimpleFamilyMaterialBindingService.Execute(doc, request);
                    TaskDialog.Show("绑定材质完成", result.BuildSummary(request.IsInstanceParameter, request.MaterialName));
                    return result.AssignedCount > 0 ? Result.Succeeded : Result.Failed;
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                string log = DiagnosticLog.WriteError("简易绑定材质", ex);
                TaskDialog.Show("绑定材质失败", $"{ex.Message}\n\n诊断日志：{log}");
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static List<ElementId> GetPreselectedInstances(UIDocument uidoc, Document doc)
        {
            return uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<FamilyInstance>()
                .Select(instance => instance.Id)
                .GroupBy(id => id.GetIdValue())
                .Select(group => group.First())
                .ToList();
        }

        private static List<ElementId> PickInstances(UIDocument uidoc)
        {
            IList<Reference> references = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new FamilyInstanceSelectionFilter(),
                "请选择需要绑定并赋予材质的族实例（可多选，完成后点击完成）");
            return references
                .Select(reference => reference.ElementId)
                .GroupBy(id => id.GetIdValue())
                .Select(group => group.First())
                .ToList();
        }
    }
}
