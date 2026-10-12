using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.CadToRevit
{
    public sealed class CadToRevitTypeCatalog
    {
        private readonly Document _document;

        public CadToRevitTypeCatalog(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            TextTypeNames = CollectElementTypeNames<TextNoteType>();
            FillTypeNames = CollectElementTypeNames<FilledRegionType>();
            DimensionTypeNames = CollectElementTypeNames<DimensionType>();
            LineStyleNames = CollectLineStyleNames();
        }

        public List<string> TextTypeNames { get; }
        public List<string> LineStyleNames { get; }
        public List<string> FillTypeNames { get; }
        public List<string> DimensionTypeNames { get; }

        public List<string> GetNames(CadRevitObjectKind kind)
        {
            switch (kind)
            {
                case CadRevitObjectKind.Text: return new List<string>(TextTypeNames);
                case CadRevitObjectKind.Curve: return new List<string>(LineStyleNames);
                case CadRevitObjectKind.Fill: return new List<string>(FillTypeNames);
                case CadRevitObjectKind.Dimension: return new List<string>(DimensionTypeNames);
                default: return new List<string>();
            }
        }

        public string SuggestTarget(CadRevitObjectKind kind, string sourceKey)
        {
            List<string> names = GetNames(kind);
            if (names.Count == 0) return string.Empty;
            string source = (sourceKey ?? string.Empty).Trim();
            string? exact = names.FirstOrDefault(n => string.Equals(n, source, StringComparison.CurrentCultureIgnoreCase));
            if (exact != null) return exact;

            string normalized = kind == CadRevitObjectKind.Curve ? NormalizeCadLineStyleName(source) : source;
            exact = names.FirstOrDefault(n => string.Equals(n, normalized, StringComparison.CurrentCultureIgnoreCase));
            if (exact != null) return exact;

            string compact = Compact(normalized);
            string? compactMatch = names.FirstOrDefault(n => Compact(n) == compact);
            if (compactMatch != null) return compactMatch;


            return string.Empty;
        }

        public static string NormalizeCadLineStyleName(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return "CAD-线型";
            string value = Regex.Replace(source.Trim(), @"\s+(White|Black|Red|Yellow|Green|Cyan|Blue|Magenta|ByLayer|ByBlock)(\s+\d+)?$", string.Empty, RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(value) ? source.Trim() : value;
        }

        public static string DefaultNewTypeName(CadRevitObjectKind kind, string sourceKey)
        {
            string source = (sourceKey ?? string.Empty).Trim();
            if (kind == CadRevitObjectKind.Curve) return NormalizeCadLineStyleName(source);
            if (string.IsNullOrWhiteSpace(source)) return kind == CadRevitObjectKind.Text ? "CAD-文字" : kind == CadRevitObjectKind.Fill ? "CAD-填充" : "CAD-标注";
            return source;
        }

        private List<string> CollectElementTypeNames<T>() where T : ElementType
        {
            return new FilteredElementCollector(_document)
                .OfClass(typeof(T))
                .Cast<T>()
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private List<string> CollectLineStyleNames()
        {
            var names = new List<string>();
            Category? lines = Category.GetCategory(_document, BuiltInCategory.OST_Lines);
            if (lines?.SubCategories == null) return names;
            foreach (Category sub in lines.SubCategories)
            {
                if (!string.IsNullOrWhiteSpace(sub.Name)) names.Add(sub.Name);
            }
            return names.Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static string Compact(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return Regex.Replace(value, @"[\s_\-]+", string.Empty).ToLowerInvariant();
        }
    }
}
