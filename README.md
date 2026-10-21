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

## RC2S｜模型修改区布尔几何：差集错误不再静默保留未切实体、Void 和缺失几何拒绝、删除族前核对其他实例、族独立编辑/项目事务分离、删除与回读确认、窗口选取与危险选项说明。仅源码静态验收，4.0.0 RELEASE NO-GO。

## RC2R｜实体提取 SolidGenerator Bug/UI：增加源/局部/项目实例 XYZ 外包范围与总体积校验，修复 ProjectElevation/显式标高失效回退/材质写入读回/回滚状态/窗口长 ID。静态通过不等于 Windows/Revit Runtime GO。

## 2026-10-10｜YangTools 4.0.0 RC2Q Loft 世界高度 / Bug/UI 保护

以 RC2P canonical 为源码基线。RC2Q 修正无效目标标高默认回退、轮廓重复/跨文档、源几何与局部 Loft 世界 Z 边界未校验以及提交前实例几何高度未核对；新增 UI 来源高度说明、长 ID tooltip、回滚未知状态。工作区与 ZIP 静态套件及专项结果以独立报告为准，**未执行 Windows C# 编译或 Revit 实机建模**，4.0.0 Release NO-GO，3.1.36 为稳定版。下一 RC2R 实体提取。

## 2026-10-10｜YangTools 4.0.0 RC2P 基于面转换 / Bug 与 UI 加固

以 RC2O 完整源码为基线。已审查 FaceBasedConverterWindow/Engine，修正部分实例失败仍提交、几何/参数复制静默丢失、危险删除旧实例、近似朝向分组、BBox 误定位与共享临时文件误删；修正全选/关闭按钮/最小窗口尺寸/危险删除提示。严格删除模式可能拒绝无法保留全部参数的任务；不能声称支持全部类型。源码测试与 Windows/Revit 结果以 RC2P Acceptance 为准；正式稳定版本 3.1.36 FINAL_E2E，4.0.0 Release NO-GO。下一批 RC2Q Loft。

## YangTools 4.0.0 RC2O 最新开发候选（2026-10-10）
模型修改区“标高修改”全量预检与单事务原子改标高、ProjectElevation 保持世界 Z、复杂宿主族拒绝不安全重建、改后回读与 UI 失败分离。详见 docs/YANGTOOLS_4_0_0_RC2O_LEVEL_MODIFIER_20261010.md。仅 SOURCE STATIC，尚无 Revit 实机结果，不代表发布。

---

## 最新开发 RC2N｜线性布置（模型修改区优先）
本批修复线性布置路径和起终点参数边界、固定数量/间距异常、事务与回读、WPF 排队状态；新 `LinearPlacementRequest` 预留未来 MCP 调用入口。RC2N 仅静态候选，不能当作正式 Revit 发布。

## 2026-10-10｜YangTools 4.0.0 RC2M 沿线布柱 UI 请求/事务保护（STATIC ONLY）

从 RC2L canonical SHA `4BB68A47AF1E11B4F151BFFAD6C0B1D2D2B36F64F659BDD4091D05BF7856554A` 继续，先优化模型修改区。沿线布柱点击快照、旧 ExternalEvent/关闭防护、非法输入与 10000 根上限、单 Transaction/回滚状态/提交后回读与 UI 错误分离。新增 `ColumnAlongPathPlacementRequest` / `Result`，保留原几何算法、中文 XAML 与 Rebar/MCP/Ribbon。详细计划：`docs/YANGTOOLS_4_0_0_OPTIMIZATION_ROADMAP_MODEL_FIRST_20261010.md`，专题 `docs/YANGTOOLS_4_0_0_RC2M_COLUMN_ALONG_PATH_20261010.md`。Windows Build / Revit GUI / Undo 未测；4.0 RELEASE NO-GO，稳定 3.1.36。

---


## 2026-10-10｜YangTools 4.0.0 RC2L FamilyInstance 批量换型原子事务 / UI
从 RC2K 唯一开发源码推进。新增 `FamilyInstanceBatchTypeService` typed Request/Result/Replacement，替代逐实例 SubTransaction 部分提交；ChangeTypeId 可替换新图元 ID，记录并回读校验；WPF 保留中文入口，添加窗口/筛选/Tab/行/目标/重复请求保护。源代码、静态测试与 ZIP 门禁见 `docs/YANGTOOLS_4_0_0_RC2L_FAMILY_INSTANCE_BATCH_TYPE_20261010.md`。Windows Build、Revit Loaded/GUI/Undo 均 NOT_TESTED，4.0.0 RELEASE NO-GO。

## 4.0.0 RC2K｜族实例批量参数安全

唯一最新开发候选 RC2K（基线 RC2J SHA `C8A32105F1C66390A4DE936EDA01F1B7C25DBD301B53671CFF06FF7B94D8F9B4`）；FamilyInstanceManager 批量写参数已拆 `FamilyInstanceBatchParameterService`，改为全目标预检+单 Transaction/失败整批回滚，no-op 不产生 Undo；UI 关闭/Tab/筛选/选择/后发请求与提交后刷新错误保护。保留现有 `BatchChangeType` 旧逐行提交路径（待办）、MCP/Rebar/Ribbon/XAML/csproj 不变。工作区/fresh ZIP 测试与 SHA 见验收报告，Windows/Revit Runtime NOT_TESTED、4.0 RELEASE NO-GO，稳定版 3.1.36。详情见 `docs/YANGTOOLS_4_0_0_RC2K_FAMILY_INSTANCE_BATCH_PARAMETER_20261010.md`。

---

# 4.0.0 RC2J｜族实例逐格参数编辑（SOURCE STATIC GO）

从 RC2I 继续，仅将 FamilyInstanceManagerWindow.InstanceDataGrid_CellEditEnding / SystemDataGrid_CellEditEnding 的参数写入移入 FamilyInstanceCellEditService typed Request/Result；保留显式绑定、原 UI，加入文档/身份/只读检查、单 Transaction、提交/回滚/模型回读分离，以及 Tab/筛选/窗口关闭/行对象/同格最新请求校验。保留 MCP 27 命令、Rebar/Ribbon、43 个 XAML、csproj。工作区与全新解压 49/49 suites、RC2J 专项 94/94 PASS。Windows Build / Revit GUI / Undo 未测：4.0.0 RELEASE NO-GO，3.1.36 FINAL_E2E 稳定。详细 docs/YANGTOOLS_4_0_0_RC2J_FAMILY_INSTANCE_CELL_EDIT_20261008.md。

---

# 4.0.0 RC2I 当前开发源（2026-10-08）

本版由 RC2H 继续：族实例管理器只读类型选项抽到 typed Service，`RequestRead` 增加窗口关闭/Tab/筛选过期回调检查；保持所有 XAML、MCP、Rebar、Ribbon 及写入事务路径不变。详情 `docs/YANGTOOLS_4_0_0_RC2I_FAMILY_INSTANCE_READ_UI_20261008.md`。仅静态候选；4.0.0 Release NO-GO。

---

## 最新开发输入｜4.0.0 RC2H（静态候选，非发行版）
线图案 ComboBox 现在必须在 Revit 事务提交且回读确认后更新 ViewModel；详细风险、验收边界及 ProjectAssetManager 收口清单见 `docs/YANGTOOLS_4_0_0_RC2H_LINE_PATTERN_CLOSEOUT_20261008.md`。Windows/Revit 运行仍未测试，3.1.36 FINAL_E2E 继续作为稳定版。

## 2026-10-08｜YangTools 4.0.0 RC2G LineColor UI/Service（SOURCE STATIC GO）

从 RC2F 正式候选出发，只将 ProjectAssetManager 线样式颜色对话框移出 ExternalEvent，把写入移到 ProjectAssetLineColorService typed Request/Result；支持负值类别 ID、颜色无变化 no-op、单 Transaction/回滚、原文档/标签/关闭/行身份守卫、已提交但刷新失败分离。旧线图案、填充区域、其他 UI 功能均未重构；43 XAML、MCP、Rebar、Ribbon、csproj 均未修改。新增 RC2G 71 项源码合同；旧 RC2F 的固定套件数测试改为下限校验。工作树 + 全新解压各 46/46 静态 suites PASS；原生 C# Build/Revit 颜色点击/Undo 仍 NOT_TESTED，4.0.0 RELEASE NO-GO。详细 docs/YANGTOOLS_4_0_0_RC2G_LINE_COLOR_UI_SERVICE_20261008.md；下轮 RC2H 线图案选择闭环后进行资产管理器收口审计，避免无限拆分。

