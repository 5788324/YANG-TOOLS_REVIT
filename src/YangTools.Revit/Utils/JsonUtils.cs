using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using YangTools.Revit.Mcp;

namespace YangTools.Revit.Utils;

/// <summary>
/// v0.1 JSON 序列化/反序列化工具。
/// 使用 JavaScriptSerializer（零 NuGet 依赖），强制输出小写协议字段名。
/// </summary>
public static class JsonUtils
{
    private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

    /// <summary>
    /// 从 JSON 字符串反序列化为 McpRequest。
    /// 输入 JSON 的字段名大小写不敏感（tool/Tool/TOOL 均可）。
    /// 返回 null 表示 JSON 格式错误。
    /// </summary>
    public static McpRequest? DeserializeRequest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var dict = Serializer.Deserialize<Dictionary<string, object>>(json);
            if (dict == null)
                return null;

            var request = new McpRequest();

            // 不区分大小写查找 tool 字段
            string? toolValue = GetStringIgnoreCase(dict, "tool");
            if (toolValue != null)
                request.Tool = toolValue;

            // 不区分大小写查找 args 字段
            object? argsValue = GetValueIgnoreCase(dict, "args");
            if (argsValue is Dictionary<string, object> argsDict)
            {
                var converted = new Dictionary<string, object?>();
                foreach (var kvp in argsDict)
                    converted[kvp.Key] = kvp.Value;
                request.Args = converted;
            }
            else if (argsValue is IDictionary<string, object> argsIDict)
            {
                var converted = new Dictionary<string, object?>();
                foreach (var kvp in argsIDict)
                    converted[kvp.Key] = kvp.Value;
                request.Args = converted;
            }

            return request;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 将 McpResponse 序列化为 JSON 字符串。
    /// 强制输出小写字段名：ok / tool / data / error / warnings。
    /// </summary>
    public static string SerializeResponse(McpResponse response)
    {
        var dict = new Dictionary<string, object?>
        {
            ["ok"] = response.Ok,
            ["tool"] = response.Tool,
            ["data"] = response.Data,
            ["error"] = response.Error,
            ["warnings"] = response.Warnings ?? new List<string>(),
        };

        return Serializer.Serialize(dict);
    }

    /// <summary>
    /// 从 Dictionary 中按不区分大小写的方式获取字符串值。
    /// </summary>
    private static string? GetStringIgnoreCase(Dictionary<string, object> dict, string key)
    {
        foreach (var kvp in dict)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value?.ToString();
            }
        }
        return null;
    }

    /// <summary>
    /// 从 Dictionary 中按不区分大小写的方式获取原始值。
    /// </summary>
    private static object? GetValueIgnoreCase(Dictionary<string, object> dict, string key)
    {
        foreach (var kvp in dict)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        return null;
    }
}
