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

                DeleteDirectoryBestEffort(backupFolder, fileSystem);
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
