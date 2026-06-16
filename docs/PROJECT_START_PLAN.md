# PROJECT_START_PLAN

# YangTools 项目启动规划 v1.0

## 1. 项目定位

YangTools 是 Yang 个人使用的 Revit AI 工具箱 / MCP 控制插件。

它不是商业软件，不做企业级平台，不追求复杂架构。第一阶段只做一个轻量、可用、可交接的 Revit 工具箱框架。

核心目标：

```text
自然语言需求
→ AI 判断需要调用哪个 Tool
→ MCP/HTTP 发送请求
→ Revit 插件执行
→ 写操作 Transaction 可撤销
→ 记录 operation log
→ DeepSeek / Gemini 写 WORKLOG
→ Codex 审查
→ 成功后并入主线
```

项目优先级：

```text
能用 > 简单 > 可撤销 > 有日志 > 可交接 > 架构漂亮
```

---

## 2. 基本约定

```text
项目名：YangTools
主测版本：Revit 2024
正式主线语言：C#
Python 定位：临时脚本、旧插件迁移参考、dev 调试
MCP v0：本地 HTTP
接口地址：POST http://127.0.0.1:8081/mcp/
```

Revit 2021-2027 是长期目标，不是 v0.1 验收目标。  
v0.1 只验收 Revit 2024。

---

## 3. 角色分工

### Codex：唯一管理者

Codex 负责：

1. 项目规则。
2. 技术规范。
3. 任务拆分。
4. 代码审查。
5. Git 主线合并。
6. 版本发布。
7. 关键 Bug 修复。
8. 生成 DeepSeek / Gemini 任务卡。
9. 判断旧插件保留、迁移、重写或废弃。

Codex 不负责大量业务代码。  
Codex 额度有限，优先用于规则、审查、合并、关键问题和技术决策。

### DeepSeek：正式代码工人

DeepSeek 负责：

1. 按 Codex 任务卡写 C# 代码。
2. 实现 MCP Tool。
3. 迁移旧插件。
4. 修复普通 Bug。
5. 写 WORKLOG。
6. 写 HANDOFF_TO_CODEX。

DeepSeek 不允许私自修改 MCP 协议。  
DeepSeek 不允许私自重构核心框架。

### Gemini：白天临时开发工人

Gemini 用于工作地点白天临时开发。

Gemini 可以：

1. 临时写 Revit 插件。
2. 临时写 MCP Tool。
3. 临时修 Bug。
4. 临时满足当天工作需求。
5. 整理旧插件。
6. 写交接日志。

Gemini 写的代码默认不可信，属于临时代码。  
必须晚上打包源码交给 Codex 审查，通过后才能进入主线。

### Yang

Yang 负责：

1. 提需求。
2. 在 Revit 2024 实机测试。
3. 截图报错。
4. 打包 Gemini 临时代码。
5. 晚上交给 Codex。
6. 判断功能是否符合工作习惯。

---

## 4. v0.1 冻结范围

v0.1 只做最小闭环。

### 必做

1. Revit 2024 插件能加载。
2. HTTP `/mcp/` 能启动。
3. MCP 请求/返回格式固定。
4. ToolRouter 可用。
5. OperationLogger 可用。
6. TransactionRunner 可用。
7. `revit.status` 可用。
8. `revit.selection.read` 可用。
9. `revit.parameter.get` 可用。
10. `revit.parameter.set` 可用。
11. `revit.parameter.set` 支持 dryRun。
12. `revit.parameter.set` 正式执行后可 Ctrl+Z 撤销。
13. 每次 MCP 操作写 operation log。
14. 每次代码修改写 WORKLOG。
15. 每次交接写 HANDOFF_TO_CODEX。
16. 每次新对话可通过 CURRENT_STATE 接手。

### 可选

1. 整理旧 Gemini 插件清单。
2. 迁移 1 个只读旧插件，例如中文检查或文件统计。

### v0.1 不做

1. 不完整适配 Revit 2021-2027。
2. 不做复杂 AI 聊天面板。
3. 不做不开 Revit 修改 RVT。
4. 不做 APS 云端批处理。
5. 不迁移所有旧插件。
6. 不做复杂几何工具。
7. 不大改 UI。
8. 不重写所有 Gemini 旧代码。

