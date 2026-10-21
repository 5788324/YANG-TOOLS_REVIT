## 2026-10-21｜RC3H：删除钢筋测试12入口 / CAD文档语义守卫修复 / 2025-2026 API固定

- **当前唯一源码候选 RC3H**，从 RC3G 派生；RC3G 2021–2026 BUILD GO 是历史编译基线，**不能推断 RC3H 编译/运行通过**。
- 按用户指令删除“钢筋测试”下12个独立 `Rebar*Command` 命令类和26张对应图标，Ribbon 预期 44→32；同时撤销12个不再适用的老静态入口脚本与历史输出。**保留生产 GR01 渐变钢筋集**；被 GR01 或已发布 MCP wire 共同使用的 Rebar Core/业务服务仍保留，避免误伤依赖。MCP 入口没有删除，不要把本轮理解为删除所有钢筋后端能力。
- “从 CAD 粘贴”修复目标文档判定：原 `ReferenceEquals(Document, Document)` 变为通过 `Document.Equals` 比较 Revit 文档身份，保留原有 `ActiveView.Id`、只读、事务、回读、单次 Undo 防护。同步修正 CAD 转 Revit 图纸（两处）、布尔几何、实体提取、批任务中文档身份比较。**不能仅凭静态修复认定导入实机 PASS**。
- 2025 API/2026 API NuGet `RevitAPI` 与 `RevitAPIUI` 版本固定到 `2025.4.20` / `2026.4.0`（对应已知最小用户宿主补丁）；包可从 NuGet 精确引用但存在 unlisted 状态，Windows 离线环境能否拿到两个版本必须复测；2021–2024/2027 原有浮动版本待后续严格冻结。
- 增加删项残留编译硬门禁，拒绝将旧文件覆盖进 RC3H。新专项 `Test-RC3H-CadIdentityAndRebarRetirement.py`，全部61个现存 Test-*.py 静态脚本应按RC3H重新验收；不含Windows编译/GUI/RVT事务验证。
- 安全边界：Revit 2023当前生产使用，绝不允许测试者关闭、前台切换、部署或修改项目；DeepSeek仅进行全新解压隔离编译和证据采集，不推Git。
- 发布裁定：RC3H STATIC GO（取决于完整复测），WINDOWS BUILD/2025 CAD IMPORT/Undo NOT_TESTED，4.0.0 RELEASE NO-GO。

## 2026-10-14｜YangTools 4.0.0 RC3D Phase A 编译错误定点修复（SOURCE STATIC）

- 基于唯一 RC3C ZIP SHA `321A43E8630F445128EC41AB1E27080F9B4B6387D32D57A623214F7C17575940`。独立核对 Gemini V2 Revit 2025 evidence ZIP SHA `7F56521604FED9F9634415C8F5CF137672FD365406608308B69B5F5B76AE387F`，其源文件清单 651/651 与 RC3C 完全一致。dotnet build 2025 exit 1，DLL 不存在；C# CS1010/CS1003/CS1026/CS1002。用户报告另称 2024 同类失败，但此轮没有收到其独立证据 ZIP，未独立核验。
- 真正的源码缺陷位于 `src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml.cs` 1112–1113：普通 C# 字符串被物理换行拆开，RC3A 首次引入；改为 `Environment.NewLine` 拼接以保留提示语义。没有修改线样式复制的业务处理逻辑。
- 将 `tools/Invoke-RC3C-WindowsCompileOnly.ps1` 补为 UTF-8 BOM（Windows PowerShell 5.1 中文解析兼容），不更改其只编译不部署的执行逻辑。新增 `tools/Test-CSharpStringLexicalGate.py` 全工程词法守卫，检测普通 C# 字符串中的真实跨行及未闭合，同时允许合法逐字字符串、原始字符串和注释。
- 本批仅 SOURCE STATIC GO，Windows 重编译尚未发生，不能宣称编译 PASS；Revit GUI/模型/Undo 均 NOT_TESTED，正式发布 NO-GO。下一步由 Gemini 使用新 RC3D ZIP 直接 `dotnet build` 并回传两版本完整证据，禁止部署/代码自修复。
- Google Drive 为唯一同步主仓库，GitHub 保持不变。

## 2026-10-13｜YangTools 4.0.0 RC3C 族类型 Excel 导入修复（STATIC）

- RC3B → RC3C：族管理器“族类型 Excel 导入”从 UI 逐行提交改为单事务整批预检与回滚，校验跨族图元 ID、重复类型、参数歧义、类型名冲突和提交后内部值读回；遇 ExternalEvent 拒绝取消队列项。
- 增加 Windows 编译但不部署的只读验证入口 `tools/Invoke-RC3C-WindowsCompileOnly.ps1`；此环境 Linux 无 `dotnet`，**脚本尚未执行，Windows C# Build NOT_TESTED**。其他 Revit 加载/GUI/Undo 仍 NOT_TESTED。
- 交接与测试见 `docs/YANGTOOLS_4_0_0_RC3C_FAMILY_TYPE_EXCEL_AND_BUILD_GATE_20261013.md`。仅 SOURCE STATIC GO，4.0.0 RELEASE NO-GO。GitHub 不同步。

# 2026-10-13｜YangTools 4.0.0 RC3B 图框事件队列/批处理完成状态（SOURCE STATIC）

- 唯一输入 RC3A SHA：`4B79D0987CB2DFB94E88543139471DC87E0E8007534F355D45D8FD058CFEB7C5`，保持已有 27 条 MCP wire、Rebar、Ribbon、installer 与 csproj 不变。
- 图框换型：为 `RevitEventHandler` 添加可选 `onSkipped`、按操作 ID 取消未执行队列项；Revit 文档已切换导致 action 被跳过时清理图框 pending 与 ComboBox 状态。`Raise` 返回 Denied/TimedOut 或抛异常时，防止任务日后被另一事件意外执行。
- 换型预检图框实例及目标类型、只读文档；`Transaction` 提交前后核对 FamilySymbol；提交后读回问题与未提交分开报告，不能误告用户可直接重试。
- 批处理 UI：预检异常恢复 busy 按钮；偏好保存失败不阻断执行；输出目录自动打开与报告保存失败不会倒灌为“导出中断”；警告在结束对话框显示并尽可能写入报告。
- 新增静态专项 `tools/Test-RC3B-QueueBatchUI.py`，仅核对源码合同；不是 Revit 运行/视觉点击。
- 没有 Windows C# 编译或 Revit 实机。状态 `SOURCE STATIC GO` 取决于本轮独立复跑，正式 `RELEASE NO-GO`。

## 2026-10-13｜YangTools 4.0.0 RC3A（线样式复制 / 临时插件箱 UI 状态收口）

- 唯一输入 RC2Z；原有复制线样式在 WPF 中直接构造事务，未检查 Start/Commit 成功且先刷新 UI。新增独立 `ProjectAssetLineDuplicateService`，通过 OST_Lines.SubCategories 解析含负数的 CategoryId，冻结来源、校验过期选择，单事务复制线颜色、投影粗细及投影线图案，提交前后读回并分离模型成功与 UI 刷新失败。
- 临时插件箱扫描开始即清空上次选择并禁用运行/收藏/删除，扫描异常不再残留旧 DLL 列表；删除按钮初始禁用；只有双击当前实际条目才能触发运行，移除项目异常会给出诊断。用户设置保存失败不会把已成功执行的运行/删除误报为失败。
- 更新 RC2Y 静态测试对旧内部 helper 的范围断言，新增 `Test-RC3A-LineMicroUI.py` 67 项源码专项。源代码静态测试工作区 66/66；43 XAML XML PASS。Windows C# Build、Revit 加载、真实复制/窗口交互、Ctrl+Z **NOT_TESTED**。
- 本轮只改变源码和源码静态测试，不增加新功能，不改变 MCP 27 wire、Rebar、Ribbon、installer、csproj。不推送 GitHub，交接主仓库是 Google Drive。4.0.0 RELEASE NO-GO，正式稳定仍为 3.1.36 FINAL_E2E。

## 2026-10-13｜YangTools 4.0.0 RC2Z 图框 Excel / 级联删除 / PDF 恢复（静态候选）

- RC2Y canonical 唯一源继续；图框 Excel 导入复用实例参数单事务服务，非图框 ID 拒绝、元数据列不写参数，整批预检/回滚/读回并区分模型提交与 UI 刷新错误。
- 族实例及图纸删除增加跨文档、失效/重复 ID、预检集合/实删集合一致性验证，在事务组内读取目标是否已真正删除，防止未预检的额外连带删除静默提交。
- PDF 导出/虚拟打印覆盖统一走可故障注入的 CommitPdf：发生失败保留可恢复的原件备份与暂存文件；已成功输出但清理备份失败则发警告；新增 Windows .NET 故障注入 harness 的 5 种用例（尚未运行）。
- 图纸基本信息/参数/附加修订/图框单格参数补齐事务开始/提交状态检查，部分操作增加读回。静态测试增加 RC2Z 专项；无 Windows C# 编译/Revit GUI/模型/Undo 实测，本候选 RELEASE NO-GO。GitHub 不同步，Google Drive 为主。


## 2026-10-13｜YangTools 4.0.0 RC2Y（静态开发候选）

以 RC2X 第一批完整源码为唯一基线；族实例 Excel 导入由逐行部分提交改为独立 Service 整批单事务，参数稳定身份/读写/内部值读回，重复/只读拒绝、无变化不写；项目资产填充区域前后景图案、颜色及复制迁入新服务，事务与状态校验，修复异步 UI 刷新误报；批量导出 NWC/IFC/DWG 新输出成功但旧备份未清理时明确警告和保留路径，清理后查询失败不回滚已提交文件。保留原业务入口与 MCP/Rebar/Ribbon/installer。Google Drive 是主仓库，Git 本批不推。

**仅源码静态验收；没有 Windows Build/Revit Loaded/模型/Undo/GUI 的运行证据，4.0.0 RELEASE NO-GO；稳定仍 3.1.36 FINAL_E2E。**验收基线与源码哈希以 RC2Y 独立报告为准。

## 2026-10-13｜YangTools 4.0.0 RC2X 第一批（MCP/图纸 Excel/填充区域；源码静态）

- RC2W canonical 为唯一输入，修 MCP 执行中超时“返回失败但操作可能继续”的误判；加入排队/执行中三态与 operationId 日志，**不更改 27 个 MCP wire 字段**。
- `SheetManager` Excel 整批导入改 `Core/Sheets/SheetExcelImportService`，缺失/重复/只读参数、重复图号或任一异常均拒绝/整批回滚；单 Transaction，一次 Undo；不再允许参数静默跳过，严格拒绝重复同名参数。
- 项目资产“填充区域名称/遮罩”改独立 `ProjectAssetFilledRegionEditService`，no-op、事务、回滚、模型读回和关闭/切 Tab 的过期 UI 保护。
- 新增 RC2X 静态专项及 Windows 验收清单；首次工作区全量 63/63 PASS、专项 88/88（后续最终以验收报告为准）。Windows Build/Revit GUI/Undo NOT_TESTED；**4.0.0 RELEASE NO-GO**、稳定版 3.1.36 FINAL_E2E。RC2X 未全收口；剩余批处理、其余微工具及项目管理仍在后续任务中。
- 详见 `docs/YANGTOOLS_4_0_0_RC2X_PHASE1_MCP_SHEETS_FILLEDREGIONS_20261013.md`。未推 Git，不要求用户手测。

## 2026-10-12｜YangTools 4.0.0 RC2W 临时插件箱安全收口（源码静态）

- 从 RC2V 继续，修临时插件收藏包 **整目录误删/同名普通目录覆盖**、缓存目录非 PID 误删、junction/symlink 遍历复制、仓库注册表非原子保存和 IOException 错误覆盖提示；帮助页面明确 DLL 执行无沙箱。新增 MicroTool 110 条源静态/路径策略专项；具体结果见独立验收报告。
- 保留原 MicroTool `IExternalCommand` 执行语义/说明清单，MCP 27 wire、Rebar、Ribbon、installer、csproj 不变。Windows C# Build/Revit Loaded/GUI/依赖加载/真实文件系统 NOT_TESTED；4.0.0 RELEASE NO-GO、稳定版 3.1.36 FINAL_E2E。

## 2026-10-12｜YangTools 4.0.0 RC2V（视图覆盖清理/文本/标注 Bug/UI）

- 以 RC2U 完整源码为基线；修复 ViewGraphicCleaner 统计异常静默变 0、清理跨视图部分提交及 UI 提前标成功，MergeText 提交后界面失败误报，DimTextOverride 无效目标跳过，DistributeText 非法间距；保留原有对齐/微工具业务语义。
- 新增 `tools/Test-P1ViewTextRC2V.py` 静态专项与专题文档；全部验收证据、ZIP SHA 和文件差异以 RC2V 独立 Acceptance 报告为准。
- Windows C# Build / Revit Loaded / GUI / 模型读回 / Undo / 跨版本 **NOT_TESTED**；4.0.0 **RELEASE NO-GO**、正式稳定 3.1.36 FINAL_E2E。MCP/Rebar/Ribbon/installer/csproj 均未改，未新增 MCP tools。

## 2026-10-12｜YangTools 4.0.0 RC2U（P1 CAD 导入与剖面 Bug/UI 安全）

- 从 RC2T 唯一完整源码继续，修复 PasteCad/ CAD→Revit/SectionByLine 三个命令：模态窗口返回后的原文档和视图身份校验；DWG 导入结果回读与提交/回滚状态明确区分；CAD 部分成功提示；剖面零向量与过短线、事务状态、提交后回读。
- 保留既有 CAD→Revit 逐对象失败/部分成功语义、共享坐标定位选项、原 MCP 27 wire、Rebar/Ribbon、安装器及 csproj。不宣称新增 MCP 命令。
- 详见 `docs/YANGTOOLS_4_0_0_RC2U_P1_CAD_VIEW_SAFETY_20261012.md`。Windows C# 编译、Revit GUI/模型和 Undo NOT_TESTED；4.0.0 RELEASE NO-GO，稳定 3.1.36 FINAL_E2E。

## 2026-10-12｜YangTools 4.0.0 RC2T 模型修改区横向收口（STATIC）

输入 RC2S SHA `8B471ACC72D26AA7FC10CA6DAEF77B08BF62341DF4A0E4DA120D23C2DD288AD3`。审查模型修改七功能共同的坐标、高程、事务、窗口状态并做定点 Bug 修复：Loft UI 的 `Elevation` 排序和失效标高默认回退；线性布置保存的族/标高失效自动替换、路径拾取在 ExternalEvent 验证前隐藏导致窗口不可见、关闭重复保存；沿线布柱 Base/Top Level + Offset 与插入点旧用 `Elevation` 的共享坐标偏差，修复为 `ProjectElevation` 且提交后回读两个 Level 和 Offset，UI 项目高程排序/旧选项失效保护。新增 `Test-ModelModifyRC2T.py` 145 项源码/UI 合约，增加横向收口专题文档。完整运行验证、SHA 和差异见 Acceptance；Windows/Revit 真正编译/加载/Undo 仍 **NOT_TESTED**，4.0.0 **RELEASE NO-GO**，正式稳定仍为 3.1.36 FINAL_E2E。下一批 RC2U 进入 P1 CAD 粘贴和视图相关模块。

