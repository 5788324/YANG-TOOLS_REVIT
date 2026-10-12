using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.Core.ProjectAssets
{
    /// <summary>
    /// Typed, read-only request shared by WPF and potential MCP adapters.
    /// Execute only in a valid Revit API context (IExternalCommand or ExternalEvent).
    /// </summary>
    internal sealed class ProjectAssetFilterUsageRequest
    {
        public IReadOnlyCollection<ElementId> FilterIds { get; set; } = Array.Empty<ElementId>();
        public bool IncludeElementMatches { get; set; }
    }

    internal sealed class ProjectAssetFilterUsageItem
    {
        public bool ControlsInstances { get; set; }
        public bool UsedInViews { get; set; }
        public bool UsedInTemplates { get; set; }
    }

    internal sealed class ProjectAssetFilterUsageResult
    {
        private readonly Dictionary<long, ProjectAssetFilterUsageItem> _items =
            new Dictionary<long, ProjectAssetFilterUsageItem>();

        public ProjectAssetFilterUsageItem Get(ElementId id)
        {
            if (id != null && _items.TryGetValue(id.GetIdValue(), out var item))
                return item;
            return new ProjectAssetFilterUsageItem();
        }

        internal void Add(ElementId id, ProjectAssetFilterUsageItem item)
        {
            _items[id.GetIdValue()] = item;
        }
    }

    /// <summary>
    /// Builds a fresh, per-request view/template filter index instead of keeping
    /// Revit View objects alive across modeless UI events. No transaction or UI dependency.
    /// </summary>
    internal static class ProjectAssetFilterUsageService
    {
        internal static ProjectAssetFilterUsageResult Execute(
            Document document, ProjectAssetFilterUsageRequest request)
        {
            if (document == null || !document.IsValidObject)
                throw new ArgumentException("项目文档已关闭，无法分析过滤器。", nameof(document));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var result = new ProjectAssetFilterUsageResult();
            var filters = (request.FilterIds ?? Array.Empty<ElementId>())
                .Where(id => id != null)
                .GroupBy(id => id.GetIdValue())
                .Select(group => group.First())
                .ToList();
            if (filters.Count == 0) return result;

            var usedInViews = new HashSet<long>();
            var usedInTemplates = new HashSet<long>();
            foreach (View view in new FilteredElementCollector(document)
                .OfClass(typeof(View)).Cast<View>())
            {
                try
                {
                    if (view == null || !view.AreGraphicsOverridesAllowed()) continue;
                    var target = view.IsTemplate ? usedInTemplates : usedInViews;
                    foreach (var filterId in view.GetFilters())
                        target.Add(filterId.GetIdValue());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[YangTools] 读取视图过滤器失败: " + ex.Message);
                }
            }

            foreach (var id in filters)
            {
                var filter = document.GetElement(id) as ParameterFilterElement;
                if (filter == null) continue;

                result.Add(id, new ProjectAssetFilterUsageItem
                {
                    ControlsInstances = request.IncludeElementMatches && HasMatchingInstances(document, filter),
                    UsedInViews = usedInViews.Contains(id.GetIdValue()),
                    UsedInTemplates = usedInTemplates.Contains(id.GetIdValue())
                });
            }
            return result;
        }

        private static bool HasMatchingInstances(Document document, ParameterFilterElement filter)
        {
            try
            {
                var categoryIds = filter.GetCategories();
                if (categoryIds == null || categoryIds.Count == 0) return false;
                var collector = new FilteredElementCollector(document)
                    .WhereElementIsNotElementType()
                    .WherePasses(new ElementMulticategoryFilter(categoryIds));
                var elementFilter = filter.GetElementFilter();
                if (elementFilter != null) collector = collector.WherePasses(elementFilter);
                return collector.GetElementCount() > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[YangTools] 分析过滤器控制实例失败: " + ex.Message);
                return false;
            }
        }
    }
}
