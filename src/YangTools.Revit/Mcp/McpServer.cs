using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using YangTools.Revit.Tools;
using YangTools.Revit.Utils;

namespace YangTools.Revit.Mcp;

/// <summary>
/// 统一 MCP HTTP 入口。
///
/// 请求链路：
///   HTTP /mcp/ 请求
///   → JSON body → McpRequest
///   → McpServer.Dispatch() → ToolRouter.Route()
///   → McpResponse → JSON body → HTTP response
///   → OperationLogger.Log()  ← 所有路径均写日志
///
/// 线程边界：
///   HTTP 监听线程只做请求接收、JSON 解析、响应返回。
///   绝不直接访问 Revit Document 或 Revit API。
///   后续 Tool 如需 Revit API，必须通过 ExternalEvent 投递到 Revit 主线程。
/// </summary>
public sealed class McpServer
{
    private readonly ToolRouter _toolRouter;
    private HttpListener? _listener;
    private Thread? _listenerThread;
    private volatile bool _running;

    public McpServer(ToolRouter toolRouter)
    {
        _toolRouter = toolRouter ?? throw new ArgumentNullException(nameof(toolRouter));
    }

    public string Endpoint => App.McpEndpoint;
    public bool IsRunning => _running;

    /// <summary>
    /// 启动 HTTP 监听。在独立后台线程上运行，不阻塞调用者。
    /// </summary>
    public void Start()
    {
        if (_running)
            return;

        _listener = new HttpListener();
        _listener.Prefixes.Add(Endpoint);

        try
        {
            _listener.Start();
        }
        catch (HttpListenerException ex)
        {
            throw new InvalidOperationException(
                $"无法启动 HTTP listener（{Endpoint}）。" +
                $"如端口被占用或缺少 administrator 权限，请检查。 " +
                $"内部错误：{ex.Message}", ex);
        }

        _running = true;
        _listenerThread = new Thread(ListenerLoop)
        {
            Name = "YangTools-McpHttp",
            IsBackground = true,
        };
        _listenerThread.Start();
    }

    /// <summary>
    /// 停止 HTTP 监听。
    /// </summary>
    public void Stop()
    {
        _running = false;
        try
        {
            _listener?.Stop();
        }
        catch (ObjectDisposedException) { }
        _listenerThread?.Join(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// 核心路由：同步调用 ToolRouter。
    /// HTTP listener 线程调用此方法，不直接访问 Revit Document。
    /// </summary>
    public McpResponse Dispatch(McpRequest request)
    {
        return _toolRouter.Route(request);
    }

    // ────────────────────────────────────────────
    //  HTTP Listener 主循环
    // ────────────────────────────────────────────

    private void ListenerLoop()
    {
        while (_running && _listener != null && _listener.IsListening)
        {
            HttpListenerContext? context = null;
            try
            {
                context = _listener.GetContext();
            }
            catch (HttpListenerException)
            {
                if (!_running) break;
                continue;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            ThreadPool.QueueUserWorkItem(_ => ProcessRequest(context));
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        var httpRequest = context.Request;
        var httpResponse = context.Response;
        httpResponse.ContentType = "application/json; charset=utf-8";

        try
        {
            // ── 1. 路径检查 ──
            if (!httpRequest.Url!.AbsolutePath.Equals("/mcp/", StringComparison.OrdinalIgnoreCase))
            {
                RespondAndLog(httpResponse, 404,
                    McpResponse.Failure(string.Empty, "未找到该路径。仅支持 POST /mcp/"));
                return;
            }

            // ── 2. 方法检查 ──
            if (!httpRequest.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                RespondAndLog(httpResponse, 405,
                    McpResponse.Failure(string.Empty, $"不支持的 HTTP 方法：{httpRequest.HttpMethod}。仅支持 POST。"));
                return;
            }

            // ── 3. 读取 body ──
            string body;
            using (var reader = new StreamReader(httpRequest.InputStream, httpRequest.ContentEncoding))
            {
                body = reader.ReadToEnd();
            }

            // ── 4. JSON 解析 ──
            var mcpRequest = JsonUtils.DeserializeRequest(body);
            if (mcpRequest == null)
            {
                RespondAndLog(httpResponse, 400,
                    McpResponse.Failure(string.Empty, "无法解析请求 JSON。请检查 body 格式。"));
                return;
            }

            // ── 5. 空 tool 检查 ──
            if (string.IsNullOrWhiteSpace(mcpRequest.Tool))
            {
                RespondAndLog(httpResponse, 400,
                    McpResponse.Failure(string.Empty, "tool 不能为空。"), mcpRequest);
                return;
            }

            // ── 6. 路由（同步，不访问 Revit Document）──
            var mcpResponse = Dispatch(mcpRequest);

            // ── 7. 返回 + 日志 ──
            int statusCode = mcpResponse.Ok ? 200 : 400;
            RespondAndLog(httpResponse, statusCode, mcpResponse, mcpRequest);
        }
        catch (Exception ex)
        {
            var error = McpResponse.Failure(string.Empty,
                $"服务器内部错误：{ex.GetType().Name} - {ex.Message}");
            RespondAndLog(httpResponse, 500, error);
        }
    }

    /// <summary>
    /// 写 HTTP 响应 + 记录操作日志（所有响应路径的统一出口）。
    /// </summary>
    private static void RespondAndLog(HttpListenerResponse httpResponse, int statusCode,
        McpResponse mcpResponse, McpRequest? mcpRequest = null)
    {
        OperationLogger.Log(mcpRequest, mcpResponse);
        WriteJson(httpResponse, statusCode, mcpResponse);
    }

    private static void WriteJson(HttpListenerResponse response, int statusCode, McpResponse mcpResponse)
    {
        response.StatusCode = statusCode;
        var json = JsonUtils.SerializeResponse(mcpResponse);
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }
}
