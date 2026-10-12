# Static Change Report — YangTools 3.1.38 / GR01 V1.0.2 UI/UX

日期：2026-09-20

## 变更文件

- `src/YangTools.Revit/UI/RebarGradientSetWindow.xaml`
- `src/YangTools.Revit/UI/RebarGradientSetWindow.xaml.cs`
- `src/YangTools.Revit/Models/RebarGradientSetViewModel.cs`
- `tools/Test-RebarGR01V101UX.py`（兼容新 UI 表达）
- `tools/Test-RebarGR01V102UI.py`（新增）
- 本轮文档/verification artifacts

## 主要修复

1. 修复 DataGrid 嵌套 ScrollViewer 导致鼠标滚轮无法推动外层窗口。
2. ModelLine / Edge 只作为轴线/范围；GUI 业务方向统一为 Start Control -> End Control。
3. 分离 `基准总长` 与 `有效长度`。
4. Auto Remaining 行显示 `固定/自动剩余`、`待计算`、单位 mm。
5. Zone 输入即时校验并控制 Preview Enable 状态。
6. 新增区段插入选中行之后；移除标题内部阶段版本字样。

## 静态结果

- `Test-RebarGR01V102UI.py`: 37/37 PASS
- `Test-RebarGR01V101UX.py`: 41/41 PASS
- `Test-RebarGR01V1Gui.py`: 44/44 PASS
- `Test-RebarGR01V1.py`: 81/81 PASS
- `Test-RebarGR01P0.py`: 43/43 PASS
- 全部 `tools/Test-*.py`: 35 suites / 0 failure（分两批执行）
- UI static: 201 C# / 43 XAML / 44 Ribbon / 0 error / 0 warning
- MCP `py_compile`: PASS

## 构建/运行限制

当前环境无 Windows Revit Build/Runtime。本报告不把静态结果描述为 Revit Runtime PASS。

当前判定：

`GR01 V1.0.2 UI/UX STATIC GO / WINDOWS BUILD + REVIT 2025 UI SMOKE REQUIRED / PROJECT-WIDE RELEASE NO-GO`
