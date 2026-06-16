# Gemini 临时开发任务卡模板

## 任务编号

```text
GEMINI-TEMP-XXX
```

## 任务名称

```text
白天临时功能：____
```

## 任务目标

```text
解决当天工作中的具体问题。代码属于临时代码，不允许直接进入主线。
```

## 允许行为

1. 可以写临时插件。
2. 可以写临时 MCP Tool。
3. 可以写 Python 验证脚本。
4. 可以修当天需要的小 Bug。

## 禁止行为

1. 不准私自修改 MCP 协议。
2. 不准新增独立 endpoint。
3. 不准把临时代码标记为 stable。
4. 不准自动保存、覆盖保存、同步中心模型。
5. 不准吞异常。
6. 不准大范围重构。

## 完成后必须输出

1. 修改文件清单
2. 功能说明
3. 测试结果
4. 风险说明
5. `docs/WORKLOG.md`
6. `tasks/HANDOFF_TO_CODEX.md`
7. `logs/conversations/YYYY-MM-DD.md`
8. 如状态变化，更新 `docs/CURRENT_STATE.md`
9. 如 Tool 状态变化，更新 `docs/TOOL_INDEX.md`
10. 按 `tasks/GEMINI_DAILY_PACKAGE.md` 输出源码打包
