using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(panelName: "模型修改区", buttonText: "梁板贴面",
        tooltip: "把板的现有形状点或直梁定位线两端沿 Z 对齐到选定模型面。",
        largeIcon: "Icons/align_beam_floor_face_32.png", smallIcon: "Icons/align_beam_floor_face_16.png",
        usage: "使用：在正交三维视图选梁或板，再点选目标面。\n注意：板需已启用形状编辑。未投中的点保留原标高。")]
    public sealed class AlignBeamFloorToFaceCommand : IExternalCommand
    {
        private const string Title = "梁板贴面";
        private const double Tolerance = 0.1 / 304.8;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument? uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null) return Result.Cancelled;
            Document doc = uiDoc.Document;
            if (doc.IsFamilyDocument || doc.IsReadOnly || !(uiDoc.ActiveView is View3D view) || view.IsPerspective)
            {
                TaskDialog.Show(Title, "请在可编辑的项目中打开正交三维视图。");
                return Result.Cancelled;
            }

            try
            {
                var sourceFilter = new SelectionFilter();
                var selected = uiDoc.Selection.GetElementIds().Select(doc.GetElement).OfType<Element>()
                    .Where(sourceFilter.AllowElement).Take(2).ToList();
                Element source = selected.Count == 1 ? selected[0] :
                    doc.GetElement(uiDoc.Selection.PickObject(ObjectType.Element, sourceFilter, "选择一根直梁或一块板"));
                if (source.Pinned || source.GroupId != ElementId.InvalidElementId)
                    throw new InvalidOperationException("梁板已固定或属于模型组，无法单独修改。");

                Reference target = uiDoc.Selection.PickObject(ObjectType.Face, new SelectionFilter(source.Id),
                    "选择要贴合的目标面");
                string stableFace = target.ConvertToStableRepresentation(doc);
                var box = doc.GetElement(target.ElementId).get_BoundingBox(null)
                    ?? throw new InvalidOperationException("无法取得目标面的模型范围。");
                double top = box.Max.Z + 1;
                using var intersector = new ReferenceIntersector(target.ElementId, FindReferenceTarget.Face, view);
                XYZ? Project(XYZ point)
                {
                    var origin = new XYZ(point.X, point.Y, top);
                    double? elevation = intersector.Find(origin, -XYZ.BasisZ)
                        .Where(hit => hit.GetReference().ConvertToStableRepresentation(doc) == stableFace)
                        .Select(hit => (double?)(top - hit.Proximity))
                        .OrderBy(z => Math.Abs(z.GetValueOrDefault() - point.Z)).FirstOrDefault();
                    return elevation.HasValue ? new XYZ(point.X, point.Y, elevation.Value) : null;
                }

                SlabShapeEditor? editor = null;
                List<SlabShapeVertex>? vertices = null;
                XYZ[] original;
                if (source is Floor floor)
                {
#if REVIT2024_OR_GREATER
                    editor = floor.GetSlabShapeEditor();
#else
                    editor = floor.SlabShapeEditor;
#endif
                    if (editor == null) throw new InvalidOperationException("此板无法编辑形状点。");
                    if (!editor.IsEnabled)
                        throw new InvalidOperationException("此板尚无可编辑的形状点，请先在 Revit 中完成形状编辑。");
                    vertices = editor.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
                    original = vertices.Select(vertex => vertex.Position).ToArray();
                }
                else
                {
                    var line = (Line)((LocationCurve)source.Location).Curve;
                    original = new[] { line.GetEndPoint(0), line.GetEndPoint(1) };
                }

                var projected = original.Select(Project).ToArray();
                XYZ[] result = projected.Select((point, index) => point ?? original[index]).ToArray();
                var changes = Enumerable.Range(0, original.Length)
                    .Where(i => original[i].DistanceTo(result[i]) > Tolerance).ToList();
                int missed = projected.Count(point => point == null);
                if (changes.Count == 0)
                {
                    TaskDialog.Show(Title, $"无需移动。{missed} 个点未投到所选面。");
                    return Result.Cancelled;
                }

                using var tx = new Transaction(doc, Title);
                if (tx.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动修改事务。");

                if (source is Floor)
                {
                    var level = (Level)doc.GetElement(source.LevelId);
                    double baseZ = level.ProjectElevation +
                        source.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM).AsDouble();
                    foreach (int i in changes)
                        editor!.ModifySubElement(vertices![i], result[i].Z - baseZ);
                }
                else
                {
                    var start = source.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION);
                    var end = source.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION);
                    SetBeamOffset(start, result[0].Z - original[0].Z);
                    SetBeamOffset(end, result[1].Z - original[1].Z);
                }

                doc.Regenerate();
                bool matches;
                if (editor != null)
                {
                    var actual = editor.SlabShapeVertices.Cast<SlabShapeVertex>().Select(vertex => vertex.Position).ToList();
                    matches = actual.Count == result.Length &&
                        result.All(point => actual.Any(candidate => candidate.DistanceTo(point) <= Tolerance));
                }
                else
                {
                    var curve = ((LocationCurve)source.Location).Curve;
                    matches = curve.GetEndPoint(0).DistanceTo(result[0]) <= Tolerance &&
                              curve.GetEndPoint(1).DistanceTo(result[1]) <= Tolerance;
                }
                if (!matches)
                    throw new InvalidOperationException("Revit 的修改结果与目标点不符。请检查梁端约束或板的形状编辑状态。");
                if (tx.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit 未提交点标高修改。");
                if (missed > 0)
                    TaskDialog.Show(Title, $"已更新 {changes.Count} 个点。{missed} 个点未投到所选面，保留原标高。");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                message = $"{Title}失败：{ex.Message}";
                return Result.Failed;
            }
        }

        private static void SetBeamOffset(Parameter? parameter, double delta)
        {
            if (Math.Abs(delta) <= Tolerance) return;
            if (parameter == null || parameter.IsReadOnly || !parameter.Set(parameter.AsDouble() + delta))
                throw new InvalidOperationException("梁的端点标高受约束，无法修改。");
        }

        private sealed class SelectionFilter : ISelectionFilter
        {
            private readonly ElementId? _source;

            public SelectionFilter(ElementId? source = null) => _source = source;

            public bool AllowElement(Element element) => _source == null
                ? element is Floor || element is FamilyInstance beam && beam.StructuralType == StructuralType.Beam &&
                    beam.Location is LocationCurve location && location.Curve is Line
                : !element.ViewSpecific && element.Id != _source && !(element is RevitLinkInstance);

            public bool AllowReference(Reference reference, XYZ position) => _source != null;
        }
    }
}
