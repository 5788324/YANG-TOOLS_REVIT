# TOOL_INDEX

记录所有 MCP Tool。新增、修改、废弃 Tool 时必须更新。

| Tool | 状态 | 风险等级 | dryRun | 修改模型 | 实现文件 | 测试版本 | 说明 |
|---|---|---|---|---|---|---|---|
| revit.status | experimental | ReadOnly | 否 | 否 | `src/YangTools.Revit/Tools/RevitStatusTool.cs` | Revit 2024 | 查询 Revit / 当前文档 / MCP 状态。Revit 上下文字段当前为占位。 |
| revit.selection.read | planned | ReadOnly | 否 | 否 | `src/YangTools.Revit/Tools/SelectionReadTool.cs` | Revit 2024 | 读取当前选择元素基础信息 |
| revit.parameter.get | planned | ReadOnly | 否 | 否 | `src/YangTools.Revit/Tools/ParameterGetTool.cs` | Revit 2024 | 读取当前选择元素参数 |
| revit.parameter.set | planned | BatchWrite | 是 | 是 | `src/YangTools.Revit/Tools/ParameterSetTool.cs` | Revit 2024 | 批量修改当前选择元素参数 |
| dev.eval_python | planned | DevOnly | 不适用 | 可能 | 待定 | Revit 2024 | 仅开发调试，不得作为正式功能 |
| legacy.gemini.* | legacy | 待评估 | 待评估 | 待评估 | `src/Legacy/GeminiOldPlugins/` | 未测 | Gemini 旧插件临时归档 |

## 状态说明

```text
planned       计划中
experimental  已实现但未稳定
stable        Codex 审查通过，可正式使用
legacy        旧插件 / 临时代码
deprecated    废弃
```

## 风险等级

```text
ReadOnly      只读
SafeWrite     小范围写入
BatchWrite    批量写入
Destructive   删除、覆盖、清空等高风险
DevOnly       开发调试
```
