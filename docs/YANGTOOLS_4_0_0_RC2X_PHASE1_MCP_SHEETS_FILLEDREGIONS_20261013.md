# YangTools 4.0.0 RC2X｜第一批：MCP 超时、图纸 Excel 与填充区域可靠性

日期：2026-10-13。来源：RC2W 唯一 canonical 源码，SHA-256 `EC1013D53E223E42214DC066D267341056523B7659003368A74EF92B54C5B55D`。

> **源码静态改造，不是可安装发布版。** 本批没有 Windows .NET 编译、Revit Loaded、模型操作、WPF 实机、Ctrl+Z 证据；4.0.0 RELEASE NO-GO，正式稳定版本仍 3.1.36 FINAL_E2E。源 ZIP 的最终外层 SHA 放在单独 Acceptance 中，防止自引用。

## 目的及范围

用户明确恢复开发：由 ChatGPT 负责架构/开发/测试/验收，不要求用户懂代码或做人工测试；可按实际风险决定修补、重构或局部重写。RC2X 先按前一轮独立对比报告确认的实际高风险着手，不扩大 UI/Ribbon 功能，不新增 MCP 命令，不更改 27 条 MCP wire 字段、Rebar、installer、csproj。

## 1. MCP 执行器异步状态

### 老行为/风险

`RevitExecutionBroker.AwaitWithTimeoutAsync` 原来无条件完成超时任务，在 ExternalEvent 已开始 `Dispatch` 时也给出失败；其后 Revit 操作仍可能提交，客户端却误认为没有执行。后台 pending 被提前减一，完成结果被丢失。

### 本批修改

- 请求队列变成原子三状态 `0 queued -> 1 executing -> 2 terminal`。`TryCancelBeforeStart` 只允许在 queued 状态；`TryComplete` 只由已启动的 Revit event handler 完成。
- **排队超时**：将请求标为取消；后续 event handler 不再执行；返回明确“尚未开始，已取消”。
- **执行中超时**：不干预进行中的 Revit API；保留真实请求直至主线程完成，客户端使用原有 `success/result/error/durationMs` wire 响应，但 `error` 明确“结果未知、禁止自动重试”，附 operationId。
- 实际 `Dispatch` 后，无论成功、失败或异常，通过 `DiagnosticLog.WriteProductOperation` 记录 operationId 和 `DISPATCH_RETURNED_SUCCESS` / `DISPATCH_RETURNED_ERROR` / `DISPATCH_THROWN`。这里的“返回成功”**不等于任何模型事务已成功提交**；需要各业务 Tool 自身读回。
- `CurrentCommand` / `LastCommand` / `PendingCount` 原状态接口保留。**没有引入完整的查询历史操作结果工具或跨会话幂等键**，后续仍需建设。

### 仍需实机负向测试

排队中超时、真正运行中超过 60 秒、网关 Stop 时取消、先完成后超时的并发时序；确认 operationId 与磁盘日志关联；请求超时后用户/AI 不重复发起模型写操作。

## 2. SheetManager 图纸 Excel 整批导入

- 新增 `Core/Sheets/SheetExcelImportService.cs`（typed Request/Result/Service），窗口只负责 Excel 读取、文档/关闭校验、请求派发和 UI 状态。Excel 行复制后通过 ExternalEvent 在目标文档执行。
- **语义调整（有意）**：原来“每行独立 Transaction 成功行继续提交”，现改为**所有非空行同一 Transaction**，任何非空无图号、重复图号、目标重复、参数缺失/只读/重名/写入失败均整批拒绝或回滚；不允许“某些参数没写成却提示行成功”。完全空行允许跳过。
- 成功后核对图纸 ID、图号、图名，并区分“已提交但后续读回/窗口刷新失败”。所有修改由一次 Ctrl+Z 撤销；空 Excel 不创建 Transaction/Undo。
- 对同名重复项目参数执行 `GetParameters` 唯一性检查，避免 `LookupParameter` 随机选中一个。这意味着重复参数未清理的模型可能被拒绝导入，优先保护数据准确性。
- 当前只确认了图纸身份/名称的写前和提交后读回；其余复杂类型参数写入由 `TrySetFromDisplayValue` 的成功值做最低门槛，**尚未实现每列格式化后的逐值精确回读**，不能声称已保证所有字段完全一致。

### Windows/Revit 验收条件

成功：同时新建+修改多张图纸 → 只产生一次 Undo，回滚后恢复原图纸；失败：单行不存在/只读/同名多参数/重复图号 → 整批不修改；Excel 文件异常 → 不触碰模型；工作中关闭窗口/切项目 → 不在错误文档写入；无修改 → 无 Undo。测试记录原始图元 ID、图纸数量、参数、Journal、Undo。

## 3. ProjectAsset 填充区域逐格编辑

- 新增 `Core/ProjectAssets/ProjectAssetFilledRegionEditService.cs`，名称与遮罩两种操作均采用 Request→Service→Result、原文档、单 Transaction、Commit/Rollback 检查、读前读后确认；no-op 不创建 Undo。
- WPF `FilledRegionDataGrid_CellEditEnding` 只捕获用户编辑值，不在模型事务成功前修改 ViewModel；窗口关闭、Tab/旧列表、重复编辑请求均阻断过期回调。
- 失败提示与“已提交但 UI 更新失败”分离；保留原中文 XAML，未重新设计整体视觉。
- **未迁移**：填充前景/背景图案、颜色、复制等旧实现仍需后续 RC2X 综合处理。

## 4. 旧测试更新的缘由

`tools/Test-ReviewFixes.py` 原契约硬编码 `SheetManagerWindow` 中包含旧 `TransactionGroup` 按行提交。新实现不再合法匹配旧字面串，旧测试的真正意图是“批量导入不留下无法撤销的半批修改”。因此将该合同更新为：窗口必须调用 `SheetExcelImportService.Execute`，服务必须有整批单 `Transaction`，不是移除校验或标记跳过。

增加 `tools/Test-RC2X-McpSheetFilledRegion.py`，检查队列 CAS 状态、MCP 响应字段不变、批量预检/回滚、UI 过期状态、No-op、读回等源码合同。**这些断言仅检验源代码中的结构和关键语句，不能模拟 Revit API/真实并发。**

## 5. 保留范围

- 不修改 `McpGateway.cs`、4 个 MCP 路由领域处理器、Python MCP wire、Rebar、Ribbon、installer、csproj。
- 不更改现有 MCP 请求体 `command/args` 和响应 `success/result/error/durationMs` 的字段；超时 `error` 更明确。
- 不清理旧功能、不新增按钮，不恢复 AI/摸鱼窗口。
- 不能据静态测试数字宣称跨 Revit 2021–2027 已兼容。

## 6. RC2X 剩余待办

1. Windows 2024/2025 Build/Deploy/Loaded SHA、异常路径、GUI 与 Undo 真实证据（P0）。
2. MCP operationId 的可查询终态、幂等操作/客户端安全重试策略（P0/长期 MCP）。
3. 项目资产 FillPattern/颜色/复制、FamilyInstance Excel/删除、SheetManager 其他旧事务、BatchTaskEngine 文件生命周期、中文检查、族管理、剩余微工具 UI 审查（P1）。
4. 后续统一做中文 UI 视觉与 DPI/缩放/控件实际交互审查，不能仅靠 XAML XML 解析代替。

本批完成的是 RC2X 的第一批风险修复，**不是 RC2X 全部工作收口，更不是 4.0.0 发布完成**。
