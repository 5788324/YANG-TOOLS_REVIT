# DeepSeek / Codex：YangTools 3.1.35 打印 PDF 编译部署与专项验收任务

## 角色

你在 Windows + Revit 本机执行。不要新增功能，不推送 Git，不做无关重构。只修真实编译错误或实机证明存在的问题。

用户不参与手工测试；能自动完成的验证由你执行。必须保留原始日志、Journal、结果 JSON 和哈希。

## 输入基线

以本任务同批交付的 `YangTools_Project_3.1.35_Source_BATCH_PRINT_PDF_20260807.zip` 为唯一源码基线。必须解压到全新空目录，不覆盖旧源码目录。

## A. 源码与静态门禁

1. 运行 `Verify-SourceBaseline.ps1`。
2. 运行：
   - `python tools/Test-UiPanelsStatic.py --root .`
   - `python tools/Test-ReviewFixes.py --root .`
   - `python tools/Test-BatchLinkPathMatching.py`
   - `python tools/Test-AlignTextWidth.py --root .`
   - `python tools/Test-GeneratorLevelPlacement.py --root .`
   - `python tools/Test-ParameterCleanupPipeline.py`
   - `python tools/Test-BatchPrintPdf.py --root .`
3. `Test-BatchPrintPdf.py` 必须 32/32。
4. 任何失败先定位真实原因，不允许删检查规则换 PASS。

## B. 独立编译

因为产品代码已变化，旧 FINAL3 DLL 不能复用。

逐年份独立编译：

- 2021 / net48
- 2022 / net48
- 2023 / net48
- 2024 / net48
- 2025 / net8.0-windows
- 2026 / net8.0-windows
- 2027 / net10.0-windows（仅当前机已具备 .NET 10 SDK 时执行；否则保留 NETSDK1045 并标记 NOT_BUILT）

每个年份保存：

- restore.log
- build.log
- build.binlog
- DLL 大小
- DLL SHA-256
- error / warning 数

禁止跨年份复用 DLL。

## C. 部署

至少部署实际安装的 Revit 2021、2024、2025；如 2022/2023 已安装也可一起部署做最小冒烟。

部署到 `%APPDATA%\Autodesk\Revit\Addins\<年份>\YangTools\`，部署 DLL 必须与该年份构建 DLL SHA-256 一致。

## D. Revit 2021 专项——这是本功能关键版本

### D1. UI

打开 `总控中心 → 批处理与云链接`：

- “导出 PDF（Revit 2022+）”必须禁用。
- “打印 PDF（虚拟 PDF 打印机，Revit 2021+）”必须可用。
- PDF 打印机、打印设置、视图/图纸集、PDF 文件名控件正常显示。

### D2. 打印机枚举

记录 UI 中发现的全部 PDF 打印机名称，并与 Windows 实际安装打印机对比。

如存在 `Microsoft Print to PDF`，必须单独记录其行为，不得把出现保存对话框写成自动化 PASS。

### D3. 单视图/单图纸自动打印

对每个可用 PDF 打印机分别测试：

1. 当前视图或一张图纸；
2. 文件名 `{模型名}-print-test`；
3. 输出到专用临时目录；
4. 记录 `SubmitPrint` 返回、是否出现弹窗、最终文件路径、大小、SHA-256。

分类：

- `AUTO_SAVE_PASS`
- `INTERACTIVE_ONLY`
- `FAILED`

只有 `AUTO_SAVE_PASS` 才能作为无人值守批处理打印机。

### D4. 视图/图纸集

使用一个至少包含 2 张可打印图纸的已保存 ViewSheetSet：

- 输出应为一个合并 PDF；
- 页数至少 2（如当前环境可自动读取 PDF 页数则记录；否则记录文件大小并通过打印结果/Journal 旁证）；
- 不得修改原命名 ViewSheetSet 内容。

### D5. 打印设置

分别测试：

- `<当前打印设置>`；
- 一个实际存在的命名 PrintSetting；
- 外部模型缺同名 PrintSetting：必须明确失败，不允许自动回退。

### D6. 覆盖保护

1. 最终 PDF 不存在：成功创建。
2. 已存在且“覆盖”关闭：不得提交打印，不得改变旧文件 SHA。
3. 已存在且“覆盖”开启：先打印临时文件，再替换；成功后 SHA 应变化。
4. 锁定旧 PDF：新打印/替换失败时旧文件 SHA 必须保持不变。

### D7. 打印状态恢复

打印前记录：

- PrinterName
- PrintRange
- PrintToFile
- CombinedFile
- PrintToFileName（可读时）
- CurrentPrintSetting 名称/InSession
- CurrentViewSheetSet 名称/InSession 与视图 ID

打印结束后再次读取，必须尽可能与打印前一致。任何差异都必须写入结果 JSON，不能静默忽略。

## E. Revit 2024 / 2025 回归

至少各做：

1. 打开插件并确认 28 Ribbon 注册；
2. 打开批处理窗口；
3. 现有“导出 PDF”仍可用；
4. 新“打印 PDF”入口可用；
5. 使用已在 D 阶段判定为 `AUTO_SAVE_PASS` 的同一 PDF 驱动完成一份 PDF；
6. 验证旧的 PDF 导出 B6 保护逻辑没有回退。

## F. Microsoft Print to PDF 特别规则

Autodesk 社区存在长期记录：该驱动在 Revit API 下即使设置 `PrintToFileName`，部分环境仍会弹出“另存为”。

因此：

- 如弹窗：标记 `INTERACTIVE_ONLY`，关闭/取消弹窗后继续其它打印机测试；不得伪写 `AUTO_SAVE_PASS`。
- 如当前机器只有 Microsoft Print to PDF：本功能源码/编译可以 GO，但“无人值守批量打印 PDF”必须标记条件 GO，等待一个支持自动保存的 PDF 驱动，或后续单独设计交互模式。
- 不允许通过 Task.Run/后台线程调用 Revit PrintManager 绕过弹窗。

## G. 证据

输出到 `artifacts/evidence-print-pdf/`：

- `FINAL_VERDICT_PRINT_PDF.md`
- `PRINT_PDF_VERIFICATION_REPORT_20260807.md`
- `runtime-print-pdf-20260807.json`
- Revit Journal
- YangTools 日志
- 构建日志/binlog
- 打印前后状态 JSON
- PDF 文件大小与 SHA-256
- `SHA256SUMS-20260807.txt`

## H. 文档同步

若真实编译/实机暴露问题并修复，必须同步：

- README.md
- PROJECT.md
- WORKLOG.md
- docs/HANDOFF_3.1.35.md
- docs/FEATURE_AUDIT_3.1.35.md
- docs/REVIT_2021_2027_COMPATIBILITY_3.1.35.md
- docs/BATCH_PRINT_PDF_3.1.35_20260807.md

## 最终判定规则

- 源码静态 + 2021–2026 编译通过：源码 GO。
- 至少一个实际 PDF 驱动在 Revit 2021/2024/2025 达成 `AUTO_SAVE_PASS`，且状态恢复/覆盖保护通过：打印 PDF 功能 GO。
- 只有交互式 PDF 驱动：功能条件 GO，不允许声称无人值守批量打印已通过。
- 2027 未构建不得影响 2021–2026 的单独结论，但全版本发布仍 NO-GO。
