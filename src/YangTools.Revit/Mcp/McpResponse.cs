using System.Collections.Generic;

namespace YangTools.Revit.Mcp;

public sealed class McpResponse
{
    public bool Ok { get; set; }

    public string Tool { get; set; } = string.Empty;

    public object? Data { get; set; }

    public string? Error { get; set; }

    public List<string> Warnings { get; set; } = new List<string>();

    public static McpResponse Success(string tool, object? data)
    {
        return new McpResponse
        {
            Ok = true,
            Tool = tool,
            Data = data,
            Error = null,
        };
    }

    public static McpResponse Failure(string tool, string error)
    {
        return new McpResponse
        {
            Ok = false,
            Tool = tool,
            Data = null,
            Error = error,
        };
    }
}