---

## 5. 最小项目结构

```text
YangTools/
├─ src/
│  ├─ YangTools.Revit/
│  │  ├─ App.cs
│  │  ├─ Mcp/
│  │  │  ├─ McpServer.cs
│  │  │  ├─ McpRequest.cs
│  │  │  └─ McpResponse.cs
│  │  ├─ Tools/
│  │  │  ├─ IRevitTool.cs
│  │  │  ├─ ToolRouter.cs
│  │  │  ├─ RevitStatusTool.cs
│  │  │  ├─ SelectionReadTool.cs
│  │  │  ├─ ParameterGetTool.cs
│  │  │  └─ ParameterSetTool.cs
│  │  ├─ Utils/
│  │  │  ├─ TransactionRunner.cs
│  │  │  ├─ OperationLogger.cs
│  │  │  └─ JsonUtils.cs
│  │  └─ UI/
│  └─ Legacy/
│     └─ GeminiOldPlugins/
├─ docs/
│  ├─ PROJECT_START_PLAN.md
│  ├─ AGENT_RULES.md
│  ├─ CURRENT_STATE.md
│  ├─ TOOL_INDEX.md
│  ├─ WORKLOG.md
│  ├─ DECISIONS.md
│  └─ summaries/
├─ tasks/
│  ├─ CURRENT_TASK.md
│  ├─ HANDOFF_TO_CODEX.md
│  ├─ TASK_TEMPLATE_DEEPSEEK.md
│  ├─ TASK_TEMPLATE_GEMINI.md
│  └─ CODEX_REVIEW_CHECKLIST.md
└─ logs/
   ├─ operations/
   └─ conversations/
```

第一阶段不要拆成复杂企业架构。  
后续工具超过 20 个后，再考虑拆分模块。

---

## 6. 文档策略

文档不能太多，但日志必须长期保留。

### AI 每次必读

1. `docs/AGENT_RULES.md`
2. `docs/CURRENT_STATE.md`
3. `tasks/CURRENT_TASK.md`
4. `tasks/HANDOFF_TO_CODEX.md`
5. `docs/TOOL_INDEX.md`

### 长期日志

1. `docs/WORKLOG.md`
2. `logs/conversations/`
3. `logs/operations/`

长期日志可以很长，但 AI 每次不需要全读。

### 定期摘要

1. `docs/summaries/`

规则：

1. 每完成 5 个任务，Codex 必须更新一次 `CURRENT_STATE.md`。
2. 每完成 10 个任务，Codex 必须生成一次阶段总结。
3. `WORKLOG.md` 保留完整历史，但 `CURRENT_STATE.md` 才是新 AI 每次必读摘要。

---

## 7. MCP v0 协议

统一入口：

```text
POST http://127.0.0.1:8081/mcp/
```

请求格式：

```json
{
  "tool": "revit.selection.read",
  "args": {}
}
```

返回格式：

```json
{
  "ok": true,
  "tool": "revit.selection.read",
  "data": {},
  "error": null,
  "warnings": []
}
```

写操作示例：

```json
{
  "tool": "revit.parameter.set",
  "args": {
    "target": "selection",
    "parameterName": "备注",
    "value": "AI测试",
    "dryRun": true
  }
}
```

禁止：

1. 不允许每个工具新增 endpoint。
2. 不允许私自修改请求格式。
3. 不允许私自修改返回格式。
4. 不允许正式功能依赖 eval_python。
5. 不允许绕过 ToolRouter。

---

## 8. Tool 命名规范

格式：

```text
revit.<模块>.<动作>
legacy.<来源>.<动作>
agent.<模块>.<动作>
dev.<模块>.<动作>
```

示例：

```text
revit.status
revit.selection.read
revit.parameter.get
revit.parameter.set
revit.sheet.list
revit.sheet.rename
revit.view.list
revit.text.check_chinese
legacy.gemini.chinese_check
legacy.gemini.file_stats
dev.eval_python
```

---

## 9. 写操作规范

所有写模型操作必须满足：

