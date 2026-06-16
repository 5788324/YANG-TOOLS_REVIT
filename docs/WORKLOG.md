# WORKLOG

记录每次代码修改。不要删除历史记录。新 AI 默认只读最近 1-3 条，完整历史用于追溯。

---

## 模板

### YYYY-MM-DD / TASK-XXX / 执行者

#### 本次目标

```text
写清楚本次任务目标。
```

#### 修改文件

```text
- path/to/file1.cs
- path/to/file2.md
```

#### 做了什么

```text
1. ...
2. ...
3. ...
```

#### 没做什么

```text
1. ...
2. ...
```

#### 测试结果

```text
- Revit 2024：未测 / 通过 / 失败
- 编译：未测 / 通过 / 失败
```

#### 已知问题

```text
- ...
```

#### 需要 Codex 审查

```text
1. ...
2. ...
```

#### 下一步

```text
- ...
```

---

## 2026-06-15 / INIT / Assistant

#### 本次目标

创建 YangTools 项目基础文档包。

#### 修改文件

```text
- README.md
- docs/AGENT_RULES.md
- docs/CURRENT_STATE.md
- docs/TOOL_INDEX.md
- docs/WORKLOG.md
- docs/DECISIONS.md
- tasks/CURRENT_TASK.md
- tasks/HANDOFF_TO_CODEX.md
- tasks/TASK_TEMPLATE_DEEPSEEK.md
- tasks/TASK_TEMPLATE_GEMINI.md
- tasks/CODEX_REVIEW_CHECKLIST.md
- logs/conversations/README.md
- logs/operations/README.md
```

#### 做了什么

1. 固定项目名为 YangTools。
2. 固定主测版本为 Revit 2024。
3. 固定 C# 为正式主线，Python 为临时/迁移/调试。
4. 固定 MCP v0 为 HTTP `127.0.0.1:8081/mcp/`。
5. 建立工作日志、交接文档、操作日志和对话日志规则。
6. 建立 Codex/DeepSeek/Gemini 分工规则。

#### 没做什么

1. 没有写业务代码。
2. 没有创建真实 Visual Studio 工程。
3. 没有审查现有 Gemini 插件源码。

#### 测试结果

```text
文档包创建完成。
Revit 未测试。
编译未测试。
```

#### 已知问题

```text
需要 Codex 根据真实源码路径微调。
```

#### 需要 Codex 审查

```text
1. 文档是否需要压缩。
2. 目录是否适合当前仓库。
3. 第一批任务卡是否需要拆得更细。
```

#### 下一步

```text
Codex 落地文档到仓库，并创建第一批开发任务。
```

---

## 2026-06-16 / INIT-BOOTSTRAP / Codex

#### 本次目标

正式启动 YangTools 项目，创建最小 C# 主线骨架、第一批任务卡、状态日志和 BASE 基线。

#### 修改文件

```text
- YangTools.sln
- src/YangTools.Revit/**
- src/Legacy/GeminiOldPlugins/README.md
- base/YangTools_BASE_v0.1-dev/**
- tasks/GEMINI_DAILY_PACKAGE.md
- tasks/TASK-001.md
- tasks/TASK-002.md
- tasks/TASK-003.md
- tasks/TASK-004.md
- tasks/TASK-005.md
- tasks/TASK-006.md
- tasks/TASK-007.md
- tasks/TASK-008.md
- tasks/TASK-009.md
- tasks/CURRENT_TASK.md
- tasks/HANDOFF_TO_CODEX.md
- tasks/TASK_TEMPLATE_GEMINI.md
- docs/AGENT_RULES.md
- docs/CURRENT_STATE.md
- docs/DECISIONS.md
- logs/conversations/2026-06-16.md
```

#### 做了什么

1. 创建 `YangTools.sln` 和 `src/YangTools.Revit/` 最小 C# 主线骨架。
2. 固定第一版 `YangTools_BASE v0.1-dev` 基线目录和 manifest。
3. 生成 TASK-001 到 TASK-009 正式任务卡，并明确 TASK-001 到 TASK-008 由 DeepSeek 执行，TASK-009 可由 Gemini 或 DeepSeek 做 inventory。
4. 固定 Gemini 白天临时代码的夜间打包交接格式。
5. 把 `CURRENT_STATE`、`CURRENT_TASK`、`HANDOFF_TO_CODEX`、conversation log 改成可直接接手的真实状态。

