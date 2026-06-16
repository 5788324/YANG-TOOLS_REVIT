namespace YangTools.Revit.Utils;

/// <summary>
/// MCP 操作日志占位。真正的 JSONL 写入格式由 TASK-003 固定。
/// </summary>
public sealed class OperationLogger
{
    public string Status => "planned";
}
