namespace YangTools.Revit.Utils;

/// <summary>
/// TASK-000 只保留最小 JSON 工具占位，避免在 net48 骨架阶段引入额外序列化依赖。
/// 真正的请求/响应序列化由 TASK-001 再统一落地。
/// </summary>
public static class JsonUtils
{
    public static string Status => "planned";
}
