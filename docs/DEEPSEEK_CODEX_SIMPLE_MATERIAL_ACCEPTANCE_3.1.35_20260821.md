# DeepSeek / Codex：YangTools 3.1.35 当前源码编译部署与专项验收

日期：2026-08-21

## 0. 任务边界

输入交接基线：`YangTools_Project_3.1.35_Source_SIMPLE_MATERIAL_20260821.zip`。

当前 Windows 复验候选源码：`YangTools_Project_3.1.35_Source_SIMPLE_MATERIAL_P0_Z_GATE_20260821.zip`。

当前状态：`STATIC GO / WINDOWS RETEST REQUIRED`。

本轮禁止：

- 新增功能；
- 推送 Git；
- 跨年份复用 DLL；
- 用旧 MATERIAL_BINDING/FINAL3 DLL 冒充当前构建；
- 要求用户手工测试。

只修真实编译/部署/Revit 实机暴露的问题。

## A. 全新目录核验

解压到全新空目录，依次执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Verify-SourceBaseline.ps1
python .\tools\Test-UiPanelsStatic.py --root .
python .\tools\Test-SimpleFamilyMaterialBinding.py
python .\tools\Test-FamilyMaterialBinding.py
python .\tools\Test-ReviewFixes.py --root .
python .\tools\Test-BatchLinkPathMatching.py
python .\tools\Test-AlignTextWidth.py --root .
python .\tools\Test-GeneratorLevelPlacement.py --root .
python .\tools\Test-BatchPrintPdf.py --root .
python .\tools\Test-ChineseCleanup.py
python .\tools\Test-LinearElevationProfile.py --root .
python .\tools\Test-FamilyCategoryChange.py
python .\tools\Test-AssistantDataCleanup.py
python .\tools\Test-ParameterCleanupPipeline.py
```

预期 Ribbon：30。

## B. 独立编译

按本机可用环境独立构建 2021–2027 Configuration：

```powershell
dotnet build .\src\YangTools.Revit\YangTools.Revit.csproj -c 2024 -p:Platform=x64
dotnet build .\src\YangTools.Revit\YangTools.Revit.csproj -c 2025 -p:Platform=x64
```

其它年份同理。

每个年份保存：

- restore.log；
- build.log；
- build.binlog；
- error/warning 数；
- DLL 大小、版本、SHA-256。

2027 如果缺 .NET 10 SDK，保留真实 NETSDK1045 日志，写 NOT_BUILT，不得 PASS。

## C. 部署与启动

至少部署 Revit 2024/2025：

1. 关闭对应 Revit；
2. 备份旧 YangTools；
3. 仅部署当前源码构建产物；
4. 构建 DLL 与部署 DLL SHA-256 必须一致；
5. 启动 Revit；
6. Journal/日志确认 30 个 Ribbon 命令注册；
7. AI/摸鱼助手入口不得出现；MCP 仍可正常加载。

## D. 第一优先：线性布置原 Bug 复验

必须先验证当前 `LINEAR_LEVEL_OFFSET_FIX` 是否真正解决用户原问题。

优先使用用户曾报错的原项目/原族/原路线；若能复现旧错误，记录：

- FamilyPlacementType；
- Level.Elevation；
- Level.ProjectElevation；
- 输入起/终点相对高程；
- 实际使用的 offset BuiltInParameter；
- 每个实例 LocationPoint.Z；
- 理论世界 Z；
- 最大误差；
- Ctrl+Z。

旧错误特征：期望 `29.527559 ft`，实际 `18.044619 ft`，误差 `11.48294 ft`。当前版本必须证明该固定误差消失。

### D1. P0 硬通过标准

对**每一个**生成实例执行：

```text
|实际 LocationPoint.Z - 理论 WorldZ| <= 1 mm
```

必须逐实例记录：

```text
FamilyPlacementType
LevelId
Level.Elevation
Level.ProjectElevation
实际生效的 BuiltInParameter
Offset.AsDouble()
理论 WorldZ
LocationPoint.Z
误差（mm）
```

实际生效参数必须明确属于：

```text
INSTANCE_ELEVATION_PARAM
INSTANCE_FREE_HOST_OFFSET_PARAM
FAMILY_BASE_LEVEL_OFFSET_PARAM
```

不能只看“命令没有异常”或“Parameter.Set 返回成功”。源码已要求写入候选 Offset 后直接读取真实 `LocationPoint.Z`；不生效的候选会恢复原值再尝试下一个。

**P0 失败规则：**任意实例误差 >1 mm、实际 Offset 参数不明确、回滚失败、或仍出现固定 11.48294 ft 偏差，立即停止后续材质/其它功能验收，先只修线性布置。此时属于明确 P0 阻塞，可判 NO-GO。

## E. 简易“绑定材质”专项

准备：

- 至少两个可编辑载入族；
- 每族至少一个带 Material 属性的实体；
- 每族至少两个实例，其中至少两个实例属于同一 FamilySymbol；
- 项目已有材质 A/B；
- 一个不可编辑或就地族作为负例（如方便）。

### E1 当前预选

1. 预选 2–3 个 FamilyInstance；
2. 点击 `绑定材质`；
3. 窗口应直接使用预选，不重新要求选择；
4. 统计实例/族/类型数量正确。

### E2 无预选 / 重新选择

- 清空预选后点击命令，应进入 FamilyInstance 多选；
- 非 FamilyInstance 不允许选入；
- 点击“重新选择实例”后可以替换当前选择集合。

### E3 实例参数

- 参数名：`测试材质_实例`；
- 参数方式：实例参数；
- 材质：A；
- 执行后验证：
  - 族内所有可绑定几何关联到该 FamilyParameter；
  - 只有所选实例参数值为 A；
  - 同类型未选实例不能被自动改成 A；
  - 参数 StorageType=ElementId，值等于 A.Id；
  - 一次 Ctrl+Z 撤销族回载 + 材质赋值。

### E4 类型参数

- 参数名：`测试材质_类型`；
- 参数方式：类型参数；
- 材质：B；
- 选择同一类型的两个实例 + 另一个类型一个实例；
- 验证只对唯一 FamilySymbol 写值；相同类型其它未选实例随类型变化属于预期。

### E5 多族

一次选择来自两个族的实例，验证：

- 两个族都正确 EditFamily/LoadFamily；
- 成功族、失败族汇总准确；
- Family/Symbol/Instance ID 不异常变化；
- 既有正 ID 自定义参数值保持。

### E6 材质搜索

- ComboBox 输入部分文字可筛选/定位；
- 输入不存在完整名称时禁止执行；
- 不得自动创建新的项目 Material。

### E7 同名冲突

分别测试：

- 同名 Text 参数；
- 同名 Material 但当前是类型参数、本次要求实例参数；
- 同名 Material 但当前是实例参数、本次要求类型参数。

都必须明确失败，禁止删除重建旧参数。

### E8 已有其它绑定

- `覆盖已有绑定=true`：允许把可处理几何重新关联到本次参数；
- `false`：已有其它关联保持，计为跳过。

### E9 零成功保护

构造全部目标不可处理的情况，验证：

- 最终 `AssignedCount=0` 时整次 TransactionGroup 回滚；
- 项目中不留下只创建但未实际赋材质的空操作结果。

## F. “批量绑定材质”专项

1. Ribbon 名称必须为 `批量绑定材质`；
2. 参数名称输入框文字完整显示、垂直居中，不被底部说明遮挡；
3. 类型/实例、覆盖已有绑定、多族链继续可用；
4. 不要求批量版直接赋项目 Material——这是简易版职责。

## G. Assistant 退役数据专项

这部分属于当前源码新增链路，旧 FINAL3/MATERIAL_BINDING 证据不能代替。

### G1. Revit 启动/当前 DLL

在测试用户目录预置：

```text
%APPDATA%\YangTools\Assistant\
%APPDATA%\YangTools\copilot_settings.json
%APPDATA%\YangTools\copilot_history.json
%APPDATA%\YangTools\moyu_settings.json
%APPDATA%\YangTools\moyu_history.json
```

同时预置保护样本：

```text
%APPDATA%\YangTools\MCP\keep.txt
%APPDATA%\YangTools\keep-current-setting.txt
```

启动部署后的当前 DLL，验证：

- `Assistant\` 和四个 copilot/moyu 文件全部删除；
- `%APPDATA%\YangTools\MCP\` 完整保留；
- 其它当前 YangTools 设置不被误删；
- 清理重复执行无异常（幂等）；
- AI/摸鱼 Ribbon/窗口仍不存在，MCP 仍可加载。

### G2. 部署/安装/更新/卸载脚本

至少做脚本级/隔离目录验证一次：

- `tools/Build-Deploy-2024-2025.ps1`；
- `tools/Build-All.ps1 -Deploy`；
- `installer/Install.ps1`；
- `installer/Uninstall.ps1`；
- `installer/YangTools安装器.ps1`。

只允许删除明确列出的退役 Assistant/copilot/moyu 数据；MCP 和其它当前设置必须保持。

## H. 文档与交付

如果修了真实问题，同步：

- README.md
- PROJECT.md
- WORKLOG.md
- docs/HANDOFF_3.1.35.md
- docs/FEATURE_AUDIT_3.1.35.md
- docs/REVIT_2021_2027_COMPATIBILITY_3.1.35.md
- docs/SIMPLE_FAMILY_MATERIAL_BINDING_3.1.35_20260821.md
- docs/NEW_CHAT_PROMPT_3.1.35.txt

最终交付：

1. 修复后完整源码 ZIP；
2. 构建/部署证据；
3. Revit Journal、YangTools 日志和专项结果 JSON/截图；
4. GO / 条件 GO / NO-GO；
5. SHA-256。

## I. 发布规则

本轮不是分享发布任务，不生成正式 installer payload。

**当前项目目录中即使存在 `installer/` 或历史 `payload`，也不能视为本候选源码的可分享版本。** 没有使用当前源码重新编译、完成对应 Revit 实机 PASS、重新写入 payload 并验证安装/覆盖更新/卸载前，禁止把安装器发给别人。

日常只需编译+直接部署。只有用户明确“需要分享”时才：真实编译目标年份 → 实机验收 → 写 `installer/payload/{年份}` → 生成自包含安装/更新/卸载包 → 干净机验证 → SHA-256。
