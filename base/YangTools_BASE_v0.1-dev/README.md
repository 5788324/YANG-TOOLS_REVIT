# YangTools_BASE v0.1-dev

这是 YangTools 第一版主线基座，不是稳定发布包。

## 包含内容

1. `YangTools.sln`
2. `src/YangTools.Revit/` 最小 C# 主线骨架
3. `docs/` 启动文档与状态文档
4. `tasks/` 第一批正式任务卡
5. `logs/` 操作日志与对话日志目录

## 当前用途

1. 作为 Codex 管理的正式主线起点。
2. 作为 DeepSeek 实现 TASK-001 到 TASK-008 的基座。
3. 作为 Gemini 白天临时代码的对照基线。

## 当前边界

1. 还没有接入真实 Revit API 引用。
2. 还没有实现 HTTP listener。
3. 还没有实现真实 Transaction / Tool 逻辑。
4. 还不能宣称 `v0.1-test` 或 `v0.1-stable`。
