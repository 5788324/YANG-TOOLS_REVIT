using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    /// <summary>Deduplicate selected overlapping straight detail lines without changing their union.</summary>
    [Transaction(TransactionMode.Manual)]
    [RibbonButton("模型修改区", "清理重叠线段", "仅清理选中、同视图同线样式的重叠详图直线；保留总覆盖范围，支持一次撤销", "Icons/merge_lines_32.png", "Icons/merge_lines_16.png")]
    public sealed class RemoveOverlappingDetailLinesCommand : IExternalCommand
    {
        private const double Tolerance = 0.1 / 304.8;
        private const double AngleTolerance = 1e-7;
        private sealed class Segment
        {
            public DetailCurve Item = null!;
            public XYZ Start = null!;
            public XYZ End = null!;
            public XYZ Axis = null!;
            public ElementId StyleId = null!;
            public double A, B;
        }
        private sealed class Replacement
        {
            public List<Segment> Sources = new List<Segment>();
            public XYZ Start = null!;
            public XYZ End = null!;
            public ElementId Style = null!;
        }
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            UIDocument? uidoc = data.Application.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;
            try
            {
                var ids = uidoc.Selection.GetElementIds();
                if (ids.Count < 2 || ids.Count > 1500)
                {
                    TaskDialog.Show("清理重叠线段", "请先选中 2–1500 条当前视图的详图直线。模型线、弧线、锁定及组内对象不会处理。");
                    return Result.Cancelled;
                }
                var segments = new List<Segment>();
                foreach (ElementId id in ids)
                {
                    if (!(doc.GetElement(id) is DetailCurve element) || !(element.GeometryCurve is Line line) ||
                        element.Pinned || element.OwnerViewId.GetIdValue() != view.Id.GetIdValue() ||
                        element.GroupId.GetIdValue() != ElementId.InvalidElementId.GetIdValue()) continue;
                    XYZ a = line.GetEndPoint(0), b = line.GetEndPoint(1);
                    if (a.DistanceTo(b) <= doc.Application.ShortCurveTolerance) continue;
                    Element? style = element.LineStyle;
                    if (style == null) continue;
                    segments.Add(new Segment { Item = element, Start = a, End = b,
                        Axis = (b-a).Normalize(), StyleId = style.Id });
                }
                if (segments.Count < 2) return Result.Cancelled;
                var replacements = new List<Replacement>();
                // Partition by geometry and line-style; connect only intervals with *strict positive overlap*.
                // Mere endpoint contact, crossing, parallel displacement and different styles remain unchanged.
                var visited = new HashSet<int>();
                for (int i = 0; i < segments.Count; i++)
                {
                    if (visited.Contains(i)) continue;
                    var seed = segments[i];
                    var indices = new List<int>();
                    for (int j = i; j < segments.Count; j++)
                    {
                        if (visited.Contains(j)) continue;
                        Segment candidate = segments[j];
                        if (candidate.StyleId.GetIdValue() != seed.StyleId.GetIdValue() ||
                            seed.Axis.CrossProduct(candidate.Axis).GetLength() > AngleTolerance ||
                            (candidate.Start-seed.Start).CrossProduct(seed.Axis).GetLength() > Tolerance ||
                            (candidate.End-seed.Start).CrossProduct(seed.Axis).GetLength() > Tolerance)
                            continue;
                        candidate.A = (candidate.Start-seed.Start).DotProduct(seed.Axis);
                        candidate.B = (candidate.End-seed.Start).DotProduct(seed.Axis);
                        if (candidate.A > candidate.B) { double temp = candidate.A; candidate.A = candidate.B; candidate.B = temp; }
                        indices.Add(j);
                    }
                    indices.Sort((a,b) => segments[a].A.CompareTo(segments[b].A));
                    // Consecutive overlapping intervals form one union. A and B always refer to seed axis.
                    int k = 0;
                    while (k < indices.Count)
                    {
                        var component = new List<Segment> { segments[indices[k]] };
                        double start = component[0].A, end = component[0].B;
                        int next = k + 1;
                        bool overlapping = false;
                        while (next < indices.Count && segments[indices[next]].A < end - Tolerance)
                        {
                            var seg = segments[indices[next]];
                            overlapping = true;
                            end = Math.Max(end, seg.B);
                            component.Add(seg);
                            next++;
                        }
                        if (overlapping && component.Count > 1)
                        {
                            replacements.Add(new Replacement { Sources = component,
                                Start = seed.Start + seed.Axis.Multiply(start),
                                End = seed.Start + seed.Axis.Multiply(end), Style = seed.StyleId });
                        }
                        foreach (var segment in component)
                            visited.Add(segments.IndexOf(segment));
                        k = next;
                    }
                }
                // Fail closed if the same source was accidentally assigned to multiple replacement groups.
                if (replacements.SelectMany(x => x.Sources).Select(x => x.Item.Id.GetIdValue()).Distinct().Count() !=
                    replacements.Sum(x => x.Sources.Count))
                    throw new InvalidOperationException("线段聚类不唯一；未修改模型。");
                if (replacements.Count == 0)
                {
                    TaskDialog.Show("清理重叠线段", "未发现同线样式、共线且有正长度重叠的选中直线；未删除任何元素。");
                    return Result.Cancelled;
                }
                int sourceCount = replacements.Sum(x => x.Sources.Count);
                var preview = new TaskDialog("清理重叠线段预览") {
                    MainInstruction = $"将 {sourceCount} 条互相重叠的详图线整理成 {replacements.Count} 条？",
                    MainContent = "保持覆盖范围，不处理单纯端点相接、交叉、弧线或不同线样式。旧 ElementId 将删除，依赖旧线的标注引用可能失效；请先在测试图核对。一次 Ctrl+Z 撤销。",
                    CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel
                };
                if (preview.Show() != TaskDialogResult.Ok) return Result.Cancelled;
                if (data.Application.ActiveUIDocument?.Document == null ||
                    !doc.Equals(data.Application.ActiveUIDocument.Document) ||
                    doc.ActiveView.Id.GetIdValue() != view.Id.GetIdValue() || doc.IsReadOnly)
                    return Result.Cancelled;
                var newIds = new List<ElementId>();
                using (var transaction = new Transaction(doc, "清理重叠详图直线"))
                {
                    if (transaction.Start() != TransactionStatus.Started) return Result.Failed;
                    try
                    {
                        foreach (var group in replacements)
                        {
                            if (group.Start.DistanceTo(group.End) < doc.Application.ShortCurveTolerance)
                                throw new InvalidOperationException("合并后线段低于 Revit 最短曲线限制。");
                            DetailCurve line = doc.Create.NewDetailCurve(view, Line.CreateBound(group.Start, group.End));
                            line.LineStyle = doc.GetElement(group.Style);
                            newIds.Add(line.Id);
                            foreach (var source in group.Sources) doc.Delete(source.Item.Id);
                        }
                        if (transaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("重叠线清理事务未提交。");
                    }
                    catch { if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack(); throw; }
                }
                try { uidoc.Selection.SetElementIds(newIds); } catch { }
                TaskDialog.Show("清理重叠线段", $"已将 {sourceCount} 条线整理为 {newIds.Count} 条，可一次 Ctrl+Z 撤销。");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                DiagnosticLog.WriteError("清理重叠线段", ex);
                return Result.Failed;
            }
        }
    }
}
