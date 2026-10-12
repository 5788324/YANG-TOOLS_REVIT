using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using YangTools.Revit.Core;

namespace YangTools.Revit.Mcp
{
    internal sealed class McpGatewaySettings
    {
        public bool AutoStart { get; set; } = false;
        public bool AllowPythonExecution { get; set; } = false;
    }

    internal static class McpGatewaySettingsStore
    {
        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YangTools", "MCP", "settings.json");

        internal static McpGatewaySettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new McpGatewaySettings();
                return JsonConvert.DeserializeObject<McpGatewaySettings>(File.ReadAllText(SettingsPath, Encoding.UTF8))
                       ?? new McpGatewaySettings();
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("读取 MCP 设置", ex);
                return new McpGatewaySettings();
            }
        }

        internal static void Save(McpGatewaySettings settings)
        {
            string? directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(settings, Formatting.Indented), Encoding.UTF8);
        }
    }
}
