# DECISIONS

记录关键项目决策。如需修改，必须新增记录，不覆盖旧结论。

---

## D-001：项目名统一为 YangTools

结论：

```text
项目名、程序集、插件显示名、文档称呼统一为 YangTools / YangTools.Revit。
```

---

## D-002：Codex 是唯一管理者

结论：

```text
Codex 负责规则、任务拆分、审查、合并与主线决策。
DeepSeek / Hermes / Gemini 只负责按边界交付代码和文档。
```

---

## D-003：主测版本固定为 Revit 2024

结论：

```text
v0.1 只验收 Revit 2024，不做 2021-2027 全版本适配。
```

---

## D-004：正式主线语言为 C#

结论：

```text
C# 是正式主线语言。
Python 只作为临时脚本、旧插件迁移参考、dev 调试。
```

---

## D-005：MCP v0 采用本地 HTTP

结论：

```text
POST http://127.0.0.1:8081/mcp/
```

---

## D-006：MCP 只有一个入口

结论：

```text
只保留 /mcp/ 统一入口。
新增能力只能新增 Tool，不新增独立 endpoint。
```

---

## D-007：GitHub 仓库是唯一主线

结论：

```text
5788324/YANG-TOOLS_REVIT 是唯一主线。
任何本地目录都只是工作区，不是最终真相源。
```

---

## D-008：Revit API 只能走合法主线程上下文

结论：

```text
所有 Revit API 操作必须通过 ExternalEvent / 主线程调度执行。
HTTP 线程不能直接访问 Revit Document。
```

---

## D-009：logs/operations 默认不进 Git 主线

结论：

```text
Git 主线只保留 logs/operations/README.md 和 .gitkeep。
运行产生的 jsonl 随源码包交接，重要摘要写入 WORKLOG / HANDOFF。
```

---

## D-010：TASK-000 先于 TASK-001

结论：

```text
在 MCP 开始前，必须先固定 GitHub 主线、可编译骨架、.addin、引用策略和线程边界。
```

---

## D-011：v0.1 验收顺序先插件加载，再 MCP

结论：

```text
插件加载 -> Ribbon/按钮 -> HTTP /mcp/ -> revit.status -> ToolRouter -> selection.read -> parameter.get -> parameter.set dryRun -> parameter.set apply + Ctrl+Z
```

---

## D-012：日志与交接是强制验收项

结论：

```text
任何任务完成后，必须实际更新 docs/WORKLOG.md、tasks/HANDOFF_TO_CODEX.md、logs/conversations/YYYY-MM-DD.md。
如 Tool 状态变化，必须更新 docs/TOOL_INDEX.md。
如当前状态变化，必须更新 docs/CURRENT_STATE.md。
没有这些记录，任务一律视为未完成。
Codex 审查必须先卡日志，缺项则退回补日志，不允许合并。
```