## RC2S｜BooleanGeometry Bug/UI 安全审查与最小修复：实体 Union→Cut→Join、删除与回滚安全、族文档/项目 Transaction 语义、UI 长 ID/ESC 保护。见 docs/YANGTOOLS_4_0_0_RC2S_BOOLEAN_GEOMETRY_BUG_UI_20261010.md；下轮 RC2T 模型修改区整体收口。

## RC2R 模型修改区实体提取：优先 Bug/UI 与几何安全，静态修复详细见 docs/YANGTOOLS_4_0_0_RC2R_SOLID_EXTRACT_BUG_UI_20261010.md。保留现有 MCP/Rebar/Ribbon/installer，Windows Revit 未验证。

## 2026-10-10｜YangTools 4.0.0 RC2Q Loft 世界高度 / Bug/UI 保护

以 RC2P canonical 为源码基线。RC2Q 修正无效目标标高默认回退、轮廓重复/跨文档、源几何与局部 Loft 世界 Z 边界未校验以及提交前实例几何高度未核对；新增 UI 来源高度说明、长 ID tooltip、回滚未知状态。工作区与 ZIP 静态套件及专项结果以独立报告为准，**未执行 Windows C# 编译或 Revit 实机建模**，4.0.0 Release NO-GO，3.1.36 为稳定版。下一 RC2R 实体提取。

## 2026-10-10｜YangTools 4.0.0 RC2P 基于面转换 / Bug 与 UI 加固

以 RC2O 完整源码为基线。已审查 FaceBasedConverterWindow/Engine，修正部分实例失败仍提交、几何/参数复制静默丢失、危险删除旧实例、近似朝向分组、BBox 误定位与共享临时文件误删；修正全选/关闭按钮/最小窗口尺寸/危险删除提示。严格删除模式可能拒绝无法保留全部参数的任务；不能声称支持全部类型。源码测试与 Windows/Revit 结果以 RC2P Acceptance 为准；正式稳定版本 3.1.36 FINAL_E2E，4.0.0 Release NO-GO。下一批 RC2Q Loft。

## 2026-10-10｜4.0.0 RC2O 模型修改区标高修改（源码静态候选）
整批标高调整已下沉 LevelModifierRequest/Result/Service：预检、单事务、世界 ProjectElevation/Offset、宿主/镜像/结构重建保护、提交后 1mm 世界位置与类型回读。详见 docs/YANGTOOLS_4_0_0_RC2O_LEVEL_MODIFIER_20261010.md。Windows/Revit Runtime NOT_TESTED，4.0 RELEASE NO-GO，正式稳定版仍 3.1.36。

---

## RC2N（2026-10-10）｜模型修改区线性布置
线性布置增加点击参数快照、原文档 ExternalEvent、输入/数量/连续路线预检、单事务状态和提交后位置/类型回读；保留 ProjectElevation 基准和非 WPF Request。53/53 并发静态回归、136/136 专项；Windows/Revit NOT_TESTED，Release NO-GO。参见 docs/YANGTOOLS_4_0_0_RC2N_LINEAR_PLACEMENT_20261010.md。

## 2026-10-10｜YangTools 4.0.0 RC2M 模型修改区优先调整（STATIC ONLY）

从 RC2L canonical SHA `4BB68A47AF1E11B4F151BFFAD6C0B1D2D2B36F64F659BDD4091D05BF7856554A` 继续，先优化模型修改区。沿线布柱点击快照、旧 ExternalEvent/关闭防护、非法输入与 10000 根上限、单 Transaction/回滚状态/提交后回读与 UI 错误分离。新增 `ColumnAlongPathPlacementRequest` / `Result`，保留原几何算法、中文 XAML 与 Rebar/MCP/Ribbon。详细计划：`docs/YANGTOOLS_4_0_0_OPTIMIZATION_ROADMAP_MODEL_FIRST_20261010.md`，专题 `docs/YANGTOOLS_4_0_0_RC2M_COLUMN_ALONG_PATH_20261010.md`。Windows Build / Revit GUI / Undo 未测；4.0 RELEASE NO-GO，稳定 3.1.36。

---


## 2026-10-10｜YangTools 4.0.0 RC2L FamilyInstance 批量换型原子事务 / UI
从 RC2K 唯一开发源码推进。新增 `FamilyInstanceBatchTypeService` typed Request/Result/Replacement，替代逐实例 SubTransaction 部分提交；ChangeTypeId 可替换新图元 ID，记录并回读校验；WPF 保留中文入口，添加窗口/筛选/Tab/行/目标/重复请求保护。源代码、静态测试与 ZIP 门禁见 `docs/YANGTOOLS_4_0_0_RC2L_FAMILY_INSTANCE_BATCH_TYPE_20261010.md`。Windows Build、Revit Loaded/GUI/Undo 均 NOT_TESTED，4.0.0 RELEASE NO-GO。

## YangTools 4.0.0 RC2K（2026-10-10）

唯一最新开发候选 RC2K（基线 RC2J SHA `C8A32105F1C66390A4DE936EDA01F1B7C25DBD301B53671CFF06FF7B94D8F9B4`）；FamilyInstanceManager 批量写参数已拆 `FamilyInstanceBatchParameterService`，改为全目标预检+单 Transaction/失败整批回滚，no-op 不产生 Undo；UI 关闭/Tab/筛选/选择/后发请求与提交后刷新错误保护。保留现有 `BatchChangeType` 旧逐行提交路径（待办）、MCP/Rebar/Ribbon/XAML/csproj 不变。工作区/fresh ZIP 测试与 SHA 见验收报告，Windows/Revit Runtime NOT_TESTED、4.0 RELEASE NO-GO，稳定版 3.1.36。详情见 `docs/YANGTOOLS_4_0_0_RC2K_FAMILY_INSTANCE_BATCH_PARAMETER_20261010.md`。

---

## YangTools 4.0.0 RC2J（2026-10-08）

唯一最新开发输入 RC2J，以 RC2I 为基础，FamilyInstanceManager 逐格参数编辑接入 typed FamilyInstanceCellEditService；UI 过期回调与提交后显示状态区分；不修改批量换型、Excel、删除或 Rebar/MCP/Ribbon。49/49 静态 suites（workspace + fresh ZIP）与专项 94/94 PASS；Windows/Revit Build/Loaded/Undo 均 NOT_TESTED。4.0.0 RELEASE NO-GO；下一批 RC2K 优先审查 FamilyInstanceManager 批量写参数事务，再决定是否转下一模块。详情见 docs/YANGTOOLS_4_0_0_RC2J_FAMILY_INSTANCE_CELL_EDIT_20261008.md。

---

# 4.0.0 RC2I｜族实例管理器阶段重构（2026-10-08）

最新开发输入为 RC2I；基线 RC2H SHA `01FAF685ADE74D986485584D4D36870113C5B8206B88EAF02EFC3C43A2B210D2`。本批 Service 只读类型选项与 UI 排队读请求过期保护；Windows/Revit 尚未 Build/Loaded/Undo，Release NO-GO、3.1.36 仍正式稳定版。详情见 RC2I 专题。

---

## 2026-10-08｜YangTools 4.0.0 RC2H（仅源码候选）
ProjectAssetManager 线图案选择的提前更新 VM 问题，改为 Explicit ComboBox binding + typed LinePattern Service。补提交流转/读取状态区分，项目资产模块做阶段性收口，填充区域及复制路径列为 BACKLOG；下一步 FamilyInstanceManager。Windows/Revit Build/GUI/Undo 未测，RELEASE NO-GO。

## 2026-10-08｜YangTools 4.0.0 RC2G LineColor UI/Service（SOURCE STATIC GO）

从 RC2F 正式候选出发，只将 ProjectAssetManager 线样式颜色对话框移出 ExternalEvent，把写入移到 ProjectAssetLineColorService typed Request/Result；支持负值类别 ID、颜色无变化 no-op、单 Transaction/回滚、原文档/标签/关闭/行身份守卫、已提交但刷新失败分离。旧线图案、填充区域、其他 UI 功能均未重构；43 XAML、MCP、Rebar、Ribbon、csproj 均未修改。新增 RC2G 71 项源码合同；旧 RC2F 的固定套件数测试改为下限校验。工作树 + 全新解压各 46/46 静态 suites PASS；原生 C# Build/Revit 颜色点击/Undo 仍 NOT_TESTED，4.0.0 RELEASE NO-GO。详细 docs/YANGTOOLS_4_0_0_RC2G_LINE_COLOR_UI_SERVICE_20261008.md；下轮 RC2H 线图案选择闭环后进行资产管理器收口审计，避免无限拆分。

---


## 2026-10-08｜YangTools 4.0.0 RC2F 线样式名称/线宽编辑（SOURCE STATIC GO）

基线 RC2E 不变；本轮只重构 `ProjectAssetManagerWindow.LineStyleDataGrid_CellEditEnding` 的名称和线宽到 `ProjectAssetLineStyleEditService`。新增 typed Request/Result/Kind、文档有效性/只读/类别 ID 预检、内置样式重命名保护、名称与线宽约束、单 Transaction 和 RollBack、no-op 无 Undo；ExternalEvent 仅在原 Tab 和仍在列表中的行有效时执行，UI 不提前提交 ViewModel，更改成功但 UI 刷新失败单独提示。原 43 XAML、MCP、钢筋、Ribbon、工程目标框架没有修改；填充区域/颜色/图案路径未重构。原工作树 + 独立重新解压均 45/45 suites PASS；专项 59/59 PASS。由于缺 Windows/.NET/Revit 执行环境，Build/Loaded/点击/Undo NOT_TESTED，`4.0.0 RELEASE NO-GO`；正式稳定版仍 3.1.36。源码详细说明见 `docs/YANGTOOLS_4_0_0_RC2F_LINE_STYLE_EDIT_UI_20261008.md`。下一项 RC2G：剩余颜色/图案/填充区域 UI 最小安全闭环。

---

## 2026-10-08｜YangTools 4.0.0 RC2E 逐格编辑安全 Service

ProjectAssetManager 资产名/普通参数/项目信息单元格编辑统一 typed Request/Service/Result + 单事务、异常回滚，修复旧 catch 吞错、只读静默跳过、旧 Tab/Tree 行 VM 刷新、提交成功与 WPF 刷新失败混淆；动态 ProjectInfo Value 列改 Explicit 绑定，全部 XAML 文件未改。专项 Test-ProjectAssetRC2E.py，详见 docs/YANGTOOLS_4_0_0_RC2E_PROJECT_ASSET_CELL_EDIT_20261008.md。**仅 SOURCE STATIC，Revit UI / Build / Undo NOT_TESTED；Release NO-GO。**

## 2026-10-08｜YangTools 4.0.0 RC2D 删除功能原子化/UI 安全

批次范围仅 ProjectAssetManager 删除（`DeleteCheckedNodes_Click`、`DeleteSelected_Click`、`ExecuteDelete`）。`ProjectAssetDeleteService` 负责不可变请求快照/试删除回滚/级联 ID 收集/单 Transaction 真删除/实际级联集合一致性/错误回滚，WPF 负责确认弹窗和正确视图状态缓存。修复旧代码把弹窗留在 TransactionGroup 内、UI 只移除直接选择 ID、旧树状态回调可能执行、删除已提交后 UI 刷新错误仍报“删除失败”的风险。用户确认默认 No，一次 Undo。沿用单 DLL、ExternalEvent、原 XAML/主题、MCP/Rebar/Ribbon。完整验收见 docs/YANGTOOLS_4_0_0_RC2D_PROJECT_ASSET_DELETE_20261008.md。**静态验证不能证明 Revit 事务回滚/类别 ID 或 UI 动作实际可用，Release NO-GO。** 后续 RC2E 单元格参数编辑小模块。

## 2026-10-08｜YangTools 4.0.0 RC2C Excel 导入安全改造

本批只改 ProjectAssetManager Excel 导入业务，新增 Core/ProjectAssets/ProjectAssetExcelImportService.cs typed Request/Service/Result；严格保留中文 XAML、Excel 列别名、ExternalEvent、Revit TransactionGroup 合并为一次 Undo。异常不静默提交任何行；无变更回滚不创建 Undo；UI 过滤已关闭/过期导入请求，模型已提交但表格刷新失败不误提示全批失败。Test-ReviewFixes 的原 3 条导入合同迁至 Service，不删除业务断言；另增专项 Test-ProjectAssetRC2C。现为静态候选，Windows Build/Loaded/实机交互与 Undo 未测试，RELEASE NO-GO。后续 RC2D 优先项目资产删除/参数编辑安全与 UI 小范围整理。

## 2026-10-08｜4.0.0 RC2B：ProjectAssetManager 批量重命名安全下沉

限定操作 BatchRename_Click → ProjectAssetBatchRenameService（typed Request/Result）; 先预检、单事务、任何失败回滚整个批次、一次 Undo；UI 过期回调保护；未改 XAML/其他业务/MCP/Rebar。SOURCE STATIC ONLY，Build/Runtime NOT_TESTED，RELEASE NO-GO。下一步 RC2C 导入 Excel 的多行事务及错误总结、UI 关闭时序小范围审查，不进行大重写。

# YangTools 4.0.0｜RC2A ProjectAssetManager UI / Service（源码静态候选）

- 以上一阶段 4.0.0 RC1A 为基线：继续保留 27 个 MCP 命令、Revit ExternalEvent、单 DLL、全部既有界面布局与生产业务。
- RC2A 首批把“过滤器控制实例/视图/模板使用情况”的只读业务抽出 WPF 到 `Core/ProjectAssets/ProjectAssetFilterUsageService` 的 Request/Service/Result；未新增或更名 MCP tool。
- 同时修复 ProjectAssetManager 快速切换 Tab / TreeView 排队读取过期回调、关闭窗口后排队读取、状态栏加载数量被“就绪”覆盖、动态列和行加载顺序风险。
- 本批不修改原 XAML、Rebar、Ribbon、现有写事务及安装器，不对外宣称 UI 实机 BUG 全部修复。
- 运行 `python tools/Invoke-StaticTests.py`；新增 `tools/Test-ProjectAssetRC2A.py`，对 XAML 按钮/绑定/时序/服务边界 82 项检查。
- **STATIC ONLY**：Windows Build、XAML 真实加载与交互、Revit 模型读回均 NOT_TESTED；4.0.0 RELEASE NO-GO，3.1.36 FINAL_E2E 仍是稳定版。下一步 RC2B 分模块处理写操作及更完整 UI 回归。
- 详细说明：`docs/YANGTOOLS_4_0_0_RC2A_PROJECT_ASSET_UI_SERVICE_20261008.md`。

