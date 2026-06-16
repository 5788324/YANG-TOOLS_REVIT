# DeepSeek 任务卡模板

## 任务编号

```text
TASK-XXX
```

## 任务名称

```text
实现 / 修复 / 迁移：____
```

## 任务目标

```text
请按 Codex 要求完成单个明确任务。不要扩展需求，不要大范围重构。
```

## 允许修改文件

```text
- ...
```

## 禁止修改文件

```text
- MCP 基础请求格式
- MCP 基础返回格式
- 与本任务无关的核心文件
```

## 技术规则

1. 不准修改 MCP 协议。
2. 不准新增 endpoint。
3. 不准绕过 ToolRouter。
4. 不准引入新依赖，除非 Codex 明确允许。
5. 写操作必须 Transaction。
6. 批量写操作必须 dryRun。
7. 异常必须返回到 McpResponse.error，不能吞异常。
8. 完成后必须更新 WORKLOG 和 HANDOFF_TO_CODEX。

## Tool 信息

```text
Tool 名称：
风险等级：
是否 dryRun：
是否写模型：
是否需要 Transaction：
```

## 请求示例

```json
{
  "tool": "revit.xxx.xxx",
  "args": {}
}
```

## 返回示例

```json
{
  "ok": true,
  "tool": "revit.xxx.xxx",
  "data": {},
  "error": null,
  "warnings": []
}
```

## 测试要求

1. 编译通过。
2. MCP 调用返回统一格式。
3. 错误输入能返回 error。
4. 写操作能 Ctrl+Z 撤销。
5. 日志写入成功。
