using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YangTools.Revit.Core.BatchTasks
{
    internal interface IBatchFileSystem
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        IEnumerable<string> EnumerateFiles(string path);
        void CreateDirectory(string path);
        void MoveFile(string source, string target);
        void DeleteFile(string path);
        void DeleteDirectory(string path, bool recursive);
        string GetFullPath(string path);
    }

    internal sealed class PhysicalBatchFileSystem : IBatchFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public IEnumerable<string> EnumerateFiles(string path) => Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public void MoveFile(string source, string target) => File.Move(source, target);
        public void DeleteFile(string path) => File.Delete(path);
        public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);
        public string GetFullPath(string path) => Path.GetFullPath(path);
    }

    internal sealed class BatchFileCommitResult
    {
        public bool Success { get; set; }
        public List<string> CommittedFiles { get; } = new List<string>();
        public string Error { get; set; } = string.Empty;
        public string BackupFolder { get; set; } = string.Empty;
        public bool BackupRetained { get; set; }
        public List<string> RecoveryMessages { get; } = new List<string>();
    }

    internal sealed class PdfFileCommitResult
    {
        public bool Success { get; set; }
        public bool BackupRetained { get; set; }
        public string BackupPath { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string Warning { get; set; } = string.Empty;
    }

    /// <summary>
    /// 批量导出文件的最终提交与恢复语义。
    /// 该类不依赖 Revit API，Windows 验收可将本文件编入小型故障注入 harness，
    /// 用假的 IBatchFileSystem 精确模拟“第二个新文件移动失败 / 旧文件恢复失败 / 目标被占用”。
    /// </summary>
    internal static class BatchFileCommitService
    {
        public static BatchFileCommitResult Commit(
            string stagingFolder,
            string outputFolder,
            bool overwrite,
            ISet<string>? batchOutputReservations)
        {
            return Commit(stagingFolder, outputFolder, overwrite, batchOutputReservations, new PhysicalBatchFileSystem());
        }

        internal static BatchFileCommitResult Commit(
            string stagingFolder,
            string outputFolder,
            bool overwrite,
            ISet<string>? batchOutputReservations,
            IBatchFileSystem fileSystem)
        {
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            var result = new BatchFileCommitResult();

            List<string> stagedFiles = fileSystem.DirectoryExists(stagingFolder)
                ? fileSystem.EnumerateFiles(stagingFolder).ToList()
                : new List<string>();
            if (stagedFiles.Count == 0)
            {
                result.Error = "暂存目录没有输出文件。";
                return result;
            }

            var targets = stagedFiles.ToDictionary(
                source => source,
                source => Path.Combine(outputFolder, Path.GetFileName(source)),
                StringComparer.OrdinalIgnoreCase);

            List<string> batchConflicts = targets.Values
                .Where(path => IsReserved(path, batchOutputReservations, fileSystem))
                .ToList();
            if (batchConflicts.Count > 0)
            {
                result.Error = "同一批次存在重复输出路径。请在命名模板中加入 {模型名}/{图纸号}，或启用每模型独立子目录："
                    + string.Join("；", batchConflicts.Take(5));
                return result;
            }

            List<string> conflicts = targets.Values.Where(fileSystem.FileExists).ToList();
            if (conflicts.Count > 0 && !overwrite)
            {
                result.Error = "以下文件已存在，且未勾选覆盖：" + string.Join("；", conflicts.Take(5));
                return result;
            }

            string backupFolder = Path.Combine(outputFolder, $".yangtools_backup_{Guid.NewGuid():N}");
            result.BackupFolder = backupFolder;
            var backups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (conflicts.Count > 0)
                    fileSystem.CreateDirectory(backupFolder);

                foreach (string target in conflicts)
                {
                    string backup = Path.Combine(backupFolder, Path.GetFileName(target));
                    fileSystem.MoveFile(target, backup);
                    backups[target] = backup;
                }

                foreach (KeyValuePair<string, string> pair in targets)
                {
                    fileSystem.MoveFile(pair.Key, pair.Value);
                    result.CommittedFiles.Add(pair.Value);
                }

                foreach (string committed in result.CommittedFiles)
                    AddReservation(committed, batchOutputReservations, fileSystem);

                // Outputs have committed, but old outputs must not disappear silently if
                // backup cleanup fails. Report the exact retained location to the caller.
                DeleteDirectoryBestEffort(backupFolder, fileSystem);
                // Cleanup verification must never roll back already committed output;
                // the backups may have been deleted by the preceding cleanup.
                try
                {
                    if (fileSystem.DirectoryExists(backupFolder))
                    {
                        result.BackupRetained = true;
                        result.RecoveryMessages.Add("导出已成功，但旧文件备份未能清理，请检查：" + backupFolder);
                    }
                }
                catch (Exception checkError)
                {
                    result.BackupRetained = true;
                    result.RecoveryMessages.Add("导出已成功，但旧备份清理结果无法确认：" + backupFolder + "；" + checkError.Message);
                }
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                foreach (string committed in result.CommittedFiles.ToList())
                {
                    try
                    {
                        if (fileSystem.FileExists(committed))
                            fileSystem.DeleteFile(committed);
                    }
                    catch (Exception deleteEx)
                    {
                        result.RecoveryMessages.Add($"无法删除失败批次的新文件 {committed}：{deleteEx.Message}");
                    }
                }

                foreach (KeyValuePair<string, string> pair in backups)
                {
                    try
                    {
                        if (fileSystem.FileExists(pair.Key))
                        {
                            result.RecoveryMessages.Add($"旧文件目标已被占用，备份已保留：{pair.Value}");
                            continue;
                        }
                        if (!fileSystem.FileExists(pair.Value))
                        {
                            result.RecoveryMessages.Add($"旧文件备份缺失：{pair.Value}");
                            continue;
                        }
                        fileSystem.MoveFile(pair.Value, pair.Key);
                    }
                    catch (Exception restoreEx)
                    {
                        result.RecoveryMessages.Add($"恢复旧文件失败 {pair.Key}（备份：{pair.Value}）：{restoreEx.Message}");
                    }
                }

                bool allBackupsRecovered = backups.All(pair => fileSystem.FileExists(pair.Key) && !fileSystem.FileExists(pair.Value));
                if (allBackupsRecovered)
                {
                    DeleteDirectoryBestEffort(backupFolder, fileSystem);
                }
                else if (fileSystem.DirectoryExists(backupFolder))
                {
                    result.BackupRetained = true;
                    result.RecoveryMessages.Add("为避免旧成果丢失，备份目录已保留：" + backupFolder);
                }

                result.Error = "提交暂存输出失败：" + ex.Message;
                if (result.RecoveryMessages.Count > 0)
                    result.Error += "；恢复状态：" + string.Join("；", result.RecoveryMessages);
                else
                    result.Error += "；旧文件已恢复。";
                return result;
            }
        }

        /// <summary>
        /// One PDF replacement. Never delete an unrecovered old PDF or the produced
        /// temporary PDF on failure. A cleanup failure after successful commit is a
        /// warning, not a rollback. Can be fault-injected through IBatchFileSystem.
        /// </summary>
        internal static PdfFileCommitResult CommitPdf(
            string temporaryPath, string finalPath, bool overwrite, IBatchFileSystem fileSystem)
        {
            if (fileSystem == null) throw new ArgumentNullException(nameof(fileSystem));
            var result = new PdfFileCommitResult();
            if (string.IsNullOrWhiteSpace(temporaryPath) || string.IsNullOrWhiteSpace(finalPath))
            {
                result.Error = "PDF 暂存路径或目标路径为空，未替换文件。";
                return result;
            }
            // Resolve before any file operations and refuse accidentally identical paths.
            string source = fileSystem.GetFullPath(temporaryPath);
            string target = fileSystem.GetFullPath(finalPath);
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            {
                result.Error = "PDF 暂存文件与目标文件相同，已拒绝覆盖。";
                return result;
            }
            if (!fileSystem.FileExists(source))
            {
                result.Error = "PDF 暂存文件不存在，未修改已有文件：" + source;
                return result;
            }
            if (fileSystem.FileExists(target) && !overwrite)
            {
                result.Error = "目标 PDF 已存在且未允许覆盖：" + target;
                return result;
            }

            string backup = target + $".yangtools_backup_{Guid.NewGuid():N}";
            result.BackupPath = backup;
            bool movedOld = false;
            try
            {
                if (fileSystem.FileExists(target))
                {
                    fileSystem.MoveFile(target, backup);
                    movedOld = true;
                }
                fileSystem.MoveFile(source, target);
                if (!fileSystem.FileExists(target))
                    throw new IOException("文件移动未得到目标 PDF 读回确认。");
                result.Success = true;

                if (movedOld)
                {
                    try
                    {
                        fileSystem.DeleteFile(backup);
                        if (fileSystem.FileExists(backup))
                            throw new IOException("备份文件仍存在。");
                    }
                    catch (Exception cleanupError)
                    {
                        result.BackupRetained = true;
                        result.Warning = "PDF 已替换成功，但原文件备份清理失败，备份位置："
                            + backup + "；" + cleanupError.Message;
                    }
                }
                return result;
            }
            catch (Exception commitError)
            {
                // The move to target may have partially succeeded. Never overwrite or
                // remove an uncertain target because that could destroy recoverable data.
                if (movedOld)
                {
                    try
                    {
                        if (!fileSystem.FileExists(target) && fileSystem.FileExists(backup))
                            fileSystem.MoveFile(backup, target);
                        if (fileSystem.FileExists(backup))
                        {
                            result.BackupRetained = true;
                            result.Error = "PDF 替换失败，原文件备份仍保留：" + backup
                                + "；暂存文件：" + source + "；原因：" + commitError.Message;
                            return result;
                        }
                        if (!fileSystem.FileExists(target))
                            throw new IOException("原 PDF 文件恢复后仍不存在。");
                    }
                    catch (Exception recoveryError)
                    {
                        result.BackupRetained = true;
                        result.Error = "PDF 替换失败且原文件恢复状态未知，请勿重复覆盖。目标：" + target
                            + "；备份：" + backup + "；暂存：" + source
                            + "；替换错误：" + commitError.Message + "；恢复错误：" + recoveryError.Message;
                        return result;
                    }
                }
                result.Error = "PDF 替换未完成；暂存文件保留供检查：" + source
                    + "；目标：" + target + "；原因：" + commitError.Message;
                return result;
            }
        }

        private static bool IsReserved(string path, ISet<string>? reservations, IBatchFileSystem fs)
        {
            if (reservations == null || string.IsNullOrWhiteSpace(path)) return false;
            return reservations.Contains(Normalize(path, fs));
        }

        private static void AddReservation(string path, ISet<string>? reservations, IBatchFileSystem fs)
        {
            if (reservations == null || string.IsNullOrWhiteSpace(path)) return;
            reservations.Add(Normalize(path, fs));
        }

        private static string Normalize(string path, IBatchFileSystem fs)
        {
            try
            {
                return fs.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }

        private static void DeleteDirectoryBestEffort(string path, IBatchFileSystem fs)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && fs.DirectoryExists(path))
                    fs.DeleteDirectory(path, true);
            }
            catch
            {
                // 成功提交后的备份目录清理失败只会留下冗余备份，不允许反向破坏已提交输出。
            }
        }
    }
}
