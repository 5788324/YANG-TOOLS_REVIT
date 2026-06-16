using System.Collections.Generic;
using YangTools.Revit.Mcp;

namespace YangTools.Revit.Tools;

/// <summary>
/// revit.status — 返回 YangTools 与 Revit 当前状态。
///
/// 实装字段（有真实数据源）：
///   status, projectVersion, registeredToolCount, mcpEndpoint, mcpServerRunning
///   注：mcpServerRunning 需要 SetServer() 注入 McpServer 后才能返回真实状态；
///   未注入时返回 false。
///
/// 占位字段（需要 Revit API 上下文，当前不可用）：
///   revitContextAvailable, revitVersion, hasActiveDocument, documentName, documentPath
///
/// Revit 主线程边界：
///   本 Tool 不直接访问 Revit API。未来在 Revit 插件加载后，
///   通过 ExternalEvent 在主线程取值并写入共享状态，再由本 Tool 读取。
///   当前阶段所有 Revit 依赖字段明确标记为不可用。
/// </summary>
public sealed class RevitStatusTool : IRevitTool
{
    private ToolRouter? _router;
    private McpServer? _server;

    public string Name => "revit.status";

    public bool IsWriteTool => false;

    /// <summary>
    /// 由组合根在 ToolRouter 创建后调用，建立反向引用。
    /// </summary>
    public void SetRouter(ToolRouter router)
    {
        _router = router;
    }

    /// <summary>
    /// 由组合根在 McpServer 创建后调用，用于读取真实运行状态。
    /// </summary>
    public void SetServer(McpServer server)
    {
        _server = server;
    }

    public McpResponse Execute(McpRequest request)
    {
        int toolCount = _router?.RegisteredToolCount ?? 0;

        var data = new Dictionary<string, object?>
        {
            // ── 实装字段（有真实数据源）──
            ["status"] = "ok",
            ["projectVersion"] = App.MainVersion,
            ["registeredToolCount"] = toolCount,
            ["mcpEndpoint"] = App.McpEndpoint,
            ["mcpServerRunning"] = _server?.IsRunning ?? false,

            // ── Revit 上下文字段（占位，不可用）──
            // 原因：TASK-002 阶段尚未接入 Revit ExternalApplication；
            // 这些字段需要 Revit API 在主线程取值，当前返回占位。
            ["revitContextAvailable"] = false,
            ["revitVersion"] = null,
            ["hasActiveDocument"] = false,
            ["documentName"] = null,
            ["documentPath"] = null,
        };

        return McpResponse.Success(Name, data);
    }
}
