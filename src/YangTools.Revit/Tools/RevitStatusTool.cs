using YangTools.Revit.Mcp;

namespace YangTools.Revit.Tools;

public sealed class RevitStatusTool : IRevitTool
{
    public string Name => "revit.status";

    public bool IsWriteTool => false;

    public McpResponse Execute(McpRequest request)
    {
        return McpResponse.Success(Name, new
        {
            status = "planned",
            revitTarget = App.RevitTarget,
            mcpEndpoint = App.McpEndpoint,
        });
    }
}
