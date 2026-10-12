# YangTools 4.0.0 RC2L｜族实例批量换型原子性与 UI 安全

日期：2026-10-10。输入 RC2K SHA `CCF3E5FA1AFD97ED413C8B3133C5D3EBDDC5467B5E7F2483ADBA18CE99E8CD6D`。

## 修改边界

- 仅 `FamilyInstanceManagerWindow.BatchChangeType_Click` 下沉至 `Core/FamilyInstances/FamilyInstanceBatchTypeService.cs`。
- `Request/Result/Replacement` 保存请求中的实例 ID、目标 TypeId、ChangeTypeId 返回的新 ElementId 和事务/回读状态；一个旧 ID 可能对应一个替换新 ID。
- 预先核验 Document、目标 ElementType、实例存在性、重复 ID、已使用类型，无实际改动不创建 Undo；同一 Revit Transaction 对所有待修改实例调用 ChangeTypeId。失败不提交任何部分结果，并明确提示不能确认回滚的情况。
- `ChangeTypeId` 可能替换图元对象，在事务内部和提交后均用有效新 ID 回读检查，不假定旧 ID 始终有效。
- UI 请求前冻结目标类型、所选实例、Tab、筛选、scope、request generation；失效的排队操作不写入模型。Revit 模型已提交但回读失败或 UI 刷新失败与真正写入失败分开提示。
- 保留已有中文 XAML/按钮、一个 DLL、原 MCP 27 个 wire 命令、Rebar/Ribbon、csproj、2021–2027 配置；未修改 BatchSetParameter、Excel/删除、其他功能。

## 验收边界

当前只有源码、静态测试与 ZIP 完整性检查；**没有 Windows Build、Revit GUI/模型读回/Undo 的实际证据，Release NO-GO**。对应运行时必须由可用 Windows/Revit 执行器验证 ChangeTypeId 的返回新 ID、族/系统不同目标类型、失败回滚、一次 Ctrl+Z 及快捷切换 UI。正式稳定版仍为 3.1.36 FINAL_E2E。

## FamilyInstanceManager 阶段性收口 / 遗留

RC2I 的类型选项查询与 UI 旧回调保护、RC2J 的逐格编辑、RC2K 的批量写参数和 RC2L 的批量换型均已做 Service 与源码合同测试，**不等同运行时验收**。Excel 导入的部分成功、删除的预演/关联删除、以及其他 FamilyManager 入口保持 backlog；ProjectAssetManager 填充区域和复制操作依旧 backlog。下一步应转向下一个高耦合窗口或做统一 Windows 编译门禁，而不是继续逐按钮无限拆分。
