using System.Collections.Generic;

namespace YangTools.Revit.Mcp;

public sealed class McpRequest
{
    public string Tool { get; set; } = string.Empty;

    public Dictionary<string, object?> Args { get; set; } = new Dictionary<string, object?>();
}
