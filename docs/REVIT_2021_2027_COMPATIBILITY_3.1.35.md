## 2026-08-21 线性布置 P0 真实 Z 门禁的版本影响

- 新增校验仅使用现有 Revit API：`LocationPoint.Point.Z`、`Parameter.AsDouble/Set`、`Document.Regenerate`，未引入新的年份 API 条件分支。
- 2021–2027 的兼容设计不变，但当前源码必须各年份独立编译；最低实机仍优先 2024/2025。
- 每个 level-based 实例必须用真实 `LocationPoint.Z` 验证理论世界 Z，允许误差 `<=1 mm`；第一个可写 Offset 不再自动视为有效。
- 当前源码新增 C#，所以 `Source_SIMPLE_MATERIAL_20260821` 及其之前的任何 DLL 都不能证明本候选已编译/实机通过。
- 状态：`STATIC GO / WINDOWS RETEST REQUIRED`。

## 2026-08-21 简易绑定材质的版本影响

- 新增简易版使用 `UIDocument.Selection.PickObjects`、`FamilyInstance`、`Material`、`Parameter.Set(ElementId)`、`TransactionGroup/SubTransaction` 等长期 API。
- Material 参数类型校验继续分支：Revit 2021 使用 `ParameterType.Material`，Revit 2022+ 使用 `SpecTypeId.Reference.Material`。
- 当前源码新增 C#/XAML，上一 MATERIAL_BINDING 或 FINAL3 DLL 均不代表本轮源码；2021–2027 Configuration 必须重新独立构建。
- 最低实机：Revit 2024/2025 各验证简易实例参数、简易类型参数、批量版、同名冲突、不可编辑族、Ctrl+Z；2021 至少验证编译和简易窗口/材质参数旧 API 路径。
- Revit 2027 仍要求 .NET 10 SDK；未构建前不得宣称兼容。

## 2026-08-14 “绑定材质”的版本影响

- 核心 API 为 `Document.EditFamily`、`FamilyManager.AddParameter`、`BuiltInParameter.MATERIAL_ID_PARAM`、`FamilyManager.AssociateElementParameterToFamilyParameter`、`Document.LoadFamily`。
- Revit 2022+ 使用 ForgeTypeId 材质参数创建方式；Revit 2021 保留 `BuiltInParameterGroup.PG_MATERIALS + ParameterType.Material` 条件编译，因此设计目标仍为 2021–2027 独立编译。
- 关联查询/解绑/可关联性采用运行时能力探测以降低跨年份 API 差异风险；实际行为必须由目标 Revit 年份实测。
- 当前源码新增 C#/XAML，上一 AssistantCleanup/REMOVE_AI_FAMILY_CATEGORY DLL 不代表本轮构建。最低门禁：2024/2025 类型参数、实例参数、覆盖已有绑定、多族回载、数据完整性和 Ctrl+Z。
- Ribbon 当前总数 30；2027 仍需要 .NET 10 SDK 才能建立构建证据。

# YangTools 3.1.35 Revit 2021–2027 兼容性说明

## 2026-08-14 AI 删除 / 族类别修改的版本影响

- 删除 AI/摸鱼助手后不再编译 Assistant/Copilot UI、DockablePane provider、网络客户端和 `RevitApiSearcher`；MCP 独立保留。
- 新 `族类别修改` 使用 `Document.EditFamily`、`Family.FamilyCategory`、族文档 Transaction、`Document.LoadFamily`、`Family.GetFamilySymbolIds`、FamilyInstance 与 Parameter 内部值读取，按 Revit 2021+ 共用 API 设计。
- Family Category 只能对可编辑载入族执行；系统族 API 编辑不在支持范围。目标类别是否合法仍由具体 family document/Revit 版本决定。
- 回载后增加 Family/FamilySymbol/FamilyInstance ID 与正 ID 自定义参数内部值门禁；任何异常要求当前批次回滚，避免类别变更导致自定义数据静默变化。
- 本轮产品 C#/XAML/依赖集合已变化，上一 LINEAR_LEVEL_OFFSET_FIX/FINAL3 DLL 均不能作为当前源码构建证据；必须重新独立编译。最低实机：Revit 2024/2025 的 `Abutments → Generic Models`（有该族时）或等价载入族样本。
- 2027 仍需要 .NET 10 SDK 才能建立真实构建证据。

