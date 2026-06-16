# CODEX_REVIEW_CHECKLIST

Codex 每次审查 DeepSeek / Gemini 代码时，必须按本清单检查。

## 1. 架构检查

- [ ] 是否修改 MCP 基础协议
- [ ] 是否新增 endpoint
- [ ] 是否绕过 ToolRouter
- [ ] 是否把 eval_python 当正式功能
- [ ] 是否大范围修改无关文件
- [ ] 是否引入不必要依赖

## 2. Revit 写操作检查

- [ ] 写操作是否使用 Transaction / TransactionGroup
- [ ] 批量写操作是否支持 dryRun
- [ ] dryRun=true 是否完全不修改模型
- [ ] dryRun=false 是否可 Ctrl+Z 撤销
- [ ] 失败时是否 RollBack
- [ ] 是否禁止自动保存 / 覆盖保存 / 同步中心模型
- [ ] 是否返回 changedItems / failedItems

## 3. 日志检查

- [ ] 是否更新 WORKLOG
- [ ] 是否更新 HANDOFF_TO_CODEX
- [ ] 是否更新 TOOL_INDEX
- [ ] 是否更新 CURRENT_STATE
- [ ] 是否更新 conversations 日志
- [ ] MCP 操作是否写 operation log

## 4. 代码质量检查

- [ ] 是否能编译
- [ ] 是否有硬编码本机路径
- [ ] 是否吞异常
- [ ] 是否有明显空引用风险
- [ ] 是否符合 Revit 2024 主测目标

## 5. 审查结论

只能选择一个：

```text
允许合并
小修后合并
退回重写
废弃
```

## 6. Codex 输出格式

```text
审查结论：
阻塞问题：
非阻塞建议：
Codex 直接修复内容：
是否合并主线：
下一步任务：
```