#### 没做什么

1. 没有实现真实 Revit API 调用。
2. 没有实现真实 HTTP listener。
3. 没有把任何临时代码并入主线。

#### 测试结果

```text
- Revit 2024：未测
- 编译：失败（当前环境写入 obj 临时目录被拒绝，属于环境限制，非已确认代码错误）
```

#### 已知问题

```text
- 当前目录不是 Git 工作树。
- 还没有配置 Revit API 引用，因此当前只是主线骨架，不是可加载插件。
```

#### 需要 Codex 审查

```text
1. 后续是否把 Git 初始化也纳入启动步骤。
2. 何时补入 Revit 2024 API 引用与 addin 清单。
```

#### 下一步

```text
- DeepSeek 从 TASK-001 开始实现基础 MCP 主线。
- Gemini 如需白天写临时代码，必须按 GEMINI_DAILY_PACKAGE 规则打包。
```

---

## 2026-06-16 / TASK-000 / Codex

#### 本次目标

把启动轮修正为 TASK-000 优先，补入 GitHub 主线唯一性、可编译 Revit 插件骨架前置条件、.addin、引用策略和主线程边界。

#### 修改文件

```text
- .gitignore
- Directory.Build.props
- src/YangTools.Revit/Addin/YangTools.addin.template
- src/YangTools.Revit/Revit/ExternalEventPipeline.cs
- src/YangTools.Revit/Revit/RevitApiReferenceStrategy.md
- docs/THREADING_RULES.md
- docs/AGENT_RULES.md
- docs/CURRENT_STATE.md
- docs/WORKLOG.md
- docs/DECISIONS.md
- docs/PROJECT_START_PLAN.md
- tasks/CURRENT_TASK.md
- tasks/HANDOFF_TO_CODEX.md
- tasks/TASK-000.md
- tasks/TASK-001.md
- logs/conversations/2026-06-16.md
```

#### 做了什么

1. 新增 TASK-000，并把它提升为当前唯一任务。
2. 把 GitHub 仓库 `5788324/YANG-TOOLS_REVIT` 固定为唯一主线。
3. 新增 `.gitignore`，明确 `logs/operations/*.jsonl` 不默认进 Git。
4. 准备 `.addin` 模板、Revit 2024 API 引用路径策略和 Revit API 主线程边界说明。
5. 把 v0.1 验收顺序改成“先插件能加载，再 MCP”。

#### 没做什么

1. 没有开始 TASK-001 的正式实现。
2. 没有验证 Revit 2024 实机加载。
3. 没有完成 GitHub 主线实际推送。

#### 测试结果

```text
- Git 初始化：失败（当前环境未成功建立正常 Git 工作树）
- Revit 2024：未测
- 编译：未完成验证
```

#### 已知问题

```text
- 当前目录里 `git init` 失败，说明 TASK-000 的 Git 主线接管还未实证完成。
- 当前骨架仍未接入真实 RevitAPI / RevitAPIUI 引用。
```

#### 需要 Codex 审查

```text
1. 后续是在当前目录继续修 Git，还是直接切到目标 Git 工作树处理。
2. Revit 2024 API DLL 的本机实际安装路径是否需要再做一次实机探测。
```

#### 下一步

```text
- 完成 TASK-000 的剩余 Git / 引用 / 可编译验证。
- TASK-000 完成后，再下发 DeepSeek 的 TASK-001 开工指令。
```

---

## 2026-06-16 / TASK-000-CLOSEOUT / Codex

#### 本次目标

按用户确认方案合并远端初始提交，保持当前 YangTools README 为主，并完成 TASK-000 收口前验证。

#### 修改文件

```text
- README.md
- docs/CURRENT_STATE.md
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
- logs/conversations/2026-06-16.md
```

#### 做了什么

1. 执行 `git pull origin main --allow-unrelated-histories`。
2. 解决 `README.md` add/add 冲突，保留当前 YangTools 版本。
3. 再次执行 `dotnet build YangTools.sln`，确认编译通过。
4. 更新 TASK-000 收口状态文档。

#### 没做什么

1. 还没有验证 Revit 2024 内实际加载插件。
2. 还没有正式下发 DeepSeek 的 TASK-001 指令。

#### 测试结果

```text
- git pull：完成，只有 README.md 冲突，已解决
- 编译：通过（0 warning, 0 error）
- Revit 2024：未测
```

