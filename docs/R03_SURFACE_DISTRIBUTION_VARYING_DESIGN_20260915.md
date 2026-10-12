# R03 设计｜Surface Distribution + Varying Length

日期：2026-09-15
状态：STATIC GO / Revit 2024 Runtime Required / RELEASE NO-GO

## 1. 目标

R03 只解决 Shape Driven 的下一层问题：

1. 钢筋集**沿宿主平面内方向分布**，不再沿厚度方向分布；
2. 钢筋中心线保持 Cover；
3. 起点/终点由两个边界 Cover 面控制；
4. 可启用 Revit 原生 Varying Length，让各 bar position 按约束重新求解长度；
5. 保持 UI/MCP 共用同一 Service；
6. 保持 dryRun / Transaction / rollback / R00 readback。

不包含 Free Form、曲面、Hook、Coupler、Lap、碰撞、BS8666。

## 2. 三面约束模型

用户/调用方提供同一 Rebar Host 的三个 exposed PlanarFace：

- `CoverFace`：控制钢筋深度；
- `StartBoundaryFace`：控制 StartOfBar；
- `EndBoundaryFace`：控制 EndOfBar。

以及 Cover 面上的两个 seed point，用于定义单筋方向。

## 3. Primary Plane 与分布方向

定义：

```text
barDirection       = 单筋中心线方向
coverNormal        = Cover Face 法向
distributionNormal = normalize(barDirection × coverNormal)
```

因此：

```text
distributionNormal · coverNormal  ≈ 0
distributionNormal · barDirection ≈ 0
```

`distributionNormal` 同时作为 `Rebar.CreateFromCurves(... normal ...)` 的 normal，使 Shape Driven set 的分布方向落在 Cover 面内。

R03 Result 会返回上述向量与两个 dot-product 诊断值。

## 4. 为什么 R03 用 Edge -> ToCover

直筋的 Edge handle 控制单个线段，并在 Rebar primary plane 内沿线段法向移动。

R03 的 primary plane 由 `barDirection` 与 `coverNormal` 张成，因此 Edge handle 的可动方向对应 Cover 深度方向。故：

```text
Edge       -> CoverFace ToCover
StartOfBar -> StartBoundaryFace ToCover
EndOfBar   -> EndBoundaryFace ToCover
```

这与 R02 用 RebarPlane -> ToCover 的验证目的不同。

## 5. 初始中心线

初始钢筋中心线不直接贴混凝土面，而按：

```text
中心线偏移 = Cover + 0.5 × BarModelDiameter
```

先根据三个 Cover 面计算一条合法 seed bar，再创建 Rebar，并用 Constraint 接管后续几何更新。

如果任一 Edge/Start/End 的 ToCover preferred constraint 建立失败，整笔 Transaction 回滚，禁止留下“看似正确但实际是死 XYZ”的钢筋。

## 6. Varying API 兼容层

R03 新增 `RebarApiCompatibility`，版本差异不得散落业务代码。

### Preferred Constraint

- Revit 2021–2024：`SetPreferredConstraintForHandle(handle, constraint)`
- Revit 2025+：`SetPreferredConstraint(constraint)`

### Varying Length

- Revit 2021–2024：`Rebar.DistributionType = VaryingLength / Uniform`
- Revit 2025+：`RebarShapeDrivenAccessor.UseRebarConstraintsToProduceVaryingBars`

R00 Inspector 同时改为反射读取旧 `DistributionType`，并在新版本读取 accessor varying 状态，减少版本硬依赖。

## 7. R03 接口

UI：

```text
钢筋工具区 → 表面变长 R03
```

MCP：

```text
create_shape_rebar_varying_on_face(...)
```

默认 `dry_run=true`。

核心参数：

- hostId
- cover/start/end face stable references
- bar type
- cover face seed start/end
- numberOfBarPositions
- spacingMm
- enableVarying
- dryRun

## 8. Runtime 硬门禁

### A. Fresh Build / SHA

- Revit 2024 build 0 error；
- 2025 configuration 也必须至少编译一次，用来验证兼容层另一分支；
- Build=Deploy=Loaded SHA；
- 2024 journal 与真实 Loaded path。

### B. 平面梯形宿主｜Uniform 控制组

- 创建结构 Floor/Slab 梯形夹具；
- 使用 Cover + 两个边界面；
- `enableVarying=false`；
- 确认钢筋集各 bar position 沿 Cover 面内展开；
- `abs(distributionNormal · coverNormal)` 接近 0；
- 不允许沿板厚方向展开。

### C. 平面梯形宿主｜真正 Varying

- `enableVarying=true`；
- Edge/Start/End 三个 preferred constraint 均为 ToCover；
- R00 readback 中 varying 状态正确；
- first/mid/last 三个 sample 中至少两个长度差 > 2 mm；
- `HasVariableLengthBars=true`（API 可用版本）；
- 同一 Rebar set ElementId。

### D. 斜平面宿主

- 用结构 Floor slope 创建 inclined planar face；
- 创建 R03 set；
- Cover 中心线偏移正确；
- 分布方向仍位于斜面内；
- 至少读取 first/mid/last sample。

### E. Undo

对最后一次 R03 产品创建执行一次 Revit `PostableCommand.Undo`：整个 set 必须一次撤销。

### F. 错误矩阵

至少验证：

- invalid host
- 空 stable reference
- face 来自其他 host
- 非 exposed face（如可构造）
- seed 起终点重合
- seed 与边界平行导致无交点
- count < 2
- spacing <= 0

失败后不留半成品。

## 9. 不应误判的情况

启用 varying 并不保证一定出现不同长度。如果 start/end 边界对 distribution path 是平行等效关系，各 bar 仍可能等长。这不是 API 失败。

因此 Runtime 必须使用梯形/斜边界夹具证明“真正变长”。

## 10. 下一阶段预留

只有 R03 Runtime 通过后，再进入：

- R04：复杂坡度/尺寸变化回归与 constraint re-solve；或
- Free Form 基础探针（视 R03 结果决定）。