---


## 2026-10-08｜YangTools 4.0.0 RC2F 线样式名称/线宽编辑（SOURCE STATIC GO）

基线 RC2E 不变；本轮只重构 `ProjectAssetManagerWindow.LineStyleDataGrid_CellEditEnding` 的名称和线宽到 `ProjectAssetLineStyleEditService`。新增 typed Request/Result/Kind、文档有效性/只读/类别 ID 预检、内置样式重命名保护、名称与线宽约束、单 Transaction 和 RollBack、no-op 无 Undo；ExternalEvent 仅在原 Tab 和仍在列表中的行有效时执行，UI 不提前提交 ViewModel，更改成功但 UI 刷新失败单独提示。原 43 XAML、MCP、钢筋、Ribbon、工程目标框架没有修改；填充区域/颜色/图案路径未重构。原工作树 + 独立重新解压均 45/45 suites PASS；专项 59/59 PASS。由于缺 Windows/.NET/Revit 执行环境，Build/Loaded/点击/Undo NOT_TESTED，`4.0.0 RELEASE NO-GO`；正式稳定版仍 3.1.36。源码详细说明见 `docs/YANGTOOLS_4_0_0_RC2F_LINE_STYLE_EDIT_UI_20261008.md`。下一项 RC2G：剩余颜色/图案/填充区域 UI 最小安全闭环。

---

## 4.0.0 RC2E｜项目资产逐格编辑结构调整

新增 `ProjectAssetCellEditService`：项目资产名称、普通参数、项目信息参数按一格一事务执行；WPF 改显式数据绑定提交，直到 Revit 写入确认才更新表格；过期 UI 回调不写旧页面。未修改现有 XAML / MCP / 钢筋 / Ribbon。静态检查不等于 Windows/Revit GUI 实测。

# YangTools 4.0.0 RC2D｜项目资产删除业务安全拆分（2026-10-08）

接 RC2C，本批仅限 `ProjectAssetManagerWindow` 的删除勾选节点/删除选中资产。新增 `Core/ProjectAssets/ProjectAssetDeleteService.cs` typed Request/Preview/Result：ExternalEvent 中无提交的删除预演必须回滚，显示级联警告、默认 No；用户确认后单 Transaction 正式删除，删除集合与预演不一致则回滚，避免无意扩大删除范围。UI 捕捉 Tab/Tree/关闭状态，清理实际连带删除 ID 而非仅选择 ID，分离“已提交、界面刷新失败”与“事务失败”。线样式类别负 ID 允许走 Revit 预演以判断支持情况。**未调整逐格编辑、XAML、MCP、钢筋或工程版本。** 全部为源码静态候选：Windows 编译/真实 Revit Delete/Undo/UI 未执行，4.0.0 RELEASE NO-GO。

---

# YangTools 4.0.0 RC2C｜资产 Excel 原子导入与 UI 安全（2026-10-08）

基于 RC2B，将 ProjectAssetManager.Excel 导入从 UI 下沉到 ProjectAssetExcelImportService 的 Request/Service/Result；不修改 XAML、MCP、Rebar、Ribbon。异常时回滚事务组而不再局部提交；零实际修改不创建 Undo；缺失/只读参数明确失败，非法 ID 行计数并提示，重复 ID 拒绝；过期/关闭窗口队列不写模型，成功提交但刷新 UI 出错必须明确区分。静态专项 64 个源码合同点；Windows/.NET/Revit 未实测，4.0.0 RELEASE NO-GO。

---

# YangTools 4.0.0 RC2B｜项目资产批量重命名 Service（2026-10-08）

当前批次仅静态候选。将 ProjectAssetManager 的批量重命名写操作下沉至 ProjectAssetBatchRenameService，采用预检 + 单 Transaction + 异常回滚；修复单图元失败被吞、局部提交却显示成功的问题。UI 捕获所选元素和参数，ExternalEvent 中调用 Service；标签/节点切换或窗口关闭后不刷旧页面。XAML、MCP、Rebar/Ribbon、其他写操作保持不变。新专项 Test-ProjectAssetRC2B 源码合同断言；仍需 Windows 编译与 Revit GUI/Undo 实机验收，4.0.0 RELEASE NO-GO。

---

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

# 2026-09-18｜Rebar R10 Coupler

> 当前正式稳定版仍为 **3.1.36 CAD→Revit FINAL_E2E**。3.1.38 Rebar 分支当前进入 **R10 Coupler STATIC GO / WINDOWS+REVIT RUNTIME REQUIRED / RELEASE NO-GO**。

R10 仅使用 Revit 原生 `RebarCoupler.Create`：支持 Two-Bar Coupler 与 Single-End Cap；新增 `list_rebar_coupler_types` / `create_rebar_coupler` Typed MCP 和 `Coupler R10` Ribbon。dryRun 不开启 Transaction，Apply 单事务、失败回滚、精确读回 Coupler 与 Rebar 端点关系。Splice/Lap 不在 R10，留给 R11。

详细：`docs/R10_REBAR_COUPLER_DESIGN_20260918.md`、`docs/STATIC_CHANGE_REPORT_3.1.38_REBAR_R10_20260918.md`。

## 2026-09-11｜Reliability Hardening R2 源码复审补丁

在首版 F01–F05 静态候选上进行独立源码复审，发现两类不能带入 Windows 终验的残留，并已做小范围修复：

- F03：`FamilyManagerWindow` 仍有 6 处直接 `Commit()` 后继续成功流程；`LevelModifierCommand` / `DimTextOverrideCommand` 调用公共提交 helper 后仍无条件返回 `Result.Succeeded`。R2 已统一要求 `TransactionStatus.Committed`，未提交即失败，不再把 UI/命令返回值当模型真值。
- F05：`WriteProductOperation` 文档声称支持 `OperationId/BatchRunId`，实际 payload 只有 `OperationId`；Batch 又把 runId 直接当 operationId。R2 新增独立 `BatchRunId` 字段，并为一次 Batch 用户动作分别生成 `operationId` 与 `batchRunId`，所有 Batch 产品日志显式携带二者。
- `Test-ReliabilityHardening.py` 从 38 项扩展到 **43/43 PASS**；全量 `tools/Test-*.py` 仍为 **19/19 suites PASS**；UI static 153 C# / 42 XAML / 31 Ribbon / 0 error / 0 warning。
- 当前环境仍无 `dotnet/csc/mcs`，因此状态仍是 **SOURCE STATIC PASS / WINDOWS BUILD+FAULT-HARNESS+REVIT RUNTIME REQUIRED / RELEASE NO-GO**。
- 未混入 UI 七分组、MCP SDK v2、全功能 MCP 或安装器发布模式改造。

详细复审：`docs/RELIABILITY_HARDENING_R2_SOURCE_REVIEW_20260911.md`。

# 2026-09-11｜3.1.38 Reliability Hardening（F01–F05）

> Drive 审查 28/29/31 将 3.1.38 判定从“只缺实机”改为 **SOURCE REVIEW NO-GO**。本轮只修可靠性阻塞，不混入大 UI/Ribbon/MCP 全量迁移。

- F01：新增 `BatchFileCommitService`；提交失败时逐项恢复旧输出，恢复失败保留备份目录与明确路径，不再无条件删除最后备份。
- F02：执行按钮与预检按钮共用 `RunFullExportPreflight(BatchRunRequest)`，全批路径冲突必须在任何输出前阻止。
- F03：Transaction/TransactionGroup 仅 `Committed` 成功；Family 参数单元格写入检查 Commit 状态并从模型 readback。
- F04：新增不可变运行快照 `BatchRunRequest/BatchRunOptions`，引擎不再读取活 WPF ViewModel；运行期锁定输入区。
- F05：产品自然执行日志增加 OperationId/BatchRunId、PID、Loaded DLL SHA、Document、事务/ElementId/输出路径；Batch 记录真实模态链，不伪造 ExternalEvent。
- Windows 故障注入：`tools/BatchFileCommitFaultHarness` 直接链接真实文件提交服务，在临时目录注入 staged move / restore failure。
- 静态：`Test-ReliabilityHardening.py = 38/38 PASS`；全量 `19/19` suites PASS；UI static 153 C# / 42 XAML / 31 Ribbon / 0 error / 0 warning。
- 本环境无 C# 编译器，当前只能判 **RELIABILITY FIX STATIC PASS / WINDOWS BUILD+RUNTIME REQUIRED / RELEASE NO-GO**。
- 长期架构按审查 31：保留成熟业务，渐进改为按功能组织的模块化单体；GUI/MCP 共用 Request/Result/Service，不开展全项目重写。

