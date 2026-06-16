using System;
using System.Threading;
using YangTools.Revit.Mcp;
using YangTools.Revit.Tools;

namespace YangTools.Revit.Test;

public static class TestHost
{
    public static void Main()
    {
        var statusTool = new RevitStatusTool();

        var tools = new IRevitTool[] { statusTool, new PingTool() };
        var router = new ToolRouter(tools);

        // 反向注入：RevitStatusTool 需要 ToolRouter 引用以获取 registeredToolCount
        statusTool.SetRouter(router);

        var server = new McpServer(router);

        // 反向注入：RevitStatusTool 需要 McpServer 引用以获取真实运行状态
        statusTool.SetServer(server);

        Console.WriteLine($"Starting MCP server on {server.Endpoint}");

        try
        {
            server.Start();
            Console.WriteLine("Server started. Press Ctrl+C to stop.");
            Console.WriteLine($"Registered tools: {router.RegisteredToolCount}");
            Console.WriteLine("  - revit.status");
            Console.WriteLine("  - test.ping");
            Console.WriteLine();

            var exit = new ManualResetEvent(false);
            Console.CancelKeyPress += (_, _) => exit.Set();
            exit.WaitOne();

            server.Stop();
            Console.WriteLine("Server stopped.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: {ex}");
            Environment.Exit(1);
        }
    }
}

/// <summary>
/// 最小测试 Tool，仅用于验证 ToolRouter + McpResponse 200 链路。
/// </summary>
internal sealed class PingTool : IRevitTool
{
    public string Name => "test.ping";
    public bool IsWriteTool => false;

    public McpResponse Execute(McpRequest request)
    {
        return new McpResponse
        {
            Ok = true,
            Tool = Name,
            Data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["message"] = "pong",
                ["receivedArgs"] = request.Args,
            },
        };
    }
}
