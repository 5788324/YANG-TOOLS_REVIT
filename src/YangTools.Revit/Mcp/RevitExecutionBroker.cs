using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using YangTools.Revit.Core;

namespace YangTools.Revit.Mcp
{
    internal sealed class RevitExecutionResult
    {
        internal bool Success { get; set; }
        internal string Result { get; set; } = string.Empty;
        internal string Error { get; set; } = string.Empty;
        internal long DurationMilliseconds { get; set; }
    }

    internal sealed class RevitExecutionStatus
    {
        internal bool IsReady { get; set; }
        internal int PendingCount { get; set; }
        internal string CurrentCommand { get; set; } = string.Empty;
        internal string LastCommand { get; set; } = string.Empty;
        internal string LastResult { get; set; } = string.Empty;
        internal string LastError { get; set; } = string.Empty;
        internal long LastDurationMilliseconds { get; set; }
        internal DateTime? LastCompletedAt { get; set; }
        internal bool PythonInitialized { get; set; }
        internal bool PythonAvailable { get; set; }
        internal string PythonError { get; set; } = string.Empty;
    }

    internal sealed class RevitExecutionBroker : IDisposable
    {
        private readonly RevitExecutionEventHandler _handler;
        private readonly ExternalEvent _externalEvent;
        private int _pendingCount;
        private bool _disposed;

        internal RevitExecutionBroker()
        {
            _handler = new RevitExecutionEventHandler(this);
            _externalEvent = ExternalEvent.Create(_handler);
        }

        internal Task<RevitExecutionResult> ExecuteAsync(
            string command,
            JObject? args,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RevitExecutionBroker));
            if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("执行命令不能为空。", nameof(command));

            var completion = new TaskCompletionSource<RevitExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var request = new RevitExecutionRequest(this)
            {
                Command = command.Trim(),
                OperationId = Guid.NewGuid().ToString("N"),
                Args = args ?? new JObject(),
                Completion = completion,
                CancellationToken = cancellationToken,
                QueuedAt = DateTime.UtcNow
            };

            _handler.Enqueue(request);
            Interlocked.Increment(ref _pendingCount);

            try
            {
                string raiseStatus = _externalEvent.Raise().ToString();
                if (!raiseStatus.Equals("Accepted", StringComparison.OrdinalIgnoreCase) &&
                    !raiseStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                {
                    request.TryCancelBeforeStart("Revit ExternalEvent 未接受请求：" + raiseStatus);
                }
            }
            catch (Exception ex)
            {
                request.TryCancelBeforeStart("无法发起 Revit ExternalEvent：" + ex.Message);
            }

            return AwaitWithTimeoutAsync(request, timeout, cancellationToken);
        }

        internal RevitExecutionStatus GetStatus()
        {
            return new RevitExecutionStatus
            {
                IsReady = !_disposed,
                PendingCount = Math.Max(0, Volatile.Read(ref _pendingCount)),
                CurrentCommand = _handler.CurrentCommand,
                LastCommand = _handler.LastCommand,
                LastResult = _handler.LastResult,
                LastError = _handler.LastError,
                LastDurationMilliseconds = _handler.LastDurationMilliseconds,
                LastCompletedAt = _handler.LastCompletedAt,
                PythonInitialized = _handler.PythonInitialized,
                PythonAvailable = _handler.PythonAvailable,
                PythonError = _handler.PythonError
            };
        }

