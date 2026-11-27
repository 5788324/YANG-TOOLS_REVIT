using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "模型修改区", buttonText: "DWG生管",
        tooltip: "选择导入或链接的 DWG，识别指定图层的直线、多段线和三维多段线并自动创建管道与管件连接。",
        largeIcon: "Icons/dwg_to_pipe_32.png", smallIcon: "Icons/dwg_to_pipe_16.png",
        usage: "使用：预选或运行后点选一个 DWG，勾选一个或多个包含管线的图层，设置管道系统/类型/管径及高程模式，点击生成管道。\n注意：支持二维直线/多段线与三维多段线（保留 3D 坡度与立管），自动创建多段线弯头及共端点弯头/三通/四通，支持单次 Ctrl+Z 撤销。")]
    public sealed class DwgToPipeCommand : IExternalCommand
    {
        private const string Title = "DWG 转管道";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument? uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null) return Result.Cancelled;
            Document doc = uiDoc.Document;
            if (doc.IsFamilyDocument || doc.IsReadOnly || doc.IsModifiable)
            {
                TaskDialog.Show(Title, "请在可编辑的项目文档中运行此命令。");
                return Result.Cancelled;
            }

            try
            {
                ImportInstance? dwg = uiDoc.Selection.GetElementIds()
                    .Select(doc.GetElement)
                    .OfType<ImportInstance>()
                    .FirstOrDefault();

                if (dwg == null)
                {
                    Reference picked = uiDoc.Selection.PickObject(
                        ObjectType.Element,
                        new ImportInstanceSelectionFilter(),
                        "请选择一个导入或链接的 DWG 图纸");
                    dwg = doc.GetElement(picked.ElementId) as ImportInstance;
                }

                if (dwg == null) return Result.Cancelled;

                List<DwgCurveChain> chains = DwgToPipeService.ExtractChains(doc, dwg);
                if (chains.Count == 0)
                {
                    TaskDialog.Show(Title, "所选 DWG 中未识别到直线、多段线或三维多段线。");
                    return Result.Cancelled;
                }

                List<DwgLayerSummary> layers = DwgToPipeService.SummarizeLayers(chains);

                List<PipingSystemType> systemTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(PipingSystemType))
                    .Cast<PipingSystemType>()
                    .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                List<PipeType> pipeTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(PipeType))
                    .Cast<PipeType>()
                    .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                List<Level> levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(x => x.Elevation)
                    .ToList();

                if (systemTypes.Count == 0 || pipeTypes.Count == 0 || levels.Count == 0)
                {
                    TaskDialog.Show(Title, "当前项目缺少管道系统类型、管道类型或标高，请先在项目中配置至少一个后再运行。");
                    return Result.Cancelled;
                }

                ElementId defaultLevelId = doc.ActiveView?.GenLevel?.Id ?? levels[0].Id;
                string dwgName = dwg.Category?.Name ?? dwg.Name ?? "DWG";

                var dialog = new DwgToPipeWindow(dwgName, layers, systemTypes, pipeTypes, levels, defaultLevelId);
                new WindowInteropHelper(dialog).Owner = uiApp.MainWindowHandle;
                if (dialog.ShowDialog() != true || dialog.Request == null)
                    return Result.Cancelled;

                if (!DocumentIdentityGuard.IsSameActiveDocument(uiApp, doc))
                {
                    TaskDialog.Show(Title, "当前活动项目已变化，已取消创建管道。");
                    return Result.Cancelled;
                }

                DwgToPipeResult result = DwgToPipeService.Execute(doc, chains, dialog.Request);

                string summary =
                    $"已创建管道：{result.PipesCreated} 根\n" +
                    $"已创建连接：弯头 {result.ElbowsCreated} 个 · 三通 {result.TeesCreated} 个 · 四通 {result.CrossesCreated} 个";
                if (result.SkippedShortSegments > 0)
                    summary += $"\n已忽略过短线段：{result.SkippedShortSegments} 段";
                if (result.FailedSegments > 0)
                    summary += $"\n创建失败线段：{result.FailedSegments} 段";
                if (result.FailedConnections > 0)
                    summary += $"\n未能自动连接的转角/节点：{result.FailedConnections} 处（请检查管道类型的布管系统配置）";

                TaskDialog.Show(Title, summary);
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show(Title, $"DWG 转管道失败：{ex.Message}");
                return Result.Failed;
            }
        }

        private sealed class ImportInstanceSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is ImportInstance;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
