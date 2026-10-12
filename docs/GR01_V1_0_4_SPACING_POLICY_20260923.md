# GR01 V1.0.4 间距策略修复

日期：2026-09-23

## 1. 触发问题

生产使用发现：区段长度约 `29130.046 mm` 时，`最大间距=200` 可 Preview，但 `最大间距=100` 返回 `REGULAR_SPACING_NOT_FOUND`。

根因不是 100 非法，而是旧自动策略把“最大间距”同时绑定到：

- 从 MaxSpacing 往下按 5mm 档位搜索；
- 尾差必须等于 0 或 `tail/main >= 0.75`。

这会使合法的“任何实际间距 <= MaxSpacing”输入因为规整档位/尾差策略被拒绝，也可能为了命中 5mm 档位而产生不必要的额外钢筋。

## 2. V1.0.4 决策

自动模式改为 `UniformWithinMax`：

```text
intervalCount = ceil(ZoneLength / MaxSpacing)
actualSpacing = ZoneLength / intervalCount
```

性质：

- `actualSpacing <= MaxSpacing`；
- 首末区段边界都保留钢筋；
- 区段内部所有自动间距相等；
- 没有自动尾差；
- 不再存在 `REGULAR_SPACING_NOT_FOUND`；
- `SpacingStepMm=5` / `MinTailRatio=0.75` 仅保留旧 MCP wire compatibility，自动模式不再参与计算。

手动 `ManualActualSpacingMm` 语义保留：按用户指定间距从区段起点排布，末端允许 remainder/tail。

## 3. 数值例子

### 29130.046mm / Max 100

- intervals = 292
- actual spacing = 99.7604315068mm
- bars = 293
- tail = 0

### 29130.046mm / Max 200

- intervals = 146
- actual spacing = 199.5208630137mm
- bars = 147
- tail = 0

### 原 12.55m 五区 fixture

区段：

- 1000 / 100 -> 10 intervals @ 100
- 4250 / 200 -> 22 intervals @ 193.181818...
- 2000 / 100 -> 20 intervals @ 100
- 4300 / 200 -> 22 intervals @ 195.454545...
- 1000 / 100 -> 10 intervals @ 100

合计：84 intervals / 85 bars。

这有意 supersede 旧的 86 intervals / 87 bars 规整策略，仅限 spacing policy；GR01 几何插值、ZAxis/ModelLine/Edge、FreeForm 创建、事务、readback、Undo 架构不变。

## 4. 兼容/指纹

- `SpacingStepMm` / `MinTailRatio` 属性与 MCP 参数保留，避免旧调用直接破坏；当前自动策略忽略它们。
- PlanFingerprint 升级内部前缀到 `GR01.V1.2|UNIFORM_WITHIN_MAX|`，并不再把两个已忽略字段作为指纹决定项。
- SpacingPlan 结果新增 `SpacingStrategy=UniformWithinMax`；Zone Plan 新增 `SpacingMode`。

## 5. Runtime 要求

因为 Station/BarCount 会变化，本改动必须重新做一次 Revit 2025 Preview/Create/readback/Undo 集成门禁。旧 Core Runtime 证据继续证明几何/事务机制，但不能直接证明新 Station 计划。
