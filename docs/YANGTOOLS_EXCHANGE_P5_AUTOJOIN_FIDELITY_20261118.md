# Exchange P5 — Autojoin 微调有界核验（2026-11-18）

## 输入证据和事实边界
- 原始 P4 证据：`P4_EVIDENCE.zip`，135370 bytes，SHA-256 `B675402B620EA642C4D3175039D007426FB8BAD2B4BD1CCA18CC450ADA1050CD`。ZIP 只含 2025 编译日志、运行汇总、Journal 和 9 项单元结果，**没有 810 条曲线的逐条几何比对明细**。
- Revit 2025 DLL 在原证据中只有报告的哈希、大小，**DLL 本体未附**。
- Gemini 报告 810 条存活、772 严格一致、38 经 Autojoin 微调，最大 0.119100mm。**这 38 个数值尚未被逐条独立复核**。
- 在原 Journal 中检索不到 `Infinite requestFinishDocumentTransaction loop? terminating after 666 iterations`，但有正常的 Join 失败消息及诊断事务。真实 C4 命令仍 Failed，最终图元 0。

## P5 设计决策
- 继续使用 P4 `SameGeometry` 的 `1e-6 ft` 严格比较；对不变的曲线判定方式不变。
- **只在本次线条事务存在 `ResolvedCount > 0` 的合法 DetachElements 时**，允许新建 Line 或有界 Arc 的轻微微调；整圆（无界 Arc）只接受严格原样一致。
- 上限为 `min(0.15 mm, 0.20 × Revit ShortCurveTolerance, 0.20 × 源曲线长度)`，端点双向、长度、弧心、半径、弧中点分别比较；类型、IsBound、弧法向仍保持严格。
- **超限、缺失、意外类型、互补半圆、未知失败继续回滚**，不增加故障处理重试次数。
- 所有接受的微调都写入 P1 审计 JSON 的 `audit_warnings`，包含源 SourceId、Revit ElementId、最大偏移毫米数；成功摘要含微调数量与最大值。
- 这是 **有偏移的有限容忍**，不是无损导入。发布文案不可表述为“几何与原 CAD 完全一致”。

## 下一轮运行验证
1. Revit 2025 隔离编译，核对新 DLL SHA 与加载路径；禁止复用 P4 DLL。
2. 使用哈希 `1D506D76C1AFF58C819C652482924E7634B5343B558E2BF6EE73D22B17F2E794` 的重导出 C4 MAP 在 QA 模型执行真实 `CadToRevitCommand.Execute`。
3. 需要完整保存 **810 条** `SourceId/ElementId`、修改前后 Curve 几何摘要（类型、IsBound、长度、端点，Arc 中心/半径/法向/中点）、允许偏移阈值、实际偏移、PASS/FAIL；确认报告 38 条是否均符合该上限。
4. 核对 `TransactionGroup` 真实提交、没有未知失败或 666 次循环；逐源 P1 JSON 与 `Document.GetElement()` 数量一致；原生 QAT Undo 一次恢复。
5. 如果 C4 仍失败，原样回传失败的 SourceId/ElementId 和偏移；**不要擅自继续放宽到 ShortCurveTolerance**。
6. 仅 2025 通过后再编译 2021/2022/2023/2024/2026。不得碰生产 Revit 2022/2023，勿修改 YangCAD 或原 DWG。

## 尚未处理
C4 19 个短边 Fill、10 个弧边 Fill、短引线标注，不因为 P5 放宽详图线核验就被记作恢复。整体 Release NO-GO。
