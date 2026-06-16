# TASK-009

## 任务名称

整理旧 Gemini 插件清单并给出迁移建议。

## 执行者

```text
Gemini 或 DeepSeek
```

## 目标

```text
只做 inventory，不做大规模迁移；把旧插件清点成可供 Codex 审查的列表。
```

## 允许修改文件

```text
- src/Legacy/GeminiOldPlugins/*
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
- docs/CURRENT_STATE.md（如状态变化）
```

## 输出字段

1. 插件名称
2. Ribbon 位置
3. 源码入口
4. 功能说明
5. 是否修改模型
6. 是否有 Transaction
7. 是否适合迁移为 MCP Tool
8. 建议：保留 / 包装 / 重写 / 废弃

## 备注

```text
TASK-009 不允许顺手把旧插件直接并入主线。Gemini 产物必须按 tasks/GEMINI_DAILY_PACKAGE.md 打包交接。
```
