# CURRENT_STATE

> 新 AI / 新对话最重要的项目状态摘要。请保持短，不要写成长篇历史。

## 当前版本

```text
YangTools v0.1-dev
```

## 主测环境

```text
Revit：2024
主线语言：C#
Python：临时脚本 / 旧插件迁移参考 / dev 调试
MCP v0：HTTP 127.0.0.1:8081/mcp/
GitHub 主线：5788324/YANG-TOOLS_REVIT
本地 Git 分支：main
```

## 当前项目目标

v0.1 只做最小闭环：

1. Revit 2024 插件能加载。
2. Ribbon 或最小按钮能出现。
3. HTTP `/mcp/` 可用。
4. ToolRouter 可用。
5. OperationLogger 可用。
6. TransactionRunner 可用。
7. `revit.status` 可用。
8. `revit.selection.read` 可用。
9. `revit.parameter.get` 可用。
10. `revit.parameter.set` 可用，并支持 dryRun、Transaction、Ctrl+Z 撤销。
11. 日志与交接体系可用。

## 当前任务

```text
TASK-000 已完成。
下一任务：TASK-001（等待 Codex 正式下发给 DeepSeek）
```

## 已完成

- [x] 项目目录创建
- [x] 基础文档创建
- [x] MCP v0 协议固定
- [x] 第一批任务卡创建
- [x] YangTools_BASE v0.1-dev 创建
- [x] GitHub 主线唯一性规则写入
- [x] 本地 Git 工作树初始化
- [x] origin 已指向 `https://github.com/5788324/YANG-TOOLS_REVIT.git`
- [x] 本地分支统一为 `main`
- [x] 已合并远端 `Initial commit`，README 以当前 YangTools 版本为主
- [x] `logs/operations/*.jsonl` 默认不进 Git 规则写入
- [x] `.gitignore` 已添加
- [x] `.addin` 模板已准备
- [x] Revit 2024 引用路径策略已准备
- [x] 默认 Revit 2024 API DLL 路径存在实证
- [x] Revit API 主线程 / ExternalEvent 边界已写入
- [x] 最小可编译 C# / Revit 插件骨架验证通过

## 正在做

```text
完成合并收口并准备 push 到 GitHub 主线，然后正式下发 TASK-001。
```

## 未完成

- 当前合并结果推送到 GitHub 主线
- Revit 2024 插件实际加载验证
- Ribbon / 最小按钮验证
- HTTP `/mcp/` 正式实现
- ToolRouter / OperationLogger / TransactionRunner 正式实现
- revit.status / revit.selection.read / revit.parameter.get / revit.parameter.set 正式实现
- 旧 Gemini 插件清单
- v0.1-test / v0.1-stable 发布

## 最近风险

1. 本地 Git 和合并收口已完成，但 GitHub 推送是否成功还未验证。
2. 还没验证 Revit 2024 内实际加载插件。
3. Gemini 白天代码不能直接进主线。
4. DeepSeek / Hermes 不允许改 MCP 协议，不允许直接决定合并。
5. 任何 Revit API 调用都不能从 HTTP 后台线程直接执行，必须经 ExternalEvent / 主线程调度。

## 最近一次 Codex 审查结论

```text
TASK-000 已完成本地收口并通过编译。
完成 GitHub push 后，即可正式下发 DeepSeek 的 TASK-001。
```

## 下一步

1. 完成合并提交并 push 到 `5788324/YANG-TOOLS_REVIT`。
2. 成功后正式下发 DeepSeek 的 TASK-001 开工指令。
