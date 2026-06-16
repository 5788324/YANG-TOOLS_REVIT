using YangTools.Revit.Tools;

namespace YangTools.Revit.Mcp;

/// <summary>
/// 统一 MCP HTTP 入口占位。HTTP listener 与 Revit 线程协同逻辑由后续任务实现。
/// </summary>
public sealed class McpServer
{
    private readonly ToolRouter _toolRouter;

    public McpServer(ToolRouter toolRouter)
    {
        _toolRouter = toolRouter;
    }

    public string Endpoint => App.McpEndpoint;

    public McpResponse Dispatch(McpRequest request)
    {
        return _toolRouter.Route(request);
    }
}
