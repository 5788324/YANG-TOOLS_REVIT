# DeepSeek / Codex 执行任务：删除 AI/摸鱼助手 + 族类别修改

日期：2026-08-14
版本：YangTools 3.1.35
输入源码：`YangTools_Project_3.1.35_Source_ASSISTANT_CLEANUP_20260814.zip`

## 0. 角色和禁止事项

你负责 Windows 真实编译、部署和 Revit GUI/模型验收。用户不参加手工测试。

禁止：

- 推送 Git；
- 增加无关功能；
- 跨年份复用 DLL；
- 把静态检查写成 Revit 实机 PASS；
- 跳过或保留 `%APPDATA%\YangTools\Assistant\` 及旧 copilot/moyu 历史/设置文件；
- 发现失败后用旧 DLL 覆盖当前源码结果。

## A. 全新源码与静态基线

1. 解压到全新空目录，不覆盖旧源码。
2. 记录源码 ZIP SHA-256。
3. 运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Verify-SourceBaseline.ps1
python .\tools\Test-UiPanelsStatic.py --root .
python .\tools\Test-FamilyCategoryChange.py
python .\tools\Test-AssistantDataCleanup.py
python .\tools\Test-ReviewFixes.py
python .\tools\Test-BatchLinkPathMatching.py
python .\tools\Test-AlignTextWidth.py
python .\tools\Test-GeneratorLevelPlacement.py
python .\tools\Test-BatchPrintPdf.py
python .\tools\Test-ChineseCleanup.py
python .\tools\Test-LinearElevationProfile.py
python .\tools\Test-ParameterCleanupPipeline.py
```

预期：

- UI 0 error / 0 warning；
- Ribbon=28；
- FamilyCategoryChange/AI removal 53/53；
- 其它专项全部保持通过。

若实际数量因本轮工具文档更新变化，以工具真实输出为准，但失败必须修复后才能继续。

## B. AI / 摸鱼助手删除硬门禁

源码和最终部署目录均检查：

