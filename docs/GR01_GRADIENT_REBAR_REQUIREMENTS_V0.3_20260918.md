# YangTools｜GR01 渐变钢筋集（Managed FreeForm）功能需求 v0.3

日期：2026-09-18  
状态：DESIGN REVIEW PATCHED / POC READY / NOT IMPLEMENTED  
主目标版本：Revit 2025  
用途：交给其他 AI 做开发前技术审查；不要把本文中的未验证 API 路线写成 PASS。

---

## 1. 功能目标

用户先在 Revit 中创建两根真实的“控制钢筋”，分别代表最终渐变钢筋集的第一根和最后一根几何；再选择一条分布路径，并定义若干分区及每区最大间距。

YangTools 根据路径 Station 计算所有钢筋位置，在两根控制钢筋之间插值生成全部钢筋，最终创建为：

**1 个 Rebar Element / 1 个完整 Free Form Rebar Set。**

最终结果必须重新创建第一根和最后一根。原来的两根控制钢筋只作为输入模板，不属于结果集；用户确认结果正确后可自行隐藏或删除控制筋。

---

## 2. 典型场景

桥墩/墩柱横截面沿高度线性扩大或缩小：

- 底部横向钢筋轮廓较大；
- 顶部横向钢筋轮廓较小；
- 中间横向钢筋连续渐变；
- 沿高度存在普通区、加密区；
- 区段长度往往不是间距整数倍；
- 设计给的是“最大间距”，实际希望自动得到 200、195、190、180、100 等规整数，而不是 193.1818mm。

Shape Driven 对复杂渐变截面可能无法稳定建立所需约束；Raw Free Form 能表达几何，但原生分布和保护层管理弱。本功能在 Revit 2025 下补一层 Managed FreeForm 产品逻辑。

---

## 3. 已锁定的产品规则

### 3.1 两根控制筋

输入：

- StartControlRebar
- EndControlRebar

V1 要求：

- 都是普通 Rebar；
- 都是 single-position；
- Host 相同；
- BarType 相同；
- RebarStyle 相同；
- 中心线拓扑可配对；
- 不允许已有 Coupler；
- Hook/Termination 是否支持，由 POC 决定；若无法可靠复制，V1 明确拒绝，不允许静默丢失。

### 3.2 最终只允许一个钢筋集

如果计算结果为 N 根：

- Position 0 = StartControlRebar 几何；
- Position N-1 = EndControlRebar 几何；
- 中间 Position 全部由插件生成。

必须输出：

- 1 个 Rebar Element；
- N 个 physical bar positions。

禁止为了实现方便创建 N 个独立 Rebar Element。

### 3.3 Distribution Path 与 Bar Orientation 分离

Distribution Path 只负责：

- 路径长度；
- Station；
- 钢筋“放在哪里”。

钢筋自身形状和方向由两根控制筋插值决定。

因此不采用“路径切线自动决定钢筋方向”的隐式逻辑，避免 3D Path 中钢筋垂直路径而实际工程要求保持其他方向的问题。

### 3.4 V1 路径类型

支持：

- ModelLine；
- 模型几何 Edge。

内部统一为 DistributionPath。

V1 只接受**直线路径**。Arc / Spline / 多段 3D Path 后续再做。

---

## 4. Spacing Zone 模型

Core 不写死“普通/加密”两个类型，而统一使用：

```text
SpacingZone
- LengthMm
- MaxSpacingMm
- OptionalManualActualSpacingMm
```

UI 可以显示“普通”“加密”快捷标签，但执行只看 MaxSpacingMm。

这样以后可以支持任意项目规则，例如：

- 100mm；
- 125mm；
- 150mm；
- 180mm；
- 200mm。

---

## 5. 示例

总路径：

```text
1.00 + 4.25 + 2.00 + 4.30 + 1.00 = 12.55m
```

示例：

