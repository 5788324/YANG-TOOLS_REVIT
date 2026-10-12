namespace YangTools.Revit.Models
{
    /// <summary>
    /// 参数窗口关闭后交还给 IExternalCommand 的下一步动作。
    /// 选择操作必须由仍处于 Revit API 上下文中的外部命令执行，
    /// 不能在 ShowDialog 窗口内部 Hide 后直接调用 PickObject(s)。
    /// </summary>
    public enum GeneratorDialogAction
    {
        Cancel = 0,
        SelectInput = 1,
        Generate = 2
    }
}
