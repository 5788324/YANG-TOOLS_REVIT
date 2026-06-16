using YangTools.Revit.Mcp;

namespace YangTools.Revit.Tools;

public sealed class ParameterSetTool : IRevitTool
{
    public string Name => "revit.parameter.set";

    public bool IsWriteTool => true;

    public McpResponse Execute(McpRequest request)
    {
        return McpResponse.Success(Name, new
        {
            status = "planned",
            supportsDryRun = true,
            message = "等待接入 TransactionRunner 与 Revit 2024 参数写入逻辑。",
        });
    }
}
