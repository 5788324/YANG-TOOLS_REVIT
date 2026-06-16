# WORKLOG

记录每次代码或规则修改。不要删除历史。

---

## 2026-06-15 / INIT / Assistant

#### 本次目标

创建 YangTools 项目基础文档包。

#### 修改文件

```text
- README.md
- docs/AGENT_RULES.md
- docs/CURRENT_STATE.md
- docs/TOOL_INDEX.md
- docs/WORKLOG.md
- docs/DECISIONS.md
- tasks/CURRENT_TASK.md
- tasks/HANDOFF_TO_CODEX.md
- tasks/TASK_TEMPLATE_DEEPSEEK.md
- tasks/TASK_TEMPLATE_GEMINI.md
- tasks/CODEX_REVIEW_CHECKLIST.md
- logs/conversations/README.md
- logs/operations/README.md
```

#### 做了什么
```text
1. 固定项目名为 YangTools。
2. 固定主测版本为 Revit 2024。
3. 固定 C# 为正式主线，Python 为临时脚本/迁移参考/dev 调试。
4. 固定 MCP v0 为 HTTP 127.0.0.1:8081/mcp/。
5. 建立工作日志、交接文档、操作日志和对话日志规则。
```

#### 没做什么
```text
1. 没有写业务代码。
2. 没有创建真实 Visual Studio 项目。
```

#### 测试结果
```text
- 文档包创建完成
- 编译：未测试
- Revit 2024：未测试
```

---

## 2026-06-16 / TASK-000 / Codex

#### 本次目标

正式启动 YangTools 项目，创建最小主线骨架并完成 TASK-000。

#### 修改文件

```text
- YangTools.sln
- src/YangTools.Revit/**
- docs/AGENT_RULES.md
- docs/CURRENT_STATE.md
- docs/DECISIONS.md
- docs/PROJECT_START_PLAN.md
- docs/WORKLOG.md
- tasks/TASK-000.md
- tasks/TASK-001.md
- tasks/TASK-002.md
- tasks/TASK-003.md
- tasks/TASK-004.md
- tasks/TASK-005.md
- tasks/TASK-006.md
- tasks/TASK-007.md
- tasks/TASK-008.md
- tasks/TASK-009.md
- tasks/CURRENT_TASK.md
- tasks/HANDOFF_TO_CODEX.md
- logs/conversations/2026-06-16.md
```

#### 做了什么
```text
1. 创建主线解决方案和最小 C# / Revit 插件骨架。
2. 固定 GitHub 主线、命名规范、线程边界和 .addin / 引用策略。
3. 建立 TASK-000 到 TASK-009。
4. 完成远端初始提交合并、编译验证和 push。
```

#### 没做什么
```text
1. 没有验证 Revit 2024 实机加载。
2. 没有实现正式业务 Tool。
```

#### 测试结果
```text
- dotnet build YangTools.sln：通过（历史结果 0 warning, 0 error）
- Revit 2024：未测试
```

---

## 2026-06-16 / REVIEW-GATE-LOGS / Codex

#### 本次目标

把“日志是强制交付物、缺日志不允许合并”固化到 YangTools 主线规则，并补记本轮 Codex 对 TASK-001 / TASK-002 的审查动作。

#### 修改文件

```text
- docs/AGENT_RULES.md
- docs/DECISIONS.md
- docs/CURRENT_STATE.md
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
- tasks/CODEX_REVIEW_CHECKLIST.md
- tasks/TASK_TEMPLATE_DEEPSEEK.md
- tasks/TASK_TEMPLATE_GEMINI.md
- tasks/TASK-000.md
- tasks/TASK-001.md
- tasks/TASK-002.md
- logs/conversations/2026-06-16.md
```

#### 做了什么
```text
1. 明确日志是交付物，不是可选项。
2. 明确缺 WORKLOG / HANDOFF_TO_CODEX / conversations 记录时，审查结论必须是“退回补日志，不允许合并”。
3. 把日志要求写进 Codex 审查清单和任务模板。
4. 把 TASK-001 / TASK-002 的日志写入要求补进验收标准。
5. 更新 CURRENT_STATE，暂停进入下一任务，先完成日志补记与重新审查。
6. 补记本轮 Codex 审查动作到主线 WORKLOG / HANDOFF / conversation log。
```

#### 没做什么
```text
1. 没有修改任何业务代码。
2. 没有合并 origin/hermes/task-001。
3. 没有下发 TASK-003 或后续任务。
```

#### 测试结果
```text
- 编译：本轮未执行；本次仅修改规则和日志文档
- 代码审查：已确认需先卡日志，再给 TASK-001 / TASK-002 正式结论
- Revit 2024：未测试
```

#### 已知风险
```text
- 远端 TASK-002 仍存在 CURRENT_STATE 文档前后不一致的问题。
- 远端 revit.status 的 mcpServerRunning 目前仍是硬编码 true。
```

#### 是否建议合并
```text
当前不下 TASK-001 / TASK-002 的合并结论，先补齐日志流程后重新审查。
```

#### 重审结果补充
```text
1. 已按新规则先检查远端日志交付，确认 TASK-001 / TASK-002 具备 WORKLOG / HANDOFF / conversations / CURRENT_STATE / TOOL_INDEX。
2. 因此进入代码审查。
3. 当前重审结论：小修后合并。
4. 退修点仅剩两项：远端 CURRENT_STATE 前后不一致；revit.status 的 mcpServerRunning 为硬编码 true，语义需明确。
```
