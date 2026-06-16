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
5. 写操作必须走 Transaction。
6. 批量写操作必须支持 dryRun。
7. 异常必须进入 `McpResponse.error`，不能吞异常。

## 验收标准必须包含

1. 已更新 `docs/WORKLOG.md`
2. 已更新 `tasks/HANDOFF_TO_CODEX.md`
3. 已更新 `logs/conversations/YYYY-MM-DD.md`
4. 如状态变化，已更新 `docs/CURRENT_STATE.md`
5. 如 Tool 状态变化，已更新 `docs/TOOL_INDEX.md`

## 测试要求

1. 编译通过。
2. MCP 调用返回统一格式。
3. 错误输入能返回 error。
4. 写操作能 `Ctrl+Z` 撤销。
5. MCP 操作能写 operation log。
