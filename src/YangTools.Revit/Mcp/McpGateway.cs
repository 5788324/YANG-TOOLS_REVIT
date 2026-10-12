using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YangTools.Revit.Core;

namespace YangTools.Revit.Mcp
{
    internal sealed class McpGatewayStatusSnapshot
    {
        internal bool IsRunning { get; set; }
        internal int Port { get; set; }
        internal string ServerUrl { get; set; } = string.Empty;
        internal string Token { get; set; } = string.Empty;
        internal string LastCommand { get; set; } = string.Empty;
        internal string LastResult { get; set; } = string.Empty;
        internal string LastError { get; set; } = string.Empty;
        internal long LastDurationMilliseconds { get; set; }
        internal DateTime? LastRequestAt { get; set; }
        internal string InstanceFilePath { get; set; } = string.Empty;
    }

    internal sealed class McpGateway : IDisposable
    {
        private readonly RevitExecutionBroker _broker;
        private readonly object _sync = new object();
        private TcpListener? _listener;
        private CancellationTokenSource? _cancellation;
        private Task? _listenTask;
        private string _token = string.Empty;
        private int _port;
        private string _lastCommand = string.Empty;
        private string _lastResult = string.Empty;
        private string _lastError = string.Empty;
        private long _lastDurationMilliseconds;
        private DateTime? _lastRequestAt;
        private DateTime? _startedAt;
        private bool _disposed;

        internal McpGateway(RevitExecutionBroker broker)
        {
            _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        }

        internal bool IsRunning
        {
            get
            {
                lock (_sync) return _listener != null;
            }
        }

        internal string ServerUrl => IsRunning ? "http://127.0.0.1:" + _port + "/mcp/" : string.Empty;
        internal string Token => _token;
        internal string InstanceFilePath => GetInstanceFilePath();

        internal void Start()
        {
            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(McpGateway));
                if (_listener != null) return;

                TcpListener? listener = null;
                int selectedPort = 0;
                for (int port = 8081; port <= 8090; port++)
                {
                    try
                    {
                        listener = new TcpListener(IPAddress.Loopback, port);
                        listener.Start();
                        selectedPort = port;
                        break;
                    }
                    catch (SocketException)
                    {
                        try { listener?.Stop(); } catch { }
                        listener = null;
                    }
                }
                if (listener == null)
                    throw new InvalidOperationException("8081–8090 端口均不可用，无法启动 MCP 网关。 ");

