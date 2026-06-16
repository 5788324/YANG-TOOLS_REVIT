namespace YangTools.Revit.Revit;

/// <summary>
/// 记录 YangTools 的主线程边界：
/// HTTP 线程只接收请求，不直接访问 Revit API；
/// 真正的 Revit API 调用必须通过 ExternalEvent 或等价主线程调度执行。
/// </summary>
public static class ExternalEventPipeline
{
    public const string Policy =
        "HTTP /mcp/ -> ToolRouter -> ExternalEvent/MainThread -> Revit API -> McpResponse";
}
