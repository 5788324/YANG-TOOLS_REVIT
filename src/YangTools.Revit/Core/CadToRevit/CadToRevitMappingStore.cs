using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace YangTools.Revit.Core.CadToRevit
{
    public static class CadToRevitMappingStore
    {
        public static string MappingPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YangTools", "CadToRevit", "Mapping.json");

        public static CadToRevitMappingFile Load()
        {
            try
            {
                if (!File.Exists(MappingPath)) return new CadToRevitMappingFile();
                string json = File.ReadAllText(MappingPath);
                CadToRevitMappingFile? data = JsonConvert.DeserializeObject<CadToRevitMappingFile>(json);
                return Normalize(data ?? new CadToRevitMappingFile());
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("读取 CAD 转 Revit 映射", ex);
                return new CadToRevitMappingFile();
            }
        }

        public static void Save(IEnumerable<CadToRevitMappingRow> rows)
        {
            var file = Load();
            foreach (CadToRevitMappingRow row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.SourceKey)) continue;
                Dictionary<string, CadToRevitMappingSnapshot> dict = GetDictionary(file, row.Kind);
                dict[row.SourceKey] = new CadToRevitMappingSnapshot
                {
                    Action = row.Action ?? CadToRevitMappingActions.UseExisting,
                    TargetName = row.TargetName ?? string.Empty,
                    NewTypeName = row.NewTypeName ?? string.Empty
                };
            }

            string? dir = Path.GetDirectoryName(MappingPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            string temp = MappingPath + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(file, Formatting.Indented));
            if (File.Exists(MappingPath))
            {
                try { File.Replace(temp, MappingPath, null); }
                catch
                {
                    // 某些文件系统不支持 Replace；覆盖复制保留旧文件直到新内容完整落盘，避免 delete-first 空窗。
                    File.Copy(temp, MappingPath, true);
                    File.Delete(temp);
                }
            }
            else
            {
                File.Move(temp, MappingPath);
            }
        }

        public static bool TryGet(CadToRevitMappingFile file, CadRevitObjectKind kind, string sourceKey, out CadToRevitMappingSnapshot snapshot)
        {
            Dictionary<string, CadToRevitMappingSnapshot> dict = GetDictionary(file, kind);
            if (dict.TryGetValue(sourceKey, out CadToRevitMappingSnapshot value))
            {
                snapshot = value ?? new CadToRevitMappingSnapshot();
                return true;
            }
            snapshot = new CadToRevitMappingSnapshot();
            return false;
        }

        private static Dictionary<string, CadToRevitMappingSnapshot> GetDictionary(CadToRevitMappingFile file, CadRevitObjectKind kind)
        {
            switch (kind)
            {
                case CadRevitObjectKind.Text: return file.Text;
                case CadRevitObjectKind.Curve: return file.Line;
                case CadRevitObjectKind.Fill: return file.Fill;
                case CadRevitObjectKind.Dimension: return file.Dimension;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static CadToRevitMappingFile Normalize(CadToRevitMappingFile file)
        {
            file.Text = Rebuild(file.Text);
            file.Line = Rebuild(file.Line);
            file.Fill = Rebuild(file.Fill);
            file.Dimension = Rebuild(file.Dimension);
            return file;
        }

        private static Dictionary<string, CadToRevitMappingSnapshot> Rebuild(Dictionary<string, CadToRevitMappingSnapshot>? source)
        {
            var result = new Dictionary<string, CadToRevitMappingSnapshot>(StringComparer.OrdinalIgnoreCase);
            if (source == null) return result;
            foreach (KeyValuePair<string, CadToRevitMappingSnapshot> pair in source)
            {
                if (string.IsNullOrWhiteSpace(pair.Key)) continue;
                result[pair.Key] = pair.Value ?? new CadToRevitMappingSnapshot();
            }
            return result;
        }
    }
}
