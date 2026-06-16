using YangTools.Revit.Mcp;

namespace YangTools.Revit.Tools;

public interface IRevitTool
{
    string Name { get; }

    bool IsWriteTool { get; }

    McpResponse Execute(McpRequest request);
}
