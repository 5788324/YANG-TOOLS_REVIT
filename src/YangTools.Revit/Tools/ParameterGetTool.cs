using YangTools.Revit.Mcp;

namespace YangTools.Revit.Tools;

public sealed class ParameterGetTool : IRevitTool
{
    public string Name => "revit.parameter.get";

    public bool IsWriteTool => false;

    public McpResponse Execute(McpRequest request)
    {
        return McpResponse.Success(Name, new
        {
            status = "planned",
            message = "等待接入 Revit 2024 Parameter 读取逻辑。",
        });
    }
}