详细：`docs/RELIABILITY_HARDENING_3.1.38_20260911.md`。

# YangTools Revit

> **2026-09-02 版权/签名更新**：3.1.38 当前源码加入作者/版权/版本元数据、关于页许可声明、`LICENSE.md / NOTICE.md / THIRD_PARTY_NOTICES.md`，并建立 Authenticode 签名脚本。源码包不包含任何私钥/PFX。公开可信签名仍需作者自己的受信任代码签名证书；自签名仅作为本地/内部签名。当前 3.1.38 仍未升 FINAL，剩余门禁是双模型 Batch、真实 UI 截图与产品链日志。

> **2026-09-02 当前状态**：3.1.36 `CAD→Revit FINAL_E2E` 仍是正式运行基线。当前统一开发候选为 **3.1.38**，包含 `PasteCAD Shared + Family Management V2.1 + Batch Publisher V2`。全部静态门禁已通过，但尚未完成 Windows/Revit 当前 DLL 统一实机验收，因此状态为 **STATIC GO / WINDOWS REVIT UNIFIED RETEST REQUIRED / RELEASE NO-GO**。

## 2026-09-02｜3.1.38 Batch Publisher V2：批处理与云链接重做

- “批处理与云链接”从旧的当前文档设置复用器重做为**多模型 / 云模型批量发布器**。目标：同一批任务可靠输出 `NWC / IFC / DWG / PDF 导出 / PDF 打印`，并在真正写文件前逐模型预检。
- 目标模型支持本地 RVT、网络/同步 RVT、当前已打开模型，以及当前会话已打开的 Autodesk Docs/BIM 360 云模型。云模型保存 Region/Project GUID/Model GUID，可用 `ConvertCloudGUIDsToCloudPath` 重新打开；本轮不伪造 ACC 项目浏览器。
- 跨模型资源不再只读当前模型：命名 3D 视图、ViewSheetSet、DWG/PDF/Print 设置均按**每个目标模型**重新匹配；ComboBox 可编辑，便于输入统一公司设置名；预检提前列出缺失模型。
- NWC：支持整个模型/自动3D/当前3D/指定3D，内部/共享坐标，RVT Links、元素属性、ElementId、房间属性/几何、按标高拆分；先检查 `IsNavisworksExporterAvailable`。
- IFC：支持整个模型/自动3D/指定3D、IFC 版本、基础数量、空间边界、墙柱按标高拆分；先检查 `IsIFCAvailable`；导出时在 Transaction 中执行。
- DWG：支持全部图纸/全部可打印视图/当前视图/指定 ViewSheetSet；每个视图/图纸独立调用 Export，主 DWG 文件名由 YangTools 精确控制；设置按目标模型匹配；支持坐标覆盖和 MergedViews；检查 `IsDWGExportAvailable`。
- PDF 导出：仅 Revit 2022+。分开导出时每张图用 `Combine=true` 单独调用，从而绕开 `Combine=false` 下 Revit NamingRule 对文件名的接管；合并模式生成单文件；Revit 2025+强制 `SetExportInBackground(false)`，避免后台导出导致批任务进入下一模型时文件尚未落盘。
- PDF 打印：Revit 2021+。使用 `PrintManager + PrintRange.Select + InSession ViewSet + PrintToFileName + SubmitPrint`；支持精确命名和合并/分开；等待实际文件落盘；打印后尽力恢复用户 PrintManager 状态。若驱动强制“另存为”则不能宣称无人值守 PASS。
- 文件命名统一支持 `{模型名} / {模型文件名} / {图纸号} / {图纸名} / {视图名} / {序号} / {日期}`；默认 NWC/IFC=`{模型名}`，DWG/PDF/打印=`{模型名}-{图纸号}-{图纸名}`。
- `每模型独立子目录` 现在严格尊重用户勾选，不再在多模型时强制开启。
- 增加**全批次输出规划**：预检阶段就检测同批次重复最终路径、目标路径被目录占用、Overwrite=false 时已有文件；即使允许覆盖旧文件，也禁止本批新输出互相覆盖。
- NWC/IFC/DWG 使用 staging→验证非空→备份旧输出→commit；DWG 会验证预期主 `.dwg` 且同时提交伴随文件/XRef；PDF/打印用临时文件成功后安全替换，失败时不提前破坏旧成果。
- UI：1320×860，Min1120×720；模型列表 Row30/Header34；左侧链接区 360px；右侧发布设置 Tab；长路径/设置不靠压窄列解决，保持滚动与 MinWidth。
- 专项静态：`Test-BatchPublisherV2.py = 99/99 PASS`；旧 `Test-BatchPrintPdf.py = 32/32 PASS`。
- 详细说明：`docs/BATCH_PUBLISHER_V2_3.1.38_20260902.md`。

## 2026-09-02｜3.1.38 Family Management V2.1

- 族文档管理新增 Family/Type 使用统计、未使用过滤、安全清理未使用类型、管理实例跳转、多族批量 RFA、刷新统计。
- 族实例管理新增 Scope（全项目/当前视图/当前选择）、Type/Level 过滤、Level/Host/Pinned 固定列、批量换型、批量同参数写入/仅填空值、动态系统类别。
- 两个窗口统一引入 StableParameterKey：Shared GUID / BIP / ParameterId 优先，名称仅 fallback；重复同名参数无法唯一定位时拒绝写入。
- UI 尺寸进入硬契约：族文档 1280×780 Min1080×650；族实例 1360×800 Min1160×660；Row 30 / Header 36；固定列和动态列均有 Width/Min/Max；缩小窗口用横向滚动而不是把列压成单字或按钮换行。
- 批量写入继续使用 Document-bound ExternalEvent + Transaction + per-item SubTransaction；成功批次目标一次 Ctrl+Z。
- 静态：Family V2.1 110/110；Batch Publisher V2 99/99；全 Test-*.py 17/17；UI 31 Ribbon、0 error、0 warning、151 C# / 42 XAML。
- 详细说明：`docs/FAMILY_MANAGEMENT_V2_1_3.1.38_20260902.md`。

## 2026-09-02｜3.1.37 从 CAD 粘贴：新增“对齐共享坐标”

- `导入工具区 → 从 CAD 粘贴` 的定位方式由 2 种扩展为 3 种：`原点到原点 / 中心到中心 / 对齐共享坐标`。
- `对齐共享坐标` 直接使用 Revit `ImportPlacement.Shared`；YangTools **不会**自动获取/发布共享坐标，也不会在失败后静默回退到“原点到原点”。
- UI 新增定位说明：共享坐标模式要求当前 Revit 项目已建立有效共享坐标，并且 CAD 坐标基准与项目共享坐标一致。
- `UserSettings.PasteCadPlacementIndex` 持久化范围由 `0..1` 扩展为 `0..2`。
- 导入链保持原安全边界：工作副本 → 单 Transaction → `Document.Import` → 失败 RollBack → finally 清理临时文件 → 成功后选中新 ImportInstance → 一次 Ctrl+Z。
- `DWGImportOptions` 改为显式释放；诊断日志增加 Unit / Color / Placement / ThisViewOnly；共享坐标失败显示专门原因。
- 新增 `tools/Test-PasteCad.py`：**19/19 PASS**；全量 `tools/Test-*.py`：**15/15 suites PASS**；Ribbon 仍为 **31**，UI 静态 **0 error / 0 warning**。
- 详细说明：`docs/PASTE_CAD_SHARED_COORDINATES_3.1.37_20260902.md`。
- 当前状态：**STATIC GO / WINDOWS REVIT RETEST REQUIRED**。

## 2026-09-02｜族文档管理 / 族实例管理 V2 源码审查

