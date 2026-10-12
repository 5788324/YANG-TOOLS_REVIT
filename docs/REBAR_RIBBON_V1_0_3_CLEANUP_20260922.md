# YangTools 3.1.38｜Rebar Ribbon V1.0.3 Cleanup

日期：2026-09-22

## 目标

将已完成 Runtime/UX 验收的 `渐变钢筋集` 固定为当前唯一生产级钢筋创建入口；其余 R00–R10 研发/验证命令统一收纳到 `钢筋测试`，避免把研究功能和正式功能混在用户主入口。

## Ribbon 结构

钢筋工具区只保留两个一级 Pulldown：

- `创建钢筋`
  - `渐变钢筋集`
- `钢筋测试`
  - 按面布筋
  - 变长布筋
  - 中心线布筋
  - 自由形状
  - 排布修改
  - 约束修改
  - 自由形状修改
  - 弯钩
  - 钢筋约束
  - 机械连接
  - 钢筋诊断
  - 碰撞与净距

`创建钢筋` 中不得再出现实验命令；12 个非 GR01 钢筋命令全部属于 `钢筋测试`。

## 图标

删除钢筋命令复用 `project_inspector` 占位图标的做法。

新增独立图标族：

- `rebar_gradient_set`
- `rebar_test`
- `rebar_face`
- `rebar_surface_varying`
- `rebar_shape_create`
- `rebar_freeform_create`
- `rebar_shape_modify`
- `rebar_constraint`
- `rebar_freeform_modify`
- `rebar_hook`
- `rebar_relationship`
- `rebar_coupler`
- `rebar_inspector`
- `rebar_clearance`

每个图标都有 16×16 / 32×32 PNG，由 `tools/Generate-RibbonIcons.py` 统一生成，继续遵守 `docs/ICON_DESIGN_GUIDE.md` 的深蓝灰 + 浅蓝线条体系。

为了避免“组内第一条命令被隐藏后 Pulldown 图标变化”，`RibbonButtonAttribute` 增加可选 `GroupLargeIcon / GroupSmallIcon`，RibbonBuilder 优先使用分组图标，缺失时才回退到组内第一条命令图标。

## 不变范围

本轮不修改：

- GR01 spacing planner；
- Line/Arc 插值；
- ZAxis / ModelLine / Edge 分布算法；
- CreateFreeForm；
- Transaction / Rollback / Undo；
- MCP Request/Service/Result；
- 已验收的 GR01 V1.0.2 GUI 行为。

## 静态门禁

- `Test-RebarRibbonV103.py`: 87/87 PASS
- `Test-RebarGR01V102UI.py`: 40/40 PASS
- `Test-RebarGR01V1Gui.py`: 44/44 PASS
- `Test-RebarGR01V1.py`: 81/81 PASS
- `Test-CadToRevit.py`: 76/76 PASS
- `Test-UiPanelsStatic.py`: 0 error / 0 warning
- 全部 `tools/Test-*.py`: 36 suites / 0 failure
- MCP `py_compile`: PASS
- Ribbon command count: 44（命令数量未变，仅分组变化）

## 当前结论

`REBAR RIBBON V1.0.3 STATIC GO / REVIT 2025 RIBBON+ICON SMOKE REQUIRED / YANGTOOLS 3.1.38 PROJECT-WIDE RELEASE NO-GO`
