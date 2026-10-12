# YangTools 4.0.0 RC2F｜线样式逐格编辑安全重构

日期：2026-10-08；范围：`ProjectAssetManagerWindow.LineStyleDataGrid_CellEditEnding` 的 **名称、线宽** 两项。

## 本批范围

- 新增 `Core/ProjectAssets/ProjectAssetLineStyleEditService.cs`：typed `ProjectAssetLineStyleEditRequest/Result/Kind`，不依赖 WPF；基于 `OST_Lines.SubCategories` 定位 Category（支持负 ID 的内置类别）。
- 内置线样式禁止重命名；线宽限制 1–16；名称去空白、拒绝空名；no-op 不开启事务。
- 一个非空有效编辑只使用一个 Revit `Transaction`；检查 Start/Commit，失败尝试 RollBack 并区分无法确认回滚的情况；仅 Commit 验证后返回 `Changed=true`。
- WPF 原有 `UpdateSourceTrigger=Explicit` 的两个 DataGridTextColumn 继续生效：不在 Revit 事务执行前写入 ViewModel。窗口关闭、Tab 状态变化、列表项失效时丢弃过期执行/刷新；已提交而 UI 更新失败时单独提醒。
- 不更改原 XAML、颜色/图案/填充区域事件，未改变 MCP、Rebar、Ribbon、csproj。**这批不声称填充区域编辑已优化。**

## 静态结果与限制

- 基线 RC2E 44/44；RC2F 新增 `Test-ProjectAssetRC2F.py` 59 个源码及 XAML 合同断言。
- 项目源工作树与干净 ZIP 解压分别 45/45；Python MCP py_compile；XAML 43/43 可解析、保留 27 MCP 命令入口（由原有合同测试覆盖）。
- 无 Windows C# 编译器、Revit/WPF 宿主；API 兼容、实际提交/回滚、UI 点击和 Ctrl+Z 仍 NOT_TESTED。开发结论 `SOURCE STATIC GO`，项目 `RELEASE NO-GO`；正式稳定版本仍 3.1.36 FINAL_E2E。

## 未来 RC2G

仅处理线样式颜色、线图案、填充类型颜色/遮罩中最小一段，优先切分 `ColorSelectionDialog` 与 Transaction，再验证 ComboBox `DropDownClosed` 是否会产生未提交先变更 VM 的问题。不得把本批合同测试扩大到这些未修改路径。
