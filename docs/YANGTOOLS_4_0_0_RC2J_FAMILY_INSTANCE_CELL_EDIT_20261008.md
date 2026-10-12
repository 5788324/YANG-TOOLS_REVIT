# YangTools 4.0.0 RC2J｜FamilyInstanceManager 逐格参数编辑与 UI 静态加固

日期：2026-10-08

## 范围与基线

唯一输入 RC2I `YangTools_Project_4.0.0_Source_RC2I_FAMILY_INSTANCE_READ_UI_STATIC_20261008.zip`（SHA-256 `493DC091EE00AC2BEB04BD251EA369E4D20C4838F1D234FC8A76FE16FC25B42C`）。RC2J 仅修改 `InstanceDataGrid_CellEditEnding` / `SystemDataGrid_CellEditEnding` 对应单元格文本参数编辑流程；不改换型、Excel、删除等写操作，不修改任何 XAML 文件及 MCP、Rebar、Ribbon、csproj。

## 问题与修改

1. 旧代码在目标图元或参数不存在、只读时静默 return；新 Service 明确返回异常和用户可见提示。
2. 旧代码捕获事务异常但不检查回滚状态。新 `FamilyInstanceCellEditService` 使用一个 `Transaction`，检查 `Start` / `Commit`，写入失败尝试 `RollBack`，无法确认回滚时明确报告。
3. 旧代码可能把提交成功后回读失败误作整体写入失败；新 Result 单独标识 `ReadbackConfirmed`、`ReadbackWarning`，避免用户重复写入。
4. 保留两类表格原先 `UpdateSourceTrigger=Explicit` 的动态参数列绑定；只有模型写入及回读确认后才更新 VM。
5. ExternalEvent 请求冻结 InstanceId、StableParameterKey、输入值；绑定窗口 `_doc`，拒绝关闭/切 Tab/重筛选/过期行、同一单元格较旧请求。
6. 参数写入统一使用原先的 `StableParameterKey.Resolve` 和 `ParameterValueHelper.TrySetFromDisplayValue`，继续支持项目显示单位与同名参数稳定身份。

## 静态验收

- `Test-FamilyInstanceRC2J.py`：94/94 PASS，检查 Service、事务、UI 绑定、回读、错误恢复、两种表格及原功能兼容。
- `tools/Test-ReliabilityHardening.py` 的 2 条实例逐格编辑断言对齐新 Service；`tools/Test-FamilyManagementV2.py` 的 1 条 Key 写入断言对齐新 Service，原校验均保留。
- 工作区全量 49/49 suites PASS；ZIP 独立解压后的结果以独立日志为准。
- 无 Windows .NET/MSBuild/Revit 执行器，未进行编译、加载、界面点击、实例参数读回与真实 Ctrl+Z。因此 `4.0.0 SOURCE STATIC GO / RELEASE NO-GO`，已发布稳定版仍为 3.1.36 FINAL_E2E。

## 遗留任务

族实例管理器批量换型、批量写参数、Excel 导入、删除仍有潜在局部提交/级联/ UI 时序风险，需另批审查；项目资产管理器填充区域/复制功能遗留未关闭。所有当前/未来功能 MCP 可调用的架构目标仍需单独覆盖验收：本批仅保持已有 MCP 27 命令兼容，不宣称新服务已有独立 MCP 命令。
