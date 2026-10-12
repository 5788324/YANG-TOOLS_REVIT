# YangTools 3.1.38｜Rebar R00 静态变更报告

日期：2026-09-11

## 状态

**R00 STATIC GO / WINDOWS + REVIT 2024 RUNTIME RETEST REQUIRED**

本候选只新增钢筋只读能力探针，不代表钢筋创建/修改功能已可用，也不得替代当前可靠性加固主线候选。

## 本轮新增

- `Core/Rebar/RebarInspectionModels.cs`
- `Core/Rebar/RebarInspectionService.cs`
- `Commands/RebarSelectionFilter.cs`
- `Commands/RebarInspectorCommand.cs`
- MCP broker command `inspect_rebar`
- MCP stdio tool `inspect_rebar(element_ids=None)`
- `tools/Test-RebarR00.py`
- `docs/REBAR_MODULE_PLAN_R00_20260911.md`
- `docs/DEEPSEEK_GEMINI_REBAR_R00_ACCEPTANCE_3.1.38_20260911.md`

## R00 读取范围

- Shape Driven / Free Form；
- RebarStyle；
- Schedule Mark；
- Bar Type / 直径；
- Host ElementId / UniqueId / 类别；
- Shape；
- LayoutRule / DistributionType / NumberOfBarPositions / MaxSpacing；
- Varying capability / actual varying 状态；
- Shape Driven ArrayLength / BarsOnNormalSide / Varying constraints / MultiplanarDepth；
- Constraint handle 数量、当前约束类型、目标 Element；
- Hook / Coupler / Lap（API 可用时）；
- Free Form valid server / aligned server / server GUID / unconstrained 状态；
- 第一 / 中间 / 最后 Bar Position 样本长度；
- 第一有效 Bar Position 的中心线 curve chain。

## 安全边界

- Ribbon 命令使用 `TransactionMode.ReadOnly`；
- 核心 Service 不创建 Transaction / TransactionGroup / SubTransaction；
- MCP 与 UI 共用同一 Service；
- 单次最多 500 个 ElementId；
- 不调用 Rebar 修改 API；
- `HasVariableLengthBars` 不主动 `Document.Regenerate()`，报告中明确其准确性前提。

## 静态验证

- `Test-RebarR00.py`：44/44 PASS；
- `Test-UiPanelsStatic.py`：32 Ribbon commands，0 error / 0 warning；
- 全部 `tools/Test-*.py`：PASS；
- `McpServer/server.py`：`py_compile` PASS。

## 当前无法证明

当前 Linux 环境没有 .NET SDK / Revit，因此未证明：

- Revit 2021–2027 全配置真实编译；
- Revit 2024 API 运行时成员行为；
- Free Form Server/Constraint 的项目实际状态；
- 横坡/纵坡/变截面后的约束保持；
- Standard ↔ Stirrup/Tie 的实际 `ToOtherRebar` 关系；
- MCP 在真实 Revit 主线程的返回结果。

这些必须按 `DEEPSEEK_GEMINI_REBAR_R00_ACCEPTANCE_3.1.38_20260911.md` 实机验收。
