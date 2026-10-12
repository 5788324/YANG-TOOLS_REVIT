using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using YangTools.Revit.Models.ParameterCleanup;

namespace YangTools.Revit.Core.ParameterCleanup
{
    internal sealed class CsvReportWriter
    {
        private readonly Document _document;

        public CsvReportWriter(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public string WriteAll(ScanResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            string folder = EnsureReportFolder(result);
            WriteSummary(result, Path.Combine(folder, "00_汇总.csv"));
            WriteGroups(result, Path.Combine(folder, "01_重复参数组.csv"));
            WriteParameters(result, Path.Combine(folder, "02_参数明细.csv"));
            WriteReferences(result, Path.Combine(folder, "03_引用关系.csv"));
            WriteConflicts(result, Path.Combine(folder, "04_参数值冲突.csv"));
            WriteErrors(result, Path.Combine(folder, "05_错误.csv"));
            WriteDeletions(result, Path.Combine(folder, "06_删除记录.csv"));
            WriteDiagnostics(result, Path.Combine(folder, "10_运行诊断.csv"));
            return folder;
        }

        public void WriteVerification(
            ScanResult before,
            ScanResult after,
            int deletedCount)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));

            string folder = EnsureReportFolder(before);
            var rows = new List<string[]>
            {
                new[] { "项目参数总数", before.AllParameters.Count.ToString(CultureInfo.InvariantCulture), after.AllParameters.Count.ToString(CultureInfo.InvariantCulture) },
                new[] { "唯一参数名称数", before.DistinctNameCount.ToString(CultureInfo.InvariantCulture), after.DistinctNameCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "不重复参数数", before.NonDuplicateParameterCount.ToString(CultureInfo.InvariantCulture), after.NonDuplicateParameterCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "重复参数组数", before.Groups.Count.ToString(CultureInfo.InvariantCulture), after.Groups.Count.ToString(CultureInfo.InvariantCulture) },
                new[] { "重复组内参数总数", before.DuplicateParameterCount.ToString(CultureInfo.InvariantCulture), after.DuplicateParameterCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "本轮成功删除参数", "-", deletedCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "清理后是否仍有重复参数", "-", after.Groups.Count > 0 ? "是" : "否" }
            };
            WriteCsv(Path.Combine(folder, "07_清理后复核.csv"), new[] { "项目", "清理前", "清理后" }, rows);
        }


        public void WriteIntegrityVerification(
            ScanResult before,
            ScanResult after,
            ParameterCleanupVerificationResult verification)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));
            if (verification == null) throw new ArgumentNullException(nameof(verification));

            string folder = EnsureReportFolder(before);

            var valueRows = new List<string[]>
            {
                new[]
                {
                    "汇总", string.Empty, string.Empty, string.Empty,
                    $"快照值 {verification.SnapshotValueCount}",
                    $"通过 {verification.VerifiedValueCount}",
                    verification.ValueIntegrityPassed
                        ? "通过：清理前的不同非空内部值均仍存在"
                        : $"失败：快照读取错误 {verification.SnapshotReadErrorCount}，缺失 {verification.MissingValueCount}，元素缺失 {verification.MissingElementCount}，复核读取错误 {verification.ReadErrorCount}"
                }
            };
            valueRows.AddRange(verification.ValueEntries.Select(x => new[]
            {
                x.ParameterName,
                x.ElementId.ToString(CultureInfo.InvariantCulture),
                x.ElementUniqueId,
                x.CategoryName,
                x.ExpectedValue,
                x.CurrentValues,
                x.Result
            }));
            WriteCsv(
                Path.Combine(folder, "08_参数值完整性复核.csv"),
                new[] { "参数名称", "元素Id", "元素UniqueId", "类别", "清理前内部值", "清理后现有内部值", "复核结果" },
                valueRows);

            IEnumerable<string[]> groupRows = verification.GroupEntries.Select(x => new[]
            {
                x.ParameterName,
                x.BeforeCount.ToString(CultureInfo.InvariantCulture),
                x.AfterCount.ToString(CultureInfo.InvariantCulture),
                x.DeletedSourceCount.ToString(CultureInfo.InvariantCulture),
                x.ProtectedSourceCount.ToString(CultureInfo.InvariantCulture),
                x.ExpectedFullyCleaned ? "是" : "否",
                x.IsUniqueAfterCleanup ? "是" : "否",
                x.Result
            });
            WriteCsv(
                Path.Combine(folder, "09_已选参数唯一性复核.csv"),
                new[] { "参数名称", "清理前参数数", "清理后参数数", "成功删除来源数", "受保护或失败来源数", "应已完全清理", "清理后唯一", "复核结果" },
                groupRows);
        }

        public static void OpenFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;

            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }

        private string EnsureReportFolder(ScanResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.ReportFolder) && Directory.Exists(result.ReportFolder))
                return result.ReportFolder;

            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Revit参数清理报告");
            Directory.CreateDirectory(root);

            string modelName = SanitizeFileName(_document.Title);
            string folder = Path.Combine(
                root,
                modelName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            result.ReportFolder = folder;
            return folder;
        }

        private static void WriteSummary(ScanResult result, string path)
        {
            var rows = new List<string[]>
            {
                new[] { "项目参数总数", result.AllParameters.Count.ToString(CultureInfo.InvariantCulture) },
                new[] { "唯一参数名称数", result.DistinctNameCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "当前不重复参数数", result.NonDuplicateParameterCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "重复参数名称组数", result.Groups.Count.ToString(CultureInfo.InvariantCulture) },
                new[] { "重复组内参数总数", result.DuplicateParameterCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "理论可合并来源参数数", result.DuplicateSourceCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "可自动处理组数", result.CompatibleGroupCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "不兼容组数", result.IncompatibleGroupCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "冲突数量", result.TotalConflictCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "冲突明细是否截断", result.ConflictDetailsTruncated ? "是（仅保留前 " + result.MaxDetailedConflicts.ToString(CultureInfo.InvariantCulture) + " 条）" : "否" },
                new[] { "错误数量", result.Errors.Count.ToString(CultureInfo.InvariantCulture) },
                new[] { "已删除参数数量", result.Deletions.Count(x => x.Deleted).ToString(CultureInfo.InvariantCulture) },
                new[] { "视图过滤器引用扫描是否不完整", result.FilterReferenceScanIncomplete ? "是" : "否" }
            };
            WriteCsv(path, new[] { "项目", "结果" }, rows);
        }

        private static void WriteGroups(ScanResult result, string path)
        {
            IEnumerable<string[]> rows = result.Groups.Select(plan => new[]
            {
                plan.Selected ? "是" : "否",
                plan.Name,
                plan.IsCompatible ? "是" : "否",
                plan.IncompatibilityReason,
                plan.BindingText,
                plan.KeeperIdText,
                plan.SourceIdsText,
                plan.SharedSummary,
                plan.ReferenceSummary,
                plan.CopiedValueCount.ToString(CultureInfo.InvariantCulture),
                plan.ConflictCount.ToString(CultureInfo.InvariantCulture),
                plan.ErrorCount.ToString(CultureInfo.InvariantCulture),
                plan.Status
            });

            WriteCsv(path, new[]
            {
                "已选择", "参数名称", "兼容", "不兼容原因", "绑定方式", "保留参数Id", "来源参数Ids",
                "共享参数", "引用关系", "复制值", "冲突", "错误", "状态"
            }, rows);
        }

        private static void WriteParameters(ScanResult result, string path)
        {
            IEnumerable<string[]> rows = result.Groups.SelectMany(plan => plan.Records.Select(record => new[]
            {
                plan.Name,
                record.Id.ToString(CultureInfo.InvariantCulture),
                plan.Keeper != null && record.Id == plan.Keeper.Id ? "保留" : "来源",
                record.BindingKind.ToString(),
                record.DataTypeKey,
                record.GroupTypeKey,
                record.CategoryIds.Count.ToString(CultureInfo.InvariantCulture),
                record.IsShared ? "是" : "否",
                record.SharedGuidText,
                record.HasBlockingConflict ? "是" : "否",
                record.HasWriteError ? "是" : "否",
                record.HasGlobalParameterAssociation ? "是" : "否",
                record.Deleted ? "是" : "否",
                record.DeleteBlockReason
            }));

            WriteCsv(path, new[]
            {
                "参数名称", "参数Id", "角色", "绑定方式", "参数数据类型", "参数分组", "绑定类别数量",
                "共享参数", "共享参数Guid", "存在冲突", "写入错误", "全局参数关联", "已删除", "删除阻止原因"
            }, rows);
        }

        private static void WriteReferences(ScanResult result, string path)
        {
            var rows = new List<string[]>();
            foreach (ParameterRecord record in result.Groups.SelectMany(x => x.Records))
            {
                rows.AddRange(record.ScheduleReferences.Select(reference => new[]
                {
                    record.Name,
                    record.Id.ToString(CultureInfo.InvariantCulture),
                    "明细表",
                    reference
                }));
                rows.AddRange(record.FilterReferences.Select(reference => new[]
                {
                    record.Name,
                    record.Id.ToString(CultureInfo.InvariantCulture),
                    "视图过滤器",
                    reference
                }));
            }

            WriteCsv(path, new[] { "参数名称", "参数Id", "引用类型", "引用对象" }, rows);
        }

        private static void WriteConflicts(ScanResult result, string path)
        {
            IEnumerable<string[]> rows = result.Conflicts.Select(x => new[]
            {
                x.ParameterName,
                x.KeeperParameterId.ToString(CultureInfo.InvariantCulture),
                x.SourceParameterId.ToString(CultureInfo.InvariantCulture),
                x.ElementId.ToString(CultureInfo.InvariantCulture),
                x.ElementUniqueId,
                x.CategoryName,
                x.KeeperValue,
                x.SourceValue,
                x.Decision
            });

            WriteCsv(path, new[]
            {
                "参数名称", "保留参数Id", "来源参数Id", "元素Id", "元素UniqueId", "类别",
                "保留值", "来源值", "处理决定"
            }, rows);
        }

        private static void WriteErrors(ScanResult result, string path)
        {
            IEnumerable<string[]> rows = result.Errors.Select(x => new[]
            {
                x.Stage,
                x.ParameterName,
                x.ParameterId.ToString(CultureInfo.InvariantCulture),
                x.ElementId.HasValue ? x.ElementId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                x.ElementUniqueId,
                x.Message
            });

            WriteCsv(path, new[] { "阶段", "参数名称", "参数Id", "元素Id", "元素UniqueId", "错误" }, rows);
        }

        private static void WriteDeletions(ScanResult result, string path)
        {
            IEnumerable<string[]> rows = result.Deletions.Select(x => new[]
            {
                x.ParameterName,
                x.ParameterId.ToString(CultureInfo.InvariantCulture),
                x.Deleted ? "是" : "否",
                x.Reason
            });

            WriteCsv(path, new[] { "参数名称", "参数Id", "已删除", "结果" }, rows);
        }


        private static void WriteDiagnostics(ScanResult result, string path)
        {
            IEnumerable<string[]> rows = result.Diagnostics
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => new[] { x.Key, x.Value });
            WriteCsv(path, new[] { "诊断项目", "结果" }, rows);
        }

        private static void WriteCsv(string path, IEnumerable<string> headers, IEnumerable<string[]> rows)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine(string.Join(",", headers.Select(Escape)));
                foreach (string[] row in rows)
                    writer.WriteLine(string.Join(",", row.Select(Escape)));
            }
        }

        private static string Escape(string? value)
        {
            value ??= string.Empty;
            bool quote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            value = value.Replace("\"", "\"\"");
            return quote ? "\"" + value + "\"" : value;
        }

        private static string SanitizeFileName(string? value)
        {
            string result = value ?? "未命名项目";
            foreach (char c in Path.GetInvalidFileNameChars())
                result = result.Replace(c, '_');
            return string.IsNullOrWhiteSpace(result) ? "未命名项目" : result;
        }
    }
}
