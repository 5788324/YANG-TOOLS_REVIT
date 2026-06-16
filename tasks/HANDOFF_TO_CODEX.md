# HANDOFF_TO_CODEX

DeepSeek / Gemini 完成任务后，必须填写本文件，交给 Codex 审查。

## 当前状态

```text
TASK-001 已完成（Hermes 执行），退修单 TASK-001-REVIEW-01 已处理。
审查结论：小修后合并 → 退修已执行，待 Codex 最终确认。
下一任务：TASK-002 / ToolRouter 正式实现 + revit.status / 等待 Codex 下发。
```

---

## TASK-001 交接

### 任务编号 / 名称

```text
TASK-001：统一 McpRequest / McpResponse 结构，并打通 /mcp/ 基础入口。
```

### 执行者

```text
Hermes（DeepSeek 等价代码工人）
```

### 日期

```text
2026-06-16（初始交付） → 2026-06-16（退修 TASK-001-REVIEW-01）
```

### 当前状态

```text
完成（退修已处理）
```

### 审查历程

| 轮次 | 结论 | 问题 |
|------|------|------|
| 初交 | 退回重写 | 代码未提交到 Codex 可视范围 |
| 补交 | 退回重写 | 代码仍在 Hermes 本地，Codex 工作区未更新 |
| 再交 (hermes/task-001 分支) | **小修后合并** ✅ | test/TestHost 应从 sln 移除 |
| 退修 TASK-001-REVIEW-01 | **待最终审查** | test/TestHost 已从 sln 移除 |

### 主要修改

1. **JsonUtils** — 基于 JavaScriptSerializer 实现完整 JSON 工具。`DeserializeRequest()` 支持大小写不敏感解析，`SerializeResponse()` 强制输出小写协议字段名（ok/tool/data/error/warnings）。
2. **McpServer** — 从占位类扩展为完整 HTTP 服务器。`HttpListener` + 后台线程 + `Start()/Stop()/IsRunning` + `Dispatch()` 链路 + 完整错误处理。
3. **标准错误处理** — 空 tool、未知 tool、JSON 格式错误、非 POST 方法、空 body → 全部返回标准 McpResponse JSON。
4. **csproj** — 新增 `System.Web.Extensions` 程序集引用（net48 内置，零 NuGet 下载）。
5. **退修**：test/TestHost 从 YangTools.sln 移除，目录保留 + test/README.md 标记 dev-only。

### 修改文件

```
- src/YangTools.Revit/Utils/JsonUtils.cs           （重写）
- src/YangTools.Revit/Mcp/McpServer.cs              （重写）
- src/YangTools.Revit/YangTools.Revit.csproj         （新增引用）
- YangTools.sln                                      （移除 TestHost）
- test/TestHost/Program.cs                           （新增，不在 sln 中）
- test/TestHost/TestHost.csproj                      （新增，不在 sln 中）
- test/README.md                                     （新增，dev-only 说明）
```

未修改：App.cs、McpRequest.cs、McpResponse.cs、ToolRouter.cs、IRevitTool.cs、ExternalEventPipeline.cs。

### 测试结果

```
- 编译：dotnet build YangTools.sln → 0 warning, 0 error ✓
- dotnet sln list → 仅 src/YangTools.Revit/YangTools.Revit.csproj ✓
- Test 1-7：7 项 curl 测试全部通过（初交时已验证，退修未触及主体逻辑）
- Revit 2024：未测
- Ctrl+Z 撤销：不适用
```

### 没测试的内容

```
- Revit 2024 实机加载。
- System.Web.Extensions 在 Revit 加载时的兼容性。
```

### 风险

```
1. HttpListener 只注册 /mcp/ 前缀，非 /mcp/ 路径返回系统 HTML 404。
2. System.Web.Extensions 未在 Revit 环境实证。
```

### 需要 Codex 重点审查的问题

```
无新增问题（退修仅移除 TestHost sln 引用，未动主体代码）。
```

### 是否建议合并

```text
允许合并。
```

### 备注

```text
退修选择方案 A：test/TestHost 目录保留但不在 sln 中。
Codex 合并后可直接在 Codex 工作区做最终验证。
```
