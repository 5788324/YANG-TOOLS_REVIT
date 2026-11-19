using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Commands;

namespace YangTools.Revit.Core
{
    internal static class DwgViewLinkMigration
    {
        private const double PositionTolerance = 0.01 / 304.8; // 0.01 mm
        private const double BasisTolerance = 1e-8;

        internal sealed class Plan
        {
            public ImportInstance Source { get; }
            public string Name { get; }
            public string Path { get; set; } = "";
            public List<View> Views { get; } = new List<View>();
            public List<string> Blockers { get; } = new List<string>();
            public bool CanConvert => Blockers.Count == 0 && Views.Count > 0;

            public Plan(ImportInstance source)
            {
                Source = source;
                Name = $"{source.Name}（ID {source.Id.GetIdValue()}）";
            }
        }

        internal static List<Plan> Scan(Document doc, ICollection<ElementId> selectedIds)
        {
            var selected = new HashSet<ElementId>(selectedIds);
            var plans = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .Where(link => link.IsLinked && !link.ViewSpecific && (selected.Count == 0 || selected.Contains(link.Id)))
                .Where(link => doc.GetElement(link.GetTypeId()) is CADLinkType)
                .Select(link => new Plan(link)).ToDictionary(plan => plan.Source.Id);

            foreach (Plan plan in plans.Values.ToList())
            {
                var linkType = (CADLinkType)doc.GetElement(plan.Source.GetTypeId());
                try
                {
                    using var reference = linkType.GetExternalFileReference();
                    var absolutePath = reference?.GetAbsolutePath();
                    if (absolutePath != null)
                        plan.Path = ModelPathUtils.ConvertModelPathToUserVisiblePath(absolutePath);

                    if (plan.Path.Length > 0 && !Path.GetExtension(plan.Path).Equals(".dwg", StringComparison.OrdinalIgnoreCase))
                    {
                        plans.Remove(plan.Source.Id);
                        continue;
                    }

                    if (reference?.GetLinkedFileStatus() == LinkedFileStatus.Unloaded)
                        plan.Blockers.Add("DWG 已卸载。");
                    else if (!File.Exists(plan.Path))
                        plan.Blockers.Add("无法读取源 DWG 路径或文件不存在。");
                    else if (plan.Source.Category == null)
                        plan.Blockers.Add("无法读取 CAD 图层类别。");
                    else if (plan.Source.GroupId != ElementId.InvalidElementId || plan.Source.DesignOption != null)
                        plan.Blockers.Add("属于模型组或设计选项。");
                }
                catch (Exception ex)
                {
                    plan.Blockers.Add("无法解析源路径：" + ex.Message);
                }
            }

            if (plans.Count == 0) return new List<Plan>();
            var dimensionLinks = new HashSet<ElementId>();
            foreach (Dimension dimension in new FilteredElementCollector(doc).OfClass(typeof(Dimension)).Cast<Dimension>())
            {
                var references = dimension.References;
                if (references == null) continue;
                foreach (Reference reference in references)
                    if (plans.ContainsKey(reference.ElementId)) dimensionLinks.Add(reference.ElementId);
            }
            foreach (ElementId id in dimensionLinks)
                plans[id].Blockers.Add("尺寸或对齐引用了旧链接。");

            var visibleById = plans.Where(pair => pair.Value.Blockers.Count == 0)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            var views = visibleById.Count == 0 ? new List<View>()
                : new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                    .Where(view => !view.IsTemplate && FilteredElementCollector.IsViewValidForElementIteration(doc, view.Id))
                    .OrderBy(view => view.Name).ToList();
            foreach (View view in views)
            {
                if (view.IsInTemporaryViewMode(TemporaryViewMode.TemporaryViewProperties))
                {
                    foreach (Plan plan in visibleById.Values)
                        plan.Blockers.Add($"「{view.Name}」启用了临时视图属性，请退出该模式后重新扫描。");
                    continue;
                }

                if (view.IsTemporaryHideIsolateActive())
                {
                    foreach (Plan plan in visibleById.Values)
                        if (!IsExplicitlyHidden(doc, view, plan.Source))
                            plan.Blockers.Add($"「{view.Name}」启用了临时隐藏或隔离，请退出该模式后重新扫描。");
                    continue;
                }

                foreach (ElementId id in new FilteredElementCollector(doc, view.Id).OfClass(typeof(ImportInstance)).ToElementIds())
                {
                    if (!visibleById.TryGetValue(id, out Plan? plan) || IsExplicitlyHidden(doc, view, plan.Source))
                        continue;

                    if (!SupportsReplacement(view))
                    {
                        plan.Blockers.Add($"「{view.Name}」需要保留链接，但该视图类型不支持本次转换。");
                        continue;
                    }

                    int crop = ClassifyCrop(view, plan.Source);
                    if (crop < 0) continue; // Entire bounding box is outside this rectangular crop.
                    if (crop == 0)
                        plan.Blockers.Add($"「{view.Name}」的裁剪与 DWG 相交或无法可靠核对。");
                    else if (view.GetPrimaryViewId() != ElementId.InvalidElementId || view.GetDependentViewIds().Count > 0)
                        plan.Blockers.Add($"「{view.Name}」属于主视图与相关视图组合，转换可能改变共享注释的显示。");
                    else
                        plan.Views.Add(view);
                }
            }

            foreach (Plan plan in plans.Values)
                if (plan.Views.Count == 0 && plan.Blockers.Count == 0)
                    plan.Blockers.Add("没有找到可保留链接的目标视图。");
            return plans.Values.OrderBy(plan => plan.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private static bool IsExplicitlyHidden(Document doc, View view, ImportInstance link)
        {
            if (link.IsHidden(view)) return true;
            if (doc.IsWorkshared && !view.IsWorksetVisible(link.WorksetId)) return true;
            return view.AreGraphicsOverridesAllowed() &&
                (view.AreImportCategoriesHidden || view.GetCategoryHidden(link.Category.Id));
        }

        private static bool SupportsReplacement(View view) =>
            view.AreGraphicsOverridesAllowed() && view.CanBePrinted &&
            (view is ViewPlan || view is ViewSection || view.ViewType == ViewType.DraftingView);

        // 1: wholly inside model crop (or uncropped), -1: wholly outside, 0: uncertain.
        private static int ClassifyCrop(View view, ImportInstance link)
        {
            if (!view.CropBoxActive) return 1;
            using var manager = view.GetCropRegionShapeManager();
            if (manager.ShapeSet || manager.Split) return 0;
            var box = link.get_BoundingBox(view);
            var crop = view.CropBox;
            if (box == null || crop == null) return 0;

            Transform toCrop = crop.Transform.Inverse.Multiply(box.Transform);
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            foreach (double x in new[] { box.Min.X, box.Max.X })
            foreach (double y in new[] { box.Min.Y, box.Max.Y })
            foreach (double z in new[] { box.Min.Z, box.Max.Z })
            {
                XYZ point = toCrop.OfPoint(new XYZ(x, y, z));
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y);
                maxY = Math.Max(maxY, point.Y);
            }

            if (maxX < crop.Min.X || minX > crop.Max.X || maxY < crop.Min.Y || minY > crop.Max.Y)
                return -1;
            return minX >= crop.Min.X && maxX <= crop.Max.X &&
                   minY >= crop.Min.Y && maxY <= crop.Max.Y ? 1 : 0;
        }

        internal static int Convert(Document doc, Plan plan)
        {
            var source = plan.Source;
            Transform original = source.GetTotalTransform();
            var newIds = new Dictionary<ElementId, ElementId>();
            using var transaction = new Transaction(doc, "DWG 转视图专用链接");
            if (transaction.Start() != TransactionStatus.Started)
                throw new InvalidOperationException("无法开始转换事务。");

            foreach (View view in plan.Views)
            {
                using var options = new DWGImportOptions
                {
                    ThisViewOnly = true,
                    Placement = ImportPlacement.Origin,
                    OrientToView = false,
                    Unit = ImportUnit.Default
                };
                if (!doc.Link(plan.Path, options, view, out ElementId newId) ||
                    !(doc.GetElement(newId) is ImportInstance replacement) ||
                    newId == source.Id || !replacement.IsLinked || !replacement.ViewSpecific || replacement.OwnerViewId != view.Id)
                    throw new InvalidOperationException($"「{view.Name}」未创建正确的视图专用链接。");

                // Reuse the same link type/category so existing per-view CAD layer settings remain effective.
                if (replacement.GetTypeId() != source.GetTypeId() || replacement.Category?.Id != source.Category.Id)
                    throw new InvalidOperationException($"「{view.Name}」重建后使用了不同的 CAD 图层类别，无法保留原图层设置。");

                replacement.Pinned = false;
                MatchPlacement(doc, view, replacement, original);
                replacement.Pinned = source.Pinned;
                using var overrides = view.GetElementOverrides(source.Id);
                if (overrides.HasOverrides())
                    view.SetElementOverrides(newId, overrides);
                newIds.Add(view.Id, newId);
            }

            var deleted = doc.Delete(source.Id);
            if (deleted.Count != 1 || !deleted.Contains(source.Id))
                throw new InvalidOperationException("移除旧链接会影响其他图元，已取消该 DWG 的转换。");

            doc.Regenerate();
            foreach (View view in plan.Views)
            {
                ElementId id = newIds[view.Id];
                if (!(doc.GetElement(id) is ImportInstance current) || current.OwnerViewId != view.Id ||
                    IsExplicitlyHidden(doc, view, current) ||
                    !new FilteredElementCollector(doc, view.Id).OfClass(typeof(ImportInstance)).ToElementIds().Contains(id) ||
                    !SamePlacement(original, current.GetTotalTransform()))
                    throw new InvalidOperationException($"「{view.Name}」的最终位置或可见性核对失败。");
            }

            if (transaction.Commit() != TransactionStatus.Committed)
                throw new InvalidOperationException("转换事务未提交。");
            return newIds.Count;
        }

        private static void MatchPlacement(Document doc, View view, ImportInstance link, Transform target)
        {
            var current = link.GetTotalTransform();
            XYZ normal = view.ViewDirection.Normalize();
            XYZ from = current.BasisX - normal * current.BasisX.DotProduct(normal);
            XYZ to = target.BasisX - normal * target.BasisX.DotProduct(normal);
            if (from.GetLength() > BasisTolerance && to.GetLength() > BasisTolerance)
            {
                double angle = Math.Atan2(normal.DotProduct(from.CrossProduct(to)), from.DotProduct(to));
                if (Math.Abs(angle) > BasisTolerance)
                {
                    var axis = Line.CreateUnbound(current.Origin, normal);
                    ElementTransformUtils.RotateElement(doc, link.Id, axis, angle);
                    doc.Regenerate();
                    current = link.GetTotalTransform();
                }
            }

            XYZ move = target.Origin - current.Origin;
            if (move.GetLength() > PositionTolerance)
            {
                ElementTransformUtils.MoveElement(doc, link.Id, move);
                doc.Regenerate();
            }

            if (!SamePlacement(target, link.GetTotalTransform()))
                throw new InvalidOperationException($"「{view.Name}」无法匹配原链接的位置、旋转或比例。");
        }

        private static bool SamePlacement(Transform a, Transform b) =>
            a.Origin.DistanceTo(b.Origin) <= PositionTolerance &&
            a.BasisX.DistanceTo(b.BasisX) <= BasisTolerance &&
            a.BasisY.DistanceTo(b.BasisY) <= BasisTolerance &&
            a.BasisZ.DistanceTo(b.BasisZ) <= BasisTolerance;
    }
}
