# HANDOFF_TO_CODEX

DeepSeek / Gemini 完成任务后，必须填写本文件，交给 Codex 审查。

## 当前状态

```text
TASK-000 已本地完成并通过编译。
当前正等待 merge commit push 完成。
下一任务待下发：TASK-001 / 统一 McpRequest / McpResponse 结构，并打通 /mcp/ 基础入口 / 执行者：DeepSeek
```

## TASK-000 完成摘要

```text
1. GitHub 主线唯一性已固定为 5788324/YANG-TOOLS_REVIT。
2. 本地 Git 工作树已初始化，origin 已指向 GitHub 仓库，分支已统一为 main。
3. .gitignore、.addin 模板、Revit 2024 API 引用路径策略、ExternalEvent 主线程边界已落地。
4. dotnet build YangTools.sln 已通过。
5. 已按用户要求合并远端初始提交，README 以当前 YangTools 版本为主。
6. TASK-000 完成后，才允许放行 TASK-001。
```

## DeepSeek / Hermes 后续交接时必须包含

```text
1. 任务编号 / 任务名称 / 执行者 / 日期
2. 当前状态：完成 / 部分完成 / 失败
3. 主要修改
4. 修改文件清单
5. 测试结果
6. 没测试的内容
7. 风险
8. 需要 Codex 重点审查的问题
9. 是否建议合并
10. 备注
```

## 提交模板

```text
任务编号：
任务名称：
执行者：
日期：

当前状态：

主要修改：
1.
2.
3.

修改文件：
- 

测试结果：
- 编译：
- Revit 2024：
- MCP 调用：
- Ctrl+Z 撤销：

没测试的内容：
- 

风险：
- 

需要 Codex 重点审查的问题：
1.
2.

是否建议合并：

备注：
```
