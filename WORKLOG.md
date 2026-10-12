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

## 2026-10-10 RC2S｜从 RC2R SHA 304C1D49426C29E0FDF845E88D6A8EF2E28BADFC118100556864C2657AFF219D 继续；修布尔差集/融合的异常吞噬、原形体及刀具删除校验、Void/空实体拒绝、删除族保护、编辑族与项目事务分离、组合回滚判定、主体几何回读及 UI 提示。Windows/Revit/Undo NOT_TESTED，不推 Git。

## 2026-10-10 RC2R｜实体提取：世界坐标边界/体积读回、标高 ProjectElevation、材料成功校验、事务组回滚不确定反馈、UI 长 ID/失效标高。基于 RC2Q canonical，静态测试和 ZIP 证据另附；Windows/Revit/Undo NOT_TESTED，下一 RC2S 布尔几何。

## 2026-10-10｜YangTools 4.0.0 RC2Q Loft 世界高度 / Bug/UI 保护

以 RC2P canonical 为源码基线。RC2Q 修正无效目标标高默认回退、轮廓重复/跨文档、源几何与局部 Loft 世界 Z 边界未校验以及提交前实例几何高度未核对；新增 UI 来源高度说明、长 ID tooltip、回滚未知状态。工作区与 ZIP 静态套件及专项结果以独立报告为准，**未执行 Windows C# 编译或 Revit 实机建模**，4.0.0 Release NO-GO，3.1.36 为稳定版。下一 RC2R 实体提取。

## 2026-10-10｜YangTools 4.0.0 RC2P 基于面转换 / Bug 与 UI 加固

以 RC2O 完整源码为基线。已审查 FaceBasedConverterWindow/Engine，修正部分实例失败仍提交、几何/参数复制静默丢失、危险删除旧实例、近似朝向分组、BBox 误定位与共享临时文件误删；修正全选/关闭按钮/最小窗口尺寸/危险删除提示。严格删除模式可能拒绝无法保留全部参数的任务；不能声称支持全部类型。源码测试与 Windows/Revit 结果以 RC2P Acceptance 为准；正式稳定版本 3.1.36 FINAL_E2E，4.0.0 Release NO-GO。下一批 RC2Q Loft。

## 2026-10-10｜RC2O 标高修改（Model Modify）
ChatGPT 从 RC2N canonical SHA A0AA68AF5F415014F1687811047F9C7AFC888B826CF57B809B667B06B3A2952F 静态修改 LevelModifier；新增 typed Service/Request/Result，预检/单事务/失败整批回滚、项目世界高度、宿主与重建限制及提交后模型回读；适配 F03 测试路径。测试和最终 SHA/Acceptance 以独立验收报告为准。Windows/Revit Build/GUI/Undo NOT_TESTED，NO-GO，不推 Git。

---

## 2026-10-10 RC2N｜模型修改区线性布置
由 RC2M 继承；修正旧起终点颠倒静默返回及极小间距无上限，冻结 WPF 请求，绑定原文档，保留世界 Z 基准公式，完整连续路线预检；单事务回滚、已提交后位置/类型回读，UI 错误与模型提交分离；53 套并发 PASS，专项 136/136。Windows/Revit 未测，不推 Git，4.0 NO-GO。

## 2026-10-10｜YangTools 4.0.0 RC2M RC2M 沿线布柱与后续优化工作计划（STATIC ONLY）

从 RC2L canonical SHA `4BB68A47AF1E11B4F151BFFAD6C0B1D2D2B36F64F659BDD4091D05BF7856554A` 继续，先优化模型修改区。沿线布柱点击快照、旧 ExternalEvent/关闭防护、非法输入与 10000 根上限、单 Transaction/回滚状态/提交后回读与 UI 错误分离。新增 `ColumnAlongPathPlacementRequest` / `Result`，保留原几何算法、中文 XAML 与 Rebar/MCP/Ribbon。详细计划：`docs/YANGTOOLS_4_0_0_OPTIMIZATION_ROADMAP_MODEL_FIRST_20261010.md`，专题 `docs/YANGTOOLS_4_0_0_RC2M_COLUMN_ALONG_PATH_20261010.md`。Windows Build / Revit GUI / Undo 未测；4.0 RELEASE NO-GO，稳定 3.1.36。

---


## 2026-10-10｜YangTools 4.0.0 RC2L FamilyInstance 批量换型原子事务 / UI
从 RC2K 唯一开发源码推进。新增 `FamilyInstanceBatchTypeService` typed Request/Result/Replacement，替代逐实例 SubTransaction 部分提交；ChangeTypeId 可替换新图元 ID，记录并回读校验；WPF 保留中文入口，添加窗口/筛选/Tab/行/目标/重复请求保护。源代码、静态测试与 ZIP 门禁见 `docs/YANGTOOLS_4_0_0_RC2L_FAMILY_INSTANCE_BATCH_TYPE_20261010.md`。Windows Build、Revit Loaded/GUI/Undo 均 NOT_TESTED，4.0.0 RELEASE NO-GO。

## 2026-10-10｜YangTools RC2K 批量参数事务修复

唯一最新开发候选 RC2K（基线 RC2J SHA `C8A32105F1C66390A4DE936EDA01F1B7C25DBD301B53671CFF06FF7B94D8F9B4`）；FamilyInstanceManager 批量写参数已拆 `FamilyInstanceBatchParameterService`，改为全目标预检+单 Transaction/失败整批回滚，no-op 不产生 Undo；UI 关闭/Tab/筛选/选择/后发请求与提交后刷新错误保护。保留现有 `BatchChangeType` 旧逐行提交路径（待办）、MCP/Rebar/Ribbon/XAML/csproj 不变。工作区/fresh ZIP 测试与 SHA 见验收报告，Windows/Revit Runtime NOT_TESTED、4.0 RELEASE NO-GO，稳定版 3.1.36。详情见 `docs/YANGTOOLS_4_0_0_RC2K_FAMILY_INSTANCE_BATCH_PARAMETER_20261010.md`。

---

## 2026-10-08｜YangTools 4.0.0 RC2J FamilyInstance Cell Edit

从 RC2I 源码切入；将族实例与系统实例的单元格参数写入从 WPF 移入 FamilyInstanceCellEditService；Request/Result 冻结 ID/Key/新值。失败检查事务回滚，成功后回读异常不误报已回滚；WPF 加行、标签、筛选、同格请求版本门禁，保留旧值直到提交确认。更新 Test-ReliabilityHardening 与 Test-FamilyManagementV2 旧断言至新 Service；新增 Test-FamilyInstanceRC2J 94 源码合同；原 UI XAML 与 MCP/Rebar/Ribbon 未改。工作区 49/49 + fresh extract 49/49、专项 94/94；仅 SOURCE STATIC GO，Windows/Revit Runtime NO-GO。下一步审计批量参数写入风险。

---

# 2026-10-08｜4.0.0 RC2I FamilyInstanceManager 只读类型选项 / UI 时序

来源 RC2H；更新 FamilyInstanceManagerWindow.xaml.cs；新增 FamilyInstanceTypeOptionsService 与 RC2I 静态合同；旧 RC2H 测试 suite 数下限兼容；不修改写事务、XAML、MCP、Rebar、Ribbon、.csproj。目标保留旧全部测试并取得新鲜解压复测；无 Windows .NET/Revit 环境，不宣称 Runtime GO。不推 Git，Mimo 停用。

---

## 2026-10-08｜4.0.0 RC2H ProjectAssetManager 线图案与收口审查
基线 RC2G SHA 1F2A58AE06E04575EDAB8CE7F1A5FFC6068CA70F651AD5AD6F547B7978CFC6E5；RC2H 改线图案 ComboBox Explicit 绑定，新增 ProjectAssetLinePatternService typed Request/Result，事务回滚、读取失败单独提示，旧任务状态守卫；旧 RC2G 数量锁定测试改最小保留数量。未修改 MCP/Rebar/Ribbon/工程。填充区域与复制风险列 backlog，下一模块 FamilyInstanceManager。仅静态候选；Windows Build/Loaded/Revit UI/Undo NOT_TESTED，4.0.0 RELEASE NO-GO。

## 2026-10-08｜YangTools 4.0.0 RC2G LineColor UI/Service（SOURCE STATIC GO）

从 RC2F 正式候选出发，只将 ProjectAssetManager 线样式颜色对话框移出 ExternalEvent，把写入移到 ProjectAssetLineColorService typed Request/Result；支持负值类别 ID、颜色无变化 no-op、单 Transaction/回滚、原文档/标签/关闭/行身份守卫、已提交但刷新失败分离。旧线图案、填充区域、其他 UI 功能均未重构；43 XAML、MCP、Rebar、Ribbon、csproj 均未修改。新增 RC2G 71 项源码合同；旧 RC2F 的固定套件数测试改为下限校验。工作树 + 全新解压各 46/46 静态 suites PASS；原生 C# Build/Revit 颜色点击/Undo 仍 NOT_TESTED，4.0.0 RELEASE NO-GO。详细 docs/YANGTOOLS_4_0_0_RC2G_LINE_COLOR_UI_SERVICE_20261008.md；下轮 RC2H 线图案选择闭环后进行资产管理器收口审计，避免无限拆分。

---


## 2026-10-08｜YangTools 4.0.0 RC2F 线样式名称/线宽编辑（SOURCE STATIC GO）

基线 RC2E 不变；本轮只重构 `ProjectAssetManagerWindow.LineStyleDataGrid_CellEditEnding` 的名称和线宽到 `ProjectAssetLineStyleEditService`。新增 typed Request/Result/Kind、文档有效性/只读/类别 ID 预检、内置样式重命名保护、名称与线宽约束、单 Transaction 和 RollBack、no-op 无 Undo；ExternalEvent 仅在原 Tab 和仍在列表中的行有效时执行，UI 不提前提交 ViewModel，更改成功但 UI 刷新失败单独提示。原 43 XAML、MCP、钢筋、Ribbon、工程目标框架没有修改；填充区域/颜色/图案路径未重构。原工作树 + 独立重新解压均 45/45 suites PASS；专项 59/59 PASS。由于缺 Windows/.NET/Revit 执行环境，Build/Loaded/点击/Undo NOT_TESTED，`4.0.0 RELEASE NO-GO`；正式稳定版仍 3.1.36。源码详细说明见 `docs/YANGTOOLS_4_0_0_RC2F_LINE_STYLE_EDIT_UI_20261008.md`。下一项 RC2G：剩余颜色/图案/填充区域 UI 最小安全闭环。

---

## 2026-10-08｜YangTools 4.0.0 RC2E 逐格编辑 / UI 加固

基线 RC2D；审查并修复 ProjectAssetManager.AssetDataGrid_CellEditEnding：原匿名回调吞异常、参数缺失/只读静默返回、ProjectInfo 同名参数误匹配、旧 Tab/Tree 回调污染 VM 等。Service typed 三种单元格操作 + 单事务回滚，ProjectInfo 根据 Parameter.Id 校验，WPF 三编辑列 Explicit Binding，成功写入后仅刷新当前 VM；调整 RC2D 一条源文件位置检查，新增 RC2E 专项自动检查。Windows C#/Revit/GUI/Undo NOT_TESTED，4.0.0 Release NO-GO。

## 2026-10-08｜RC2D ProjectAsset Delete Request/Preview/Service/Result

输入 RC2C SHA 893B69EEEEB3E9BCB8EB4FE54BF4F8A36C0CE8A59CCFC51EF67DA00E97D6ABBC。仅将原 ProjectAssetManager 两条删除入口重构为共用 typed `ProjectAssetDeleteService`。预演在无提交 Transaction 中获取包含连带图元的 ID 并回滚，确认后单 Transaction 真实删除且核对完整集合；窗口关闭、Tab/Tree 切换时拒绝旧队列请求；树与三个列表按实际 deletedIds 移除；完成事务后 UI 异常报告“已提交、刷新失败”，防止重复删除。注意线样式类别负值 ID 不强制 GetElement 存在。Excel/批重命名 Service、AssetDataGrid 逐格编辑、XAML、MCP、Rebar、Ribbon 未改动。增加专项静态 Test-ProjectAssetRC2D，完整 43 套件（待最终 ZIP 复验）；Windows/.NET/Revit 实机仍 NOT_TESTED。未推送 Git。

## 2026-10-08｜RC2C ProjectAsset Excel Import Service

输入 4.0.0 RC2B SHA A874A166B5C5534053B48767856652C4220411BA9660072544A560B0022A1BEE。审查旧代码发现导入允许单行失败仍 Assimilate 已提交行、缺失参数静默跳过、UI 刷新失败被误当导入失败。新 ProjectAssetExcelImportService 使用 typed Request/Result、前置 ID/元素检查、逐行 Transaction + 外层 TransactionGroup；任何写入失败回滚整批，零变更组回滚，成功组合并一次 Undo。UI 先读取并复制 Excel 行，ExternalEvent 执行，关闭/Tab/Tree/重复导入请求守卫，区分读入失败/事务失败/刷新失败。同步更新 ReviewFixes 的已迁移断言，新专项 Test-ProjectAssetRC2C；不碰 XAML、MCP、Rebar、Ribbon。验收状态仅静态，禁止称 Windows/Revit Runtime PASS。

## 2026-10-08｜YangTools 4.0.0 RC2B ProjectAssetManager BatchRename

源输入：4.0.0 RC2A 20261008 ZIP（SHA 1BDA3A1A9D659A921882EE1D321319247768611F97B45EA73B0C932A27A38B00）。新增 Core/ProjectAssets/ProjectAssetBatchRenameService.cs (Request/Service/Result)、tools/Test-ProjectAssetRC2B.py、docs/RC2B。WPF BatchRename_Click 重定向 typed service; 预检缺失图元/空目标名，单 Transaction，任何名称设置/提交失败显式 rollback 并显示失败；成功/无变化分别展示撤销提示，UI 拦截关闭窗口/过期标签或节点刷新。不改 XAML/MCP/Rebar/旧写操作。新增源码合同与 UI 检查；最终测试 SHA 以 Acceptance 为准。Windows Build、Revit Loaded、实际 Ctrl+Z NOT_TESTED；4.0 RELEASE NO-GO。

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

## 2026-09-20｜GR01 V1.0.1 UX 优化：Z轴 + 自动剩余区
- 在已验收的 GR01 V1 Explicit ManagedFreeForm Core/GUI 上做小范围易用性优化，不扩算法边界、不做 Update/2027。
- 分布方式新增并默认 `ZAxis`：无需拾取路径，Core 使用首末控制筋 transformed centerline 几何中心的 Z 差自动计算有效长度；ModelLine/Edge 旧路径保持不变。
- Zone 新增 `AutoLength`：最多一个“自动剩余”区，实际长度=有效分布长度−其余固定区段；默认窗口只有一个普通自动区。增加固定普通/加密区后自动扣减；超配明确拒绝。
- GUI 新增“⇅ 交换首末”；自动区长度不可手工编辑；已知路径长度后实时显示路径/固定/自动剩余；ZAxis 下“拾取路径”禁用。
- Typed MCP 同步支持 `distribution_path={"kind":"ZAxis"}` 与 Zone `auto_length=true`，GUI/MCP 继续共用同一个 `RebarGradientSetService`。
- 原 `RebarGradientSpacingPlanner` 未修改；`CreateFreeForm`、事务、readback、fingerprint/Undo 既有核心链保持。Auto zone fingerprint 使用解析后的实际长度，避免 Preview 0 长度与 Create 显示长度造成伪 stale。
- 静态：GR01 V1 81/81；GUI 44/44；V1.0.1 UX 41/41；全部 `34 Test-*.py suites / 0 failure`；UI 201 C# / 43 XAML / 44 Ribbon / 0 error / 0 warning；MCP py_compile PASS。
- 当前环境无 dotnet/MSBuild/Revit，故状态：`GR01 V1.0.1 UX STATIC GO / REVIT 2025 ZAXIS+AUTO-ZONE RUNTIME REQUIRED / RELEASE NO-GO`。

# WORKLOG

## 2026-09-20 — GR01-P0 Engine GO → GR01 V1 Explicit ManagedFreeForm Core
- GR01-P0 evidence independently closed: Revit 2025 V1 engine is `Explicit ManagedFreeForm`; Native Surface/Aligned remain unproven/not selected for GR01 V1.
- Added `RebarGradientSetModels` + `RebarGradientSetService`: two single-position control Rebars, straight ModelLine/Edge path, Zone Station planning, Line/Arc interpolation, one explicit FreeForm Rebar Set, exact pre-commit physical-bar geometry readback, stale-preview fingerprint, rollback and product logs.
- Controls must share Host/BarType/RebarStyle; Hook/EndTreatment/Coupler are conservatively rejected in V1. Controls are never modified or deleted.
- Added typed MCP `preview_gradient_rebar_set` and `create_gradient_rebar_set`; create defaults dryRun. GUI/Ribbon intentionally deferred until Core runtime closes.
- Static: GR01 V1 **80/80**; GR01 P0 **43/43**; all **32 Test-*.py suites / 0 failure**; UI **198 C# / 42 XAML / 43 Ribbon / 0 error / 0 warning**; MCP py_compile PASS.
- Current environment has no dotnet/MSBuild/Revit runtime. Status: `GR01 V1 STATIC IMPLEMENTED / REVIT 2025 CORE RUNTIME REQUIRED / GUI NOT IMPLEMENTED / RELEASE NO-GO`.

## 2026-09-18 — Rebar Ribbon UI consolidation
- Restored `GroupName` Pulldown support and added stable Ribbon order.
- Rebar panel consolidated into four product groups: 创建钢筋 / 修改钢筋 / 端部与连接 / 检查钢筋.
- Removed Rxx stage labels from Rebar Ribbon/user-facing command titles.
- R05 GUI restricted to Runtime-closed Edge rebind; Start/End exact-face remains OPEN and is not exposed in GUI.
- Static: R00-R10 all PASS; UI 192 C#/42 XAML/43 commands/0 error/0 warning; all 30 Test-*.py suites PASS; MCP py_compile PASS.
- Added simplified planning doc for the proposed “首末模型线自动排布钢筋” feature; feature code not implemented in this change.
- Windows/Revit build/runtime not run in this environment; candidate remains RELEASE NO-GO.

## 2026-09-18｜R09 Runtime GO → R10 Coupler STATIC GO
- R09 independent runtime review closed Level-1 straight Rebar↔Rebar collision/clearance core; curved/tessellated runtime remains NOT_TESTED; release remains NO-GO.
- R10 adds native `RebarCoupler.Create` only: TwoBar + SingleEndCap, typed MCP + Ribbon share `RebarCouplerService`.
- DryRun performs no Transaction; preflight covers identities, end 0/1, existing Coupler/Hook, set count, Coupler type, and readable Bar Size 1/2 vs nominal diameter. Apply uses one Transaction, native `RebarCouplerError`, exact `RebarId+End` readback, identity/host preservation, rollback on invariant failure, and product logs.
- Typed MCP: `list_rebar_coupler_types`, `create_rebar_coupler`; Ribbon: `Coupler R10`. Splice/Lap stays deferred to R11.
- Static: R10 50/50; R00-R10 focused suites all PASS; all 30 Test-*.py suites 0 failure; UI 192 C# / 42 XAML / 43 Ribbon / 0 error / 0 warning; MCP py_compile PASS.
- Local C# build NOT RUN in Linux sandbox because dotnet/MSBuild is unavailable. Current status: `R10 STATIC GO / WINDOWS+REVIT RUNTIME REQUIRED / RELEASE NO-GO`.

## 2026-09-18｜R08 Final Runtime GO → R09 Collision/Clearance Static GO
- R08 full runtime + E-only retest independently reviewed. Final: `FREE FORM IN-PLACE MODIFY CORE RUNTIME GO / CONSTRAINED FREE FORM NOT_TESTED / RELEASE NO-GO`.
- E-only result SHA `12BD79B8326D72C85DDC2B345B49F3976400E2D5DE5645E0D7DBF3329F1F9D34`; source SHA and Build=Deploy=Loaded unchanged; six product-level rejection guards all closed; before/after normalized Inspector state identical.
- R09 opens QA Level-1 detection: read-only Rebar↔Rebar hard clash and requested clear-spacing screening. Surface gap = centerline min distance - radii sum; Clash and ClearanceViolation are separate statuses.
- R09 supports Shape Driven + Free Form physical bar positions, transformed centerlines, AABB broad phase, exact Line/Line 3D segment distance, and tessellated screening for non-Line curves. No Transaction / no auto-fix.
- Typed MCP `analyze_rebar_clearance`; Ribbon `碰撞/净距 R09`; UI v1 fixed 25mm, MCP fully configurable.
- Static: R09 37/37; R00-R09 all focused suites PASS; all 29 Test-*.py suites 0 failure (executed in batches); UI 189 C# / 42 XAML / 42 Ribbon / 0 error / 0 warning; MCP py_compile PASS.
- Current: `R09 STATIC GO / WINDOWS+REVIT RUNTIME REQUIRED / RELEASE NO-GO`.

## 2026-09-17｜Rebar R07 Runtime 复核 → R08 Free Form 原位修改
- R07 原始 RESULT 复核：`FREE FORM MINIMAL CORE RUNTIME GO / UI SCREENSHOT INVALID / CONSTRAINED FREE FORM OUT OF SCOPE / RELEASE NO-GO`。
- R07 实证：Revit 2024/2025 0 error；2024 Build=Deploy=Loaded；单根 FreeForm/unconstrained；explicit 3-bar set 原生 FixedNumber；Undo；错误矩阵。截图实际未显示 Revit，拒绝作为 UI 证据。
- R08 新增 `RebarFreeFormAccessor.SetCurves` 原位修改，仅接受 unconstrained/no-server Free Form；拒绝 Shape Driven 与 server-driven/constrained Free Form，不调用 DisconnectFromServer。
- R08 要求 ElementId/UniqueId/Host/Engine 保持，可原位改变 explicit set bar count。MCP `modify_free_form_rebar` 默认 dryRun=true；Ribbon `Free Form修改 R08`。
- 静态：R08 34/34；全部 Test-*.py 28 suites 0 failure；UI 41 Ribbon / 0 error / 0 warning。状态 `R08 STATIC GO / RUNTIME REQUIRED / RELEASE NO-GO`。

