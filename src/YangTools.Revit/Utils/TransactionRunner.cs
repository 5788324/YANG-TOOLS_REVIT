namespace YangTools.Revit.Utils;

/// <summary>
/// Revit Transaction 封装占位。真正的事务生命周期由 DeepSeek 在 TASK-004 落地。
/// </summary>
public sealed class TransactionRunner
{
    public string Status => "planned";
}
