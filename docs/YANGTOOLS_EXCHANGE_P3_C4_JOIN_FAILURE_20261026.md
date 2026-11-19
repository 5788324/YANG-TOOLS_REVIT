# YangTools Exchange P3 — C4 线条连接约束安全恢复（2026-10-26）

## 发现与证据边界

- 用户转述的 C4 2026-10-26 重导出 MAP：841 项 = HATCH 71 + ARC 95 + LINE 467 + LWPOLYLINE 35 + CIRCLE 6 + DIMENSION 58 + MTEXT 109。原始 MAP、此次 Journal、诊断 JSON **未上传本轮对话**，故属于待独立审计的外部实测报告。
- 该 MAP (`..._RVT_RVT_MAP.json`, 637,435 bytes, SHA 1D506D76...) **不同于历史 C4** (634,507 bytes, SHA A0AB7D...)。短边 HATCH 集合、标注 Handle 同历史记录不完全一致，**不能仅凭实体总数相同宣称“同一拓扑”**。
- 本次 report 指线条父事务 `CAD 转 Revit - 线条` 在 Commit 期间出现两条 `Can't keep elements joined`，图元 `1059968` 对 `1059995` / `1059992`；该连接错误与 19 条短边 HATCH、1 条短标注是**不同层级的阻断**。
- 来源 report 同时指出圆弧 HATCH 10 项、短边 HATCH 19 项、短参考线标注 1 项；**P3 不修这三类不支持/不可安全创建对象，不修改源 DWG/MAP**。

## 修复策略（新增源码 P3）

1. **仅在 `CreateCurves` 新建的 Transaction** 上注册 `IFailuresPreprocessor`；不设 Revit application 全局事件。
2. 只匹配已知 join 失败定义 ID (`CannotKeepJoined` / `CannotJoinElements` / `CannotJoinElementsError`)；不用语言相关错误字符串判断。
3. 仅当失败涉及至少 2 个 **本次成功提交子事务的新建 DetailCurve**，且 Revit 明确允许 `DetachElements` 处理时，才尝试解除连接；不允许对既有对象或其它失败自动修复。
4. 对全部待处理错误先做安全审查，任一未知/不可安全解的 Error 即全事务回滚；绝不 `DeleteElements` / `DeleteWarning`。
5. **最多一轮处理**：第一次 `ResolveFailure` 后用 `ProceedWithCommit` 触发 Revit 重检，第二轮仍报错立即回滚；避免无限循环。
6. 事务提交后核查所有源 Handle→ElementId 的对应图元仍存在，且曲线类型、长度、端点、Arc 圆心和半径保持不变；任何异常均抛出并由外层 `TransactionGroup` 回滚。
7. 成功时仅记录被尝试解耦的数量和审计备注。即使代码层成功，**仍需 Revit 2025 QA 验证真实 C4 的成功提交、几何和逐 Handle 对账**。

## F1/F2 任务（必须实机）

- 先 Revit 2025 隔离编译并读回 DLL SHA。2025 F2 合格后再编译 2021–2024/2026；Revit 2023 生产 Addins 绝不能部署。
- 可丢弃 Revit 2025 模型 + 只读原工程 MAP 副本。仅使线条导入映射启用，记录**本次完整失败定义 ID、available resolutions、failing IDs**。同一失败如果不能用 `DetachElements`，应记录失败且回滚，不能硬跳过。
- 传入当前报告中 603 条曲线全部对应的 CAD Handle；运行前后从 View 读回元素数量及所有已创建 Line/Arc 几何与源 MAP 比较；**没有比对数据不称“603 全成功”**。
- 复现用户报告中的 Join 提交错误，检查是否仅针对新建 DetailCurve、是否断开约束而不删除/移动线条。任何非白名单错误必须回滚且无模型残留。
- 测试 Undo 使用原生 Revit `ID_BUTTON_UNDO`，并保留 before/after/undo。不要拿 `TransactionGroup.RollBack` 代替用户 Undo。
- 分别测试故意无法处理的 Failure、既有线段参与的 Join、标注/填充原先的短边失败，确保不会误处理。
- 不要修改 YangCAD `project.revit.prepare`，除非用完整 real+dry_run 证据证明 CAD 端存在新问题。

## 结果门禁

- 本次仅 SOURCE STATIC 候选，Windows BUILD / Revit Runtime 均 NOT_TESTED。
- 原 C4 新 MAP 的 19 个短边填充、10 个曲线边界填充、短界线标注仍单独列为**未恢复**，不得为 Join 错误补丁冒称整体转换无损。
- 未合并 main、不改生产插件，正式 RELEASE NO-GO。
