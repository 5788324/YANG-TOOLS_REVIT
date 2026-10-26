using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace YangTools.Revit.Core
{
    public class RibbonConfig
    {
        // Key = PanelName, Value = (Key = Command FullName, Value = Is Visible on Main Panel)
        public Dictionary<string, Dictionary<string, bool>> PanelSettings { get; set; }
            = new Dictionary<string, Dictionary<string, bool>>();
    }

    public static class RibbonConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YangTools",
            "ribbon_config.json");

        private static RibbonConfig? _currentConfig;

        public static RibbonConfig Current => _currentConfig ??= Load();

        private static RibbonConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonConvert.DeserializeObject<RibbonConfig>(json) ?? new RibbonConfig();
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("Ribbon 配置读取", ex);
            }

            return new RibbonConfig();
        }

        public static void Save(RibbonConfig config)
        {
            string? directory = Path.GetDirectoryName(ConfigPath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("无法确定 Ribbon 配置目录。");

            Directory.CreateDirectory(directory);
            string tempPath = ConfigPath + ".tmp";
            string backupPath = ConfigPath + ".bak";
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);

            try
            {
                File.WriteAllText(tempPath, json);
                if (File.Exists(ConfigPath))
                {
                    File.Copy(ConfigPath, backupPath, true);
                    File.Delete(ConfigPath);
                }
                File.Move(tempPath, ConfigPath);
                _currentConfig = config;
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                    if (!File.Exists(ConfigPath) && File.Exists(backupPath))
                        File.Copy(backupPath, ConfigPath, true);
                }
                catch
                {
                    // 保留原始异常。
                }
                throw;
            }
        }

        public static void ReplacePanelSettings(Dictionary<string, Dictionary<string, bool>> panelSettings)
        {
            var clean = new RibbonConfig
            {
                PanelSettings = panelSettings ?? new Dictionary<string, Dictionary<string, bool>>()
            };
            Save(clean);
        }

        /// <summary>查询命令是否显示在 Ribbon 主区；false 表示进入面板展开区。</summary>
        public static bool IsCommandVisibleOnMainPanel(string panelName, string commandFullName, bool defaultVisible = false)
        {
            // 优先使用当前面板中的显隐设置。迁移 Ribbon 面板后，旧版 JSON 仍按原面板名存储；
            // 以命令的唯一 FullName 回退查找，避免升级时静默丢失用户已有的自定义设置。
            var panelSettings = Current.PanelSettings;
            if (panelSettings == null) return defaultVisible;
            if (panelSettings.TryGetValue(panelName, out var commands) && commands != null &&
                commands.TryGetValue(commandFullName, out bool isVisible))
                return isVisible;
            foreach (var panel in panelSettings)
            {
                if (panel.Key == panelName || panel.Value == null) continue;
                if (panel.Value.TryGetValue(commandFullName, out bool previousVisibility))
                    return previousVisibility;
            }
            return defaultVisible;
        }
    }
}