| Zone | 长度 | 最大间距 |
|---|---:|---:|
| Z1 | 1000mm | 100mm |
| Z2 | 4250mm | 200mm |
| Z3 | 2000mm | 100mm |
| Z4 | 4300mm | 200mm |
| Z5 | 1000mm | 100mm |

规则：

- 每个 Zone 起点、终点必须有筋；
- 相邻 Zone 共用边界筋，不重复；
- 所有实际间距不得超过对应最大间距；
- 主间距优先规整；
- 允许一个较小尾差，不强制全部均分成小数。

目标示例：

```text
Z1: 100 × 10
Z2: 195 × 21 + 155
Z3: 100 × 20
Z4: 180 × 23 + 160
Z5: 100 × 10
```

总 interval：

```text
10 + 22 + 20 + 24 + 10 = 86
```

总钢筋：

```text
87 根
```

最终仍是 1 个 Rebar Element。

---

## 6. 规整间距算法（建议默认，待其他 AI 审查）

输入：

```text
ZoneLength = L
MaxSpacing = Smax
```

候选间距从大到小按 5mm 搜索：

```text
Smax
Smax - 5
Smax - 10
...
```

默认：

```text
SpacingStepMm = 5
MinTailRatio = 0.75
```

对每个候选 S：

```text
fullCount = floor(L / S)
remainder = L - fullCount * S
```

接受条件：

```text
remainder == 0
或
remainder / S >= MinTailRatio
```

选择第一个满足条件的最大 S。

示例：

### 4250 / Max 200

```text
200 -> tail 50  -> 0.25，拒绝
195 -> tail 155 -> 0.795，接受
```

结果：

```text
195 × 21 + 155
```

### 4300 / Max 200

```text
200 -> 100/200 = 0.50，拒绝
195 -> 10/195 ≈ 0.05，拒绝
190 -> 120/190 ≈ 0.63，拒绝
185 -> 45/185 ≈ 0.24，拒绝
180 -> 160/180 ≈ 0.89，接受
```

结果：

```text
180 × 23 + 160
```

注意：5mm 和 0.75 是产品排布策略，不是结构规范数值。请其他 AI/工程人员重点审查。

允许用户对单个 Zone 手动指定 ActualSpacing，但必须：

```text
0 < ActualSpacing <= MaxSpacing
```

---

## 7. Station 计算

直线 DistributionPath：

```text
P0 = Path Start
P1 = Path End
D = normalize(P1 - P0)
```

为两根控制筋分别计算稳定参考点 Cstart / Cend，建议基于 transformed centerline 几何中心。

投影：

```text
Sstart = dot(Cstart - P0, D)
Send   = dot(Cend   - P0, D)
```

若方向相反，允许自动 PathReversed，但不得交换两根控制筋的业务语义。

有效长度：

```text
DistributionLength = abs(Send - Sstart)
```

所有 Zone Length 合计必须与 DistributionLength 在容差内一致，建议 V1：

```text
±1mm
```

不一致时 DryRun 失败，不自动偷偷拉伸 Zone。

---

## 8. 控制筋几何匹配

读取两根控制筋 transformed centerline。

V1 建议支持：

- Line ↔ Line；
- Arc ↔ Arc。

要求：

- curve count 相同；
- curve type 顺序相同；
- 曲线连续；
- 无 <1mm 退化段；
- Spline / NURBS / Ellipse 暂不支持。

自动比较正序和反序匹配误差，选择更合理的端部对应关系，并返回：

```text
EndControlReversed = true/false
```

---

## 9. 中间钢筋插值

目标 Station 为 S：

```text
t = (S - Sstart) / (Send - Sstart)
```

其中：

```text
0 <= t <= 1
```

首根：

```text
t=0
```

末根：

```text
t=1
```

### Line

插值两端点：

```text
A(t)=A0+t(A1-A0)
B(t)=B0+t(B1-B0)
```

### Arc

不直接插值 Radius。

建议分别插值：

- Start Point；
- Mid Point；
- End Point；