## 2026-08-13 ProjectElevation 修复的版本影响

- `Level.ProjectElevation` 在 Revit 2021 API 中已存在，且定义为“始终相对项目原点，不受 Elevation Base 设置影响”；因此本修复不增加新版本 API 门槛。
- 线性布置世界 XYZ 必须基于 `ProjectElevation`。`Elevation` 可能按共享坐标显示，只能用于 UI/诊断，不能用于世界 Z。
- 本轮改动产品 C#/XAML，必须重新独立构建实际目标年份。最低实机回归：Revit 2024/2025，在 Level.Elevation 与 Level.ProjectElevation 不同的项目中验证首尾和中间世界 Z。


## 2026-08-12 线性布置高程控制的版本影响

- 新增逻辑使用 Revit 2021+ 已存在的 `Level.ProjectElevation`、`Level.Elevation`、`XYZ`、`FamilyInstance`、`Transaction`、`ElementTransformUtils` 和项目既有 `FamilyPlacementHelper`，未主动引入新的年份条件分支。
- 高程单位入口统一为 mm，进入引擎后转换为 Revit 内部英尺；世界 Z 明确包含所选 Level 自身高程，因此不能用“标高=0”的测试替代跨标高测试。
- 当前源码产品 C#/XAML 已变化，上一 CHINESE_CLEANUP/FINAL3 编译 DLL 不再代表本轮源码。最低实机矩阵应包含 Revit 2024、2025；其它实际安装年份执行启动 + 一次高程布置最小回归。
- 2027 仍需要 .NET 10 SDK 才能建立真实构建证据。


## 2026-08-07 中文检查与清理升级的版本影响

- 新功能使用的主要 API（Element.Name、Document.Delete、Transaction/SubTransaction、TextNote.Text、WorksetTable.RenameWorkset、ViewSheet.SheetNumber、ScheduleField.ColumnHeading、SpatialElement.Number）均按 Revit 2021+ API 设计。
- 未引入仅 2025+ 的 UI/线程模型；仍使用模态窗口，写操作发生在有效 ExternalCommand 上下文。
- 本轮修改产品 C#/XAML，因此上一 BATCH_PRINT_PDF/FINAL3 DLL 不能作为**当前源码**构建证据。必须重新独立构建 2021–2026；2027 仍取决于 .NET 10 SDK。
- 历史 FINAL3 已在本机完成 2021–2026 编译，其中 2025 还完成真实启动、28 个 Ribbon 注册和 MCP/DLL 加载冒烟；这些证据仅证明未加入“打印 PDF/中文检查与清理”之前的产品基线。
- 最低实机门禁：2021、2024、2025 窗口扫描 + 一次重命名 + 一次删除/清空 + Ctrl+Z。

## 2026-08-07 PrintManager 打印 PDF 的版本影响

- 新打印路径使用长期存在的 `PrintManager`、`PrintRange.Select`、`ViewSheetSetting`、`PrintSetup`、`PrintSetting`、`SubmitPrint` 和 `PrintToFileName`，设计目标覆盖 Revit 2021–2027。
- Revit 2021 的原生 `PDFExportOptions` 仍不可用；区别是新“打印 PDF”不依赖该 API，因此 Revit 2021 UI 中“导出 PDF”保持禁用、“打印 PDF”可启用。
- Windows PDF 驱动是外部依赖。某年份源码编译通过，并不代表该机器上的 PDF 打印驱动支持无人值守文件名；必须按实际驱动验收。
- 尤其 `Microsoft Print to PDF` 可能弹出“另存为”窗口，应标记 `INTERACTIVE_ONLY`，除非该具体机器实测可无弹窗输出。
- 本轮修改产品代码，FINAL3 之前的构建矩阵是**上一基线历史证据**。当前 PrintPDF 源码需重新编译后才能更新本表当前构建状态。

