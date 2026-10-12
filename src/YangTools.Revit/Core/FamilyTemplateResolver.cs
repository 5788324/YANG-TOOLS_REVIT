using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// 解析常规模型族模板。兼容设置值为文件、目录，以及不同语言模板目录。
    /// </summary>
    public static class FamilyTemplateResolver
    {
        private static readonly string[] GenericModelTemplateNames =
        {
            "公制常规模型.rft",
            "Metric Generic Model.rft",
            "Generic Model.rft"
        };

        public static string Resolve(UIApplication uiApp, params string?[] requestedPaths)
        {
            foreach (string? requestedPath in requestedPaths)
            {
                string resolved = ResolveCandidate(requestedPath);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }

            string applicationPath = uiApp.Application.FamilyTemplatePath;
            string fromApplication = ResolveCandidate(applicationPath);
            if (!string.IsNullOrEmpty(fromApplication)) return fromApplication;

            string version = uiApp.Application.VersionNumber;
            string commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var fallbackRoots = new List<string>
            {
                Path.Combine(commonData, "Autodesk", $"RVT {version}", "Family Templates"),
                Path.Combine(commonData, "Autodesk", $"RVT {version}", "Family Templates", "Chinese"),
                Path.Combine(commonData, "Autodesk", $"RVT {version}", "Family Templates", "English")
            };

            foreach (string root in fallbackRoots)
            {
                string resolved = ResolveCandidate(root);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }

            return string.Empty;
        }

        private static string ResolveCandidate(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return string.Empty;

            string path = candidate.Trim().Trim('"');
            try
            {
                if (File.Exists(path) && string.Equals(Path.GetExtension(path), ".rft", StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(path);

                if (!Directory.Exists(path)) return string.Empty;

                foreach (string templateName in GenericModelTemplateNames)
                {
                    string direct = Path.Combine(path, templateName);
                    if (File.Exists(direct)) return Path.GetFullPath(direct);
                }

                string[] languageFolders = { "Chinese", "English", "简体中文", "中文" };
                foreach (string languageFolder in languageFolders)
                {
                    string languagePath = Path.Combine(path, languageFolder);
                    if (!Directory.Exists(languagePath)) continue;

                    foreach (string templateName in GenericModelTemplateNames)
                    {
                        string direct = Path.Combine(languagePath, templateName);
                        if (File.Exists(direct)) return Path.GetFullPath(direct);
                    }
                }

                // 仅在直接候选未命中时递归搜索同名模板；模板目录规模通常很小。
                foreach (string templateName in GenericModelTemplateNames)
                {
                    string? recursive = Directory
                        .EnumerateFiles(path, templateName, SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (!string.IsNullOrEmpty(recursive)) return Path.GetFullPath(recursive);
                }
            }
            catch
            {
                // 路径不可访问时由调用方要求用户手动浏览选择。
            }

            return string.Empty;
        }
    }
}