然后用三点重建 Arc。

若三点退化导致 Arc 创建失败，整次创建 rollback；禁止静默拉直成 Line。

---

## 10. Revit 2025 Engine 策略

正式开发前先做一个很小的 Native Capability POC，比较：

1. Revit 2025 Surface Free Form Distribution；
2. Revit 2025 Aligned Free Form Distribution；
3. 现有 R07/R08 explicit/raw Free Form。

POC 只回答：

- Revit 2025 API 能否可靠程序化创建原生 Surface/Aligned Free Form；
- 是否能保持一个 Rebar Element；
- 是否能表达控制筋 A → B 的完整渐变；
- 是否能表达非均匀 Zone station；
- 是否支持可靠 readback / Undo / update；
- 是否真正保留 Native Cover/Constraint；
- 是否需要 IRebarUpdateServer。

如果原生 Surface/Aligned 不能同时满足“一组一个 Element + 非均匀 Zone + 自动化 API + readback”，V1 使用现有 R07/R08 已验证的 Explicit ManagedFreeForm 路线。

不得把 raw Free Form 写成 Native ToCover 已成立。

---

## 11. Cover 语义

V1 最低保证：

- 两根控制筋由用户放在正确位置；
- 中间钢筋严格按控制筋几何插值；
- 对线性变截面/平面放坡构件，这种插值可以表达连续渐变。

但：

```text
ManagedGeometry != Native ToCover Constraint
```

若使用 fallback Explicit FreeForm，Result 必须明确：

```text
ConstraintMode = ManagedGeometry
NativeToCover = false
```

后续可增加：

- Host Face distance QA；
- Cover + BarRadius 偏差检查；
- 原生 Surface/Aligned Adapter。

---

## 12. 控制筋删除后的后续更新准备

用户可能在确认结果后删除控制钢筋，因此 V1 创建时建议保存 metadata / Extensible Storage：

```text
SchemaVersion
OperationId
StartControlGeometrySnapshot
EndControlGeometrySnapshot
StartControlUniqueId
EndControlUniqueId
DistributionPathKind
DistributionPathReference
Zones[]
BarTypeId
HostId
RebarStyle
```

这样后续 V1.1 即使控制筋被删除，也能：

- 修改 Zone；
- 修改最大间距；
- 重新计算数量；
- 使用首末 geometry snapshot 更新 set。

如果需要修改首末几何，再重新拾取新的控制筋。

---

## 13. GUI 建议

Ribbon：

```text
钢筋工具区
└─ 创建钢筋 ▼
   └─ 渐变钢筋集
```

窗口：

```text
┌─ 渐变钢筋集 ─────────────────────────┐
│ 起始控制筋  [拾取]                   │
│ 结束控制筋  [拾取]                   │
│ 分布路径    [拾取 ModelLine / Edge] │
│                                     │
│ 类型   长度    最大间距   计算结果  │
│ 加密   1000    100        100       │
│ 普通   4250    200        195+155   │
│ 加密   2000    100        100       │
│ 普通   4300    200        180+160   │
│ 加密   1000    100        100       │
│                                     │
│ [+区段] [-区段] [↑] [↓]             │
│                                     │
│ 有效路径：12550mm                    │
│ 区段合计：12550mm ✓                 │
│ 总钢筋：87                           │
│ 输出：1 个 FreeForm Rebar Set        │
│                                     │
│ [计算预览]      [创建]      [取消]  │
└─────────────────────────────────────┘
```

---

## 14. Service / MCP

GUI 与 MCP 共用同一个 Service。

建议：

```text
RebarGradientSetModels.cs
RebarGradientSetService.cs
RebarGradientSetMetadata.cs
RebarGradientSetCommand.cs
```

MCP：

```text
preview_gradient_rebar_set
create_gradient_rebar_set
```

V1.1：

```text
update_gradient_rebar_set
```

Request 核心字段：

