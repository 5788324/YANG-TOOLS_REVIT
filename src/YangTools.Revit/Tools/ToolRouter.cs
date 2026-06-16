using System;
using System.Collections.Generic;
using YangTools.Revit.Mcp;

namespace YangTools.Revit.Tools;

/// <summary>
/// 正式 ToolRouter。
///
/// 请求链路：
///   McpServer.Dispatch() → ToolRouter.Route() → IRevitTool.Execute()
///
/// 分发规则：
///   - tool 为空 → 标准错误
///   - tool 未注册 → 标准错误
///   - tool 已注册 → 调用 IRevitTool.Execute() 返回 McpResponse
///
/// Revit 主线程边界：
///   Route() 本身在 HTTP 线程上执行，不直接访问 Revit Document。
///   Tool 内部如需 Revit API，必须通过 ExternalEvent 投递。
/// </summary>
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
