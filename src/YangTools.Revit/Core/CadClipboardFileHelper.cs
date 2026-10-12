using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace YangTools.Revit.Core
{
    public sealed class CadClipboardFileInfo
    {
        public string FullPath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public DateTime LastWriteTime { get; set; }
        public long SizeBytes { get; set; }
        public bool IsStale { get; set; }

        public string DisplayText
        {
            get
            {
                string age = IsStale ? "较早" : "最新";
                return $"{FileName}  ·  {LastWriteTime:MM-dd HH:mm:ss}  ·  {FormatSize(SizeBytes)}  ·  {age}";
            }
        }

        private static string FormatSize(long value)
        {
            if (value >= 1024L * 1024L)
                return $"{value / 1024d / 1024d:F1} MB";
            if (value >= 1024L)
                return $"{value / 1024d:F1} KB";
            return $"{value} B";
        }
    }

    public sealed class CadCacheCleanupResult
    {
        public int RemovedDirectories { get; set; }
        public int RemovedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public long RemovedBytes { get; set; }

        public void Add(CadCacheCleanupResult other)
        {
            if (other == null) return;
            RemovedDirectories += other.RemovedDirectories;
            RemovedFiles += other.RemovedFiles;
            SkippedFiles += other.SkippedFiles;
            RemovedBytes += other.RemovedBytes;
        }
    }

    /// <summary>
    /// 发现 AutoCAD 剪贴板临时 DWG，并管理 YangTools 自己创建的导入工作副本。
    /// </summary>
    public static class CadClipboardFileHelper
    {
        private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(30);
        private static readonly string CacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YangTools", "Temp", "PasteCad");
        private static readonly string SessionRoot = Path.Combine(
            CacheRoot,
            Process.GetCurrentProcess().Id.ToString());

        public static string GetCacheRoot() => CacheRoot;

        public static IReadOnlyList<CadClipboardFileInfo> FindCandidates(int maximumCount = 20)
        {
            var found = new Dictionary<string, CadClipboardFileInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (string directory in GetClipboardTempDirectories())
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(directory, "A$C*.dwg", SearchOption.TopDirectoryOnly).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (string file in files)
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (!info.Exists || info.Length <= 0)
                            continue;

                        found[NormalizePath(info.FullName)] = new CadClipboardFileInfo
                        {
                            FullPath = info.FullName,
                            FileName = info.Name,
                            LastWriteTime = info.LastWriteTime,
                            SizeBytes = info.Length,
                            IsStale = DateTime.Now - info.LastWriteTime > StaleThreshold
                        };
                    }
                    catch
                    {
                        // 单个临时文件不可访问时跳过，不阻断其余候选项。
                    }
                }
            }

            return found.Values
                .OrderByDescending(item => item.LastWriteTime)
                .Take(Math.Max(1, maximumCount))
                .ToList();
        }

        public static CadClipboardFileInfo CreateInfo(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0)
                throw new FileNotFoundException("所选 DWG 文件不存在或大小为零。", path);

            return new CadClipboardFileInfo
            {
                FullPath = info.FullName,
                FileName = info.Name,
                LastWriteTime = info.LastWriteTime,
                SizeBytes = info.Length,
                IsStale = DateTime.Now - info.LastWriteTime > StaleThreshold
            };
        }

        public static string CreateWorkingCopy(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("DWG 源文件路径为空。", nameof(sourcePath));
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("DWG 源文件不存在。", sourcePath);

            // 每次运行前顺手清理已结束会话的一天前残留；不触碰当前会话缓存。
            CleanupYangToolsCache(false, TimeSpan.FromDays(1));

            string root = Path.Combine(SessionRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            string destination = Path.Combine(root, Path.GetFileName(sourcePath));
            Exception? lastError = null;

            for (int attempt = 1; attempt <= 4; attempt++)
            {
                try
                {
                    CopyReadableSnapshot(sourcePath, destination);
                    var copied = new FileInfo(destination);
                    if (!copied.Exists || copied.Length <= 0)
                        throw new IOException("DWG 工作副本为空。请重新在 AutoCAD 中复制。 ");
                    return destination;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    TryDeleteFile(destination);
                    Thread.Sleep(150 * attempt);
                }
            }

            CleanupWorkingCopy(destination);
            throw new IOException("无法读取 AutoCAD 临时 DWG。文件可能仍在写入或已被清理。", lastError);
        }

        public static void CleanupWorkingCopy(string? workingFilePath)
        {
            if (string.IsNullOrWhiteSpace(workingFilePath))
                return;

            string? directory = Path.GetDirectoryName(workingFilePath);
            if (string.IsNullOrWhiteSpace(directory))
                return;

            for (int attempt = 1; attempt <= 4; attempt++)
            {
                try
                {
                    if (Directory.Exists(directory))
                        Directory.Delete(directory, true);
                    RemoveEmptySessionRoot();
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == 4)
                    {
                        DiagnosticLog.WriteError("清理从 CAD 粘贴工作副本", ex);
                        return;
                    }
                    Thread.Sleep(120 * attempt);
                }
            }
        }

        /// <summary>
        /// 清理 YangTools 自己创建的 PasteCad 缓存。默认不删除当前 Revit 会话目录。
        /// </summary>
        public static CadCacheCleanupResult CleanupYangToolsCache(
            bool includeCurrentSession,
            TimeSpan? minimumAge = null)
        {
            var result = new CadCacheCleanupResult();
            if (!Directory.Exists(CacheRoot))
                return result;

            TimeSpan age = minimumAge ?? TimeSpan.Zero;
            int currentPid = Process.GetCurrentProcess().Id;

            foreach (string directory in Directory.GetDirectories(CacheRoot))
            {
                string name = Path.GetFileName(directory);
                if (int.TryParse(name, out int pid))
                {
                    if (pid == currentPid)
                    {
                        if (includeCurrentSession)
                        {
                            foreach (string runDirectory in SafeGetDirectories(directory))
                                TryDeleteDirectory(runDirectory, age, result);
                            RemoveEmptyDirectory(directory);
                        }
                        continue;
                    }

                    if (IsProcessRunning(pid))
                        continue;

                    TryDeleteDirectory(directory, age, result);
                }
                else
                {
                    // 兼容 3.1.10 及更早直接以 GUID 建立在 PasteCad 根目录下的缓存。
                    TryDeleteDirectory(directory, age, result);
                }
            }

            return result;
        }

        /// <summary>
        /// 清理 AutoCAD 留在系统临时目录中的旧 A$C*.dwg。只删除达到指定年龄且未被占用的文件。
        /// </summary>
        public static CadCacheCleanupResult CleanupOldClipboardFiles(TimeSpan minimumAge)
        {
            var result = new CadCacheCleanupResult();
            DateTime cutoff = DateTime.Now - minimumAge;

            foreach (string directory in GetClipboardTempDirectories())
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(directory, "A$C*.dwg", SearchOption.TopDirectoryOnly).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (string file in files)
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (!info.Exists || info.LastWriteTime > cutoff)
                            continue;
                        if (!CanOpenExclusively(file))
                        {
                            result.SkippedFiles++;
                            continue;
                        }

                        long size = info.Length;
                        File.Delete(file);
                        result.RemovedFiles++;
                        result.RemovedBytes += size;
                    }
                    catch
                    {
                        result.SkippedFiles++;
                    }
                }
            }

            return result;
        }

        private static void CopyReadableSnapshot(string sourcePath, string destinationPath)
        {
            var before = new FileInfo(sourcePath);
            before.Refresh();
            long lengthBefore = before.Length;
            DateTime writeBefore = before.LastWriteTimeUtc;

            using (var source = new FileStream(
                       sourcePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var destination = new FileStream(
                       destinationPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None))
            {
                source.CopyTo(destination);
                destination.Flush();
            }

            var after = new FileInfo(sourcePath);
            after.Refresh();
            if (after.Length != lengthBefore || after.LastWriteTimeUtc != writeBefore)
                throw new IOException("DWG 文件在复制过程中仍发生变化，请稍后重试。 ");
        }

        private static IReadOnlyList<string> GetClipboardTempDirectories()
        {
            return new[]
            {
                Path.GetTempPath(),
                Environment.GetEnvironmentVariable("TEMP") ?? string.Empty,
                Environment.GetEnvironmentVariable("TMP") ?? string.Empty,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp")
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .Where(path => path.Length > 0 && Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        }

        private static void TryDeleteDirectory(string directory, TimeSpan minimumAge, CadCacheCleanupResult result)
        {
            try
            {
                var info = new DirectoryInfo(directory);
                if (!info.Exists || DateTime.Now - info.LastWriteTime < minimumAge)
                    return;

                long bytes = 0;
                int files = 0;
                foreach (string file in SafeGetFiles(directory))
                {
                    files++;
                    try { bytes += new FileInfo(file).Length; } catch { }
                }

                Directory.Delete(directory, true);
                result.RemovedDirectories++;
                result.RemovedFiles += files;
                result.RemovedBytes += bytes;
            }
            catch
            {
                result.SkippedFiles++;
            }
        }

        private static IEnumerable<string> SafeGetFiles(string directory)
        {
            try { return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToArray(); }
            catch { return Array.Empty<string>(); }
        }

        private static IEnumerable<string> SafeGetDirectories(string directory)
        {
            try { return Directory.GetDirectories(directory); }
            catch { return Array.Empty<string>(); }
        }

        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                    return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static bool CanOpenExclusively(string path)
        {
            try
            {
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void RemoveEmptySessionRoot()
        {
            RemoveEmptyDirectory(SessionRoot);
        }

        private static void RemoveEmptyDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory)
                    && Directory.GetFiles(directory).Length == 0
                    && Directory.GetDirectories(directory).Length == 0)
                {
                    Directory.Delete(directory, false);
                }
            }
            catch { }
        }

        private static string NormalizePath(string path)
        {
            try { return Path.GetFullPath(path.Trim()); }
            catch { return string.Empty; }
        }
    }
}
