# YangTools 3.1.35 — 批处理与云链接：打印 PDF

更新时间：2026-08-07

## 目标

在现有“导出 PDF（Revit 2022+）”之外增加一条独立的“打印 PDF”路径。

- **导出 PDF**：继续使用 Revit 2022+ 的 `PDFExportOptions`。
- **打印 PDF**：使用 Revit `PrintManager` 调用 Windows PDF 虚拟打印机，代码层适用于 Revit 2021+。

两条路径互不替代，可以分别开启。

## UI

“批处理与云链接 → 导出任务”新增：

- `打印 PDF（虚拟 PDF 打印机，Revit 2021+）`
- `PDF 打印机`：自动发现 Windows 中名称包含 `PDF` 的打印机，也可手动输入。
- `视图/图纸集`：沿用现有视图/图纸集列表；多文档时严格按名称匹配。
- `打印设置`：读取 Revit 当前模型中的 `PrintSetting`；默认可使用 `<当前打印设置>`。
- `PDF 文件名`：支持现有 `{模型名}` 模板。

为避免误打印到实体打印机，执行时打印机名称必须包含 `PDF`。

## 执行逻辑

1. 读取目标视图/图纸集，并拒绝不可打印视图。
2. 保存当前 Revit 打印状态：打印机、PrintRange、PrintToFile、CombinedFile、文件名、PrintSetting、ViewSheetSet。
3. `SelectNewPrintDriver()` 切换到所选 PDF 打印机；切换后重新获取 `PrintManager`。
4. 可选应用同名 `PrintSetting`；多文档缺少同名设置时明确失败，不自动回退。
5. 先设置 `PrintRange.Select`，再把目标视图写入 In-Session `ViewSheetSet`。
6. 固定 `PrintToFile=true`、`CombinedFile=true`，先打印到 YangTools 临时 PDF。
7. `SubmitPrint()` 成功后等待打印驱动落盘；60 秒内必须出现可独占读取且非空的 PDF。
8. 成功后用现有安全替换逻辑把临时 PDF 替换为最终路径。已有 PDF 只有在新文件成功后才替换；替换失败时尽力恢复原文件。
9. `finally` 中尽力恢复打印前的 Revit 打印状态。

## PDF 打印驱动限制

`PrintManager.PrintToFileName` 是 Revit 的打印到文件路径，但最终是否能够“无弹窗自动保存”还取决于 Windows PDF 打印驱动。

已知风险：`Microsoft Print to PDF` 在部分 Revit/Windows 环境中仍会弹出“另存为”窗口，因此不应直接假设其可用于无人值守批处理。UI 已明确提示此限制。

正式验收必须记录本机每个 PDF 打印机的行为：

- `AUTO_SAVE_PASS`：无需弹窗，能按指定路径自动生成 PDF。
- `INTERACTIVE_ONLY`：出现另存为/驱动窗口，只适合人工交互打印，不作为批处理自动化通过。
- `FAILED`：SubmitPrint 失败、输出文件缺失或打印设置不兼容。

## 数据与状态保护

- 不修改模型几何或参数。
- 不创建/保存新的 ViewSheetSet 或 PrintSetting，只使用 In-Session 选择集。
- 不在打印前删除已有 PDF。
- 打印失败时清理临时文件。
- 打印结束后恢复用户原来的 Revit 打印状态；恢复失败会写入批处理警告。

## 兼容性

使用的核心 API（`PrintManager`、`PrintRange.Select`、`ViewSheetSetting`、`PrintSetup`、`PrintSetting`、`SubmitPrint`、`PrintToFileName`）均为长期存在的 Revit 打印 API，因此源码设计目标为 Revit 2021–2027。

但本轮新增代码后，之前 FINAL3 的编译证据不能自动覆盖当前源码。必须重新执行对应年份编译；至少在 Revit 2021、2024、2025 中做打印专项实机。

## 静态检查

新增 `tools/Test-BatchPrintPdf.py`，覆盖：

- UI 与 ViewModel 接线；
- 用户设置持久化；
- 打印机发现；
- `PrintRange.Select` 顺序；
- In-Session 视图集；
- 临时输出与安全替换；
- 打印状态快照/恢复；
- 防误实体打印机；
- 多模型严格打印设置匹配。

静态契约不能替代 Windows/Revit/打印驱动实机。
