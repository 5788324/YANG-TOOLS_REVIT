using YangTools.Revit.Mcp;

namespace YangTools.Revit.Tools;

public sealed class SelectionReadTool : IRevitTool
{
    public string Name => "revit.selection.read";

    public bool IsWriteTool => false;

    public McpResponse Execute(McpRequest request)
    {
        return McpResponse.Success(Name, new
        {
            status = "planned",
            message = "等待接入 Revit 2024 Selection API。",
        });
    }
}
