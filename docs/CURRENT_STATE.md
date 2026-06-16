# CURRENT_STATE

> 新 AI / 新对话最重要的项目状态摘要。保持短、真实、可接手。

## 当前版本

```text
YangTools v0.1-dev
```

## 主测环境

```text
Revit 2024
主线语言：C#
Python：临时脚本 / 旧插件迁移参考 / dev 调试
MCP v0：POST http://127.0.0.1:8081/mcp/
GitHub 主线：5788324/YANG-TOOLS_REVIT
本地分支：main
```

## 当前任务

```text
TASK-001 已合入主线：统一 McpRequest / McpResponse + /mcp/ 基础入口。
TASK-002 已合入主线：ToolRouter 正式化 + revit.status 接入。
下一任务待 Codex 下发：TASK-003（OperationLogger）。
```

## 已完成

- TASK-000 已完成并已推送到 GitHub 主线。
- TASK-001 已完成并已合入主线。
- TASK-002 已完成并已合入主线。
- 主线规则已固定：GitHub 唯一主线、Revit 2024、C# 主线、HTTP `/mcp/`、ExternalEvent 主线程边界。
- 日志门槛已固定：缺 WORKLOG / HANDOFF / conversations 记录时，不允许合并。

## 正在做

```text
TASK-001 / TASK-002 已收口并已推送到 GitHub 主线，准备正式下发 TASK-003。
```

## 未完成

- Revit 2024 插件实际加载验证
- Ribbon / 最小按钮验证
- OperationLogger
- TransactionRunner
- revit.selection.read
- revit.parameter.get
- revit.parameter.set
- 旧 Gemini 插件清单
- v0.1-test / v0.1-stable 发布

## 主要风险

1. 还没完成 Revit 2024 实机加载验证。
2. `System.Web.Extensions` 在 Revit 加载环境中的兼容性仍未实证。
3. `mcpServerRunning` 当前为“部分实装”：TestHost 已接入真实状态，主线 Revit 组合根后续仍需补 `SetServer()`。
4. 任何 Revit API 调用都不能从 HTTP 后台线程直接执行。

## 最近一次 Codex 审查结论

```text
TASK-001：允许合并。
TASK-002：允许合并。
当前已进入主线收口阶段。
```

## 下一步

1. 正式下发 TASK-003（OperationLogger）。
2. DeepSeek 完成后按日志门槛回交。