#### 已知问题

```text
- GitHub push 结果尚未确认。
- Revit 2024 插件加载与 Ribbon 显示仍待实机验证。
```

#### 需要 Codex 审查

```text
1. push 成功后，TASK-000 即可视为完全收口。
2. TASK-001 下发前无需再补规则层改动。
```

#### 下一步

```text
- 完成 merge commit / push。
- push 成功后，正式下发 DeepSeek 的 TASK-001 开工指令。
```

#### 结果补充

```text
- merge commit 已完成：e854def
- git push 已完成：origin/main 已同步
```

---

## 2026-06-16 / TASK-001 / Hermes

#### 本次目标

统一 McpRequest / McpResponse 结构，实现 JSON 序列化/反序列化，并打通 HTTP POST /mcp/ 基础入口。

#### 修改文件

```text
- src/YangTools.Revit/Utils/JsonUtils.cs          （从占位重写为完整 JSON 工具）
- src/YangTools.Revit/Mcp/McpServer.cs             （从占位重写为完整 HTTP MCP 服务器）
- src/YangTools.Revit/YangTools.Revit.csproj        （添加 System.Web.Extensions 引用）
- YangTools.sln                                     （添加 TestHost 测试项目）
- test/TestHost/Program.cs                          （新增：MCP 测试主机）
- test/TestHost/TestHost.csproj                     （新增：测试项目文件）
```

#### 做了什么

1. JsonUtils 完整实现：基于 JavaScriptSerializer（零 NuGet 依赖），DeserializeRequest() 支持大小写不敏感 JSON 解析，SerializeResponse() 强制输出小写字段名 ok/tool/data/error/warnings。
2. McpServer 完整实现：HttpListener + 后台线程 + Start/Stop/IsRunning + Dispatch() + 完整错误处理。
3. 标准错误覆盖：空 tool、未知 tool、JSON 格式错误、非 POST 方法、空 body——全部返回标准 McpResponse JSON。
4. 添加 test/TestHost 项目，注册一个 test.ping Tool，用于非 Revit 环境测试验证。
5. csproj 新增 System.Web.Extensions 程序集引用（net48 内置，零 NuGet 下载）。

#### 没做什么

1. 没有实现真实 Revit Tool（revit.status/selection.read 等）。
2. 没有访问 Revit Document。
3. 没有调用 Revit API。
4. 没有实现 ExternalEvent 投递机制。
5. 没有修改 MCP 协议字段名。
6. 没有引入 Newtonsoft.Json 或任何 NuGet 依赖。

#### 测试结果

```text
- 编译：dotnet build YangTools.sln → 0 warning, 0 error ✓
- Test 1: POST test.ping → 200 OK, JSON 小写字段 ✓
- Test 2: POST unknown tool → 400, "未注册的 tool" ✓
- Test 3: POST empty tool → 400, "tool 不能为空" ✓
- Test 4: POST invalid JSON → 400, "无法解析请求 JSON" ✓
- Test 5: GET /mcp/ → 405, "不支持的 HTTP 方法" ✓
- Test 6: PascalCase keys → 200 OK, 大小写不敏感解析 ✓
- Test 7: Empty body → 400, JSON 解析错误 ✓
- Revit 2024：未测（TASK-001 不涉及 Revit API）
```

#### 已知问题

```text
- HttpListener 只注册了 /mcp/ 前缀，POST /other 路径由系统返回 HTML 404 而非 JSON 404。
  这是 HttpListener 前缀机制的限制，不影响 /mcp/ 正常工作。如需覆盖其他路径，
  需要额外前缀或通配注册（管理员权限），留待后续决定。
- test/TestHost 是纯测试工具，不应进入主线源码。Codex 审查后决定保留位置或移除。
```

#### 需要 Codex 审查

```text
1. JsonUtils 使用 JavaScriptSerializer → 确认零 NuGet 策略满足 v0.1 需求。
2. McpServer HTTP listener 线程模型 → 确认 Start/Stop 设计适合后续 ExternalEvent 集成。
3. test/TestHost 是否应保留在仓库中，或移到单独测试方案。
4. System.Web.Extensions 引用是否与 Revit 加载环境兼容（通常 Revit 自带此程序集）。
```

#### 下一步

```text
- Codex 审查 TASK-001 成果。
- 审查通过后下发 TASK-002（ToolRouter 正式实现 + revit.status）。
```
