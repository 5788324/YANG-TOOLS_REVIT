using System;
using System.Collections.Generic;
using YangTools.Revit.Mcp;

namespace YangTools.Revit.Tools;

public sealed class ToolRouter
{
    private readonly Dictionary<string, IRevitTool> _tools;

    public ToolRouter(IEnumerable<IRevitTool> tools)
    {
        _tools = new Dictionary<string, IRevitTool>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in tools)
        {
            _tools[tool.Name] = tool;
        }
    }

    public int RegisteredToolCount => _tools.Count;

    public McpResponse Route(McpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Tool))
        {
            return McpResponse.Failure(string.Empty, "tool 不能为空。");
        }

        return _tools.TryGetValue(request.Tool, out var tool)
            ? tool.Execute(request)
            : McpResponse.Failure(request.Tool, $"未注册的 tool: {request.Tool}");
    }
}
