# YangTools

YangTools 是个人使用的 Revit AI 工具箱 / MCP 控制插件。

## 项目定位

- 主测版本：Revit 2024
- 正式主线语言：C#
- Python 定位：临时脚本、旧插件迁移参考、dev 调试
- 通信方案：本地 HTTP `POST http://127.0.0.1:8081/mcp/`
- 项目目标：AI 可通过 Tool 调用 Revit，读取、分析、修改模型；写操作必须可撤销、有日志、可交接。

## 角色分工

- Codex：唯一管理者，负责规则、架构、审查、合并、发布。
- DeepSeek：正式代码工人，按 Codex 任务卡写代码。
- Gemini：白天临时开发工人，写临时代码，晚上交给 Codex 审查。
- Yang：提需求、实机测试、截图报错、打包源码。

## v0.1 范围

v0.1 只做最小闭环：

1. Revit 2024 插件能加载。
2. HTTP `/mcp/` 能启动。
3. MCP 请求/返回格式固定。
4. ToolRouter 可用。
5. OperationLogger 可用。
6. TransactionRunner 可用。
7. `revit.status` 可用。
8. `revit.selection.read` 可用。
9. `revit.parameter.get` 可用。
10. `revit.parameter.set` 可用，并支持 dryRun、Transaction、Ctrl+Z 撤销。
11. 每次 MCP 操作写 operation log。
12. 每次代码修改写 WORKLOG。
13. 每次交接写 HANDOFF_TO_CODEX。
14. 每次新对话可通过 CURRENT_STATE 接手。

## 启动方式

Codex 首次进入项目时，先读：

1. `docs/AGENT_RULES.md`
2. `docs/CURRENT_STATE.md`
3. `tasks/CURRENT_TASK.md`
4. `tasks/HANDOFF_TO_CODEX.md`
5. `docs/TOOL_INDEX.md`

不要直接读取全部历史日志，除非需要追溯问题。
