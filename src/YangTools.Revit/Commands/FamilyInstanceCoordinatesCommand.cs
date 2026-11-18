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
        panelName: "项目管理区", buttonText: "族坐标",
        tooltip: "查看和设置点定位族的三套坐标。",
        largeIcon: "Icons/family_coordinates_32.png", smallIcon: "Icons/family_coordinates_16.png",
        usage: "使用：选中族实例并打开窗口，点击一套坐标，输入目标位置后应用。\n限制：仅当前项目中的点定位族。每次移动一个实例，可撤销。")]
    public sealed class FamilyInstanceCoordinatesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null || uidoc.Document.IsFamilyDocument)
            {
                TaskDialog.Show("族实例坐标", "请先打开一个 Revit 项目。");
                return Result.Cancelled;
            }

            try
            {
                var doc = uidoc.Document;
                var selected = uidoc.Selection.GetElementIds();
                var ids = selected.Count > 0
                    ? selected
                    : uidoc.Selection.PickObjects(ObjectType.Element, new PointFamilyFilter(),
                        "请选择点定位的族实例").Select(reference => reference.ElementId).ToList();

                var instances = ids.Distinct().Select(id => doc.GetElement(id))
                    .OfType<FamilyInstance>().Where(instance => instance.Location is LocationPoint).ToList();
                if (instances.Count == 0)
                {
                    TaskDialog.Show("族实例坐标", "请选择具有插入点的族实例，例如点定位的柱或设备。");
                    return Result.Cancelled;
                }

                var window = new FamilyInstanceCoordinatesWindow(doc, instances);
                new WindowInteropHelper(window).Owner = data.Application.MainWindowHandle;
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private sealed class PointFamilyFilter : ISelectionFilter
        {
            public bool AllowElement(Element element) => element is FamilyInstance instance && instance.Location is LocationPoint;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
