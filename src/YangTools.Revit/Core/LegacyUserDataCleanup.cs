using System;
using System.Collections.Generic;
using System.IO;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// 清理已经从 YangTools 中永久删除的功能所遗留的用户数据。
    /// 仅删除明确列出的旧助手目录/文件，不触碰 MCP、主题、功能配置等仍在使用的数据。
    /// </summary>
    internal static class LegacyUserDataCleanup
    {
        private static readonly string YangToolsAppDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YangTools");

        private static readonly string AssistantDirectory = Path.Combine(
            YangToolsAppDataRoot,
            "Assistant");

        private static readonly string[] LegacyAssistantFiles =
        {
            Path.Combine(YangToolsAppDataRoot, "copilot_settings.json"),
            Path.Combine(YangToolsAppDataRoot, "copilot_history.json"),
            Path.Combine(YangToolsAppDataRoot, "moyu_settings.json"),
            Path.Combine(YangToolsAppDataRoot, "moyu_history.json")
        };

        internal static void RemoveRetiredAssistantData()
        {
            var removed = new List<string>();
            var failures = new List<string>();

            TryDeleteDirectory(AssistantDirectory, removed, failures);
            foreach (string path in LegacyAssistantFiles)
                TryDeleteFile(path, removed, failures);

            if (removed.Count > 0)
            {
                DiagnosticLog.WriteInfo(
                    "清理已删除 AI/摸鱼助手旧数据",
                    "已删除：" + string.Join("；", removed));
            }

            if (failures.Count > 0)
            {
                DiagnosticLog.WriteInfo(
                    "清理已删除 AI/摸鱼助手旧数据",
                    "部分旧数据未能删除：" + string.Join("；", failures));
            }
        }

        private static void TryDeleteDirectory(
            string path,
            ICollection<string> removed,
            ICollection<string> failures)
        {
            if (!Directory.Exists(path)) return;
            try
            {
                Directory.Delete(path, true);
                removed.Add(path);
            }
            catch (Exception ex)
            {
                failures.Add(path + "（" + ex.Message + "）");
            }
        }

        private static void TryDeleteFile(
            string path,
            ICollection<string> removed,
            ICollection<string> failures)
        {
            if (!File.Exists(path)) return;
            try
            {
                File.Delete(path);
                removed.Add(path);
            }
            catch (Exception ex)
            {
                failures.Add(path + "（" + ex.Message + "）");
            }
        }
    }
}
