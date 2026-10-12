using System;
using System.IO;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// 为临时族文件创建独立目录，避免同名任务互相覆盖，并统一清理。
    /// 文件名决定载入项目后的族名称，因此不会把 GUID 写入文件名。
    /// </summary>
    public static class FamilyFileHelper
    {
        public static string CreateTempFamilyPath(string requestedFamilyName, string operationFolder)
        {
            string safeName = SanitizeFamilyName(requestedFamilyName);
            string root = Path.Combine(
                Path.GetTempPath(),
                string.IsNullOrWhiteSpace(operationFolder) ? "YangTools_FamilyTemp" : operationFolder,
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(root);
            return Path.Combine(root, safeName + ".rfa");
        }

        public static void Cleanup(string? familyPath)
        {
            if (string.IsNullOrWhiteSpace(familyPath)) return;

            try
            {
                if (File.Exists(familyPath)) File.Delete(familyPath);
            }
            catch { }

            try
            {
                string? directory = Path.GetDirectoryName(familyPath);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch { }
        }

        private static string SanitizeFamilyName(string? requestedFamilyName)
        {
            string name = string.IsNullOrWhiteSpace(requestedFamilyName)
                ? "YangToolsGeneratedFamily"
                : requestedFamilyName.Trim();

            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            name = name.Trim().TrimEnd('.', ' ');
            if (string.IsNullOrWhiteSpace(name)) name = "YangToolsGeneratedFamily";
            if (name.Length > 120) name = name.Substring(0, 120).TrimEnd('.', ' ');
            return name;
        }
    }
}
