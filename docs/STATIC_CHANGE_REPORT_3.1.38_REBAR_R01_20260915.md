# YangTools 3.1.38 Rebar R01｜静态变更报告

日期：2026-09-15
状态：**STATIC GO / WINDOWS REVIT 2024 RUNTIME REQUIRED**

## 基线

来自 R00 原始源码，并合入 Mimo 运行中证明必须的两个最小修复：

1. `BatchTaskWindow.xaml.cs` 非法跨行插值字符串；
2. `RebarInspectionService.GetCenterlineCurves` Revit 2024 参数名改为 `suppressHooks`。

另根据 R00 运行证据修正 Inspector 的 RebarStyle 读取，并将 Inspector schema 升为 `R00.2`。

## R01 新增

- `Core/Rebar/RebarShapeDrivenCreateModels.cs`
- `Core/Rebar/RebarShapeDrivenCreateService.cs`
- `Commands/RebarHostSelectionFilter.cs`
- `Commands/RebarShapeDrivenCreateCommand.cs`
- MCP `list_rebar_types`
- MCP `create_shape_rebar`
- `tools/Test-RebarR01.py`

## 创建能力

仅：

- Shape Driven；
- Standard straight bar；
- Single；
- NumberWithSpacing；
- dryRun；
- 单 Transaction；
- Inspector readback。

明确没有加入 Free Form / Varying / Hook / Coupler / Lap / Collision / BS8666。

## 静态结果

- `Test-RebarR00.py`：44/44 PASS；
- `Test-RebarR01.py`：35/35 PASS；
- `Test-UiPanelsStatic.py`：
  - C# 161；
  - XAML 42；
  - Ribbon 33；
  - error 0；
  - warning 0；
- 全量 `tools/Test-*.py`：21 suites / 21 PASS（Linux 解包后先恢复了原 ZIP 中中文 installer 文件名，属于打包编码处理，不是产品源码修改）。

## 当前不能声明

本环境没有 dotnet/Revit SDK 编译宿主，因此：

- C# compile：NOT_TESTED；
- Revit 2024 runtime：NOT_TESTED；
- Ctrl+Z：NOT_TESTED；
- UI click：NOT_TESTED；
- MCP write runtime：NOT_TESTED。

必须交 Mimo 做 R01 实机门禁。
