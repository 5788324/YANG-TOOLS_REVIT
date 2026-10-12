# YangTools 3.1.38｜GR01 V1 GUI Static Change Report

日期：2026-09-20

## 基线

- GR01 V1 Core source SHA：`5126B39F901898A99FF2D9814A814816FB5EEB41C002A512A2E9C0266AB93F09`
- GR01 V1 Core Runtime：GO（Revit 2025.4.2）
- Engine：`ExplicitManagedFreeForm`

## 本轮新增

- `Commands/RebarGradientSetCommand.cs`
- `Models/RebarGradientSetViewModel.cs`
- `UI/RebarGradientSetWindow.xaml`
- `UI/RebarGradientSetWindow.xaml.cs`
- `tools/Test-RebarGR01V1Gui.py`
- `docs/GR01_V1_GUI_DESIGN_20260920.md`

## 本轮修改

- `tools/Test-RebarGR01V1.py`：由“GUI/Ribbon 尚未实现”切换为“GUI 入口 + 共用 Service”静态门禁；
- `tools/Test-UiPanelsStatic.py`：Ribbon 总命令数 43 -> 44；钢筋工具区仍必须只有四个一级 Pulldown；
- R01-R10 静态测试中的 cumulative Ribbon count 同步为 44；不修改各 Rxx 产品逻辑。

## GUI 设计

- Ribbon：`钢筋工具区 -> 创建钢筋 -> 渐变钢筋集`；
- modeless + `ModelessWindowRegistry`，同文档单实例；
- Document-bound `ExternalEvent`；
- `wrapInTransactionGroup:false`，产品事务仍由已验收的 `RebarGradientSetService` 唯一管理；
- GUI 支持起始/结束控制筋、直线 ModelLine/Edge、Zone DataGrid、普通/加密快捷添加、Preview/Create；
- Preview fingerprint 失效机制防止旧预览创建；
- UI 不实现 Update、2027 Adapter、NativeToCover。

## 静态结果

- `Test-RebarGR01V1.py`：81/81 PASS；
- `Test-RebarGR01V1Gui.py`：44/44 PASS；
- `Test-UiPanelsStatic.py`：201 C# / 43 XAML / 44 Ribbon / 0 error / 0 warning；
- 全部 `tools/Test-*.py`：33 suites / 0 failure；
- `python -m py_compile McpServer/server.py`：PASS。

## 当前环境限制

当前沙箱无 Windows / .NET SDK / MSBuild / Revit，因此：

- C# Fresh Build：NOT_RUN；
- XAML Runtime load：NOT_TESTED；
- Revit 2025 GUI interaction：NOT_TESTED。

## 判定

`GR01 V1 GUI STATIC GO / WINDOWS BUILD + REVIT 2025 GUI RUNTIME REQUIRED / RELEASE NO-GO`