---

# YangTools 4.0.0｜架构升级 RC1A（源码静态候选）

- 开发输入：YangTools 3.1.38 RC0，保留现有功能、中文 UI、单 Revit 插件 DLL、MCP 命令名和 JSON 契约。
- 本批完成：将 MCP Broker 的 ExternalEvent/队列与 Router、CoreDocument、Rebar、Python 适配器分离；不调整现有钢筋几何、族管理、批量发布或 GUI 算法。
- 当前状态：4.0.0 RC1A **STATIC ONLY**；Windows Build/Deploy/Loaded/模型执行均 **NOT_TESTED / DEFERRED**，不代表正式发布。
- 正式稳定版：3.1.36 FINAL_E2E；3.1.38/4.0.0 均不可无证据升级为正式 Release GO。
- 新自动化测试：`python tools/Invoke-StaticTests.py`（含 `Test-McpRouterV400.py`）。
- 下一轮：RC2 将 WPF 高耦合业务逻辑逐模块提取到可由 GUI/MCP 共用的 Request/Service/Result，不全量重写、不改变 UI 布局。
- 详情：`docs/YANGTOOLS_4_0_0_RC1A_MCP_ROUTING_20261008.md`。

---

## 2026-09-23｜GR01 V1.0.4：最大间距生产策略修复 + V1.0.3 Ribbon 合并验收
- 生产问题：29130.046mm 区段在 MaxSpacing=100 时被旧 5mm/0.75 尾差策略错误拒绝；MaxSpacing=200 又会降到 180，产生额外钢筋。
- V1.0.4 自动间距改为 `UniformWithinMax`：`intervalCount=ceil(length/max)`，`actualSpacing=length/intervalCount`，保证实际间距不超过最大间距且区段内均匀、无尾差。
- 29130.046/100 -> 292 intervals / 293 bars / 99.7604mm；29130.046/200 -> 146 intervals / 147 bars / 199.5209mm。
- 原 12.55m 五区自动计划从 86 intervals / 87 bars 有意变为 84 intervals / 85 bars。
- `SpacingStepMm/MinTailRatio` 仅保留旧 wire compatibility；自动模式不再使用；手动 ActualSpacing 仍允许尾差。
- 几何插值、ZAxis/ModelLine/Edge、CreateFreeForm、Transaction/readback/Undo 不改。
- V1.0.3 Ribbon 两分区/新图标继续保留，尚未做首次 Revit smoke；V1.0.4 与其合并成一次 Mimo Build/Deploy/Test。
- 静态：V1.0.4 focused 31/31；全部 37 Test suites / 0 failure；MCP py_compile PASS。
- 当前：`REBAR V1.0.4 SPACING + RIBBON V1.0.3 STATIC GO / COMBINED REVIT 2025 RUNTIME REQUIRED / PROJECT-WIDE RELEASE NO-GO`。

## 2026-09-22｜Rebar Ribbon V1.0.3：生产入口/测试入口分离 + 图标重做
- `钢筋工具区` 收敛为两个一级 Pulldown：`创建钢筋` 与 `钢筋测试`。
- `创建钢筋` 只保留已完成 Runtime/GUI 验收的 `渐变钢筋集`；其余 12 个 R00–R10 钢筋研发/诊断命令全部移入 `钢筋测试`。
- 13 个钢筋功能全部换成独立 16/32 图标，新增稳定的 `rebar_test` 分组图标；不再复用 `project_inspector` 占位图。
- Ribbon 属性新增可选 GroupLargeIcon/GroupSmallIcon，使 Pulldown 图标不依赖组内第一条可见命令。
- 不修改 GR01 Core/Spacing/Interpolation/CreateFreeForm/Transaction/MCP。
- 静态：Rebar Ribbon 87/87；GR01 V1.0.2 40/40；全部 36 Test suites / 0 failure；UI 201 C#/43 XAML/44 Ribbon/0 error/0 warning；MCP py_compile PASS。
- 当前：`REBAR RIBBON V1.0.3 STATIC GO / REVIT 2025 RIBBON+ICON SMOKE REQUIRED / PROJECT-WIDE RELEASE NO-GO`。

## 2026-09-20｜GR01 V1.0.2 UI/UX：滚轮修复 + 方向语义统一
- 修复 `ZoneGrid` 嵌套 ScrollViewer 吃掉鼠标滚轮：DataGrid 自己可滚时继续滚表格；到顶/底或不可滚时把滚轮交给外层窗口。
- ModelLine / Edge 不再向用户表达第二套路径正反方向；业务方向统一为 `Start Control -> End Control`，Zone[0] 永远从 Start 一侧开始。
- GUI 区分 `基准总长` 与 `有效长度`：ModelLine/Edge 拾取后显示原始几何长度，Preview 后显示控制筋投影之间的真实分布长度。
- Auto Remaining 行改进：显示 `固定/自动剩余`、未解析时显示 `待计算`、自动行长度不可编辑，表头补全 mm 单位。
- 增加 Zone 即时输入校验：非法长度/间距、超配等直接提示并禁用 Preview；新 Zone 插入当前选中行之后。
- 标题去除 GR01/V1/Revit 版本测试字样；保留 ManagedGeometry / Native ToCover 技术披露。
- Core spacing/interpolation/CreateFreeForm/Transaction/Undo/MCP 算法均未修改。
- 静态：V1.0.2 37/37；V1.0.1 41/41；GUI 44/44；V1 81/81；P0 43/43；全部 35 Test suites / 0 failure；UI 201 C#/43 XAML/44 Ribbon/0 error/0 warning；MCP py_compile PASS。
- 当前：`GR01 V1.0.2 UI/UX STATIC GO / WINDOWS BUILD + REVIT 2025 UI SMOKE REQUIRED / PROJECT-WIDE RELEASE NO-GO`。

## 2026-09-20｜GR01 V1.0.1 UX 优化：Z轴 + 自动剩余区
- 在已验收的 GR01 V1 Explicit ManagedFreeForm Core/GUI 上做小范围易用性优化，不扩算法边界、不做 Update/2027。
- 分布方式新增并默认 `ZAxis`：无需拾取路径，Core 使用首末控制筋 transformed centerline 几何中心的 Z 差自动计算有效长度；ModelLine/Edge 旧路径保持不变。
- Zone 新增 `AutoLength`：最多一个“自动剩余”区，实际长度=有效分布长度−其余固定区段；默认窗口只有一个普通自动区。增加固定普通/加密区后自动扣减；超配明确拒绝。
- GUI 新增“⇅ 交换首末”；自动区长度不可手工编辑；已知路径长度后实时显示路径/固定/自动剩余；ZAxis 下“拾取路径”禁用。
- Typed MCP 同步支持 `distribution_path={"kind":"ZAxis"}` 与 Zone `auto_length=true`，GUI/MCP 继续共用同一个 `RebarGradientSetService`。
- 原 `RebarGradientSpacingPlanner` 未修改；`CreateFreeForm`、事务、readback、fingerprint/Undo 既有核心链保持。Auto zone fingerprint 使用解析后的实际长度，避免 Preview 0 长度与 Create 显示长度造成伪 stale。
- 静态：GR01 V1 81/81；GUI 44/44；V1.0.1 UX 41/41；全部 `34 Test-*.py suites / 0 failure`；UI 201 C# / 43 XAML / 44 Ribbon / 0 error / 0 warning；MCP py_compile PASS。
- 当前环境无 dotnet/MSBuild/Revit，故状态：`GR01 V1.0.1 UX STATIC GO / REVIT 2025 ZAXIS+AUTO-ZONE RUNTIME REQUIRED / RELEASE NO-GO`。

## 2026-09-20｜GR01 V1 Explicit ManagedFreeForm Core
- GR01-P0 Engine Decision 已闭合：Revit 2025 主路线采用 `Explicit ManagedFreeForm`；Native Surface/Aligned 不作为 V1 依赖。
- 当前 GR01 V1 已实现 Core + Typed MCP：两根 single-position 控制筋、直线 ModelLine/Edge DistributionPath、Zone/规整间距、Line/Arc 插值、一个 FreeForm Rebar Element + N physical bars、PlanFingerprint、全 physical-bar readback、rollback/one-Undo 语义。
- 新 MCP：`preview_gradient_rebar_set` / `create_gradient_rebar_set`；GUI/Ribbon 暂不增加，待 Revit 2025 Core Runtime 门禁后再接。
- 静态：GR01 V1 80/80；全部 32 Test-*.py suites 0 failure；UI 43 Ribbon / 0 error / 0 warning；MCP py_compile PASS。
- 当前状态：`GR01 V1 STATIC IMPLEMENTED / REVIT 2025 CORE RUNTIME REQUIRED / GUI NOT IMPLEMENTED / RELEASE NO-GO`。

## 2026-09-18｜Rebar R10 Coupler 当前开发候选
- 正式稳定基线仍是 3.1.36 FINAL_E2E；3.1.38 Rebar 分支未升 Release。
- R09 Level-1 collision/clearance core 已有 Revit 2024 Runtime GO，curved tessellated screening 仍 NOT_TESTED。
- 当前 R10 使用 Revit 原生 `RebarCoupler.Create`，支持 TwoBar / SingleEndCap；GUI/MCP 共用 Service/Request/Result，默认 dryRun。
- R10 静态 50/50；全 30 Test suites 0 failure；UI 43 Ribbon / 0 error / 0 warning。
- 当前判定：`R10 STATIC GO / WINDOWS+REVIT RUNTIME REQUIRED / RELEASE NO-GO`。R11 才处理 Revit 2025+ native Splice/Lap。

## 2026-09-11｜Reliability Hardening R2 源码复审补丁

在首版 F01–F05 静态候选上进行独立源码复审，发现两类不能带入 Windows 终验的残留，并已做小范围修复：

- F03：`FamilyManagerWindow` 仍有 6 处直接 `Commit()` 后继续成功流程；`LevelModifierCommand` / `DimTextOverrideCommand` 调用公共提交 helper 后仍无条件返回 `Result.Succeeded`。R2 已统一要求 `TransactionStatus.Committed`，未提交即失败，不再把 UI/命令返回值当模型真值。
- F05：`WriteProductOperation` 文档声称支持 `OperationId/BatchRunId`，实际 payload 只有 `OperationId`；Batch 又把 runId 直接当 operationId。R2 新增独立 `BatchRunId` 字段，并为一次 Batch 用户动作分别生成 `operationId` 与 `batchRunId`，所有 Batch 产品日志显式携带二者。
- `Test-ReliabilityHardening.py` 从 38 项扩展到 **43/43 PASS**；全量 `tools/Test-*.py` 仍为 **19/19 suites PASS**；UI static 153 C# / 42 XAML / 31 Ribbon / 0 error / 0 warning。
- 当前环境仍无 `dotnet/csc/mcs`，因此状态仍是 **SOURCE STATIC PASS / WINDOWS BUILD+FAULT-HARNESS+REVIT RUNTIME REQUIRED / RELEASE NO-GO**。
- 未混入 UI 七分组、MCP SDK v2、全功能 MCP 或安装器发布模式改造。

详细复审：`docs/RELIABILITY_HARDENING_R2_SOURCE_REVIEW_20260911.md`。

## 2026-09-11｜3.1.38 第一批优化：可靠性 F01–F05

- 依据 Drive 审查 28、优化计划 29、持续扩展复查 31。
- 先修 Batch 文件恢复、全批预检、事务结果、运行请求快照、产品 provenance；不与全面 UI/Ribbon 重构混做。
- 新增 `BatchFileCommitService`、`BatchRunRequest/BatchRunOptions`、产品 OperationId/BatchRunId 日志、Windows .NET 故障注入 harness。
- Batch 当前合法路径仍是 `IExternalCommand → modal ShowDialog → BtnExecute_Click → BatchTaskEngine`；Family modeless 路径继续用 ExternalEvent。
- 审查 31 的 Feature/Operation/Request/Result/Service 模块化单体方向作为下一阶段标准，但本轮不整体迁移。
- 静态全量 `19/19 suites PASS`；Runtime 尚未验证。当前：**SOURCE FIX STATIC PASS / RELEASE NO-GO**。

## 2026-09-02｜3.1.38 版权、版本声明与代码签名发布层

- 版本保持 **3.1.38**，不改变 PasteCAD / Family / Batch 业务逻辑。
- `YangTools.Revit.csproj` 固定 Assembly/File/Informational version、Authors/Company/Product/Copyright。
- “系统设置 → 关于”改为正式项目声明页：YangTools、版本、原作者 Yang、Copyright © 2026 Yang、非商业署名许可、Revit 版本边界、当前 DLL Authenticode 证书显示。
- 根目录新增 `LICENSE.md / NOTICE.md / THIRD_PARTY_NOTICES.md`，并随构建复制至 `Docs/`。
- 新增 `New-LocalCodeSigningCertificate.ps1 / Sign-YangToolsRelease.ps1 / Verify-YangToolsCodeSignature.ps1`。私钥/PFX/密码禁止进入源码 ZIP/Drive 公共交付。
- 本地自签名可以证明该二进制由当前证书签出，但其他电脑默认不信任；公开“受信任发布者”仍需可信 CA 证书。
- 新增 `Test-CopyrightSigning.py`：47/47 PASS；全量静态现为 **18/18 suites PASS**。
- 当前发布状态仍是 **NO-GO**：待 Gemini 用本版权候选完成签名后 Build/Deploy/Loaded、双模型 Batch、真实屏幕截图和产品日志终验。

## 2026-09-02 当前统一开发候选：3.1.38 PasteCAD Shared + Family Management V2.1 + Batch Publisher V2

- 正式运行基线仍为 3.1.36 FINAL_E2E；3.1.38 尚未通过 Windows/Revit 统一实机验收。
- PasteCAD：新增 `ImportPlacement.Shared`，不 Acquire/Publish，不失败 fallback Origin；必须实机证明真实坐标对齐。
- Family Management V2.1：使用统计、未使用清理、StableParameterKey、Scope/Type/Level 过滤、批量换型/参数、多族 RFA、UI 尺寸门禁。
- Batch Publisher V2：重做多模型/云模型 NWC/IFC/DWG/PDF 导出/PDF 打印；跨模型按名称重新匹配资源，逐模型预检，精确命名、全批路径冲突预检、staging/安全覆盖；2021 只走 PDF Print，2022+支持原生 PDF Export。
- 当前静态：Batch Publisher 99/99，Family Management 110/110，PasteCAD 19/19，CAD→Revit 76/76；全部 `Test-*.py` **17/17 suites PASS**；UI **151 C# / 42 XAML / 37 roots / 4 themes / 31 Ribbon / 0 error / 0 warning**。
- 当前状态：**STATIC GO / WINDOWS REVIT UNIFIED RETEST REQUIRED / RELEASE NO-GO**。
- 下一步：只生成一个 3.1.38 完整候选交给 Gemini，统一编译/部署/实机测试；未通过前不得替换 3.1.36 FINAL_E2E。

