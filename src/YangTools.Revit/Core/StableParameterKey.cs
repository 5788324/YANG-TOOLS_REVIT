using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// A stable-enough parameter identity used by the management grids.
    /// Display names are never treated as the primary write identity.
    /// </summary>
    public sealed class StableParameterDescriptor
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsReadOnly { get; set; }
    }

    public static class StableParameterKey
    {
        private const string BipPrefix = "BIP_";
        private const string GuidPrefix = "GUID_";
        private const string PidPrefix = "PID_";
        private const string NamePrefix = "NAME_";

        public static bool IsStableKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.StartsWith(BipPrefix, StringComparison.Ordinal)
                || value.StartsWith(GuidPrefix, StringComparison.Ordinal)
                || value.StartsWith(PidPrefix, StringComparison.Ordinal)
                || value.StartsWith(NamePrefix, StringComparison.Ordinal);
        }

        public static string Create(Parameter parameter)
        {
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));

            try
            {
                if (parameter.IsShared)
                {
                    Guid guid = parameter.GUID;
                    if (guid != Guid.Empty)
                        return GuidPrefix + guid.ToString("N").ToUpperInvariant();
                }
            }
            catch
            {
                // Some Revit parameter implementations throw when GUID is queried.
            }

            try
            {
                long idValue = parameter.Id.GetIdValue();
                if (idValue < 0) return BipPrefix + idValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (idValue > 0) return PidPrefix + idValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                // Fall back to an encoded definition name only when no API identity is available.
            }

            string name = parameter.Definition?.Name ?? string.Empty;
            return NamePrefix + EncodeName(name);
        }

        public static Parameter? Resolve(Element element, string? keyOrLegacyName)
        {
            if (element == null || string.IsNullOrWhiteSpace(keyOrLegacyName)) return null;
            string key = keyOrLegacyName.Trim();

            try
            {
                if (key.StartsWith(GuidPrefix, StringComparison.Ordinal))
                {
                    string raw = key.Substring(GuidPrefix.Length);
                    if (Guid.TryParseExact(raw, "N", out Guid guid))
                        return element.get_Parameter(guid);
                }
                else if (key.StartsWith(BipPrefix, StringComparison.Ordinal))
                {
                    string raw = key.Substring(BipPrefix.Length);
                    if (long.TryParse(raw, out long value))
                        return element.get_Parameter((BuiltInParameter)value);
                }
                else if (key.StartsWith(PidPrefix, StringComparison.Ordinal))
                {
                    string raw = key.Substring(PidPrefix.Length);
                    if (long.TryParse(raw, out long value))
                    {
                        foreach (Parameter parameter in element.GetOrderedParameters())
                        {
                            if (parameter.Id.GetIdValue() == value)
                                return parameter;
                        }
                    }
                }
                else if (key.StartsWith(NamePrefix, StringComparison.Ordinal))
                {
                    string name = DecodeName(key.Substring(NamePrefix.Length));
                    return ResolveUniqueName(element, name);
                }
                else
                {
                    // Legacy config migration path. Deliberately refuse ambiguous same-name parameters.
                    return ResolveUniqueName(element, key);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public static Parameter? ResolveByDisplayLabel(Element element, string? label)
        {
            if (element == null || string.IsNullOrWhiteSpace(label)) return null;
            string trimmed = label.Trim();

            // Stable keys are accepted directly for machine-generated files.
            if (IsStableKey(trimmed)) return Resolve(element, trimmed);

            var descriptors = Collect(new[] { element });
            StableParameterDescriptor? exactDisplay = descriptors.FirstOrDefault(x =>
                string.Equals(x.DisplayName, trimmed, StringComparison.Ordinal));
            if (exactDisplay != null) return Resolve(element, exactDisplay.Key);

            var sameName = descriptors.Where(x => string.Equals(x.Name, trimmed, StringComparison.Ordinal)).ToList();
            if (sameName.Count == 1) return Resolve(element, sameName[0].Key);

            return null;
        }

        public static List<StableParameterDescriptor> Collect(IEnumerable<Element> elements)
        {
            var byKey = new Dictionary<string, StableParameterDescriptor>(StringComparer.Ordinal);

            foreach (Element element in elements ?? Enumerable.Empty<Element>())
            {
                if (element == null || !element.IsValidObject) continue;
                IEnumerable<Parameter> parameters;
                try { parameters = element.GetOrderedParameters(); }
                catch { continue; }

                foreach (Parameter parameter in parameters)
                {
                    try
                    {
                        string name = parameter.Definition?.Name ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        string key = Create(parameter);
                        bool readOnly = !ParameterValueHelper.CanEditAsText(parameter);

                        if (byKey.TryGetValue(key, out StableParameterDescriptor? existing))
                        {
                            // Header is considered read-only only if every observed parameter is read-only.
                            existing.IsReadOnly = existing.IsReadOnly && readOnly;
                        }
                        else
                        {
                            byKey[key] = new StableParameterDescriptor
                            {
                                Key = key,
                                Name = name,
                                DisplayName = name,
                                IsReadOnly = readOnly
                            };
                        }
                    }
                    catch
                    {
                        // Parameter enumeration must never make the whole grid unusable.
                    }
                }
            }

            foreach (var group in byKey.Values.GroupBy(x => x.Name, StringComparer.Ordinal))
            {
                bool duplicateName = group.Count() > 1;
                foreach (StableParameterDescriptor item in group)
                {
                    item.DisplayName = duplicateName
                        ? $"{item.Name} 〔{GetShortIdentity(item.Key)}〕"
                        : item.Name;
                }
            }

            return byKey.Values
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.Key, StringComparer.Ordinal)
                .ToList();
        }

        public static string NormalizeConfiguredKey(string? configured, IReadOnlyCollection<StableParameterDescriptor> available)
        {
            if (string.IsNullOrWhiteSpace(configured)) return string.Empty;
            string value = configured.Trim();

            if (IsStableKey(value))
            {
                if (value.StartsWith(NamePrefix, StringComparison.Ordinal))
                {
                    string legacyName = DecodeName(value.Substring(NamePrefix.Length));
                    var matches = available.Where(x => string.Equals(x.Name, legacyName, StringComparison.Ordinal)).ToList();
                    if (matches.Count == 1) return matches[0].Key;
                }
                return value;
            }

            // Legacy .txt files stored only the visible parameter name.
            var nameMatches = available.Where(x => string.Equals(x.Name, value, StringComparison.Ordinal)).ToList();
            if (nameMatches.Count == 1) return nameMatches[0].Key;
            return NamePrefix + EncodeName(value);
        }

        public static string GetDisplayName(string? key, IReadOnlyCollection<StableParameterDescriptor> available)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            StableParameterDescriptor? descriptor = available.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
            if (descriptor != null) return descriptor.DisplayName;

            if (key.StartsWith(NamePrefix, StringComparison.Ordinal))
                return DecodeName(key.Substring(NamePrefix.Length));
            return key;
        }

        private static Parameter? ResolveUniqueName(Element element, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            IList<Parameter> parameters = element.GetParameters(name);
            return parameters.Count == 1 ? parameters[0] : null;
        }

        private static string GetShortIdentity(string key)
        {
            if (key.StartsWith(GuidPrefix, StringComparison.Ordinal))
            {
                string raw = key.Substring(GuidPrefix.Length);
                return "GUID:" + (raw.Length > 8 ? raw.Substring(0, 8) : raw);
            }
            if (key.StartsWith(BipPrefix, StringComparison.Ordinal)) return "BIP:" + key.Substring(BipPrefix.Length);
            if (key.StartsWith(PidPrefix, StringComparison.Ordinal)) return "PID:" + key.Substring(PidPrefix.Length);
            return "Name";
        }

        private static string EncodeName(string value)
        {
            string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string DecodeName(string value)
        {
            try
            {
                string padded = value.Replace('-', '+').Replace('_', '/');
                switch (padded.Length % 4)
                {
                    case 2: padded += "=="; break;
                    case 3: padded += "="; break;
                }
                return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            }
            catch
            {
                return value;
            }
        }
    }
}
