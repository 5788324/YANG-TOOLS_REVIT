namespace YangTools.Revit;

/// <summary>
/// v0.1 主线入口占位。
/// TASK-000 先固定项目名、addin、引用策略和主线程边界；
/// 真正的 Revit ExternalApplication 接入在后续正式实现中落地。
/// </summary>
public sealed class App
{
    public const string ProjectName = "YangTools";
    public const string MainVersion = "v0.1-dev";
    public const string RevitTarget = "2024";
    public const string McpEndpoint = "http://127.0.0.1:8081/mcp/";
}
