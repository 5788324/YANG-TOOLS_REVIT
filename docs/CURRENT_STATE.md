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
TASK-001 ✅ 完成（退修已处理，待 Codex 合并）。
TASK-002 ✅ 完成（ToolRouter 正式化 + revit.status 接入）。
下一任务：TASK-003（OperationLogger，等待 Codex 下发）。
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
- [x] **TASK-001：HTTP /mcp/ + JsonUtils + McpServer 完整实现**（Hermes，2026-06-16）
- [x] **TASK-002：ToolRouter 正式化 + revit.status 接入**（Hermes，2026-06-16）

## 正在做

```text
等待 Codex 审查 TASK-001 和 TASK-002 成果。
```

## 未完成

- Revit 2024 插件实际加载验证
- Ribbon / 最小按钮验证
- OperationLogger / TransactionRunner 正式实现
- revit.selection.read / revit.parameter.get / revit.parameter.set 正式实现
- 旧 Gemini 插件清单
- v0.1-test / v0.1-stable 发布

## 最近风险

1. 还没验证 Revit 2024 内实际加载插件。
2. System.Web.Extensions 在 Revit 加载环境中的兼容性未实证。
3. Gemini 白天代码不能直接进主线。
4. DeepSeek / Hermes 不允许改 MCP 协议，不允许直接决定合并。
5. 任何 Revit API 调用都不能从 HTTP 后台线程直接执行，必须经 ExternalEvent / 主线程调度。

## 最近一次 Codex 审查结论

```text
TASK-000：已完全收口，Git、编译、主线同步均已完成。
TASK-001：小修后合并（退修已处理，test/TestHost 已从 sln 移除）。
TASK-002：待审查（ToolRouter 正式化 + revit.status 接入，编译 0w0e，curl 测试通过）。
```

## 下一步

1. Codex 审查 TASK-001 和 TASK-002 成果。
2. 审查通过后下发 TASK-003（OperationLogger）。