- 本轮不直接扩大族管理产品逻辑，只完成源码级能力盘点与下一步方案，避免在两个 1200+ 行 WPF code-behind 上盲目加按钮。
- 当前“族文档管理”实质更接近“载入族 + 族类型表格管理器”：已有类型参数、重命名、复制类型、类别修改、Excel、RFA 导出、删除，但缺少使用统计、未使用类型清理、批量 RFA、实例跳转和真正 Family Document 参数/公式/嵌套族检查。
- 当前“族实例管理”实质更接近“实例参数表格管理器”：已有参数编辑、Excel、选择/定位、安全删除，但缺少明确 Scope、批量换类型、批量写参数、Level/Host/Pinned 等状态列和稳定 ParameterKey。
- V2.1 优先建议：族使用统计 + 未使用清理 + 管理实例跳转 + 批量 RFA；实例侧增加 Scope / Type / Level 过滤、批量换类型、批量写参数、固定状态列；同时把 `LookupParameter(name)` 主路径升级为稳定参数身份。
- 不做大型 MVVM 重写，优先抽 `FamilyUsageService / FamilyTypeEditService / FamilyFileService` 等小服务降低回归面。
- 详细审查：`docs/FAMILY_MANAGEMENT_V2_REVIEW_3.1.37_20260902.md`。

## 2026-09-01｜3.1.36 新增“CAD 转 Revit 图纸”

- 新增 `导入工具区 → CAD转Revit图纸`，Ribbon 由 30 增至 **31**；版本提升到 **3.1.36**。
- 功能定位：读取 YangCAD 预处理/RVT Exchange JSON，把二维 CAD 图纸按用户映射重建为 Revit 原生详图对象；不做复杂 CAD 标准同步。
- 实体解析：支持 YangCAD 官方 `DxfType` 字段（LINE、LWPOLYLINE、ARC、CIRCLE、TEXT、MTEXT、MULTILEADER、HATCH、DIMENSION），并向下兼容 `type` / `kind` / `entity_type` / `object_type` 等别名。
- V1 处理四类映射：`Text/MText/Leader → TextNote + Leader`、二维曲线 → `DetailCurve`、Hatch → `FilledRegion`、Dimension → 专用参考 `DetailCurve` + 真正 `Dimension`。
- 映射 UI 按 CAD 类型聚合显示数量，右侧可选 `使用现有类型 / 复制现有类型 / 自动新建 / CAD炸开参照（备用） / 跳过`。炸开产生的乱码类型仅保留为低优先级备用来源，永不自动选中；`跳过` 是合法结果。
- 文字不根据 CAD `H100` 等命名猜 Revit 文字高度；例如 `RVT-T H100 romans.shx → 2.5mm` 必须通过 Revit 类型映射决定。复制/新建文字类型以现有 Revit `TextNoteType` 为模板。
- YangCAD 已显式归一化的线宽/线型可用于自动创建线样式，例如 `#0.25 Continuous White → #0.25 Continuous`；也可直接映射/复制当前项目已有 LineStyle。
- CAD 尺寸界线不与普通结构/详图线混用：默认创建/使用专用线样式 `YangTools-尺寸界线`，先生成参考 DetailCurve，再创建真正 Revit Dimension；单个标注失败时其参考线同一子事务回滚。
- 映射只按 `Category/类型名/目标名` 保存，不保存 ElementId；全局配置：`%APPDATA%\YangTools\CadToRevit\Mapping.json`。
- 整次转换使用 `TransactionGroup`，成功对象最终合并为一次 Ctrl+Z；单对象用 `SubTransaction` 隔离，零成功整批回滚。
- 新窗口使用现有 `ThemeHelper`，适配暖光米白、深色现代、专业蓝灰、苹果水滴四套主题；新增独立 16/32 px Ribbon 图标。
- 实机验证：已在 Windows / Revit 2024 (live MCP / API) 完成实体坐标门禁（5组几何误差 0.000 mm <= 1.0 mm）、Dimension 生成与回滚、事务回滚与映射持久化实测。
- 当前状态：**CAD_TO_REVIT LIVE VERIFIED / DxfType COMPATIBLE / TEST EXECUTION COMPLETE**。

## 2026-08-21 线性布置 P0 真实 Z 门禁加固

- 当前 Windows 复验候选源码：`YangTools_Project_3.1.35_Source_SIMPLE_MATERIAL_P0_Z_GATE_20260821.zip`；版本仍为 3.1.35，不新增 Ribbon 功能。
- 架构体检结论：当前不做大型 MVVM/分层重构。大型 WPF code-behind 是后续可维护性债务，但在当前 Windows/Revit 闭环前拆分会扩大回归面。
- 修正 `FamilyPlacementHelper.CreateAtLevelRelativeElevation` 的候选 Offset 判定：不再仅用 `ProjectElevation + Offset.AsDouble()` 自算结果，而是每次写入候选参数后直接读取真实 `LocationPoint.Z`。
- 只有 `|LocationPoint.Z - 理论 WorldZ| <= 1 mm` 才接受该 BuiltInParameter；无效候选会恢复原 Offset 后再尝试下一个，恢复失败则立即抛错并由外层事务回滚。
- 最终诊断日志固定记录 `FamilyPlacementType / LevelId / Level.Elevation / Level.ProjectElevation / BuiltInParameter / Offset.AsDouble() / 理论 WorldZ / LocationPoint.Z / 误差(mm)`。
- `tools/Test-LinearElevationProfile.py` 已升级为 37/37 静态契约 + 20/20 数值组合；但这仍不能替代用户原问题模型的 Revit 实机 PASS。
- 当前状态继续统一为 **STATIC GO / WINDOWS RETEST REQUIRED**；线性布置 P0 失败时停止后续材质专项，先修真实问题。
- 当前 `installer/payload` 不是本候选源码的正式分享产物；只有用户明确要求分享并完成当前 DLL 实机 PASS 后才重新生成。

## 2026-08-21 新增“绑定材质（简易）”并修复批量版参数名称 UI

- 输入交接源码基线：`YangTools_Project_3.1.35_Source_SIMPLE_MATERIAL_20260821.zip`。
- 保留原批处理能力并改名为 `项目管理区 → 批量绑定材质`：按 Family 列表多选，适合一次处理大量族。
- 新增日常入口 `项目管理区 → 绑定材质`：优先读取当前预选的 `FamilyInstance`；没有预选时进入 Revit 多选实例。小窗口只保留参数名、类型/实例、项目材质和覆盖已有绑定四项。
- 简易版默认使用**实例参数**；实例模式只给所选实例赋材质。类型参数模式给所选实例对应的 `FamilySymbol` 赋材质，并明确提示同类型其它实例会一起变化。
- `赋予材质` 下拉来自当前项目现有 `Material`，支持输入搜索，但不自动创建新项目材质。
- 简易版复用批量版 `EditFamily → 创建/复用 Material FamilyParameter → 绑定几何 → LoadFamily` 主链；回载后再把选定 `Material.Id` 写入实例或类型参数。族绑定与材质赋值由外层 `TransactionGroup` 合并，目标是一次 Ctrl+Z 撤销。
- 若一个目标都无法成功赋值，会回滚整次简易操作，避免只留下空材质参数。逐实例/类型赋值使用 `SubTransaction` 隔离并汇总失败原因。
- 修复截图中的批量版“参数名称”输入框：参数名称和参数方式拆成两行，输入框 Height=34、Padding=8,2、垂直居中，不再被底部说明区挤压。
- Ribbon 当前为 **30**：新增简易版“绑定材质”，原入口改名“批量绑定材质”。
- 当前静态复跑：文本 324、C# 141、XAML 41、窗口/视图根节点 36、主题 4、Ribbon 30；UI 0 错误、0 警告；简易绑定材质 31/31、批量绑定材质 31/31，既有专项全量复跑通过。
- MCP SDK v2 / MCP 2026-07-28 **只记录，不在本轮迁移**；当前 Python bridge 继续 `mcp>=1.2.0,<2`。
- 当前源码产品 C#/XAML 已变化，之前 MATERIAL_BINDING/FINAL3 DLL 均不能作为当前构建证据。必须由 DeepSeek/Codex 在 Windows 重新独立编译、部署和 Revit 实测。
- 当前状态：**源码/静态 GO；Windows/Revit RETEST REQUIRED；正式发布 NO-GO**。

## 2026-08-14 新增“绑定材质”；MCP SDK v2 仅记录

