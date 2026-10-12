# R04｜Shape Driven 原位修改设计

日期：2026-09-15
状态：STATIC GO，待 Windows + Revit 2024 Runtime

## 1. 目标

R04 验证并建立 Shape Driven 的第一层修改能力（M1 Parametric Update）：

- 修改现有 Rebar，而不是删除重建；
- 保持 ElementId / UniqueId / Host；
- 保持已有 Constraint 类型与目标；
- 支持 `NumberWithSpacing` 数量/间距修改；
- 支持可选 RebarBarType 修改；
- 已是 varying 的 set 在布局修改后继续保持 varying；
- 所有写操作可 dryRun、可 Undo、失败回滚。

R04 **不做**：约束重定向（M2）、Shape 拓扑变化/重建（M3）、Free Form、Hook/Coupler/Lap、碰撞。

## 2. 为什么先做修改

R00-R03 已证明创建链和约束链成立。生产项目真正高频的是设计变化后的修改：直径、数量、间距、宿主尺寸与约束关系变化。若修改层不稳定，创建工具越多，后期返工风险越大。

R04 先固定最安全的 M1：只改可参数化内容；任何导致身份、宿主或约束改变的结果都回滚。

## 3. 安全不变量

提交前必须满足：

```text
same ElementId
same UniqueId
same HostId + HostUniqueId
same constraint signature (HandleType + ConstraintType + TargetSummary)
```

若 `RequireConstraintPreservation=true`，任一约束签名变化均回滚。

BarType `ChangeTypeId` 如果返回替代 ElementId，也直接回滚；R04 禁止隐式重建。

## 4. R00 Inspector 同步升级到 R00.3

R03-B 暴露出 raw centerline 不能证明 uniform set 的真实 bar position。

R00.3 的 sample position 增加：
- Start / End / Midpoint；
- `GeometryMode`；
- 优先通过反射调用 `GetTransformedCenterlineCurves`，得到应用 BarPositionTransform / MovedBarTransform 后的实际模型位置；
- 若目标 Revit 版本不支持，退回 `RawCenterlineFallback`。

反射是为了不给 2021-2027 编译矩阵增加新的直接 API 依赖。

## 5. 服务接口

`ShapeDrivenRebarModifyRequest`
- RebarId
- ExpectedUniqueId（可选的 stale-target 防护）
- BarTypeId（可选）
- NumberOfBarPositions（可选）
- SpacingMm（可选）
- BarsOnNormalSide
- IncludeFirstBar / IncludeLastBar
- RequireConstraintPreservation=true
- DryRun=true

`ShapeDrivenRebarModifyResult`
- Before / After Inspection
- Before / After Type/Layout/Varying
- SameElementId / SameUniqueId / SameHost
- ConstraintsPreserved
- TransactionStatus

## 6. MCP

新增 typed tool：

```text
modify_shape_rebar(...)
```

默认 `dry_run=true`，不通过通用 Python 实现业务写操作。

## 7. UI

钢筋工具区增加：`原位修改 R04`

当前验证 UI：选择 Shape Driven Rebar -> 输入数量 -> 输入间距 -> Dry Run / Apply。

R04 不追求最终钢筋管理器 UI；后续稳定后再合并入口。

## 8. Runtime 硬门禁

1. R00.3 能真实读回 uniform set 的不同 bar position；
2. dryRun 不写模型；
3. `6@200 -> 8@150` 原位修改，ElementId/UniqueId/Host 不变；
4. Edge/Start/End ToCover constraint signature 不变；
5. 若源 set 为 Varying，修改后仍是 Varying，且 sample lengths 仍非等长；
6. Revit Undo 一次恢复修改前布局，而不是删除整根；
7. stale UniqueId / invalid id / bad count / bad spacing / FreeForm 必须安全失败；
8. 2024 runtime + 2025 compile-only。