        private async Task<RevitExecutionResult> AwaitWithTimeoutAsync(
            RevitExecutionRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            TimeSpan effective = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(15) : timeout;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                Task delay = Task.Delay(effective, linked.Token);
                Task finished = await Task.WhenAny(request.Completion.Task, delay).ConfigureAwait(false);
                if (finished == request.Completion.Task)
                {
                    linked.Cancel();
                    return await request.Completion.Task.ConfigureAwait(false);
                }

                // Once Revit has entered Dispatch, neither a timeout nor HTTP client
                // cancellation can undo/interrupt a Revit API call. Do NOT complete
                // the request in that state: the event handler still owns its result.
                if (cancellationToken.IsCancellationRequested)
                {
                    if (request.TryCancelBeforeStart("请求在进入 Revit 主线程之前已取消，未执行。"))
                        throw new OperationCanceledException(cancellationToken);
                    return new RevitExecutionResult
                    {
                        Success = false,
                        Error = "请求已取消，但 Revit 主线程已开始执行；操作结果未知，禁止自动重试。操作编号：" + request.OperationId + "。请查看 YangTools 日志与模型实际结果。"
                    };
                }

                if (request.TryCancelBeforeStart("等待 Revit 主线程执行超时；请求尚未开始，已取消，未修改模型。"))
                    return await request.Completion.Task.ConfigureAwait(false);

                // Running or concurrently completed. If already completed, prefer
                // its real result; otherwise return an explicit uncertainty result.
                if (request.Completion.Task.IsCompleted)
                    return await request.Completion.Task.ConfigureAwait(false);
                return new RevitExecutionResult
                {
                    Success = false,
                    Error = "等待 Revit 主线程超时，但操作已开始且无法中断；实际提交状态未知。操作编号：" + request.OperationId + "。禁止自动重试或认定模型未修改，请查询 MCP 状态/操作日志并核对模型。"
                };
            }
        }

        internal void NotifyRequestFinished()
        {
            int remaining = Interlocked.Decrement(ref _pendingCount);
            if (remaining < 0) Interlocked.Exchange(ref _pendingCount, 0);
        }

        public void Dispose()
        {
            _disposed = true;
            try { _externalEvent.Dispose(); } catch { }
        }

        private sealed class RevitExecutionRequest
        {
            private readonly RevitExecutionBroker _owner;
            // 0 = queued; 1 = running in Revit; 2 = finished or canceled before start.
            private int _state;

            internal RevitExecutionRequest(RevitExecutionBroker owner)
            {
                _owner = owner;
            }

            internal string Command { get; set; } = string.Empty;
            internal string OperationId { get; set; } = string.Empty;
            internal JObject Args { get; set; } = new JObject();
            internal TaskCompletionSource<RevitExecutionResult> Completion { get; set; } = null!;
            internal CancellationToken CancellationToken { get; set; }
            internal DateTime QueuedAt { get; set; }

            internal bool TryStart() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;

            internal bool TryCancelBeforeStart(string error)
            {
                if (Interlocked.CompareExchange(ref _state, 2, 0) != 0) return false;
                try { return Completion.TrySetResult(new RevitExecutionResult { Success = false, Error = error }); }
                finally { _owner.NotifyRequestFinished(); }
            }

            internal bool TryComplete(RevitExecutionResult result)
            {
                // The handler is the only actor completing a running request.
                if (Interlocked.CompareExchange(ref _state, 2, 1) != 1) return false;
                try { return Completion.TrySetResult(result); }
                finally { _owner.NotifyRequestFinished(); }
            }

            internal bool TryCompleteFailure(string error)
            {
                return TryComplete(new RevitExecutionResult { Success = false, Error = error ?? string.Empty });
            }
        }

        private sealed class RevitExecutionEventHandler : IExternalEventHandler
        {
            private readonly ConcurrentQueue<RevitExecutionRequest> _queue = new ConcurrentQueue<RevitExecutionRequest>();
            private readonly RevitExecutionBroker _owner;
            private readonly PythonToolHandler _pythonHandler;
            private readonly RevitToolRouter _router;

            internal RevitExecutionEventHandler(RevitExecutionBroker owner)
            {
                _owner = owner;
                _pythonHandler = new PythonToolHandler(error => LastError = error);
                _router = new RevitToolRouter(_pythonHandler);
            }

            internal string CurrentCommand { get; private set; } = string.Empty;
            internal string LastCommand { get; private set; } = string.Empty;
            internal string LastResult { get; private set; } = string.Empty;
            internal string LastError { get; private set; } = string.Empty;
            internal long LastDurationMilliseconds { get; private set; }
            internal DateTime? LastCompletedAt { get; private set; }
            internal bool PythonInitialized => _pythonHandler.PythonInitialized;
            internal bool PythonAvailable => _pythonHandler.PythonAvailable;
            internal string PythonError => _pythonHandler.PythonError;

            internal void Enqueue(RevitExecutionRequest request) => _queue.Enqueue(request);

            public void Execute(UIApplication uiapp)
            {
                while (_queue.TryDequeue(out RevitExecutionRequest? request))
                {
                    // A queued timeout can cancel before execution, never during it.
                    if (request.CancellationToken.IsCancellationRequested)
                    {
                        request.TryCancelBeforeStart("请求在开始执行前被取消，未修改模型。");
                        continue;
                    }
                    if (!request.TryStart()) continue;
                    var stopwatch = Stopwatch.StartNew();
                    CurrentCommand = request.Command;
                    try
                    {
                        RevitExecutionResult result = _router.Dispatch(uiapp, request.Command, request.Args);
                        stopwatch.Stop();
                        result.DurationMilliseconds = stopwatch.ElapsedMilliseconds;
                        LastCommand = request.Command;
                        LastDurationMilliseconds = result.DurationMilliseconds;
                        LastCompletedAt = DateTime.Now;
                        LastResult = result.Success ? Trim(result.Result, 600) : string.Empty;
                        LastError = result.Success ? string.Empty : Trim(result.Error, 600);
                        request.TryComplete(result);
                        // Correlate late completions with a timeout/unknown response.
                        // A router success does not imply a model-transaction commit.
                        LogDispatchOutcome(uiapp, request, result.Success ? "DISPATCH_RETURNED_SUCCESS" : "DISPATCH_RETURNED_ERROR",
                            result.Success ? result.Result : result.Error);
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        string logPath = DiagnosticLog.WriteError("Revit 执行器 " + request.Command, ex);
                        LastCommand = request.Command;
                        LastDurationMilliseconds = stopwatch.ElapsedMilliseconds;
                        LastCompletedAt = DateTime.Now;
                        LastResult = string.Empty;
                        LastError = ex.Message;
                        request.TryComplete(new RevitExecutionResult
                        {
                            Success = false,
                            Error = ex.Message + "\n诊断日志：" + logPath,
                            DurationMilliseconds = stopwatch.ElapsedMilliseconds
                        });
                        LogDispatchOutcome(uiapp, request, "DISPATCH_THROWN", ex.Message);
                    }
                    finally
                    {
                        CurrentCommand = string.Empty;
                    }
                }
            }

            private static void LogDispatchOutcome(UIApplication app, RevitExecutionRequest request,
                string state, string detail)
            {
                try
                {
                    DiagnosticLog.WriteProductOperation("MCP." + request.Command,
                        request.OperationId, state, app.ActiveUIDocument?.Document, Trim(detail, 400));
                }
                catch { /* Disk logging must not change the outcome of a Revit call. */ }
            }

            private static string Trim(string value, int max) => string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max) + "…";

            public string GetName() => "YangTools Revit Execution Broker";
        }
    }
}
