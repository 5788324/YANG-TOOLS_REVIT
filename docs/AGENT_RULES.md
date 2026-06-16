# AGENT_RULES

本文件是 YangTools 项目的硬规则。任何 AI 新开对话、接手任务、修改代码前必须先读。

## 1. 项目原则

优先级固定：

```text
能用 > 简单 > 可撤销 > 有日志 > 可交接 > 架构漂亮
```

YangTools 是个人 Revit 工具箱，不是商业平台，不做复杂企业架构。

## 2. 主线唯一性

GitHub 仓库是唯一主线：

```text
5788324/YANG-TOOLS_REVIT
```

本地目录都只是工作区：

```text
G:\Codex\...              Codex 工作区
G:\Hermes Agent\...       DeepSeek / Hermes 工作区
Gemini 白天目录           临时工作区
```

任何本地工作区都不是最终真相源。最终以 GitHub 主线为准。

## 3. 命名统一

正式命名统一为：

```text
仓库名：YANG-TOOLS_REVIT
项目名：YangTools
程序集 / 命名空间：YangTools.Revit
插件显示名：YangTools
文档称呼：YangTools
```

本地目录名暂时可以不改，但代码和文档不再混用 `YangAgent`、`RevitWorkAgent`。

## 4. 角色规则

### Codex

Codex 是唯一管理者，负责：

- 项目规则
- 技术规范
- 任务拆分
- 代码审查
- Git 主线合并
- 版本发布
- 关键 Bug 修复
- 判断旧插件保留、迁移、重写或废弃

Codex 不负责大量业务代码。

### DeepSeek / Hermes

DeepSeek / Hermes 是正式代码工人，只能按 Codex 任务卡写代码。

他们只能提交：

```text
源码包
patch
修改说明
WORKLOG
HANDOFF_TO_CODEX
测试说明
```

他们不直接推主线，不直接决定合并。

DeepSeek / Hermes 不允许：

- 私自修改 MCP 协议
- 私自重构核心框架
- 私自新增 endpoint
- 私自引入大型依赖
- 私自把临时代码标记为 stable

### Gemini

Gemini 是白天临时开发工人。

Gemini 写的代码默认属于临时代码，不能直接进入主线。必须晚上交给 Codex 审查。
Gemini 交接必须按 `tasks/GEMINI_DAILY_PACKAGE.md` 打包，不接受只有编译产物没有源码的提交。

## 5. MCP 规则

v0.1 采用本地 HTTP：

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

禁止：

1. 不允许每个工具新增 endpoint。
2. 不允许私自修改请求格式。
3. 不允许私自修改返回格式。
4. 不允许正式功能依赖 `eval_python`。
5. 不允许绕过 ToolRouter。

## 6. Revit API 主线程边界

HTTP Server / MCP 监听线程不能直接调用 Revit API。

唯一允许的执行方向：

```text
HTTP /mcp/ 请求
-> ToolRouter
-> 如果需要 Revit API
-> 投递到 ExternalEvent / Revit 主线程
-> 执行 Tool
-> 返回 McpResponse
```

不要让后台 HTTP 线程直接读写 Revit `Document`。

## 7. Tool 命名规则

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
revit.text.check_chinese
legacy.gemini.chinese_check
dev.eval_python
```

## 8. 写操作规则

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

## 9. 日志规则

每次代码修改必须更新：

- `docs/WORKLOG.md`
- `tasks/HANDOFF_TO_CODEX.md`
- `docs/TOOL_INDEX.md`，如果新增/修改 Tool
- `docs/CURRENT_STATE.md`，如果当前状态变化

每次 MCP 操作必须写：

- `logs/operations/YYYY-MM-DD.jsonl`

但 `logs/operations/*.jsonl` 不默认进入 Git 主线。Git 主线只保留：

- `logs/operations/README.md`
- `logs/operations/.gitkeep`

重要操作摘要写入 `WORKLOG` / `HANDOFF_TO_CODEX`。

每次重要 AI 对话必须写：

- `logs/conversations/YYYY-MM-DD.md`

## 10. 新开对话必读顺序

任何新 AI / 新对话必须先读：

1. `docs/AGENT_RULES.md`
2. `docs/CURRENT_STATE.md`
3. `tasks/CURRENT_TASK.md`
4. `tasks/HANDOFF_TO_CODEX.md`
5. `docs/TOOL_INDEX.md`

`WORKLOG.md` 只读最近 1-3 条，不要求全读。

## 11. 日志压缩规则

- 每完成 5 个任务，Codex 必须更新一次 `CURRENT_STATE.md`。
- 每完成 10 个任务，Codex 必须生成一次 `docs/summaries/阶段总结.md`。
- `WORKLOG.md` 保留完整历史，但 `CURRENT_STATE.md` 才是 AI 每次必读摘要。

## 12. 版本规则

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

## 13. 失败回退规则

DeepSeek / Gemini 写坏时，不允许无限修坏代码。Codex 可以直接：

1. 回退到上一个 BASE 包。
2. 废弃本次修改。
3. 只保留需求说明。
4. 重新生成任务卡。

## 14. v0.1 验收顺序

推荐验收顺序固定为：

1. Revit 2024 能加载 YangTools 插件。
2. Ribbon 或最小按钮能出现。
3. HTTP `/mcp/` 能启动。
4. `revit.status` 能返回。
5. ToolRouter 可用。
6. `revit.selection.read` 可用。
7. `revit.parameter.get` 可用。
8. `revit.parameter.set` dryRun 可用。
9. `revit.parameter.set` 正式写入可 Ctrl+Z 撤销。
