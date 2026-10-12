using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.FamilyInstances
{
    // Request and result contain no WPF controls or window/view-model references.
    // Call only from a Revit API context (the existing read-only ExternalEvent).
    public sealed class FamilyInstanceTypeOptionsRequest
    {
        public Document Document { get; }
        public bool IsSystemTab { get; }
        public ElementId FamilyId { get; }
        public BuiltInCategory SystemCategory { get; }

        public FamilyInstanceTypeOptionsRequest(Document document, bool isSystemTab,
            ElementId familyId, BuiltInCategory systemCategory)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            IsSystemTab = isSystemTab;
            FamilyId = familyId;
            SystemCategory = systemCategory;
        }
    }

    public sealed class FamilyInstanceTypeOption
    {
        public ElementId TypeId { get; }
        public string DisplayName { get; }

        public FamilyInstanceTypeOption(ElementId typeId, string displayName)
        {
            TypeId = typeId;
            DisplayName = displayName ?? string.Empty;
        }
    }

    public sealed class FamilyInstanceTypeOptionsResult
    {
        public IReadOnlyList<FamilyInstanceTypeOption> Options { get; }

        public FamilyInstanceTypeOptionsResult(IReadOnlyList<FamilyInstanceTypeOption> options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }
    }

    public static class FamilyInstanceTypeOptionsService
    {
        public static FamilyInstanceTypeOptionsResult Read(FamilyInstanceTypeOptionsRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var items = new List<FamilyInstanceTypeOption>();
            Document doc = request.Document;
            if (!doc.IsValidObject)
                throw new InvalidOperationException("族实例所属文档已关闭。 ");

            if (request.IsSystemTab)
            {
                if (request.SystemCategory != BuiltInCategory.INVALID)
                {
                    foreach (Element type in new FilteredElementCollector(doc)
                        .OfCategory(request.SystemCategory)
                        .WhereElementIsElementType())
                    {
                        items.Add(new FamilyInstanceTypeOption(type.Id, type.Name));
                    }
                }
            }
            else if (request.FamilyId != null && request.FamilyId != ElementId.InvalidElementId)
            {
                Family family = doc.GetElement(request.FamilyId) as Family;
                if (family != null)
                {
                    foreach (ElementId id in family.GetFamilySymbolIds())
                    {
                        FamilySymbol symbol = doc.GetElement(id) as FamilySymbol;
                        if (symbol != null)
                            items.Add(new FamilyInstanceTypeOption(symbol.Id, symbol.Name));
                    }
                }
            }
            return new FamilyInstanceTypeOptionsResult(items
                .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList());
        }
    }
}
