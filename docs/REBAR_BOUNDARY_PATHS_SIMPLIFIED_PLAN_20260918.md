# 按首末模型线自动排布钢筋｜简化开发方案

日期：2026-09-18  
状态：规划稿，尚未开发

## 1. 目标

把原 `RebarByBoundaryLines_REQUIREMENTS_v0.1` 收敛成一个单一、可验收的功能：

> 用户分别选择“第一根钢筋路径”和“最后一根钢筋路径”的模型线链，选择宿主与钢筋类型，输入最大间距；插件自动计算根数，并在线性插值后创建一组 Free Form 钢筋。

第一版只解决这个问题，不同时承担 Shape Driven、更新、曲线匹配、规范验算、碰撞消解等任务。

## 2. 第一版只保留 5 个输入

1. 第一根路径：一组连续 ModelLine；
2. 最后一根路径：一组连续 ModelLine；
3. Rebar Host；
4. RebarBarType；
5. 最大间距 `MaxSpacingMm`。

默认约定：模型线表示**钢筋中心线**，本功能不再额外套保护层偏移。

## 3. 明确删掉/延后的复杂项

第一版不做：

- `ShapeDrivenFixed` 第二模式；
- `ClosestProjection`；
- 用户自选分布轴；
- `EvenlySpace` 开关（始终均匀分布）；
- 更新已有组；
- 自动加载 Coupler/钢筋族；
- 自动碰撞消解；
- Arc/螺旋/样条 ModelLine；
- 详细坐标点表写入普通用户报告；
- 共享参数写回。

这些能力只有在第一版 Runtime GO 后再逐项增加。

## 4. 输入几何规则

### 4.1 ModelLine 实际按“线链”选择

Revit 的一条 ModelLine 是一个 CurveElement，不等于整条多段线。因此请求模型应使用：

- `FirstPathLineIds[]`
- `LastPathLineIds[]`

而不是单个 `FirstLineId / LastLineId`。

### 4.2 第一版几何门禁

- 只接受直线 ModelLine；
- 每条路径必须连续；
- 两条路径必须有相同段数；
- 每段长度 >= 1 mm；
- 自动尝试整条路径反向，使首末对应点距离更小；
- 不自动重排路径中间段，不做 ClosestProjection。

整理后得到对应顶点：

`F0...Fk` 与 `L0...Lk`。

## 5. 最大间距算法修正

原需求中的：

`n = floor(L / s_max) + 1`

不能保证“最大间距不超过 s_max”。例如 `L=10000 mm, s_max=300 mm`：34 根只有 33 个间隔，实际间距约 303.03 mm，已经超限。

第一版采用：

```text
Dmax = max(|Li - Fi|)
intervalCount = ceil(Dmax / s_max)
barCount = intervalCount + 1
actualMaxSpacing = Dmax / intervalCount
```

因此：

```text
Dmax = 10000 mm
s_max = 300 mm
intervalCount = 34
barCount = 35
actualMaxSpacing = 294.12 mm
```

选择 `Dmax` 而不是“对应点平均距离”，原因是平均值可能在较宽的一端产生超过最大间距的情况。使用最大对应点位移可给第一版一个简单、保守、可证明的上限。

## 6. 中间钢筋生成

对第 `j` 根：

```text
t = j / intervalCount
P(i,j) = Fi + t * (Li - Fi)
```

其中 `j = 0...intervalCount`。

每根路径：

```text
P(0,j) -> P(1,j) -> ... -> P(k,j)
```

包含 XYZ，因此高度、平面位置和折线路径会一起渐变。

## 7. Revit 创建策略

第一版只使用 **Free Form Explicit**：

- 复用 R07 已验证的 `Rebar.CreateFreeForm` 路线；
- 优先创建一个 explicit multi-bar Rebar set；
- Host、BarType 由用户明确选择；
- 不把它声明成 Shape Driven；
- 不伪造 ToCover / MaximumSpacing 约束；
- 成功创建使用一个 Transaction，失败完整 RollBack；
- 一次 Ctrl+Z 撤销整次创建。

如 Revit 原生 validation 拒绝任意一根路径，则整个操作失败并回滚，不留下半组钢筋。

## 8. UI 第一版

Ribbon 位置：

`钢筋工具区 -> 创建钢筋 -> 首末线排布`

窗口只保留：

```text
第一根路径   [拾取线链]
最后一根路径 [拾取线链]
宿主         [拾取]
钢筋类型     [下拉]
最大间距     [200] mm

预览：35 根 / 最大实际间距 294.12 mm

[计算预览] [创建] [取消]
```

不让用户在第一版理解 Free Form / Shape Driven 两种引擎差异。

## 9. MCP / Service

UI 与 MCP 必须共用同一个 Service。

建议 Tool：

`create_rebar_between_boundary_paths`

最小请求：

```json
{
  "first_path_line_ids": [774385, 774380, 774375],
  "last_path_line_ids": [774421, 774410, 774405],
  "host_id": 744989,
  "bar_type_id": 180250,
  "max_spacing_mm": 200,
  "dry_run": true
}
```

最小返回：

```json
{
  "success": true,
  "changed_model": false,
  "bar_count": 142,
  "interval_count": 141,
  "max_spacing_mm": 200,
  "actual_max_spacing_mm": 199.33,
  "created_rebar_id": null,
  "warnings": []
}
```

普通返回不发送全部路径点；需要诊断时再打开 `include_debug_geometry`。

## 10. 第一版验收

只要求闭合以下 8 项：

1. 两组三段直线 ModelLine 可识别、排序、自动反向；
2. `10000 / 200` => 51 根；
3. `10000 / 300` => 35 根，约 294.12 mm；
4. DryRun 不写模型；
5. Apply 创建的首根/末根与两条输入路径重合；
6. 中间根按 XYZ 线性插值；
7. 一个 Transaction / 一次 Undo / 失败无残留；
8. Revit 2024 Runtime + 2025 compile-only。

## 11. 第二阶段再做

第一版 Runtime GO 后再考虑：

- 修改最大间距后原位更新；
- ExtensibleStorage 保存 source line UniqueId / MaxSpacing / OperationId；
- Arc 路径；
- Shape Driven 可行时的单独模式；
- 创建后调用 R09 碰撞/净距分析；
- 自动识别宿主与 BarType；
- 更复杂的路径匹配。

## 12. 与现有钢筋架构的关系

该功能不是新的第三个钢筋引擎，而是：

```text
ModelLine Path Input
    -> Boundary Path Planner
    -> explicit interpolated bar paths
    -> existing Free Form engine/service
```

因此应该尽量复用 R07/R08 Free Form 的几何校验、数量限制、事务和读回，而不是再复制一套 Free Form 创建逻辑。
