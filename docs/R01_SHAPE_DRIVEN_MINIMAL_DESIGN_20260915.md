# Rebar R01｜Shape Driven 最小创建器设计

日期：2026-09-15

## 目标

在 R00 E1/E2 已验证的事实上，建立第一个可撤销、可 dryRun、UI/MCP 共用的钢筋写服务。

R01 不是生产级“按面配筋”，只验证创建链本身。

## 当前能力

### 输入

- `hostId`
- `barTypeId`（可省略，测试模式会选项目第一个 RebarBarType）
- `startMm = [x,y,z]`
- `endMm = [x,y,z]`
- `normal = [x,y,z]`
- `layoutMode = Single | NumberWithSpacing`
- `numberOfBarPositions`
- `spacingMm`
- `dryRun`

### 创建

- `RebarHostData.IsValidHost(host)` 预检；
- `Rebar.CreateFromCurves`；
- `RebarStyle.Standard`；
- 无 Hook；
- `GetShapeDrivenAccessor()`；
- `SetLayoutAsSingle` / `SetLayoutAsNumberWithSpacing`；
- 单一 Transaction；
- Commit 状态必须为 Committed；
- 异常 RollBack。

### Readback

创建成功后立即调用 `RebarInspectionService.Inspect()`，返回真实：

- engine mode；
- host；
- shape；
- diameter；
- layout；
- constraints；
- sample curve。

## dryRun

`dryRun=true` 时：

- 只解析 host / bar type；
- 校验 line length；
- 校验 normal 与 line 垂直；
- 校验 layout；
- 不创建 Transaction；
- `ChangedModel=false`。

## UI

新增 Ribbon：

`钢筋工具区 → 直筋创建 R01`

交互：

1. 选择有效 Rebar Host；
2. PickPoint 起点；
3. PickPoint 终点；
4. 使用当前项目第一个 RebarBarType（仅 R01 测试 UI）；
5. 用户选：
   - Dry Run；
   - 创建单根；
   - 创建 3 根 @200mm。

生产版后续改为真正参数面板，R01 不提前扩 UI。

## MCP

新增：

- `list_rebar_types()`
- `create_shape_rebar(...)`

MCP 默认：

`dry_run=True`

避免 AI 未明确确认就写模型。

## 明确限制

R01 的 `startMm/endMm` 是**钢筋中心线坐标**。

R01 暂时不做：

`选择宿主 Face → 读取 Cover → 自动求钢筋中心线`

该能力属于后续 Shape Driven Constraint / Face Placement 阶段。

这样可以先证明：

`Intent → Revit Create → Layout → Commit → Inspector readback`

这条最小闭环可靠，再向上加 Face/Cover 语义。
