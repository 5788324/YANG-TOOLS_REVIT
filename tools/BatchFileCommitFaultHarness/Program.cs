using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using YangTools.Revit.Core.BatchTasks;

internal static class Program
{
    private static int Main()
    {
        try
        {
            RunRestoreSuccessCase();
            RunRestoreFailureRetainsBackupCase();
            RunPdfReplaceSuccessCase();
            RunPdfMoveFailureRestoresOldCase();
            RunPdfRestoreFailureRetainsBackupCase();
            RunPdfCleanupFailureWarningCase();
            RunPdfOverwriteRefusedCase();
            Console.WriteLine("BatchFileCommitFaultHarness: PASS");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("BatchFileCommitFaultHarness: FAIL");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void RunRestoreSuccessCase()
    {
        using var fixture = new Fixture();
        fixture.SeedOldAndStaged();
        var fs = new ThrowingFileSystem(new PhysicalBatchFileSystem(), 4);
        BatchFileCommitResult result = BatchFileCommitService.Commit(
            fixture.Staging, fixture.Output, true, new HashSet<string>(StringComparer.OrdinalIgnoreCase), fs);

        Require(!result.Success, "second staged move must fail");
        Require(File.Exists(fixture.TargetA) && Hash(fixture.TargetA) == fixture.OldAHash, "old A must be restored");
        Require(File.Exists(fixture.TargetB) && Hash(fixture.TargetB) == fixture.OldBHash, "old B must be restored");
        Require(!result.BackupRetained, "fully restored run should not require retained backup");
    }

    private static void RunRestoreFailureRetainsBackupCase()
    {
        using var fixture = new Fixture();
        fixture.SeedOldAndStaged();
        // Move calls: 1-2 backup old files; 3 first staged output; 4 second staged output (fail);
        // recovery call 5 restores first old file (also fail). The remaining backup must survive.
        var fs = new ThrowingFileSystem(new PhysicalBatchFileSystem(), 4, 5);
        BatchFileCommitResult result = BatchFileCommitService.Commit(
            fixture.Staging, fixture.Output, true, new HashSet<string>(StringComparer.OrdinalIgnoreCase), fs);

        Require(!result.Success, "fault-injected commit must fail");
        Require(result.BackupRetained, "restore failure must retain backup directory");
        Require(Directory.Exists(result.BackupFolder), "retained backup directory must exist");
        Require(Directory.EnumerateFiles(result.BackupFolder).Any(), "retained backup directory must contain recoverable data");
        Require(result.RecoveryMessages.Any(m => m.Contains("备份", StringComparison.Ordinal)), "result must report backup recovery information");
    }

    private static void RunPdfReplaceSuccessCase()
    {
        using var fixture = new Fixture();
        string temp = Path.Combine(fixture.Staging, "new.pdf");
        string target = Path.Combine(fixture.Output, "drawing.pdf");
        Directory.CreateDirectory(fixture.Staging);
        Directory.CreateDirectory(fixture.Output);
        File.WriteAllText(temp, "NEW-PDF");
        File.WriteAllText(target, "OLD-PDF");
        PdfFileCommitResult result = BatchFileCommitService.CommitPdf(temp, target, true, new PhysicalBatchFileSystem());
        Require(result.Success && File.ReadAllText(target) == "NEW-PDF", "PDF should contain new content");
        Require(!result.BackupRetained && !File.Exists(result.BackupPath), "old PDF backup should be removed");
    }

    private static void RunPdfMoveFailureRestoresOldCase()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(fixture.Staging);
        Directory.CreateDirectory(fixture.Output);
        string temp = Path.Combine(fixture.Staging, "new.pdf");
        string target = Path.Combine(fixture.Output, "drawing.pdf");
        File.WriteAllText(temp, "NEW-PDF");
        File.WriteAllText(target, "OLD-PDF");
        var fs = new ThrowingFileSystem(new PhysicalBatchFileSystem(), 2);
        PdfFileCommitResult result = BatchFileCommitService.CommitPdf(temp, target, true, fs);
        Require(!result.Success, "second move should fail");
        Require(File.ReadAllText(target) == "OLD-PDF", "original PDF must be restored");
        Require(File.ReadAllText(temp) == "NEW-PDF", "uncommitted PDF must remain recoverable");
    }

    private static void RunPdfRestoreFailureRetainsBackupCase()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(fixture.Staging);
        Directory.CreateDirectory(fixture.Output);
        string temp = Path.Combine(fixture.Staging, "new.pdf");
        string target = Path.Combine(fixture.Output, "drawing.pdf");
        File.WriteAllText(temp, "NEW-PDF");
        File.WriteAllText(target, "OLD-PDF");
        var fs = new ThrowingFileSystem(new PhysicalBatchFileSystem(), 2, 3);
        PdfFileCommitResult result = BatchFileCommitService.CommitPdf(temp, target, true, fs);
        Require(!result.Success && result.BackupRetained, "restore failure should retain original backup");
        Require(File.ReadAllText(result.BackupPath) == "OLD-PDF", "old PDF must remain in backup");
        Require(result.Error.Contains("备份", StringComparison.Ordinal), "recoverability must be explicit");
    }