```json
{
  "start_control_rebar_id": 100001,
  "end_control_rebar_id": 100002,
  "distribution_path": {
    "kind": "Edge",
    "stable_reference": "..."
  },
  "zones": [
    {"length_mm": 1000, "max_spacing_mm": 100},
    {"length_mm": 4250, "max_spacing_mm": 200},
    {"length_mm": 2000, "max_spacing_mm": 100},
    {"length_mm": 4300, "max_spacing_mm": 200},
    {"length_mm": 1000, "max_spacing_mm": 100}
  ],
  "spacing_step_mm": 5,
  "min_tail_ratio": 0.75,
  "dry_run": true
}
```

Result 至少返回：

```text
Success
DryRun
ChangedModel
EngineUsed
ConstraintMode
DistributionLengthMm
PathReversed
EndControlReversed
Zones[]
StationsMm[]
BarCount
CreatedRebarId
PhysicalBarCount
HostId
BarTypeId
Warnings[]
Error
```

---

## 15. Transaction / Undo

Preview / DryRun：

- 只读；
- 不开写 Transaction；
- ChangedModel=false。

Create：

- 一次产品事务；
- 任一几何、API create、readback invariant 失败 -> rollback；
- 成功只产生一个结果 Rebar Element；
- 一次 Ctrl+Z 删除整个结果集；
- 两根控制筋保持原样。

---

## 16. 创建后强制 Readback

至少验证：

1. Result RebarId > 0；
2. 只有一个结果 Rebar Element；
3. HostId 与控制筋一致；
4. BarTypeId 与控制筋一致；
5. PhysicalBarCount = Preview BarCount；
6. 第一根与 StartControlRebar 几何匹配；
7. 最后一根与 EndControlRebar 几何匹配；
8. 中间筋与计划 Station/t 插值一致；
9. 控制筋 Id / UniqueId / Host / geometry 不变。

---

## 17. 必测错误输入

至少包括：

- 控制筋不存在/不是 Rebar；
- 两根控制筋相同；
- 不是 single-position；
- Host 不同；
- BarType 不同；
- RebarStyle 不同；
- curve count/type 不匹配；
- unsupported curve；
- <1mm 退化段；
- Path 不是直线；
- Edge reference 失效；
- Zones 为空；
- Zone Length <=0；
- MaxSpacing <=0；
- Zone 合计与有效路径长度不一致；
- 手动 actual spacing > max；
- physical bar 数超 hard limit；
- native create 失败；
- readback 数量或首末几何不一致。

任何失败：

```text
ChangedModel=false
无残留
```

---

## 18. Workload Hard Limits

建议 V1 初始值：

```text
MaxZones = 50
MaxPhysicalBars = 1000
MaxCurvesPerBar = 100
```

性能实测后再调整。

---

## 19. 版本策略

### Revit 2025
主开发、主运行版本。

### Revit 2026
独立 Adapter / compile/runtime 验证，不把 2026 API 假设为 2025 可用。

### Revit 2027+
后续可利用：

- 3D Path；
- Custom Spacing；
- Morphed Rebar；
- 新约束/更新 API。

但 2027 不是 V1 前置条件。

---

## 20. V1 不做

- 曲线 Distribution Path；
- Spline/NURBS 控制筋；
- 自动 clash resolve；
- Coupler / Lap / Splice；
- 自动生成/删除控制筋；
- 纵向主筋自动化；
- 2027 专属 Engine 正式产品化；
- 复杂非线性曲面 Cover 自动求解；
- 多 Rebar Element 拼成“伪 Set”。

---

## 21. 开发顺序

### Step 0：Revit 2025 Native Capability POC
最小实验，不做完整 UI。

输出必须明确为：

```text
NATIVE_SURFACE_USABLE
NATIVE_ALIGNED_USABLE
或
EXPLICIT_MANAGED_FALLBACK_REQUIRED
```

### Step 1：Pure Calculation
先实现：

