using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using YangTools.Revit.Mcp;

namespace YangTools.Revit.Utils;

/// <summary>
/// MCP 操作日志：每次 /mcp/ 调用（成功或失败）写入 logs/operations/YYYY-MM-DD.jsonl。
///
/// 日志字段：
///   timestamp, tool, ok, error, warnings, dryRun, requestDigest
///
/// 设计约束：
///   - 不写入完整 body / args 值（requestDigest 只含摘要）
///   - 线程安全（lock）
///   - 写入异常不向上传播（不因日志崩溃）
///   - 相对路径，不写死本机路径
/// </summary>
public static class OperationLogger
{
    private static readonly object Lock = new object();
    private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

    private const string LogDir = "logs/operations";

    /// <summary>
    /// 记录一次 MCP 操作。
    /// </summary>
    /// <param name="request">已解析的 McpRequest，可以为 null（表示请求解析前就失败）。</param>
    /// <param name="response">McpResponse。</param>
    public static void Log(McpRequest? request, McpResponse response)
    {
        var entry = new Dictionary<string, object?>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["tool"] = request?.Tool ?? string.Empty,
            ["ok"] = response.Ok,
            ["error"] = response.Error,
            ["warnings"] = response.Warnings?.Count > 0 ? response.Warnings : null,
            ["dryRun"] = GetDryRun(request),
            ["requestDigest"] = GetRequestDigest(request),
        };

        var line = Serializer.Serialize(entry);

        lock (Lock)
        {
            try
            {
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogDir);
                Directory.CreateDirectory(dir);

                var fileName = DateTime.UtcNow.ToString("yyyy-MM-dd") + ".jsonl";
                var filePath = Path.Combine(dir, fileName);

                File.AppendAllText(filePath, line + Environment.NewLine);
            }
            catch
            {
                // 日志写入失败不应导致 MCP 请求失败
            }
        }
    }

    /// <summary>
    /// 提取 dryRun 标记。默认 false。
    /// </summary>
    private static bool GetDryRun(McpRequest? request)
    {
        if (request?.Args == null)
            return false;

        foreach (var kvp in request.Args)
        {
            if (string.Equals(kvp.Key, "dryRun", StringComparison.OrdinalIgnoreCase))
            {
                if (kvp.Value is bool b)
                    return b;
                if (bool.TryParse(kvp.Value?.ToString(), out var parsed))
                    return parsed;
            }
        }
        return false;
    }

    /// <summary>
    /// 生成请求摘要：args 键名列表 + 值类型，不写入具体值。
    /// </summary>
    private static Dictionary<string, string> GetRequestDigest(McpRequest? request)
    {
        var digest = new Dictionary<string, string>();

        if (request?.Args == null)
            return digest;

        foreach (var kvp in request.Args)
        {
            var key = kvp.Key;
            var val = kvp.Value;

            if (val == null)
                digest[key] = "null";
            else if (val is string s)
                digest[key] = $"string({Math.Min(s.Length, 50)} chars)";
            else if (val is bool)
                digest[key] = "bool";
            else if (val is int || val is long || val is double || val is float || val is decimal)
                digest[key] = "number";
            else if (val is System.Collections.IDictionary)
                digest[key] = "object";
            else if (val is System.Collections.IList)
                digest[key] = "array";
            else
                digest[key] = val.GetType().Name;
        }

        return digest;
    }
}