    private static void RunPdfCleanupFailureWarningCase()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(fixture.Staging);
        Directory.CreateDirectory(fixture.Output);
        string temp = Path.Combine(fixture.Staging, "new.pdf");
        string target = Path.Combine(fixture.Output, "drawing.pdf");
        File.WriteAllText(temp, "NEW-PDF");
        File.WriteAllText(target, "OLD-PDF");
        var fs = new ThrowingDeleteFileSystem(new PhysicalBatchFileSystem());
        PdfFileCommitResult result = BatchFileCommitService.CommitPdf(temp, target, true, fs);
        Require(result.Success && result.BackupRetained, "cleanup failure must not undo new PDF");
        Require(File.ReadAllText(target) == "NEW-PDF", "committed new PDF must remain");
        Require(File.ReadAllText(result.BackupPath) == "OLD-PDF", "retained backup must be identifiable");
        Require(result.Warning.Contains("备份", StringComparison.Ordinal), "show cleanup warning");
    }

    private static void RunPdfOverwriteRefusedCase()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(fixture.Staging);
        Directory.CreateDirectory(fixture.Output);
        string temp = Path.Combine(fixture.Staging, "new.pdf");
        string target = Path.Combine(fixture.Output, "drawing.pdf");
        File.WriteAllText(temp, "NEW-PDF");
        File.WriteAllText(target, "OLD-PDF");
        PdfFileCommitResult result = BatchFileCommitService.CommitPdf(temp, target, false, new PhysicalBatchFileSystem());
        Require(!result.Success && File.ReadAllText(target) == "OLD-PDF", "existing PDF must be preserved");
        Require(File.ReadAllText(temp) == "NEW-PDF", "uncommitted PDF must remain");
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ThrowingFileSystem : IBatchFileSystem
    {
        private readonly IBatchFileSystem _inner;
        private readonly HashSet<int> _failMoveCalls;
        private int _moveCall;

        public ThrowingFileSystem(IBatchFileSystem inner, params int[] failMoveCalls)
        {
            _inner = inner;
            _failMoveCalls = new HashSet<int>(failMoveCalls);
        }

        public bool FileExists(string path) => _inner.FileExists(path);
        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
        public IEnumerable<string> EnumerateFiles(string path) => _inner.EnumerateFiles(path);
        public void CreateDirectory(string path) => _inner.CreateDirectory(path);
        public void DeleteFile(string path) => _inner.DeleteFile(path);
        public void DeleteDirectory(string path, bool recursive) => _inner.DeleteDirectory(path, recursive);
        public string GetFullPath(string path) => _inner.GetFullPath(path);

        public void MoveFile(string source, string target)
        {
            _moveCall++;
            if (_failMoveCalls.Contains(_moveCall))
                throw new IOException($"Injected MoveFile failure #{_moveCall}: {source} -> {target}");
            _inner.MoveFile(source, target);
        }
    }

    private sealed class ThrowingDeleteFileSystem : IBatchFileSystem
    {
        private readonly IBatchFileSystem _inner;
        internal ThrowingDeleteFileSystem(IBatchFileSystem inner) => _inner = inner;
        public bool FileExists(string path) => _inner.FileExists(path);
        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
        public IEnumerable<string> EnumerateFiles(string path) => _inner.EnumerateFiles(path);
        public void CreateDirectory(string path) => _inner.CreateDirectory(path);
        public void MoveFile(string source, string target) => _inner.MoveFile(source, target);
        public void DeleteFile(string path) => throw new IOException("Injected backup cleanup failure");
        public void DeleteDirectory(string path, bool recursive) => _inner.DeleteDirectory(path, recursive);
        public string GetFullPath(string path) => _inner.GetFullPath(path);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "YangToolsBatchCommitHarness_" + Guid.NewGuid().ToString("N"));
        public string Output => Path.Combine(Root, "output");
        public string Staging => Path.Combine(Root, "staging");
        public string TargetA => Path.Combine(Output, "A.dwg");
        public string TargetB => Path.Combine(Output, "B.pcp");
        public string OldAHash { get; private set; } = string.Empty;
        public string OldBHash { get; private set; } = string.Empty;

        public void SeedOldAndStaged()
        {
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory(Staging);
            File.WriteAllText(TargetA, "OLD-A");
            File.WriteAllText(TargetB, "OLD-B");
            OldAHash = Hash(TargetA);
            OldBHash = Hash(TargetB);
            File.WriteAllText(Path.Combine(Staging, "A.dwg"), "NEW-A");
            File.WriteAllText(Path.Combine(Staging, "B.pcp"), "NEW-B");
        }

        public void Dispose()
        {
            try { if (Directory.Exists(Root)) Directory.Delete(Root, true); } catch { }
        }
    }
}
