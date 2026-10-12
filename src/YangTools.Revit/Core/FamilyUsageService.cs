using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    public sealed class FamilyUsageSnapshot
    {
        public Dictionary<long, int> FamilyInstanceCounts { get; } = new Dictionary<long, int>();
        public Dictionary<long, int> CurrentViewFamilyInstanceCounts { get; } = new Dictionary<long, int>();
        public Dictionary<long, int> TypeInstanceCounts { get; } = new Dictionary<long, int>();

        public int GetFamilyCount(ElementId familyId) => Get(FamilyInstanceCounts, familyId);
        public int GetCurrentViewFamilyCount(ElementId familyId) => Get(CurrentViewFamilyInstanceCounts, familyId);
        public int GetTypeCount(ElementId typeId) => Get(TypeInstanceCounts, typeId);

        private static int Get(Dictionary<long, int> source, ElementId id)
        {
            if (id == null || id == ElementId.InvalidElementId) return 0;
            return source.TryGetValue(id.GetIdValue(), out int value) ? value : 0;
        }
    }

    public static class FamilyUsageService
    {
        public static FamilyUsageSnapshot Build(Document document, ElementId activeViewId)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var snapshot = new FamilyUsageSnapshot();

            IEnumerable<FamilyInstance> projectInstances = new FilteredElementCollector(document)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>();
            Accumulate(projectInstances, snapshot.FamilyInstanceCounts, snapshot.TypeInstanceCounts);

            try
            {
                if (activeViewId != null && activeViewId != ElementId.InvalidElementId)
                {
                    IEnumerable<FamilyInstance> viewInstances = new FilteredElementCollector(document, activeViewId)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>();
                    Accumulate(viewInstances, snapshot.CurrentViewFamilyInstanceCounts, null);
                }
            }
            catch
            {
                // Some non-graphical views cannot be used by a view-scoped collector.
            }

            return snapshot;
        }

        private static void Accumulate(
            IEnumerable<FamilyInstance> instances,
            Dictionary<long, int> familyCounts,
            Dictionary<long, int>? typeCounts)
        {
            foreach (FamilyInstance instance in instances)
            {
                FamilySymbol? symbol = instance.Symbol;
                Family? family = symbol?.Family;
                if (family == null || symbol == null) continue;

                Increment(familyCounts, family.Id.GetIdValue());
                if (typeCounts != null) Increment(typeCounts, symbol.Id.GetIdValue());
            }
        }

        private static void Increment(Dictionary<long, int> target, long id)
        {
            target[id] = target.TryGetValue(id, out int count) ? count + 1 : 1;
        }
    }
}
