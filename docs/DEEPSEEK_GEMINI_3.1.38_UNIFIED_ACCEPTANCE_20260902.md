# Gemini / DeepSeek｜YangTools 3.1.38 统一 Windows/Revit 实机验收

## 0. 身份与权限

你是 Windows/Revit **执行器**，不是最终验收者。完成后只能写 `TEST EXECUTION COMPLETE`；不得自行写 FINAL GO / RELEASE GO。

开工前先读 Google Drive：
- `00_CURRENT/GEMINI_START_HERE.md`
- `00_CURRENT/GEMINI_DRIVE_DELIVERY_RULES.md`
- 本任务

所有有效源码、DLL、日志、MCP raw、截图、实际导出文件、SHA、报告必须进入本轮 Acceptance Drive 目录。没有进 Drive = 未交付。

## 1. 输入源码

使用 ChatGPT 在 `01_YangTools` 放置的 3.1.38 完整静态候选。

开工时先记录：文件名、Drive ID、ZIP SHA-256。解压到全新空目录，禁止继续使用旧 E:\Antigravity 工作树。

## 2. 状态枚举

只允许：
`PASS / FAIL / NOT_TESTED / NOT_INSTALLED / NOT_BUILT / USER_TEST_REQUIRED`。

## 3. 静态与 Windows Build

在 fresh extract：
1. 全部 `tools/Test-*.py`；
2. 记录 Batch Publisher / Family V2 / PasteCAD 专项；
3. 编译 2021–2026；2027 有 .NET10 则编，否则 `NOT_BUILT`；
4. 不因为没有安装某年份 Revit 就把 build 写 runtime；
5. 记录 build log、DLL bytes、SHA。

至少部署并运行 Revit 2024；Revit 2025 如已安装则启动运行；Revit 2021 用于 Print PDF 专项，如没有可安全启动的 2021 测试环境则标对应 Runtime `NOT_TESTED`，不要关闭用户已经运行的旧 2021 进程。

Build/Deploy/Loaded 同一目标必须提供 SHA。

---

# A. PasteCAD Shared

## A1 Controlled data
准备 DWG/RVT，二者共享坐标存在一个非零平移/旋转，至少一个已知控制点。

## A2 产品链
必须通过：`导入工具区 → 从 CAD 粘贴 → 对齐共享坐标`。
不得直接写 Revit Import API 脚本代替产品。

## A3 Gate
记录 ImportInstance ElementId + Transform/control point。

`ImportSucceeded = true` 不是坐标门禁。必须证明 Shared 后控制点与 Revit Shared 预期误差 <=1 mm。

另测 Origin/Centered 最小冒烟，确认旧模式未回退。

---

# B. Family Management V2.1

## B1 UI hard gate
Family Manager：1280×780 与 Min1080×650。
Family Instance Manager：1360×800 与 Min1160×660。

四主题逐个查看。默认尺寸和最小尺寸必须截图。FAIL 条件：
- 文字被裁到不可读；
- DataGrid 固定列被压成单字；
- 按钮重叠/被遮挡/强制换多行；
- 横向滚动不可达动态参数；
- 黑屏/资源异常。

AI无法可靠判断视觉时标 `USER_TEST_REQUIRED`，不要凭代码写 PASS。

## B2 Family usage
控制模型至少 2 个 loadable families，每个>=2 Types，包含 used/unused type。
用 MCP/API collector独立统计 Family/Type instance count，与 UI 数量逐项对账。

## B3 unused cleanup
选择一个无引用 Type 做安全删除：
- Trial Delete 结果无额外危险 cascade；
- 真删成功；
- 一次 Ctrl+Z 恢复。
再构造一个存在额外 cascade 风险的样本，必须安全阻止。

## B4 Family→Instance
点击管理实例，窗口复用/跳转到正确 Family；不得创建重复 ExternalEvent 主链。

## B5 multi RFA
选择 >=2 editable loadable families，批量导出 RFA；每个实际文件存在/非空，记录 SHA。

## B6 Instance scopes / filters
逐测 Project / ActiveView / Selection；Type filter；Level filter；固定列 Level/Host/Pinned 与 API 真值一致。

## B7 batch type
至少 3 instances；批量 ChangeTypeId；记录 before/after TypeId/ElementId；包含一个失败隔离样本。成功结果一次 Ctrl+Z 完整恢复。

## B8 batch parameter
对 selection 写同一个参数；测 overwrite 和 only-empty；使用 shared/BIP/PID 至少两类 StableParameterKey。若可以构造同名歧义，必须拒绝乱写。一次 Ctrl+Z。

---

# C. Batch Publisher V2

## C0 原则
**必须通过 YangTools 批处理窗口/BatchTaskEngine 产品链。**
MCP 可用于读回、创建 controlled test model、核验结果，但禁止直接 `Document.Export`/PrintManager 脚本代替产品然后写 PASS。

使用至少 **2 个模型**。若有安全可用 Autodesk Docs/BIM 360 native cloud model，再加 1 个已打开 cloud model；没有则 Cloud Runtime=`NOT_TESTED`，禁止模拟。