## 2026-09-02 3.1.38 子任务：Family Management V2.1（已合并到统一候选）

- 正式运行基线仍为 3.1.36 FINAL_E2E；3.1.38 从 3.1.37 Shared 静态候选继续开发，Shared 仍需当前 DLL 实机。
- 族文档管理：使用统计、未使用过滤/清理、管理实例、多族 RFA、StableParameterKey。
- 族实例管理：Scope、Type/Level filter、Level/Host/Pinned、批量 ChangeTypeId、批量参数/仅空值、动态系统类别、StableParameterKey。
- UI 尺寸是本轮验收硬门禁，不允许“功能正确但表格压成单字/按钮重叠”进入 GO。
- 版本 3.1.38，Ribbon 31；Family V2 静态 110/110；全测试 16/16；UI 0 error / 0 warning。
- 当前：**STATIC GO / WINDOWS REVIT RETEST REQUIRED / RELEASE NO-GO**。

## 2026-09-02 当前开发候选：3.1.37 PasteCAD Shared

- 正式输入基线：`YangTools_Project_3.1.36_Source_CAD_TO_REVIT_FINAL_E2E_20260901.zip`，SHA-256 `D126F1A65F0DD4A74A1F97AD6F1CD3302309F77212E30BD7752666DE740CDBA4`；3.1.36 CAD→Revit 在 Revit 2024 已完成真实 11/11 Exchange Reader、Bulge Arc、MLeader、Fill/Dimension 与一次 Ctrl+Z U3。
- 本轮产品改动仅聚焦 `从 CAD 粘贴`：新增 `ImportPlacement.Shared` 的“对齐共享坐标”，原 `Origin / Centered` 保留。
- 共享坐标模式不执行 Acquire/Publish Coordinates，不修改项目坐标系统，不允许失败后静默退回 Origin。
- PasteCAD 继续使用工作副本与单事务导入，失败 rollback、临时文件 finally 清理、成功选中新 ImportInstance；新增更完整日志和专用失败说明。
- 版本：**3.1.37**；Ribbon：**31**。
- 静态：`Test-PasteCad.py 19/19`；全量 15/15 suites PASS；UI 0 error / 0 warning。
- 族文档管理 / 族实例管理：本轮只完成 V2 源码审查，不修改业务代码。结论是两者代码量大但管理动作不足；下一独立功能建议做 Family Management V2.1。
- 当前判定：**STATIC GO / WINDOWS REVIT RETEST REQUIRED**。

### Family Management V2.1 建议范围

- 族文档侧：实例/使用类型/未使用类型统计；仅未使用筛选；安全清理未使用类型；从族直接跳实例管理；批量导出 RFA；稳定 ParameterKey。
- 族实例侧：显式 Scope（全项目/当前视图/当前选择）；Type/Level 过滤；批量换类型；批量同值写参数；固定 Level/Host/Pinned 状态列；稳定 ParameterKey。
- 技术策略：不做全面 MVVM 重构，先抽出 `FamilyUsageService / FamilyTypeEditService / FamilyFileService` 与共享参数身份/动态表格逻辑。

## 2026-09-01 当前开发候选：3.1.36 CAD 转 Revit 图纸

- 输入基线：用户回传并完成 3.1.35 P0_Z_GATE Windows/Revit 修复后的源码；本轮只新增 CAD→Revit 功能，不重构既有 MCP、材质、线性布置等稳定主链。
- 新增入口：`导入工具区 → CAD转Revit图纸`，当前 Ribbon **31**。
- UI 是四张“CAD 类型 → Revit 类型”映射表：文字/引线、线条、填充、标注；支持使用现有、复制现有、自动新建、CAD炸开参照（备用）、跳过。
- CAD 炸开类型可能产生乱码/低质量名称，只作为用户主动选择的备用来源，绝不作为默认映射。
- 文字标准不从 CAD 高度/字体命名硬猜；Revit 目标类型由用户/历史映射选择。线条自动创建只在 YangCAD 已明确给出线宽/线型时按显式标准生成。
- Dimension V1 使用 `YangTools-尺寸界线` 专用 DetailCurve 作为 Reference，再创建 Revit 原生 Dimension；参考线与普通图纸线分离。
- 映射持久化使用名称，不保存跨项目失效的 ElementId。
- 写操作固定为 `TransactionGroup + 类别 Transaction + 单对象 SubTransaction`；零成功回滚，成功批次一次 Ctrl+Z。
- 版本：**3.1.36**；状态：**CAD_TO_REVIT LIVE VERIFIED / DxfType COMPATIBLE / TEST EXECUTION COMPLETE**。
- 实机验证：Revit 2024 (PID 26420/2428) 真实完成 5 组几何坐标实测（误差 0.000 mm）、Dimension 生成与回滚、批次事务 Undo 及映射持久化。

## 2026-08-21 当前 P0 加固候选：线性布置真实 LocationPoint.Z 门禁

- 当前 Windows 复验候选源码：`YangTools_Project_3.1.35_Source_SIMPLE_MATERIAL_P0_Z_GATE_20260821.zip`；来源为 `Source_SIMPLE_MATERIAL_20260821` 的定点 P0 加固，版本仍为 3.1.35。
- 本轮不做结构性重构、不新增功能：保留单项目结构、现有 ExternalEvent/Transaction 设计和 30 Ribbon。
- level-based 线性布置的 Offset 候选现在必须让实例真实 `LocationPoint.Z` 达到理论世界 Z，误差硬门禁 `<= 1 mm`；仅参数可写或自算公式正确不再算成功。
- 候选顺序仍为 `INSTANCE_ELEVATION_PARAM / INSTANCE_FREE_HOST_OFFSET_PARAM / FAMILY_BASE_LEVEL_OFFSET_PARAM`，但不生效的候选必须恢复原值再继续，避免叠加多个 Offset。
- P0 诊断日志记录族放置类型、标高两种高程、实际 BuiltInParameter、Offset 值、理论/实际 Z 和误差。
- Assistant 退役数据清理加入本轮实机验收；MCP 数据必须保持。
- 当前 `installer/payload` 不代表本候选源码可分享，日常仍按“真实编译 -> 直接部署 -> 实机验收”。
- 状态：**STATIC GO / WINDOWS RETEST REQUIRED**。

## 2026-08-21 当前开发基线：简易绑定材质

- 输入交接源码基线：`YangTools_Project_3.1.35_Source_SIMPLE_MATERIAL_20260821.zip`。
- 新增 `SimpleFamilyMaterialBindingCommand / Service / Window`：项目中选择一个或多个族实例后，自动给相关载入族绑定材质族参数并把指定项目 Material 赋到所选实例或对应类型。
- 原 `FamilyMaterialBindingCommand` 改名为 `批量绑定材质`，逻辑仍面向 Family 列表批处理；参数名称输入区完成布局修复。
- 简易版默认实例参数；类型参数模式会影响所有使用所选类型的实例，UI 和确认框必须持续明确提示。
- 项目 Material 只允许选择当前项目已有材质，本轮不做自动创建 Material。
- 族编辑链继续要求：`EditFamily → FamilyManager Material Parameter → AssociateElementParameterToFamilyParameter → LoadFamily`，同名非 Material 或类型/实例模式冲突禁止删除重建。
- 赋材质使用 `Material.Id`，写后验证 `AsElementId()`；整体使用 `TransactionGroup`，每个实例/类型赋值用 `SubTransaction`。
- Ribbon 由 29 增至 **30**。产品源码已变化，当前 DLL 尚未建立 Windows 编译/实机证据。
- MCP SDK v2 仅记录为后续独立任务，不与 Revit 功能开发混合。
- 默认发布规则：平时仅 DeepSeek/Codex 对目标年份编译+直接部署；只有用户明确“需要分享”时，才做目标年份实机验收、写入 `installer/payload`、生成可安装/更新/卸载的自包含安装包并输出 SHA-256。

## 2026-08-14 新增批量“绑定材质”；MCP v2 进入待办但不迁移

- 新增 `FamilyMaterialBindingCommand / FamilyMaterialBindingService / FamilyMaterialBindingWindow`，入口为 `项目管理区 → 绑定材质`。
- 用户可批量选择载入族、指定材质参数名称，并选择类型参数或实例参数；工具打开每个 Family Document，扫描 `MATERIAL_ID_PARAM`，创建/复用 Material FamilyParameter，并关联全部可处理几何后 `LoadFamily` 回载项目。
- 默认允许覆盖已有其它材质参数关联；用户可关闭覆盖。已有同名非材质参数、同名材质但类型/实例方式冲突时禁止自动替换。
- 没有可处理 Material 参数的族直接跳过，不创建无意义参数；嵌套族内部几何不通过宿主族间接改写。
- 回载安全门沿用族类别修改的原则：Family/FamilySymbol/FamilyInstance ID 和已有正 ID 自定义参数内部值不得静默变化；回载后异常触发当前批次回滚。
- Ribbon 数量由 28 增至 **29**。最终静态复跑：文本 316、C# 138、XAML 40、窗口/视图根节点 35、主题 4、Ribbon 29；UI/XAML/资源错误 0、警告 0；绑定材质专项 31/31。
- MCP SDK v2 / 2026-07-28 规范只保留评估和未来迁移任务，本轮 `requirements.txt` 与 `FastMCP` 实现不变，不把 MCP 协议升级与 Revit 功能开发混在一起。

## 2026-08-14 上一轮：删除 AI/摸鱼助手；新增批量“族类别修改”

