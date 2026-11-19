# Exchange P4 — 整圆几何断言最小修复（2026-11-17）

## 基线与范围

- 基线：P3 Draft PR #7，Commit `259776749934feb64df2e9381670d19f17b450f0`，649 文件。
- P3 实测：2021–2026 编译日志均 Exit 0 / 0 errors；Revit 2025 Join Failure 具备单次 DetachElements 处理证据，但整圆的 `GetEndPoint` 抛 `The input curve is not bound`，C4 仍整体回滚。
- 本轮仅修改 `CadToRevitJoinFailurePreprocessor.SameGeometry`，保留 P3 的严格失败白名单、有限重试及回滚策略。

## 修复逻辑

1. 检查类型、`IsBound` 一致性与曲线长度。
2. 对 `Arc`（含整圆）核对中心、半径、法向平面。允许法向反向，不允许不共面。
3. 两个 `IsBound=false` 的 `Arc` 在上述几何一致后直接 PASS，不读取 `GetEndPoint`。
4. 两个有界圆弧继续核对中点及起终点（可反向）；两条相对的半圆即使端点与长度相同也不能判为相同。
5. 对非 `Arc` 的无界曲线安全拒绝，不猜测其相同性。

## Gemini 最小 F1/F2 测试

- F1 只先构建 2025：`dotnet build -c 2025 -p:Platform=x64 -p:DeployAfterBuild=false -p:RunPostBuildEvent=Never -m:1`；源码必须匹配本 P4 ZIP，DLL SHA 现场计算。
- F2 在 **独立 Revit 2025 QA / 已保存的可丢弃 RVT** 使用当前 C4 重导出 MAP（SHA `1D506D76C1AFF58C819C652482924E7634B5343B558E2BF6EE73D22B17F2E794`）。原有生产 Revit 2022/2023、DWG 与 Addins 不可改动。
- 先单独验证：完整圆（相同几何 PASS、不同半径/中心/平面 REJECT）、有界圆弧相同 PASS、互补半圆 REJECT、线段反向 PASS、混合 IsBound REJECT，均不抛异常。必须是真实 Revit API 调用，报告不能将 Python 模拟当作 Runtime PASS。
- 然后执行真实 `CadToRevitCommand.Execute`，观察 603 曲线的父事务提交、P3 Failure 预处理器的 `ResolvedCount`、原始 Journal 与错误；避免在 Revit Failure 处人为自动点确定后隐去故障。
- **必须检查** Journal 是否出现 `Infinite requestFinishDocumentTransaction loop? terminating after 666 iterations`。P3 证据确实含该警告，本轮修复不保证消除。
- 验证 6 个整圆保持原半径/中心/法向、所有创建曲线 Handle→ElementId 存在、一次原生 Undo 可撤销、19 个短边填充/10 个曲线边填充/短标注仍被保守拒绝和报告，不能写成恢复成功。
- 若 2025 成功，再集中编译 2021/2022/2023/2024/2026；不用重复 HF2 Ribbon/其他工具 GUI。

## 验收门禁

SOURCE STATIC GO 不等于 BUILD GO 或 Runtime GO。P4 不改其他模块。若 C4 仍出现 666 次失败循环，不要无上限重试，不要自动吞掉任何新 Failure，回传原始日志后再评估 P5。整体 Release NO-GO。