控制模型准备：
- 同名 3D view；
- 同名 ViewSheetSet；
- 同名 DWG setup；
- 2022+ 同名 PDF setup；
- PrintSetting；
- 至少 2 张可打印 Sheet，图号/图名不同。
另准备一个模型故意缺少某个命名 setup，验证 preflight。

## C1 preflight zero-output
选择一个实际不存在于模型2的命名资源。执行预检必须指出模型2缺失；开始导出前该模型保持 0 新文件。

## C2 NWC
如果 `IsNavisworksExporterAvailable=false`：状态 `NOT_INSTALLED`，保存原始结果，继续其它格式。
如果存在：使用 YangTools 导出真实 `.nwc`，文件必须存在且 >0 bytes，记录最终名、bytes、SHA；核验 scope/coordinates/links/properties 等本次配置。

## C3 IFC
真实 `.ifc`，>0 bytes，精确命名。至少记录版本、scope、BaseQuantities、SpaceBoundary、WallColumnSplitting。若 `IsIFCAvailable=false` 则 FAIL/NOT_INSTALLED 按真实环境，不得假输出。

## C4 DWG
真实导出至少 2 张 Sheet。主 DWG 实际名必须精确等于模板展开；每个 >0 bytes。若产生 XRef/伴随文件，列出全部文件。

另测：模型2缺同名 `ExportDWGSettings` 时必须 preflight fail，而不是执行到中途再失败。

## C5 Native PDF Export
仅 Revit 2022+：
- Separate：至少2张 Sheet，最终名字精确匹配 `{模型名}-{图纸号}-{图纸名}.pdf`；
- Combined：一个合并文件；
- 每个 PDF >0 bytes；记录 SHA。
Revit 2025+额外证明 options `GetExportInBackground`/行为已被 YangTools 强制同步关闭，当前文件完整落盘后才进入下一输出。

## C6 Print PDF / Revit 2021
在 Revit 2021 使用**实际安装的 PDF printer**通过 YangTools `PDF 打印（2021+）`：
- Separate 至少1张；
- 输出名字准确；
- 文件 >0 bytes。

如果打印机弹 Save As 或无视 `PrintToFileName`，不要自动点窗口冒充无人值守：标 `USER_TEST_REQUIRED` 或 FAIL，并记录 printer 名称/现象。

Revit 2024/2025 可补测 Print path，但不能代替 2021 gate。

## C7 naming collision hard gate
关闭每模型独立子目录，让两个模型使用同一固定文件名，例如 `Coordination.nwc`，即使 `OverwriteExisting=true`：
**预检必须 FAIL，且 0 输出。**

再恢复 `{模型名}` 或启用模型子目录，预检 PASS。

## C8 existing file overwrite safety
预先放一个旧目标文件：
- Overwrite=false → preflight阻止；旧文件 SHA 不变；
- Overwrite=true + 成功输出 → 新文件替换；
- 尽可能构造导出失败 → 旧文件不能因为失败先被删除；记录 before/after SHA。

## C9 UseDocumentSubfolders
同样两模型分别测试 checkbox on/off，实际输出目录必须与选择一致。

## C10 native cloud（若环境有）
通过 `添加已打开模型（含云）` 加入真正 cloud Document；记录：
- IsModelInCloud
- Region
- Project GUID
- Model GUID
- LocationDescription
- 若安全，关闭后用保存 metadata 重新打开。
Desktop Connector 本地 path 不得冒充 native cloud。

## C11 输出对账表
最终 JSON/MD 必须逐文件：
`Model / Format / SourceViewId / SheetNumber / NamingTemplate / ExpectedPath / ActualPath / Bytes / SHA256 / Status`。

---

# D. Regression / undo / evidence

1. `CAD→Revit` 至少打开窗口 + controlled map 最小转换，不需要重跑已经闭环的全部 3.1.36 gates；
2. MCP 正常；AI/摸鱼入口仍 0；Ribbon 31；
3. 每个写项目的 Family/PasteCAD 操作做真实 Ctrl+Z；Batch export 文件不是 Revit Undo 对象，不要求 Ctrl+Z。

## Drive 输出目录
在 `03_Acceptance/<本轮3.1.38目录>/` 至少：

```text
00_TASK.md
01_RUNTIME_MATRIX.md
02_BUILD/
03_DEPLOY/
04_MCP_RAW/
05_INPUT/
06_OUTPUT/
07_SCREENSHOTS/
08_LOGS/
09_HASHES.txt
10_PASTE_CAD_SHARED_GATE.json
11_FAMILY_MANAGER_GATE.json
12_FAMILY_INSTANCE_GATE.json
13_BATCH_PREFLIGHT.json
14_BATCH_OUTPUT_FILES.json
15_BATCH_NAMING_GATE.json
16_PRINT_PDF_2021_GATE.json
17_USER_TEST_REQUIRED.md
18_FINAL_TEST_EXECUTION_REPORT.md
CURRENT_UPDATE_PROPOSAL.md
```

`04_MCP_RAW` 必须是实际 request/response；`08_LOGS` 不得空；截图无法取得时必须写 USER_TEST_REQUIRED。

如果执行中修源码：记录文件/原因，重新编译部署并仅重跑受影响 gate，生成新的完整 source ZIP + SHA 并上传 `01_YangTools`；不要自行覆盖 FINAL。
