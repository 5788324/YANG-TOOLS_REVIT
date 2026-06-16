# CODEX_REVIEW_CHECKLIST

Codex 每次审查 DeepSeek / Gemini 代码时，必须按本清单检查。

## 1. 先查日志，再看代码

- [ ] `docs/WORKLOG.md` 是否新增本次任务记录
- [ ] `tasks/HANDOFF_TO_CODEX.md` 是否更新为本次交接
- [ ] `logs/conversations/YYYY-MM-DD.md` 是否记录本次 AI 对话
- [ ] 如当前状态变化，`docs/CURRENT_STATE.md` 是否更新
- [ ] 如 Tool 状态变化，`docs/TOOL_INDEX.md` 是否更新

硬门槛：

```text
缺任何一项，审查结论必须是：退回补日志，不允许合并。
```

## 2. 架构检查

- [ ] 是否修改 MCP 基础协议
- [ ] 是否新增 endpoint
- [ ] 是否绕过 ToolRouter
- [ ] 是否把 eval_python 当正式能力
- [ ] 是否大范围修改无关文件
- [ ] 是否引入不必要依赖

## 3. Revit 写操作检查

- [ ] 写操作是否使用 Transaction / TransactionGroup
- [ ] 批量写操作是否支持 dryRun
- [ ] dryRun=true 是否完全不修改模型
- [ ] dryRun=false 是否可 Ctrl+Z 撤销
- [ ] 失败时是否 RollBack
- [ ] 是否禁止自动保存 / 覆盖保存 / 同步中心模型
- [ ] 是否返回 changedItems / failedItems

## 4. 代码质量检查

- [ ] 是否能编译
- [ ] 是否有硬编码本机路径
- [ ] 是否吞异常
- [ ] 是否符合 Revit 2024 主测目标

## 5. 审查结论

只能选择一个：

```text
允许合并
小修后合并
退回重做
废弃
```