1. 必须使用 Transaction 或 TransactionGroup。
2. 批量写操作必须支持 dryRun。
3. `dryRun=true` 时不得修改模型。
4. `dryRun=false` 时才正式修改模型。
5. 修改成功后必须能 Ctrl+Z 撤销。
6. 失败时必须 RollBack。
7. 必须写 operation log。
8. 必须返回 changedItems 和 failedItems。
9. 参数不存在、只读、类型不匹配时不得崩溃。

禁止：

1. 自动保存模型。
2. 覆盖保存模型。
3. 自动同步中心模型。
4. 默认删除大量元素。
5. 默认清空大量参数。
6. 吞异常。
7. 写死本机路径。

---

## 10. 第一批任务

### TASK-000：初始化 GitHub 主线与可编译 Revit 插件骨架

目标：在 TASK-001 前，先固定 GitHub 主线、最小 Revit 2024 插件骨架、.addin 清单、引用路径策略和日志规则。

必须完成：

1. 确认 GitHub 仓库 `5788324/YANG-TOOLS_REVIT` 是唯一主线。
2. 把当前 YangTools 文档骨架同步进仓库。
3. 创建最小 C# / Visual Studio 项目骨架。
4. 创建 Revit 2024 插件基础结构。
5. 准备 `.addin` 清单。
6. 配置 RevitAPI / RevitAPIUI 引用路径策略。
7. 添加 `.gitignore`。
8. 更新 `docs/CURRENT_STATE.md`。
9. 写 `docs/WORKLOG.md`。
10. 写 `tasks/HANDOFF_TO_CODEX.md`。

### TASK-001：MCP Request / Response

目标：实现 `McpRequest` 和 `McpResponse` 的统一结构。

验收：

1. POST `/mcp/` 能解析 tool 和 args。
2. 未知 tool 能返回标准错误。

### TASK-002：ToolRouter

目标：根据 `request.tool` 分发到对应 `IRevitTool`。

验收：

1. `revit.status` 能通过 ToolRouter 调用。
2. 未知 tool 返回错误。

### TASK-003：OperationLogger

目标：每次 MCP 调用写 `logs/operations/YYYY-MM-DD.jsonl`。

验收：

1. 成功和失败请求都能写日志。

### TASK-004：TransactionRunner

目标：统一封装 Transaction / TransactionGroup。

验收：

1. 写操作成功 Commit。
2. 失败 RollBack。
3. Revit Ctrl+Z 可撤销。

### TASK-005：revit.status

目标：返回 Revit / 当前文档 / MCP 状态。

返回建议：

1. Revit 版本。
2. 当前文档名称。
3. 当前文档路径。
4. 是否有活动文档。
5. 已注册 Tool 数量。

### TASK-006：revit.selection.read

目标：读取当前选择元素基础信息。

返回：

1. ElementId。
2. Category。
3. Name。
4. FamilyName。
5. TypeName。
6. DocumentName。

### TASK-007：revit.parameter.get

目标：读取当前选择元素参数。

返回：

1. 参数名。
2. 参数值。
3. StorageType。
4. 是否只读。

### TASK-008：revit.parameter.set

目标：批量修改当前选择元素指定参数。

要求：

1. 支持 dryRun。
2. 正式执行使用 Transaction。
3. 可 Ctrl+Z 撤销。
4. 返回 changedItems / failedItems。
5. 写 operation log。

### TASK-009：旧 Gemini 插件清单

目标：整理 `src/Legacy/GeminiOldPlugins/` 下的旧插件。

字段：

1. 插件名称。
2. Ribbon 位置。
3. 源码入口。
4. 功能说明。
5. 是否修改模型。
6. 是否有 Transaction。
7. 是否适合迁移为 MCP Tool。
8. 建议：保留 / 包装 / 重写 / 废弃。

---

## 11. 旧插件迁移顺序

优先迁移：

1. 中文检查。
2. 文件统计。
3. 图纸列表。
4. 视图列表。
5. CAD 导入列表。
6. 链接模型列表。
7. 参数读取。

第二批：