- Station；
- Zone Plan；
- Regularized Spacing；
- Station merge/dedupe；
- Line/Arc interpolation；
- Preview JSON。

### Step 2：Create Service
实现：

- 一个 Rebar Set；
- 单 Transaction；
- exact readback；
- rollback；
- Undo。

### Step 3：Typed MCP
先打通完整产品链。

### Step 4：GUI
最后做拾取、Zone 编辑器、预览、创建。

### Step 5：Runtime Acceptance
Revit 2025 主验收，再按项目需要验证其它版本。

---

## 22. 请其他 AI 重点审查

请逐项回答，不要只给总体评价：

1. Revit 2025 API 是否能可靠程序化创建原生 Surface/Aligned Free Form？
2. 如果能，能否在一个 Rebar Set 中表达非均匀 Zone station？
3. 如果不能，explicit `CreateFreeForm` 是否能稳定创建一个 Rebar Element + N physical bars？
4. R07/R08 explicit FreeForm 在 Revit 2025 的性能、Schedule、Bar Mark、Numbering 有哪些限制？
5. Line/Arc 匹配与三点 Arc 插值有哪些退化风险？
6. `SpacingStep=5mm + MinTailRatio=0.75` 是否合理？有没有更符合工程习惯的算法？
7. Zone 边界强制有筋是否存在明显问题？
8. 控制筋删除后保存 geometry snapshot 支持 spacing-only update 是否合理？
9. 500~1000 physical bars 的单 FreeForm Set 性能是否可接受？
10. V1 是否应仅 Create，Update 独立为 V1.1？
11. 是否遗漏 Schedule / Mark / Shape Matching / Hook / Termination 风险？
12. 2027 Custom Spacing / Path / Morphed 是否适合做独立 Adapter，而不污染 2025 Core？

---

## 23. 当前结论

功能已经可以进入开发前技术 POC。

**下一步不是直接写完整 WPF，而是先在 Revit 2025 做 Native Surface/Aligned 与 Explicit ManagedFreeForm 的最小能力对比。**

POC 选定 Engine 后，再冻结 V1 并正式开发。

---

## 24. v0.3 评审补丁｜只补疏漏，不扩大 V1 范围

本节根据开发前独立审查补充。除本节明确修正处外，V1 范围保持 v0.2 不变。

### 24.1 Zone 顺序与 Path 方向消歧

业务语义固定为：

```text
Zone[0] 永远从 StartControlRebar 一侧开始。
最后一个 Zone 永远结束于 EndControlRebar 一侧。
```

`Path Start / Path End` 的几何方向不得改变 Zone 的业务顺序。

执行时先把拾取到的 DistributionPath 归一化：

```text
NormalizedStation(StartControlRebar) = 0
NormalizedStation(EndControlRebar)   = DistributionLength
```

如果原始 Path 方向相反，仅设置：

```text
PathReversed = true
```

然后内部翻转 Station 轴。不得反转 `Zones[]`，不得交换 Start/End Control Rebar。

---

### 24.2 DryRun 与 Create 必须重新校验，禁止信任旧 Preview 缓存

Preview / DryRun 返回一个只用于一致性检查的 `PlanFingerprint`，至少绑定：

```text
StartControlRebar UniqueId
EndControlRebar UniqueId
StartControl geometry signature
EndControl geometry signature
DistributionPath stable reference / ModelLine UniqueId
DistributionPath geometry signature
Zones configuration
BarTypeId
HostId
```

点击 Create 时必须：

1. 重新读取两根控制筋；
2. 重新读取 DistributionPath；
3. 重新跑输入校验；
4. 重新计算 Zone / Station / Geometry Plan；
5. 比较 Preview 时的 `PlanFingerprint`（若 UI 带入）；
6. 若模型已变化，则拒绝使用旧预览，提示重新预览。

Create 不允许直接复用旧 DryRun 的几何缓存作为最终写入依据。

---

### 24.3 控制筋正反向匹配规则量化

