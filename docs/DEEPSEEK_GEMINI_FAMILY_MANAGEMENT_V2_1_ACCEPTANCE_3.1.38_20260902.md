# Gemini / DeepSeek｜YangTools 3.1.38 实机验收任务

## 身份

输入候选：`YangTools_Project_3.1.38_Source_FAMILY_MANAGEMENT_V2_1_STATIC_20260902.zip`

当前身份只能是：`STATIC CANDIDATE`。

不得自行宣布 FINAL/RELEASE GO。

## A. 构建与部署

1. 全新目录解压；
2. 先运行全部 `tools/Test-*.py`；
3. 编译 Revit 2024、2025；能编译 2021–2023/2026 则一并记录；2027 缺 .NET 10 则 `NOT_BUILT`；
4. 2024 Build DLL == Deploy DLL SHA；
5. 完全关闭旧 Revit 2024 后部署并启动新进程；
6. 保存 PID/StartTime/Assembly path/SHA/Journal。

## B. 先闭环 carried-forward PasteCAD Shared

严格执行已有：
`03_Acceptance/20260902_PASTE_CAD_SHARED/00_GEMINI_PASTE_CAD_SHARED_ACCEPTANCE_3.1.37.md`

但使用 3.1.38 DLL。

`ImportSucceeded` 与 `CoordinateAlignmentVerified` 必须分开。

## C. 族文档管理 V2.1

### C1 UI 尺寸

默认窗口：1280×780；缩小到 Min 1080×650。

截图/记录：
- 左栏约 340px；
- RowHeight≈30；Header≈36；
- Type 220、Usage 82、Status 90；
- 动态参数列约 160，不能压成单字；
- 最小窗口底部按钮仍单行，必要时出现横向滚动；
- 长族名不会覆盖右侧“管理实例/批量导出/清理”按钮；
- 四主题各切一次，不黑屏、不丢资源。

### C2 使用统计硬门禁

选一个至少 3 Type、至少 2 个有实例、至少 1 个未使用 Type 的载入族。

MCP/API 独立读取：
- Family instance count；
- 每个 FamilySymbol instance count；
- current view count。

与 UI 数字逐项一致。

### C3 未使用过滤

- “仅显示有未使用类型的族”正确；
- “类型表仅显示未使用类型”正确；
- 使用中 Type 不得进入未使用列表。

### C4 管理实例

点击“管理实例”：
- 打开/激活族实例管理；
- 自动选择当前 Family；
- 重复点击不产生第二个同文档实例管理窗口。

### C5 批量 RFA

Ctrl/Shift 选择 2 个可编辑族 → 批量导出。
验证两个 RFA 存在且可打开；同名覆盖提示只出现一次。

### C6 安全清理未使用类型

对含 1 个未使用类型的测试族：
- Trial Delete；
- 真实删除只删未使用 Type；
- 有实例 Type 保留；
- UI 统计刷新；
- 一次 Ctrl+Z 恢复。

另构造会级联删除额外对象的类型（若可稳定构造）：必须被 V2.1 自动阻止；无法稳定构造则标 `NOT_TESTED`，不得脑补。

## D. 族实例管理 V2.1

### D1 UI 尺寸

默认 1360×800；缩小到 1160×660。

必须确认：
- Global 区不重叠；
- fixed columns 92/190/130/180/70 仍完整；
- 动态参数列约 160；
- 横向滚动出现而不是压缩列；
- 底部工具按钮保持单行；
- 四主题可用。

### D2 Scope

同一 Family 对账：
- 全项目；
- 当前视图；
- 当前选择。

UI 行数必须和 MCP/API collector 一致。
当前选择含非 FamilyInstance 时不能残留旧过滤。

### D3 Type / Level filter

分别选择一个 Type、一个 Level，再组合筛选；行数和每行固定状态列必须正确。

### D4 Batch Change Type

选至少 2 个同 Family 实例 → 目标 Type → 应用。
MCP/API 读取实际 GetTypeId 验证。
一次 Ctrl+Z 恢复全部成功项。

如果能制造一个失败实例，验证该 SubTransaction 回滚而其它实例成功；做不到则该混合失败样本 `NOT_TESTED`。

### D5 Batch Parameter

选至少 3 个实例，找一个可写实例参数：
1. “仅填空值”勾选；预先设置一项非空，确认该项跳过、空项写入；
2. 取消“仅填空值”，覆盖全部选中行；
3. MCP/API 读回；
4. 一次 Ctrl+Z。

### D6 System Family

系统类别必须来自项目实际内容，而不是旧固定 10 类清单。
选择 Walls/Floors 等存在类别：Scope/Type/Level filter、固定列读取正常；至少做一次合法 ChangeTypeId（若项目有 2 个类型）。

### D7 Excel

导出当前显示结果：必须含
`图元ID / 类型 (Type) / 标高 / 主体 / 固定` + 动态参数列。
改一个可写参数再导入，写回正确；固定信息列不得被当作参数写入。

## E. Stable ParameterKey

普通可写参数至少验证 1 个。
若测试模型可构造重复同名参数，必须确认 UI 列头带身份后缀且不会写错；构造不了则记录 `NOT_TESTED`，静态契约仍保留。

## F. 原始证据

新建：
`03_Acceptance/20260902_FAMILY_MANAGEMENT_V2_1/`

至少：
- 00_TASK.md
- 01_BUILD/
- 02_DEPLOY/
- 03_REVIT_MCP_RAW/
- 04_UI_SCREENSHOTS/
- 05_FAMILY_USAGE_GATE.json
- 06_SCOPE_FILTER_GATE.json
- 07_BATCH_TYPE_GATE.json
- 08_BATCH_PARAMETER_GATE.json
- 09_UNUSED_CLEANUP_GATE.json
- 10_EXCEL_GATE.json
- 11_PASTE_CAD_SHARED_GATE.json
- 12_USER_TEST_REQUIRED.md
- 13_FINAL_REPORT.md
- 14_HASHES.txt

MCP raw 必须是真实 request/response，不是事后手写 summary。

## G. 状态

只允许：PASS / FAIL / NOT_TESTED / NOT_INSTALLED / NOT_BUILT / USER_TEST_REQUIRED。

Gemini 最终只提交 `CURRENT_UPDATE_PROPOSAL.md`；ChatGPT 决定 GO/NO-GO。
