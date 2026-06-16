# HANDOFF_TO_CODEX

DeepSeek / Gemini 完成任务后，必须填写本文件，交给 Codex 审查。

## 当前状态

```text
TASK-001 已完成（Hermes 执行）。
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
2026-06-16
```

### 当前状态

```text
完成
```

### 主要修改

1. **JsonUtils** — 基于 JavaScriptSerializer 实现完整 JSON 工具。`DeserializeRequest()` 支持大小写不敏感解析，`SerializeResponse()` 强制输出小写协议字段名（ok/tool/data/error/warnings）。
2. **McpServer** — 从占位类扩展为完整 HTTP 服务器。`HttpListener` + 后台线程 + `Start()/Stop()/IsRunning` + `Dispatch()` 链路 + 完整错误处理。
3. **标准错误处理** — 空 tool、未知 tool、JSON 格式错误、非 POST 方法、空 body → 全部返回标准 McpResponse JSON。
4. **csproj** — 新增 `System.Web.Extensions` 程序集引用（net48 内置，零 NuGet 下载）。
5. **test/TestHost** — 最小测试项目，注册 `test.ping` Tool，用于验证非 Revit 环境下的 HTTP /mcp/ 链路。

### 修改文件

```
- src/YangTools.Revit/Utils/JsonUtils.cs           （重写）
- src/YangTools.Revit/Mcp/McpServer.cs              （重写）
- src/YangTools.Revit/YangTools.Revit.csproj         （新增引用）
- YangTools.sln                                      （新增 TestHost）
- test/TestHost/Program.cs                           （新增）
- test/TestHost/TestHost.csproj                      （新增）
```

未修改：App.cs、McpRequest.cs、McpResponse.cs、ToolRouter.cs、IRevitTool.cs、ExternalEventPipeline.cs。

### 测试结果

```
- 编译：dotnet build YangTools.sln → 0 warning, 0 error ✓
- Test 1: POST test.ping → 200 OK, JSON 小写字段 ✓
- Test 2: POST unknown tool → 400, "未注册的 tool" ✓
- Test 3: POST empty tool → 400, "tool 不能为空" ✓
- Test 4: POST invalid JSON → 400, "无法解析请求 JSON" ✓
- Test 5: GET /mcp/ → 405, "不支持的 HTTP 方法" ✓
- Test 6: PascalCase keys → 200 OK, 大小写不敏感解析 ✓
- Test 7: Empty body → 400, JSON 解析错误 ✓
- Revit 2024：未测
- Ctrl+Z 撤销：不适用
```

### 没测试的内容

```
- Revit 2024 实机加载（TASK-001 不涉及 Revit API）。
- System.Web.Extensions 在 Revit 加载时的兼容性（通常 Revit 自带，但未实证）。
- 并发请求压力测试（HttpListener 使用 ThreadPool，理论安全，未实测）。
```

### 风险

```
1. HttpListener 只注册 /mcp/ 前缀，非 /mcp/ 路径返回系统 HTML 404 而非 JSON 404。
   不影响 /mcp/ 正常工作，但不符合"统一 JSON 返回"的审美。
2. test/TestHost 是临时测试工具，不应作为主线发布的组成部分。
3. System.Web.Extensions 是 .NET Framework 3.5+ 内置程序集，理论上 Revit 2024 一定自带，
   但未在 Revit 加载环境中验证。
```

### 需要 Codex 重点审查的问题

1. JsonUtils JavaScriptSerializer 方案是否满足长期需求（vs 未来迁移 Newtonsoft.Json / System.Text.Json）。
2. McpServer Start/Stop 线程模型是否与后续 ExternalEvent 集成兼容。
3. test/TestHost 应保留、移除还是移到单独测试仓库。
4. System.Web.Extensions 引用是否有 Revit 兼容性顾虑。
5. 代码风格（注释/命名/异常处理）是否符合项目标准。

### 是否建议合并

```text
小修后合并。
test/TestHost 建议从主线移除（或移到独立测试方案），其余源码可直接合并。
```

### 备注

```text
- 严格遵守 TASK-001 边界：不访问 Revit Document、不调用 Revit API、不修改 MCP 协议。
- 所有 JSON 输出强制小写字段名，协议一致性已验证。
- McpServer 代码中已预留 ExternalEvent 投递入口的注释和架构说明。
```
