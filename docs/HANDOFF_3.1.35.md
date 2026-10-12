# YangTools 3.1.35 当前交接

更新时间：2026-08-21  
当前 Windows 复验候选源码：`YangTools_Project_3.1.35_Source_SIMPLE_MATERIAL_P0_Z_GATE_20260821.zip`

输入交接基线：`YangTools_Project_3.1.35_Source_SIMPLE_MATERIAL_20260821.zip`

## 0. 2026-08-21 接手后 P0 加固

- 当前状态：**STATIC GO / WINDOWS RETEST REQUIRED**。
- 本轮架构体检不建议大重构；大型 code-behind 暂记技术债，当前只做会直接影响 P0 正确性的定点修复。
- 线性布置候选 Offset 必须通过真实 `LocationPoint.Z` 门禁：`|实际 LocationPoint.Z - 理论 WorldZ| <= 1 mm`。
- 每个实例日志必须记录 `FamilyPlacementType`、`LevelId`、`Level.Elevation`、`Level.ProjectElevation`、实际 `BuiltInParameter`、`Offset.AsDouble()`、理论 WorldZ、`LocationPoint.Z`、误差。
- 候选 Offset 不生效时恢复原值再尝试下一个；无法恢复则立即失败并回滚，禁止叠加错误 Offset。
- P0 若失败，停止材质等后续专项并先修线性布置。
- Assistant 退役数据必须加入实机：旧 Assistant/copilot/moyu 数据删除，`%APPDATA%\YangTools\MCP\` 和其它当前设置不变。
- 当前 `installer/payload` 不是 `YangTools_Project_3.1.35_Source_SIMPLE_MATERIAL_P0_Z_GATE_20260821.zip` 的正式分享产物；只有用户明确要求分享时，才在当前 DLL 编译和实机 PASS 后重建 payload 并验证安装/更新/卸载。

## 1. 接手原则

- 本文、`README.md`、`PROJECT.md`、`WORKLOG.md` 和当前源码是事实来源；旧聊天、旧 ZIP、历史 DLL 只能作为历史证据。
- 用户不做手工编译/部署/GUI 测试；需要 Windows、MSBuild、Revit GUI、真实部署的部分交给 DeepSeek/Codex 自动执行。
- 当前不要推送 Git。代码完成后提供完整源码 ZIP、变更报告、静态检查和本机验收任务。
- 用户是代码/AI 小白；交流、UI、说明、验收文档使用中文。
- 项目个人使用，优先效率；不做无价值的企业级流程，但所有模型写操作必须可撤销，不能静默丢数据。

## 2. 当前项目规模

- 版本：YangTools 3.1.35。
- Revit：2021–2027，各年份独立编译，禁止跨年份复用 DLL。
- 当前源码：141 个 C#、41 个 XAML、36 个窗口/视图根节点、4 套主题。
- Ribbon：**30 个命令**。
- 当前静态 UI：0 错误、0 警告。
- 当前源码自上一真实编译基线后已多次修改，因此**当前 DLL 尚未建立 Windows 编译/实机证据**。

## 3. 2026-08-21 最新功能变化

### 3.1 绑定材质（简易）

入口：`项目管理区 → 绑定材质`

流程：

1. 使用当前预选的一个/多个 `FamilyInstance`；没有预选时进入 Revit 多选实例。
2. 输入材质参数名称，默认 `材质`。
3. 选择实例参数/类型参数；简易版默认实例参数。
4. 搜索并选择当前项目已经存在的 `Material`。
5. 自动编辑所选实例所属载入族，创建/复用 Material FamilyParameter，把族内可绑定几何的 Material 参数关联到它并回载项目。
6. 实例模式只给所选实例写 `Material.Id`；类型模式给所选实例对应的 FamilySymbol 写值，同类型其它实例会随类型一起变化。
7. 族编辑 + 项目材质赋值合并在外层 `TransactionGroup` 中；目标为一次 Ctrl+Z 撤销。
8. 若没有任何目标成功赋材质，整次操作回滚，避免只留下空参数。

### 3.2 批量绑定材质

原 `绑定材质` 改名为 `批量绑定材质`，继续面向 Family 列表多选批处理：只建立/统一族几何 Material 与族参数的关联，不主动指定项目 Material。

2026-08-21 修复了批量窗口参数名称输入框被底部区域挤压的问题：参数名称和参数方式拆为两行，输入框高度 34、Padding 8,2、垂直居中。

### 3.3 MCP v2

用户决定：**只记录，不迁移。**

- 当前 `McpServer/requirements.txt` 继续 `mcp>=1.2.0,<2`。
- MCP Python SDK v2 / MCP `2026-07-28` 作为后续独立任务。
- 禁止在普通 Revit 功能开发中顺带升级 MCP。

## 4. 当前 30 个 Ribbon 功能

### 导入工具区
- 从 CAD 粘贴

### 总控中心
- 批处理与云链接

### 文本工具区
- 基准对齐
- 对齐到线
- 文本修改
- 文本合并
- 等距分布

### 标注工具区
- 标注替换

### 检查工具区
- 中文检查
- 重复项目参数清理

### 模型修改区
- 基于面转换
- 实体提取
- 实体生成（Loft）
- 布尔几何
- 标高修改
- 沿线布柱
- 线性布置

### 系统管理区
- 系统设置

### 视图修改区
- 剖面（By Line）
- 可见性拷贝
- 覆盖清理

### 项目信息区
- 文件信息

### 项目工具区
- 临时插件箱

### 项目管理区
- 图纸管理
- 族实例管理
- 族文档管理
- 族类别修改
- 绑定材质
- 批量绑定材质
- 项目资产管理器

## 5. 高风险/未闭环事项

### 5.1 线性布置高度

这是当前最高优先级运行风险。

用户曾实机报错：期望 Z `29.527559 ft`、实际 `18.044619 ft`，固定误差 `11.48294 ft`。先后排查了 `Level.Elevation/ProjectElevation` 与 level-based Z 约束。当前最新源码已经改为 `FamilyPlacementHelper.CreateAtLevelRelativeElevation`：通过 `INSTANCE_ELEVATION_PARAM / INSTANCE_FREE_HOST_OFFSET_PARAM / FAMILY_BASE_LEVEL_OFFSET_PARAM` 控制相对标高，不再依赖 `MoveElement` 修正 level-based Z。

**这个最新 LevelOffset 版本尚未拿到用户原模型实机 PASS。** DeepSeek/Codex 下一轮必须优先在用户原问题项目/原族复验，不能只跑静态数学测试。

### 5.2 当前新增功能均需重新编译实测

自上一真实 Windows 编译基线之后，又新增/修改了：

- 打印 PDF；
- 中文检查与清理；
- 线性布置高程及后续 LevelOffset 修复；
- 删除 AI/摸鱼助手；
- 清理旧 Assistant 数据；
- 族类别修改；
- 批量绑定材质；
- 本轮简易绑定材质。

因此旧 FINAL3/MATERIAL_BINDING DLL 不代表当前源码。

## 6. 必须保持的技术决策

- 不创建第二个 WPF `Application`，不向 Revit 全局资源注入 YangTools 主题。
- 无模式窗口访问 Revit API 必须走 ExternalEvent；禁止 `Task.Run` 直接访问 Document。
- 重复项目参数清理：保留原参数合并，内部值迁移；禁止全部删除同名重建；禁止 `LookupParameter(name)` 做清理主链；删除后做值完整性和唯一性复核。
- AI/摸鱼助手已经整体删除，禁止恢复 Copilot/Assistant/DockablePane 聊天 UI。旧 `%APPDATA%\YangTools\Assistant\` 和 copilot/moyu settings/history 会由启动/部署/安装/卸载清理；MCP 数据不删除。
- 族类别修改：使用 `EditFamily → FamilyCategory → LoadFamily`，禁止删除重建族；回载后验证 Family/Symbol/Instance ID 与自定义参数数据。
- 材质绑定：同名参数不是 Material 或类型/实例模式冲突时失败，禁止删除重建旧参数；回载使用 `overwriteParameterValues=false` 并做数据安全复核。
- 临时插件必须有功能说明/使用步骤，并按目标 Revit 年份独立编译。

## 7. 编译/发布规则

日常个人开发默认：

`ChatGPT 修改源码 → DeepSeek/Codex 本机独立编译目标年份 → 直接部署 → 自动实机验收`

**不需要每轮生成安装包。**

只有用户明确说“需要分享给别人”时才进入发布流程：

`真实编译目标年份 → 对应 Revit 实机验收 → 将 DLL/依赖/资源写入 installer/payload/{年份} → 生成自包含安装/更新/卸载包 → 干净环境验证 → SHA-256`

接收者不应需要源码、Visual Studio、.NET SDK 或 AI 工具。

## 8. 下一轮固定任务

1. 在全新空目录解压当前源码。
2. 运行 `tools/Verify-SourceBaseline.ps1` 和全部 Python 静态专项，包括 `Test-SimpleFamilyMaterialBinding.py`。
3. Windows 独立编译 2021–2027 可用 Configuration；至少 2024/2025 必须成功，2027 若仍缺 .NET 10 SDK则记录真实阻塞。
4. 部署当前 2024/2025 DLL，确认 30 Ribbon 注册。
5. **第一优先**复验线性布置原错误项目。
6. 实测简易绑定材质：预选、无预选、多实例、多族、重新选择、实例模式、类型模式、材质搜索、同名冲突、不可编辑族、已有其它绑定、失败回滚、一次 Ctrl+Z。
7. 实测批量绑定材质参数名称 UI 和核心批量链。
8. 只修真实编译/实机问题，不继续新增功能。
9. 同步 README、PROJECT、WORKLOG、FEATURE_AUDIT、兼容性说明和本 HANDOFF。

执行任务见：`docs/DEEPSEEK_CODEX_SIMPLE_MATERIAL_ACCEPTANCE_3.1.35_20260821.md`。

## 9. 当前判定

- 当前源码：**STATIC GO**。
- Windows 编译：**RETEST REQUIRED**。
- Revit 2024/2025：**RETEST REQUIRED**。
- Revit 2021–2027 全版本兼容：**未证明**。
- 正式发布：**NO-GO**。