`EndControlReversed` 只用于确定曲线序列方向，不用于判断两根控制筋“是否太不一样”。

正序/反序评分采用每条对应 Curve 的代表点距离之和：

```text
Line: Start + End
Arc:  Start + Mid + End
```

例如：

```text
ScoreForward = Σ distance(representative points)
ScoreReverse = Σ distance(representative points after reversing curve order/direction)
```

取更小者。

注意：

- Score 只用于**方向选择**；
- 不设置“形状差异过大就拒绝”的全局距离阈值；
- 合法性仍由 curve count、curve type、连续性、退化段、Revit bending validation 等规则决定。

---

### 24.4 Station 去重容差

所有 Zone 边界和内部 Station 合并前统一排序。

V1 固定：

```text
StationMergeToleranceMm = 0.5
```

若两个 Station 距离 <= 0.5mm：

- 合并为一个 Station；
- Zone 边界优先保留；
- DryRun Result 记录 `MergedStationCount`；
- 禁止生成两根几乎重合的钢筋。

---

### 24.5 短 Zone 与规整间距搜索的终止规则

补充两个遗漏条件。

#### A. ZoneLength <= MaxSpacing

直接生成一个 interval：

```text
ActualSpacing = ZoneLength
IntervalCount = 1
TailSpacing = 0
```

不进入规整间距搜索。

#### B. 规整候选搜索无结果

搜索必须有明确下界与终止条件，禁止无限向下循环。

V1 不静默退化成不可读的长小数间距。

若在允许的候选范围内没有满足规整规则的间距：

```text
Success = false
ErrorCode = REGULAR_SPACING_NOT_FOUND
```

UI 要求用户：

- 手动指定该 Zone 的 `ManualActualSpacingMm`；或
- 调整 Zone 长度 / MaxSpacing。

`SpacingStepMm=5`、`MinTailRatio=0.75` 暂继续作为产品默认值，不把它写成结构规范要求；POC/工程人员确认后再冻结。

---

### 24.6 Arc 插值语义补充

V1 的 Arc 三点插值保证：

- Start Point 连续插值；
- Mid Point 连续插值；
- End Point 连续插值；
- 最终几何经过这三个插值点。

它**不保证 Radius 或 Center 也按首末 Arc 线性插值**。

因此验收不得写成“半径线性渐变”。

Arc 重建前必须检查：

- 三点不重合；
- 三点不近似共线；
- 新 Arc 有效；
- 曲线方向与控制筋对应方向一致；
- Revit bending validation 通过。

任何 Arc 退化：

```text
整次 Create 失败并 RollBack
```

禁止静默转成 Line。

---

### 24.7 Geometry Snapshot 坐标语义

未来 V1.1 使用的几何快照必须保存：

**已经变换到 Revit Document 模型坐标系中的 transformed centerline 几何。**

不得只保存：

- FamilyInstance 局部坐标；
- 未应用 transform 的原始 Curve；
- 仅共享坐标显示值。

序列化需显式记录：

```text
CoordinateSpace = DocumentModel
LengthUnit = RevitInternal / 或明确版本化单位
```

用户删除控制筋后，Snapshot 只允许支持“首末几何不变”的 spacing-only 更新。

如果 Host 位置/几何已经变化，V1.1 必须重新校验；无法证明快照仍适用时，应要求重新拾取控制筋，不得盲目更新。

---

### 24.8 Schedule / Mark / Numbering / Shape Matching 加入 POC，不先下结论

V1 文档不预设：

- 一个渐变 Set 一定产生多少个 Shape Family；
- 一定产生多少个 Bar Mark；
- 一定不能/一定能形成某种 BBS；
- Numbering 一定按某种方式展开。

这些行为必须在 Revit 2025 POC / Runtime 中实际读回。

至少记录：

```text
Rebar Number
Schedule Mark
Partition
Rebar Shape / Reported Shape
NumberOfBarPositions
NumberVaryingLengthRebarsIndividually（若版本/API 可读）
各 physical bar 的 shape/length 差异
Schedule 中实际展开方式
```