## 2026-08-07 FINAL3 交付修正

- FINAL3 不修改产品源码或目标框架；FINAL2 的编译 DLL 与 Revit 2024 实机证据继续有效。
- 最终源码包静态检查已排除 Revit Journal/运行日志的宿主编码，源码、XAML、文档和配置仍执行 UTF-8 门禁。
- 独立证据包与源码包使用新的外层 SHA-256；旧嵌套 `artifacts/evidence.zip` 已移除。


## 2026-08-07 文本段落宽度同步的版本影响

- 新增链路使用长期存在的 `TextNote.Width`、`TextNote.GetMinimumAllowedWidth(Document, ElementId)`、`GetMaximumAllowedWidth`、`SubTransaction` 和 `ElementId`，源码不增加年份条件分支。
- 代码层目标仍为 Revit 2021–2027 共用；但不同年份、不同文字类型、对齐方式和引线组合的实际行为仍需独立编译和 Revit 实测。
- 2024 为强制功能验收版本；其它实际安装年份至少执行“打开窗口、仅同步宽度、组合 X/Y/旋转、Ctrl+Z”最小回归。
- 静态 API 存在性不等于七年份兼容完成，当前兼容矩阵状态不升级。

## 2026-08-05 报告复核修复的版本影响

- 新增修改主要使用既有稳定 API：`Transaction/SubTransaction/TransactionGroup`、`Level`、`LocationPoint`、`ElementTransformUtils` 和 WPF DataGrid 属性。源码仍共用 2021–2027 条件编译。
- Revit 2021 编译下 `CanExportPdf=false`，PDF 复选框禁用；2022+ 才启用 `PDFExportOptions` 路径。
- FamilyManager 的 `familyDoc.LoadFamily(projectDoc, ...)` 保持在项目文档非 Modifiable 状态调用，禁止按误报添加项目 Transaction。
- 当前只完成源码静态复核；所有年份仍需独立编译。Revit 2024 需优先验证项目资产表头、四个管理窗口、线性/基于面/标高/布尔和批处理。


## 2026-07-30 生成器标高修复的版本要求

- Loft 与实体提取新增共享放置助手，只使用长期稳定的 `NewFamilyInstance(XYZ, FamilySymbol, Level, StructuralType)`、`Element.LevelId`、`LocationPoint`、`Document.Regenerate` 和 `ElementTransformUtils.MoveElement`。
- 源码为 2021–2027 共用逻辑，但“API 长期存在”仍不能替代每个年份的真实编译。
- 2024 为本轮强制专项版本；2021–2023、2025–2027 至少先完成对应 Configuration 编译，实际安装年份再做最小位置回归。
- `final.7z` 的构建报告互相矛盾且缺少日志/DLL/binlog，本文件不采纳其 GO 结论。


## 2026-07-29 复核状态

- 已静态确认项目、解决方案、构建脚本和安装器均按 2021–2027 独立配置。
- 已静态确认输出路径按年份隔离，Addin 清单使用相对路径，普通构建默认不部署。
- 尚未在 Windows 还原 NuGet 或编译任何 3.1.35 Configuration；表中“未编译/未实测”状态不变。
- 当前源码可进入 Windows 编译，但不能列入“已兼容”版本名单。

## 当前可以确认的内容

当前源码基线已经在 FINAL3 之后连续新增打印 PDF、中文清理、线性高程修复，随后删除 AI/摸鱼助手、加入族类别修改、批量绑定材质和简易绑定材质，因此旧 DLL 不能标记为当前源码编译通过。应区分**上一已编译基线**和**当前源码待重新编译**：