                _listener = listener;
                _port = selectedPort;
                _token = GenerateToken();
                _startedAt = DateTime.Now;
                _lastCommand = string.Empty;
                _lastResult = string.Empty;
                _lastError = string.Empty;
                _lastDurationMilliseconds = 0;
                _lastRequestAt = null;
                _cancellation = new CancellationTokenSource();
                _listenTask = Task.Run(() => ListenLoopAsync(listener, _cancellation.Token));
                WriteInstanceFile();
            }
        }

        internal void Stop()
        {
            TcpListener? listener;
            CancellationTokenSource? cancellation;
            lock (_sync)
            {
                listener = _listener;
                cancellation = _cancellation;
                _listener = null;
                _cancellation = null;
                _listenTask = null;
                _port = 0;
                _token = string.Empty;
                _startedAt = null;
            }
            try { cancellation?.Cancel(); } catch { }
            try { listener?.Stop(); } catch { }
            try { cancellation?.Dispose(); } catch { }
            DeleteInstanceFile();
        }

        internal void Restart()
        {
            Stop();
            Start();
        }

        internal McpGatewayStatusSnapshot GetStatus()
        {
            lock (_sync)
            {
                return new McpGatewayStatusSnapshot
                {
                    IsRunning = _listener != null,
                    Port = _port,
                    ServerUrl = _listener != null ? "http://127.0.0.1:" + _port + "/mcp/" : string.Empty,
                    Token = _token,
                    LastCommand = _lastCommand,
                    LastResult = _lastResult,
                    LastError = _lastError,
                    LastDurationMilliseconds = _lastDurationMilliseconds,
                    LastRequestAt = _lastRequestAt,
                    InstanceFilePath = GetInstanceFilePath()
                };
            }
        }

        internal async Task<string> SelfTestAsync(CancellationToken cancellationToken)
        {
            McpGatewayStatusSnapshot status = GetStatus();
            if (!status.IsRunning) throw new InvalidOperationException("MCP 网关尚未启动。 ");
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
            using (var request = new HttpRequestMessage(HttpMethod.Post, status.ServerUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", status.Token);
                request.Content = new StringContent(
                    new JObject
                    {
                        ["command"] = "get_revit_status",
                        ["args"] = new JObject()
                    }.ToString(Formatting.None),
                    Encoding.UTF8,
                    "application/json");
                using (HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException("MCP 自检失败：HTTP " + (int)response.StatusCode + "\n" + body);
                    JObject payload = JObject.Parse(body);
                    if (payload["success"]?.Value<bool>() != true)
                        throw new InvalidOperationException("MCP 执行器自检失败：" + (payload["error"]?.ToString() ?? "未知错误"));
                    return payload.ToString(Formatting.Indented);
                }
            }
        }

        private async Task ListenLoopAsync(TcpListener listener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        lock (_sync) _lastError = ex.Message;
                        DiagnosticLog.WriteError("MCP 网关监听", ex);
                    }
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    HttpRequestData request = await ReadRequestAsync(stream, cancellationToken).ConfigureAwait(false);
                    if (!IsAuthorized(request))
                    {
                        await WriteJsonResponseAsync(stream, 401, new JObject
                        {
                            ["success"] = false,
                            ["error"] = "Unauthorized"
                        }, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    string normalizedPath = request.Path.TrimEnd('/');
                    if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                        normalizedPath.Equals("/mcp/status", StringComparison.OrdinalIgnoreCase))
                    {
                        RecordGatewayActivity("get_gateway_status", true, "状态查询", string.Empty, 0);
                        await WriteJsonResponseAsync(stream, 200, BuildStatusPayload(), cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    if (normalizedPath.Equals("/mcp", StringComparison.OrdinalIgnoreCase) &&
                        !request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteJsonResponseAsync(stream, 405, new JObject
                        {
                            ["success"] = false,
                            ["error"] = "Method Not Allowed; use POST /mcp/"
                        }, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                    if (!normalizedPath.Equals("/mcp", StringComparison.OrdinalIgnoreCase))
                    {
                        await WriteJsonResponseAsync(stream, 404, new JObject
                        {
                            ["success"] = false,
                            ["error"] = "Not Found"
                        }, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    JObject body = string.IsNullOrWhiteSpace(request.Body) ? new JObject() : JObject.Parse(request.Body);
                    string command = body["command"]?.ToString() ?? string.Empty;
                    JObject args = body["args"] as JObject ?? new JObject();
                    McpGatewaySettings settings = McpGatewaySettingsStore.Load();
                    if (command.Equals("execute_revit_python", StringComparison.Ordinal) && !settings.AllowPythonExecution)
                    {
                        RecordGatewayActivity(command, false, string.Empty, "外部 MCP Python 执行未启用。", 0);
                        await WriteJsonResponseAsync(stream, 403, new JObject
                        {
                            ["success"] = false,
                            ["error"] = "外部 MCP Python 执行未启用。"
                        }, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    DateTime started = DateTime.UtcNow;
                    RevitExecutionResult result = await _broker.ExecuteAsync(
                        command,
                        args,
                        TimeSpan.FromSeconds(60),
                        cancellationToken).ConfigureAwait(false);
                    long duration = (long)(DateTime.UtcNow - started).TotalMilliseconds;
                    RecordGatewayActivity(
                        command,
                        result.Success,
                        result.Result,
                        result.Error,
                        result.DurationMilliseconds > 0 ? result.DurationMilliseconds : duration);

                    await WriteJsonResponseAsync(stream, result.Success ? 200 : 500, new JObject
                    {
                        ["success"] = result.Success,
                        ["result"] = result.Result,
                        ["error"] = result.Error,
                        ["durationMs"] = result.DurationMilliseconds
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (JsonException ex)
                {
                    await WriteJsonResponseAsync(stream, 400, new JObject
                    {
                        ["success"] = false,
                        ["error"] = "JSON 无效：" + ex.Message
                    }, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lock (_sync) _lastError = ex.Message;
                    DiagnosticLog.WriteError("MCP 网关请求", ex);
                    try
                    {
                        await WriteJsonResponseAsync(stream, 500, new JObject
                        {
                            ["success"] = false,
                            ["error"] = ex.Message
                        }, cancellationToken).ConfigureAwait(false);
                    }
                    catch { }
                }
            }
        }

        private void RecordGatewayActivity(string command, bool success, string result, string error, long durationMilliseconds)
        {
            lock (_sync)
            {
                _lastCommand = command ?? string.Empty;
                _lastResult = success ? Trim(result, 600) : string.Empty;
                _lastError = success ? string.Empty : Trim(error, 600);
                _lastDurationMilliseconds = Math.Max(0, durationMilliseconds);
                _lastRequestAt = DateTime.Now;
                WriteInstanceFile();
            }
        }

        private JObject BuildStatusPayload()
        {
            RevitExecutionStatus execution = _broker.GetStatus();
            McpGatewayStatusSnapshot gateway = GetStatus();
            return new JObject
            {
                ["success"] = true,
                ["gateway"] = new JObject
                {
                    ["running"] = gateway.IsRunning,
                    ["port"] = gateway.Port,
                    ["url"] = gateway.ServerUrl,
                    ["processId"] = Process.GetCurrentProcess().Id,
                    ["lastRequestAt"] = gateway.LastRequestAt?.ToString("O") ?? string.Empty
                },
                ["execution"] = new JObject
                {
                    ["ready"] = execution.IsReady,
                    ["pending"] = execution.PendingCount,
                    ["currentCommand"] = execution.CurrentCommand,
                    ["pythonAvailable"] = execution.PythonAvailable,
                    ["lastCommand"] = execution.LastCommand,
                    ["lastError"] = execution.LastError
                }
            };
        }

        private bool IsAuthorized(HttpRequestData request)
        {
            string authorization;
            if (!request.Headers.TryGetValue("Authorization", out authorization)) return false;
            const string prefix = "Bearer ";
            if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            string candidate = authorization.Substring(prefix.Length).Trim();
            return FixedTimeEquals(candidate, _token);
        }

        private void WriteInstanceFile()
        {
            if (!IsRunning) return;
            string path = GetInstanceFilePath();
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            RevitExecutionStatus execution = _broker.GetStatus();
            var payload = new JObject
            {
                ["processId"] = Process.GetCurrentProcess().Id,
                ["port"] = _port,
                ["url"] = "http://127.0.0.1:" + _port + "/mcp/",
                ["token"] = _token,
                ["startedAt"] = (_startedAt ?? DateTime.Now).ToString("O"),
                ["lastRequestAt"] = _lastRequestAt?.ToString("O") ?? string.Empty,
                ["lastCommand"] = _lastCommand,
                ["lastError"] = _lastError,
                ["executionReady"] = execution.IsReady
            };
            File.WriteAllText(path, payload.ToString(Formatting.Indented), Encoding.UTF8);
        }

        private void DeleteInstanceFile()
        {
            try
            {
                string path = GetInstanceFilePath();
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        private static string GetInstanceFilePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YangTools", "MCP", "instances",
                Process.GetCurrentProcess().Id + ".json");
        }

        private static string GenerateToken()
        {
            byte[] bytes = new byte[32];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            byte[] a = Encoding.UTF8.GetBytes(left ?? string.Empty);
            byte[] b = Encoding.UTF8.GetBytes(right ?? string.Empty);
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static async Task<HttpRequestData> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            var bytes = new List<byte>();
            int headerEnd = -1;
            while (headerEnd < 0)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (read <= 0) throw new IOException("连接已关闭。 ");
                bytes.AddRange(buffer.Take(read));
                headerEnd = FindHeaderEnd(bytes);
                if (bytes.Count > 1024 * 1024) throw new InvalidOperationException("HTTP 请求头过大。 ");
            }

            string headerText = Encoding.UTF8.GetString(bytes.Take(headerEnd).ToArray());
            string[] lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] requestLine = lines[0].Split(' ');
            if (requestLine.Length < 2) throw new InvalidOperationException("HTTP 请求行无效。 ");
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }

            int contentLength = 0;
            if (headers.TryGetValue("Content-Length", out string value)) int.TryParse(value, out contentLength);
            int bodyStart = headerEnd + 4;
            while (bytes.Count - bodyStart < contentLength)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (read <= 0) break;
                bytes.AddRange(buffer.Take(read));
                if (bytes.Count > 8 * 1024 * 1024) throw new InvalidOperationException("HTTP 请求体过大。 ");
            }

            string body = contentLength > 0
                ? Encoding.UTF8.GetString(bytes.Skip(bodyStart).Take(contentLength).ToArray())
                : string.Empty;
            return new HttpRequestData
            {
                Method = requestLine[0],
                Path = requestLine[1].Split('?')[0],
                Headers = headers,
                Body = body
            };
        }

        private static int FindHeaderEnd(IList<byte> bytes)
        {
            for (int i = 0; i <= bytes.Count - 4; i++)
                if (bytes[i] == 13 && bytes[i + 1] == 10 && bytes[i + 2] == 13 && bytes[i + 3] == 10)
                    return i;
            return -1;
        }

        private static async Task WriteJsonResponseAsync(
            NetworkStream stream,
            int statusCode,
            JObject payload,
            CancellationToken cancellationToken)
        {
            byte[] body = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));
            string reason = statusCode == 200 ? "OK" : statusCode == 400 ? "Bad Request" :
                statusCode == 401 ? "Unauthorized" : statusCode == 403 ? "Forbidden" :
                statusCode == 404 ? "Not Found" : statusCode == 405 ? "Method Not Allowed" :
                "Internal Server Error";
            string header = "HTTP/1.1 " + statusCode + " " + reason + "\r\n" +
                            "Content-Type: application/json; charset=utf-8\r\n" +
                            "Content-Length: " + body.Length + "\r\n" +
                            "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync(body, 0, body.Length, cancellationToken).ConfigureAwait(false);
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value ?? string.Empty;
            return value.Substring(0, max) + "…";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }

        private sealed class HttpRequestData
        {
            internal string Method { get; set; } = string.Empty;
            internal string Path { get; set; } = string.Empty;
            internal Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            internal string Body { get; set; } = string.Empty;
        }
    }
}