## 2026-09-15｜Rebar R04 Runtime GO → R05 M2 Constraint Rebind STATIC GO

- R04 Mimo 原始结果包复核：2024/2025 build 0 error；2024 Build=Deploy=Loaded；R00.3 transformed positions 正确；Varying set 6@200→8@150 原位修改保持 ElementId/UniqueId/Host/Edge+Start+End ToCover；BarType 12.7→16mm 原位修改；Undo 恢复 8@150。
- R04 最终：**CORE RUNTIME GO / UI_INTERACTIVE NOT_TESTED / RELEASE NO-GO**。
- 证据纠正：结果包 `screenshots/revit2024_r04.png` 实际标题栏为 Revit 2025.4，不作为 2024 UI 证据；但 journal.0105、MCP `RevitVersion=2024`、2024 Loaded path 与 SHA triple 独立闭合 2024 Runtime。
- R05 开始 M2：新增 `RebarConstraintRebindService`，可将既有 Shape Driven Rebar 的 Edge/StartOfBar/EndOfBar 原位重绑到同一 Host 的新 exposed PlanarFace `ToCover`。
- R05 强制保持 ElementId、UniqueId、Host、Varying 模式和未请求约束；精确目标面不再只按 HostId 判断。
- R00 Inspector 升级 R00.4：host-face constraints 增加 `TargetHostFaceStableReferences`，基于 `GetTargetHostFaceReference` + stable representation 验证 exact face。
- R05 增加 no-op target 拒绝、same-host filter、dryRun candidate preflight、失败 rollback。
- MCP 新增 `rebind_shape_rebar_constraints`；Ribbon 新增“约束重绑 R05”，累计 37 入口。
- 静态：R05 **57/57 PASS**；全量 **25/25 suites PASS**；UI static **175 C# / 42 XAML / 37 Ribbon / 0 error / 0 warning**。
- 当前：**R05 STATIC GO / WINDOWS+REVIT RUNTIME REQUIRED / RELEASE NO-GO**。

## 2026-09-11｜Reliability Hardening R2 源码复审补丁

在首版 F01–F05 静态候选上进行独立源码复审，发现两类不能带入 Windows 终验的残留，并已做小范围修复：

- F03：`FamilyManagerWindow` 仍有 6 处直接 `Commit()` 后继续成功流程；`LevelModifierCommand` / `DimTextOverrideCommand` 调用公共提交 helper 后仍无条件返回 `Result.Succeeded`。R2 已统一要求 `TransactionStatus.Committed`，未提交即失败，不再把 UI/命令返回值当模型真值。
- F05：`WriteProductOperation` 文档声称支持 `OperationId/BatchRunId`，实际 payload 只有 `OperationId`；Batch 又把 runId 直接当 operationId。R2 新增独立 `BatchRunId` 字段，并为一次 Batch 用户动作分别生成 `operationId` 与 `batchRunId`，所有 Batch 产品日志显式携带二者。
- `Test-ReliabilityHardening.py` 从 38 项扩展到 **43/43 PASS**；全量 `tools/Test-*.py` 仍为 **19/19 suites PASS**；UI static 153 C# / 42 XAML / 31 Ribbon / 0 error / 0 warning。
- 当前环境仍无 `dotnet/csc/mcs`，因此状态仍是 **SOURCE STATIC PASS / WINDOWS BUILD+FAULT-HARNESS+REVIT RUNTIME REQUIRED / RELEASE NO-GO**。
- 未混入 UI 七分组、MCP SDK v2、全功能 MCP 或安装器发布模式改造。

详细复审：`docs/RELIABILITY_HARDENING_R2_SOURCE_REVIEW_20260911.md`。

# 2026-09-11｜3.1.38 Reliability Hardening F01–F05

- 读取 Drive 审查 28、计划 29、MCP/分发 30、持续架构复查 31；确认优先级为可靠性→产品日志→Family/Batch UI→Ribbon/配置→全功能 MCP/发布。
- F01：抽出 `BatchFileCommitService`；恢复失败不删除备份；返回 BackupRetained/RecoveryMessages。新增真实 .NET 临时文件故障注入 harness。
- F02：`BtnExecute_Click` 强制调用和预检按钮相同的全批 preflight，共享 output reservations，确定性冲突在输出前失败。
- F03：TransactionHelper 检查 Commit/Assimilate 返回状态；Family 参数编辑成功后从模型 readback。
- F04：新增 BatchRunRequest 快照、IBatchTaskOptions；运行期锁定 RootLayout 输入，Engine 不依赖活 ViewModel。
- F05：DiagnosticLog 产品日志包含 OperationId/RunId、PID、Loaded DLL SHA、文档身份；Batch/PasteCAD/Family 在真实入口记录结果。
- `Test-ReliabilityHardening.py` 38/38 PASS；全量 19/19 suites PASS；UI static 0 errors/warnings。
- 当前 Linux 环境无 dotnet/csc/mcs，未执行 C# Build/Revit Runtime；不得扩大为 Runtime PASS。

# 2026-09-02｜3.1.38 版权声明 / About / Authenticode 发布层

- 用户决定不做反编译/DRM，仅保留所有权与来源声明：允许免费使用、公开分发、修改和二次开发；禁止未经授权商用；衍生版必须注明 YangTools / 原作者 Yang。
- `YangTools.Revit.csproj` 新增 Authors/Company/Product/Title/Description/Copyright，并固定 AssemblyVersion/FileVersion/InformationalVersion。
- 重做“系统设置 → 关于”页：版本、原作者、版权、许可摘要、Revit 2021–2027 独立编译说明、当前 DLL Authenticode 证书信息。
- 新增 `LICENSE.md / NOTICE.md / THIRD_PARTY_NOTICES.md`，构建输出复制到 Docs。
- 新增 Authenticode 流水线脚本：本地自签开发证书、SHA256+timestamp 正式签名、独立签名验证。禁止将 PFX/私钥/密码写入源码或交接。
- 本环境没有作者正式代码签名私钥，因此没有伪造受信任签名；下一 Windows 实机任务可生成 CurrentUser 非导出自签证书 `CN=Yang, O=YangTools` 做本地签名/加载 smoke，公开可信签名留待作者取得 CA 证书。
- 新增 `Test-CopyrightSigning.py` 47/47 PASS；全量 18/18 suites PASS。
- 本轮不修改 PasteCAD/Family/Batch 核心业务；3.1.38 仍需双模型 Batch、真实桌面 UI、产品链日志终验。

# 2026-09-02｜3.1.38 Batch Publisher V2 重做并进入统一候选

- 用户反馈旧“批处理与云链接”在真实使用中 NWC/PDF/DWG/IFC 经常导不出、设置跨模型不可靠、命名不可控，因此按用户工作流全面审查并重做。
- 旧根因：资源设置只从当前模型读取；DWG 多视图只给前缀；PDF 非合并 FileName 无效；云模型不是真正 Cloud ModelPath；NWC/IFC 关键设置缺失；输出子目录选项被代码强制；缺少批前输出路径规划。
- 新架构：`BatchDocumentOpener + BatchExportNaming + BatchTaskEngine + BatchTaskViewModel + BatchTaskWindow`。
- 支持本地/同步/已打开/原生云模型；云模型记录 region/project/model GUID 并可重建 Cloud ModelPath。
- NWC/IFC/DWG/PDF/Print 全部增加逐模型 preflight、OptionalFunctionality guard、视图/设置按目标模型解析、输出文件实际非空验证。
- PDF Export：2022+；分开输出采用每图 `Combine=true` 单调用实现精确名称；2025+强制关闭后台导出。
- PDF Print：2021+；InSession ViewSet + PrintToFileName + SubmitPrint，60s 文件落盘门禁并恢复用户打印状态；交互型驱动不得写无人值守 PASS。
- 输出安全：全批次路径 reservations；同批任务禁止互相覆盖；NWC/IFC/DWG staging commit，PDF/Print 临时文件安全替换；失败不先删旧成果。
- UI：1320×860 / Min1120×720，模型 Grid Row30/Header34，左链接区360，右侧 NWC/IFC/DWG/PDF/Print tabs；跨模型设置 ComboBox 可编辑。
- 新增/升级 `Test-BatchPublisherV2.py`：99/99 PASS。
- 当前全量静态：17/17 suites PASS；151 C# / 42 XAML / 31 Ribbon / UI 0 error 0 warning；C# lexical 151/151。
- 3.1.38 将 PasteCAD Shared + Family Management V2.1 + Batch Publisher V2 合并为一次 Windows/Revit 统一验收，未通过前正式基线仍是 3.1.36 FINAL_E2E。

## 2026-09-02｜3.1.38 Family Management V2.1 实现

- 执行者：ChatGPT。
- 基线：3.1.37 PasteCAD Shared 静态候选；3.1.36 FINAL_E2E 仍为运行正式基线。
- 新增 `StableParameterKey` / `FamilyUsageService`；ModelessWindowRegistry 支持激活已存在窗口时执行定位配置。
- 族文档管理新增使用统计、未使用过滤、安全清理、实例跳转、多族 RFA、刷新；动态参数列改为全 Symbol union + stable key。
- 族实例管理新增 Project/View/Selection Scope、Type/Level filter、Level/Host/Pinned、批量换型、批量参数/only-empty、系统类别动态发现；Excel 增加固定状态列并使用 stable display label。
- UI：Family 1280×780 Min1080×650，Instance 1360×800 Min1160×660；Row 30/Header36；固定/动态列显式 Width/Min/Max；工具栏/DataGrid 横向滚动防压缩。
- 新增 `Test-FamilyManagementV2.py`，110/110 PASS；全部 16 个 Test 套件 PASS；UI 150 C#/42 XAML/31 Ribbon/0 error/0 warning；C# lexical 150/150。
- 本环境无 dotnet/Revit，未把静态结论写成 Runtime PASS。
- 下一步：Gemini 用 3.1.38 一次部署同时闭环 carried-forward PasteCAD Shared + Family V2.1。

## 2026-09-02｜3.1.37 从 CAD 粘贴共享坐标 + 族管理 V2 审查

- 执行者：ChatGPT。
- 输入正式基线：`YangTools_Project_3.1.36_Source_CAD_TO_REVIT_FINAL_E2E_20260901.zip`，SHA `D126F1A65F0DD4A74A1F97AD6F1CD3302309F77212E30BD7752666DE740CDBA4`。
- 版本提升至 3.1.37；Ribbon 数量不变（31）。
- `从 CAD 粘贴`：定位 ComboBox 新增“对齐共享坐标”，映射 `ImportPlacement.Shared`；设置索引支持 0..2。
- 共享坐标模式明确要求项目已建立有效共享坐标；YangTools 不获取/发布坐标、不修改项目坐标，也不在失败后静默 fallback Origin。
- `PasteCadCommand` 增强：`DWGImportOptions` using；日志记录 Unit/Color/Placement/ThisViewOnly；Shared 导入失败给专用诊断；成功信息记录定位方式；原 Transaction rollback / 工作副本清理 / 选中新 ImportInstance / 一次 Ctrl+Z 语义保持。
- 新增 `tools/Test-PasteCad.py`，19/19 PASS；全量 `tools/Test-*.py` 15/15 suites PASS；UI 静态 31 Ribbon、0 error、0 warning。
- 源码审查 `FamilyManagerWindow.xaml.cs`（约 1209 行）与 `FamilyInstanceManagerWindow.xaml.cs`（约 1258 行）：当前分别偏“类型参数表格编辑器”和“实例参数表格编辑器”，高价值管理动作不足。
- Family Manager 重点缺口：使用/未使用统计、Family→实例管理跳转、安全清理未使用类型、批量 RFA、真正 Family Document 参数/公式/嵌套/Host/Connector 摘要；并发现参数主路径大量 `LookupParameter(name)`、参数列只取首个 Symbol、配置按 FamilyName 文件名保存、删除缺少 Trial Delete 预检。
- Family Instance Manager 重点缺口：显式 Scope、Type/Level/Host/Pinned 过滤、批量换类型、批量写同值参数、状态列、稳定 ParameterKey；参数列只取首实例，System Families 类别硬编码。
- 本轮族管理仅形成 `docs/FAMILY_MANAGEMENT_V2_REVIEW_3.1.37_20260902.md`，不修改现有族管理业务代码。
- 当前状态：**STATIC GO / WINDOWS REVIT RETEST REQUIRED**。

## 2026-09-01｜3.1.36 CAD 转 Revit 图纸 Windows/Revit 真实实机验收

- 执行者：DeepSeek / Codex (Antigravity)。
- 源码基线：`YangTools_Project_3.1.36_Source_CAD_TO_REVIT_STATIC_20260901.zip`。
- 新增功能：`导入工具区 → CAD转Revit图纸`，Ribbon 提升至 **31**。
- 实体兼容加固：修复 `CadRevitExchangeReader` 对 YangCAD 官方 `DxfType` 实体字段解析，保留 `type` / `kind` / `entity_type` / `object_type` 兼容别名；修复 `Test-CadToRevit.py` 增设 69/69 契约项。
- 真实编译构建：Revit 2021–2026 逐年独立编译 0 error（2024 DLL SHA: `FA7F58D1...`, 2025 DLL SHA: `23EB78BB...`）；2027 因缺少 .NET 10 SDK 真实标记 `NOT_BUILT (NETSDK1045)`。
- 部署与宿主：部署 DLL 与构建 DLL SHA 1:1 一致；Revit 2021 (PID 18544) 持续安全运行未被干扰；Revit 2024 完成全新重启（PID 29244/2428）并加载最新 DLL。
- 实机 MCP 验证：在 Revit 2024 活动视图中完成 5 组几何实测（水平、垂直、斜向、圆弧、填充，误差均为 0.000 mm <= 1.0 mm）；真实 Dimension (ID 1185239) 与专用线样式 `YangTools-尺寸界线` 隔离创建；注入坏标注 SubTransaction 干净回滚（0 孤儿线残留）；批次图元创建后 Undo 数量精确归位；Mapping.json 持久化无 ElementId。
- 交付产物：生成 `YangTools_Project_3.1.36_Source_CAD_TO_REVIT_FINAL_20260901.zip` 与 10 项结构化交付文件。

## 2026-08-21｜线性布置 P0 真实 Z 门禁加固 + 当前源码静态复核

- 执行者：ChatGPT。
- 输入基线：`YangTools_Project_3.1.35_Source_SIMPLE_MATERIAL_20260821.zip`。
- 当前 Windows 复验候选：`YangTools_Project_3.1.35_Source_SIMPLE_MATERIAL_P0_Z_GATE_20260821.zip`。
- 架构体检：没有发现需要在实机闭环前进行的大型结构重构；保留当前单项目 + Core/Commands/UI/Mcp 分区，避免扩大回归面。
- 发现 P0 校验缺口：`SetLevelRelativeOffset()` 之前写入候选 Offset 后只用 `ProjectElevation + Offset.AsDouble()` 推导 Z，没有读取实例真实 `LocationPoint.Z`，存在把“可写但不生效”的第一个候选误判为成功的风险。
- 修复：每个候选参数写入后立即 `Regenerate` 并读取 `LocationPoint.Z`；只有真实 Z 误差 `<=1 mm` 才接受。失败候选恢复原 Offset，再试下一候选；恢复失败时抛错，外层线性布置事务回滚。
- 增强最终日志：`FamilyPlacementType / LevelId / Level.Elevation / Level.ProjectElevation / BuiltInParameter / Offset.AsDouble() / 理论 WorldZ / LocationPoint.Z / 误差(mm)`。
- `Test-LinearElevationProfile.py`：31/31 -> **37/37**；数值组合继续 20/20。
- 本环境已全量运行现有 `tools/Test-*.py`：UI 0 错误/0 警告、30 Ribbon、简易材质 31/31、批量材质 31/31、Assistant 清理 37/37，其它专项全部通过。
- 未完成：Windows 2024/2025 真实编译与部署、用户原问题模型线性布置 P0、两套材质绑定实机、Assistant 数据清理实机。
- 状态：**STATIC GO / WINDOWS RETEST REQUIRED**；未生成分享安装包，不推送 Git。

## 2026-08-21｜简易绑定材质 + 批量版 UI 修复 + 新对话交接

- 基线：`Source_MATERIAL_BINDING_20260814` → `Source_SIMPLE_MATERIAL_20260821`。
- 新增 `项目管理区 → 绑定材质`：从当前预选或 Revit PickObjects 获取一个/多个 FamilyInstance；支持重新选择。
- 小窗口输入：材质参数名、实例/类型参数、项目已有材质、是否覆盖已有几何材质参数绑定。
- 默认实例参数；实例模式只修改所选实例，类型模式对所选实例对应 Symbol 去重后赋值。
- 新增 `SimpleFamilyMaterialBindingService`，复用批量族材质绑定服务的族编辑/回载链，并把族绑定和项目 Material.Id 赋值合并到一个外层 TransactionGroup。
- `FamilyMaterialBindingService` 新增 `ExecuteWithinTransactionGroup`，允许简易版在已有 TransactionGroup 中复用，不重复产生独立撤销项。
- 原 `绑定材质` 改名为 `批量绑定材质`；修复批量窗口参数名称输入框被挤压/垂直显示异常。
- Ribbon：29 → 30。
- 新增 `tools/Test-SimpleFamilyMaterialBinding.py`：31/31；批量绑定材质 31/31；UI 静态 0 错误/0 警告；既有专项全量复跑未见回退。
- MCP SDK v2 继续仅记录，不迁移。
- 未完成：当前源码 Windows 真编译、Revit 2024/2025 简易/批量材质实机、线性布置最新 LevelOffset 修复的用户原模型复验。
- 本轮完成后生成完整源码 ZIP、完整交接包、DeepSeek/Codex 任务和新对话启动提示词；不推送 Git。

## 2026-08-14｜新增“绑定材质”；MCP v2 仅记录

- 新增 `项目管理区 → 绑定材质`：`FamilyMaterialBindingCommand`、`FamilyMaterialBindingService`、`FamilyMaterialBindingWindow.xaml(.cs)`。
- 支持一个/多个可编辑载入族；材质参数名称自定义；类型参数/实例参数可选；默认允许覆盖已有其它材质参数关联。
- 族文档扫描 `MATERIAL_ID_PARAM`，创建/复用 Material FamilyParameter，关联全部可处理几何并 `LoadFamily` 回载项目。
- 同名参数非 Material 或类型/实例冲突时安全失败；无可绑定几何时不创建无用参数。
- 回载后保护既有 Family/FamilySymbol/FamilyInstance ID 与正 ID 自定义参数值；异常触发整批回滚。
- 新增 `tools/Test-FamilyMaterialBinding.py` 与专项验收文档；Ribbon 由 28 增至 29。
- MCP SDK v2 只记录为未来独立任务；当前 `mcp>=1.2.0,<2` 与 FastMCP 实现不变。
- 当前环境仅做静态检查；Windows/Revit 编译部署与实机交给 DeepSeek/Codex。

## 2026-08-14｜彻底清理已删除 AI/摸鱼助手用户数据