- 新增 `项目管理区 → 绑定材质`：选择一个或多个可编辑载入族，输入材质参数名称，选择类型参数/实例参数，把族内所有具有 `Material` 参数且可关联的几何元素统一关联到该族参数，然后自动回载当前项目。
- 默认参数名为 `材质`；支持“覆盖已有的其它材质参数绑定”。同名参数若不是 Material，或类型/实例方式与本次选择冲突，该族安全失败，禁止自动替换旧参数。
- Revit 2022+ 使用 `GroupTypeId.Materials + SpecTypeId.Reference.Material` 创建材质参数；Revit 2021 使用 `PG_MATERIALS + ParameterType.Material` 兼容路径。
- 工具只建立族参数与几何 Material 属性的关联，不自动指定具体项目材质；没有可绑定几何的族不会创建无用参数。
- 回载后复核 Family/FamilySymbol/FamilyInstance ElementId、既有正 ID 自定义参数内部值，并确认目标材质参数已出现在类型或已有实例。数据完整性异常触发整批回滚。
- 回载使用 `IFamilyLoadOptions` 且 `overwriteParameterValues=false`，避免为了新增材质参数而主动覆盖项目中现有族参数值。
- Ribbon 当前为 **29**：上一轮 28 个入口基础上新增“绑定材质”。详细说明：[docs/FAMILY_MATERIAL_BINDING_3.1.35_20260814.md](docs/FAMILY_MATERIAL_BINDING_3.1.35_20260814.md)。
- 用户决定 MCP SDK v2 **仅记录，不在本轮迁移**；`McpServer/requirements.txt` 继续锁定 `mcp>=1.2.0,<2`。评估记录继续维护在 [docs/MCP_2026_07_28_SDK_V2_EVALUATION_20260814.md](docs/MCP_2026_07_28_SDK_V2_EVALUATION_20260814.md)。
- 最终静态复跑：文本 316、C# 138、XAML 40、窗口/视图根节点 35、主题 4、Ribbon 29；UI 0 错误、0 警告；绑定材质专项 31/31，既有 Review2 54/54、链接 6/6、文本宽度 15/15、Loft/实体提取 16/16、打印 PDF 32/32、中文检查 67/67、线性高程 31/31 + 20/20、参数清理模拟均通过。
- 本轮修改产品 C#/XAML，上一 AssistantCleanup/REMOVE_AI DLL 不能作为当前源码构建证据；需 DeepSeek/Codex 重新编译部署并做 Revit 2024/2025 专项。

## 2026-08-14 上一轮：删除 AI/摸鱼助手并新增“族类别修改”

