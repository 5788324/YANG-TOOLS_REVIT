using System.Linq;
using System.Text;
using YangTools.Revit.Models.ParameterCleanup;

namespace YangTools.Revit.Core.ParameterCleanup
{
    internal static class ParameterCleanupPreviewBuilder
    {
        public static string BuildScanPreview(ScanResult result, MergeOptions options)
        {
            int selectedCompatibleGroups = result.Groups.Count(x => x.Selected && x.IsCompatible);
            int selectedSourceParameters = result.Groups
                .Where(x => x.Selected && x.IsCompatible)
                .Sum(x => x.SourceCount);
            int selectedSharedSources = result.Groups
                .Where(x => x.Selected && x.IsCompatible)
                .SelectMany(x => x.Sources)
                .Count(x => x.IsShared);
            var selectedPlans = result.Groups
                .Where(x => x.Selected && x.IsCompatible)
                .ToList();
            int selectedReferencedSources = selectedPlans
                .SelectMany(x => x.Sources)
                .Count(x => x.IsReferenced);

            // 理论值：所有选中兼容组都成功收敛为 1 个参数。
            int idealRemainingParameters = result.AllParameters.Count - selectedSourceParameters;
            int idealRemainingDuplicateGroups = result.Groups.Count - selectedCompatibleGroups;
            int idealNonDuplicateParameters = result.NonDuplicateParameterCount + selectedCompatibleGroups;

            // 扫描阶段能确认的已知保护项；参数值冲突、全局参数关联和级联删除要到执行阶段才能确定。
            bool IsKnownProtected(ParameterRecord source) =>
                source.IsReferenced || (!options.AllowDeleteSharedParameters && source.IsShared);

            int knownProtected = selectedPlans
                .SelectMany(x => x.Sources)
                .Count(IsKnownProtected);
            int knownProtectedGroups = selectedPlans
                .Count(x => x.Sources.Any(IsKnownProtected));

            int conservativeDeletable = System.Math.Max(0, selectedSourceParameters - knownProtected);
            int conservativeRemainingParameters = result.AllParameters.Count - conservativeDeletable;
            int conservativeRemainingDuplicateGroups = idealRemainingDuplicateGroups + knownProtectedGroups;
            int conservativeNonDuplicateParameters =
                result.NonDuplicateParameterCount + selectedCompatibleGroups - knownProtectedGroups;

            var sb = new StringBuilder();
            sb.AppendLine("──────── 扫描预览报告 ────────");
            sb.AppendLine($"当前项目参数：{result.AllParameters.Count} 个；唯一名称：{result.DistinctNameCount} 个。");
            sb.AppendLine($"当前不重复参数：{result.NonDuplicateParameterCount} 个；重复参数：{result.DuplicateParameterCount} 个，分为 {result.Groups.Count} 组。");
            sb.AppendLine($"可自动处理：{result.CompatibleGroupCount} 组；不兼容：{result.IncompatibleGroupCount} 组。");
            sb.AppendLine($"当前选中：{selectedCompatibleGroups} 组，包含 {selectedSourceParameters} 个待合并来源参数。");
            sb.AppendLine();
            sb.AppendLine("理论清理结果（所有选中组均成功合并并删除来源参数）：");
            sb.AppendLine($"  项目参数预计剩余 {idealRemainingParameters} 个；重复组预计剩余 {idealRemainingDuplicateGroups} 组；不重复参数预计 {idealNonDuplicateParameters} 个。");
            sb.AppendLine();
            sb.AppendLine("保守预估（仅按扫描阶段已知保护项计算）：");
            sb.AppendLine($"  已知受引用来源 {selectedReferencedSources} 个；共享来源 {selectedSharedSources} 个；已知受保护来源 {knownProtected} 个。");
            sb.AppendLine($"  预计至少可删除 {conservativeDeletable} 个；项目参数保守预计剩余约 {conservativeRemainingParameters} 个。");
            sb.AppendLine($"  已知至少仍会有 {conservativeRemainingDuplicateGroups} 组重复参数；预计最多形成 {conservativeNonDuplicateParameters} 个不重复参数。");
            sb.AppendLine();
            sb.AppendLine("说明：参数值冲突、全局参数关联和 Revit 级联删除只能在执行阶段确认，实际剩余重复组可能更多。完成后程序会自动重新扫描并输出实际复核结果。");
            return sb.ToString();
        }

        public static string BuildPostCleanupReport(
            ScanResult before,
            ScanResult after,
            int deletedCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine("──────── 清理后复核报告 ────────");
            sb.AppendLine($"项目参数：{before.AllParameters.Count} → {after.AllParameters.Count}，实际减少 {before.AllParameters.Count - after.AllParameters.Count} 个。");
            sb.AppendLine($"重复参数组：{before.Groups.Count} → {after.Groups.Count}，实际减少 {before.Groups.Count - after.Groups.Count} 组。");
            sb.AppendLine($"不重复参数：{before.NonDuplicateParameterCount} → {after.NonDuplicateParameterCount} 个。");
            sb.AppendLine($"唯一参数名称：{before.DistinctNameCount} → {after.DistinctNameCount} 个。");
            sb.AppendLine($"本轮删除记录：成功 {deletedCount} 个；受保护或回滚 {before.Deletions.Count(x => !x.Deleted)} 个。");
            sb.AppendLine($"清理后仍有重复参数：{(after.Groups.Count > 0 ? "是" : "否")}。剩余 {after.Groups.Count} 组，其中可处理 {after.CompatibleGroupCount} 组、不兼容 {after.IncompatibleGroupCount} 组。");
            if (after.Groups.Count > 0)
            {
                string names = string.Join("、", after.Groups.Take(8).Select(x => x.Name));
                sb.AppendLine("剩余示例：" + names + (after.Groups.Count > 8 ? "……" : string.Empty));
            }
            sb.AppendLine("完整明细已写入 CSV 报告目录。");
            return sb.ToString();
        }
    }
}
