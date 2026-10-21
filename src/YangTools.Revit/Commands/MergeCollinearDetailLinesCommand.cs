using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    /// <summary>Merge only selected straight DetailCurves in the current view, one Undo.</summary>
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("模型修改区", "合并直线", "合并同一视图、同一线样式、端点相接的共线详图短直线；单次撤销", "Icons/merge_lines_32.png", "Icons/merge_lines_16.png")]
    public sealed class MergeCollinearDetailLinesCommand : IExternalCommand
    {
        // 0.1 mm maximum endpoint mismatch. Not a global proximity cleanup.
        private const double JoinToleranceFeet = 0.1 / 304.8;
        private const double DirectionSinTolerance = 1e-5;
        private sealed class Segment
        {
            public DetailCurve Curve = null!;
            public XYZ A = null!;
            public XYZ B = null!;
            public XYZ Axis = null!;
            public ElementId StyleId = null!;
        }
        private sealed class Cluster
        {
            public List<Segment> Items = new List<Segment>();
            public XYZ Start = null!;
            public XYZ End = null!;
            public ElementId StyleId = null!;
        }

        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument? uiDoc = data.Application.ActiveUIDocument;
            if (uiDoc == null) return Result.Cancelled;
            Document doc = uiDoc.Document;
            View view = doc.ActiveView;
            try
            {
                List<ElementId> ids = uiDoc.Selection.GetElementIds().ToList();
                if (ids.Count < 2)
                {
                    ids = uiDoc.Selection.PickObjects(ObjectType.Element, new DetailLineFilter(view.Id),
                        "请选择至少两根同视图、相连的直线详图线").Select(r => r.ElementId).Distinct().ToList();
                }
                if (ids.Count < 2 || ids.Count > 600)
                {
                    TaskDialog.Show("合并直线", "请选择 2–600 根详图直线。曲线、模型线及组内对象不会被合并。");
                    return Result.Cancelled;
                }
                var segments = new List<Segment>();
                foreach (ElementId id in ids)
                {
                    if (!(doc.GetElement(id) is DetailCurve curve) || !(curve.GeometryCurve is Line line)
                        || curve.OwnerViewId.GetIdValue() != view.Id.GetIdValue() || curve.Pinned || curve.GroupId.GetIdValue() != ElementId.InvalidElementId.GetIdValue())
                        continue;
                    Element? style = curve.LineStyle;
                    if (style == null) continue;
                    XYZ a = line.GetEndPoint(0), b = line.GetEndPoint(1);
                    if (a.DistanceTo(b) < doc.Application.ShortCurveTolerance) continue;
                    segments.Add(new Segment { Curve = curve, A = a, B = b,
                        Axis = (b - a).Normalize(), StyleId = style.Id });
                }
                if (segments.Count < 2) { TaskDialog.Show("合并直线", "选择中不足两根符合要求的直线详图线。 "); return Result.Cancelled; }
                // Components only link near-coincident endpoints, never join through a gap or an intersection.
                int n = segments.Count;
                int[] parents = Enumerable.Range(0, n).ToArray();
                int Root(int i) { while (parents[i] != i) { parents[i] = parents[parents[i]]; i = parents[i]; } return i; }
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                        if (Compatible(segments[i], segments[j])) parents[Root(j)] = Root(i);
                var candidates = new List<Cluster>();
                foreach (var component in Enumerable.Range(0, n).GroupBy(Root))
                {
                    List<Segment> group = component.Select(i => segments[i]).ToList();
                    if (group.Count < 2) continue;
                    XYZ baseAxis = group[0].Axis;
                    XYZ basePoint = group[0].A;
                    var ends = group.SelectMany(s => new[] { s.A, s.B })
                        .Select(point => new { Point = point, T = (point - basePoint).DotProduct(baseAxis) }).ToList();
                    var first = ends.OrderBy(x => x.T).First();
                    var last = ends.OrderByDescending(x => x.T).First();
                    if (first.Point.DistanceTo(last.Point) < doc.Application.ShortCurveTolerance) continue;
                    // All lines must be collinear with the overall spanning line; no corners/branching.
                    XYZ direction = (last.Point - first.Point).Normalize();
                    if (group.Any(s => (s.A - first.Point).CrossProduct(direction).GetLength() > JoinToleranceFeet ||
                                       (s.B - first.Point).CrossProduct(direction).GetLength() > JoinToleranceFeet)) continue;
                    candidates.Add(new Cluster { Items = group, Start = first.Point, End = last.Point, StyleId = group[0].StyleId });
                }
                int replacedCount = candidates.Sum(g => g.Items.Count);
                if (replacedCount < 2)
                {
                    TaskDialog.Show("合并直线", "没有找到端点连接、同线型的共线直线组；未修改模型。");
                    return Result.Cancelled;
                }
                var preview = new TaskDialog("合并直线预览")
                {
                    MainInstruction = $"将 {replacedCount} 根短直线合并为 {candidates.Count} 根直线？",
                    MainContent = "仅操作当前视图中选定、共线、端点相接且线样式一致的详图直线。删除原线可能影响引用它们的标注；请在测试图中核对。一次 Ctrl+Z 撤销全部。",
                    CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel
                };
                if (preview.Show() != TaskDialogResult.Ok) return Result.Cancelled;
                if (!data.Application.ActiveUIDocument.Document.Equals(doc) || doc.ActiveView.Id.GetIdValue() != view.Id.GetIdValue() || doc.IsReadOnly)
                    return Result.Cancelled;
                var newIds = new List<ElementId>();
                using (var transaction = new Transaction(doc, "合并共线详图线"))
                {
                    if (transaction.Start() != TransactionStatus.Started) return Result.Failed;
                    try
                    {
                        foreach (Cluster group in candidates)
                        {
                            var newCurve = doc.Create.NewDetailCurve(view, Line.CreateBound(group.Start, group.End));
                            newCurve.LineStyle = doc.GetElement(group.StyleId);
                            newIds.Add(newCurve.Id);
                            foreach (Segment segment in group.Items)
                            {
                                var deleted = doc.Delete(segment.Curve.Id);
                                if (!deleted.Contains(segment.Curve.Id))
                                    throw new InvalidOperationException("无法删除旧直线，已回滚。 ");
                            }
                        }
                        if (transaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("线条合并提交失败。 ");
                    }
                    catch { if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack(); throw; }
                }
                try { uiDoc.Selection.SetElementIds(newIds); }
                catch (Exception ex) { DiagnosticLog.WriteError("线条合并后选中结果", ex); }
                TaskDialog.Show("完成", $"已将 {replacedCount} 根短直线合并为 {newIds.Count} 根，单次 Ctrl+Z 撤销。");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                message = ex.ToString();
                DiagnosticLog.WriteError("合并直线", ex);
                TaskDialog.Show("合并直线失败", ex.Message + "\n事务失败时已尝试回滚。");
                return Result.Failed;
            }
        }

        private static bool Compatible(Segment a, Segment b)
        {
            if (a.StyleId.GetIdValue() != b.StyleId.GetIdValue()) return false;
            if (a.Axis.CrossProduct(b.Axis).GetLength() > DirectionSinTolerance) return false;
            if ((b.A - a.A).CrossProduct(a.Axis).GetLength() > JoinToleranceFeet) return false;
            return a.A.DistanceTo(b.A) <= JoinToleranceFeet || a.A.DistanceTo(b.B) <= JoinToleranceFeet ||
                   a.B.DistanceTo(b.A) <= JoinToleranceFeet || a.B.DistanceTo(b.B) <= JoinToleranceFeet;
        }
        private sealed class DetailLineFilter : ISelectionFilter
        {
            private readonly ElementId _view;
            public DetailLineFilter(ElementId view) { _view = view; }
            public bool AllowElement(Element e) => e is DetailCurve c && c.OwnerViewId.GetIdValue() == _view.GetIdValue() && c.GeometryCurve is Line && !c.Pinned;
            public bool AllowReference(Reference r, XYZ p) => false;
        }
    }
}
