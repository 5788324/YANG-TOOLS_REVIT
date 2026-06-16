# DECISIONS

记录关键项目决策。不要在新对话中反复推翻已确认决策；如需修改，必须新增记录。

---

## D-001：项目名为 YangTools

原因：

```text
用户名为 Yang，项目是个人 Revit 工具箱。
```

结论：

```text
项目名和工具箱名统一为 YangTools。
```

---

## D-002：Codex 是唯一管理者

原因：

```text
Codex 额度有限，但适合架构、审查、合并、任务拆分。
DeepSeek / Gemini 负责写代码更省 Codex 额度。
```

结论：

```text
Codex 负责主线；DeepSeek / Gemini 只负责按任务卡干活。
```

---

## D-003：主测 Revit 2024

原因：

```text
用户当前主要工作目标为 Revit 2024，v0.1 需要先落地，不做完整多版本适配。
```

结论：

```text
v0.1 只验收 Revit 2024。
```

---

## D-004：C# 为正式主线，Python 为临时/迁移/调试

原因：

```text
C# 更适合长期 Revit 插件维护、Transaction、UI 和 Codex 审查。
Python 适合快速验证和迁移旧逻辑。
```

结论：

```text
正式 Tool 尽量用 C#。
Python 只能作为临时脚本、旧插件迁移参考和 dev 调试能力。
```

---

## D-005：MCP v0 采用本地 HTTP

原因：

```text
现有 MCP 雏形使用 HTTP 8081。
HTTP 便于调试，适合个人工具箱。
```

结论：

```text
v0.1 使用 POST http://127.0.0.1:8081/mcp/
```

---

## D-006：MCP 只有一个入口，所有功能 Tool 化

原因：

```text
避免每个插件一个 endpoint 导致协议失控。
```

结论：

```text
只保留 /mcp/ 统一入口。
新功能只能新增 Tool。
```

---

## D-007：日志长期保留，但 AI 每次读 CURRENT_STATE

原因：

```text
WORKLOG 越长越有价值，但 AI 不会每次读全量历史。
```

结论：

```text
CURRENT_STATE 是每次新对话必读摘要。
WORKLOG / operations / conversations 保留完整历史。
定期生成 summaries。
```

---

## D-008：eval_python 只能作为 dev 工具

原因：

```text
任意执行 Python 灵活但风险高，不能作为正式功能。
```

结论：

```text
eval_python 降级为 dev.eval_python，默认不进入正式工作流。
```

---

## D-009：GitHub 仓库是唯一主线

原因：

```text
Codex、Hermes / DeepSeek、Gemini 都可能在不同本地工作区作业，必须有唯一真相源。
```

结论：

```text
唯一主线固定为 5788324/YANG-TOOLS_REVIT。
任何本地目录都只是工作区，不是最终真相源。
```

---

## D-010：Revit API 只能走主线程合法上下文

原因：

```text
HTTP / MCP 监听线程不是合法 Revit API 上下文，直接访问 Document 风险高且不稳定。
```

结论：

```text
所有 Revit API 操作必须通过 ExternalEvent / 主线程调度执行。
后台 HTTP 线程不能直接读写 Revit Document。
```

---

## D-011：logs/operations 默认不进 Git 主线

原因：

```text
operation logs 可能很大，也可能包含模型路径，不适合默认全部提交到 GitHub 主线。
```

结论：

```text
Git 主线只保留 logs/operations/README.md 和 .gitkeep。
运行期 jsonl 交接时随源码包提供，重要摘要写入 WORKLOG / HANDOFF。
```

---

## D-012：TASK-000 先于 TASK-001

原因：

```text
在 MCP 实现前，必须先固定 GitHub 主线、可编译 Revit 插件骨架、.addin、引用策略和日志规则。
```

结论：

```text
TASK-000 是正式开发前置任务。
TASK-000 完成前，不允许 DeepSeek 开始 TASK-001。
```

---

## D-013：v0.1 验收顺序先插件加载，再 MCP

原因：

```text
YangTools 首先是 Revit 插件；如果插件不能加载，后续 MCP Tool 全部没有落点。
```

结论：

```text
v0.1 验收顺序固定为：
插件加载 -> Ribbon/按钮 -> HTTP /mcp/ -> revit.status -> ToolRouter -> selection.read -> parameter.get -> parameter.set dryRun -> parameter.set apply + Ctrl+Z
```
