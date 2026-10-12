using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// 轻量本地诊断日志。用于避免 Revit/WPF 事件或引擎异常只表现为“无反应”。
    /// </summary>
    public static class DiagnosticLog
    {
        private static readonly object SyncRoot = new object();
        private static string? _assemblySha256;

        public static string WriteInfo(string operation, string message)
        {
            return Write(operation, "INFO", message, null);
        }

        public static string WriteError(string operation, Exception exception)
        {
            return Write(operation, "ERROR", exception.Message, exception);
        }

        /// <summary>
        /// 产品链自然执行日志。用于把 UI/Command/Handler/Service 的真实运行与具体文档、
        /// OperationId/BatchRunId、签名后的已加载 DLL 会话对应起来。
        /// </summary>
        public static string WriteProductOperation(
            string operation,
            string operationId,
            string state,
            Document? document,
            string message,
            string? batchRunId = null)
        {
            string documentIdentity = "<none>";
            if (document != null)
            {
                try
                {
                    string path = string.IsNullOrWhiteSpace(document.PathName) ? "<unsaved>" : document.PathName;
                    documentIdentity = $"Title={document.Title}; Path={path}; IsCloud={document.IsModelInCloud}";
                }
                catch
                {
                    documentIdentity = "<invalid document>";
                }
            }

            string payload =
                $"OperationId: {operationId}\n" +
                $"BatchRunId: {(string.IsNullOrWhiteSpace(batchRunId) ? "<none>" : batchRunId)}\n" +
                $"State: {state}\n" +
                $"ProcessId: {Process.GetCurrentProcess().Id}\n" +
                $"LoadedAssemblySha256: {GetLoadedAssemblySha256()}\n" +
                $"Document: {documentIdentity}\n" +
                $"Detail: {message}";
            return Write(operation, "PRODUCT", payload, null);
        }

        private static string GetLoadedAssemblySha256()
        {
            if (!string.IsNullOrWhiteSpace(_assemblySha256))
                return _assemblySha256!;

            lock (SyncRoot)
            {
                if (!string.IsNullOrWhiteSpace(_assemblySha256))
                    return _assemblySha256!;

                try
                {
                    string location = Assembly.GetExecutingAssembly().Location;
                    if (string.IsNullOrWhiteSpace(location) || !File.Exists(location))
                        return "UNAVAILABLE";

                    using (var stream = File.OpenRead(location))
                    using (SHA256 sha = SHA256.Create())
                    {
                        byte[] hash = sha.ComputeHash(stream);
                        _assemblySha256 = BitConverter.ToString(hash).Replace("-", string.Empty);
                    }
                }
                catch
                {
                    _assemblySha256 = "UNAVAILABLE";
                }

                return _assemblySha256!;
            }
        }

        private static string Write(string operation, string level, string message, Exception? exception)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "YangTools",
                    "Logs");
                Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, $"YangTools-{DateTime.Now:yyyy-MM-dd}.log");
                var builder = new StringBuilder();
                builder.AppendLine("============================================================");
                builder.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                builder.AppendLine($"Level: {level}");
                builder.AppendLine($"Operation: {operation}");
                builder.AppendLine($"Message: {message}");
                if (exception != null)
                {
                    builder.AppendLine("Exception:");
                    builder.AppendLine(exception.ToString());
                }

                lock (SyncRoot)
                {
                    File.AppendAllText(path, builder.ToString(), new UTF8Encoding(false));
                }

                return path;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
