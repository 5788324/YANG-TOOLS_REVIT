using System;
using System.IO;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.BatchTasks
{
    internal static class BatchExportNaming
    {
        public static string ResolveBaseName(string? template, Document doc, View? view, int index)
        {
            string modelName = SanitizeFileName(doc.Title);
            string modelFileName = modelName;
            try
            {
                if (!string.IsNullOrWhiteSpace(doc.PathName))
                    modelFileName = SanitizeFileName(Path.GetFileNameWithoutExtension(doc.PathName));
            }
            catch { }

            string sheetNumber = string.Empty;
            string sheetName = string.Empty;
            string viewName = string.Empty;
            if (view != null)
            {
                viewName = view.Name ?? string.Empty;
                if (view is ViewSheet sheet)
                {
                    sheetNumber = sheet.SheetNumber ?? string.Empty;
                    sheetName = sheet.Name ?? string.Empty;
                }
            }

            string value = string.IsNullOrWhiteSpace(template) ? "{模型名}" : template.Trim();
            value = value.Replace("{模型名}", modelName)
                         .Replace("{模型文件名}", modelFileName)
                         .Replace("{图纸号}", sheetNumber)
                         .Replace("{图纸名}", sheetName)
                         .Replace("{视图名}", viewName)
                         .Replace("{序号}", (index + 1).ToString("000"))
                         .Replace("{日期}", DateTime.Now.ToString("yyyyMMdd"));

            value = Regex.Replace(value, @"[-_ ]{2,}", "-");
            value = value.Trim(' ', '-', '_', '.');
            value = SanitizeFileName(value);
            return string.IsNullOrWhiteSpace(value) ? modelName : value;
        }

        public static string EnsureUniqueBaseName(string baseName, System.Collections.Generic.ISet<string> used)
        {
            string candidate = baseName;
            int suffix = 2;
            while (!used.Add(candidate))
                candidate = $"{baseName}-{suffix++:00}";
            return candidate;
        }

        public static string SanitizeFileName(string? value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "未命名" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                source = source.Replace(invalid, '_');
            return source.Trim().TrimEnd('.');
        }
    }
}