- 删除 AI/摸鱼助手完整运行链：`CopilotCommand`、Assistant/Core 网络客户端、DockablePane provider、工作/私人聊天 UI、模式存储、历史管理、相关图标与启动注册。MCP 网关、RevitExecutionBroker、IronPython MCP 执行能力保留，二者不再与聊天助手绑定。
- `App.OnStartup` 不再注册任何 YangTools DockablePane；静态门禁改为禁止旧 Assistant/Copilot 文件回流。
- 新增 `项目管理区 → 族类别修改`，用于批量修改 loadable Family 的 **Family Category**，例如 `Abutments → Generic Models`。这是族类别变更，不是 FamilySymbol/族类型名称修改。
- UI 支持族名/类别搜索、逐行勾选、全选/反选/全不选、目标类别选择，并显示族名、当前类别、类型数、实例数、可编辑状态、ElementId。系统族/内建族/不可编辑族只显示不执行。
- 执行固定走 `EditFamily → familyDoc Transaction → OwnerFamily.FamilyCategory → LoadFamily → 回载安全校验`。回载前单族错误可记录并继续；回载后若类别、Family/FamilySymbol/FamilyInstance ElementId 或正 ID 自定义参数内部值异常，则抛出安全异常并回滚整个 TransactionGroup。
- 实机必须验证：Abutments→Generic Models、多个族混合批量、同类别跳过、不可编辑族跳过、Family/类型/实例 ID 与自定义参数值不丢、一次撤销。
- 用户已明确要求彻底清除已删除 AI/摸鱼助手数据：`%APPDATA%\YangTools\Assistant\` 以及旧 `copilot_settings.json` / `copilot_history.json` / `moyu_settings.json` / `moyu_history.json` 在启动、安装/更新、直接部署和卸载路径均执行幂等清理；MCP 与其它 YangTools 用户配置不受影响。
- 该轮 Ribbon 数量为 28：删除一个 AI 入口，新增一个族类别入口。当前“绑定材质”基线已增至 29。
- MCP 现状：外部 Python 桥仍固定 `mcp>=1.2.0,<2`（SDK v1）。2026-08-14 已评估官方 Python SDK v2 / MCP `2026-07-28` 规范，本轮不混入迁移；后续作为独立任务升级。

## 2026-08-13 线性布置 level-based Z 约束二次修复

- 第二次实机复验表明，仅将 `Level.Elevation` 改为 `ProjectElevation` 仍不足：level-based FamilyInstance 的 Z 不能依赖 `ElementTransformUtils.MoveElement` 修正。
- 新实现将“世界 Z 计算”和“Revit level-based 放置机制”分离：世界目标仍为 `ProjectElevation + relativeElevation`，实际实例高程通过内置实例偏移参数写入。
- 线性布置不再调用通用 `CreateAtWorldPoint`；改用 `CreateAtLevelRelativeElevation`，并禁止 TwoLevelsBased / CurveDrivenStructural 走通用线性布置。
- 这轮属于真实实机 Bug 定点修复，未新增其它功能。

# YangTools 项目总文档

更新时间：2026-08-14｜当前版本：3.1.35

> 这是项目介绍、要求、开发规范、功能说明和状态的唯一权威文档。任何 AI 开工前必须完整阅读本文件和 `WORKLOG.md`；修改代码后必须同步更新功能状态和工作日志。

## 2026-08-13 线性布置 ProjectElevation 修复

- 用户实机暴露精确高程差：期望 9000 mm、实际 5500 mm、差值 3500 mm。
- 复核确认上一实现把 `Level.Elevation` 用作 XYZ 世界坐标基准。Autodesk API 语义中 `Level.Elevation` 可随 Elevation Base 使用项目或共享基准，而 `Level.ProjectElevation` 始终相对项目原点。
- 世界坐标公式固定为：`WorldZ = targetLevel.ProjectElevation + relativeElevation`；`Level.Elevation` 仅用于 UI/诊断显示，禁止再参与线性布置世界 Z 计算。
- UI 若发现 `ProjectElevation` 与 `Elevation` 不同，同时显示两者，帮助识别共享坐标偏移。
- 保留首尾实例线性插值、反转路线、KeepVertical、单事务撤销等上一轮规则。
- 当前源码须重新 Windows 编译并在用户原问题模型复验；静态 GO，实机条件 GO。

## 2026-08-12 线性布置高程控制升级

- `LinearPlacementViewModel` 新增 `AvailableLevels / SelectedLevel / StartElevationMm / EndElevationMm`，窗口新增基准标高与首尾高程输入。
- 用户输入的起点/终点高程统一按 mm 解释为**相对基准标高高程**；计算时先转 Revit 内部英尺，再使用 `targetLevel.ProjectElevation + relativeElevation` 得到真实世界 Z，禁止默认标高为 0。
- 高程插值端点使用“实际生成的首尾两个轮廓实例”：中间实例按 `DistanceAlongPath` 比例线性插值，确保首实例=起点高程、尾实例=终点高程。单实例且首尾高程不同会明确提示并采用起点高程。
- 反转路线后，高程方向与有效路线同步反转；高程设置不改变原有按距离/参数、固定间距/固定数量、末端补放等布置规则。
- 所有实例继续关联用户选定 Level，并通过 `FamilyPlacementHelper` 校正/验证世界坐标。设置文件记忆标高名称和首尾高程，不持久化 ElementId。
- 专项静态：`tools/Test-LinearElevationProfile.py` 26/26，非零标高数学矩阵 20/20，并覆盖共享显示高程与项目高程相差 3500 mm 的真实错误回归；仍需 Windows 编译及 Revit 2024/2025 实测。


## 2026-08-07 中文检查与清理升级

- `检查工具区 → 中文检查` 从四块只读列表升级为分类 DataGrid 管理器，当前扫描 39 个细分类别，覆盖模型资源、视图图纸/修订、项目标准（含用户子类别）、参数文字、空间项目信息、链接输出。
- 当前分类支持搜索、逐行勾选、全选/全不选/反选、Revit 定位、复制 ID。
- 实例参数从仅族实例扩展到全部具有类别的非类型元素；字符串值全面扫描，参数名称只统计正 ID 的用户/项目/族参数，避免中文 Revit 内置参数名称造成大量假阳性。
- 双击“名称 / 内容”按对象能力执行 `Element.Name`、`TextNote.Text`、字符串参数、工作集、图号、明细表列标题、空间编号或装配类型名修改；失败事务回滚。
- 删除实体前在回滚事务中调用 `Document.Delete` 做级联影响预检；字符串参数值的“删除”仅清空值。批量处理一个主事务 + 每项 SubTransaction，单项失败隔离，整批一次 Ctrl+Z。
- 参数定义名称和无安全删除语义的对象保持只读；不使用 `AsValueString()` 扫数值假阳性，不扫描链接文档/族文件内部。
- 专项：`tools/Test-ChineseCleanup.py` 67/67；说明 `docs/CHINESE_CHECK_CLEANUP_3.1.35_20260807.md`；本机任务 `docs/DEEPSEEK_CODEX_CHINESE_CHECK_ACCEPTANCE_3.1.35_20260807.md`。
- 本轮再次修改产品源码，必须重新编译，当前静态 GO、Windows/Revit 条件 GO。

## 2026-08-07 批处理新增 PrintManager 打印 PDF

- `总控中心 → 批处理与云链接` 新增独立“打印 PDF（虚拟 PDF 打印机，Revit 2021+）”；旧“导出 PDF（Revit 2022+）”保持原样。
- 打印路径使用 `PrintManager / PrintRange.Select / ViewSheetSetting / PrintSetup / SubmitPrint / PrintToFileName`。视图选择使用 In-Session 集合，禁止为临时批处理保存新的命名 ViewSheetSet。
- 仅允许名称包含 `PDF` 的打印机，避免误提交到实体打印机；PDF 驱动自动保存能力必须实机验证，不能把 Microsoft Print to PDF 的“另存为”弹窗误判为无人值守通过。
- 打印输出采用临时文件 + `ReplaceOutputSafely`，旧 PDF 仅在新 PDF 成功落盘后替换；结束后尽力恢复用户打印状态。
- 本轮修改产品源码，因此 FINAL3 旧 DLL 只代表上一基线，当前源码必须重新编译并做 Revit 2021/2024/2025 打印专项。
- 专项说明：`docs/BATCH_PRINT_PDF_3.1.35_20260807.md`；验收任务：`docs/DEEPSEEK_CODEX_BATCH_PRINT_PDF_ACCEPTANCE_3.1.35_20260807.md`。

## 2026-08-07 FINAL3 证据与文档闭环

- FINAL3 为交付层修正：产品源码 `src/YangTools.Revit` 与 FINAL2 完全一致；编译 DLL 和 Revit 2024 实机证据继续适用。
- 删除源码包内残留的旧 `artifacts/evidence.zip`；正式证据仅使用独立 `YangTools_3.1.35_Review2_Evidence_FINAL3_20260807.zip`。
- 修正 HANDOFF 当前基线、编译状态、警告数量、哈希文件名和功能矩阵；历史记录保留但明确标为历史。
- Revit Journal/运行日志不纳入 UTF-8 源码编码门禁；最终打包后静态复跑 0 错误、0 警告。
- 项目总体仍为 **条件 GO**；正式发布仍为 **NO-GO**。

## 2026-08-07 Windows 独立编译与 Revit 2024 实机复验

- 在全新空目录解压 `Source_REVIEW2_20260807` 并执行完整复验；修复 2 个真实编译错误：`ParameterDeleteService` 变量重名（CS0136）、`FamilyManagerWindow.LoadFamily` 返回值类型（CS0029，Revit 2024 返回 `Family`）。
- 修复 2 个工具脚本编码问题（`Verify-SourceBaseline.ps1`、`Build-Deploy-2024-2025.ps1` 补 UTF-8 BOM，PowerShell 5.1 解析）。
- 上一 FINAL3 构建：2021–2026 全 0 错误；2025 后续已部署并完成启动/28 Ribbon/MCP 加载冒烟，2026 未实机；2027 缺 .NET 10 SDK 未构建。当前源码已继续新增打印 PDF 与中文清理，必须重新编译。
- Revit 2024 实机（上一 FINAL3 基线）：批处理链接跳过/同名目录/失败续跑/PDF 覆盖保护/缺视图报错、线性布置世界 Z、标高修改无重复实例、布尔 Union 失败安全门等通过；原始证据归档于独立 `YangTools_3.1.35_Review2_Evidence_FINAL3_20260807.zip`。

## 2026-08-07 文本基准对齐段落宽度同步

- `AlignTextToTextState` 新增 `MatchParagraphWidth`，窗口新增默认未勾选的“段落宽度相同（复制源文字框宽度）”。
- 执行时读取源 `TextNote.Width`，逐目标检查 `GetMinimumAllowedWidth` / `GetMaximumAllowedWidth` 后精确写入目标 `Width`。
- 宽度超出目标文字类型允许范围、目标被锁定/成组/受约束或写入后校验不一致时，该目标的 `SubTransaction` 回滚；其它目标继续执行。
- 完成提示最多列出 8 条目标 ID 与失败原因，避免只显示笼统“跳过”。
- X/Y、旋转、宽度四项可以任意组合；只勾选宽度也允许执行。文字内容、文字类型、字号和格式不复制。
- 当前仅完成源码和静态检查；必须在 Revit 2024 验证同类型/不同类型、单行/多行、左/中/右对齐、带引线及撤销。

## 2026-08-05 报告复核与管理/模型链路修复

- 以源码而非报告结论为准，复核批处理、模型修改区和四个项目管理窗口。原报告中“LoadFamily 必须包项目事务”“列头 ComboBox 必然无限循环”“正常入口无文档必然 NRE”“Revit 允许重复图号”等结论已更正。
- 项目资产管理器的视图过滤状态列改为固定可读宽度；导入导出表头闭环、Project Info 操作守卫、操作栏、分析按钮、图案下拉写入时机和动态参数绑定均修复。
- 族实例管理修复参数名截断、选择过滤残留、非族选择死胡同和双 Tab 配置串页；族文档管理修复参数名截断并增加无变化防重入。
- 图纸管理增加 InitializeComponent 期间的事件守卫，修正图框列头和状态栏。
- 线性布置、基于面转换、标高修改、布尔几何补齐显式事务/子事务回滚；线性布置和基于面转换改用世界坐标放置助手。
- Revit 2021 禁用 PDF 导出入口；批量链接提交/合并失败记录状态并尽可能恢复。ACC 路径等价和 PDF 旧文件保护保持为实机待证，不进行猜测性修改。
- 本轮未在 Windows 编译，正式发布仍为 NO-GO。详细报告：`docs/REVIEW_AUDIT_FIX_3.1.35_20260805.md`。


## 2026-07-30 Loft / 实体提取世界高度定点修复

- 用户实机报告两个生成器选择目标标高后，生成模型世界 Z 与源几何不一致。
- 旧实现依赖“目标标高 Elevation + 猜测实例偏移参数”，无法跨族模板稳定证明最终位置。
- 新增共享 `FamilyPlacementHelper`，直接使用原几何世界坐标锚点调用 `NewFamilyInstance`，并在再生后验证 `LevelId` 和 `LocationPoint`；若模板重解释位置，则移动一次并重新校验。
- Loft 锚点为首轮廓 XY 与全轮廓最低 Z；实体提取锚点为所有 Solid 联合包围框中心。目标标高只改变实例关联，不改变原几何世界位置。
- 位置误差超过 `1e-6 ft` 时抛错，外层 TransactionGroup 回滚；模型修改仍可撤销。
- 同步处理结果包中能静态确认的 ComboBox/Grid/FontFamily 命名歧义、FeatureHelp TextBox 属性和 Revit 2021 参数类型读取风险。
- 当前仅完成源码和静态审查，Revit 2024 必须用正/零/负三个目标标高分别验证 Loft 与实体提取的 MinZ/MaxZ/CenterZ、LevelId、LocationPoint 和 Ctrl+Z。
- 专项任务：`docs/DEEPSEEK_CODEX_GENERATOR_LEVEL_ACCEPTANCE_3.1.35_20260730.md`。
- 最终静态复跑：257 个文本、145 个 C#、40 个 XAML、35 个窗口/视图根节点、4 套主题、28 个 Ribbon；0 错误、0 警告。


## 2026-07-29 源码核验与 Windows 编译门槛

- 本轮按 3.1.35 交接包重新核验项目文件、解决方案、构建脚本、Addin 清单、安装器、WPF 资源隔离、ExternalEvent、重复参数清理身份和值完整性规则。
- 产品业务代码没有修改；没有依据猜测修补 API。只有 Windows/MSBuild 或 Revit 实机证明存在的问题才允许进入下一轮代码修复。
- 静态复跑结果：251 个文本文件、144 个 C#、40 个 XAML、35 个窗口/视图根节点、4 套主题、28 个 Ribbon；0 错误、0 警告。重复参数清理链路模拟通过，606 个安全候选仍为 3 次初始批量 Delete 调用。
- 当前环境缺少 Windows、PowerShell、.NET SDK、MSBuild 和 Revit，故正式发布状态仍为 NO-GO；源码交付状态为“条件 GO，可进入 Windows 编译”。
- Windows 执行者必须先完成七配置编译，再完成 Revit 2024 的 28 入口烟雾测试和六项专项验收。未安装年份只能标记未测试，不得推断兼容。
- 执行任务：`docs/DEEPSEEK_CODEX_WINDOWS_TASK_3.1.35_20260729.md`。


## 3.1.35 临时插件说明体系与四项功能审查

- 临时插件箱从“只列 DLL”升级为带说明的工具仓库。主 DLL 必须提供 `<DLL名>.microtool.json`；功能说明和使用步骤为必填，缺少时禁止运行与收藏。收藏包同步保存说明，脱离原目录后仍可阅读。
- 新增通用 `FeatureHelpWindow`，正文可选择复制；临时插件箱、文本等距分布、文本基准对齐、中文检查均提供窗口内说明入口。
- `MicroTool_Developer_Guide V3.md` 更新为内容版本 V3.1，并作为构建内容复制到 `Docs`；新增标准模板 `docs/templates/SampleTool.microtool.json`。
- 文本等距分布审查结论：原算法按 `TextNote.Coord` 插入点而非文字边框工作，原“方向+对齐”组合容易误解。现改为六个操作模式并明确排序、方向、间距与视觉边框限制。
- 文本基准对齐审查结论：X/Y 的含义原说明不足。现明确 X 坐标相同=插入点落在竖直线，Y 坐标相同=落在水平线；每个目标使用独立 `SubTransaction`，锁定/成组失败不会使所有目标回滚。
- 中文检查：旧阶段曾取消删除职责并扩展扫描；当前已升级为分类“中文检查与清理”，恢复受控写操作：按能力双击重命名、字符串参数修改/清空、实体删除级联预检与二次确认。
- 面板设置审查修复：关闭窗口不自动保存；系统设置固定主区；每个面板至少保留一个主区按钮；旧坏配置运行时自动提升一个按钮；配置完整快照写入 `.tmp` 并保留 `.bak`。
- 28 个 Ribbon 命令完成静态入口和 UI 资源检查。当前版本尚无 2021–2027 七年份逐功能实机矩阵；兼容性状态必须区分“目标框架/条件编译已配置”和“真实版本验收已完成”。

## 3.1.35 重复参数快速值索引与可诊断执行

- 回收 DeepSeek/MCP 的三项目实机成果：606/573/135 个重复组，分别删除 1770/3019/179 个来源参数；RIS_003 同类项目成功迁移 21891 个值，单项目总耗时约 46–50 秒。
- 审查后只吸收已验证的算法，不照搬临时脚本：保留单次值索引、内部值复制和直接 Delete；拒绝跳过绑定合并、盲目接受级联对象、错误后提交半成品，以及用 `Task.Run` 直接访问 Revit 模型。
- `CaptureBefore` 同时生成值完整性快照和 `ParameterId -> 有值 ElementId` 索引，快速处理不再进行第二次全模型值扫描。
- `MergeValuesIndexed` 先构建 `ElementId -> 来源值操作`，只访问相关元素；每个元素的参数只遍历一次，并按 ParameterElement Id 建立局部映射。
- 身份回退：共享参数使用 GUID，普通项目参数使用 Definition；返回参数必须再次匹配目标 ParameterElement Id，禁止名称模糊匹配。
- keeper 当前为空才允许复制；复制后同一 Parameter 对象立即成为已有值，后续不同来源只能记录冲突，不能覆盖。
- 值访问大面积异常时启用熔断：重取失败达到 2000 且至少占已处理操作一半，立即回滚值事务，阻止进入删除并输出诊断。
- WPF 日志增加主线程安全心跳：快照/索引每 5000 元素、绑定每 100 组、迁移每 1000 相关元素、删除每 250 来源、复核每 5000 条刷新。Revit API 不移出主线程。
- 新增 `10_运行诊断.csv`：值索引规模、相关元素、身份回退、错误熔断、Delete/事务耗时、批次/二分/回滚和依赖对象统计。
- 静态与纯逻辑检查通过；下一步由 DeepSeek 真实编译 3.1.35，并通过 YangTools 界面复测，不再用临时 Python 脚本替代插件验收。

## 3.1.33 重复参数实机性能定向修复

- DeepSeek 实机报告：2750 个项目参数、978 个唯一名称、606 个重复组；按清单计算共有 1772 个冗余来源参数。全部为实例绑定，未发现共享参数。
- A/B/C 回滚实验：10 个来源直接 `Document.Delete` 99.9 ms；`BindingMap.Remove` 95.5 ms但实际移除 0；Remove+Delete 119.2 ms。因此拒绝新增解绑步骤，继续直接删除来源 ParameterElement。
- 每个来源 ParameterElement 会连带 1 个 Revit 依赖子对象。旧门禁把这些正常依赖误判为未知级联，使每个批次回滚并递归拆成单项，是“10 个也慢”的核心原因。
- 新删除路径在调用 Delete 前使用 `Element.GetDependentElements(null)` 收集官方定义的依赖子对象，允许这些对象随父参数删除。返回结果若包含允许集合之外的对象，或未包含请求参数，才回滚并二分定位。
- 初始批次提升为 250；正常情况下 606 个候选只需 3 次 API 调用，理论 1772 个来源约 8 批。异常批次仍递归隔离，不恢复逐项循环。
- 快速处理取消事务内 CSV；取消提交后的第二次完整扫描。提交前扫描与完整性复核结果在成功提交后直接复用，减少模型读取、引用扫描和界面刷新重复工作。
- 新增全链路分阶段计时：快照、合并、Delete API、子事务提交、外层事务提交、复扫、完整性复核、报告写入、UI 刷新与总耗时。
- 保留硬复核：所有清理前不同非空内部值必须保留；报告全部来源删除成功的组必须只剩一个 ParameterElement。任何失败回滚整个 TransactionGroup。
- 当前静态检查和纯逻辑模拟通过；真实 Revit 2021 需先复测“最多 10 个来源参数”，确认 Delete 不再递归二分、清理后唯一且值完整。


## 3.1.32 重复参数清理提交前完整性复核

- 快速处理和“仅删除”在事务组提交前创建清理前快照，按元素 ID、参数名称和 Revit 内部 `StorageType` 保存每一个不同非空值；不使用 `AsValueString()` 比较。
- 清理后在同一事务组内重新扫描，逐元素检查清理前的每个不同内部值是否仍存在于清理后的同名参数集合中。发现任何值缺失、元素缺失、快照/复核读取错误时，整个事务组自动回滚。
- 对本轮选中组执行唯一性复核：若所有来源参数都已报告删除成功，清理后必须准确剩余 1 个 `ParameterElement`；否则视为实现异常并回滚。因共享参数、引用、冲突或级联保护而未完全删除的组允许保留，但会明确报告“仍重复”。
- 新增 `08_参数值完整性复核.csv` 与 `09_已选参数唯一性复核.csv`。运行输出显示快照值数量、验证通过/缺失数量、完全清理组唯一性和仍重复组数量。
- 纯逻辑链路测试新增两类反例：删除冲突来源导致某个原值消失时必须检测失败；来源均报告删除但清理后仍有 2 个同名参数时必须检测失败。
- 保留 3.1.31 自适应批量删除：默认 25 个参数一批，异常批次二分隔离；完整性复核只增加两次只读单次遍历，不恢复逐参数扫描。
- 当前静态结果：234 个文本文件、142 个 C#、39 个 XAML、34 个根节点、4 套主题、28 个 Ribbon 命令；错误 0、警告 0。真实 Revit 2021 需先用不超过 10 个来源参数验证清理后唯一、值完整和撤销。


## 3.1.30 重复项目参数清理整合与预览复核

- 将已完成 Revit 2021 编译和 606 组扫描验证的独立参数清理工具整合进 YangTools，入口固定为 `检查工具区 → 重复项目参数清理`；Ribbon 总数由 27 增至 28。
- 删除独立 WinForms 视觉方案，新增 YangTools WPF 窗口，使用现有 `ThemeHelper`、四套主题、共享按钮/DataGrid/底栏样式；窗口由 Revit 主窗口拥有。
- 新增统一图标 `parameter_cleanup_16/32.png`：两个重复参数记录向一个保留记录收敛，并以勾选表示清理，符合 `ICON_DESIGN_GUIDE.md` 的工程线条风。
- 窗口底栏在“关闭”旁新增“功能说明”，明确扫描、仅合并、快速处理、仅删除、撤销和风险选项的实际行为。
- 扫描后自动输出预览报告：项目参数总数、唯一名称、不重复参数数、重复组/重复参数数、兼容组、选中来源数、理论清理后参数/重复组/不重复参数，以及按共享参数和已知引用计算的保守删除预估。
- 快速处理和单独删除完成后自动重新扫描，并输出清理前后对比：参数总数、重复组、不重复参数、唯一名称、本轮删除数、是否仍有重复参数及剩余示例；CSV 增加 `07_清理后复核.csv`。
- 性能主线固定为实例元素与元素类型各一次 `FilteredElementCollector` 遍历；参数 ID 字典匹配，禁止 `LookupParameter(name)`。内层循环复用字典/集合，取消每个元素/参数组的 LINQ List/Tuple 分配。
- 绑定类别已一致时完全跳过 `BindingMap.ReInsert`，只有实际需要扩展类别的保留参数才写数据库；无 ReInsert 时不执行多余 `Document.Regenerate()`。
- 参数值必须按 `StorageType` 使用 `AsDouble/AsInteger/AsString/AsElementId` 与对应 `Set` 直接复制。`AsValueString()` 仅允许用于显示，不得参与回填。
- 继续采用“保留原参数并原位合并”，禁止“全部删除再同名重建”；共享 GUID、明细表/过滤器引用和文档内 ParameterElement ID 不允许通过重建假定恢复。
- 门禁保持快速上线级别：共享参数和冲突来源默认保护但可手动放开；删除时若 `Document.Delete()` 返回保留参数之外的任何 ID，当前 SubTransaction 自动回滚，其他参数继续。
- 新增 Revit 2021–2027 参数 API 兼容层：2021 使用 `ParameterType/BuiltInParameterGroup`，2022+ 使用 `GetDataType/GetGroupTypeId/ForgeTypeId`；ElementId 使用现有 long 适配。
- 当前静态结果：219 个文本文件、141 个 C#、39 个 XAML、34 个根节点、4 套主题、28 个 Ribbon 命令；错误 0、警告 0。当前环境无 Windows/.NET/Revit，需 DeepSeek 编译并在项目副本执行快速处理与撤销验收。


## 3.1.29 Revit 原生 AI 面板与默认模型参数

- AI 助手从 Revit Owner 独立 `ToolWindow` 改为真正的 Revit `DockablePane`。`App.OnStartup` 注册固定 `DockablePaneId` 与 `IDockablePaneProvider`，Ribbon 命令只负责显示/隐藏；独立 `AssistantWindow.xaml/.cs` 已删除。
- 面板内容继续使用本地 WPF 资源隔离：不创建第二个 `Application`、不写 `Application.Current.Resources`、不使用 NativeHook 或 Idling 自动展开。初始停靠在 Revit 右侧，之后位置由 Revit 保存和管理。
- 工作面板可视树只包含工作视图。私人会话不在 XAML 中预声明，首次通过左上角无文字双击手势触发时才创建；工作界面没有“摸鱼”、私人模式按钮、说明或斜杠命令。
- 工作与私人会话仍使用 `%APPDATA%\YangTools\Assistant\Work` 与 `...\Chat` 两套独立设置、提示词、历史和 DPAPI API Key。私人会话始终关闭 Selection、Revit API Tool 和 Python 执行。
- 删除 `AssistantResponseStyle`、`ResponseStyleBox`、`Temperature`、`MaxOutputTokens`、`GetTemperature` 及三类 API payload 中的 `temperature/max_tokens/max_output_tokens/num_predict`。模型名仍保留，因为服务请求必须指定目标模型；具体生成行为由模型与服务商默认设置决定。
- 输出截断只根据服务端明确的 `finish_reason` / `incomplete_details.reason` / `done_reason` 判断。服务没有返回结束原因时不再根据插件自定义上限推测；明确截断时仍显示“继续”。
- 消息正文与技术详情的 `FlowDocumentScrollViewer` 允许获得焦点、鼠标框选、`Ctrl+C` 和右键复制/全选。引用块改为可选择的 `Paragraph`，代码块继续使用只读 `TextBox`。
- 静态检查更新为 3.1.29 基线：禁止恢复独立窗口、模型生成参数、流式链路和工作面板可见私人文案；要求 DockablePane 注册、私人视图延迟创建、可选择正文和 27 个 Ribbon 入口。
- 当前非 Windows/Revit 环境已完成 205 个文本文件、130 个 C#、38 个 XAML、33 个根节点静态核验，错误 0、警告 0。真实编译及 Revit 2024 停靠/浮动/重启恢复仍需实机验收。

## 3.1.28 AI / 摸鱼助手界面与输出重构

- 删除运行时流式输出链路：设置项、SSE/逐块读取、增量回调和消息刷新逻辑全部移除；Chat Completions、Responses、Ollama 均使用一次性非流式响应，避免半成品 Markdown、频繁重排和“正在生成但内容不可见”。
- AI 助手继续采用 Revit 主窗口 Owner 的非模态 WPF `ToolWindow`，不重新引入曾导致黑屏/崩溃的 `DockablePane` 和 NativeHook；界面改为接近 Revit 属性/工具面板的紧凑布局，窗口不进入独立任务栏。
- 消息布局改为非对称：用户保留右侧气泡，AI 使用全宽文档式区域；正文 14px、21px 行高，普通 Markdown 单换行合并为空格，空行才分段。
- 代码不再被归入“技术详情”。代码块在正文以独立卡片显示，支持复制；工作模式的 Python 卡片提供显式“执行”按钮，取消生成后自动弹窗打断对话。
- Markdown 增强：标题、段落、列表、引用、表格、行内代码、粗斜体和 HTTPS 链接；显式 reasoning、工具参数、Revit 原始上下文继续折叠在“推理与工具详情”。
- 顶部增加人设快速切换和“携带当前选择”开关；设置按连接、回复、人设分组，工作默认严谨温度 0.2，摸鱼默认自由温度 0.85。
- 人设编辑器支持导入/导出、复制预设和实时字符/UTF-8 大小显示。存储与请求链路不截断内容；导入单文件上限 2 MB，因此 100 KB+ 人设可完整保存和发送。
- 大人设仍受所选模型上下文窗口限制。若 API 返回 context-length / too-many-tokens 类错误，界面会明确提示缩短人设、减少历史或换大上下文模型，不再显示笼统 HTTP 400。
- 输出上限可按模式配置为 256–131072 tokens，默认 8192。程序读取 Chat `finish_reason`、Responses `incomplete_details`、Ollama `done_reason` 和 usage；检测到长度上限时显示警告和“继续”按钮。对不返回结束原因的兼容代理，若 usage 精确达到配置上限则保守提示可能截断。
- 当前环境没有 Windows/.NET/Revit，已完成 204 个文本文件、130 个 C#、39 个 XAML、34 个根节点的静态检查，错误 0、警告 0；最终仍需 Windows 编译与 Revit 2024 实机对话/执行验收。

## 3.1.27 运行时资源修复与部分年份安装器

- 用户实机报告若干窗口抛出 `Provide value on 'System.Windows.StaticResourceExtension' threw an exception`。根因不是资源键不存在，而是 Revit 宿主中的 WPF 初始化顺序：当窗口声明自己的 `Window.Resources` / `UserControl.Resources` 时，初始化阶段可能暂时看不到 ThemeManager 预挂载的外部共享字典。
- 已消除业务 XAML 对外部共享样式的 `StaticResource` 依赖：共享样式在依赖属性上统一改为 `DynamicResource`；局部 `Style.BasedOn` 无法动态解析的部分改为自包含样式。
- 重点修复 AI 子面板、批处理与云链接、项目中文检查、族文档管理、族实例管理、图纸与图框管理；同时对全部 34 个 XAML 根节点执行同类扫描，外部共享 `StaticResource` 残留为 0。
- `ThemeHelper.InitializeWithTheme` 现在会把最深层 WPF 资源错误和面板类型带入异常消息，后续实机若仍失败可直接看到具体资源/类型，不再只有 `StaticResourceExtension` 外层信息。
- 静态检查新增 `external-static-resource` 回归规则：局部资源只允许静态引用同一 XAML 内定义的资源；共享主题资源必须动态解析。
- 安装器不再要求 2021–2027 七套 DLL 同时存在。用户选择了缺失年份时会记录并跳过，其他真实编译年份继续安装。
- 源码模式安装器会直接识别 `src\YangTools.Revit\bin\x64\{年份}`，因此 DeepSeek 完成 `dotnet build/deploy` 后可直接安装，无需额外生成 payload。
- `Package-Installer.ps1 -SkipBuild` 会从本地 bin 收集已有年份并生成部分年份正式包；只有显式 `-RequireAll` 才要求七年份齐全。
- 安装器除 DLL 和 addin 外还校验 `UI\SharedStyles.xaml` 与四套主题文件，避免安装“编译成功但运行资源不完整”的负载。
- 当前环境仍不能启动 Windows/Revit。已完成 XML/XAML、UTF-8、资源作用域、事件处理器、PowerShell 结构和源码基线静态核验；最终 GO 需重新编译并在 Revit 2024 点击高风险面板。

## 3.1.26 面板静态验收与免管理员图形安装器

- 新增 `tools/Test-UiPanelsStatic.py` 与根目录 `静态检查面板与编码.cmd`，统一检查全部文本 UTF-8、XAML/XML 解析、主题资源键、Static/DynamicResource、事件处理器、XAML 代码后置类、主题隔离以及 Ribbon 数量。
- 本轮实际静态结果：199 个文本文件、130 个 C# 文件、39 个 XAML、34 个窗口/视图根节点、4 套主题、27 个 Ribbon 命令；错误 0、警告 0。
- 34 个 XAML 根节点由 32 个 Window/Dialog 和 2 个 UserControl 组成；全部代码后置文件均使用 `ThemeHelper.InitializeWithTheme(this, InitializeComponent)`。
- 新增 `installer/YangTools安装器.ps1` 和 `YangTools安装器.cmd`：中文图形界面，自动识别 Revit 2021–2027、显示 payload/安装状态、支持多版本安装更新与卸载。
- 安装位置仍为当前用户 `%APPDATA%\Autodesk\Revit\Addins\{年份}`，不写注册表、不写 Program Files、不需要管理员权限。安装过程改为 staging + backup 原子替换，失败时恢复旧版本；卸载保留 `%APPDATA%\YangTools` 个人设置。
- `Package-Installer.ps1` 改为从 csproj 自动读取版本，默认要求 2021–2027 七套真实构建产物，生成安装说明和 SHA-256 清单；只有显式 `-AllowPartial` 才允许制作调试用部分年份包。
- Revit 2027 继续独立编译为 `net10.0-windows`；不能复制 2026 DLL 冒充。
- 当前环境不是 Windows/Revit，不能宣称所有面板已经在 Revit 中真实点击打开；发布前仍需 Revit 2024 完成一次逐入口烟雾验收。

## 3.1.25 AI 简洁显示、执行器/MCP 重构与图标收口

- AI 窗口正式命名为 `AssistantWindow`，系统设置窗口命名为 `SystemSettingsWindow`；仍采用 Revit 主窗口 Owner 的独立非模态工具窗，只在用户点击 Ribbon 后创建，不恢复 DockablePane。
- 保留工作/会话两个真实面板。两边分别保存设置、提示词、历史和 DPAPI API Key；会话面板硬关闭 Revit 上下文、工具调用和 Python。
- 默认启用“简洁显示”：正常回答直接显示；API 显式 reasoning、`<think>`/`<analysis>`、代码、工具调用、Revit 上下文、MCP/HTTP 诊断统一折叠到“技术详情”。普通成功聊天没有技术内容时不显示空详情；复制回复只复制可见主回答。流式阶段只更新纯文本，结束后一次性渲染 Markdown。
- 代码块增加最大高度、内部滚动和复制按钮，避免长代码撑高整个聊天记录。
- Endpoint 解析改用 `UriBuilder`，明确区分 Base URL 与完整聊天 Endpoint，带查询参数的完整地址不再被错误追加路径。HTTP 405 主提示简化，方法、最终 URL、状态码和返回体放入技术详情。
- 设置面板使用提示词深拷贝，点击取消会丢弃全部编辑，不再污染当前会话；DPAPI 加密失败时拒绝保存 API Key，不再退回 Base64。
- Revit API 工具只在 Chat Completions 且本地 `Docs/RevitAPI.xml` 存在时可用；Responses/Ollama 或文档缺失时自动关闭。
- 新增统一 `RevitExecutionBroker`，内置工作助手和外部 MCP 共用同一个 ExternalEvent 执行器；具备队列、状态、超时、取消和延迟 IronPython 初始化，超时请求不会稍后继续执行。
- 外部 `McpGateway` 默认关闭，只绑定 127.0.0.1，在 8081–8090 动态选端口，每次启动生成随机 Bearer Token，并写入按 Revit 进程区分的实例文件。
- `McpServer/server.py` 改用 FastMCP stdio，从实例文件自动发现网关，不再固定 8081；新增 `requirements.txt` 与独立使用说明；删除未实现的 `run_revit_command` 和错误命名的 `run_pyrevit_script`，只公开六个真实工具。外部 Python 默认禁止。
- “MCP状态”页更名为“MCP与执行服务”，显示执行器、Python状态、待处理任务、网关地址/令牌/实例文件、最近请求/命令/耗时/错误，并提供启动、停止、重启、复制配置和自检。
- 图标规范继续执行；实体生成（Loft）与实体提取不再共用图标。当前为 27 组语义、54 个 16/32 PNG，核验脚本增加真实像素尺寸和透明通道检查。
- AI/摸鱼助手已在 2026-08-14 删除；MCP 独立边界以当前 `Mcp/` 源码、系统设置和 HANDOFF 为准。图标规范见 `docs/ICON_DESIGN_GUIDE.md`。
- 当前环境没有 Windows .NET SDK/Revit；本版本只完成源码静态审查和资源核验，未进行真实编译、AI请求或 Revit/MCP 实机验证。

## 3.1.23 工作助手 / 摸鱼助手一体化与完全隔离

- 保持一个 YangTools 插件、一个独立 AI 窗口和一套共享聊天底层，不再拆分 `YangTools.Moyu.dll`。
- 工作助手与摸鱼助手使用两个独立运行配置：工作模式允许读取 Revit 选择上下文、调用 Revit API 搜索工具和人工确认后的 Python 执行；摸鱼模式不读取 Revit 上下文、不调用 Revit 工具、不执行 Python。
- 摸鱼模式内置但隐藏：默认无可见切换按钮；双击左上角模式图标或输入 `/moyu` 进入，输入 `/work` / `/ai` 返回工作模式。
- YangTools 不为摸鱼模式增加关键词屏蔽或内容改写；实际模型服务仍可能执行其自身的服务规则。
- 两种模式的持久化完全隔离，每种模式分别保存 `settings.json`、`prompts.json`、`history.json`、`api_key.dat`。
- 新目录：`%APPDATA%\YangTools\Assistant\Work` 与 `%APPDATA%\YangTools\Assistant\Chat`。旧 `copilot_*` / `moyu_*` 文件首次启动时只迁移复制，不删除原数据。
- API Key 独立保存并使用当前 Windows 用户范围加密；工作模式与摸鱼模式不共享 Key、模型、URL、提示词或历史。
- 修复模式切换期间异步回复可能写入另一模式的问题：发送任务绑定到启动时的 ModeState，回复完成前阻止切换。

## 3.1.22 AI 助手独立窗口与崩溃修复

- 移除 Revit `DockablePane` 注册，不再创建空黑停靠面板，也不再继承 Revit 保存的自动展开状态。
- AI 助手改为独立非模态 WPF 窗口，只有点击 Ribbon“AI 助手”时才创建；再次点击隐藏/显示。
- `CopilotPanel` 从 `Page` 改为 `UserControl`，避免停靠页导航/宿主生命周期导致的初始化异常。
- 删除无订阅者的全局 `Ctrl+Shift+Q` 低级键盘钩子，避免无意义的进程级输入监听。
- 增加 AI 设置与历史记录兼容清理，旧 JSON 中空列表、空对话数据不再导致窗口初始化崩溃。
- 已发现“摸鱼助手”：具有独立设置、独立历史和三组默认人设；仅在部署目录存在 `YangTools.Moyu.dll` 时开放切换。
- 修复双击 AI 图标可绕过 `YangTools.Moyu.dll` 检查的问题。

## 3.1.21 AI 停靠面板 WPF/System.Drawing 编译兼容修复

- 修复 `CopilotPaneProvider.cs` 中 3 处 `Brushes` 与 1 处 `FontFamily` 的 `CS0104` 命名空间歧义。
- 原因是项目同时启用 WPF、WinForms 与隐式 using，编译器可同时看到 `System.Drawing` 和 `System.Windows.Media` 中的同名类型。
- 4 处 WPF 类型统一使用 `System.Windows.Media` 全限定名；不改 AI 助手按需创建、停靠面板显示逻辑或文本工具业务。
- 源码核验新增回归检查，禁止 `CopilotPaneProvider.cs` 恢复未限定 `Brushes` / `FontFamily`。
- 3.1.20 因 Revit 2024/2025 均出现 4 个 `CS0104`，停止使用；3.1.21 待 Windows 双版本编译与部署。

## 3.1.20 文本工具 WPF/Revit ComboBox 编译兼容修复

- 修复 `DistributeTextWindow.xaml.cs` 与 `AlignTextToLineWindow.xaml.cs` 中 `ComboBox` 同时命中 `System.Windows.Controls.ComboBox` 和 `Autodesk.Revit.UI.ComboBox` 的命名空间歧义。
- 3 处方法参数改为 `System.Windows.Controls.ComboBox` 全限定类型；不改文本工具 UI、选择状态机或业务算法。
- 源码核验新增回归拦截，禁止在同时引用 Revit UI 的文本窗口中恢复未限定 `ComboBox`。
- 3.1.19 因 2024/2025 均出现 CS0104，停止使用；3.1.20 随后因 Copilot WPF/System.Drawing 歧义停止使用。

## 3.1.19 AI 按需打开、移除 MEP 废弃功能、文本工具 UI/选择链修复

- AI 助手停靠面板仍在 Revit 启动阶段注册，但只创建空宿主；真实 `CopilotPanel` 仅在用户点击“AI 助手”后创建。
- 首次 Idling 强制隐藏 Revit 恢复的旧停靠面板可见状态，避免自动展开。
- 删除 `管井标高修改`、`管道修改 TypeA` 两个 MEP 功能及 4 个图标；Ribbon 数量由 29 降为 27。
- 重做文本工具区 4 个选择型窗口：选择区固定写入 XAML，不再运行时插入 Grid 行，修复控件覆盖/窄窗截断。
- `等距分布`、`对齐到线`、`对齐文本`、`文本合并` 改为“窗口请求选择 → 外部命令执行 Revit Selection → 重开窗口”的状态机，禁止模态窗口 `Hide/Show` 后直接选图元。
- 文本修改增加输入校验、事务提交检查和完整错误日志，失败时整批回滚。

## 3.1.18 启动兼容修复：不依赖 Application.Current

- 3.1.17 已解决 ACC 云模型打开时的全局 WPF 资源污染，但在部分 Revit 启动阶段 `Application.Current` 尚未建立，导致 YangTools 主动返回加载失败。
- 主题和共享样式现在从 XAML 初始化开始就只附着在当前 YangTools 根元素，不再依赖、读取或短暂写入 `Application.Current.Resources`。
- AI 助手停靠面板改用 `IFrameworkElementCreator` 按需创建；`OnStartup` 只注册轻量 Provider，不再提前构造 `CopilotPanel`。
- AI 面板自身初始化失败时返回隔离的错误页并写诊断日志，不再拖垮整个插件启动。
- 保持 3.1.17 的 ACC 隔离原则：不创建第二个 WPF Application，不向 Revit 全局资源注入 YangTools 样式。

## 3.1.17 紧急兼容修复：ACC 云模型启动崩溃

- 3.1.16 会把主题与 `SharedStyles.xaml` 长期合并到 Revit 的 `Application.Current.Resources`。共享样式包含 `Window`、`Page`、`Button`、`TextBox`、`ComboBox` 等隐式样式，可能污染 Autodesk 自己的 ACC 云模型 WPF 界面。
- 用户实机表现：本地模型与中心模型正常；打开 ACC 云模型时 Revit 闪退，崩溃模块为 `PresentationFramework.dll`，托管异常为 `System.Windows.Markup.XamlParseException`；卸载 YangTools 后恢复。
- 3.1.17 不再创建第二个 `System.Windows.Application`，也不再让 YangTools 资源长期驻留 Revit 全局资源域。
- 所有 XAML 根窗口/页面通过 `ThemeHelper.InitializeWithTheme` 在同步解析期间短暂提供资源，解析完成后立即把字典转移到当前 YangTools 根元素；纯代码窗口直接使用本地资源。
- 主题切换只更新已注册的 YangTools 根元素，不修改 Revit/ACC 的全局 WPF 样式。
- 3.1.16 应视为严重缺陷版本，在 3.1.17 完成 ACC 实机复验前不要继续使用。


## 1. 项目定位

YangTools 是 Yang 的个人 Revit 工作流插件，不商业化、不对外发布、不开源。目标是把高频 Revit / CAD / 批处理 / 本地 AI 操作收敛到一个中文工具页，而不是制作插件市场或企业平台。

优先级：

1. 重点功能可用、操作可撤销。
2. Revit 2021–2027 真实兼容。
3. 中文 UI、主题和图标统一。
4. 安装、更新、卸载简单。
5. 文档足够让 AI 延续开发，不做无价值的企业级流程。

## 2. 硬要求

- Revit 2021–2027 使用同一份业务源码，但每个年份必须引用对应 Revit API 独立编译。
- 2021–2024：`.NET Framework 4.8`；2025–2026：`.NET 8`；2027：`.NET 10`。
- 禁止把某一年的 DLL 复制给其他年份冒充兼容。
- 安装到 `%APPDATA%\Autodesk\Revit\Addins\{年份}`，不得要求管理员权限。
- 界面和提示使用中文；开发术语确有必要时可保留英文缩写。
- 模型写入必须在 Revit Transaction / TransactionGroup 中完成；非模态窗口调用 Revit API 必须经过 ExternalEvent。
- 个人项目安全边界从简；工作助手生成的 Python 在执行前仍保留一次人工确认；摸鱼模式不进入 Revit/Python 执行链。
- 不追求全功能自动化测试。源码静态检查、目标年份编译、Revit 2024 关键实机验收即可。

## 3. UI 与图标规范

- 所有主题颜色从 `UI/Themes/*.xaml` 获取，业务 XAML 禁止写死白色、黑色、灰色或十六进制颜色。
- 已打开窗口中的主题色必须使用 `DynamicResource`；禁止用 `StaticResource` 固化主题画刷。强色按钮统一使用 `AccentText`，不要复用标题栏文字色。
- 公共控件优先使用 `UI/SharedStyles.xaml`：`StandardButton`、`PrimaryButton`、`DangerButton`、`StandardTextBox`、`CardBorder`、`WindowTitle`、`BottomBar` 等。
- XAML 窗口、Page 和停靠面板必须使用 `ThemeHelper.InitializeWithTheme(this, InitializeComponent)`；纯代码窗口使用 `ThemeHelper.PrepareForInitialize(this)`。
- YangTools 主题和共享样式必须限定在插件自己的根元素中；禁止长期写入 Revit 的 `Application.Current.Resources`，也禁止创建第二个 `System.Windows.Application`。主题切换由 `ThemeManager` 更新已注册的 YangTools 根元素。
- 窗口默认字体为 `Microsoft YaHei UI`；文本文件统一 UTF-8。包含中文的 PowerShell 脚本使用 UTF-8 BOM；源码基线核验脚本使用纯 ASCII 输出，避免 Windows PowerShell 5.1 控制台编码差异。
- Ribbon 图标统一采用简约、克制、优雅的工程线条风；完整规则以 `docs/ICON_DESIGN_GUIDE.md` 为准。
- 运行时只保留实际引用的透明 PNG，每个功能必须同时提供 16×16 与 32×32；主轮廓色 `#355D7A`，强调色 `#6E9FC4`，禁止渐变、阴影、拟物、照片和 Emoji。
- 新增或修改图标必须更新 `tools/Generate-RibbonIcons.py`，不得手工复制其他功能图标充当占位。
- 标题栏、内容边距、底部操作栏应复用共享样式，不在每个窗口复制一整套按钮模板。

## 4. 开发规范

### 开工与收尾

1. 每个版本解压到全新的空目录，禁止覆盖旧版本目录；先运行 `核验源码.cmd`。
2. 先读 `PROJECT.md` 与 `WORKLOG.md`。
3. 确认本轮目标和重点功能，不顺手扩张范围。
4. 修改后完成 XML/编码/引用静态检查。
5. 更新本文件中的功能状态，再追加 `WORKLOG.md`。
6. 只有确实需要 Windows/Revit 时，才给 DeepSeek 一段完整、可直接执行的本机提示词。

### Revit API

- 使用 `REVIT2021_OR_GREATER` 至 `REVIT2027_OR_GREATER` 处理被编译器或实机证明存在的 API 差异。
- ElementId 通过 `ElementIdExtensions` 读写，避免 2024 的 64 位 ID 差异散落到业务代码。
- 捕获取消操作时返回 `Result.Cancelled`；真正失败才返回 `Result.Failed`。
- 批量操作尽量使用一个 TransactionGroup，使用户能一次 Ctrl+Z 撤销。
- 不吞掉会改变结果的异常；允许忽略单个不可读参数，但必须汇总失败数量或写调试日志。

### 文件与发布

- 源码仓库不保存 `bin`、`obj`、PDB、旧 deploy 或安装包。
- 新源码包必须解压到空目录。禁止覆盖解压；ZIP 不会删除旧版已移除文件。构建前运行 `tools\Verify-SourceBaseline.ps1`，项目文件也会阻止陈旧入口参与编译。
- 普通构建不得自动覆盖 Revit 插件目录；开发部署必须显式传入 `-p:DeployAfterBuild=true`。
- 版本号以 `YangTools.Revit.csproj` 为准；关于页运行时读取程序集版本，不再硬编码。
- 高频功能的纯文本/数值/开关输入保存到 `%APPDATA%\YangTools\settings.json`；不得持久化 Revit `ElementId`、曲线或实例选择，避免跨项目引用失效。
- 发布包结构只能是 `安装.cmd`、`卸载.cmd`、PowerShell 脚本和 `payload/{年份}`。

### AI 分工

- ChatGPT：项目管理、架构、Revit API 风险、代码修改、审查、文档与最终判断。
- DeepSeek：只在当前环境无法完成时承担 Windows 编译、命令执行和 Revit 实机步骤；不得自行大重构或新增功能。
- DeepSeek 返回内容必须包含：执行命令、完整错误、修改文件、各年份结果和最终文件路径。

## 5. 构建与安装

Windows 开发环境需要 Visual Studio 2022、.NET Framework 4.8 Developer Pack、.NET 8 SDK、.NET 10 SDK和可访问 NuGet。

```powershell
# 单独构建 Revit 2024
dotnet build .\src\YangTools.Revit\YangTools.Revit.csproj -c 2024 -p:Platform=x64

# 构建 2021–2027 并整理 payload
.\tools\Build-All.ps1

# 生成无管理员权限安装包
.\tools\Package-Installer.ps1 -SkipBuild

# 本项目主力年份：一次性编译并部署 2024 + 2025，防止只部署一个年份
.\tools\Build-Deploy-2024-2025.ps1
# 或双击根目录：编译部署2024和2025.cmd
```

开发机只部署 2024：

```powershell
dotnet build .\src\YangTools.Revit\YangTools.Revit.csproj -c 2024 -p:Platform=x64 -p:DeployAfterBuild=true
```

## 6. 功能清单与状态

状态说明：`源码已审` 表示完成静态入口/事务审查；`待实机` 表示必须在 Revit 验证；没有“实机通过”记录就不能宣称功能完成。

### A. 模型修改区（最高优先级）

| 功能 | 作用 | 当前状态 |
| --- | --- | --- |
| 线性布置 | 沿连续曲线按距离/数量放置族并对齐方向；支持基准标高 + 首尾相对高程线性插值 | 2026-08-12 新增非零标高安全高程控制；源码/静态通过，需当前源码重新编译并做 Revit 2024/2025 专项复验 |
| 沿线布柱 | 选择连续模型线，按间距布置结构柱/建筑柱，并设置 Base/Top Level 与 Offset | 3.1.14 新增；3.1.15 补齐沿路线切线旋转，并增加可记忆的旋转偏移角度；待 Revit 2024/2025 双部署及实机验证 |
| 标高修改 | 修改所选图元标高并尽量保持世界坐标高度 | 3.1.5 修复后用户最小实机通过；更多 MEP 类别按需补测 |
| 基于面转换 | 将基于面族转换为非基于面常规模型族 | 3.1.5 修复后用户最小实机通过；删除旧实例仍只在项目副本使用 |
| 实体生成（Loft） | 从轮廓创建放样/拉伸族并载入项目 | 3.1.8 用户确认实机成功；材质允许留空并使用族类别/模板默认材质；模板、族名、类别、标高、材质和 Void 选项会记忆；模型修改区本阶段收口 |
| 实体提取 | 从图元几何提取 Solid 并生成族 | 3.1.8 用户确认实机成功；材质留空时使用族类别/模板默认材质且不创建材质参数；模板、族名、类别、标高和材质会记忆；模型修改区本阶段收口 |
| 布尔几何 | 主体与多个目标执行融合、剪切、连接 | 3.1.5 修复后用户最小实机通过；3.1.8 记忆“删除目标实例/目标族”选项，图元选择不跨会话保存；仍限定项目副本使用 |

### B. 项目管理区（最高优先级）

| 功能 | 作用 | 当前状态 |
| --- | --- | --- |
| 族文档管理 | 族/类型查看、参数编辑、重命名、复制、导入导出、删除 | 3.1.12 增加同文档单实例窗口；复制类型自动生成不冲突名称；重命名、改类别、导出和删除均固定操作点击时选中的族/类型 ID；RFA 改为选择导出文件夹、过滤非法文件名并确认覆盖；类型数量按模型真实值刷新；待实机 |
| 族类别修改 | 批量修改可编辑载入族的 Revit Family Category，例如 Abutments → Generic Models | 2026-08-14 新增；批量选择、搜索、目标类别、跳过不可编辑/内建族、EditFamily + FamilyCategory + LoadFamily；回载后校验 ID/正 ID 自定义参数值，异常整批回滚；待 Windows 编译与 Revit 2024/2025 实机 |
| 族实例管理 | 实例筛选、参数编辑、Excel 同步、选择与删除 | 3.1.12 增加同文档单实例窗口，避免重复窗口持有不同筛选/ExternalEvent 状态；保留 3.1.9 的非模态选择恢复、原文档绑定、显示单位参数与 Excel 处理；待实机 |
| 项目资产管理器 | 视图、过滤器、填充图案、线型及系统资产管理 | 3.1.12 增加同文档单实例窗口；普通视图分组排除图纸，避免与图纸节点重复；“未使用过滤器”同时检查普通视图与视图样板；线型预览不再把 Revit API 访问延迟到 Dispatcher；批量删除使用 `TransactionGroup.Assimilate()`；线型/填充区域名称和线宽写入失败会回退 UI；待实机 |
| 图纸管理 | 图纸参数、修订、Excel 建图、图框更换与删除 | 3.1.12 增加同文档单实例窗口；刷新时清理修订缓存；图号、图名、自定义图纸/图框参数和图框换型失败时回退界面值；动态图框参数改为失去焦点后写入，避免每次按键触发事务；图框类型变更后同步刷新显示；待实机 |

### C. 其他重点功能

| 功能 | 作用 | 当前状态 |
| --- | --- | --- |
| 临时插件箱 | 按 Revit 项目管理短期 IExternalCommand DLL，并收藏有复用价值的候选工具 | 3.1.12 明确“普通仓库=项目临时插件、默认仓库=收藏/候选转正”；新增临时项目默认目录、收藏包清单与原子覆盖更新、受管理收藏包整包删除、运行缓存清理；影子复制、多入口选择和依赖解析继续保留；新增 `MicroTool_Developer_Guide V3.md`；待实机 |
| 从 CAD 粘贴 | 查找 AutoCAD 临时 DWG 并导入当前视图 | 3.1.12 工作副本改为按 Revit 进程和运行 GUID 分层；成功/失败后重试清理本次副本；启动和导入前自动清理已结束会话的一天前残留；新增手动缓存清理，可选择仅清理 YangTools 工作副本，或同时清理 24 小时前且未被占用的 AutoCAD `A$C*.dwg`；待实机 |
| 批处理与云链接 | 当前项目批量创建 Revit/CAD 链接，并对当前或外部项目导出 NWC/IFC/DWG/PDF，另支持 Revit 2021+ 通过 PDF 虚拟打印机打印合并 PDF | 3.1.16 重构：链接只作用于当前文档且合并为一个撤销步骤；外部工作共享模型按需分离打开、只导出并无保存关闭；多模型独立子目录与 `{模型名}` 文件名模板；严格按名称匹配视图/图纸集/导出设置；检查导出返回、文件覆盖和 NWC 导出器；PDF 支持范围修正为 Revit 2022+；保存设置并生成批处理报告；仅支持本地/网络/Desktop Connector 已同步路径，不冒充直接 ACC 云浏览；待 2024/2025 双编译部署与实机 |
| MCP与执行服务 | 显示内部 Revit 执行器与可选外部 MCP 网关的真实状态 | 3.1.25：网关默认关闭、动态端口、随机令牌、实例发现、六个真实工具、外部 Python 默认禁用；待实机验证 |

### D. 非重点功能

| 区域 | 功能 | 状态 |
| --- | --- | --- |
| 文本工具 | 文本修改、合并、等距分布、对齐文本、对齐到线 | 3.1.19 统一重做 4 个选择型窗口布局，修复选择按钮覆盖内容；选择流程改为外部命令状态机，文本修改增加校验、回滚和日志；待实机 |
| 视图工具 | 可见性拷贝、覆盖清理、按线创建剖面 | 源码存在，待按需实机 |
| 标注工具 | 标注文字替换 | 源码存在，待按需实机 |
| 检查工具 | 中文检查 | 分类表格、重命名、删除/清空与级联预检已实现；需重新编译和 GUI 专项 |
| 项目信息 | 文件信息和清理后大小预估 | 乱码 UI 已重写；反射调用清理 API 未确认，待实机 |
| 系统管理 | 系统设置 | AI/摸鱼助手入口已整体删除；主题、Ribbon 面板和 MCP 状态统一由“系统设置”管理 |

## 7. 当前已知限制

- 2026-07-10 已在全新目录完成 3.1.4 正式基线验收：ZIP 哈希一致，源码为 28 个 Ribbon 命令；Revit 2024 编译 0 error / 498 warnings，Revit 2025 编译 0 error / 566 warnings；Revit 2024 干净部署后 Journal 注册 28 个按钮，三个废弃入口全部消失。该结果是当前已确认的运行基线。
- 模型修改区六个核心功能已完成当前阶段最小实机确认：线性布置、标高修改、基于面转换、布尔几何在 3.1.5 通过；Loft 与实体提取在 3.1.8 修复类别枚举、可选材质和输入记忆后由用户确认成功。除非后续出现真实问题，本阶段不再继续优化模型修改区。
- 当前审查环境没有 .NET SDK、Windows、Revit 和 Autodesk 导出器，不能复现 Windows 编译或模型测试。
- Revit 2021–2023、2026–2027 尚未完成本轮编译；Revit 2025 已完成 3.1.1 编译但未实机启动，2027 还需要可用的 .NET 10 SDK。
- 项目原压缩包只有 2022–2024 旧 DLL，已清理，不能作为七版本兼容证据。
- 3.1.13 已由 DeepSeek 在全新目录完成 Revit 2024/2025 编译，两个版本均为 0 error；Revit 2024 已干净部署并启动，Journal 注册 28 个 Ribbon 按钮，CS1626 已闭环。
- 3.1.15 修复沿线布柱未按路线方向旋转的问题，并增加旋转偏移角度；Ribbon 仍为 29 个。该版本必须由 DeepSeek **分别编译并部署 Revit 2024 和 Revit 2025**，不得只部署 2024。
- 项目管理四功能的 Excel 大批量导入、级联删除、族类别重载和跨文档保护仍属于高风险路径，只能在测试项目或副本中验收；本轮没有把静态审查写成实机通过。
- 3.1.16 的“云链接”仅接受本地、网络盘或 Desktop Connector 已同步到本机的 RVT/DWG/DXF 路径；尚未实现通过 ACC 项目/模型 GUID 直接浏览或链接云模型。
- 批处理对外部工作共享模型采用内存分离打开并关闭不保存；导出设置、三维视图和视图/图纸集按名称匹配，外部模型缺少同名项时会报错，不会静默退回当前视图。

## 2026-08-07｜3.1.35 批处理与项目管理二次复核

- 批处理重复链接识别新增统一路径匹配器，覆盖本地/网络/Desktop Connector 与 `Autodesk Docs://` 显示路径；不得退化为仅按文件名判断。
- 合并 PDF 覆盖必须先导出临时文件，成功后再替换；失败必须保留或恢复原文件。
- 多文档视图、视图集、DWG/PDF 设置继续严格按名称匹配；缺失时明确失败，禁止静默回退当前视图。
- 族实例、族类型、项目资产、图纸 Excel 导入采用逐行原子事务，最终合并为一次撤销；单行失败不得回滚已成功行，也不得静默吞掉。
- 布尔融合失败且存在交集时禁止输出重叠实体，必须回滚。
- 当前仍为条件 GO：本环境无 Windows/.NET/Revit，ACC、PDF 文件替换、布尔异常几何和四个管理窗口均待 Revit 2024 实机。
## 2026-09-20｜GR01 V1 GUI Static Implemented
- GR01 V1 Core Runtime 已由 ChatGPT 判定 GO；本轮只开发中文 WPF，不修改 Core 算法。
- 新增 Ribbon `钢筋工具区 -> 创建钢筋 -> 渐变钢筋集`，仍使用既有 Pulldown；Ribbon 总命令 44，钢筋一级入口仍为 4 组。
- 新增 modeless `RebarGradientSetWindow`：起始/结束控制筋、ModelLine/Edge 路径、Zone DataGrid、普通/加密快捷区段、Preview/Create。
- GUI 使用 Document-bound ExternalEvent 且 `wrapInTransactionGroup:false`；Preview/Create 全部调用已验收 `RebarGradientSetService`，窗口不复制 CreateFreeForm/Spacing/Transaction。
- PlanFingerprint 在控制筋、路径、区段任何变化时清空，成功 Create 后也要求重新 Preview。
- 静态：GR01 V1 81/81；GUI 44/44；全量 Test-*.py 33 suites/0 failure；UI 201 C#/43 XAML/44 Ribbon/0 error/0 warning；MCP py_compile PASS。
- 当前：`GR01 V1 GUI STATIC GO / REVIT 2025 GUI RUNTIME REQUIRED / RELEASE NO-GO`。


## GR01 V1.0.1 UX runtime follow-up (2026-09-20)
- Accepted MCP wiring fix: `ExecuteGradientRebarSet` now maps `AutoLength`.
- ZAxis Preview/AutoLength semantics have Revit 2025 runtime evidence.
- ZAxis Create is still runtime NOT CLOSED due native `RebarCantBeBent` on the first synthetic vertical-U fixture; targeted transverse-control retest required.

## 2026-09-22｜GR01 V1.0.2 UI/UX FIX1
- Integrated the Windows/Revit compile-only CS0165 fix in `RebarGradientSetWindow.RefreshActionButtons`: initialize `reason` before short-circuit evaluation and pass `out reason`. No business behavior change.
- Strengthened `Test-RebarGR01V102UI.py` for the compile fix, max-spacing live guard, and Start/End swap wiring. Focused result: 40/40 PASS.
- Full local static rerun: all `tools/Test-*.py` 35 suites / 0 failure.
- Independent evidence review reclassifies mouse-wheel handoff as runtime PASS: screenshots show the outer content visibly moves from both body-hover and DataGrid-hover wheel events; the reported user bug is closed.
- Remaining max-spacing=0 / over-allocation edit capture and Rebar A→B / ModelLine display captures are treated as non-blocking UI proof gaps; implementation/static contracts are present and underlying pick/core paths were already accepted.
- No spacing planner, interpolation, CreateFreeForm, transaction, MCP core, or Rebar geometry behavior changed.


## Architecture slimming direction (2026-09-23)

YangTools remains a single Revit add-in assembly. The project will be slimmed as a **feature-sliced modular monolith**, not rewritten or split into many DLLs.

Migration contract:

`Command / WPF / MCP adapter -> Request -> Service -> Result`

Product Revit writes and transactions belong in services/compatibility layers. UI and MCP are adapters. Existing Runtime-GO behavior is preserved and migrated in small source-SHA-bound batches.

Current priorities are documented in `docs/ARCHITECTURE_SLIMMING_AUDIT_20260923.md`.
