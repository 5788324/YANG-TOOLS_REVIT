# GR01 V1 GUI 设计｜渐变钢筋集

日期：2026-09-20  
状态：STATIC IMPLEMENTED / REVIT 2025 GUI RUNTIME REQUIRED

## 1. 范围

本轮只为已经通过 Revit 2025 Core Runtime 的 GR01 V1 增加中文 WPF 操作界面，不修改已经验收的 Station、Zone、规整间距、Line/Arc 插值和 Explicit ManagedFreeForm 创建算法。

GUI 仍调用统一链路：

`RebarGradientSetWindow -> RebarGradientSetRequest -> RebarGradientSetService -> RebarGradientSetResult`

不在窗口代码中复制 `Rebar.CreateFreeForm`、Spacing Planner 或 Transaction。

## 2. Ribbon

位置：

`钢筋工具区 -> 创建钢筋 -> 渐变钢筋集`

使用现有 Pulldown 分组，Order=130，不新增第五个钢筋一级入口。

## 3. 窗口交互

窗口分为四部分：

1. 控制钢筋：拾取起始/结束 Rebar；
2. 分布路径：选择“模型线”或“模型 Edge”后拾取；
3. 分布区段：DataGrid 编辑 Label、LengthMm、MaxSpacingMm、可选 ManualActualSpacingMm；支持添加普通区/加密区、删除、上下移动；
4. 预览：显示有效路径、IntervalCount、BarCount、Host/BarType、每区实际主间距、尾差和间隔数。

创建按钮必须先得到成功 Preview 的 `PlanFingerprint` 才启用。

## 4. Picking

窗口为同文档单实例 modeless WPF，通过绑定 Document 的 `ExternalEvent` 执行 Revit Selection/API。

- 控制筋：`RebarSelectionFilter`；
- ModelLine：只允许 `ModelCurve.GeometryCurve is Line`；
- Edge：`ObjectType.Edge` + `GetGeometryObjectFromReference() is Edge` + `Edge.AsCurve() is Line`。

窗口隐藏后进入 Revit 拾取，完成或 Esc 后恢复窗口。

## 5. Preview / Create

Preview：

- UI 生成 `RebarGradientSetRequest(DryRun=true)`；
- Service 做完整产品预检；
- 成功后保存 `PlanFingerprint`；
- 将 `SpacingPlan.Zones[]` 映射回 DataGrid 预览列。

Create：

- 没有成功 Preview 时禁用；
- UI 把当前输入重新组装为 Request；
- 传入上一次 `ExpectedPlanFingerprint`；
- Service 内部再次计算并执行 stale-plan guard；
- `DryRun=false` 时由 Service 自己开启唯一产品 Transaction；
- GUI 的 ExternalEvent 明确 `wrapInTransactionGroup:false`，不在产品 Transaction 外再包一层事务组。

## 6. Preview 失效

以下任何变化均清空 fingerprint，并立即禁用 Create：

- 起始/结束控制筋变更；
- 路径类型/路径变更；
- Zone 增删/移动；
- Zone Label/Length/MaxSpacing/ManualSpacing 编辑。

成功 Create 后也清空 fingerprint，要求下一次创建前重新 Preview。

## 7. V1 公开边界

窗口明确：

- `ManagedGeometry`；
- `NativeToCover=false`；
- 只支持直线 ModelLine / Edge；
- V1 是 Create-only；
- Update 不在本轮 GUI；
- 不添加 2027 CustomSpacing / Morphed / 3D Path 产品逻辑。

## 8. Runtime 门禁

Revit 2025 GUI 验收至少证明：

- Ribbon Pulldown 打开窗口；
- 同文档重复点击只激活同一窗口；
- 控制筋真实拾取；
- ModelLine 与 Edge 真实拾取；
- 五区 12.55m 表格编辑；
- Preview 显示 87 bars / 86 intervals 和 100 / 195+155 / 100 / 180+160 / 100；
- Create 从 GUI 产生 1 Rebar Element / 87 physical bars；
- 一次 Undo 删除结果集；
- 编辑任一区段后 Create 立即禁用；
- 无 SOURCE_DIFF。
