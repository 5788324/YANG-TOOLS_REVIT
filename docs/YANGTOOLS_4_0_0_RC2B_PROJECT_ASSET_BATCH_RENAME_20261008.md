# YangTools 4.0.0 RC2B｜项目资产批量重命名写操作下沉

日期：2026-10-08
状态：SOURCE STATIC GO（以附带测试日志为准）；Windows/.NET/Revit Runtime NOT_TESTED；项目发布 NO-GO。

## 限定范围

基于 4.0.0 RC2A 源码，仅重构 `ProjectAssetManagerWindow.BatchRename_Click`，新增 `Core/ProjectAssets/ProjectAssetBatchRenameService.cs` typed Request/Result。原 XAML、按钮、5 个标签与弹框 BatchRenameWindow 不修改。MCP 27 命令、Rebar、Ribbon 及其余删除/导入/参数编辑事务不修改。

## 确认的原有缺陷

旧代码在 `foreach (ids)` 中 `try/catch` 吞掉每个 `elem.Name = newName` 错误，随后不检查 `Transaction.Start/Commit` 返回值而显示“成功重命名 N 个”。若部分资产不可重命名，会发生用户不知情的部分提交。

## 实现的安全边界

1. UI 在对话框确认后捕获目标 ElementId 列表、前后缀、查找替换项以及当前 Tab/Tree 节点版本；绑定旧 RevitEventHandler 的 ExternalEvent 执行，不跨线程访问 Revit API。
2. Service 无 WPF 依赖；检查 Document.Valid/ReadOnly，去重 Id，预先读取当前名称并计算目标名，目标缺失或全空则拒绝执行。
3. 没有需要修改的图元时不打开事务、不产生 Undo；有需要修改的图元时仅开启一个 Transaction（“批量重命名资产”）。
4. 任一名称赋值失败立即传播包含资产旧名称的异常并回滚，不再吞掉；Commit 状态必须为 Committed，否则报告失败。整个批次只有一次 Undo 记录。
5. UI 仅在 Service 成功返回后报告统计值，批次失败显示具体错误；窗口已关闭时不弹旧窗口 UI；用户切换 Tab/Tree 后不以旧回调覆盖新表格；无变化时不误导说有 Undo。
6. 无新增 MCP tool；仅抽出可供未来 GUI/MCP 复用的 API 上下文 Service，当前仍是 GUI 单入口。

## 静态验收边界

- Test-ProjectAssetRC2B 静态合同检查 typed Service、事件接线、单 Transaction、异常传播/回滚、UI 关闭/导航守卫以及无 XAML 改动。
- 全量 Test-*.py 测试套件、XAML 事件对照和 ZIP 重新解压测试。源码测试能发现结构回归，不构成 C# 实编译或真实 Revit Undo/GUI 证明。
- **暂未改动** 导入 Excel 按行事务、资产删除的试删/确认、单项名称修改、行格式绑定。未来单独审查，不因本次重构降低任何发布门槛。

## 下一阶段

RC2C：审查 Excel 导入复杂 TransactionGroup、多行失败策略、窗口关闭和重读 UI；限定一处改动，保留兼容。正式发布前补 Windows Build/PS AST/Installer、Revit GUI 与事务/Undo 验收。
