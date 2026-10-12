using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YangTools.Revit.Core;

namespace YangTools.Revit.Mcp
{
    // RC1: Read-only document/selection queries, always executed via ExternalEvent by the Broker.
    internal static class CoreDocumentToolHandler
    {
        internal static JObject BuildRevitStatus(UIApplication uiapp)
        {
            Document? document = uiapp.ActiveUIDocument?.Document;
            return new JObject
            {
                ["ready"] = true,
                ["revitVersion"] = uiapp.Application.VersionNumber,
                ["activeDocument"] = document?.Title ?? string.Empty,
                ["hasActiveDocument"] = document != null
            };
        }

        internal static JObject BuildActiveDocument(UIApplication uiapp)
        {
            Document? document = uiapp.ActiveUIDocument?.Document;
            if (document == null) return new JObject { ["available"] = false };
            bool cloud = false;
            try
            {
                var property = document.GetType().GetProperty("IsModelInCloud");
                if (property?.GetValue(document) is bool value) cloud = value;
            }
            catch { }
            return new JObject
            {
                ["available"] = true,
                ["title"] = document.Title,
                ["path"] = document.PathName ?? string.Empty,
                ["isWorkshared"] = document.IsWorkshared,
                ["isCloudModel"] = cloud,
                ["isFamilyDocument"] = document.IsFamilyDocument
            };
        }

        internal static JArray BuildSelection(UIApplication uiapp)
        {
            UIDocument? uidoc = uiapp.ActiveUIDocument;
            var result = new JArray();
            if (uidoc == null) return result;
            Document document = uidoc.Document;
            foreach (ElementId id in uidoc.Selection.GetElementIds().Take(100))
            {
                Element? element = document.GetElement(id);
                if (element == null) continue;
                result.Add(ToElementSummary(element));
            }
            return result;
        }

        internal static JArray BuildElementParameters(UIApplication uiapp, JObject args)
        {
            Document? document = uiapp.ActiveUIDocument?.Document;
            var result = new JArray();
            if (document == null) return result;
            IEnumerable<long> ids = (args["elementIds"] as JArray ?? new JArray())
                .Select(item => item.Value<long>());
            foreach (long value in ids.Take(100))
            {
                #if REVIT2024_OR_GREATER
                Element? element = document.GetElement(new ElementId(value));
#else
                Element? element = document.GetElement(new ElementId(unchecked((int)value)));
#endif
                if (element == null) continue;
                var parameters = new JObject();
                foreach (Parameter parameter in element.GetOrderedParameters().Take(120))
                {
                    string name = parameter.Definition?.Name ?? parameter.Id.GetIdValue().ToString();
                    string display;
                    try { display = parameter.AsValueString() ?? parameter.AsString() ?? string.Empty; }
                    catch { display = string.Empty; }
                    parameters[name] = display;
                }
                JObject item = ToElementSummary(element);
                item["parameters"] = parameters;
                result.Add(item);
            }
            return result;
        }

        internal static JArray QueryElements(UIApplication uiapp, JObject args)
        {
            Document? document = uiapp.ActiveUIDocument?.Document;
            var result = new JArray();
            if (document == null) return result;
            string category = args["category"]?.ToString() ?? string.Empty;
            string name = args["nameContains"]?.ToString() ?? string.Empty;
            int max = Math.Max(1, Math.Min(500, args["maxResults"]?.Value<int?>() ?? 100));

            IEnumerable<Element> elements = new FilteredElementCollector(document)
                .WhereElementIsNotElementType()
                .Cast<Element>();
            if (!string.IsNullOrWhiteSpace(category))
                elements = elements.Where(element => (element.Category?.Name ?? string.Empty)
                    .IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!string.IsNullOrWhiteSpace(name))
                elements = elements.Where(element => (element.Name ?? string.Empty)
                    .IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (Element element in elements.Take(max)) result.Add(ToElementSummary(element));
            return result;
        }

        private static JObject ToElementSummary(Element element)
        {
            return new JObject
            {
                ["id"] = element.Id.GetIdValue(),
                ["name"] = element.Name ?? string.Empty,
                ["category"] = element.Category?.Name ?? string.Empty,
                ["type"] = element.GetType().Name
            };
        }

        private static RevitExecutionResult Success(string result) => new RevitExecutionResult { Success = true, Result = result ?? string.Empty };
        private static RevitExecutionResult Failure(string error) => new RevitExecutionResult { Success = false, Error = error ?? string.Empty };
    }
}