- AI 助手/摸鱼助手已从运行源码中整体删除：Ribbon 命令、DockablePane 注册、聊天 UI、网络客户端、模式存储、历史管理、助手图标和相关运行代码均不再编译；MCP 网关与 Revit 主线程执行器继续独立保留。
- 当时 Ribbon 总数保持 28：删除“AI 助手”，新增 `项目管理区 → 族类别修改`。当前已由“绑定材质”提升为 29。
- “族类别修改”用于批量修改**可编辑载入族的 Family Category**，例如 `Abutments → Generic Models`；不是修改族类型名称。
- 窗口支持搜索、逐族勾选、全选/反选/全不选，并显示当前类别、类型数、实例数、可编辑状态和 ElementId；目标类别默认优先“常规模型/Generic Models”。
- 执行链路固定为 `Document.EditFamily → 族文档 Transaction → OwnerFamily.FamilyCategory → LoadFamily 回载 → 类别/ID/自定义参数值校验`。系统族、内建族、不可编辑族自动跳过；回载前的单族失败可继续，其它族不受影响。
- 项目侧使用 `TransactionGroup` 聚合成功回载；回载后会核验 Family/FamilySymbol/FamilyInstance ElementId 与正 ID 自定义参数内部值。一旦发现回载后异常，整批回滚，禁止静默丢值。
- 用户已明确要求彻底清理已删除助手的旧数据：启动、安装/更新、直接部署和卸载都会删除 `%APPDATA%\YangTools\Assistant\`，并清理旧 `copilot_settings.json` / `copilot_history.json` / `moyu_settings.json` / `moyu_history.json`；不会删除仍在使用的 MCP、主题或其它 YangTools 配置。族类别说明：[docs/FAMILY_CATEGORY_CHANGE_3.1.35_20260814.md](docs/FAMILY_CATEGORY_CHANGE_3.1.35_20260814.md)；助手数据清理：[docs/ASSISTANT_DATA_CLEANUP_3.1.35_20260814.md](docs/ASSISTANT_DATA_CLEANUP_3.1.35_20260814.md)。
- 当前源码已变化，上一轮 DLL 不能作为本轮编译证据；需 DeepSeek/Codex 重新编译部署。

## 2026-08-13 线性布置 level-based 偏移参数修复（第二次实机错误）

- 用户复验仍出现固定 Z 差值：期望 29.527559 ft，实际 18.044619 ft，差 11.48294 ft。
- 根因修正：上一版虽然改对了 `ProjectElevation`，但仍通过 `ElementTransformUtils.MoveElement` 尝试修正 level-based 族的 Z。Autodesk Revit API 明确规定 level-based 元素不能用 MoveElement 改变 Z，应通过实例偏移参数控制。
- 线性布置现改为专用 `FamilyPlacementHelper.CreateAtLevelRelativeElevation`：先按所选 Level 创建实例，再显式写 `INSTANCE_ELEVATION_PARAM` / `INSTANCE_FREE_HOST_OFFSET_PARAM` / `FAMILY_BASE_LEVEL_OFFSET_PARAM` 中首个可写 Double 参数。
- 世界高程契约仍为 `Level.ProjectElevation + 相对高程`；XY 与 LevelId 单独校验。TwoLevelsBased/CurveDrivenStructural 明确安全失败并提示使用专用工具。
- 该历史轮次源码基线为 `YangTools_Project_3.1.35_Source_LINEAR_LEVEL_OFFSET_FIX_20260813.zip`，需 Windows 重新编译并在用户原问题模型复验。

## 2026-08-13 线性布置共享/项目高程基准修复

- 用户实机错误：期望 Z=`29.527559 ft`（约 9000 mm），实际 Z=`18.044619 ft`（约 5500 mm），差值 `11.48294 ft`（约 3500 mm）。
- 根因：上一轮世界 Z 使用 `Level.Elevation + 相对高程`。`Level.Elevation` 会受 Level 的 Elevation Base（Project/Shared）影响；当标高按共享坐标显示时，它不是 Revit XYZ 所需的项目原点坐标高程。
- 修复：世界 Z 改为 `Level.ProjectElevation + 相对高程`。`ProjectElevation` 始终相对项目原点，与 Elevation Base 无关。
- UI 同时显示“标高项目高程”和（两者不一致时）“显示高程”，避免把共享显示值再次叠加进世界坐标。
- 新增真实错误回归：`显示高程=3500 mm、项目高程=0 mm、相对高程=5500 mm` 时，正确世界 Z 必须为 `5500 mm`，旧错误公式会得到 `9000 mm`。
- `Test-LinearElevationProfile.py` 更新为 26/26 源码契约 + 20/20 非零标高数学组合，并加入上述 3500 mm 差值回归。
- 修复说明：[docs/LINEAR_PLACEMENT_PROJECT_ELEVATION_FIX_3.1.35_20260813.md](docs/LINEAR_PLACEMENT_PROJECT_ELEVATION_FIX_3.1.35_20260813.md)；本机复验任务：[docs/DEEPSEEK_CODEX_LINEAR_PROJECT_ELEVATION_ACCEPTANCE_3.1.35_20260813.md](docs/DEEPSEEK_CODEX_LINEAR_PROJECT_ELEVATION_ACCEPTANCE_3.1.35_20260813.md)。
- 本轮产品源码已变化，必须重新编译部署；当前状态：**静态 GO、Windows/Revit 条件 GO、正式发布 NO-GO**。

## 2026-08-12 线性布置增加标高与首尾高程

- `模型修改区 → 线性布置` 新增“基准标高”选择框，以及“起点高程 / 终点高程（mm）”两个输入框。
- 高程输入是**相对所选标高的偏移高程**。世界 XYZ 使用 `Level.ProjectElevation + 相对高程`；禁止用受 Elevation Base（项目/共享）影响的 `Level.Elevation` 参与世界坐标计算。
- 实际生成的第一个轮廓实例精确对应起点高程，最后一个轮廓实例精确对应终点高程；中间实例按沿有效路线的实际位置比例线性插值。
- `反转路线首尾` 开启后，起/终点高程跟随反转后的有效路线方向；固定间距未补终点时，仍以实际首尾两个已生成实例作为高程插值端点。
- 继续使用 `FamilyPlacementHelper.CreateAtWorldPoint`：实例关联所选标高，同时校正并验证实际世界坐标，避免再次出现“标高不是 0 时重复加高/少加高”的问题。
- `KeepVertical=false` 时，实例俯仰方向改为跟随新的线性高程坡度；`KeepVertical=true` 仍保持 Z 轴垂直。
- 新增 `tools/Test-LinearElevationProfile.py`：当前静态契约 25/25、非零标高数学组合 20/20。详细说明：[docs/LINEAR_PLACEMENT_ELEVATION_3.1.35_20260812.md](docs/LINEAR_PLACEMENT_ELEVATION_3.1.35_20260812.md)；本机任务：[docs/DEEPSEEK_CODEX_LINEAR_ELEVATION_ACCEPTANCE_3.1.35_20260812.md](docs/DEEPSEEK_CODEX_LINEAR_ELEVATION_ACCEPTANCE_3.1.35_20260812.md)。
- 本轮修改产品源码，上一 CHINESE_CLEANUP / FINAL3 DLL 不能作为当前源码构建证据；当前为 **静态 GO、Windows/Revit 条件 GO、正式发布 NO-GO**。


## 2026-08-07 中文检查升级为“中文检查与清理”

- 原四块只读 ListBox 改为“左侧分类导航 + 右侧统一 DataGrid”；族、族类型、系统类型、材质、组、装配、视图/模板、图纸、明细表/列标题、修订、项目标准、参数、文字、房间/空间/面积、链接与输出设置分别成组。
- 实例参数扫描从原来的仅 `FamilyInstance` 扩展到具有类别的全部非类型元素；字符串值全面检查，参数名称排除负 ID 的 Revit 内置本地化名称，降低假阳性。
- 每个分类支持搜索、逐行勾选、全选、全不选、反选；双击“名称 / 内容”对可编辑对象执行真实 Revit 重命名/文本修改。
- 支持 `Element.Name`、`TextNote.Text`、字符串参数、工作集、图号、明细表列标题、空间编号、装配类型名等对应写入；不可改名对象明确标记。
- “删除 / 清空所选”区分实体删除与字符串参数清空；实体删除先在回滚事务中预检 Revit 级联影响，再二次确认。批量实际写入使用一个主事务 + 逐项 `SubTransaction`，单项失败隔离，成功批次可一次 Ctrl+Z 撤销。
- 新增 `tools/Test-ChineseCleanup.py`，当前 67/67；详细说明：[docs/CHINESE_CHECK_CLEANUP_3.1.35_20260807.md](docs/CHINESE_CHECK_CLEANUP_3.1.35_20260807.md)；本机任务：[docs/DEEPSEEK_CODEX_CHINESE_CHECK_ACCEPTANCE_3.1.35_20260807.md](docs/DEEPSEEK_CODEX_CHINESE_CHECK_ACCEPTANCE_3.1.35_20260807.md)。
- 本轮修改产品源码，BATCH_PRINT_PDF/FINAL3 的旧 DLL 均不能作为当前源码构建证据；当前为 **静态 GO、Windows/Revit 条件 GO、正式发布 NO-GO**。

## 2026-08-07 批处理新增“打印 PDF”

- 在现有“导出 PDF（Revit 2022+）”之外新增“打印 PDF（虚拟 PDF 打印机，Revit 2021+）”，使用 `PrintManager`，因此 Revit 2021 也可进入打印路径。
- UI 可选择 PDF 打印机、Revit 打印设置、视图/图纸集和 PDF 文件名；打印机自动发现 Windows 中名称包含 `PDF` 的驱动，也允许手动输入。
- 执行固定 `PrintRange.Select`，使用 In-Session ViewSheetSet，不保存/修改命名视图集；多文档缺同名打印设置时明确失败，不自动回退。
- 输出先写 YangTools 临时 PDF，再安全替换目标文件；打印失败不会先删除旧 PDF。打印结束后尽力恢复原打印机、打印范围、打印设置和视图/图纸集状态。
- 注意：部分 PDF 驱动，尤其 `Microsoft Print to PDF`，在 Revit API 下可能仍弹出“另存为”，因此“可打印”不等于“可无人值守批量自动保存”；必须由 Windows/Revit 实机按驱动分类。
- 新增专项静态检查 `tools/Test-BatchPrintPdf.py`，当前 32/32；详细说明：[docs/BATCH_PRINT_PDF_3.1.35_20260807.md](docs/BATCH_PRINT_PDF_3.1.35_20260807.md)；本机任务：[docs/DEEPSEEK_CODEX_BATCH_PRINT_PDF_ACCEPTANCE_3.1.35_20260807.md](docs/DEEPSEEK_CODEX_BATCH_PRINT_PDF_ACCEPTANCE_3.1.35_20260807.md)。
- 本轮修改了产品 C#/XAML，之前 FINAL3 的 DLL/编译哈希不能作为当前源码编译证据；当前状态回到 **静态 GO、Windows/Revit 条件 GO、正式发布 NO-GO**。

## 2026-08-07 FINAL3 交付闭环修正

- FINAL3 仅修正文档、证据打包和静态检查范围；`src/YangTools.Revit` 产品源码与 FINAL2 完全一致，不要求重新编译 DLL。
- 删除源码包中残留的旧版 `artifacts/evidence.zip`，避免与 FINAL3 独立证据包混淆。
- `Test-UiPanelsStatic.py` 不再把 Revit Journal/运行日志当作 UTF-8 源码；最终源码包复跑为 0 错误、0 警告。
- 上一已编译基线：`YangTools_Project_3.1.35_Source_REVIEW2_FINAL3_20260807.zip`；当前新增“打印 PDF”后的源码需重新编译。
- 当前独立证据包：`YangTools_3.1.35_Review2_Evidence_FINAL3_20260807.zip`。

## 2026-08-07 Windows 独立编译与 Revit 2024 实机复验

- 全新空目录解压 `Source_REVIEW2_20260807`，完成源码基线/静态检查与 2021–2026 编译（2021–2024 0 错误；2025–2026 编译通过但 Revit 未装，标记未完成；2027 缺 .NET 10 SDK 未构建）。
- 修复 2 个编译错误（`ParameterDeleteService` CS0136、`FamilyManagerWindow.LoadFamily` CS0029）与 2 个工具脚本编码问题（补 UTF-8 BOM）。
- Revit 2024 实机（MCP 驱动真实 3.1.35）：批处理链接跳过/同名目录不误判/失败续跑、合并 PDF 覆盖与锁定保护、缺同名视图/设置明确报错、线性布置世界 Z、标高修改无重复实例、布尔 Union 失败安全门等通过。
- 判定：**条件 GO**；正式发布 NO-GO（Revit 2025–2027 实机、GUI 驱动项、真实 ACC 集成待补齐）。
- 上一 FINAL3 编译/实机证据已归档在独立 `YangTools_3.1.35_Review2_Evidence_FINAL3_20260807.zip`；本轮源码包不再内嵌旧 DLL、build/evidence，避免误把上一基线证据当成本轮“打印 PDF”的构建证据。

## 2026-08-07 批处理、模型修改区与项目管理区二次复核

- 对 2026-08-03 检查报告按最新源码重新逐项核对；已修项不重复改，误报/设计限制单独标记。
- 新增 Desktop Connector 本地路径与 `Autodesk Docs://` 显示路径的安全等价匹配，要求文件名及两级父目录一致；仍需真实 ACC 项目验证。
- 合并 PDF 覆盖改为“临时导出 → 旧文件备份 → 成功替换 → 失败恢复”，导出失败不再先删除旧文件。
- 布尔融合失败时检查交集；相交或无法安全判断时整次回滚，避免输出重叠实体。
- 族实例、族类型、项目资产和图纸 Excel 导入改为逐行原子事务并显示失败明细；普通删除统一确认；图框快速换型增加并发守卫。
- 多文档导出仍严格按名称匹配，不自动回退当前视图，避免无提示导出错误内容。
- 复核说明：[docs/REVIEW_BATCH_MODEL_PROJECT_3.1.35_20260807.md](docs/REVIEW_BATCH_MODEL_PROJECT_3.1.35_20260807.md)；Windows/Revit 任务：[docs/DEEPSEEK_CODEX_REVIEW2_ACCEPTANCE_3.1.35_20260807.md](docs/DEEPSEEK_CODEX_REVIEW2_ACCEPTANCE_3.1.35_20260807.md)。


