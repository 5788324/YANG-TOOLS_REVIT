using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// Conservative geometry planner for view-specific straight detail lines.
    /// Planning never opens a transaction. Ambiguous geometry is rejected, not snapped indiscriminately.
    /// </summary>
    internal static class DetailLineCleanup
    {
        internal const double Tolerance = 0.1 / 304.8; // feet, 0.1 mm model space
        internal sealed class Segment
        {
            internal DetailCurve Element = null!;
            internal XYZ A = null!;
            internal XYZ B = null!;
            internal XYZ Axis = null!;
            internal ElementId Style = null!;
            internal double Length;
        }
        internal sealed class Plan
        {
            internal List<Segment> Members = new List<Segment>();
            internal Segment Keeper = null!;
            internal XYZ Start = null!;
            internal XYZ End = null!;
        }
        internal sealed class SelectionFilter : ISelectionFilter
        {
            private readonly ElementId viewId;
            internal SelectionFilter(ElementId id) { viewId = id; }
            public bool AllowElement(Element e) => e is DetailCurve c &&
                c.OwnerViewId.GetIdValue() == viewId.GetIdValue() &&
                c.GeometryCurve is Line && !c.Pinned &&
                c.GroupId.GetIdValue() == ElementId.InvalidElementId.GetIdValue();
            public bool AllowReference(Reference r, XYZ p) => false;
        }
        internal static List<Segment> GetSegments(Document doc, View view, IEnumerable<ElementId> ids)
        {
            var result = new List<Segment>();
            foreach (ElementId id in ids.Distinct())
            {
                if (!(doc.GetElement(id) is DetailCurve c) || !(c.GeometryCurve is Line line) ||
                    c.OwnerViewId.GetIdValue() != view.Id.GetIdValue() || c.Pinned ||
                    c.GroupId.GetIdValue() != ElementId.InvalidElementId.GetIdValue()) continue;
                XYZ a = line.GetEndPoint(0), b = line.GetEndPoint(1);
                double length = a.DistanceTo(b);
                if (length <= doc.Application.ShortCurveTolerance) continue;
                Element? style = c.LineStyle;
                if (style == null) continue;
                result.Add(new Segment { Element = c, A = a, B = b,
                    Axis = (b - a).Normalize(), Style = style.Id, Length = length });
            }
            return result.OrderBy(s => s.Element.Id.GetIdValue()).ToList();
        }
        // Perpendicular distance to an unbounded line; ensures angular drift is limited over entire segment.
        private static double DistanceToAxis(XYZ point, Segment axis) =>
            ((point - axis.A).CrossProduct(axis.Axis)).GetLength();
        private static bool Aligned(Segment a, Segment b) =>
            a.Style.GetIdValue() == b.Style.GetIdValue() &&
            DistanceToAxis(b.A, a) <= Tolerance && DistanceToAxis(b.B, a) <= Tolerance &&
            DistanceToAxis(a.A, b) <= Tolerance && DistanceToAxis(a.B, b) <= Tolerance;
        private static double Dot(XYZ p, Segment axis) => (p - axis.A).DotProduct(axis.Axis);
        private static void Interval(Segment item, Segment axis, out double lo, out double hi)
        {
            double x = Dot(item.A, axis), y = Dot(item.B, axis);
            lo = Math.Min(x, y); hi = Math.Max(x, y);
        }
        private static bool PositiveOverlap(Segment a, Segment b)
        {
            if (!Aligned(a, b)) return false;
            Interval(a, a, out double a0, out double a1);
            Interval(b, a, out double b0, out double b1);
            return Math.Min(a1, b1) - Math.Max(a0, b0) > Tolerance;
        }
        private static bool NearEndpoint(Segment a, Segment b) =>
            a.A.DistanceTo(b.A) <= Tolerance || a.A.DistanceTo(b.B) <= Tolerance ||
            a.B.DistanceTo(b.A) <= Tolerance || a.B.DistanceTo(b.B) <= Tolerance;

        // Selected off-axis segments may meet a collinear chain at its interior joint.
        // Check the finite segment, not its infinitely extended supporting line.
        private static bool TouchesFiniteSegment(XYZ point, Segment segment)
        {
            XYZ direction = segment.B - segment.A;
            double lengthSquared = direction.DotProduct(direction);
            if (lengthSquared <= 0) return false;
            double t = (point - segment.A).DotProduct(direction) / lengthSquared;
            double extension = Tolerance / segment.Length;
            if (t < -extension || t > 1 + extension) return false;
            t = Math.Max(0, Math.Min(1, t));
            return point.DistanceTo(segment.A + direction.Multiply(t)) <= Tolerance;
        }

        private static List<List<Segment>> Components(List<Segment> lines, Func<Segment, Segment, bool> edge)
        {
            int n = lines.Count;
            var roots = Enumerable.Range(0, n).ToArray();
            int Root(int k) { while (roots[k] != k) { roots[k] = roots[roots[k]]; k = roots[k]; } return k; }
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (edge(lines[i], lines[j])) roots[Root(j)] = Root(i);
            return Enumerable.Range(0, n).GroupBy(Root)
                .Select(group => group.Select(i => lines[i]).ToList()).Where(group => group.Count > 1).ToList();
        }
        private static bool TryUnion(List<Segment> members, out Plan plan)
        {
            plan = new Plan();
            Segment keeper = members.OrderByDescending(s => s.Length).ThenBy(s => s.Element.Id.GetIdValue()).First();
            // Non-transitive tolerance chains must be rejected as one ambiguous component.
            if (members.Any(s => !Aligned(keeper, s))) return false;
            var ts = members.SelectMany(s => new[] { Dot(s.A, keeper), Dot(s.B, keeper) }).ToList();
            double lo = ts.Min(), hi = ts.Max();
            XYZ a = keeper.A + keeper.Axis.Multiply(lo), b = keeper.A + keeper.Axis.Multiply(hi);
            if (a.DistanceTo(b) <= keeper.Element.Document.Application.ShortCurveTolerance) return false;
            plan.Members = members; plan.Keeper = keeper; plan.Start = a; plan.End = b;
            return true;
        }
        internal static List<Plan> OverlapPlans(List<Segment> lines, out int unsafeCount)
        {
            var found = new List<Plan>(); unsafeCount = 0;
            foreach (var group in Components(lines, PositiveOverlap))
            {
                if (!TryUnion(group, out Plan candidate)) { unsafeCount += group.Count; continue; }
                // Intervals in the component must form one continuously overlapping union.
                var intervals = group.Select(s => { Interval(s, candidate.Keeper, out double lo, out double hi); return (lo, hi); })
                    .OrderBy(p => p.lo).ToList();
                double far = intervals[0].hi;
                bool valid = true;
                for (int i = 1; i < intervals.Count; i++)
                {
                    if (intervals[i].lo >= far - Tolerance) { valid = false; break; }
                    far = Math.Max(far, intervals[i].hi);
                }
                if (valid) found.Add(candidate); else unsafeCount += group.Count;
            }
            return found;
        }
        internal static List<Plan> JoinPlans(List<Segment> lines, out int unsafeCount)
        {
            var found = new List<Plan>(); unsafeCount = 0;
            foreach (var group in Components(lines, (a, b) => Aligned(a, b) && NearEndpoint(a, b)))
            {
                if (!TryUnion(group, out Plan candidate)) { unsafeCount += group.Count; continue; }
                // A simple open chain has only two end vertices; any branch, cycle or
                // positive-length overlap is unsafe for the endpoint-only command.
                var vertices = new List<List<XYZ>>();
                var degrees = new List<int>();
                int Vertex(XYZ p)
                {
                    for (int i = 0; i < vertices.Count; i++)
                        if (vertices[i].All(q => q.DistanceTo(p) <= Tolerance))
                        { vertices[i].Add(p); return i; }
                    vertices.Add(new List<XYZ> { p }); degrees.Add(0); return vertices.Count - 1;
                }
                bool valid = true;
                foreach (Segment s in group)
                {
                    int a = Vertex(s.A), b = Vertex(s.B);
                    if (a == b) { valid = false; break; }
                    degrees[a]++; degrees[b]++;
                }
                if (valid && (degrees.Count(x => x == 1) != 2 || degrees.Any(x => x > 2))) valid = false;
                // Counting degrees inside the collinear component alone misses a perpendicular
                // branch that meets a joint. Consider all user-selected valid detail lines.
                if (valid)
                {
                    var memberIds = new HashSet<long>(group.Select(s => s.Element.Id.GetIdValue()));
                    foreach (int index in Enumerable.Range(0, degrees.Count).Where(i => degrees[i] == 2))
                    {
                        XYZ joint = vertices[index][0];
                        if (lines.Any(s => !memberIds.Contains(s.Element.Id.GetIdValue()) &&
                                           TouchesFiniteSegment(joint, s)))
                        { valid = false; break; }
                    }
                }
                if (valid)
                    for (int i = 0; i < group.Count && valid; i++)
                        for (int j = i + 1; j < group.Count; j++)
                            if (PositiveOverlap(group[i], group[j])) { valid = false; break; }
                // Additional coverage gate: no interval gaps larger than endpoint tolerance.
                if (valid)
                {
                    var intervals = group.Select(s => { Interval(s, candidate.Keeper, out double lo, out double hi); return (lo, hi); })
                        .OrderBy(p => p.lo).ToList();
                    double far = intervals[0].hi;
                    for (int i = 1; i < intervals.Count; i++)
                    {
                        if (intervals[i].lo > far + Tolerance || intervals[i].lo < far - Tolerance)
                        { valid = false; break; }
                        far = Math.Max(far, intervals[i].hi);
                    }
                }
                if (valid) found.Add(candidate); else unsafeCount += group.Count;
            }
            return found;
        }

        /// <summary>Dimension references and all API-dependent elements are guarded conservatively.</summary>
        internal static HashSet<long> ProtectedLineIds(Document doc, IEnumerable<Plan> plans)
        {
            var ids = new HashSet<long>(plans.SelectMany(p => p.Members).Select(s => s.Element.Id.GetIdValue()));
            var protectedIds = new HashSet<long>();
            // Enumerate document-wide: annotations outside active view may still reference these elements.
            foreach (Dimension dimension in new FilteredElementCollector(doc).OfClass(typeof(Dimension)).Cast<Dimension>())
            {
                ReferenceArray? refs = dimension.References;
                if (refs == null) continue;
                foreach (Reference r in refs)
                    if (r.ElementId != null && ids.Contains(r.ElementId.GetIdValue()))
                        protectedIds.Add(r.ElementId.GetIdValue());
            }
            foreach (Plan p in plans)
                foreach (Segment s in p.Members)
                {
                    long selfId = s.Element.Id.GetIdValue();
                    // Revit can return the element's own ID from GetDependentElements(null).
                    // This is not a separate dependent; every OTHER ID stays protected.
                    if (s.Element.GetDependentElements(null).Any(id => id.GetIdValue() != selfId))
                        protectedIds.Add(selfId);
                }
            return protectedIds;
        }
        internal static List<ElementId> Apply(Document doc, List<Plan> plans, string txName)
        {
            var survivors = new List<ElementId>();
            using (var tx = new Transaction(doc, txName))
            {
                if (tx.Start() != TransactionStatus.Started) throw new InvalidOperationException("事务无法启动。");
                try
                {
                    foreach (Plan plan in plans)
                    {
                        Segment keep = plan.Keeper;
                        // Snapshot IDs BEFORE editing or deleting any Revit element.
                        // Element.Id on a deleted element raises InvalidObjectException.
                        ElementId keeperId = keep.Element.Id;
                        List<ElementId> redundantIds = plan.Members
                            .Select(s => s.Element.Id)
                            .Where(id => id.GetIdValue() != keeperId.GetIdValue())
                            .Distinct()
                            .ToList();
                        if (redundantIds.Count != plan.Members.Count - 1)
                            throw new InvalidOperationException("线段成员 ID 重复，已取消删除并回滚。");
                        // Retain one original ElementId instead of deleting all source lines.
                        Line target = Line.CreateBound(plan.Start, plan.End);
                        Line old = (Line)keep.Element.GeometryCurve;
                        if (old.GetEndPoint(0).DistanceTo(plan.Start) > Tolerance / 10 ||
                            old.GetEndPoint(1).DistanceTo(plan.End) > Tolerance / 10)
                        {
                            keep.Element.SetGeometryCurve(target, false);
                            doc.Regenerate();
                            Line changed = keep.Element.GeometryCurve as Line ??
                                throw new InvalidOperationException("被保留直线几何失效。");
                            bool match = changed.GetEndPoint(0).DistanceTo(plan.Start) <= Tolerance &&
                                         changed.GetEndPoint(1).DistanceTo(plan.End) <= Tolerance;
                            bool reversed = changed.GetEndPoint(1).DistanceTo(plan.Start) <= Tolerance &&
                                            changed.GetEndPoint(0).DistanceTo(plan.End) <= Tolerance;
                            if (!match && !reversed) throw new InvalidOperationException("Revit 调整了目标线段，已回滚。");
                        }
                        foreach (ElementId redundantId in redundantIds)
                        {
                            // Use the captured ElementId after deletion, NEVER the invalidated Element.
                            ICollection<ElementId> removed = doc.Delete(redundantId);
                            if (!removed.Contains(redundantId) || removed.Count != 1)
                                throw new InvalidOperationException("删除附带影响了其他元素，已回滚整个事务。");
                        }
                        survivors.Add(keeperId);
                    }
                    if (tx.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("事务提交失败。");
                }
                catch { if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); throw; }
            }
            return survivors;
        }
    }
}
