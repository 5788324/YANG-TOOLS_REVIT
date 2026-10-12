using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YangTools.Revit.Core.BatchTasks
{
    /// <summary>
    /// Compares local/network/Desktop Connector paths with paths stored by Revit.
    /// Revit may expose a Desktop Connector link as an "Autodesk Docs://" display path,
    /// so a plain full-path string comparison is insufficient.
    /// </summary>
    internal static class ExternalLinkPathMatcher
    {
        private static readonly HashSet<string> InfrastructureSegments =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ACCDocs",
                "Autodesk Docs",
                "Autodesk Docs:",
                "Project Files"
            };

        public static bool AreEquivalent(string existingPath, string candidatePath)
        {
            if (string.IsNullOrWhiteSpace(existingPath) || string.IsNullOrWhiteSpace(candidatePath))
                return false;

            string normalizedExisting = NormalizeRawPath(existingPath);
            string normalizedCandidate = NormalizeRawPath(candidatePath);
            if (string.Equals(normalizedExisting, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
                return true;

            List<string> existingSegments = GetComparableSegments(existingPath);
            List<string> candidateSegments = GetComparableSegments(candidatePath);
            if (existingSegments.Count == 0 || candidateSegments.Count == 0)
                return false;

            if (!string.Equals(
                    existingSegments[existingSegments.Count - 1],
                    candidateSegments[candidateSegments.Count - 1],
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int matchedSuffixSegments = 0;
            int existingIndex = existingSegments.Count - 1;
            int candidateIndex = candidateSegments.Count - 1;
            while (existingIndex >= 0 && candidateIndex >= 0)
            {
                if (!string.Equals(
                        existingSegments[existingIndex],
                        candidateSegments[candidateIndex],
                        StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                matchedSuffixSegments++;
                existingIndex--;
                candidateIndex--;
            }

            // Require filename plus at least two matching parent folders. This catches the
            // common Desktop Connector <-> Autodesk Docs display-path conversion without
            // treating unrelated links that merely share a filename as identical.
            return matchedSuffixSegments >= 3;
        }

        private static string NormalizeRawPath(string path)
        {
            string value = Decode(path).Trim().Trim('"').Replace('/', '\\');
            if (value.IndexOf("://", StringComparison.Ordinal) >= 0)
                return value.TrimEnd('\\');

            try
            {
                return Path.GetFullPath(value)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return value.TrimEnd('\\');
            }
        }

        private static List<string> GetComparableSegments(string path)
        {
            string value = Decode(path).Trim().Trim('"').Replace('\\', '/');
            int schemeIndex = value.IndexOf("://", StringComparison.Ordinal);
            if (schemeIndex >= 0)
                value = value.Substring(schemeIndex + 3);

            List<string> segments = value
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Trim())
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToList();

            int connectorRootIndex = segments.FindIndex(segment =>
                string.Equals(segment, "ACCDocs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "Autodesk Docs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "Autodesk Docs:", StringComparison.OrdinalIgnoreCase));
            if (connectorRootIndex >= 0 && connectorRootIndex + 1 < segments.Count)
                segments = segments.Skip(connectorRootIndex + 1).ToList();

            if (segments.Count > 0 && segments[0].Length == 2 && segments[0][1] == ':')
                segments.RemoveAt(0);

            segments = segments
                .Where(segment => !InfrastructureSegments.Contains(segment))
                .ToList();

            return segments;
        }

        private static string Decode(string value)
        {
            try
            {
                return Uri.UnescapeDataString(value ?? string.Empty);
            }
            catch
            {
                return value ?? string.Empty;
            }
        }
    }
}
