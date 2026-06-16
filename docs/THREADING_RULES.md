# THREADING_RULES

YangTools 的 HTTP Server / MCP 监听线程不能直接调用 Revit API。

## 唯一允许的方向

```text
HTTP /mcp/ 请求
-> ToolRouter
-> 如果需要 Revit API
-> 投递到 ExternalEvent / Revit 主线程
-> 执行 Tool
-> 返回 McpResponse
```

## 禁止

1. 后台 HTTP 线程直接读 `Document`。
2. 后台 HTTP 线程直接写 `Document`。
3. 为了省事把 Revit API 调用塞进普通网络回调线程。

## v0.1 约束

1. `revit.status`
2. `revit.selection.read`
3. `revit.parameter.get`
4. `revit.parameter.set`

以上 Tool 未来只要碰 Revit API，都必须遵守这条边界。