目标不是本期实现 BBS，而是确认 GR01 不会产生不可接受的出图/统计副作用。

---

### 24.9 Hook / Termination V1 默认保守拒绝

在 POC 证明可以无损复制前：

```text
控制筋存在 Hook / Termination
=> V1 Create 拒绝
```

不得：

- 静默删除 Hook；
- 静默改成无 Hook；
- 只复制一端；
- 只复制首末而不复制中间。

若 POC 后确认 API 能稳定复制，再单独解除该 Guard；这不属于 V1 当前必做扩展。

---

### 24.10 V1 明确只做 Create

V1 正式验收范围锁定为：

```text
Preview / DryRun
+
Create
+
Readback
+
Rollback
+
One Undo
```

`update_gradient_rebar_set` 不属于 V1 Gate。

Metadata / Geometry Snapshot 本期只为未来 V1.1 留数据，不要求在 V1 实现完整 Update 工作流。

---

### 24.11 性能 POC 分档

`MaxPhysicalBars = 1000` 暂时只是硬上限候选，不作为“1000 根性能已可接受”的结论。

POC / Runtime 至少记录：

| 档位 | 必测 |
|---|---|
| 约 200 bars | Create / Regenerate / Undo |
| 约 500 bars | Create / Regenerate / Undo |
| 约 1000 bars | Create / Regenerate / Undo，若测试环境可承受 |

记录：

```text
Plan calculation time
API create time
Transaction total time
Regenerate time
Undo observation
Model responsiveness
```

如 1000 档明显不可接受，再降低 Hard Limit，不在需求阶段猜测最终阈值。

---

### 24.12 Revit 2025 Native POC 增加强制矩阵

Step 0 的交付物必须包含：

| 验证项 | Native Surface | Native Aligned | Explicit Managed |
|---|---|---|---|
| 程序化创建 | TEST | TEST | 已有 R07 基础，仍复核 2025 |
| 1 Element + N bars | TEST | TEST | TEST |
| 首末渐变表达 | TEST | TEST | TEST |
| 非均匀 Zone Station | TEST | TEST | TEST |
| Native Cover / Constraint | TEST | TEST | N/A / false |
| Readback 曲线一致 | TEST | TEST | TEST |
| Undo | TEST | TEST | TEST |
| Hook / Termination | TEST | TEST | TEST |
| Schedule / Mark / Numbering | TEST | TEST | TEST |
| 200 / 500 / 1000 bars 性能 | TEST | TEST | TEST |

结论只能是基于实测证据的：

```text
NATIVE_SURFACE_USABLE
NATIVE_ALIGNED_USABLE
EXPLICIT_MANAGED_FALLBACK_REQUIRED
```

允许出现“Native 能做部分功能，但 GR01 V1 仍选择 Explicit”的结果。

---

### 24.13 关于 Revit 2025 Custom Spacing 的纠正

开发前审查中有人提出：

```text
RebarFreeFormAccessor.SetLayoutAsCustomSpacing
```

在早期版本已经存在。

**本需求不采纳这个结论。**

GR01 V1 不假设 Revit 2025 存在可直接使用的 Custom Spacing API。

Revit 2025 的非均匀 Zone 排布是否能通过 Native Surface / Aligned 路线实现，必须由 Step 0 POC 实证；如果不能，直接使用 Explicit ManagedFreeForm 的显式 Station 方案。

2027 的 Custom Spacing 继续保持独立 Adapter 路线，不反向污染 2025 Core。

---

## 25. v0.3 后的开发入口

需求不再继续扩展。

下一步固定为：

```text
GR01-P0
Revit 2025 Native Capability POC
```

只回答 Engine 选择问题，不做完整 WPF，不做 Update，不做纵向主筋，不做 2027 产品实现。

P0 完成后冻结 Engine，再开始 GR01 V1 正式开发。
