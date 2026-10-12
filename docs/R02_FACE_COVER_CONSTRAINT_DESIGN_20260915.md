# YangTools Rebar R02｜PlanarFace + Host Cover + ToCover

日期：2026-09-15
状态：STATIC CANDIDATE

## 目标

验证并固化 Shape Driven 的第一条生产级约束链：

`Host PlanarFace → RebarHostData Cover → bar model radius → initial centerline → RebarPlane preferred ToCover → readback`

R02 不解决 Varying、Free Form、Hook、Coupler、Lap、碰撞和 BS8666。

## 为什么这样拆

R01 已实机证明 `CreateFromCurves + Layout + Transaction + Inspector` 能稳定工作。R02 不再重复创建器，而只增加：

1. exposed planar face 的稳定识别；
2. Host Cover 读取；
3. cover + bar model radius 的初始中心线；
4. RebarConstraintsManager face-specific candidate；
5. RebarPlane `ToCover` preferred constraint；
6. Cover 改变后的跟随回归。

## API 依据

- `RebarHostData.IsValidHost`
- `RebarHostData.IsFaceExposed`
- `RebarHostData.GetCoverType`
- `RebarCoverType.CoverDistance`
- `Reference.ConvertToStableRepresentation / ParseFromStableRepresentation`
- `PlanarFace.FaceNormal`（实体边界时稳定朝实体外侧）
- `RebarConstraintsManager.GetAllHandles`
- `RebarConstraintsManager.GetConstraintCandidatesForHandle(handle, Reference)`
- `RebarConstraintType.ToCover`
- `RebarConstraintsManager.SetPreferredConstraint`
- `RebarConstraintsManager.GetPreferredConstraintOnHandle`

## 新增 Service

### `InspectHostFaces`

只读输出：

- host；
- face stable reference；
- PlanarFace / exposed；
- origin / normal；
- area；
- Cover Type / distance。

### `FaceDrivenRebarCreateService.Execute`

输入：

- HostId；
- FaceStableReference；
- BarTypeId；
- face 附近 start/end mm；
- Single / NumberWithSpacing；
- dryRun。

流程：

1. 解析 stable reference；
2. 强制 HostId 与 Reference.ElementId 一致；
3. 仅接受 exposed `PlanarFace`；
4. start/end 投影到 bounded face；
5. `inward = -FaceNormal`；
6. 初始中心线偏移 = `CoverDistance + 0.5 * BarModelDiameter`；
7. R01 Shape Driven 创建；
8. 找到 `RebarPlane` handle；
9. 仅从“所选 face reference”的 candidates 中寻找 `ToCover`；
10. 设置 preferred constraint；
11. 若不能确认 preferred `ToCover`，整个 Transaction 回滚；
12. commit 后 R00 Inspector readback。

## 安全约束

- 默认 MCP `dry_run=true`；
- 不接受曲面；
- 不接受 concealed face；
- 不允许 FaceStableReference 指向另一个 host；
- ToCover 未成功时禁止留下“看起来位置正确、实际未约束”的钢筋；
- 一个正式创建仅一个 Transaction；
- 错误时 rollback；
- R02 不自动修改 Host Cover Type。

## Runtime 成功标准

最低必须证明：

1. `inspect_rebar_host_faces` 能返回真实 exposed planar face + cover；
2. dryRun 不改模型；
3. Single 创建后 R00 readback 中 RebarPlane 约束为 `ToCover`；
4. 实际中心线距宿主面与 `cover + model radius` 一致或解释清楚 Revit 实际语义；
5. 修改该 face Cover 后，已有钢筋跟随移动，且 preferred/current constraint 仍为 ToCover；
6. NumberWithSpacing 保持同一面约束；
7. 错误 stable reference / 错 host / 非 exposed face 不留半成品；
8. 用正确的 PostableCommand Undo 补 R01/R02 一次 Undo 证据。

## 后续

R02 Runtime GO 后进入 R03：斜面 / 横坡 / 纵坡 / Varying / 直线变截面。
