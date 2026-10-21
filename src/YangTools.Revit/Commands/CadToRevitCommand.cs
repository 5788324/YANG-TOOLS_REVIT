using System;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Core.CadToRevit;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        "导入工具区",
        "CAD转Revit图纸",
        "读取 YangCAD RVT Exchange，按映射把 CAD 文字、引线、线条、填充和标注重建为当前视图的 Revit 原生详图对象。",
        "Icons/cad_to_revit_32.png",
        "Icons/cad_to_revit_16.png")]
    public sealed class CadToRevitCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc?.Document == null)
            {
                TaskDialog.Show("CAD 转 Revit 图纸", "请先打开一个 Revit 项目。 ");
                return Result.Cancelled;
            }

            Document document = uidoc.Document;
            if (document.IsFamilyDocument)
            {
                TaskDialog.Show("CAD 转 Revit 图纸", "V1 仅支持 Revit 项目文档中的二维视图，不在族编辑器中创建。 ");
                return Result.Cancelled;
            }

            View view = document.ActiveView;
            if (!IsSupportedView(view))
            {
                TaskDialog.Show("CAD 转 Revit 图纸",
                    "当前视图不适合创建 Revit 原生二维详图对象。\n\n" +
                    "请切换到平面、立面、剖面、详图或绘图视图后再执行；不支持三维视图、图纸、明细表和视图样板。 ");
                return Result.Cancelled;
            }
            if (document.IsReadOnly)
            {
                TaskDialog.Show("CAD 转 Revit 图纸", "当前文档为只读状态。 ");
                return Result.Cancelled;
            }

            try
            {
                var window = new CadToRevitWindow(document, view);
                new WindowInteropHelper(window).Owner = uiapp.MainWindowHandle;
                if (window.ShowDialog() != true || window.Request == null)
                    return Result.Cancelled;

                // ShowDialog 返回后检查原项目与视图：禁止旧窗口请求写入另一个上下文。
                if (!DocumentIdentityGuard.IsSameActiveDocument(uiapp, document) ||
                    document.ActiveView == null || document.ActiveView.Id.GetIdValue() != view.Id.GetIdValue() || document.IsReadOnly)
                {
                    TaskDialog.Show("CAD 转 Revit 图纸", "当前项目或活动视图已改变，已取消转换，请在目标视图重新执行。");
                    return Result.Cancelled;
                }

                CadToRevitRebuildRequest request = window.Request;
                int mappedTypes = request.TextMappings.Concat(request.LineMappings).Concat(request.FillMappings).Concat(request.DimensionMappings)
                    .Count(x => x.Action != CadToRevitMappingActions.Skip);
                int skippedTypes = request.TextMappings.Concat(request.LineMappings).Concat(request.FillMappings).Concat(request.DimensionMappings)
                    .Count(x => x.Action == CadToRevitMappingActions.Skip);

                var confirm = new TaskDialog("确认 CAD 转 Revit 图纸")
                {
                    MainInstruction = $"把 {request.Exchange.TotalCount} 个 CAD 对象重建到“{view.Name}”？",
                    MainContent =
                        $"文字：{request.Exchange.Texts.Count}\n" +
                        $"线条：{request.Exchange.Curves.Count}\n" +
                        $"填充：{request.Exchange.Fills.Count}\n" +
                        $"标注：{request.Exchange.Dimensions.Count}\n\n" +
                        $"启用映射类型：{mappedTypes}\n跳过类型：{skippedTypes}\n" +
                        $"尺寸界线专用线样式：{request.DimensionReferenceLineStyleName}\n\n" +
                        "转换会创建 Revit 原生 TextNote / DetailCurve / FilledRegion / Dimension；整次成功项合并为一次 Ctrl+Z。",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No
                };
                if (confirm.Show() != TaskDialogResult.Yes)
                    return Result.Cancelled;
                if (!DocumentIdentityGuard.IsSameActiveDocument(uiapp, document) ||
                    document.ActiveView == null || document.ActiveView.Id.GetIdValue() != view.Id.GetIdValue() || document.IsReadOnly)
                    throw new InvalidOperationException("确认期间目标文档或活动视图已改变，禁止执行过期 CAD 转换请求。");

                CadToRevitRebuildResult result = CadToRevitRebuildService.Execute(document, view, request);
                TaskDialog.Show(result.Failures.Count > 0 ? "CAD 转 Revit 图纸 - 部分完成" : "CAD 转 Revit 图纸", result.BuildSummary());
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("CAD 转 Revit 图纸", ex);
                message = ex.Message;
                TaskDialog.Show("CAD 转 Revit 图纸失败", $"{ex.Message}\n\n诊断日志：{logPath}");
                return Result.Failed;
            }
        }

        private static bool IsSupportedView(View view)
        {
            if (view == null || view.IsTemplate) return false;
            switch (view.ViewType)
            {
                case ViewType.ThreeD:
                case ViewType.DrawingSheet:
                case ViewType.Schedule:
                    return false;
                default:
                    return true;
            }
        }
    }
}