| Revit | 项目目标框架 | 上一 FINAL3 证据 | 当前 SIMPLE_MATERIAL 源码 |
|---|---|---|---|
| 2021 | .NET Framework 4.8 | 编译 0 错误并部署过 | **待重新编译/实机** |
| 2022 | .NET Framework 4.8 | 编译 0 错误并部署过 | **待重新编译/实机** |
| 2023 | .NET Framework 4.8 | 编译 0 错误并部署过 | **待重新编译/实机** |
| 2024 | .NET Framework 4.8 | 编译 0 错误；上一基线部分专项实机通过 | **待重新编译；绑定材质/族类别修改/中文清理/线性高程当前源码待实机** |
| 2025 | .NET 8 Windows | 编译 0 错误、已部署；上一基线启动/28 Ribbon/MCP 加载冒烟通过 | **待重新编译；绑定材质/族类别修改/中文清理/线性高程当前源码待实机** |
| 2026 | .NET 8 Windows | 编译 0 错误；未做该年份 Revit 实机 | **待重新编译/实机** |
| 2027 | .NET 10 Windows | 未构建（缺 .NET 10 SDK，NETSDK1045） | **未构建** |

## 不应作出的声明

目前不能写“所有功能已经兼容 Revit 2021–2027”。源码配置正确，不等于七个版本的 API、WPF、事务、导出、族编辑、MCP 和模型操作全部通过。

## 正式兼容的最低证据

每个年份至少需要：

1. 对应 Configuration 编译成功；
2. 对应年份 DLL 独立部署，禁止跨年份复用；
3. Revit 启动无加载异常；
4. 30 个 Ribbon 入口启动烟雾测试；
5. 至少一个只读功能和一个写模型功能执行成功并可撤销；
6. 该年份敏感功能专项回归，包括参数 API、几何、族保存/载入（含族类别修改、绑定材质）、批量导出、MCP 和主题资源。

## 临时插件箱的额外限制

YangTools 宿主支持某个 Revit 年份，不代表临时 DLL 自动兼容该年份。每个临时插件必须针对当前 Revit 年份、对应 Revit API 和目标框架独立编译，并在说明文件中声明 `SupportedRevitVersions`。

## 当前可信表述

> 上一 FINAL3 产品基线已完成 Revit 2021–2026 独立编译，其中 Revit 2024 有部分专项实机、Revit 2025 有启动/28 Ribbon/MCP 加载冒烟；当前 SIMPLE_MATERIAL 源码又修改了产品 C#/XAML 和依赖集合，必须重新编译后才能建立新的兼容矩阵。Revit 2027 尚未构建。

## 2026-08-07 Windows 独立编译结果（最终）

以下为 `Source_REVIEW2_20260807` 在全新空目录中、dotnet SDK 8.0.403 / MSBuild 17.11.9 的真实编译结果。警告均为 nullable 类型提示（CS86xx），**未阻塞构建**；2024 构建 DLL 与部署 DLL SHA-256 一致。

| Revit | 目标框架 | 编译 | 错误/警告 | 实机 |
|---|---|---|---|---|
| 2021 | .NET Framework 4.8 | 通过 | 0 / 198 | 已装 Revit 2021，本轮未做该年份实机 |
| 2022 | .NET Framework 4.8 | 通过 | 0 / 198 | 已装 Revit 2022，本轮未做该年份实机 |
| 2023 | .NET Framework 4.8 | 通过 | 0 / 198 | 已装 Revit 2023，本轮未做该年份实机 |
| 2024 | .NET Framework 4.8 | 通过 | 0 / 198 | **本轮实机**（批处理/模型修改区专项） |
| 2025 | .NET 8 Windows | 通过 | 0 / 454 | **编译通过、未实机**（未装 Revit 2025） |
| 2026 | .NET 8 Windows | 通过 | 0 / 227 | **编译通过、未实机**（未装 Revit 2026） |
| 2027 | .NET 10 Windows | **未构建** | NETSDK1045（缺 .NET 10 SDK） | **未构建、未实机** |

- DLL 大小与 SHA-256 见 上一 FINAL3 独立 evidence 包中的 `build-summary-20260807.json` 与 `SHA256SUMS-20260807.txt`。
- 2024 实机通过不能替代 2021–2023/2025–2026 各年份的启动与回归；2027 未编译不得推断。
- 年份配置与警告数说明：`Nice3point.Revit.Api` 使用浮动版本 `$(Configuration).*`，不同 restore 可能解析不同补丁版本，DLL 哈希可能微变；年份配置本身已隔离。