- 用户明确要求删除旧助手历史、设置和 API Key，不再保留。
- 新增 `Core/LegacyUserDataCleanup.cs`：Revit 启动时幂等删除 `%APPDATA%\YangTools\Assistant\`。
- 同时删除旧迁移源文件：`copilot_settings.json`、`copilot_history.json`、`moyu_settings.json`、`moyu_history.json`；这些旧 settings 可能包含历史 API Key，因此必须一并清理。
- `Install.ps1`、`Uninstall.ps1`、GUI 安装器、`Build-All.ps1 -Deploy`、`Build-Deploy-2024-2025.ps1` 同步执行清理，覆盖日常 DeepSeek 直接部署与分享安装包两条流程。
- 清理范围严格限定在退役助手数据；`%APPDATA%\YangTools\MCP`、主题和其它仍使用的个人配置不删除。
- 新增 `tools/Test-AssistantDataCleanup.py` 静态门禁。

# 2026-08-14 删除 AI/摸鱼助手；新增族类别修改

- 执行者：ChatGPT
- 基线：`Source_LINEAR_LEVEL_OFFSET_FIX_20260813`。
- 删除：`Commands/CopilotCommand.cs`、`Core/Assistant/`、Assistant/Copilot UI、DockablePane provider、聊天历史/模式存储、copilot 图标、已无调用方的 `Core/RevitApiSearcher.cs`；`App.OnStartup` 删除 DockablePane 注册。
- 保留：MCP Gateway、RevitExecutionBroker、MCP Python；将内部事务名 `YangTools AI Python` 改为 `YangTools MCP Python`，避免残留聊天助手语义。
- 新增：`FamilyCategoryChangeCommand`、`FamilyCategoryChangeService`、`FamilyCategoryChangeWindow.xaml(.cs)`；Ribbon 总数继续为 28。
- 语义：用户口中的“Abutments 改成 Generic Models”属于 **Family Category 修改**，不是族类型名修改。
- 批量链路：选择多个可编辑载入族 → 目标 BuiltInCategory → `EditFamily` → 族文档事务设置 `OwnerFamily.FamilyCategory` → `LoadFamily` 回载 → 回载类别核验。
- 保护：同类别跳过、不可编辑/内建族跳过；回载前的单族失败可记录并继续。回载前快照 Family/FamilySymbol/FamilyInstance ElementId 与正 ID 自定义参数内部值；回载后逐项复核，任何类别/ID/参数值异常触发整个 TransactionGroup 回滚。
- 当时策略：不自动删除 `%APPDATA%\YangTools\Assistant\` 的旧历史/设置/Key。**该策略已被同日后续用户明确要求覆盖**；当前版本会彻底清理退役助手数据，见本文件顶部记录。
- 静态：UI 面板检查 0 错误/0 警告、28 Ribbon；族类别/AI 删除专项 53/53；既有 Review2 54/54、链接 6/6、文本宽度 15/15、Loft/实体提取 16/16、PrintPDF 32/32、中文清理 67/67、线性高程 31/31 + 20/20、参数清理模拟均通过。
- 未完成：Windows 真实编译；Revit 2024/2025 Abutments→Generic Models、批量回载、实例/类型/参数/ElementId 保持、Ctrl+Z 实机。

# 2026-08-13 线性布置 level-based Z 约束二次修复

- 用户再次报告：`期望=(...,29.527559)，实际=(...,18.044619)，误差=11.48294 ft`，与上一轮相同。
- 复核确认：上一轮 `ProjectElevation` 修复只解决坐标基准问题，但 `FamilyPlacementHelper.CreateAtWorldPoint` 后续仍使用 `ElementTransformUtils.MoveElement` 做 Z 校正。
- Autodesk API 对 level-based 元素明确限制：MoveElement 不能使其在 Z 方向离开所属标高，必须用 Offset / Elevation from Level 参数。
- 修改 `FamilyPlacementHelper`：新增 `CreateAtLevelRelativeElevation`，按顺序尝试 `INSTANCE_ELEVATION_PARAM`、`INSTANCE_FREE_HOST_OFFSET_PARAM`、`FAMILY_BASE_LEVEL_OFFSET_PARAM`；写入用户相对高程后按 `ProjectElevation + offset` 校验。
- 修改 `LinearPlacementEngine`：使用专用 level-based 放置路径，并传入每个插值实例的 `RelativeElevationFeet`。
- 静态回归：UI 0 错误 0 警告；ReviewFix 53/53；线性高程 31/31 + 20/20 数值组合；其余核心专项全部通过。
- 尚未完成：Windows 真实编译与用户原问题模型实机复验。

# 2026-08-13 线性布置 ProjectElevation 实机 Bug 修复

- 执行者：ChatGPT
- 用户实机错误：期望 `(1643.6042,-560.36282,29.527559)`，实际 `(1643.6042,-560.36282,18.044619)`，Z 差 `11.48294 ft ≈ 3500 mm`。
- 根因：2026-08-12 新高程逻辑使用 `Level.Elevation` 计算世界 Z；当 Level 的 Elevation Base 使用共享坐标时，`Elevation` 不是 XYZ 所需的项目原点高程。
- 修复：`LinearPlacementEngine.ApplyElevationProfile` 改为 `targetLevel.ProjectElevation + relativeElevationFeet`；诊断同时记录项目高程与显示高程。
- UI：`SelectedLevelElevationDisplay` 以 ProjectElevation 为主；若与 Elevation 相差超过 0.5 mm，两个值同时显示。说明文字明确“项目坐标高程 + 输入高程”。
- 回归：把 9000/5500/3500 mm 实机错误加入 `Test-LinearElevationProfile.py`；当前 26/26 源码契约、20/20 数值组合，UI 静态 0 错误 0 警告；其它专项回归通过。
- 未完成：Windows 真实编译、部署，以及用户原问题模型/共享坐标项目 Revit 2024/2025 实机复验。
- 判定：源码/静态 GO；Windows/Revit 条件 GO；正式发布 NO-GO。

## 2026-08-07｜中文检查升级为分类“中文检查与清理”

## 2026-08-12｜线性布置：基准标高 + 首尾高程线性插值

- 执行者：ChatGPT
- 基线：`Source_CHINESE_CLEANUP_20260807` → `Source_LINEAR_ELEVATION_20260812`。
- 修改：`LinearPlacementViewModel.cs`、`LinearPlacementWindow.xaml/.cs`、`LinearPlacementEngine.cs`、`UserSettings.cs`。
- 新增 UI：基准标高、起点高程(mm)、终点高程(mm)，并显示所选 Level 自身高程。
- 核心公式：`世界Z = targetLevel.Elevation + relativeElevationFeet`；中间高程按实际首尾实例之间的 `DistanceAlongPath` 比例线性插值。
- 首尾语义：首个实际实例严格使用起点高程；最后实际实例严格使用终点高程。只有一个实例时若首尾不同，提示后使用起点高程。
- 方向：`ReversePath=true` 时首尾高程跟随有效路线反转；`KeepVertical=false` 时切线 Z 使用新高程坡度，避免沿旧路线 Z 倾斜。
- 设置：`UserSettings.SettingsVersion` 6→7，记忆 `LinearLevelName / LinearStartElevationMm / LinearEndElevationMm`，不持久化 Revit ElementId。
- 静态：UI 0 错误 0 警告；线性高程专项 25/25，非零标高数学组合 20/20；既有 Review2 54/54、链接 6/6、文本宽度 15/15、Loft/实体提取标高 16/16、PrintPDF 32/32、中文清理 67/67、参数清理模拟均通过。
- 未做：当前环境无 Windows/.NET/Revit，未进行真实编译、部署和 GUI/世界 Z 实测。
- 下一步：DeepSeek/Codex 在全新目录编译并部署，重点验证正/负/非零标高、正负首尾高程、固定数量/间距、反转路线、Ctrl+Z。


### 目标

把原四块只读中文列表升级为可实际管理中文结果的分类表格；用户可以按族/类型/材质/视图/图纸/参数等分组勾选、重命名、删除或清空。

### 修改

- 重写 `UI/ChineseCheckWindow.xaml(.cs)`：左分类、右 DataGrid，搜索、全选、全不选、反选、逐行复选。
- 分类扩展至 39 组，新增系统类型、组/装配、图纸/明细表列标题、修订信息、工作集、设计选项、范围框、图案、用户子类别、阶段、项目位置、房间/空间/面积、链接与输出设置。
- 双击修改按对象能力执行 Element.Name / TextNote.Text / 字符串 Parameter / Workset / SheetNumber / ScheduleHeading / SpatialNumber / AssemblyTypeName。
- 删除实体前用回滚事务做 `Document.Delete` 级联预检；参数文本值改为清空语义；高风险二次确认。
- 批量实际清理：一个主 Transaction + 每项 SubTransaction；单项失败隔离，成功批次一次 Ctrl+Z。
- Ribbon 说明更新；新增 `docs/CHINESE_CHECK_CLEANUP_3.1.35_20260807.md`、`docs/DEEPSEEK_CODEX_CHINESE_CHECK_ACCEPTANCE_3.1.35_20260807.md`、`tools/Test-ChineseCleanup.py`。

### 静态结果

- UI/XAML/资源/事件：0 错误、0 警告。
- 中文清理专项：67/67。
- 40 个 XAML XML 解析通过。
- 本环境无 Windows/.NET/Revit，真实编译和 GUI 写操作待 DeepSeek/Codex。

### 判断

**源码静态 GO；Windows/Revit 条件 GO；正式发布 NO-GO。**

---

## 2026-08-07｜FINAL3 证据与打包闭环修正

### 复查发现
- FINAL2 独立 evidence ZIP 的 CRC、59 条内层 SHA-256 和两个外层 ZIP 哈希均正确。
- FINAL2 源码包仍残留旧版 `artifacts/evidence.zip`（SHA-256 为旧轮次 CE26A233…），与独立 FINAL2 evidence ZIP 不一致。
- 最终源码包带入 Revit Journal 后，`Test-UiPanelsStatic.py` 将宿主日志误当 UTF-8 源码，产生 3 个编码错误。
- `docs/HANDOFF_3.1.35.md` 仍含未标记为历史的“未编译/旧基线”状态；README/WORKLOG 使用旧哈希文件名，WORKLOG 警告数量不一致。

### 修正
- 删除旧版嵌套 `artifacts/evidence.zip`，独立证据包作为唯一证据归档。
- 静态检查排除 `artifacts/evidence/logs/**` 的 Revit Journal/运行日志；C#/XAML/文档/JSON 等仍正常检查。
- 更新 README、PROJECT、WORKLOG、HANDOFF、兼容矩阵和功能矩阵。
- 产品源码目录 `src/YangTools.Revit` 未修改，FINAL2 编译与 Revit 2024 实机证据继续有效。

### 判断
- FINAL3 源码包：GO（交付层）。
- 独立 evidence 包：GO。
- 项目总体：条件 GO。
- 正式发布：NO-GO。

## 2026-08-07｜Windows 独立编译、Revit 2024 实机复验与编译错误修复

### 执行内容
- 解压 `Source_REVIEW2_20260807` 到全新空目录。
- `Verify-SourceBaseline.ps1` 首次运行失败：UTF-8 无 BOM 被 PowerShell 5.1 按 ANSI 解析导致含中文串语法错误 → 给 `tools/Verify-SourceBaseline.ps1`、`tools/Build-Deploy-2024-2025.ps1` 加 UTF-8 BOM。修复后基线验证通过。
- 静态检查：`Test-UiPanelsStatic.py` 0 错误 0 警告；`Test-ReviewFixes.py` 54/54、`Test-BatchLinkPathMatching.py` 6/6、`Test-AlignTextWidth.py` 15/15、`Test-GeneratorLevelPlacement.py` 16/16 全部通过（本机 Python 3.12）。
- 构建：2021/2022/2023/2024 各 0 错误、198 个 nullable 警告；2025 为 0 错误/454 警告，2026 为 0 错误/227 警告但未装 Revit → 未完成；2027 缺 .NET 10 SDK 未构建（NETSDK1045）→ 未完成。

### 本轮修复的编译错误
1. `Core/ParameterCleanup/ParameterDeleteService.cs`：`foreach (ParameterRecord rec)` 与方法体级 `rec` 同名（CS0136）→ 后者重命名 `singleRecord`。
2. `UI/FamilyManagerWindow.xaml.cs`：`familyDoc.LoadFamily(doc, ...)` 返回 `Family` 而非 `bool`（CS0029）→ 改为接收 `Family` 并判空。确认无需项目事务。

### Revit 2024 实机（MCP 驱动真实 3.1.35）
- B1 本地 RVT 重复跳过、B3 同名不同目录不误判、B4 CAD 重复识别、B5 失败续跑+IsModifiable=false、B6 合并 PDF 覆盖/锁定保护/不覆盖、B7 缺同名视图/设置明确报错 —— 通过。
- B2 云路径等价算法 7/7；真实 ACC 集成待 ACC 实机。
- C1 线性布置世界 Z（水平/斜线 ≤0.2mm）、C3 标高修改无重复实例、C4 布尔相交/不相交/Union 失败安全门、C5 负/零标高锚定 —— 通过。
- D 区（Excel 导入原子性、删除确认、图框换型、LoadFamily 运行时）为窗口事件/文件对话框驱动，MCP 无法自动化 → 源码+静态契约验证，需人工 GUI 复核。
- 已知限制：通用"线性布置"对 TwoLevelsBased 结构柱世界坐标校验为安全失败（LocationPoint.Z 不反映偏移），由专门的"沿线布柱"工具承担。

### 证据
- `artifacts\evidence\FINAL_VERDICT.md`（条件 GO；正式发布 NO-GO）
- `artifacts\evidence\VERIFICATION_REPORT_20260807.md`
- `artifacts\evidence\SHA256SUMS-20260807.txt`、`artifacts\evidence\logs\`（YangTools 日志 + Revit Journal）
- `artifacts\build\`（各年份 build.log + binlog）

## 2026-08-07｜文本基准对齐增加段落宽度同步

### 用户需求
- 在“对齐文本/基准对齐”中增加勾选框，使目标文字的段落框宽度与源文字一致，语义对应 Dynamo 的文本 Width 功能。

### 源码修改
- `AlignTextToTextState`：新增 `MatchParagraphWidth` 和失败原因列表。
- `AlignTextToTextWindow`：新增“段落宽度相同（复制源文字框宽度）”复选框，支持选择流程中保存/恢复状态；说明窗口补充换行、文字类型和宽度范围语义。
- `AlignTextToTextCommand`：读取源 `TextNote.Width`，按目标文字类型允许范围校验后精确设置目标宽度；设置后再次读取校验。
- 每个目标继续使用独立 `SubTransaction`。宽度不兼容或目标受约束时只回滚该目标，完成提示最多显示 8 条失败原因。
- 允许只勾选段落宽度执行，不要求同时勾选 X/Y/旋转。

### 静态检查
- 新增 `tools/Test-AlignTextWidth.py`，覆盖 UI、状态保存、API 读写、范围校验、设置后复核、子事务和失败提示链路。
- 当前环境无 Windows/.NET/Revit，未执行真实编译与 TextNote.Width 实机验收。

### 当前判断
- 源码/静态：条件 GO。
- 正式发布：NO-GO。
- 下一任务：`docs/DEEPSEEK_CODEX_ALIGN_TEXT_WIDTH_ACCEPTANCE_3.1.35_20260807.md`。

## 2026-07-29｜3.1.35 源码核验、文档同步与 Windows 执行任务


## 2026-08-05｜检查报告复核、项目资产表头与事务链路定点修复

### 输入
- 用户提供 `REVIEW_批处理与云链接_2026-08-03.md`。
- 用户截图显示项目资产管理器“视图过滤”三个状态列表头被压缩成单字。

### 源码修改
- `ProjectAssetManagerWindow`：过滤列 120/120/150，设置最小宽度；动态列固定最小宽度；导入/导出表头兼容；Project Info 操作守卫；操作栏和分析按钮可见性；图案下拉关闭后写入并屏蔽关闭状态滚轮；动态参数名截断修复。
- `FamilyInstanceManagerWindow`：参数绑定 `Length - 18`；选择过滤按族自动解除；非族提示；Tab 切换重载对应配置。
- `FamilyManagerWindow`：参数绑定 `Length - 18`；列头参数未变化时不刷新；保留 `LoadFamily` 无项目事务的正确 API 语义并检查返回值。
- `SheetManagerWindow`：初始化守卫；图框“图名”列头和“图框实例”状态栏。
- `LinearPlacementEngine`：完整事务回滚、3D 最近标高、世界坐标放置、断开路线提示。
- `FaceBasedConverterWindow`：载入/替换/事务组显式回滚；逐实例 SubTransaction；世界坐标放置。
- `LevelModifierEngine`：重建分支使用 SubTransaction，失败不留重复实例。
- `BooleanGeometryCommand`：族文档、项目载入事务和 TransactionGroup 状态检查/回滚。
- 批处理：链接 Commit/Assimilate 状态清理；Revit 2021 PDF UI 禁用。

### 报告复核
- 把 Commit 非 Committed 的“必然事务泄漏”降为防御性清理缺口。
- ACC 路径重复识别、布尔失败几何、快速 ExternalEvent 顺序保留为实机待证。
- 移除 FamilyManager LoadFamily 事务、必然无限循环、ProjectAsset 正常入口 NRE、合法重复图号等误报。

### 检查
- `Test-UiPanelsStatic.py`：0 错误、0 警告。
- `Test-ReviewFixes.py`：37/37 通过。
- `Test-GeneratorLevelPlacement.py`：16/16 通过。
- `Test-ParameterCleanupPipeline.py`：通过。
- XAML XML 解析：通过。
- 本环境无 Windows/.NET/Revit，未执行真实编译和 GUI 验收。

### 当前判断
- 源码/静态：条件 GO。
- 正式发布：NO-GO。
- 下一任务：`docs/DEEPSEEK_CODEX_REVIEW_FIX_ACCEPTANCE_3.1.35_20260805.md`。

### 本轮范围
- 不新增功能，不推送 Git，不修改未被真实编译或实机证明有问题的业务代码。
- 以 3.1.35 交接包为唯一事实来源，复核源码、构建配置、安装清单、静态规则和下一阶段验收任务。

### 核验结果
- 项目版本为 3.1.35；解决方案和项目文件均包含 Revit 2021–2027 七个 x64 Configuration。
- 目标框架为 2021–2024 `net48`、2025–2026 `net8.0-windows`、2027 `net10.0-windows`。
- Addin 清单指向 `YangTools/YangTools.Revit.dll`；普通构建默认不部署；构建和安装均按年份隔离。
- 源码包内无 DLL/PDB/EXE/NuGet 二进制，无已删除 Ribbon/AI/MEP 残留。
- WPF 第二 Application、全局主题注入、重复参数清理 `LookupParameter(name)`、清理窗口 `Task.Run` 模型访问均未发现回退。
- 静态复跑：251 文本、144 C#、40 XAML、35 根节点、4 主题、28 Ribbon；错误 0、警告 0。
- 重复参数清理链路模拟：source contract 0 失败；606 安全候选 3 次初始批量调用；冲突值保留、值丢失检测和唯一性异常检测通过。
- Python 工具语法检查通过。

### 代码与文档变更
- 产品 C#、XAML、主题、图标和业务算法：0 修改。
- 新增 `docs/SOURCE_VERIFICATION_3.1.35_20260729.md`。
- 新增 `docs/DEEPSEEK_CODEX_WINDOWS_TASK_3.1.35_20260729.md`。
- 新增 `artifacts/verification/source-build-audit-3.1.35.json`。
- 修正文档文件名：将内容已经是 3.1.35 的 `docs/DUPLICATE_PROJECT_PARAMETER_CLEANUP_3.1.34.md` 更名为 `docs/DUPLICATE_PROJECT_PARAMETER_CLEANUP_3.1.35.md`，修复 README 本地链接。
- 删除与 `MicroTool_Developer_Guide V3.md` 内容完全相同、文件名中包含字面 `%20` 的冗余副本；README 的 URL 编码链接仍正确指向空格文件名。
- 同步更新 README、PROJECT、WORKLOG、功能矩阵、兼容性说明和 HANDOFF。

### 当前结论
- **条件 GO：源码可交给 Windows 编译与 Revit 验收。**
- **正式发布 NO-GO：尚无 3.1.35 Windows 编译、Revit 2024 28 入口和六项专项验收证据。**

### 下一步
1. DeepSeek/Codex 按完整任务执行七配置还原和编译，保存日志、binlog、DLL 版本、大小与 SHA-256。
2. 干净部署 Revit 2024，完成 28 入口烟雾测试。
3. 优先验收重复项目参数清理，再验收临时插件箱、文本等距分布、文本基准对齐、中文检查和系统面板设置。
4. 只修真实错误，修复后重新静态检查、编译、定点回归并打包完整源码。

## 2026-07-30｜3.1.35 Loft / 实体提取目标标高与世界高度修复

### 用户确认的问题

- 实体生成（Loft）和实体提取选择目标标高后，生成模型的世界高度与原几何不一致。
- 现象符合目标标高 Elevation 未正确抵消、重复计入或写入非实际定位偏移参数。

### 代码修改

- 新增 `Core/FamilyPlacementHelper.cs`。实例直接按原几何世界坐标锚点创建，并关联用户选择的 Level。
- Revit 再生后读取 `LocationPoint.Point`；若与期望坐标不一致，使用 `ElementTransformUtils.MoveElement` 校正一次，再生后再次校验。
- `LevelId` 必须等于目标标高；最终位置误差必须 ≤ `1e-6 ft`，否则抛错并由 TransactionGroup 回滚。
- `EntityGeneratorEngine` 和 `SolidGeneratorEngine` 删除 `SetInstanceOffset` 及偏移参数猜测。
- Loft 锚点继续使用首轮廓 XY + 最低 Z；实体提取继续使用联合包围框中心。
- 两个窗口补充“目标标高仅关联、保持原几何高度”的中文说明。
- 新增 `Test-GeneratorLevelPlacement.py` 与 JSON 报告，静态拦截旧偏移逻辑回退。

### 编译风险定点处理

- WPF/Revit `ComboBox`、`Grid`、`FontFamily` 显式限定类型。
- 移除 `FeatureHelpWindow` 的 TextBox `LineHeight`。
- `ParameterDeleteService` 局部变量 `record` 改名。
- Revit 2021 `Definition.ParameterType` 读取改为反射。
- `Verify-SourceBaseline.ps1` 修正错误令牌并增加世界坐标放置门禁。

### 本环境验证

- UI/XAML/资源/UTF-8 静态检查：257 个文本、145 个 C#、40 个 XAML、35 个根节点、4 套主题、28 个 Ribbon；0 错误、0 警告。
- 重复参数清理链路模拟：通过。
- 目标标高放置契约与 16 个数学组合：通过。
- 当前环境没有 Windows、MSBuild 和 Revit，不能声称专项实机通过。

### 下一步

- DeepSeek/Codex 在 Windows 逐年份编译。
- Revit 2024 使用负、零、正三个目标标高，分别验证 Loft 和实体提取的 LevelId、LocationPoint、几何 MinZ/MaxZ/CenterZ 与撤销。
- 只修真实编译或专项复验暴露的问题。


## 2026-07-24｜3.1.35 DeepSeek 实机成果回收与快速值索引引擎

### 用户反馈
- DeepSeek 已使用 MCP/Python 在三个 Revit 2021 项目中完成实际清理，证明任务本身可以在约 46–50 秒内结束。
- 用户要求把成功经验固化进 YangTools，避免以后同类模型再次长时间无反馈、反复定位或只能依赖临时脚本。

### 报告审查
- 可靠数据：606/573/135 个重复组，删除 1770/3019/179 个来源参数；第三个项目复制 21891 个值。
- 报告误判：YangTools 3.1.33 已经是实例/类型各一次 Collector，并非每组独立扫模型；Revit API 不能通过 `Task.Run` 在后台线程直接访问。
- 临时脚本风险：跳过绑定合并；keeper 有值集合复制后未更新；Delete 后再读取已删除对象无法可靠识别级联；发现异常也没有子事务回滚；没有值完整性和唯一性硬复核。

### 代码修改
- 新增 `ParameterValueIndexService` 和 `ParameterValueIndex`。
- `ParameterCleanupVerificationService.CaptureBefore` 在同一次实例/类型扫描中同时建立值快照和值索引。
- `ParameterMergeService` 改为快速值索引引擎，只为来源真正有值的元素建立操作表；每个相关元素参数只遍历一次。
- 参数身份回退使用 GUID/Definition 并强制 ParameterElement Id 二次校验；禁止名称匹配。
- 首个来源填充空 keeper 后，后续来源按当前 keeper 值判断相同/冲突，避免覆盖。
- 参数身份重取大面积失败时提前回滚熔断，避免数万错误继续无效运行。
- `ParameterApiCompatibility` 统一全局参数关联检查。
- 快照、值索引、绑定、值迁移、删除和复核增加阶段进度心跳；只泵送 WPF Render，不把 Revit API 放入后台线程。
- `ScanResult.Diagnostics` 和 `10_运行诊断.csv` 增加值索引、身份失败、Delete 与事务统计。

### 验证
- `Test-ParameterCleanupPipeline.py`：source contract 0 失败；606 安全候选 3 次删除调用；冲突值不覆盖；值丢失和唯一性异常检测通过。
- `Test-UiPanelsStatic.py`：236 文本、143 C#、39 XAML、34 根节点、4 主题、28 Ribbon；错误 0、警告 0。
- Python 工具语法检查通过。
- 当前环境无 Windows/.NET/Revit，真实编译和三个实机模型的插件复测仍由 DeepSeek 完成。

### 下一步
1. 编译部署 3.1.35 Revit 2021。
2. 先处理最多 10 个来源参数，确认日志持续刷新、快速值索引、完整性和唯一性。
3. 再用 RIS_003 同类副本验证约 21891 个值迁移，错误不再达到数万条。
4. 收回 `10_运行诊断.csv`、完整 CSV、Journal 和总耗时，给出 GO/NO-GO。

## 2026-07-24｜3.1.33 实机性能报告回收与删除路径修复

### 实机数据
- 项目参数 2750，唯一名称 978，重复组 606；清单计算冗余来源参数 1772。
- 10 个来源直接 `Document.Delete`：99.9 ms；回滚：31.6 ms。
- `BindingMap.Remove`：95.5 ms，实际移除 0；Remove+Delete：119.2 ms。
- 每个来源参数实际返回 2 个删除 ID：ParameterElement 自身 + 1 个依赖子对象。

### 根因
- 3.1.32 将所有额外返回 ID 都视为危险级联。正常 ParameterElement 依赖子对象导致批次回滚并持续二分，最终每个参数都被单项尝试并阻止；因此删除 10 个也显得慢。
- 直接 Delete API 本身并不慢，解绑方案在当前模型是无效空操作。

### 修改
- 删除前通过 `GetDependentElements(null)` 建立每个来源参数的允许删除集合。
- `Document.Delete` 返回请求参数与预期依赖子对象时直接提交；只有未预期对象或目标参数缺失才回滚二分。
- 默认批次 25 → 250，进度日志步长 50 → 250。
- 增加 Delete API、子事务提交、外层事务提交和依赖分类计时。
- 快速处理/仅删除移除事务内 CSV，移除提交后的重复完整扫描；复用提交前复扫结果。
- 增加快照、合并、删除、复扫、复核、提交、报告、UI 与全链路计时。
- 更新静态与纯逻辑测试，正常依赖子对象不再触发回滚；606 正常候选模拟仅 3 次初始批量调用。

### 验证
- `Test-ParameterCleanupPipeline.py`：通过。
- `Test-UiPanelsStatic.py`：234 文本、142 C#、39 XAML、34 根节点，错误 0、警告 0。
- 仍需 Revit 2021 真实编译和 10 个来源参数复测。

## 2026-07-24｜3.1.30 重复项目参数清理整合、主题 UI 与清理后复核


## 2026-07-24｜3.1.32 清理后唯一性与元素参数值完整性硬复核

### 已修改

- 新增 `ParameterCleanupVerificationService`。快速处理前按实例/类型各一次遍历，保存选中参数组在各元素上的全部不同非空内部值；String/Integer/Double/ElementId 均保存原始值。
- 合并和自适应批量删除完成后，在 `TransactionGroup.Assimilate()` 之前重新扫描并逐元素复核。任何原值消失、元素缺失、读取错误都会抛出并回滚整个快速处理事务组。
- 已完整删除全部来源参数的组，清理后必须精确剩余 1 个项目参数；若删除记录与最终扫描不一致，同样回滚。
- 因共享参数、明细表/过滤器、全局参数、冲突或级联对象而保留来源参数的组不强制回滚，但报告为“未完全清理/仍重复”。
- “仅删除”也包装进独立 TransactionGroup，并执行同样的值完整性与唯一性复核。
- CSV 新增 `08_参数值完整性复核.csv` 和 `09_已选参数唯一性复核.csv`；窗口运行输出新增数据完整性复核摘要。
- 更新源码基线和纯逻辑链路测试，模拟确认能识别“冲突值被删掉”和“来源均报告删除但仍有重复参数”两类错误。

### 已验证

- `Test-ParameterCleanupPipeline.py`：选择、606 参数自适应删除、值丢失检测、唯一性异常检测全部通过。
- `Test-UiPanelsStatic.py`：234 个文本、142 个 C#、39 个 XAML、34 个根节点，错误 0、警告 0。
- 当前环境无 Windows/.NET/Revit，真实事务、参数值和撤销仍需 DeepSeek 在 Revit 2021 项目副本完成。

### 下一步

1. DeepSeek 编译部署 3.1.32。
2. 使用“测试：最多 10 个来源参数”执行快速处理。
3. 验收运行输出中的“元素参数值：通过”和“已完成删除组唯一性：通过”。
4. 检查 08/09 CSV，并抽查至少 3 个元素的清理前后参数值。
5. 执行 Revit 撤销，确认参数和数值恢复后再全量处理。


### 用户要求
- 功能进入 YangTools“检查工具区”，名称固定为“重复项目参数清理”。
- 更新图标和主题，关闭按钮旁增加功能说明按钮。
- 扫描后在运行输出显示清理预览；清理后报告是否仍有重复参数及不重复参数数量。
- 确认前轮性能与数据原则是否需要更新：按元素单次遍历、不删除重建、不用显示字符串转存数值。

### 代码修改
- 新增 `DuplicateProjectParameterCleanupCommand`、WPF 主题窗口、参数清理 Models/Services/兼容层，并将 Ribbon 总数提升至 28。
- 新增 `parameter_cleanup_16/32.png` 与图标母版绘制函数，沿用 YangTools 深蓝灰/浅蓝线条系统。
- 窗口使用 YangTools 四套主题、虚拟化 DataGrid、可选择复制的运行输出；底栏增加“功能说明”。
- 扫描输出理论和保守预览，包括已知至少剩余重复组、预计最多形成的不重复参数；快速处理/删除后自动再次扫描，输出清理前后实际对比，并生成 `07_清理后复核.csv`。
- 参数值处理保持实例和类型各遍历一次；改为复用相关参数字典与触达计划集合，移除内层 List/Tuple LINQ 分配。
- 类别一致时跳过 ReInsert；无 ReInsert 时跳过 Regenerate。
- 原始值按 StorageType 直接读取/Set；显示字符串不参与回填。
- 删除仍逐来源参数 SubTransaction 执行；Document.Delete 返回额外 ID 即回滚该参数。
- 新增 2021 与 2022+ 参数数据类型/分组/ReInsert API 兼容封装。
- 更新源码基线和 UI 静态检查，固定入口、算法、原始值复制、预览报告与级联回滚规则。
- `Build-All.ps1` 新增 `-Deploy` 当前用户级原子部署；图形安装器与命令行部署都会自动移除历史独立 `RevitParameterDeduplicator` 入口。

### 静态验证
- 文本 223、C# 141、XAML 39、XAML 根节点 34、主题 4、Ribbon 28。
- XAML/XML、资源、事件处理器、UTF-8、图标尺寸/透明通道检查通过。
- 参数清理回归检查：无 `LookupParameter`；实例/类型单次 Collector、原始 StorageType Set、Document.Delete 级联回滚、扫描预览和清理后复核均存在。
- 错误 0、警告 0。

### 未验证边界
- 当前环境无 Windows .NET SDK/Revit，未执行多版本真实编译。
- DeepSeek 需在 Revit 2021 项目副本验证 606 组快速处理总耗时、实际删除数量、自动复核、CSV、撤销和窗口主题。
- Revit 2022–2027 需至少完成逐年编译；2024/2025 建议做启动和扫描级抽查。

## 2026-07-21｜3.1.29 Revit 原生 AI 面板、隐蔽私人会话与文本选择

### 用户要求
- 删除所有模型生成设置，使用模型/服务商默认值。
- 私人会话保持隐蔽，工作面板不能出现相关信息。
- 改为真正的 Revit 原生面板，并取消回复风格。
- 对话内容可以用鼠标选择并复制。

### 代码修改
- 新增 `AssistantDockablePaneProvider`，在 `App.OnStartup` 通过 `RegisterDockablePane` 注册；Ribbon 使用 `DockablePane.Show/Hide` 切换。
- 删除 `AssistantWindow.xaml/.cs` 与 `AssistantWindowController`，项目版本提升到 3.1.29；源码基线禁止旧独立窗口残留。
- `CopilotPanel` 改为单一 `ContentControl`。工作视图立即创建，私人视图只在隐藏双击手势后延迟创建，工作 XAML 中不再存在预加载的第二面板。
- 工作面板可见文案不包含“摸鱼”；删除 `/moyu`、`/work`、`/ai` 斜杠入口，避免界面或输入帮助暴露切换方式。
- 删除 `AssistantResponseStyle`、回复风格 ComboBox、temperature、最大输出 token 配置及对应 JSON 字段。
- Chat Completions、Responses、Ollama payload 不再发送 `temperature`、`max_tokens`、`max_output_tokens`、`num_predict`；仍固定 `stream=false`。
- 截断提示仅依据服务端明确结束原因；继续按钮和历史状态保留。
- 正文/技术详情允许焦点与 IBeam 光标，增加右键复制/全选；引用从不可选择的 TextBlock 卡片改为可选择 Paragraph，代码仍为只读 TextBox。
- `Verify-SourceBaseline.ps1` 与 `Test-UiPanelsStatic.py` 更新为 3.1.29 回归规则。

### 静态验证
- 文本文件 205、C# 130、XAML 38、XAML 根节点 33、主题 4、Ribbon 27。
- XAML/XML 解析、资源键与类型、事件处理器、UTF-8、C# 括号词法检查全部通过。
- `AssistantResponseStyle/MaxOutputTokens/temperature/max_tokens/max_output_tokens/num_predict/AssistantWindow` 运行源码残留为 0。
- 静态检查：错误 0、警告 0。

### 未验证边界
- 当前环境没有 Windows .NET SDK 和 Revit，未执行真实 C# 编译。
- Revit 2024 必须验证首次加载无黑屏、面板可右侧停靠/浮动/关闭重开、重启 Revit 后位置恢复、ACC 模型打开稳定。
- FlowDocument 正文需实机验证鼠标拖选、Ctrl+C 和右键全选；不同 Revit/WPF 版本的命令路由仍以实机结果为准。

## 2026-07-21｜3.1.28 AI / 摸鱼助手界面与输出重构

### 用户要求
- 删除流式输出。
- 界面更接近 Revit 用户界面，同时保持稳定。
- 人设需要支持 100 KB+。
- AI 输出达到上限时不能静默截断。
- 其余回复格式和交互问题按既定 P0/P1 规划处理。

### 架构决策
- 保留 Revit 主窗口 Owner 的非模态 WPF `ToolWindow`；不恢复 3.1.22 已移除的 DockablePane/NativeHook，避免重新引入自动展开、黑屏和点击崩溃风险。
- “Revit 用户界面”落地为：Revit 进程内、无独立任务栏、工具窗口标题栏、紧凑分区、低圆角、属性面板式设置；不是注册到 Revit 停靠框架的 DockablePane。

### 代码修改
- 从 `AssistantProfileSettings`、UI 和 `AssistantApiClient` 删除 `UseStreaming`、SSE 读取、增量回调和流式消息状态。
- 三类协议统一 `stream=false`，回复完成后一次性构造 `FlowDocument`。
- AI 回复改为全宽文档，用户消息保留右侧气泡；普通段落的单换行合并为空格，空行分段。
- 代码块保留在正文，增加复制按钮；工作模式 Python 增加显式执行按钮，删除自动执行询问弹窗。
- 技术折叠区只保留 reasoning、工具调用、Revit 上下文和诊断信息。
- 新增快速人设切换、当前选择开关、回复风格、最大输出 tokens、提示词导入/导出/复制、实时大小显示。
- 人设 JSON 和请求消息不截断；导入单文件上限 2 MB。100 KB+ 文件可完整保存和发送。
- 新增上下文超限 HTTP 400 识别，明确提示人设+历史+问题超过模型上下文。
- 输出截断读取 `finish_reason` / `incomplete_details` / `done_reason`；兼容代理缺失结束原因时结合 usage 达到配置上限做兜底。截断消息展示警告和继续按钮。
- 工作模式默认严谨温度 0.2；摸鱼模式默认自由温度 0.85；默认输出上限 8192 tokens。

### 静态验证
- `tools/Test-UiPanelsStatic.py` 新增 AI 3.1.28 回归规则：禁止流式残留，要求超大人设、输出限制、截断检测、继续按钮和正文代码卡片。
- 204 个文本文件、130 个 C# 文件、39 个 XAML、34 个窗口/视图根节点、4 套主题、27 个 Ribbon 命令。
- XAML/XML、事件处理器、资源引用、UTF-8、C# 注释/字符串/括号词法结构均通过；错误 0、警告 0。
- Python 工具脚本 `py_compile` 通过。

### 未验证边界
- 当前环境没有 Windows .NET SDK、Revit API 运行时和 Revit GUI，未执行真实 C# 编译、网络 API 请求、100 KB 人设实发和 Revit 窗口点击。
- 模型自身的上下文/输出上限由供应商和具体模型决定；插件只能配置请求上限、识别服务返回的结束原因并提示，不能突破模型硬限制。

### 实机下一步
1. DeepSeek 在全新 3.1.28 目录编译并部署 Revit 2024。
2. 分别打开工作/摸鱼模式，确认设置中无“流式输出”。
3. 导入至少 120 KB UTF-8 人设，保存、重开 Revit、再次读取并发送短问题。
4. 将输出上限临时设为 256，要求长回答，确认显示截断警告和继续按钮。
5. 验证普通段落、列表、表格、链接、代码卡片、复制和显式 Python 执行。

## 2026-07-21｜3.1.27 运行时 StaticResource 与安装器部分年份修复

### 实机反馈
- DeepSeek 已完成目标年份编译和直接部署，但图形安装器仍报“缺少 Revit 2021 的真实编译文件”。
- 若干面板打开时抛出：`Provide value on 'System.Windows.StaticResourceExtension' threw an exception`。
- 该反馈证明 3.1.26 的“键存在/类型一致”静态检查仍不足以覆盖 Revit 宿主中的 WPF 运行时资源作用域。

### 根因
- 直接部署只写入 `%APPDATA%\Autodesk\Revit\Addins\{年份}`，不会自动生成安装器要求的 `installer\payload\{年份}`。
- 旧安装器对任一被勾选但缺少 payload 的年份立即终止，导致无关的 Revit 2021 阻断已真实编译的 Revit 2024。
- 6 个高风险面板在自身资源字典中通过 `StaticResource` 继承外部共享样式。Revit 内嵌 WPF 在 `InitializeComponent` 解析局部资源时，外部字典可能暂时不在当前查找作用域，因而运行时报错。

### 修改
- 版本提升至 3.1.27。
- 所有业务 XAML 中，外部共享样式静态引用改为 `DynamicResource`；同文件局部资源仍保留 `StaticResource`。
- AI 子面板、中文检查、族管理、族实例管理、图纸管理和批处理中的局部继承样式改为自包含，消除 `Style.BasedOn` 对外部字典的初始化依赖。
- `ThemeHelper` 增加最深层异常展开，错误提示包含具体面板名和真正资源错误。
- `Test-UiPanelsStatic.py` 新增外部共享 `StaticResource` 回归检查；当前残留 0。
- GUI/命令行安装器新增 `Get-VersionSource`：正式 payload、本地 `bin\x64\年份`、本地 artifacts payload 均可作为真实来源。
- 缺失年份改为跳过，不影响其他可安装年份；仍禁止跨年份 DLL 替代。
- 安装源校验增加共享样式和四套主题文件。
- 打包脚本默认打包实际存在的年份，并可从 bin 自动收集；`-RequireAll` 才执行七年份严格发布检查。

### 本环境验证
- 39 个 XAML、34 个根节点、130 个 C# 文件解析/映射通过。
- 外部共享 `StaticResource`：0。
- 未定义资源：0。
- UTF-8/乱码：0。
- PowerShell 修改脚本括号、引号和 here-string 结构检查通过。
- 当前环境无 Windows/.NET/Revit，未执行真实 C# 编译和窗口点击。

### 实机下一步
1. 在全新目录解压 3.1.27。
2. DeepSeek 只需编译用户实际使用年份（优先 2024）并部署。
3. 双击源码中的 `installer\YangTools安装器.cmd`，确认 2024 显示“可安装：本地编译输出”；2021 缺失不应再中断。
4. 优先点击 AI 助手、批处理与云链接、项目中文检查、族文档管理、族实例管理、图纸与图框管理。
5. 通过后再生成对应年份正式安装包。

## 2026-07-21｜3.1.26 面板/编码静态验收与快速安装器

### 项目分析
- 源码包含 27 个 Ribbon 命令、34 个 XAML 根节点（32 个窗口/对话框、2 个 UserControl）、130 个 C# 文件，C# 约 25,527 行。
- 主要风险集中在大型 code-behind：`ProjectAssetManagerWindow.xaml.cs` 1835 行、`FamilyManagerWindow.xaml.cs` 1129 行、`FamilyInstanceManagerWindow.xaml.cs` 1120 行、`AssistantModeView.xaml.cs` 934 行。当前不做无收益的大规模 MVVM 重构。
- Revit 2021–2024 使用 net48，2025–2026 使用 net8.0-windows，2027 使用 net10.0-windows；各年份仍必须独立引用对应 API 编译。

### 面板与编码检查
- 新增 `tools/Test-UiPanelsStatic.py` 和 `静态检查面板与编码.cmd`。
- 严格 UTF-8 检查 199 个文本文件；Unicode 替换字符、典型乱码标记均为 0。
- 解析 39 个 XAML；34 个窗口/视图根节点的 x:Class、代码后置类、事件处理器、资源引用与主题初始化全部通过。
- 4 套主题均为 75 个同名同类型资源键；共享样式 20 个资源键；未定义资源引用为 0。
- 禁止第二个 WPF Application 和 `Application.Current.Resources` 全局污染的源码检查通过。
- 当前环境无 Windows/Revit，因此该结果是静态 GO，不等于 Revit 实机逐窗口 GO。

### 安装器
- 新增中文 WinForms PowerShell 图形安装器，支持 Revit 2021–2027 多选安装/更新/卸载。
- 安装到当前用户 AppData，不需要管理员权限；安装前检查 Revit 进程。
- 更新采用 `YangTools.installing` + `YangTools.backup`，失败恢复旧插件。
- 卸载同时清理残留 staging/backup，但保留个人设置。
- 打包脚本自动读取项目版本、默认要求七年份 payload、生成 SHA256SUMS。

### 版本与其他修改
- 版本提升至 3.1.26。
- AI HTTP User-Agent 改为读取程序集版本，消除后续版本号硬编码漂移。

### 下一步
1. Windows 运行 `核验源码.cmd` 与 `静态检查面板与编码.cmd`。
2. 安装 .NET 8 SDK、.NET 10 SDK、.NET Framework 4.8 Developer Pack，执行 `tools\Build-All.ps1`。
3. 生成正式七版本安装包并在 Revit 2024 逐个点击 27 个入口；其中“标注替换”“标高修改”需先准备选择，“剖面(By Line)”没有 WPF 面板。
4. Revit 2024 通过后，再做 Revit 2025 与 2027 的启动级验证。

## 2026-07-14｜3.1.25 AI简洁显示、执行器/MCP重构与图标收口

### 用户目标
- 默认只看正常交流；代码、显式 reasoning、工具、Revit上下文和诊断全部折叠。
- 把上一轮 3.1.24 审查出的稳定性问题与 MCP 状态重构合并处理。
- 内置工作助手与外部 MCP 解耦；MCP状态必须反映真实执行链。

### AI修改
- `CopilotWindow` 重命名为 `AssistantWindow`；旧系统设置窗口已命名为 `SystemSettingsWindow`。AI仍只在点击后创建，不注册 DockablePane。
- 新增 `AssistantResponseParser` 和带折叠技术详情的消息模型；默认隐藏显式 reasoning、think/analysis 标签、代码块、工具调用、Revit上下文和HTTP/MCP诊断。
- 普通成功聊天没有技术内容时不再显示仅含协议/URL的折叠栏；复制回复只复制可见主回答。未闭合的显式 reasoning/tool 尾部标签也会被折叠。
- 流式输出改为160ms纯文本刷新，完成后只构造一次 `FlowDocument`；代码块限制高度并支持内部滚动/复制。
- 设置页面使用提示词编辑副本；取消不再修改正式配置。
- Endpoint输入区分 Base URL 与完整聊天 Endpoint；`UriBuilder` 保持查询参数位置，测试和聊天共用解析器。
- 非Chat Completions协议关闭本地Revit API工具；API XML缺失时也自动禁用。
- API Key只允许DPAPI存储；加密失败拒绝保存，不再Base64回退。

### Revit执行与MCP修改
- 删除旧 `McpExternalEventHandler` 和 `McpHttpServer`，新增统一 `RevitExecutionBroker`。
- Broker提供超时、取消、队列状态和延迟IronPython初始化；已完成或超时的请求在ExternalEvent稍后到达时会被跳过。
- 内置工作助手直接调用Broker，不依赖外部MCP网关。
- 新增默认关闭的 `McpGateway`：127.0.0.1、8081–8090动态端口、每次启动随机Bearer Token、按Revit进程写实例文件。
- 外部Python默认禁止；状态页可启动/停止/重启、自检、复制连接配置，并显示执行器和最近请求。
- 重写 `McpServer/server.py`：使用 FastMCP stdio，自动发现存活实例和令牌，删除虚假/错误命名工具，只公开六个真实命令。
- 新增 `McpServer/requirements.txt` 和 `McpServer/README.md`，明确 Python 依赖、stdio 配置、实例发现、stdout 限制和安全边界。
- 新增 `docs/AI_MCP_ARCHITECTURE.md` 固化模块边界和命名。

### 图标与规范
- 新增独立 `solid_extract_16/32.png`，实体提取不再复用Loft图标。
- 图标资产为27组语义、54个PNG；生成器仍是唯一母版。
- 核验脚本新增真实像素尺寸、透明通道和Loft/提取图标分离检查。

### 验证边界
- 当前环境没有Windows .NET SDK、PowerShell和Revit，不能执行真实C#编译或实机测试。
- 本轮按用户要求不交给DeepSeek；最终包只声明静态核验，不声明AI、MCP或Revit运行通过。

## 2026-07-13｜3.1.23 工作助手与摸鱼助手一体化隔离

### 目标
- 保持一个 YangTools 插件，避免维护第二个摸鱼 DLL。
- 摸鱼模式内置且入口隐蔽。
- 工作助手与摸鱼助手的设置、提示词、历史、API Key 完全独立。
- 摸鱼模式不读取 Revit 项目上下文，也不进入 Python/MCP 执行链。

### 修改
- 新增 `AssistantModeStorage.cs`，分别使用 `Assistant/Work`、`Assistant/Chat` 目录。
- 每种模式独立保存 `settings.json`、`prompts.json`、`history.json`、`api_key.dat`。
- 自动迁移旧 `copilot_settings/history.json` 与 `moyu_settings/history.json`，保留旧文件作为回退。
- 删除 `YangTools.Moyu.dll` 门槛；隐藏可见切换按钮，保留双击模式图标、`/moyu`、`/work` 入口。
- 摸鱼模式关闭 Revit 选择上下文、Revit API 工具调用和 Python 执行。
- `DeepSeekClient` 增加无工具非流式请求路径。
- 异步回复固定写入启动请求时的模式，回复期间禁止切换，避免两个模式串历史。

### 静态验收
- XAML/XML 解析通过。
- 四个助手持久化文件名与两个模式目录均存在于源码。
- `YangTools.Moyu.dll`、`_moyuAvailable` 残留为 0。
- 摸鱼模式三项 Revit 能力开关均为 false。
- C# 字符串/注释/括号轻量检查通过。
- Windows Revit 2024/2025 双编译、双部署与实机切换仍需完成。

## 2026-07-13｜3.1.22 AI 助手自动展开、黑屏与点击崩溃修复

### 目标
- AI 助手只能由用户点击 Ribbon 后创建。
- 消除 Revit 启动时自动展开的空黑停靠面板。
- 消除点击 AI 助手时的 DockablePane/WPF 宿主崩溃链。
- 核对并保护“摸鱼助手”隐藏模式。

### 修改
- 删除 `CopilotPaneProvider.cs` 和 DockablePane 注册/Idling 隐藏逻辑。
- 新增 `CopilotWindow.xaml/.cs`，使用单例非模态窗口控制显示、隐藏和关闭。
- `CopilotPanel` 从 `Page` 改为 `UserControl`。
- 删除未被订阅的 `NativeHookManager` 全局键盘钩子。
- 加入设置/历史 JSON 归一化，兼容旧数据中的 null 列表与空字段。
- “摸鱼助手”继续保留独立设置、历史和默认人设，但必须存在 `YangTools.Moyu.dll`；修复双击图标绕过检查。

### 静态验收
- XAML/XML 解析：通过（38 个 XAML）。
- XAML 事件处理器：0 缺失。
- DynamicResource / StaticResource：0 个未定义引用。
- RibbonButton：27。
- DockablePane、Idling 自动隐藏、全局键盘钩子残留：0。
- AI 设置/历史兼容归一化与 ExternalEvent 异常保护：已检查。
- Windows Revit 2024/2025 编译与实机点击仍需 DeepSeek 完成。

## 2026-07-13｜3.1.21 AI 停靠面板 WPF/System.Drawing 编译歧义修复

- 基线：3.1.20。
- 修复 `CopilotPaneProvider.cs` 中 `Brushes` 与 `FontFamily` 的 CS0104 歧义。
- 原因：项目启用了 `UseWindowsForms` 与 `ImplicitUsings`，编译器同时可见 `System.Drawing` 和 WPF 同名类型。
- 修复方式：四处 WPF 类型全部改为 `System.Windows.Media` 全限定名。
- 未修改 AI 助手延迟创建、文本工具、Ribbon 或业务逻辑。
- 增加源码基线检查，禁止该文件重新出现未限定 `Brushes` / `FontFamily`。
- 待验收：Revit 2024/2025 双编译、双部署和 Revit 2024 启动。

## 2026-07-13｜3.1.20 文本工具 ComboBox 编译歧义修复

- 基线：3.1.19。
- DeepSeek 双版本编译发现 3 个 `CS0104`：`DistributeTextWindow.xaml.cs` 2处、`AlignTextToLineWindow.xaml.cs` 1处。
- 根因：文件同时引用 `System.Windows.Controls` 与 `Autodesk.Revit.UI`，方法参数使用未限定 `ComboBox`。
- 最小修复：3 处统一改为 `System.Windows.Controls.ComboBox`，未修改文本工具业务逻辑。
- `Verify-SourceBaseline.ps1` 增加未限定 WPF ComboBox 回归检查。
- 当前环境无 .NET SDK/Revit；Revit 2024/2025 编译与双部署待 DeepSeek 验收。

## 2026-07-13｜3.1.19 AI 按需打开、删除 MEP 废弃功能、文本工具修复

- 基线：3.1.18。
- AI 助手改为点击 Ribbon 后才创建真实页面；第一次 Idling 隐藏 Revit 可能恢复的旧可见状态。
- 删除 `ManholeElevationCommand`、`PipeModifyTypeACommand` 及对应图标，Ribbon 预期 27。
- 修复文本工具窗口运行时插入行导致内容与“选择文本”按钮重叠的问题。
- 4 个选择型文本功能改为外部命令状态机，移除 WPF 模态窗口内部 `Hide/Show + PickObject(s)`。
- 文本修改增加前置校验、事务状态检查、失败回滚与诊断日志。
- 静态验收待执行；Windows Revit 2024/2025 编译与部署待 DeepSeek 完成。

## 2026-07-13｜3.1.18 Revit 启动阶段 WPF Application 兼容修复

### 现象
- 3.1.17 可以打开 ACC 云模型，但 YangTools 在加载时提示：`Revit 的 WPF Application 尚未初始化`。
- 原因是 `OnStartup` 立即构造 `CopilotPanel`，而主题初始化仍要求 `Application.Current` 非空。

### 修改
- `ThemeManager.XamlInitializationScope` 改为直接将主题和共享样式附着到当前 YangTools 根元素。
- 删除主题初始化对 `Application.Current` 的依赖；全流程不触碰 `Application.Current.Resources`。
- 新增 `CopilotPaneProvider` / `CopilotPaneCreator`，通过 `IFrameworkElementCreator` 延迟创建 AI 面板。
- `App.OnStartup` 不再构造 `CopilotPanel`。
- AI 面板初始化异常时显示代码构建的安全错误页，并写入诊断日志。
- 版本提升至 3.1.18，Ribbon 数量保持 29。

### 待验收
- Revit 2024/2025 双编译和双部署。
- Revit 2024 启动时 YangTools 正常加载。
- ACC、本地、中心模型均可打开。
- 首次点击 AI 助手时延迟创建面板且不报 XAML 错误。

## 2026-07-13｜3.1.17 ACC 云模型 XAML 崩溃紧急修复

### 用户实机故障

- 安装 3.1.16 后，本地模型和中心模型可以打开，但打开 ACC 云模型时 Revit 直接闪退。
- 崩溃信息：`PresentationFramework.dll`、CLR `e0434352`、`System.Windows.Markup.XamlParseException`。
- 卸载 YangTools 后 ACC 云模型恢复正常，确认插件是触发条件。

### 根因

- `App.OnStartup` 在 `Application.Current == null` 时主动创建 `System.Windows.Application`，这在 Revit 宿主进程中不安全。
- `ThemeManager` 把主题和 `SharedStyles.xaml` 长期写入 `Application.Current.Resources`。
- `SharedStyles.xaml` 含大量无 `x:Key` 的隐式 WPF 样式，会自动作用于同一 Application 范围内的 Autodesk 窗口。ACC 打开流程会创建额外 WPF/XAML 界面，因此最容易触发资源类型/模板解析冲突。
- 批处理模块没有注册 DocumentOpening/DocumentOpened 监听，故本次崩溃不是 ACC 路径或批处理文档打开逻辑导致。

### 修复

- 删除创建第二个 `System.Windows.Application` 的逻辑。
- `ThemeManager.Initialize` 只读取主题配置，不再持久修改 Revit 全局资源。
- 新增受控 `XamlInitializationScope`：只在 YangTools `InitializeComponent` 同步执行期间临时提供资源，随后无条件从 Application 资源域移除，并转移到当前 YangTools 根元素。
- 32 个 XAML 窗口/页面统一改用 `ThemeHelper.InitializeWithTheme(this, InitializeComponent)`。
- `CopilotPanel` 也使用相同隔离流程，避免 DockablePane 在 Revit 启动时污染全局样式。
- 纯代码 `ParameterSelectWindow` 改为本地主题资源，并用窗口自身 `TryFindResource`，不再从 Application 全局查询。
- `AssistantWindow` 的主题颜色查询改为窗口本地资源。
- 主题切换只更新 YangTools 已注册根元素。

### 状态

- 版本提升至 3.1.17，Ribbon 数量保持 29。
- 当前环境无 Windows/.NET/Revit，尚未编译。
- 本轮首要验收是 Revit 2024 与 2025 双编译/双部署，并实际打开同一个 ACC 云模型。ACC 通过前暂停批处理、AI和MCP继续开发。

## 2026-07-11｜3.1.16 批处理与云链接重构

### 审查结论

- 原流程会对每个目标模型重复执行链接任务，而外部模型又以“不保存”关闭，链接结果可能直接丢失；链接现只作用于当前活动项目。
- 外部模型原先没有明确区分普通/工作共享文档；现使用 `BasicFileInfo` 判断，工作共享模型按保留工作集方式分离打开，仅用于导出并关闭不保存。
- 多模型导出原先会静默退回当前视图/当前设置，并可能在同一目录产生同名覆盖；现严格按名称匹配，缺失时记录失败，同时支持每模型子目录和 `{模型名}` 文件名模板。
- 原导出链忽略部分 API 返回值、未检查 NWC 导出器、PDF 错误限制为 2024+；现逐项检查结果，PDF 条件修正为 Revit 2022+。
- “云链接”定位收敛为本地、网络或 Desktop Connector 已同步路径；未实现 ACC GUID 直接浏览时不再作误导性承诺。

### 已修改

- 新增 `BatchDocumentOpener`、`BatchTaskRunResult` 与链接结果模型。
- 重写 Revit/CAD 链接服务：重复路径可跳过、可固定、独立事务，并由外层事务组合并为一次撤销。
- 重写 NWC/IFC/DWG/PDF 导出：精确视图/图纸集/设置匹配、覆盖保护、结果与预期文件检查；IFC 使用临时事务后回滚，避免导出使模型保持修改状态。
- 外部模型已打开时复用且不关闭；由 YangTools 打开的模型只导出，关闭时不保存。
- 批处理窗口增加链接范围说明、输出子目录、覆盖、文件名模板、链接固定/跳过、PDF 合并等设置。
- 输入保存到 `settings.json`；每次运行生成 UTF-8 批处理报告，汇总成功、提示、失败和输出文件。
- 版本提升至 3.1.16；Ribbon 数量保持 29。

### 验证边界

- 完成 XAML/XML、事件处理器、静态资源、UTF-8、基础 C# 词法与源码基线检查。
- 当前环境无 Windows/.NET SDK/Revit/NWC 导出器，尚未完成 2024/2025 真实编译、双部署和导出实机验收。

### 下一步

- DeepSeek 必须使用统一脚本同时编译并部署 Revit 2024、2025。
- 在测试项目中验证当前文档链接、外部工作共享模型无保存关闭、同名视图/图纸集匹配、多模型输出隔离、NWC/IFC/DWG/PDF 返回与覆盖保护。

## 2026-07-11｜3.1.15 沿线布柱方向对齐与旋转偏移

### 用户反馈

- 3.1.14 创建的柱能够沿路线布置，但柱实例没有随路线切线旋转。
- 用户要求柱沿路线旋转，或提供可手动修正的旋转角度输入。

### 已修改

- 沿线布柱对每个布置点计算所在曲线的水平切线。
- 新柱创建后绕插入点竖向轴旋转，使族本地 X 轴默认沿路线方向。
- 新增“旋转偏移角度（°）”：0° 沿路线，90° 可让族方向与路线垂直；反转路线后切线与柱方向同步反转。
- 旋转采用当前实例 Rotation 与目标角度的差值，避免重复叠加默认旋转。
- 柱族不能绕竖向轴旋转时，整批事务回滚并显示明确错误。
- 旋转偏移角度写入 settings.json，重复使用时自动恢复。
- 版本提升至 3.1.15；Ribbon 数量保持 29；源码核验增加方向计算与 RotateElement 回归检查。

### 验证状态

- 完成 XAML/XML、事件处理器、静态资源、UTF-8、源码结构与 ZIP 完整性检查。
- 当前环境无 Windows .NET SDK/Revit，尚未完成 2024/2025 编译部署和 Revit 实机方向验收。

### 下一步

- DeepSeek 必须使用统一脚本同时编译并部署 2024、2025。
- 在直线、折线、圆弧路线分别检查 0°、90°、负角度及反转路线后的柱方向。

## 2026-07-11｜3.1.14 新增沿线布柱与线性布置路线选项

### 输入与确认

- 用户要求新增“沿线布柱”：选择首尾连续的模型线并连接为开放路线，默认每 6000 mm 布置一根柱；可选柱类型、首柱距路线起点、路线起点里程、Base/Top Level 与 Offset，并支持反转路线首尾、末尾不足间距时在终点补柱。
- 用户要求把“反转路线首尾”和“末尾不足间距时补放终点实例”同步加入既有线性布置。
- DeepSeek 已确认 3.1.13 在全新目录完成 Revit 2024/2025 双编译，均为 0 error；Revit 2024 干净部署和 28 个 Ribbon 按钮启动通过。

### 已修改

- 新增 `ColumnAlongPathCommand`、`ColumnAlongPathEngine`、`ColumnAlongPathModels` 和 `ColumnAlongPathWindow`。
- 新功能只接受模型线，验证开放、连续、无分叉路线；闭合环、断点和分叉会在布置前明确阻止。
- 柱类型限制为结构柱/建筑柱且放置方式为 `OneLevelBased` 或 `TwoLevelsBased`。两类柱均按 Autodesk 的柱创建方式调用 `NewFamilyInstance(XYZ, FamilySymbol, Level, StructuralType.Column)`；创建前激活 FamilySymbol，再写入 Base/Top Level 与 Offset。
- 每个实例写入 `FAMILY_BASE_LEVEL_PARAM`、`FAMILY_BASE_LEVEL_OFFSET_PARAM`、`FAMILY_TOP_LEVEL_PARAM`、`FAMILY_TOP_LEVEL_OFFSET_PARAM`；柱顶实际高程必须高于柱底实际高程。
- 默认间距为 6000 mm；“首柱距路线起点”控制第一个柱位置；“路线起点里程”仅用于结果中的 K 桩号计算，不写入模型参数。
- 新增 16/32 像素 `column_along_path` Ribbon 图标。
- 线性布置新增 `ReversePath` 和 `IncludeRemainderEndpoint`；仅固定间距模式需要终点补放，固定数量模式本身包含终点。
- 新增输入设置记忆；路线曲线和 Revit ElementId 不持久化。
- 版本提升至 3.1.14；Ribbon 预期数量由 28 增至 29；安装包脚本与源码基线脚本同步。
- 新增 `tools/Build-Deploy-2024-2025.ps1`、`Build-Deploy-2024-2025.cmd` 和 `编译部署2024和2025.cmd`：关闭 Revit 后依次清理、编译、部署并核对 2024/2025 两套 DLL，避免只部署 2024。

### 静态验证范围

- 当前环境无 Windows/.NET SDK/Revit，尚未执行真实编译和模型放置。
- 已完成 XAML/XML 解析、事件处理器匹配、图标尺寸、Ribbon 引用、源码文本结构和 ZIP 完整性检查。

### 下一步

1. DeepSeek 必须在全新目录分别编译 Revit 2024 和 Revit 2025。
2. **两个年份都必须执行 `DeployAfterBuild=true` 并核对各自 Addins 目录中的 DLL 版本**；不能只部署 2024。
3. 在 Revit 2024 实机测试直线+圆弧连续路线、反转路线、终点余量补柱、Base/Top Level 与 Offset、一次 Ctrl+Z；再复验线性布置两个新增勾选框。

## 2026-07-11｜3.1.13 修复临时插件箱编译阻塞

### 本机编译反馈

- 3.1.12 在 Revit 2024 与 2025 配置下均报告 `CS1626`。
- 原因是 `MicroToolEngine.EnumerateCandidateDlls()` 在带 `catch` 的 `try` 块内使用 `yield return`，这是 C# 编译器明确禁止的迭代器结构。
- 两个目标版本均未生成 DLL，未进入部署和实机测试。

### 已修改

- 将候选 DLL 枚举改为先写入 `List<(string file, bool isPackage)>`，完成收藏包和普通 DLL 扫描后统一返回结果。
- 保留收藏清单异常隔离、受管理收藏包去重、依赖过滤与扫描顺序，不修改临时插件箱业务行为。
- 版本提升为 3.1.13；安装包脚本和源码基线预期版本同步更新。

### 验证边界

- 静态确认目标方法中不再存在 `yield return`，因此消除本次 `CS1626` 触发结构。
- 当前环境没有 .NET SDK，不能替代 Windows 完成真实 2024/2025 编译。
- 下一步只需重新执行双版本编译；0 error 后继续 3.1.12 既定的六功能合并验收。

## 2026-07-11｜3.1.12 临时插件箱、CAD 缓存与项目管理二次审查

### 用户目标

- 恢复“项目微工具”的原始定位：它是按项目使用、项目结束后通常废弃的临时插件箱。
- 默认仓库只用于收藏有复用价值的临时插件和作为转正候选；复制 DLL 到默认仓库不等于真正转正。
- 减少“从 CAD 粘贴”留下的工作副本和历史临时 DWG。
- 审查项目管理区四个窗口及其按钮，修复高确定性的状态、事务和重复窗口问题。

### 临时插件箱

- Ribbon 与窗口名称统一改为“临时插件箱”；普通仓库标记为“临时项目”，默认仓库标记为“收藏 / 候选转正”。
- 新建临时项目默认目录改为 `%APPDATA%\YangTools\TemporaryProjects\<项目名>`；移除项目配置不删除磁盘目录。
- 默认仓库新增“收藏到默认仓库”：复制主 DLL 所在目录的运行文件，排除源码、Revit API 和主插件 DLL，并写入 `microtool.package.json`。
- 收藏或更新采用临时 staging 目录与备份回退，避免覆盖中断后破坏已有收藏包；受管理收藏包可整包删除，普通临时项目仍只删除主 DLL 和同名旁车文件。
- 保留影子复制、依赖解析和多 `IExternalCommand` 入口选择；新增手动清理旧 Revit 会话运行缓存。
- 新增 `MicroTool_Developer_Guide V3.md`，修正旧指南对“内存加载、强制单入口、宿主自动事务组”的过时描述；仍推荐一 DLL 一功能。

### 从 CAD 粘贴

- YangTools 工作副本改为 `%LOCALAPPDATA%\YangTools\Temp\PasteCad\<Revit PID>\<运行 GUID>`。
- 每次创建工作副本前自动清理已结束 Revit 会话中超过一天的缓存；成功或失败后重试删除本次 GUID 目录，并清理空会话目录。
- 窗口新增“清理缓存”：可只清理 YangTools 工作副本，也可同时清理 24 小时前且未被占用的 AutoCAD `A$C*.dwg`；正在使用和新近文件不会删除。
- 清理结果显示目录、文件、空间与跳过数量，失败写诊断日志。

### 项目管理四功能

- 新增 `ModelessWindowRegistry`：同一文档、同一种管理窗口只保留一个实例，重复点击 Ribbon 时激活已有窗口，避免重复 ExternalEvent 队列和陈旧缓存。
- 族文档管理：复制类型自动生成唯一名称；异步操作固定使用点击时的族/类型 ID；RFA 导出改为选择文件夹、过滤非法文件名并确认覆盖；删除后按模型真实类型数量刷新。
- 族实例管理：接入同文档单实例窗口；保留原文档 ExternalEvent、选择后恢复和显示单位参数处理。
- 项目资产管理器：普通视图排除 `ViewSheet`；未使用过滤器同时检查视图样板；线型预览在有效 API 上下文内生成；批量删除使用 `TransactionGroup.Assimilate()`；名称/线宽写入失败回退 UI。
- 图纸管理：刷新清空修订缓存；图号、图名、自定义参数和图框类型写入失败回退 UI；动态图框参数改为 `LostFocus` 提交，避免逐键启动事务。

### 验证边界

- 版本提升为 3.1.12；源码基线脚本增加临时插件箱、CAD 缓存、单实例窗口及项目管理回归检查。
- 当前环境没有 Windows .NET SDK / Revit，未执行 2024/2025 真实编译和六功能实机测试。
- 下一步使用全新目录完成双版本编译；通过后统一验收项目管理四功能、临时插件箱和从 CAD 粘贴。

## 2026-07-11｜3.1.11 修复项目管理窗口编译阻塞

### 本机编译反馈

- 3.1.10 在全新 Windows 环境编译 Revit 2024 与 2025 时均失败。
- 两个目标共报告相同的 8 个源码错误：`Binding`、`ComboBox`、`Visibility` 与 `SolidColorBrush` 的 WPF/Revit 命名空间冲突或缺少限定。
- 本轮严格按错误位置做最小修复，不改业务流程，不进入实机功能测试。

### 已修改

- `SheetManagerWindow.xaml.cs`：动态参数列的 `Binding` 改为 `System.Windows.Data.Binding`；图框类型下拉框改为 `System.Windows.Controls.ComboBox`。
- `FamilyManagerWindow.xaml.cs`：两处可见性枚举改为 `System.Windows.Visibility`。
- `ProjectAssetManagerWindow.xaml.cs`：四处 `SolidColorBrush` 改为 `System.Windows.Media.SolidColorBrush`。
- 版本提升为 3.1.11；源码核验脚本增加上述命名空间冲突的回归拦截。

### 当前验证边界

- 已确认报告中的 8 个错误位置全部消除，并扫描三个目标文件中同类未限定写法。
- 当前审查环境没有 .NET SDK，不能在本机替代 Windows 的真实 2024/2025 编译。
- 下一步只需重新执行双版本编译；0 error 后继续原计划的六功能合并实机验收。

## 2026-07-11｜3.1.10 项目微工具与从 CAD 粘贴优化

### 用户决策

- 暂停 3.1.9 项目管理四功能的单独检测。
- 直接优化“项目微工具”和“从 CAD 粘贴”，完成后六个功能一起做本机编译与实机验收。

### 项目微工具

- 仓库配置由易损的 `projects.txt` 改为 `%APPDATA%\YangTools\micro-projects.json`，并兼容迁移旧配置。
- 默认仓库固定保留；“移除仓库”只删除列表记录，不再自动打开文件夹或暗示删除磁盘内容。
- 增加仓库路径不存在状态、刷新、打开仓库、打开 DLL 位置和上次仓库/工具记忆。
- DLL 扫描支持子目录并过滤 RevitAPI、系统库等常见依赖；显示相对路径、大小和修改时间。
- 影子缓存改为 `%LOCALAPPDATA%\YangTools\MicroToolCache\<Revit进程ID>`，避免多个 Revit 进程争用同一临时目录。
- 影子复制递归保留同目录依赖、资源和 runtimes 子目录，同时排除 RevitAPI/RevitAPIUI/YangTools 主 DLL。
- 增加 `AssemblyResolve` 依赖解析、`ReflectionTypeLoadException.LoaderExceptions` 完整诊断和日志路径。
- 一个 DLL 包含多个 `IExternalCommand` 时不再任意执行第一个，改为弹出入口选择窗口。
- 删除工具只删除主 DLL 及同名 PDB/XML/config/deps/runtimeconfig，不误删共享依赖 DLL。
- 新建仓库改用真实文件夹选择器，移除 Topmost 和局部重复按钮样式。

### 从 CAD 粘贴

- 临时 DWG 检测集中到 `CadClipboardFileHelper`，只扫描真实临时目录并去重，最多列出 20 个候选文件。
- UI 支持选择多个候选、重新检测和手动选择；超过 30 分钟的文件显示明确“较早”警告，避免误导入历史 A$C 文件。
- 复制 AutoCAD 临时文件时允许源文件被读写/删除共享；复制前后校验长度与修改时间，变化时自动重试。
- 工作副本改为 `%LOCALAPPDATA%\YangTools\Temp\PasteCad\<GUID>`，避免固定文件名冲突和并行 Revit 会话互相覆盖。
- 导入单位、颜色、定位和“仅当前视图”写入 `settings.json`；三维视图自动禁用视图专用导入。
- 使用 `UIApplication.MainWindowHandle` 设置窗口 Owner；增加只读文档和视图样板保护。
- 事务改为显式检查 Start/Commit 状态；失败回滚并写诊断日志；成功后自动选中新建 ImportInstance，保持一次 Ctrl+Z 撤销。
- 重写两个窗口及新建仓库窗口，统一 SharedStyles 和标题栏拖动范围。

### 新增文件

- `Core/CadClipboardFileHelper.cs`
- `Models/MicroToolModels.cs`
- `UI/MicroCommandPickerWindow.xaml`
- `UI/MicroCommandPickerWindow.xaml.cs`

### 当前验证

- 当前环境无 Windows .NET SDK / Revit，未执行真实编译和实机导入。
- 已完成 XAML/XML、事件处理器、资源键、UTF-8、版本、Ribbon 数量和基础 C# 结构静态检查。
- 下一次本机验收合并覆盖：3.1.9 项目管理四功能 + 项目微工具 + 从 CAD 粘贴。

# YangTools 工作日志

只记录会影响后续开发的变更。早期日志存在严重乱码、重复和无法核验的完成声明，已在 3.0 整理时移除。AI 每轮收尾必须追加实际修改、验证范围、未验证项和下一步。

## 2026-07-11｜3.1.9 项目管理四功能第一轮架构与参数修复

### 上一阶段结论

- 用户确认 3.1.8 的实体生成（Loft）和实体提取已经成功。
- 模型修改区六个核心功能完成当前阶段最小实机确认，暂时收口，不继续追加无实际反馈的优化。

### 确定性问题

- 族文档管理、族实例管理、项目资产管理器和图纸管理均为非模态或需要持续交互的窗口，但部分 UI 事件在外部命令返回后直接读取 Revit API，违反非模态窗口必须通过 ExternalEvent 的线程边界。
- 通用 `RevitEventHandler` 会给每个操作自动包一层项目 `TransactionGroup`，而四个管理器内部已经自行管理事务；族编辑、族保存和 `LoadFamily` 在目标项目处于可修改状态时容易失败。
- 窗口持有原项目中的 ElementId，却在执行时使用当前活动文档；用户切换项目后存在把旧 ID 应用到新项目的风险。
- 多处 Excel/表格使用 `AsValueString()` 导出显示值，却用 `double.Parse + Parameter.Set(double)` 写回，可能把“1000 mm”错误当成 Revit 内部英尺或直接解析失败。
- 图纸管理的图框参数缺少值变更回调，表格看似可编辑但没有写回；动态图框列刷新时可能重复累积。
- 图框类型下拉框直接把 Revit `FamilySymbol` 对象绑定到非模态 WPF，窗口渲染和选择事件会在 API 上下文外读取 Revit 对象。

### 已修改

- `RevitEventHandler` / `RevitReadEventHandler` 增加可选的原文档绑定、文档关闭/切换保护和诊断日志；项目管理窗口禁用自动外层 TransactionGroup与自动成功弹窗，继续由各业务操作自行管理事务。
- 四个管理窗口统一建立文档绑定的读/写 ExternalEvent，并在窗口关闭时释放；模型窗口事件中的 Revit 读取迁移到只读队列。
- 族实例管理主窗口改为非模态 `Show()`；选择图元后使用 `Show()/Activate()` 恢复，避免再次进入模态循环。
- 新增 `ParameterValueHelper`：字符串直接写入；Double/Integer 使用 `SetValueString()` 按当前项目显示单位解析；通用 ElementId 参数只读，避免用名称或数字误写关联类型。
- 族文档管理的类别修改、参数列、Excel 导入和族导出改用正确 ExternalEvent/事务边界；族文档保存/载入时不再保持项目外层 TransactionGroup。
- 项目资产管理器的数据加载改为真正的只读路径；标签页、树节点、过滤器分析、颜色对话框、勾选删除预读取均通过 ExternalEvent；线型复制名称在 API 上下文内计算。
- 图纸管理修复自定义图纸参数和图框参数写回；修订编辑和参数列读取迁移到只读 ExternalEvent；图框动态列先移除上一轮列；新增 `TitleblockTypeOption` DTO，图框类型选择只保存 ElementId 和显示名，ExternalEvent 内再解析 FamilySymbol。
- 三个命令增加“未打开项目”保护；版本提升为 3.1.9，核验脚本增加项目管理 ExternalEvent、单位参数和图框 DTO 回归检查。

### 静态验证

- 28 个 Ribbon 命令保持不变。
- 全部 XAML/XML 可解析，XAML 事件处理器缺失为 0。
- 全部 C# 文件通过字符串、注释与括号轻量词法检查。
- 四个项目管理窗口均使用绑定原文档的读/写 ExternalEvent。
- 图纸管理不再存在 `List<FamilySymbol> AvailableTitleblocks` 直接绑定。
- 当前环境无 Windows .NET SDK / Revit，3.1.9 尚未真实编译和实机运行。

### 下一步

1. 全新目录编译 Revit 2024/2025并干净部署 2024。
2. 对族文档管理、族实例管理、项目资产管理器、图纸管理各做一条最小读取和一条最小写入路径。
3. 重点验证显示单位参数、Excel 回写、图框换型、族导出/类别修改和切换项目保护；根据真实错误再修复，不做大规模 UI 重构。

## 2026-07-11｜3.1.8 Revit 2024 类别枚举修复、可选材质与输入记忆

### 用户实机反馈

- Revit 2024 打开“实体生成（Loft）”时窗口初始化失败：`Enum underlying type and the object must be same type`，实际枚举底层类型为 `Int64`，旧代码却向 `Enum.IsDefined` 传入 `Int32`。
- 用户要求实体生成和实体提取的材质允许留空；留空时使用默认/按类别材质。
- 用户要求布尔几何、实体生成（Loft）、实体提取、线性布置记住上次输入，减少重复填写。

### 根因与修复

- 新增 `BuiltInCategoryHelper`：根据 `Enum.GetUnderlyingType(typeof(BuiltInCategory))` 在运行时转换类别 ID，兼容 2021–2023 的 32 位枚举与 2024+ 的 64 位枚举；两个生成窗口不再直接调用带错误数值类型的 `Enum.IsDefined`。
- 材质输入改为可编辑下拉框，列出当前项目材质；留空时不创建 `Structural Material` 族参数，也不关联 `MATERIAL_ID_PARAM`，生成实体使用族类别/模板默认材质；填写不存在的材质会在生成前明确提示。
- `UserSettings` 从单行 `settings.txt` 升级为 `%APPDATA%\YangTools\settings.json`，并兼容迁移旧模板路径。
- 记忆范围：
  - Loft：模板、族名、材质、类别、标高名、Void/剪切选项。
  - 实体提取：模板、族名、材质、类别、标高名。
  - 线性布置：族/类型名、距离/参数模式、间距/数量模式、起点、终点、间距/数量、方向、垂直和旋转偏移。
  - 布尔几何：删除目标实例、删除目标族两个开关。
- 不保存轮廓、源图元、布尔目标或路径曲线等 Revit 对象，避免跨项目复用失效 ElementId。
- 版本提升为 3.1.8，源码核验脚本增加类别转换、可选材质和输入记忆检查。

### 当前状态

- 已完成静态修改与 XAML/源码一致性检查。
- 当前环境无 Windows .NET SDK / Revit，3.1.8 尚未真实编译。
- 必须由 DeepSeek 在全新目录编译 2024/2025，并在 Revit 2024 先确认两个窗口能正常打开，再继续 Loft/实体提取最小样本测试。

## 2026-07-10｜3.1.7 Loft / 实体提取从头审查与确定性 Bug 修复

### 复审结论

- 3.1.6 的“只把 `IsExecute` 换成 `DialogResult`”并没有解决根因。两个窗口仍由 `ShowDialog()` 打开，却在“选择轮廓/选择图元”事件内执行 `Hide()`、Revit `PickObject(s)`、再 `Show()`。模态窗口被隐藏后，其 `ShowDialog()` 返回链已被结束/置空；重新 `Show()` 只是模型窗口，外部命令会得到取消结果并静默退出。
- 两个引擎都在项目 `TransactionGroup` 已启动时创建并 `SaveAs` 临时族文档。Revit 不允许插件在仍有未关闭 Transaction / SubTransaction / TransactionGroup 时执行保存，这是窗口链修好后必然遇到的第二个阻塞。
- 当时把 Revit 2024+ 的 `BuiltInCategory` 底层类型误判为 `Int32`，因此采用了把 `cat.Id.Value` 强转 Int32 的方案；3.1.8 的 Revit 2024 实机错误证明该枚举底层实际为 `Int64`，此处结论已被后续版本纠正。

### 已修改

- 新增 `GeneratorDialogAction`，两个参数窗口只负责保存状态并关闭：`SelectInput`、`Generate`、`Cancel`。
- `EntityGeneratorCommand` / `SolidGeneratorCommand` 改为循环状态机：显示模态窗口；窗口请求选择时先关闭；由仍处于 Revit 外部命令上下文的命令执行 `PickObject(s)`；保存选择后重开窗口；只有 `Generate + DialogResult=true` 才进入引擎。
- 两个窗口彻底删除 `Hide()/Show()` 和窗口内部 Revit Selection 调用，保留所有输入状态、选择结果和明确日志。
- 两个引擎拆成两阶段：先在无项目事务状态下提取几何、创建族文档、提交族事务并保存 `.rfa`；保存完成后才启动项目 `TransactionGroup` 载入和放置，使项目修改仍可一次撤销。
- Loft 轮廓按端点排序和闭合校验；等边数轮廓做起点/方向对齐，不再强制所有轮廓边数相同，也不再随意翻转 CurveLoop 法向。
- 实体提取的任何 Solid 变换或 FreeForm 创建失败均整体终止，防止残缺族。
- 当时尝试把 Revit 2024+ 类别 ID 限制到 `Int32` 后调用 `Enum.IsDefined`；该修复在 3.1.8 被实机证明错误并替换为按运行时枚举底层类型转换。
- 源码核验脚本增加两功能回归检查：窗口内不得出现 `Hide()` 或 `PickObject(s)`；命令必须包含选择状态机；族 `SaveAs` 必须出现在项目 `TransactionGroup` 之前。
- 版本提升为 3.1.7。

### 静态验证

- 两个窗口中 `Hide()`、`Show()`、`PickObject(s)` 引用均为 0。
- 两个命令均存在 `GeneratorDialogAction.SelectInput` 状态处理和外部命令 Selection 调用。
- 两个引擎的族 `SaveAs` 均在项目 `TransactionGroup` 启动前完成。
- XAML/XML 解析、XAML 事件处理器映射、UTF-8 解码、括号/字符串轻量检查通过。
- 当前环境无 Windows .NET SDK / Revit，不能宣称 3.1.7 已编译或两个功能已实机通过。

### 下一步

1. 全新目录编译 Revit 2024/2025并干净部署 2024。
2. 先测试“选择后窗口是否重开、点击生成是否进入引擎且必有结果或日志”。
3. 使用两个矩形轮廓和一个简单立方体做最小几何测试；根据真实日志继续修复，不再修改按钮样式或使用 PreviewMouse 绕过。

## 2026-07-10｜3.1.6 Loft / 实体提取初次修复（后被 3.1.7 复审证明不完整）

### 实机反馈与根因

- 用户确认模型修改区六项中，线性布置、标高修改、基于面转换、布尔几何已完成最小实机测试；Loft 与实体提取点击“生成”后无任何结果或错误提示。
- 两个命令都采用 `ShowDialog() + window.IsExecute` 自定义握手。只要按钮事件、保存状态或关闭过程未完整走到 `IsExecute=true`，命令就直接返回 `Result.Cancelled`，因此表现为完全无反应。
- 两个窗口还在整个 Window 上绑定 `MouseDown -> DragMove()`，并各自复制按钮模板；该组合会增加鼠标路由和模板行为差异。仅把按钮从 AccentButton 改为 FlatButton或改用 PreviewMouse 不能消除脆弱的 IsExecute 执行链。

### 已修改

- `EntityGeneratorCommand`、`SolidGeneratorCommand` 改用 WPF 标准 `bool? dialogResult = ShowDialog()`；只有 `DialogResult == true` 才进入引擎，不再依赖 `IsExecute`。
- 两个窗口的生成按钮设为 `IsDefault=true`，取消按钮设为 `IsCancel=true`；确认时先完整校验并保存状态，再设置 `DialogResult=true`。
- 删除两个窗口复制的 `FlatButton/AccentButton` 模板，统一使用全局 `StandardButton/PrimaryButton`；窗口拖动事件只绑定标题栏，不再绑定整个 Window。
- 新增 `FamilyTemplateResolver`：兼容设置中保存的是 `.rft` 文件或模板目录，并搜索中英文常规模型模板。生成前强制确认路径是真实 `.rft`，不允许把目录传给 `NewFamilyDocument`。
- 新增 `DiagnosticLog`，关键执行节点和完整异常写入 `%LOCALAPPDATA%\YangTools\Logs`；失败弹窗显示错误摘要和日志路径，禁止再次静默失败。
- Loft 引擎增加轮廓曲线递归提取、首尾排序和断线/多环错误说明；族类型、类别、偏移和材质关联失败均明确回滚。
- 实体提取不再吞掉 Solid 变换或 FreeForm 创建异常；任一实体失败时整体回滚，避免生成缺失几何的残缺族。
- 版本提升为 3.1.6，源码核验和安装包脚本同步。

### 静态验证

- 两个命令和窗口中 `IsExecute` 引用为 0。
- 生成按钮均使用 `DialogResult` 标准返回，窗口级 `MouseDown` 引用为 0。
- 两个窗口局部 `FlatButton/AccentButton` 定义与引用为 0。
- XAML/XML 解析、事件处理器映射、StaticResource 定义、UTF-8 解码与轻量 C# 词法检查通过。
- 当前环境无 Windows .NET SDK / Revit，3.1.6 尚未完成真实编译和 Revit 复验。

### 下一步

1. 在全新目录编译 2024/2025 并干净部署 2024。
2. 只复验 Loft 与实体提取：点击生成必须进入引擎；失败必须弹窗并生成日志。
3. 先验证执行链，再根据日志处理真实几何/族模板问题；不再使用 PreviewMouse 或移除整个 WPF 窗口的绕路方案。

## 2026-07-10｜3.1.5 模型修改区六功能第一轮源码修复

### 3.1.4 正式验收基线

- DeepSeek 在全新目录核对 `YangTools_Project_3.1.4_Source_CLEAN.zip`，SHA-256 与交付值一致。
- 源码基线：版本 3.1.4、28 个 `RibbonButton`，三个废弃命令、旧设置窗口和六个废弃图标均不存在。
- Revit 2024 编译：0 error、498 warnings；Revit 2025 编译：0 error、566 warnings。
- Revit 2024 干净部署 DLL 为 3.1.4；Journal 实际注册 28 个 PushButton，三个废弃入口全部消失。
- `Verify-SourceBaseline.ps1` 在 Windows PowerShell 5.1 出现编码相关执行问题，但人工核验与编译结果均通过。本版本把核验脚本改为纯 ASCII 输出，并增加英文启动器 `Verify-SourceBaseline.cmd`。

### 已修改：模型修改区

- `LinearPlacementEngine`：`GeometryInstance.GetInstanceGeometry()` 已返回项目坐标几何，不再重复应用实例 Transform；补充 CAD `PolyLine` 分段提取。
- `LevelModifierEngine`：对具有起点/终点标高的 MEP 曲线，同时读取和修改 `RBS_START/END_LEVEL_PARAM` 与 `RBS_START/END_OFFSET_PARAM`，保持两端绝对高度和原坡度；修正起点标高对应偏移参数映射。
- `FaceBasedConverterEngine/Window`：只把 `FamilyPlacementType.WorkPlaneBased` 视为基于面/工作平面族，避免门窗等普通宿主族误入；每个原始类型生成独立新族，避免同一家族多类型覆盖；只删除已成功生成替代实例的旧实例；补充载入、类型和标高校验。
- `EntityGeneratorEngine`：Loft 几何以首轮廓 XY 和最低 Z 为局部原点写入族；放置时实际使用用户选择的目标标高，并以实例偏移恢复原世界高度；修复族类型空引用风险。
- `SolidGeneratorEngine`：修复嵌套 `GeometryInstance` 二次变换；计算 Solid 包围框时应用 `BoundingBoxXYZ.Transform`；全部成功创建的 FreeFormElement 都关联材质参数；按用户选择标高放置并恢复偏移；没有成功创建实体时明确失败。
- `BooleanGeometryCommand`：禁止无目标、主体重复作为目标、同一目标跨多种操作重复选择；执行前提示会重写整个主体族并固化参数化几何；目标族与主体族相同时禁止误删主体族。
- 新增 `FamilyFileHelper`：生成族使用独立临时目录和稳定族文件名，避免并发/同名覆盖，并统一清理。

### 版本与脚本

- 版本提升为 3.1.5；安装包脚本同步更新。
- `Verify-SourceBaseline.ps1` 改为 ASCII 内容和明确 XML 节点读取，减少 Windows PowerShell 5.1 编码差异。
- 新增 `Verify-SourceBaseline.cmd`，保留 `核验源码.cmd` 作为同功能入口。

### 静态验证

- 全部 106 个 C# 文件通过轻量词法检查：字符串、字符、注释和括号均闭合。
- 全部 XAML/XML/csproj 可解析；全部受检文本可按 UTF-8 解码。
- RibbonButton 仍为 28；废弃入口和图标残留为 0。
- 当前环境无 Windows .NET SDK 和 Revit，3.1.5 未完成真实 C# 编译，也未执行模型操作。

### 下一步

1. 在全新目录运行源码核验，编译 Revit 2024 与 2025。
2. 干净部署 Revit 2024，在项目副本按“线性布置 → 标高修改 → Loft → 实体提取 → 基于面转换 → 布尔几何”顺序做最小测试。
3. 只修阻止编译或造成错误模型结果的明确问题；通过后再进入项目管理区四个功能。

## 2026-07-10｜3.1.4 防止覆盖解压旧源码重新参与编译

### 问题复核

- 直接解压并检查交付的 `YangTools_Project_3.1.3_Source.zip`，SHA-256 为 `dcdf61baad2347ed8d8d2f077b20f367ed8e80c13a7e91253cbe9fb0ce7adf7d`。
- 包内带 `RibbonButton` 的命令实际为 28 个；`HelloWorldCommand`、`McpStatusCommand`、`RibbonSettingsCommand`、旧设置窗口及六个废弃图标均不存在。
- DeepSeek 多轮都在同一个 `E:\WorkBuddy\YangTools Sandbox` 目录工作。把新 ZIP 覆盖解压到旧目录不会删除旧版已移除文件，SDK 项目又会默认编译目录中的所有 `.cs`，因此三个旧命令被继续编译，Journal 才仍显示 31 个按钮。
- 该报告验证的是被旧文件污染的工作目录，不是 3.1.3 源码包本身。

### 已修改

- 版本提升为 3.1.4。
- `YangTools.Revit.csproj` 新增构建前陈旧文件检测。发现三个废弃命令、旧设置窗口或六个旧图标时直接终止构建，并提示删除整个目录后重新解压。
- 新增 `tools/Verify-SourceBaseline.ps1` 与根目录 `核验源码.cmd`：检查版本、28 个 Ribbon 命令、废弃文件不存在以及系统设置依赖文件存在。
- `PROJECT.md` 和 `README.md` 增加硬规则：每个源码版本必须解压到全新的空目录，禁止覆盖旧目录。
- 安装包脚本版本同步到 3.1.4。

### 静态验证

- 3.1.4 工作树 RibbonButton 数量：28。
- 三个废弃命令、旧设置窗口及六个废弃图标残留：0。
- 系统设置 `SampleWindowCommand`、Hello 页面和 MCP 状态页面仍保留。
- 项目 XML 与全部 XAML/XML 解析通过。
- 当前环境没有 Windows .NET SDK，新增 MSBuild 拦截和 PowerShell 核验脚本仍需 Windows 执行确认。

### 下一步

1. 把 3.1.4 解压到一个从未使用过的新目录，先运行 `核验源码.cmd`。
2. 重新编译 2024/2025；若旧文件仍存在，构建必须明确失败，而不是生成 31 按钮 DLL。
3. 干净部署 2024，用户人工确认 Ribbon 为 28 个命令，并打开系统设置和重点窗口。

## 2026-07-10｜3.1.3 系统管理入口收敛与验收记录纠偏

### 核对结论

- 直接检查 3.1.2 源码包：`RibbonSettingsCommand.cs`、`RibbonSettingsWindow.xaml`、`RibbonSettingsWindow.xaml.cs` 均不存在，旧版面板设置已正确删除。
- 3.1.2 源码中实际存在 30 个带 `RibbonButton` 的命令。DeepSeek 报告中“旧设置文件仍存在、因此为 31 个按钮”的判断与源码不符，不能作为项目事实。
- DeepSeek 完成 3.1.2 的 Revit 2024/2025 编译：均为 0 error；2024 为 249 个警告，2025 为 283 个警告；Revit 2024 完成干净部署。其 CLI 环境未实际点击业务窗口，故不能据此宣称窗口实机通过。

### 已修改

- 删除独立“你好，Revit”Ribbon 命令 `HelloWorldCommand.cs`。Hello 验证页继续保留在“系统设置”中。
- 删除独立“MCP 状态”Ribbon 命令 `McpStatusCommand.cs`。MCP 状态页继续保留在“系统设置”中。
- 删除“你好，Revit”和“MCP 状态”专用的 4 个 Ribbon 图标，并清理旧版面板设置遗留的 2 个无引用图标。
- Ribbon 命令由源码实际的 30 个减少为 28 个。
- 版本提升为 3.1.3，并同步安装包脚本、项目状态与功能说明。

### 静态验证

- 带 `RibbonButton` 的命令数：28。
- `HelloWorldCommand` / `McpStatusCommand` 代码引用残留：0。
- `hello_16/32.png`、`mcp_server_16/32.png`、`ribbon_settings_16/32.png` 图标与引用残留：0。
- “系统设置”中的 Hello 页和 MCP 状态页仍保留。
- 所有 XAML/XML 可解析；StaticResource 未定义引用为 0。
- 当前环境不能运行 Windows 编译或 Revit，3.1.3 尚待本机编译、干净部署和人工点击复验。

### 下一步

1. 编译 3.1.3 的 Revit 2024 与 2025。
2. 干净部署 Revit 2024，确认 Ribbon 不再出现“你好，Revit”和独立“MCP 状态”，系统设置内对应页面仍可用。
3. 用户人工打开六个重点窗口，确认 3.1.2 修复的三类 XAML 异常在实际交互中消失。

## 2026-07-10｜3.1.2 Revit 2024 XAML 运行时修复与旧设置入口删除

### 实机反馈与判断

- DeepSeek 完成 3.1.1 编译：Revit 2024 与 2025 均为 0 error；2024 为 249 个警告，2025 为 566 个警告。Revit 2024 启动和 31 个按钮注册成功。
- 用户在打开实际窗口时报告：`StaticResourceExtension`、`ResourceDictionary.Source`、`Style` 转 `Brush` 等运行时异常。该反馈证明“编译通过、按钮注册成功”不等于窗口 XAML 可用。

### 已修改

- 删除 `ViewGraphicCleanerWindow.xaml` 中未使用且与主题画刷同名的 `TitleText` 局部 Style。该键会遮蔽主题中的 `TitleText` Brush，直接导致 `Unable to cast object of type Style to Brush`。
- 删除批处理、族文档管理、族实例管理、图纸管理窗口中重复的 `ResourceDictionary Source="SharedStyles.xaml"`。共享样式已经由 `ThemeManager` 在 Application 级加载，局部重复加载既无必要，也会引发窗口初始化阶段的 Source/StaticResource 异常。
- 删除已被“系统设置”完整替代的旧版面板设置：`RibbonSettingsCommand`、`RibbonSettingsWindow.xaml` 及其代码后置文件。Ribbon 命令预计由 31 个降为 30 个。
- 修复开发部署脚本：`DeployAfterBuild=true` 现在同时复制 `UI/SharedStyles.xaml` 和 `UI/Themes/*.xaml`，避免干净部署时只复制 DLL/图标而缺少主题字典。
- 版本提升为 3.1.2，安装包脚本和项目文档同步更新。

### 静态验证

- 全部保留 XAML/XML 解析通过。
- 业务窗口中的 `ResourceDictionary Source="SharedStyles.xaml"` 残留 0。
- 主题 Brush 键与窗口局部 Style 键的类型冲突残留 0。
- `RibbonSettingsCommand` / `RibbonSettingsWindow` 引用残留 0。
- 当前环境没有 Windows .NET SDK 与 Revit，3.1.2 尚未编译和实机复验。

### 下一步

1. DeepSeek 重新编译并部署 Revit 2024 的 3.1.2。
2. 只复验系统设置、覆盖清理、族文档管理、族实例管理、图纸管理、批处理与云链接六个入口。
3. 确认上述 XAML 异常消失后，再进入主题视觉细节和重点业务功能测试。

## 2026-07-10｜3.1.1 接手基线、动态主题修复与 2024 编译记录

### 接手与验证

- 接收 `YangTools_Project_3.1_Source(2).zip`，完成只读结构审计：37 个 Ribbon 命令、36 个 XAML、4 套主题、MCP 接收器、批处理模块和免管理员安装脚本均在源码中。
- 记录 DeepSeek 的 Windows 编译结果：`.NET SDK 8.0.403`，命令 `dotnet build .\src\YangTools.Revit\YangTools.Revit.csproj -c 2024 -p:Platform=x64 -p:DeployAfterBuild=false`，结果 0 error、498 nullable 警告，DLL 位于 `bin\x64\2024\YangTools.Revit.dll`。
- 该结果只作为 Revit 2024 编译基线；尚未验证 2025、其他年份、安装包或 Revit 实机功能。

### 已修改

- 修复主题切换残留：将业务窗口中的 45 处主题画刷 `StaticResource` 改为 `DynamicResource`，重点覆盖族文档管理、族实例管理和图纸管理三个大窗口。
- 修复 `ViewGraphicCleanerWindow.xaml` 把 `TitleText` 画刷错误绑定到 `TextBlock.Style` 的问题，改为正确的 `Foreground`，避免窗口加载时发生资源类型错误。
- 新增跨主题统一的 `AccentText` 资源；主操作、成功和危险按钮不再复用标题栏文字色，改善苹果浅色主题下的按钮对比度。
- 清除 `NewProjectDialog.xaml` 中最后一处硬编码 `White`。
- 将上传包中被转义的两个启动脚本文件名规范为 `安装.cmd` 和 `卸载.cmd`。
- 版本提升至 `3.1.1`，项目文档同步记录 DeepSeek 编译基线和未验证范围。

### 静态验证

- 38 个 XAML/XML 文件解析通过，0 个错误。
- 4 套主题均包含 75 个一致资源键，缺失 0。
- 业务 XAML 中主题画刷 `StaticResource` 残留 0。
- 业务 XAML 中白/黑/灰/十六进制硬编码颜色残留 0。
- `Style` 属性误引用主题画刷残留 0。
- 全部文本文件可按 UTF-8 解码，Unicode 替换字符残留 0。
- 当前环境没有 .NET SDK，3.1.1 修改后尚未重新编译。

### 已发现但本轮未改

- Python MCP 桥默认固定连接 8081，而 C# 接收器会在 8081–8090 自动选端口；8081 被占用时两端可能失联。
- MCP 目前实际只实现 `eval_python`，`run_revit_command` 的预设命令仍返回未实现。
- 498 个 nullable 警告数量过高，不应长期视为完全无影响；先按启动路径和重点功能逐步收敛，不做一次性清零。

### 下一步

1. 由 DeepSeek 编译 3.1.1 的 Revit 2024 与 2025，只修真实编译错误，不处理全部 nullable 警告。
2. 在 Revit 2024 验证四套主题切换，以及覆盖清理、族文档管理、族实例管理、图纸管理窗口能否正常打开并实时换色。
3. 通过后修复 MCP 动态端口发现，再进入模型修改区 6 个功能的最小实机验收。

## 2026-07-09｜3.1 UI、重点功能与文档基线

### 已修改

- 修复主题切换根因：`ThemeHelper` 不再向每个 Window 注入一份旧主题字典，主题统一由 Application 级动态资源控制。
- 扩充 `SharedStyles.xaml`：增加 Window/Page 和常用控件默认主题样式，以及统一标题、字段标签、关闭按钮、卡片和底栏。
- 完整重写 5 个已损坏的中文 XAML：
  - `TextModifierWindow.xaml`
  - `ProjectAssetManagerWindow.xaml`
  - `ChineseCheckWindow.xaml`
  - `ProjectInfoWindow.xaml`
  - `PasteCadWindow.xaml`
- 去除重点窗口的白色/灰色/状态色硬编码；状态改用 `SuccessColor`、`DangerBg` 等主题资源。
- 删除未使用的 `assistant_16/32.png` 和伪装为 PNG 的 1024×1024 头像资源；保留现有蓝/棕双色 Ribbon 图标体系。
- 模型修改区修复：
  - 线性布置先判断水平切线是否为零，再 Normalize。
  - 标高修改只接受 Double 类型的偏移参数。
  - 布尔几何不再把未相交剪切体作为最终实体输出。
  - 将第二个“实体生成”按钮改名为“实体提取”，明确与 Loft 的区别。
- 项目微工具修复：窗口可拖动；影子目录会复制同目录 DLL/PDB/JSON/config，支持带依赖的微工具且不锁原文件。
- AI 助手修复：获取模型和测试连接改为异步；移除未生效的 MCP URL 设置；AI Python 执行后等待并回显结果。
- MCP 状态修复：服务公开真实 `IsRunning/Port/ServerUrl`，设置页不再写死“运行中、8081”；删除未使用的独立 MCP 状态窗口。
- MCP 接收器由可能要求 Windows URL ACL 的 `HttpListener` 改为仅绑定 `127.0.0.1` 的 `TcpListener`，保持免管理员安装；增加 4 MB 请求上限和 60 秒执行超时。
- 关于页版本改为读取程序集版本；版本提升为 3.1.0。
- 文档收敛为 `PROJECT.md + WORKLOG.md`，README 只保留入口。

### 静态验证

- 5 个乱码 XAML 已不再包含 Unicode 替换字符。
- 38 个 XAML/XML 文件全部解析通过，0 个错误。
- XAML 事件处理器核对通过，0 个缺失。
- `StaticResource` 引用核对通过，0 个未定义资源。
- 业务 XAML 中硬编码白/黑/灰/十六进制颜色为 0。
- 60 个 Ribbon PNG 均被实际命令引用，缺失 0、闲置 0。
- 全部文本文件可按 UTF-8 解码，Unicode 替换字符残留 0。
- 当前环境无 .NET SDK，未执行 C# 编译。
- 当前环境无 Revit，未进行模型操作或界面实机测试。

### 下一步

1. 交给 DeepSeek 在 Windows 先编译 Revit 2024，只修编译错误。
2. 编译通过后生成 2024 payload，完成主题切换和重点窗口启动验收。
3. 再按模型修改区 → 项目管理区 → CAD/批处理/AI/MCP 顺序做最小实机验收。

## 2026-07-09｜3.0 项目接管与清理

- 明确个人、非商业、非开源项目定位，减少无价值的企业级流程。
- 明确 Revit 2021–2027 必须逐年份编译：2021–2024 为 .NET Framework 4.8，2025–2026 为 .NET 8，2027 为 .NET 10。
- 删除仓库中的 2022–2024 旧编译产物；这些产物既不完整，也不能证明七版本通用。
- 删除三套互相冲突的旧安装器：根目录部署脚本、Inno Setup、两个 C# 安装器项目。
- 新增唯一的无管理员权限安装/卸载脚本，目标为当前用户 AppData。
- 新增全版本构建和发布包生成脚本；发布包只接受对应年份的真实构建产物。
- 普通构建不再自动覆盖本机 Revit 插件，开发者必须显式启用部署。
- 重写 README、开发说明、项目状态和工作日志；删除过期用户手册、重复开发指南和乱码内容。
- 当前环境没有 .NET SDK 和 Revit，完成了静态检查与脚本结构检查，尚未完成 Windows 全版本编译及 Revit 实机回归。

## 3.0 阶段留下的下一步（已由 3.1 计划接管）

1. 在 Windows 开发机运行 `tools\Build-All.ps1`，先解决所有年份的编译错误。
2. 运行 `tools\Package-Installer.ps1 -SkipBuild` 生成七版本安装包。
3. 用 Revit 2024 完成一次安装、启动、核心按钮、卸载验收。
4. 只有遇到版本专属 API 报错时，再补对应条件编译；不提前做大规模重构。


## 2026-07-29｜3.1.35 临时插件说明体系与功能审查

### 已修改

- 新增通用 `FeatureHelpWindow`，支持主题、文字选择、Ctrl+C 和复制全部。
- 临时插件箱新增顶部说明按钮、所选插件功能/使用说明卡、插件说明、创建说明模板和开发规范入口。
- 新增 `<DLL名>.microtool.json` / `microtool.json` / `microtool.package.json` 说明读取；功能说明和使用步骤不完整时禁止运行与收藏。
- 收藏包同步保存说明字段；新增示例 `docs/templates/SampleTool.microtool.json`。
- `MicroTool_Developer_Guide V3.md` 内容升级至 V3.1，并加入构建输出 `Docs`。
- 补齐直接 MSBuild 部署目标的 `Docs` 复制规则，确保开发规范与说明模板随插件进入 Revit Addins 目录。
- “等距分布”改为六个明确模式，说明真实按 `TextNote.Coord` 插入点处理；补充排序和视觉间距限制。
- “对齐文本”改名/说明为“基准对齐”；每个目标使用独立子事务，失败目标跳过并统计。
- 中文检查改为只读全量范围：取消删除、取消 5000 截断、纳入共享参数/项目环境、扩展 CJK、排除数值 `AsValueString()` 假阳性、增加定位与复制 ID。
- 面板设置取消关闭时静默保存；系统设置固定显示；禁止空主区；支持全选/恢复默认；配置完整快照原子写入和备份。
- 中文检查、系统设置、标注替换、线性布置等窗口 Owner 统一使用 Revit `MainWindowHandle`。

### 静态验证

- 246 个文本文件、144 个 C#、40 个 XAML、35 个窗口/视图根节点、4 套主题、28 个 Ribbon 命令。
- XAML/XML、资源键、事件处理器、主题初始化、UTF-8：错误 0，警告 0。
- 临时插件说明清单示例 JSON 解析通过；文本模式映射与面板配置约束完成纯逻辑检查。

### 未验证

- 当前环境没有 Windows .NET SDK 和 Revit，未执行 2021–2027 七配置真实编译。
- 未在七个 Revit 版本逐个打开 28 个入口并完成模型操作回归。
- 中文检查在超大模型中的实际扫描耗时、临时 DLL 跨依赖加载、系统设置重启后 Ribbon 结果仍需实机确认。

## 2026-08-07｜3.1.35 批处理、模型修改区与项目管理区二次复核

### 已修改

- 新增 `ExternalLinkPathMatcher`，统一匹配本地/Desktop Connector 与 `Autodesk Docs://` 链接路径；要求文件名及至少两级父目录相同，避免同名文件误判。
- Revit/CAD “跳过已存在链接”改用统一匹配器。
- 合并 PDF 覆盖改为临时导出、备份旧文件、成功替换和失败恢复，导出失败不再先删除旧 PDF。
- 布尔 Union 失败后检查交集；相交或交集关系不确定时抛错回滚，避免输出重叠实体。
- 族实例、族类型、项目资产、图纸 Excel 导入改为 `TransactionGroup + 每行 Transaction`，显示成功/失败汇总和主要原因。
- 图纸导入不再直接按图号 `ToDictionary`；同号对应多个已有图纸时明确报告歧义。
- 族实例、项目资产、图纸普通删除统一增加确认，并检查预检、提交和事务组合并状态。
- 图框快速连续换型增加待处理实例守卫，失败时同步恢复 VM 和 ComboBox。
- 新增 `tools/Test-BatchLinkPathMatching.py`；扩展 `Test-ReviewFixes.py` 至 54 项契约。

### 复核但未改

- `Commit()!=Committed` 不等于文档必然持续可修改；现有代码只在状态仍为 Started 时显式回滚并记录状态。
- 多文档按名称匹配是安全设计，不自动回退当前视图。
- `familyDoc.LoadFamily(projectDoc, ...)` 不应额外包项目 Transaction。
- 按钮 IsEnabled、窗口缩放和广泛 Debug 日志清理属于一般 UX/维护项，不扩大为本轮发布阻塞重构。

### 静态验证

- UI/XAML/资源/事件/UTF-8：0 错误、0 警告。
- 本轮复核契约：54/54。
- 路径匹配组合：6/6。
- 段落宽度：15/15；Loft/实体提取标高：16/16；重复参数模拟链路：通过。

### 未验证

- 无 Windows/.NET SDK/Revit，未执行真实编译。
- ACC/Desktop Connector 路径、PDF 锁文件恢复、布尔失败样本、Excel GUI 和撤销仍需 Revit 2024 实机。

---

## 2026-08-07｜3.1.35 批处理新增“打印 PDF（Revit 2021+）”

执行者：ChatGPT

### 目标

在“总控中心 → 批处理与云链接”现有 Revit 2022+ 原生 PDF 导出之外，增加通过 Windows PDF 虚拟打印机执行的打印 PDF 功能，使 Revit 2021 也具备 PDF 打印路径。

### 修改文件

- `src/YangTools.Revit/Core/BatchTasks/BatchTaskEngine.cs`
- `src/YangTools.Revit/Models/BatchTask/BatchTaskViewModel.cs`
- `src/YangTools.Revit/Core/UserSettings.cs`
- `src/YangTools.Revit/UI/BatchTaskWindow.xaml`
- `src/YangTools.Revit/UI/BatchTaskWindow.xaml.cs`
- `src/YangTools.Revit/Commands/BatchTaskCommand.cs`
- `tools/Verify-SourceBaseline.ps1`
- `tools/Test-BatchPrintPdf.py`
- `README.md` / `PROJECT.md` / `WORKLOG.md`
- `docs/HANDOFF_3.1.35.md`
- `docs/FEATURE_AUDIT_3.1.35.md`
- `docs/REVIT_2021_2027_COMPATIBILITY_3.1.35.md`
- `docs/BATCH_PRINT_PDF_3.1.35_20260807.md`
- `docs/DEEPSEEK_CODEX_BATCH_PRINT_PDF_ACCEPTANCE_3.1.35_20260807.md`

### 实现

- 新增“打印 PDF（虚拟 PDF 打印机，Revit 2021+）”。
- 自动发现 Windows 中名称包含 PDF 的打印机，并允许手工输入。
- 支持选择 Revit PrintSetting、视图/图纸集和 `{模型名}` 文件名。
- 使用 `PrintRange.Select` + In-Session ViewSheetSet + `SubmitPrint()`。
- 打印到临时 PDF，成功后再安全替换最终文件；旧 PDF 不在打印前删除。
- 打印前快照并在 finally 中恢复打印机、打印范围、PrintSetting、ViewSheetSet 等状态。
- 打印机名称必须包含 PDF，防止误发到实体打印机。
- UI 明示 Microsoft Print to PDF 等驱动可能弹出“另存为”，无人值守能力必须实机确认。

### 静态结果

- UI 静态：0 错误 / 0 警告。
- Review2 契约：54/54。
- 批量链接路径：6/6。
- 文本段落宽度：15/15。
- Loft/实体提取标高：16/16。
- 重复参数清理模拟：通过。
- 新增 Print PDF 契约：32/32。
- 40 个 XAML XML 解析通过。

### 未完成

- 当前环境无 Windows/.NET/Revit，未真实编译本轮产品源码。
- 未验证任何实际 PDF 驱动的 `SubmitPrint` 自动保存能力。
- 必须在 Revit 2021/2024/2025 验证打印、覆盖保护、状态恢复；Microsoft Print to PDF 若弹“另存为”只能标记 INTERACTIVE_ONLY。

### 判定

**源码静态 GO；Windows/Revit 条件 GO；正式发布 NO-GO。**

上一 FINAL3 的编译/部署证据只能作为上一产品源码基线历史证据；本轮产品代码变化后必须重新编译。
# 2026-09-01｜3.1.36 CAD 转 Revit 图纸

- 执行者：ChatGPT。
- 任务：把 YangCAD 预处理后的二维 CAD 图纸通过类型映射重建为 Revit 原生详图对象。
- 新增：`Core/CadToRevit/*`、`CadToRevitWindow.xaml(.cs)`、`CadToRevitCommand.cs`、16/32 px 图标、`Test-CadToRevit.py`。
- 映射：文字/引线、线条、填充、标注四类；动作固定为使用现有、复制现有、自动新建、CAD炸开参照（备用）、跳过。
- 关键决策：炸开类型不默认；文字不从 H100 等 CAD 命名猜 Revit 高度；尺寸参考线使用专用 `YangTools-尺寸界线`；映射按名称存储；全批一次 Ctrl+Z。
- 事务：TransactionGroup 聚合；类别 Transaction；单对象 SubTransaction；标注和其参考 DetailCurve 同一子事务；零成功整批回滚。
- UI：复用现有 ThemeHelper 四主题，新增 Ribbon 图标；当前命令数 31。
- 代码加固：未知映射动作直接拒绝；复制现有必须真的找到所选模板，不允许静默退回任意第一个类型。
- 静态专项：`tools/Test-CadToRevit.py` 当前 65/65；最终全量 Test-* 结果以交付静态报告为准。
- 未完成：Windows C# 真实编译、Revit 2024/2025 GUI/API、真实 YangCAD Exchange JSON schema/单位/坐标、真实图纸对象重建和 Ctrl+Z。
- 状态：STATIC GO / WINDOWS REVIT RETEST REQUIRED。



# 2026-09-01｜3.1.36 CAD→Revit E2E 对账复核修正（Polyline bulge）

- 执行者：ChatGPT。
- 触发原因：Google Drive 实机证据显示 YangCAD 受控样本导出 11 个实体，但 YangTools Reader 仅识别 7 个并产生 4 条 warning；Gemini 报告中的“0 对象丢失 / 100% 对账”不成立。
- 独立确认缺口：
  - 3 个 `LWPOLYLINE` 的真实结构为 `Data.vertices[].point + bulge`，旧 Reader 只按普通点数组读取，导致 3 个对象被跳过。
  - 1 个 `MULTILEADER` 在实际 MAP 中 `text/raw_text` 为空；Reader 按“空文字不伪造 TextNote”规则跳过是正确行为，需使用真正带文字的 MLeader 重新做受控样本。
- 本轮源码修复：
  - 新增 `CadPolylineVertex(Point, Bulge)`；
  - Reader 原生读取 `vertices[].point` 与 `bulge`；
  - RebuildService 对非零 bulge 使用三点 `Arc.Create` 重建圆弧，不静默拉直；
  - 继续兼容普通 points / start-end / Arc / Circle；
  - 新增 `tools/fixtures/RVT_EXCHANGE_V1_READER_FIXTURE.json`，固定 DxfType、Polyline point+bulge、MLeader text+leader_lines 契约。
- 静态结果：
  - `Test-CadToRevit.py`：76/76 PASS；
  - 全部 14 套 `tools/Test-*.py`：0 failure；
  - UI：148 C# / 42 XAML / 31 Ribbon / 0 error / 0 warning。
- 当前状态：
  - 本源码为 `E2E_FIX_STATIC` 候选，不覆盖上一 FINAL；
  - 必须由 Windows/Revit 2024 重新编译并使用真实 YangCAD 受控样本验证：
    - Curves=6；
    - Texts=3（MLeader 必须真实带文字）；
    - Fills=1；
    - Dimensions=1；
    - TotalCount=11；
    - warnings=0；
    - bulge=0.5 的 Polyline 弧段方向、端点、弧高与 CAD 一致。

# 2026-09-11｜3.1.38 钢筋模块 R00 能力探针

- 执行者：ChatGPT。
- 背景：用户提供路桥钢筋图、BS8666:2005 与 Revit 2023–2026 钢筋功能资料；在正式做自动配筋前，先验证 Shape Driven / Free Form、Varying、约束、箍筋关系、Hook/Coupler/Lap 等真实 API 行为。
- 规划：锁定 `Shape Driven Engine + Free Form Engine` 双核心；Area/Path/RebarInSystem 作为后续 System Reinforcement Adapter；修改固定分参数原位修改、约束重解、拓扑重建三级；Snapshot 仅审计，真实回滚只使用 Revit Transaction/TransactionGroup。
- 新增 `Core/Rebar/RebarInspectionModels.cs`、`RebarInspectionService.cs`：只读检查 Rebar 类型、宿主、直径、Schedule Mark、Shape、Layout/Distribution、Varying、约束 Handle/Target、Hook/Coupler/Lap、Free Form Server、第一/中间/最后 Bar Position 长度与中心线。
- 新增 `RebarInspectorCommand` + `RebarSelectionFilter`：检查工具区扩展按钮“钢筋诊断”，结果输出 `%LOCALAPPDATA%/YangTools/Diagnostics/Rebar/*.json`。
- MCP 新增 `inspect_rebar`，UI 与 MCP 共用同一 `RebarInspectionService`；未传 ID 时读取当前 Revit 选择集。
- 新增 `tools/Test-RebarR00.py`；专项静态门禁 44/44 PASS。
- Ribbon 命令由 31 增至 32，`Test-UiPanelsStatic.py` 同步更新；UI 静态检查 0 error / 0 warning。
- 新增规划文档 `docs/REBAR_MODULE_PLAN_R00_20260911.md` 与 Windows/Revit 2024 实机任务 `docs/DEEPSEEK_GEMINI_REBAR_R00_ACCEPTANCE_3.1.38_20260911.md`。
- 当前环境无 .NET SDK / Revit，未真实编译、未验证 API 运行行为；本轮只能判定 **R00 STATIC GO / RUNTIME RETEST REQUIRED**，不得进入 R01 创建器或覆盖可靠性加固主线候选。

## 2026-09-15｜Rebar R00 Mimo 结果复核 → R01 启动

执行者：ChatGPT

输入：`RESULT_YANGTOOLS_REBAR_R00_MIMO_20260915_0935.zip`

复核结论：

- R00 总体 `CONDITIONAL GO`，仅允许进入 R01 Shape Driven 最小创建器；
- E1 Shape Driven single：PASS；
- E2 NumberWithSpacing set：PASS；
- E4 仅 PARTIAL，不接受完整 PASS；
- E3/E5/E6/坡度尺寸：NOT_TESTED；
- 2024 UI 截图证据无效（包内截图实际为 Revit 2025.4）；
- Build=Deploy SHA 已证明，Loaded SHA 仅间接证明；
- `journal.0095` 被引用但未包含在结果包。

本轮源代码：

1. 合入 Mimo 两个最小编译修复：
   - BatchTaskWindow 插值字符串；
   - RebarInspectionService `suppressHooks` 参数名。
2. 修复 R00 Inspector `RebarStyle` 读取：
   - Shape Driven → `RebarShape.RebarStyle`；
   - Free Form → `RebarFreeFormAccessor.RebarStyle`；
   - schema `R00.1` → `R00.2`。
3. 新增 R01 Shape Driven minimal creator：
   - dryRun；
   - RebarHostData.IsValidHost；
   - existing RebarBarType；
   - CreateFromCurves；
   - Single / NumberWithSpacing；
   - 单 Transaction；
   - Inspector readback；
   - UI/MCP 共用 Service。
4. 新增 MCP：`list_rebar_types`、`create_shape_rebar`。
5. 新增 Ribbon：`钢筋工具区 / 直筋创建 R01`。

静态结果：

- R00 44/44；
- R01 35/35；
- UI 161 C# / 42 XAML / 33 Ribbon / 0 error / 0 warning；
- 全量 `tools/Test-*.py` 21/21 suites PASS。

当前状态：`R01 STATIC GO / WINDOWS REVIT 2024 RUNTIME REQUIRED`。

## 2026-09-15｜Rebar R01 Runtime 复核完成 → R02 Face/Cover/ToCover

执行者：ChatGPT

### R01 原始证据复核

- 输入：`RESULT_YANGTOOLS_REBAR_R01_MIMO_20260915_1054.zip`；结果包 SHA `3EAC0B98DD90D789D3E4917058DBC96DFE202727EB30290A163E69F9BDBCC894`。
- 源码 SHA 与 R01 交付一致；Revit2024 build 0 error / 253 warning；本轮无源码改动。
- Build=Deploy=Loaded-path-file SHA：`E32F43768B2DCB79322B852EC8FC78F7C9D3482F10214B09993DA457E1505FE7`。
- dryRun、Single、NumberWithSpacing、错误输入、R00.2 Standard style readback：PASS。
- UI command 注册：PASS；UI 真实交互：NOT_TESTED。
- Undo：NOT_CONFIRMED；旧脚本缺少正确 PostableCommand 导入，count 未下降。
- 正式判定：`R01 CORE RUNTIME GO / UI+UNDO OPEN / RELEASE NO-GO`。

### R02 开发

- 新增 PlanarFace host-face probe，输出 stable Reference / normal / Cover。
- 新增 FaceDrivenRebarCreateService：点投影、Cover+bar radius 初始中心线、Shape Driven 创建、RebarPlane face-specific ToCover preferred constraint。
- ToCover 不能确认时 Transaction rollback。
- 新增 UI `钢筋工具区 / 按面保护层 R02`。
- 新增 MCP `inspect_rebar_host_faces`、`create_shape_rebar_on_face`，默认 dryRun。
- 静态：R00 44/44、R01 35/35、R02 44/44、UI 34 Ribbon 0/0；全量 22 suites 0 failure。
- 当前：`R02 STATIC GO / REVIT2024 RUNTIME REQUIRED`。

## 2026-09-15｜Rebar R02 Runtime 复核 → R03 Surface Distribution / Varying Static
- 执行者：ChatGPT
- 输入：`RESULT_YANGTOOLS_REBAR_R02_MIMO_20260915_1146.zip`
- R02 判定：修复后的 Runtime 候选 `CORE RUNTIME GO`；原静态 ZIP 因 Revit 2024 preferred-constraint API 签名错误被 supersede；UI interactive / curved face 保持 NOT_TESTED；RELEASE NO-GO。
- 关键实证：同一 Rebar Id 在 Cover 25→50 mm 后中心线 31.35→56.35 mm，仍 ToCover，无产品重建；Undo 一次撤销完整 set。
- 证据问题：名为 `revit2024_r02.png` 的截图实际显示 Revit 2025.4，不能作为 2024 UI 证据；2024 journals 仍能证明核心 runtime。
- 架构修正：R02 set normal=cover normal 只能证明 Layout，不能代表沿面布筋；R03 将 primary-plane normal 改为 `barDirection × coverNormal`，用 Edge 控制 Cover，Start/End 控制边界。
- 新增：`RebarApiCompatibility`、R03 models/service/command/MCP/static test。
- 静态：R00 44/44、R01 35/35、R02 44/44、R03 53/53、UI 35/0/0、全部 23 suites 0 failure。
- 状态：`R03 STATIC GO / REVIT 2024 RUNTIME REQUIRED / RELEASE NO-GO`。
- 下一步：Mimo Fresh Build + trapezoid varying + inclined face + Undo + SHA chain。

## 2026-09-15｜Rebar R03 Runtime review -> R04 Shape Driven Modify
- R03 原始 Mimo RESULT 独立复核：CORE RUNTIME GO；true varying / inclined planar / Undo / 2024+2025 build 成立。
- 拒绝 `revit2024_r03.png` 作为 2024 UI 证据：图片标题栏实际为 Revit 2025.4；journal 仍独立证明 2024 runtime。
- 识别 R03-B uniform midpoint 重合更可能是 raw `GetCenterlineCurves` 未应用 bar-position transform 的探针问题；R04 将 Inspector 升到 R00.3 transformed geometry。
- 新增 R04 Shape Driven 原位修改：可选 BarType + NumberWithSpacing，dryRun，提交前验证 ElementId/UniqueId/Host/Constraint signature，不允许隐式重建。
- R04 静态：R00 44/44；R01 35/35；R02 44/44；R03 53/53；R04 50/50；UI 36/0/0；全部 Test-*.py 24 suites / 0 failure。
- 当前：R04 STATIC GO / Revit 2024 Runtime Required / Release NO-GO。

## 2026-09-15｜Rebar R05 Runtime review -> R06 Relationship / Hook
- 输入：`RESULT_YANGTOOLS_REBAR_R05_MIMO_20260915_1650.zip`；结果包 SHA-256 `C82176D356FCF7097FE51DBEBD511D46729E71F6F014FA48F60682AD6D41B983`。
- R05 正式判定：`EDGE REBIND CORE RUNTIME GO / START-END REBIND OPEN / UI_INTERACTIVE NOT_TESTED / RELEASE NO-GO`。
- Revit 2024/2025 build 均 0 error；2024 Build=Deploy=Loaded SHA `6485F27E619F8DD63D72815DD7BFA99A2E1C42ACC752BFF66D5DD9399DABB6C0`。
- Edge exact face rebind 在同一 Rebar ElementId/UniqueId/Host 上由 `...:9:SURFACE` 原位改为 `...:10:SURFACE`，ToCover/Varying 保持；Undo 恢复 `...:9:SURFACE`。
- R00.4 exact face stable-reference readback 成立；错误输入均安全失败。
- Start/End 未闭环：左右面无 native ToCover candidate；底面 dry-run 可选但 apply 精确 readback 失败并回滚。保留为后续专用 fixture backlog，不冒充 PASS。
- Mimo 需要一处最小编译修复：`RebarPlanarFaceSelectionFilter.cs` 增加 `using YangTools.Revit.Core;`；已合入 R06 baseline。
- 截图 `revit2024_r05.png` 实际显示 Revit 2025.4，拒绝作为 2024 UI 证据；2024 journal/MCP/loaded path 仍独立证明核心 runtime。

### R06 开发
- R00 Inspector 升级到 `R00.5`：新增 Start/End Hook Type ID，以及 ToOtherRebar target Rebar ID/UID、target kind、edge/bend/hook index、distance readback。
- 新增 `RebarRelationshipService`：仅接受 Revit native `ToOtherRebar` candidate；支持 handle ordinal / target kind / target index 消歧；拒绝 source==target 与猜测式绑定；apply 后校验 source identity/host 与 exact target Rebar，失败 Transaction rollback。
- 新增 `RebarHookModifyService`：Shape Driven Hook Type 原位修改/移除，dry-run，identity/host readback，失败回滚。
- 新增 MCP：`list_rebar_hook_types`、`constrain_rebar_to_rebar`、`modify_rebar_hooks`；写工具默认 dry-run。
- 新增 Ribbon：`钢筋关系 R06`、`Hook修改 R06`；UI/MCP 共用 typed service。
- 静态：R00 44/44、R01 35/35、R02 44/44、R03 53/53、R04 50/50、R05 57/57、R06 48/48；全部 Test-*.py PASS；UI 180 C# / 42 XAML / 39 Ribbon / 0 error / 0 warning；McpServer Python compile PASS。
- 当前：`R06 STATIC GO / WINDOWS+REVIT RUNTIME REQUIRED / RELEASE NO-GO`。

## 2026-09-17 — Rebar R07
- R06 raw evidence reviewed: relationship + Hook core runtime GO; UI interactive not tested.
- Opened second engine with minimal unconstrained Free Form creator.
- Added typed MCP + Ribbon shared service; dryRun default; rollback/readback.
- Explicitly deferred constrained Free Form/IRebarUpdateServer, Coupler/Lap, collision and BS8666.
## 2026-09-20｜GR01 V1 GUI Static Implemented
- GR01 V1 Core Runtime 已由 ChatGPT 判定 GO；本轮只开发中文 WPF，不修改 Core 算法。
- 新增 Ribbon `钢筋工具区 -> 创建钢筋 -> 渐变钢筋集`，仍使用既有 Pulldown；Ribbon 总命令 44，钢筋一级入口仍为 4 组。
- 新增 modeless `RebarGradientSetWindow`：起始/结束控制筋、ModelLine/Edge 路径、Zone DataGrid、普通/加密快捷区段、Preview/Create。
- GUI 使用 Document-bound ExternalEvent 且 `wrapInTransactionGroup:false`；Preview/Create 全部调用已验收 `RebarGradientSetService`，窗口不复制 CreateFreeForm/Spacing/Transaction。
- PlanFingerprint 在控制筋、路径、区段任何变化时清空，成功 Create 后也要求重新 Preview。
- 静态：GR01 V1 81/81；GUI 44/44；全量 Test-*.py 33 suites/0 failure；UI 201 C#/43 XAML/44 Ribbon/0 error/0 warning；MCP py_compile PASS。
- 当前：`GR01 V1 GUI STATIC GO / REVIT 2025 GUI RUNTIME REQUIRED / RELEASE NO-GO`。


## 2026-09-20 — GR01 V1.0.1 UX runtime partial review / MCP AutoLength mapping integrated
- Reviewed Mimo result `RESULT_YANGTOOLS_GR01_V1_0_1_UX_MIMO_20260920_1555.zip` / SHA `CF8FCCE46AB17BA0AC3F9F26F7A7C4FC32D0ABDEA0D2D226CBB53A616CBCA57E`.
- Revit 2025.4 proved ZAxis Preview, one Auto Normal zone, Dense 1000 + Auto remainder, over-allocation guard, and Start/End swap Preview.
- Accepted SOURCE_DIFF: added missing `AutoLength` mapping inside `RevitExecutionBroker.ExecuteGradientRebarSet`; no planner/interpolation/Create algorithm change.
- Strengthened `Test-RebarGR01V101UX.py` to require both broker AutoLength mappings; focused 41/41 PASS and all 34 `tools/Test-*.py` PASS in independent local rerun (two batches).
- ZAxis Create remains NOT CLOSED: current fixture returned native `RebarCantBeBent`. Do not attribute this to wall thickness without evidence; next retest uses transverse/horizontal controls suitable for Z distribution.
## 2026-09-20 — GR01 V1.0.2 UI/UX polish
- 用户反馈鼠标悬停 Zone DataGrid 时滚轮无法滚动整个 GUI；确认是 WPF nested ScrollViewer 事件被内部 DataGrid 消费。
- 新增 `MainScrollViewer` + `ZoneGrid_PreviewMouseWheel`：内部可滚时保留 DataGrid，边界时把滚轮交给外层。
- 路径语义统一：ModelLine/Edge 只提供轴线与范围，Start/End Control 决定业务方向；GUI 不再展示 `+Z/-Z` 作为第二套方向。
- 新增基准总长/有效长度分离、Auto Remaining 待计算显示、单位、即时校验、新 Zone 插入选中行后、正式化标题文案。
- 未修改 Core planner/interpolation/CreateFreeForm/transaction/Undo。
- 静态专项 `Test-RebarGR01V102UI.py` 37/37；全部 35 suites 0 failure；UI static 0/0；MCP py_compile PASS。
- 状态：STATIC GO，等待 Revit 2025 UI smoke。

## 2026-09-22｜GR01 V1.0.2 UI/UX FIX1
- Integrated the Windows/Revit compile-only CS0165 fix in `RebarGradientSetWindow.RefreshActionButtons`: initialize `reason` before short-circuit evaluation and pass `out reason`. No business behavior change.
- Strengthened `Test-RebarGR01V102UI.py` for the compile fix, max-spacing live guard, and Start/End swap wiring. Focused result: 40/40 PASS.
- Full local static rerun: all `tools/Test-*.py` 35 suites / 0 failure.
- Independent evidence review reclassifies mouse-wheel handoff as runtime PASS: screenshots show the outer content visibly moves from both body-hover and DataGrid-hover wheel events; the reported user bug is closed.
- Remaining max-spacing=0 / over-allocation edit capture and Rebar A→B / ModelLine display captures are treated as non-blocking UI proof gaps; implementation/static contracts are present and underlying pick/core paths were already accepted.
- No spacing planner, interpolation, CreateFreeForm, transaction, MCP core, or Rebar geometry behavior changed.


## 2026-09-23｜RC0 Architecture / Release Hygiene 开始

- 在 Rebar V1.0.4 Combined Feature GO 后进入项目级收口，不新增业务功能。
- 静态架构审计确认主要问题是结构密度而非单纯代码体积：约 4.8 万 C# 行、UI code-behind 约 1.63 万行、Core 约 2.20 万行；多个 >1000 行类；UI 层仍存在直接 Transaction / FilteredElementCollector 的旧架构路径；MCP RevitExecutionBroker 继续承担过多集中路由职责。
- 本轮 RC0 只做低风险 release/tooling hygiene：修 Build-Deploy 与 Sign 脚本 PowerShell `$var:` 解析缺陷；增加统一静态测试 runner、ReleaseToolingHygiene 静态检查、canonical `Package-Source.ps1`；source package 排除 artifacts/bin/obj/__pycache__ 等生成物；确认中文 installer 文件名必须原样保留且禁止 mojibake。
- 新增 `docs/ARCHITECTURE_SLIMMING_AUDIT_20260923.md` 与 `docs/ACTIVE_DOCS_INDEX.md`，冻结后续迁移方向为 feature-sliced modular monolith；不全量重写、不拆多 DLL、不改已 Runtime GO 的 GR01 算法。
- 下一阶段顺序：P1 MCP broker 拆分 → P2 大型 UI Revit 业务逻辑下沉 Service → P3 feature folders → P4 docs/tests 整理。每批独立 source SHA / static / Windows build，按触及面做最小 runtime smoke。