- 不存在 `CopilotCommand.cs`；
- 不存在 `Core/Assistant/`；
- 不存在 `Core/RevitApiSearcher.cs`；
- 不存在 Assistant/Copilot 窗口、UserControl、DockablePane provider；
- 不存在 `copilot_16.png` / `copilot_32.png`；
- `App.OnStartup` 不调用 `RegisterDockablePane`；
- 反射程序集命令中无 `CopilotCommand`；
- Ribbon 无“AI 助手/摸鱼助手”；
- 启动前创建测试用旧 Assistant 数据后，启动当前 YangTools 应删除 `%APPDATA%\YangTools\Assistant\`；
- `copilot_settings.json` / `copilot_history.json` / `moyu_settings.json` / `moyu_history.json` 也应被清理；
- `%APPDATA%\YangTools\MCP` 和其它现用设置不得被误删。

**当前必须主动清理旧 Assistant 用户数据。** 用户已明确授权删除旧历史、设置和 API Key。必须同时验证安装/更新、直接部署、Revit 启动和卸载路径均可幂等清理，但不得扩大到 MCP 或其它仍在使用的 YangTools 数据。

MCP 必须仍正常：系统设置可见 MCP 配置/状态，网关按配置启动，`YangTools.Revit.Mcp` 类型可以加载。

## C. Windows 独立编译

优先运行项目既有 `Build-All.ps1`，每年份独立：

- 2021：net48
- 2022：net48
- 2023：net48
- 2024：net48
- 2025：net8.0-windows
- 2026：net8.0-windows
- 2027：net10.0-windows，仅安装 .NET 10 SDK 后执行；否则明确 NOT_BUILT/NETSDK1045。

每个年份保存：

- restore.log；
- build.log；
- build.binlog；
- errors/warnings；
- DLL 大小；
- DLL SHA-256。

编译错误只修真实错误，修后重新跑全部静态门禁并同步文档。

## D. 部署

部署当前源码生成的对应年份 DLL，至少：

- Revit 2024；
- Revit 2025。

如本机还有 2021–2023，可同步部署但不要跨年复用 DLL。

保存部署 DLL SHA-256，必须等于该年份 build DLL SHA-256。

## E. Revit 启动冒烟

2024、2025：

1. 启动 Revit；
2. 记录 Journal；
3. 28 个 YangTools Ribbon 命令注册；
4. 项目管理区出现 `族类别修改`；
5. 不存在 `AI 助手`；
6. 系统设置能打开；
7. MCP 仍能启动/停止或读取状态；
8. 无 Assistant DockablePane 注册异常。

## F. 族类别修改核心实机

必须在**测试模型副本**执行。优先用用户真实项目里 `Abutments` Family；若项目没有，使用一个普通可编辑载入族作为替代，但报告中不得写“Abutments 已实测”。

### F1 单族 Abutments → Generic Models

修改前记录：

- Family Name / Id / Category；
- FamilySymbol Id 列表、名称、数量；
- FamilyInstance Id 列表、数量；
- 每个类型/实例所有 `Parameter.Id > 0 && HasValue` 参数：ParameterId、StorageType、内部值；
- `Document.IsModifiable`。

在 UI：

1. 搜索目标 Family；
2. 单独勾选；
3. 目标类别选 `Generic Models / 常规模型`；
4. 执行并确认。

修改后验证：

- `Family.FamilyCategory` 对应 `OST_GenericModel`；
- Family Id 不变；
- FamilySymbol Id 集合不变；
- 原 FamilyInstance Id 均存在且仍属于该 Family；
- 正 ID 自定义参数内部值逐项一致；
- 无新旧重复 Family/实例；
- `Document.IsModifiable == false`。

任何 ID/自定义参数值不一致：FAIL，当前代码应触发整个 TransactionGroup 回滚；若没有回滚则必须修代码。

### F2 多族批量

选择至少 3 个可编辑载入族，来源类别尽量不同，统一改到 Generic Models。

要求：

- 兼容族全部成功；
- 修改前已经是 Generic Models 的族明确 SKIP；
- 不兼容/不可编辑项明确失败或跳过；
- 回载前失败不能阻塞其它安全项；
- 一旦出现“回载后类别/ID/自定义参数值异常”，应整批回滚，而不是留下部分成功。

### F3 不可编辑族

验证：

- InPlace Family；
- 不可 EditFamily Family；
- 系统族不应被当作可编辑载入族执行。

要求无崩溃、无项目改写。

### F4 目标类别兼容性

尝试一个该 Family 模板不允许的目标 Category。

要求：

- 明确失败原因；
- Family 未回载项目或项目未改变；
- 其它可安全处理项按设计继续；
- `IsModifiable=false`。

### F5 撤销

完成一个多族成功批次后执行一次 Ctrl+Z。

要求：

- 全批次恢复原 FamilyCategory；
- Family / Symbol / Instance ID、数量和正 ID 参数值保持原值；
- 不产生重复族或实例。

若 Revit 的 `LoadFamily` 无法被外层 TransactionGroup 作为一个 Undo 项聚合，必须记录真实行为并修订实现/说明，不能伪造“一次撤销”。

## G. 既有单族“族文档管理 → 改类别”回归

现有 FamilyManager 单族 ChangeCategory 功能至少跑一个最小样本，确认新功能没有破坏旧入口。

## H. 文档与证据

完成后同步：

- README.md；
- PROJECT.md；
- WORKLOG.md；
- docs/HANDOFF_3.1.35.md；
- docs/FEATURE_AUDIT_3.1.35.md；
- docs/REVIT_2021_2027_COMPATIBILITY_3.1.35.md；
- 本任务结果。

证据包至少包含：

- 最终源码 ZIP；
- build logs/binlogs/DLL；
- deployment hashes；
- Journal；
- GUI 截图；
- family-category-results.json（前后 ID/参数/类别）；
- MCP 状态结果；
- SHA256SUMS.txt；
- FINAL_VERDICT.md。

## I. 判定

只有以下条件满足，才可把本轮 `族类别修改` 标为 GO：

- 当前源码真实编译；
- 2024/2025 至少一个真实 loadable family 类别修改通过；
- ID 和正 ID 自定义参数值完整；
- 安全异常会回滚；
- 撤销行为有 Journal/结果证据；
- AI/摸鱼运行入口和代码确实不存在；
- MCP 未被误删。

Revit 2027 未构建或其它 GUI 功能未覆盖，不影响本专项 GO，但项目总体继续按兼容矩阵判定。
