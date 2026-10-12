# YangTools 4.0.0 RC2U｜P1 CAD 粘贴 / CAD 转图纸 / 按线剖面联合安全审查

日期：2026-10-12。基线：RC2T `10D249EE9A2333F1BA5EF55682FD18C015E0B400E62239BEBD237D31C7A72958`。

## 实现范围（保留旧业务算法）

### 从 CAD 粘贴

- 模态设置窗口确认之后，对原始 Document、ActiveView.Id、文档只读状态再次检查。文档/视图已变更则取消，不创建工作副本及模型图元。
- 失败时检查 Transaction.RollBack 的返回值；未经证实回滚不得报告“已经回滚”。导入失败统一在事务 catch 中回滚。与旧文案不同，诊断日志分别标识 FAILED_ROLLEDBACK / FAILED_ROLLBACK_UNCONFIRMED。
- Transaction Commit 为 Committed 后，按 ElementId 回读 `ImportInstance`；回读未确认但模型已提交时提示用户先检查模型，避免重复导入和重复 Undo。选中/刷新失败与导入成功分开提示。
- 保留 DWGImportOptions 释放、工作副本 finally 清理及 Shared 失败**不**自动降级到原点对齐。原有 ImportUnit/ColorMode/ThisViewOnly 不变。

### CAD 转 Revit 图纸

- ShowDialog 后、最终确认后双重检查原始项目和 View.Id，禁止过期请求写入其他活动上下文。
- 结果含 Failures 时确认对话框标题明确“部分完成”；原逐对象 SubTransaction/失败明细与成功项一次 Undo 的既有设计保留，**并非本轮改成全量原子转换**。

### 按线创建剖面

- 只读/族文档拒绝；短于 `Document.Application.ShortCurveTolerance` 的直线明确拒绝。
- 垂直线参考向量退化时先检查零向量后 Normalize，并用 X/Y 备选轴推导，避免 CrossProduct 直接 Normalize 抛错。
- 单 Transaction 的 Start/Commit/失败 RollBack 状态检查；提交后按 ViewSection.Id 回读，回读失败不误报已回滚。

## 额外旧代码风险（本轮不宣称修复）

- CAD→Revit 的逐行 SubTransaction 允许部分成功；对于不允许部分结果的业务，应在未来明确独立“严格原子模式”需求。本轮保留旧语义并明确提示。
- CAD→Revit 标注 `GeometryCurve.Reference`、复杂多段线弧段、FilledRegion 不同 Revit 版本和引用稳定性：需要模型级运行检验。
- CAD 粘贴 DWG 来源的实际单位/共享坐标基准无法由当前 Revit API 自动证明正确；不能用静态检查宣称共享坐标无偏移。
- 剖面方向、深度和线投影后的外包范围需在真实模型核验，尤其非水平/竖直、极短曲线、旋转视图。
- 当前没有 Windows dotnet/Revit 运行环境：C# Build / Revit Loaded / GUI / Ctrl+Z 均 NOT_TESTED。正式发布 **4.0.0 RELEASE NO-GO**。

## 验收原则

- 新增 `tools/Test-P1CadViewsRC2U.py` 源码与纯数学专项。全量旧 Test-*.py 逐套独立执行，并从打包 ZIP 全新解压再运行。
- 不改 MCP 27 旧 wire、钢筋、Ribbon、installer、csproj；无 Git 推送。用户无需人工测试。正式稳定 3.1.36 FINAL_E2E 不变。
- 下一轮 P1：其余视图工具/文本/标注横向 UI/事务风险，再进入项目微工具和管理工具；与 RC2U 不重复拆单个按钮。