1. 图纸重命名。
2. 视图重命名。
3. 批量改参数。
4. 标高修改。
5. 标注替换。
6. 批量导出 PDF。
7. 批量导出 NWC。

暂缓迁移：

1. 布尔几何。
2. Loft 实体生成。
3. 基于面转换。
4. 从 CAD 粘贴。
5. 剖面 By Line。
6. 复杂族实例管理。

---

## 12. Codex 审查清单

Codex 每次审查必须检查：

1. 是否修改 MCP 基础协议。
2. 是否新增 endpoint。
3. 是否绕过 ToolRouter。
4. 是否把 eval_python 当正式功能。
5. 写操作是否使用 Transaction。
6. 批量写操作是否支持 dryRun。
7. 正式写操作是否可 Ctrl+Z 撤销。
8. 是否写 operation log。
9. 是否吞异常。
10. 是否写死本机路径。
11. 是否大范围修改无关文件。
12. 是否更新 WORKLOG。
13. 是否更新 HANDOFF_TO_CODEX。
14. 是否更新 TOOL_INDEX。
15. 是否更新 CURRENT_STATE。
16. 是否更新 conversations 日志。
17. 是否能编译。
18. 是否应该合并主线。

审查结论只能是：

```text
允许合并
小修后合并
退回重写
废弃
```

---

## 13. 版本命名规则

```text
v0.1-dev      开发中
v0.1-test     可测试
v0.1-stable   Codex 审查通过，可给 Gemini 白天正式使用
```

Gemini 白天代码只能标记为：

```text
temp
experimental
gemini-work
```

不能直接标记为 stable。

---

## 14. 失败回退规则

DeepSeek / Gemini 写坏时，不允许无限修坏代码。Codex 可以直接：

1. 回退到上一个 BASE 包。
2. 废弃本次修改。
3. 只保留需求说明。
4. 重新生成任务卡。

---

## 15. v0.1 验收标准

v0.1 完成标准：

1. Revit 2024 插件能加载。
2. Ribbon 或最小按钮能出现。
3. HTTP `/mcp/` 能启动。
4. POST `/mcp/` 能返回统一格式。
5. `revit.status` 可用。
6. ToolRouter 可用。
7. `revit.selection.read` 可用。
8. `revit.parameter.get` 可用。
9. `revit.parameter.set` 可 dryRun。
10. `revit.parameter.set` 可正式执行。
11. 正式执行后 Revit Ctrl+Z 可撤销。
12. 每次 MCP 请求写 operation log。
13. 每次 AI 对话写 conversation log。
14. 每次代码修改写 WORKLOG。
15. 每次交接写 HANDOFF_TO_CODEX。
16. 每个 Tool 记录在 TOOL_INDEX。
17. 当前状态记录在 CURRENT_STATE。
18. 每个关键决策记录在 DECISIONS。

---

## 16. Codex 本轮启动任务

Codex 本轮不要写大量业务代码。

Codex 本轮只做：

1. 创建项目目录。
2. 创建基础文档。
3. 创建日志模板。
4. 固定 MCP v0 协议。
5. 创建 Tool 开发规范。
6. 创建 Codex 审查清单。
7. 创建 DeepSeek 任务卡模板。
8. 创建 Gemini 临时开发模板。
9. 创建第一批 TASK-001 到 TASK-009。
10. 生成 YangTools_BASE 包。

代码实现交给 DeepSeek / Gemini。

---

## 17. 项目启动后流程

```text
Codex 建规则和任务
↓
DeepSeek 写正式代码
↓
Codex 审查合并
↓
生成 stable BASE 包
↓
Gemini 白天使用 stable 包
↓
Gemini 临时写功能
↓
晚上打包给 Codex
↓
Codex 审查后决定是否进入主线
```

---

## 18. 最终目标

YangTools 最终要形成：

1. AI 可调用 Revit 工具。
2. 旧插件可逐个迁移。
3. 新插件可按任务卡快速开发。
4. Revit 写操作可撤销。
5. 所有 MCP 操作有日志。
6. 所有代码修改有工作日志。
7. 新 AI / 新对话可根据 CURRENT_STATE 和日志继续接手。
8. Codex 始终掌控主线。
