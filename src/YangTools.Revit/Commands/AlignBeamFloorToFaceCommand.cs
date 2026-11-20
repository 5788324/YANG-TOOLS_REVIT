using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(
        panelName: "模型修改区", buttonText: "梁板贴面",
        tooltip: "平移梁或板，使所选构件面与目标模型面齐平。",
        largeIcon: "Icons/align_beam_floor_face_32.png", smallIcon: "Icons/align_beam_floor_face_16.png",
        usage: "使用：在模型视图先选梁或板的面，再选目标模型面。\n限制：两个面必须是当前项目内的平行平面。")]
    public sealed class AlignBeamFloorToFaceCommand : IExternalCommand
    {
        private const string Title = "梁板贴面";
        private const double DistanceTolerance = 0.1 / 304.8;
        private const double NormalTolerance = 1e-6;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null) return Result.Cancelled;
            Document doc = uiDoc.Document;
            if (doc.IsFamilyDocument || doc.IsReadOnly)
            {
                TaskDialog.Show(Title, "请在可编辑的项目模型中使用。");
                return Result.Cancelled;
            }

            try
            {
                Reference sourceReference = uiDoc.Selection.PickObject(ObjectType.Face,
                    new PlanarModelFaceFilter(doc, sourceOnly: true), "点选要移动的梁面或板面（可按 Tab 切换）");
                Element source = doc.GetElement(sourceReference.ElementId);
                if (source.Pinned || source.GroupId != ElementId.InvalidElementId)
                {
                    TaskDialog.Show(Title, "所选梁或板已固定，或属于模型组。请选择可独立移动的构件。");
                    return Result.Cancelled;
                }
                Reference targetReference = uiDoc.Selection.PickObject(ObjectType.Face,
                    new PlanarModelFaceFilter(doc, excludedElement: source.Id), "点选另一构件上的目标模型平面");
                using var transaction = new Transaction(doc, Title);
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法开始梁板贴面。");

                var (sourceFace, targetFace) = ReadPlanes(doc, sourceReference, targetReference);
                XYZ targetOrigin = targetFace.Origin;
                XYZ targetNormal = targetFace.Normal;
                if (!AreParallel(sourceFace.Normal, targetNormal))
                {
                    TaskDialog.Show(Title, "两个面不平行。请选择与梁面或板面平行的目标面。");
                    return Result.Cancelled;
                }
                double distance = (targetOrigin - sourceFace.Origin).DotProduct(targetNormal);
                if (Math.Abs(distance) <= DistanceTolerance)
                {
                    TaskDialog.Show(Title, "两个面已齐平。");
                    return Result.Succeeded;
                }

                XYZ movement = distance * targetNormal;
                var confirm = new TaskDialog(Title)
                {
                    MainInstruction = $"平移所选{(source is Floor ? "板" : "梁")} {Math.Abs(distance) * 304.8:0.##} mm？",
                    MainContent = "构件会沿目标面的法向移动到目标平面。",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No
                };
                if (confirm.Show() != TaskDialogResult.Yes) return Result.Cancelled;

                Move(doc, source, movement);
                doc.Regenerate();

                var (movedFace, currentTarget) = ReadPlanes(doc, sourceReference, targetReference);
                if (!AreParallel(currentTarget.Normal, targetNormal) ||
                    Math.Abs((currentTarget.Origin - targetOrigin).DotProduct(targetNormal)) > DistanceTolerance ||
                    !AreParallel(movedFace.Normal, targetNormal) ||
                    Math.Abs((movedFace.Origin - targetOrigin).DotProduct(targetNormal)) > DistanceTolerance)
                    throw new InvalidOperationException("构件受连接或约束影响，移动后未能与原目标面齐平。");
                if (transaction.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit 未提交梁板贴面。");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = $"梁板贴面失败：{ex.Message}";
                return Result.Failed;
            }
        }

        private static bool AreParallel(XYZ left, XYZ right) =>
            left.CrossProduct(right).GetLength() <= NormalTolerance;

        private static ((XYZ Origin, XYZ Normal) Source, (XYZ Origin, XYZ Normal) Target) ReadPlanes(
            Document doc, Reference source, Reference target)
        {
            using var temporary = new SubTransaction(doc);
            if (temporary.Start() != TransactionStatus.Started)
                throw new InvalidOperationException("Revit 无法读取所选平面。");
            Plane sourcePlane = SketchPlane.Create(doc, source).GetPlane();
            Plane targetPlane = SketchPlane.Create(doc, target).GetPlane();
            var planes = ((sourcePlane.Origin, sourcePlane.Normal), (targetPlane.Origin, targetPlane.Normal));
            if (temporary.RollBack() != TransactionStatus.RolledBack)
                throw new InvalidOperationException("无法清理临时平面。");
            return planes;
        }

        private static void Move(Document doc, Element element, XYZ movement)
        {
            XYZ horizontal = new XYZ(movement.X, movement.Y, 0);
            if (horizontal.GetLength() > 1e-9)
                ElementTransformUtils.MoveElement(doc, element.Id, horizontal);
            if (Math.Abs(movement.Z) <= 1e-9) return;

            if (element is Floor)
            {
                SetOffset(element.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM), movement.Z);
            }
            else
            {
                var start = element.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION);
                var end = element.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION);
                double targetEnd = (end?.AsDouble() ?? 0) + movement.Z;
                SetOffset(start, movement.Z);
                if (end == null || end.IsReadOnly ||
                    Math.Abs(end.AsDouble() - targetEnd) > 1e-9 && !end.Set(targetEnd))
                    throw new InvalidOperationException("所选梁的结束标高偏移无法修改。");
            }
        }

        private static void SetOffset(Parameter? offset, double elevation)
        {
            if (offset == null || offset.IsReadOnly || !offset.Set(offset.AsDouble() + elevation))
                throw new InvalidOperationException("所选梁板的标高偏移无法修改。请检查宿主或连接约束。");
        }

        private sealed class PlanarModelFaceFilter : ISelectionFilter
        {
            private readonly Document _doc;
            private readonly bool _sourceOnly;
            private readonly ElementId? _excludedElement;

            public PlanarModelFaceFilter(Document doc, bool sourceOnly = false, ElementId? excludedElement = null)
            {
                _doc = doc;
                _sourceOnly = sourceOnly;
                _excludedElement = excludedElement;
            }

            public bool AllowElement(Element element) =>
                !element.ViewSpecific && !(element is RevitLinkInstance) && element.Id != _excludedElement &&
                (!_sourceOnly || element is Floor || element is FamilyInstance instance && instance.StructuralType == StructuralType.Beam);

            public bool AllowReference(Reference reference, XYZ position) =>
                _doc.GetElement(reference.ElementId)?.GetGeometryObjectFromReference(reference) is PlanarFace;
        }
    }
}