## 2026-08-07 文本基准对齐增加段落宽度同步

- “文本工具区 → 基准对齐”新增勾选框“段落宽度相同（复制源文字框宽度）”，默认不勾选。
- 勾选后逐个执行 `目标 TextNote.Width = 源 TextNote.Width`；不复制文字内容、文字类型、字号或格式，目标文本可能重新换行。
- 允许只勾选段落宽度，不必同时移动 X/Y 或旋转。
- 设置前按目标文字类型检查 Revit 允许的最小/最大宽度；无法精确设置时回滚该目标，不影响其它目标，并在完成提示中列出部分失败原因。
- 每个目标继续使用独立 `SubTransaction`，整个命令仍可一次 `Ctrl+Z` 撤销。
- 详细说明：[docs/ALIGN_TEXT_WIDTH_3.1.35_20260807.md](docs/ALIGN_TEXT_WIDTH_3.1.35_20260807.md)；专项静态检查：`tools/Test-AlignTextWidth.py`；Windows/Revit 验收任务：[docs/DEEPSEEK_CODEX_ALIGN_TEXT_WIDTH_ACCEPTANCE_3.1.35_20260807.md](docs/DEEPSEEK_CODEX_ALIGN_TEXT_WIDTH_ACCEPTANCE_3.1.35_20260807.md)。

## 2026-08-05 检查报告复核与项目资产表头修复

- 复核 `REVIEW_批处理与云链接_2026-08-03.md`，区分真实缺陷、设计限制、待实机项和误报；详见 [docs/REVIEW_AUDIT_FIX_3.1.35_20260805.md](docs/REVIEW_AUDIT_FIX_3.1.35_20260805.md)。
- 修复项目资产管理器“视图过滤”表头被压缩：三个状态列改为 120/120/150 px，并设置 110/110/135 px 最小宽度；动态参数列和“+”列使用固定最小宽度。
- 同步修复族实例/族文档/项目资产参数绑定名截断、选择过滤残留、项目资产导入导出表头、Project Info 错误操作、操作栏重叠、图案滚轮误写、图纸窗口初始化时序和重复列头。
- 模型修改区补齐线性布置、基于面转换、标高修改、布尔几何的事务回滚；线性布置与基于面转换统一保持原世界坐标。
- 批处理补充 Commit/Assimilate 状态清理；Revit 2021 编译下禁用 PDF 入口。ACC 云链接重复识别和 PDF 旧文件恢复仍需真实环境证据后再改。
- 本轮专项源码契约 `37/37` 通过；UI 静态检查仍为 0 错误、0 警告。当前为 **条件 GO，可进入 Windows 编译与 Revit 2024 专项复验；不可发布**。
- 本机任务：[docs/DEEPSEEK_CODEX_REVIEW_FIX_ACCEPTANCE_3.1.35_20260805.md](docs/DEEPSEEK_CODEX_REVIEW_FIX_ACCEPTANCE_3.1.35_20260805.md)。

Yang 个人使用的 Revit 效率插件，支持 Revit 2021–2027 按年份独立编译。

AI 或开发者接手前必须完整阅读：

1. [PROJECT.md](PROJECT.md)：项目介绍、硬要求、开发规范、功能清单和当前状态。
2. [WORKLOG.md](WORKLOG.md)：最近实际变更、未验证项和下一步。
3. [MicroTool_Developer_Guide V3.md](MicroTool_Developer_Guide%20V3.md)：临时插件箱的真实加载方式、事务规则和项目结束后的收藏/转正流程。
   - 注：链接中的 `%20` 为 URL 编码空格，实际文件为源码根目录 `MicroTool_Developer_Guide V3.md`（GitHub/Wiki 渲染器自动解码；本地直接查看请用字面空格文件名）。
4. [docs/ICON_DESIGN_GUIDE.md](docs/ICON_DESIGN_GUIDE.md)：Ribbon 图标的统一线条风、配色、尺寸、命名和新增流程。

构建与安装命令均记录在 `PROJECT.md`。不要从旧对话或旧压缩包猜测项目状态。


## 2026-07-30 Loft / 实体提取目标标高修复

- 用户实机确认实体生成（Loft）和实体提取在选择目标标高后出现世界高度偏差。
- 根因是旧代码先按 `targetLevel.Elevation` 放置，再从多个 BuiltInParameter 中猜测一个偏移参数写入；不同族模板并不能保证第一个可写参数就是实际定位参数。
- 新增 `FamilyPlacementHelper`：直接按原几何世界坐标锚点创建实例，目标标高只负责关联；Revit 再生后读取 `LocationPoint`，必要时移动校正并再次验证，误差超过 `1e-6 ft` 即抛错回滚。
- Loft 使用“首轮廓首点 XY + 所有轮廓最低 Z”作为锚点；实体提取使用全部 Solid 联合包围框中心。两个功能均删除旧 `SetInstanceOffset` 猜测逻辑。
- 窗口文字明确为“关联标高，保持原几何高度”，避免把目标标高误解为主动移动几何。
- 同步修复可静态确认的 WPF/Revit 类型歧义和 Revit 2021 参数类型读取风险。
- 本环境静态检查通过；Windows 编译与 Revit 2024 三标高坐标专项仍需按 [docs/DEEPSEEK_CODEX_GENERATOR_LEVEL_ACCEPTANCE_3.1.35_20260730.md](docs/DEEPSEEK_CODEX_GENERATOR_LEVEL_ACCEPTANCE_3.1.35_20260730.md) 执行。当前仍为 **条件 GO，可编译复验；不可发布**。
- 最终静态复跑：257 个文本文件、145 个 C#、40 个 XAML、35 个窗口/视图根节点、4 套主题、28 个 Ribbon；错误 0、警告 0。

详细技术说明见 [docs/GENERATOR_LEVEL_PLACEMENT_FIX_3.1.35_20260730.md](docs/GENERATOR_LEVEL_PLACEMENT_FIX_3.1.35_20260730.md)。

## 2026-07-29 源码核验与 Windows 编译准备

- 本轮未新增功能，未修改 C#、XAML、主题、图标或业务算法；只更新核验报告、Windows 执行任务和项目状态文档。
- 已复核 3.1.35 的七个 Revit Configuration、目标框架、x64 输出隔离、Revit API 包引用、Addin 清单、构建/部署脚本和用户级安装路径。
- 已重新执行 UI/编码静态检查和重复参数清理链路模拟：251 个文本文件、144 个 C#、40 个 XAML、35 个窗口/视图根节点、4 套主题、28 个 Ribbon 命令；错误 0、警告 0。
- 当前环境无 Windows/.NET/Revit，不能提供真实编译或实机通过结论。当前状态为 **条件 GO：可交给 Windows 编译与验收，不可发布**。
- 完整核验见 [docs/SOURCE_VERIFICATION_3.1.35_20260729.md](docs/SOURCE_VERIFICATION_3.1.35_20260729.md)；本机任务见 [docs/DEEPSEEK_CODEX_WINDOWS_TASK_3.1.35_20260729.md](docs/DEEPSEEK_CODEX_WINDOWS_TASK_3.1.35_20260729.md)。



## 3.1.35 临时插件箱、文本工具、中文检查与面板设置审查

