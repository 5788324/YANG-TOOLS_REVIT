# test/ — Dev-Only 测试资产

此目录下的代码 **不属于 YangTools 主线 solution**。

用途：Hermes / DeepSeek 开发期间的临时测试主机，用于在不启动 Revit 的情况下验证 HTTP /mcp/ 回环。

测试方式：

```bash
export RevitApiDir="C:/Program Files/Autodesk/Revit 2024/"
dotnet build TestHost.csproj
./bin/Debug/net48/YangTools.Revit.TestHost.exe &

# 在其他终端:
curl -X POST http://127.0.0.1:8081/mcp/ \
  -H "Content-Type: application/json" \
  -d '{"tool":"test.ping","args":{}}'
```

**注意**：此目录不会出现在 `dotnet build YangTools.sln` 的编译范围中。需要使用 `dotnet build TestHost.csproj` 单独编译。
