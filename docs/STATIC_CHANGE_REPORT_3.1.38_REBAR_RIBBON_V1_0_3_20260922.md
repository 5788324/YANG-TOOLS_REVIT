# Static Change Report — YangTools 3.1.38 Rebar Ribbon V1.0.3

日期：2026-09-22

## 源码变更

1. `RebarGradientSetCommand` 保持 `groupName="创建钢筋"`，并成为该组唯一命令。
2. 其余 12 个 `Rebar*Command` 统一改为 `groupName="钢筋测试"`。
3. 13 个钢筋功能全部改用新的独立钢筋图标，不再复用 `project_inspector`。
4. 新增 `rebar_test` 分组图标。
5. `RibbonButtonAttribute` 新增可选 `GroupLargeIcon / GroupSmallIcon`。
6. `RibbonBuilder` 支持稳定 Pulldown 分组图标，缺失时回退到首命令图标。
7. 更新图标生成器；同时补回 generator-owned `cad_to_revit` 图标，防止重生成图标时误删 CAD→Revit 运行资产。
8. 更新 Ribbon 静态门禁为 `创建钢筋=1 / 钢筋测试=12`。
9. 新增 `Test-RebarRibbonV103.py`。

## 静态结果

- Rebar Ribbon V1.0.3: 87/87 PASS
- GR01 V1.0.2 UI/UX: 40/40 PASS
- GR01 GUI: 44/44 PASS
- GR01 Core: 81/81 PASS
- CAD→Revit: 76/76 PASS
- UI static: 201 C# / 43 XAML / 44 Ribbon / 0 error / 0 warning
- all Test-*.py: 36 suites / 0 failure
- MCP py_compile: PASS

## 风险

当前环境不能执行 Windows/Revit Ribbon 实机渲染，因此新分组图标及两个 Pulldown 的实际显示仍需 Revit 2025 smoke；不要求重复 GR01 Create/Undo。