- 临时插件箱新增通用“功能说明”窗口；每个临时 DLL 读取 `<DLL名>.microtool.json`，列表显示功能说明、使用步骤、适用 Revit 版本、是否修改模型和撤销说明。缺少完整说明的工具禁止运行和收藏，可一键创建标准模板。
- `MicroTool_Developer_Guide V3.md` 内容升级为 V3.1，新增说明清单规范、Revit 2021–2027 独立编译、Revit 主线程、事务、性能、依赖和发布前检查要求；构建时复制到输出目录 `Docs`。
- “等距分布”按 `TextNote.Coord` 插入点工作，界面改成六个明确模式；“基准对齐”明确 X=同一竖线、Y=同一水平线、旋转=围绕目标插入点，并可选同步源文本的 `TextNote.Width`。两个窗口均新增可复制功能说明。
- 中文检查在当时曾收敛为只读检查；当前 2026-08-07 已进一步升级为“中文检查与清理”：分类表格、勾选/全选/反选、双击安全重命名、删除级联预检与参数文本清空。详见本页顶部最新状态。
- 系统设置的面板设置不再在关闭窗口时静默保存；只有点击“保存面板设置”才写入。系统设置固定显示；禁止把某面板全部移入展开区；支持当前面板全选、当前/全部恢复默认；配置以完整快照原子写入并保留备份。
- Revit 主窗口 Owner 统一改用 `UIApplication.MainWindowHandle`，不再依赖当前进程主窗口句柄。
- 当前仅完成 28 个 Ribbon 入口、40 个 XAML、主题、事件、资源和 UTF-8 静态检查。全功能逐版本 Revit 实机兼容尚未完成，不能把“源码可按 2021–2027 编译”表述为“所有功能已经实机兼容”。

详见 [docs/FEATURE_AUDIT_3.1.35.md](docs/FEATURE_AUDIT_3.1.35.md)。

## 重复项目参数清理（3.1.35）

- Ribbon 入口：`YangTools → 检查工具区 → 重复项目参数清理`。
- DeepSeek/MCP 已在三个 Revit 2021 项目中完成 606/573/135 个重复组，删除 1770/3019/179 个来源参数；其中一个项目迁移 21891 个参数值，总耗时约 46–50 秒。该实机成果证明“单次值索引 + 内部值复制 + 直接 Delete”主线可行。
- 3.1.35 将清理前快照与参数值索引合并为实例/类型各一次扫描。快速处理直接复用索引，只访问真正存在来源值的元素，不再为值迁移额外全量扫描模型。
- 每个相关元素的参数集合只读取一次并按真实 ParameterElement Id 映射；共享参数可按 GUID、普通项目参数可按 Definition 进行严格身份回退，回退结果仍须匹配 ParameterElement Id，禁止 `LookupParameter(name)`。
- keeper 为空时复制第一个来源内部值；后续来源只能判定为相同或冲突，不会覆盖不同值。Double/Integer/String/ElementId 继续使用内部值直接 `Set`，`AsValueString()` 只用于报告。
- 删除继续采用 3.1.33 的直接批量 `Document.Delete` 和删除前依赖允许集合，不增加对当前项目无效的 `BindingMap.Remove`。未预期级联对象仍会回滚并二分隔离。
- 不采用报告中建议的 `Task.Run` 访问 Revit 模型。Revit API 继续在有效主线程上下文执行；窗口在快照每 5000 元素、绑定每 100 组、值迁移每 1000 相关元素、删除每 250 来源和复核每 5000 条时刷新日志，避免长时间无反馈。
- 若参数身份重取失败达到 2000 次且占已处理操作至少一半，值事务会提前回滚并停止，不再产生数万条相同错误或继续无效运行。
- 报告新增 `10_运行诊断.csv`，记录值索引、身份回退、提前停止、Delete API、事务、二分、回滚及依赖对象统计。
- 数据完整性和唯一性硬复核继续保留；任何原值丢失或声称完整删除的组仍不唯一，整个 TransactionGroup 回滚。

详细说明见 [docs/DUPLICATE_PROJECT_PARAMETER_CLEANUP_3.1.35.md](docs/DUPLICATE_PROJECT_PARAMETER_CLEANUP_3.1.35.md)。

## 使用源码包前

每个版本必须解压到**全新的空目录**，禁止覆盖旧版本目录。覆盖解压不会删除已经移除的 `.cs` / `.xaml` 文件，可能把废弃 Ribbon 命令重新编译进 DLL。

解压后先双击 `核验源码.cmd` 或 `Verify-SourceBaseline.cmd`，核验通过后再编译。
## 安装与卸载

正式安装包生成后，优先双击 `YangTools安装器.cmd`：

- 中文图形界面。
- 自动识别 Revit 2021–2027。
- 可同时安装、更新或卸载多个年份。
- 安装到当前用户 `%APPDATA%\Autodesk\Revit\Addins\{年份}`，不需要管理员权限。
- 每个年份仍使用各自独立编译 DLL，绝不跨年份替代。
- 某个年份缺少编译产物时只跳过该年份，不再阻断其他可用年份。

在源码目录中，DeepSeek/Codex 已完成编译后可直接双击 `installer\YangTools安装器.cmd`。安装器会依次识别：

1. 源码模式下优先使用当前 `src\YangTools.Revit\bin\x64\{年份}` 编译输出。
2. 正式安装包使用 `installer\payload\{年份}`。
3. 最后回退到 `artifacts\payload\{年份}`。

因此只编译 Revit 2024 时，不需要先补齐 Revit 2021–2023。需要生成部分年份正式安装包时执行：

```powershell
.\tools\Package-Installer.ps1 -SkipBuild -Versions 2024
```

只有发布前明确要求七个年份全部存在时，才使用 `-RequireAll`。

源码阶段可双击 `静态检查面板与编码.cmd` 检查 XAML、主题资源、事件处理器和 UTF-8；3.1.27 起还会拦截跨资源字典的危险 `StaticResource`；当前同时检查 AI/摸鱼助手运行文件不得回流，以及“族类别修改”批量 EditFamily/FamilyCategory/LoadFamily 主链。该检查不替代 Revit 实机逐窗口验收。
## 2026-09-20｜GR01 V1 GUI Static Implemented
- GR01 V1 Core Runtime 已由 ChatGPT 判定 GO；本轮只开发中文 WPF，不修改 Core 算法。
- 新增 Ribbon `钢筋工具区 -> 创建钢筋 -> 渐变钢筋集`，仍使用既有 Pulldown；Ribbon 总命令 44，钢筋一级入口仍为 4 组。
- 新增 modeless `RebarGradientSetWindow`：起始/结束控制筋、ModelLine/Edge 路径、Zone DataGrid、普通/加密快捷区段、Preview/Create。
- GUI 使用 Document-bound ExternalEvent 且 `wrapInTransactionGroup:false`；Preview/Create 全部调用已验收 `RebarGradientSetService`，窗口不复制 CreateFreeForm/Spacing/Transaction。
- PlanFingerprint 在控制筋、路径、区段任何变化时清空，成功 Create 后也要求重新 Preview。
- 静态：GR01 V1 81/81；GUI 44/44；全量 Test-*.py 33 suites/0 failure；UI 201 C#/43 XAML/44 Ribbon/0 error/0 warning；MCP py_compile PASS。
- 当前：`GR01 V1 GUI STATIC GO / REVIT 2025 GUI RUNTIME REQUIRED / RELEASE NO-GO`。


### GR01 V1.0.1 UX current status (2026-09-20)
ZAxis/AutoRemaining Preview is runtime proven on Revit 2025.4. A missing MCP `AutoLength` mapping found during runtime has been integrated. ZAxis Create remains targeted-retest-required after a synthetic fixture returned native `RebarCantBeBent`; do not treat that single fixture as proof of a product geometry defect.

## 2026-09-22｜GR01 V1.0.2 UI/UX FIX1
- Integrated the Windows/Revit compile-only CS0165 fix in `RebarGradientSetWindow.RefreshActionButtons`: initialize `reason` before short-circuit evaluation and pass `out reason`. No business behavior change.
- Strengthened `Test-RebarGR01V102UI.py` for the compile fix, max-spacing live guard, and Start/End swap wiring. Focused result: 40/40 PASS.
- Full local static rerun: all `tools/Test-*.py` 35 suites / 0 failure.
- Independent evidence review reclassifies mouse-wheel handoff as runtime PASS: screenshots show the outer content visibly moves from both body-hover and DataGrid-hover wheel events; the reported user bug is closed.
- Remaining max-spacing=0 / over-allocation edit capture and Rebar A→B / ModelLine display captures are treated as non-blocking UI proof gaps; implementation/static contracts are present and underlying pick/core paths were already accepted.
- No spacing planner, interpolation, CreateFreeForm, transaction, MCP core, or Rebar geometry behavior changed.


## Current engineering direction — 2026-09-23

After the Rebar V1.0.4 feature gate, development has moved to project-level release/architecture cleanup. No feature rewrite is planned. See `docs/ACTIVE_DOCS_INDEX.md` and `docs/ARCHITECTURE_SLIMMING_AUDIT_20260923.md` for the current path.
