# YangTools 3.1.35 族类别修改

日期：2026-08-14

## 1. 功能定位

Ribbon：`项目管理区 → 族类别修改`

本功能修改的是 **Revit Family Category（族类别）**，不是 FamilySymbol/族类型名称。

典型用途：

```text
Abutments → Generic Models
桥台类别族 → 常规模型
```

Revit API 的正确链路是：在项目中通过 `Document.EditFamily()` 打开可编辑载入族，在族文档事务中设置 `OwnerFamily.FamilyCategory`，再 `LoadFamily()` 回载项目。

## 2. UI

窗口显示全部项目 Family，并提供：

- 族名称；
- 当前类别；
- 类型数量；
- 实例数量；
- 可编辑状态；
- Family ElementId；
- 搜索；
- 单独勾选；
- 全选 / 反选 / 全不选；
- 目标类别选择。

目标类别默认优先 `Generic Models / 常规模型`。

系统族不能通过 Family API 编辑；内建族和不可编辑 Family 只显示状态并跳过。

## 3. 执行算法

每个待修改 Family：

1. 检查 `Family.IsEditable`、`Family.IsInPlace`；
2. 记录修改前安全快照；
3. `Document.EditFamily(family)`；
4. 在 family document 中启动 Transaction；
5. 获取目标 `Category`；
6. `familyDocument.OwnerFamily.FamilyCategory = target`；
7. Commit 族文档事务；
8. `familyDocument.LoadFamily(projectDocument, FamilyOption)` 回载；
9. 验证回载后的 `FamilyCategory`；
10. 验证数据完整性；
11. 关闭临时 family document，不另存 RFA。

项目侧使用一个 `TransactionGroup` 聚合本批次成功回载，目标是形成一个 Revit 撤销项。

## 4. 数据保护

类别变更可能改变 Revit 内置的类别专用参数，这是 Revit 自身的类别语义变化，不能把所有负 ID BuiltInParameter 强行要求保持不变。

YangTools 对用户数据采用更严格的门禁：

修改前记录：

- Family ElementId；
- 全部 FamilySymbol ElementId；
- 已有 FamilyInstance ElementId；
- 上述类型和实例上 **Parameter.Id > 0** 的项目参数、共享参数和其它可持久化自定义参数内部值。

回载后必须满足：

- Family ElementId 不变；
- FamilySymbol ElementId 集合不变；
- 原实例仍存在；
- 正 ID 自定义参数仍存在；
- Double / Integer / String / ElementId 内部值保持一致。

如果族已经回载项目后才发现任一上述异常，抛出安全异常并回滚整个 `TransactionGroup`，禁止留下“部分成功但数据已变化”的项目状态。

不支持目标类别、EditFamily 失败等发生在项目回载前的单族错误，可以记录并继续其它族。

## 5. AI / 摸鱼助手删除

同一轮维护已彻底删除 YangTools 内置 AI/摸鱼助手的运行功能和源码：

- Ribbon `AI 助手`；
- `CopilotCommand`；
- `Core/Assistant/`；
- Assistant/Copilot WPF；
- DockablePane provider 和启动注册；
- 助手网络客户端、模式存储、历史管理；
- 助手图标；
- 已无引用的 `RevitApiSearcher`；
- 仅由助手使用的 ProtectedData NuGet 依赖。

MCP 网关与 `RevitExecutionBroker` 独立保留，不属于聊天助手。

用户现已明确要求**彻底删除**旧助手用户数据。当前实现会幂等清理：

- `%APPDATA%\YangTools\Assistant\` 整个目录（Work/Chat 的 `settings.json`、`prompts.json`、`history.json`、`api_key.dat` 等）；
- `%APPDATA%\YangTools\copilot_settings.json`；
- `%APPDATA%\YangTools\copilot_history.json`；
- `%APPDATA%\YangTools\moyu_settings.json`；
- `%APPDATA%\YangTools\moyu_history.json`。

清理触发点包括 Revit 启动、GUI/命令行安装或更新、`Build-All.ps1 -Deploy`、`Build-Deploy-2024-2025.ps1` 直接部署和卸载。清理范围严格限定在退役助手数据，不删除 `%APPDATA%\YangTools\MCP` 或其它仍在使用的 YangTools 设置。

## 6. 必须实机验证

Windows/Revit 必测：

1. 2021–2026 当前源码独立编译；2027 只有具备 .NET 10 SDK 时才构建；
2. Revit 2024、2025 启动，Ribbon 总数 28；
3. 不再出现 `AI 助手` Ribbon，启动不注册 YangTools Assistant DockablePane；
4. MCP 状态/网关仍可正常使用；
5. 一个真实 `Abutments → Generic Models`；
6. 多个 Family 混合批量修改；
7. 已经是目标类别时跳过；
8. 内建/不可编辑族跳过；
9. 不兼容目标类别时该族失败但未回载项目；
10. Family / FamilySymbol / FamilyInstance ID 和正 ID 自定义参数内部值前后对比；
11. 整批 `Ctrl+Z` 恢复原类别；
12. 无 `Document.IsModifiable` 泄漏、无重复族、无重复实例。

## 7. 当前状态

- 源码实现：GO；
- 静态契约：GO；
- Windows 编译：待当前源码重新执行；
- Revit 2024/2025 实机：待 DeepSeek/Codex；
- 正式发布：NO-GO。
