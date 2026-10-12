# YangTools 3.1.35 DeepSeek/Codex：绑定材质编译部署与验收任务

## 约束

- 使用全新空目录解压当前源码 ZIP，禁止覆盖旧工作树。
- 不新增功能，不推送 Git。
- 先执行源码基线和所有静态脚本，再编译。
- Revit GUI、真实编译、部署、Journal/日志由本机 AI 独立完成，不要求用户手工测试。

## A. 源码与构建

1. 运行 `tools/Verify-SourceBaseline.ps1`。
2. 运行 `tools/Test-UiPanelsStatic.py`。
3. 运行现有所有专项测试，额外运行：
   - `tools/Test-FamilyMaterialBinding.py`
   - `tools/Test-AssistantDataCleanup.py`
   - `tools/Test-FamilyCategoryChange.py`
4. 编译实际可构建的 2021–2027 Configuration；每年记录 error/warning、DLL 大小和 SHA-256。
5. 至少部署 Revit 2024、2025；确认 Ribbon 总数为 29，新增 `项目管理区 → 绑定材质`，AI 助手入口仍不存在。

## B. Revit 2024/2025 绑定材质专项

使用测试项目副本，准备至少：

- A 族：2 个以上实体，原来没有材质族参数；
- B 族：已有 Material 类型参数并已绑定部分实体；
- C 族：已有 Material 实例参数；
- D 族：同名参数存在但不是 Material；
- E 族：没有可绑定 Material 参数的几何；
- F 族：包含嵌套族（用于确认只改当前族自身可绑定元素）。

### B1 类型参数

- 选 A 族；参数名 `测试材质`；选择“类型参数”；覆盖已有绑定开启。
- 执行后重新打开族文档验证：
  - 参数存在；
  - Material 数据类型；
  - 为类型参数；
  - 当前族所有可关联几何的 Material 属性均关联到 `测试材质`。
- 项目中每个 FamilySymbol 能看到 `测试材质` 参数。

### B2 实例参数

- 重新准备干净 A 族或使用另一个族；选择“实例参数”。
- 验证已有 FamilyInstance 能看到该 Material 实例参数。

### B3 多族批量

- 同时选择 A/B/C/F 至少 4 个族；
- 结果必须逐族列出几何总数、新绑定、已绑定、重新绑定、跳过。
- 单族无可绑定元素不得造成其它族失败。

### B4 已有其它绑定覆盖

- B 族已有另一个材质参数控制部分实体。
- `覆盖已有的其它材质参数绑定=true` 时，应统一改绑到目标参数。
- `false` 时，应保留原绑定并在结果中计入跳过。

### B5 同名冲突

- D 族同名参数不是 Material，或 Material 的类型/实例模式与本次选择不同。
- 必须安全失败并提示原因；禁止自动删除/替换旧参数。

### B6 数据完整性

记录修改前后：

- Family ElementId；
- 所有 FamilySymbol ElementId；
- 已有 FamilyInstance ElementId；
- 既有正 ID 自定义参数内部值。

这些既有数据不得静默改变。

### B7 撤销

- 成功批量处理 2 个以上族；
- 一次 Ctrl+Z 后恢复到执行前状态；
- 再次打开族文档确认新增参数/绑定已撤销。

## C. 回归

- `族类别修改`仍能打开和执行最小样本；
- AI/摸鱼助手代码/入口不存在，旧 Assistant 数据清理仍执行；
- MCP v1 仍能正常启动；本轮禁止升级 SDK v2。

## D. 证据

提交：

- build/restore 日志与 binlog；
- DLL 和部署 DLL SHA-256；
- Revit Journal、YangTools 日志；
- 绑定前后族参数与几何关联清单（JSON/CSV 均可）；
- 失败样本和 Ctrl+Z 结果；
- 更新 README/PROJECT/WORKLOG/HANDOFF/FEATURE_AUDIT/兼容说明；
- 完整源码 ZIP 与 SHA-256。

## 判定

- 仅静态通过：条件 GO；
- 2024/2025 编译+专项实机通过：该功能 GO；
- 数据完整性、回载或撤销失败：NO-GO。
