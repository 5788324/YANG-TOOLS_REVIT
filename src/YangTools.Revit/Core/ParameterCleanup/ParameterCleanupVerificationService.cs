using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;
using YangTools.Revit.Models.ParameterCleanup;

namespace YangTools.Revit.Core.ParameterCleanup
{
    /// <summary>
    /// 清理前后数据完整性复核。
    /// 规则：
    /// 1. 清理前元素上同名参数的每一个不同非空内部值，清理后必须仍存在；
    /// 2. 已报告全部来源参数删除成功的参数组，清理后必须只剩一个 ParameterElement。
    /// 该服务只读取 Revit 内部值，不使用 AsValueString 参与比较。
    /// </summary>
    internal sealed class ParameterCleanupVerificationService
    {
        private const int MaxDetailedEntries = 10000;
        private readonly Document _document;

        public ParameterCleanupVerificationService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public ParameterCleanupSnapshot CaptureBefore(
            ScanResult before,
            Action<string>? log)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));

            List<DuplicateGroupPlan> plans = before.Groups
                .Where(x => x.Selected && x.IsCompatible)
                .ToList();

            var snapshot = new ParameterCleanupSnapshot
            {
                SelectedGroupCount = plans.Count,
                SelectedSourceCount = plans.Sum(x => x.SourceCount),
                ValueIndex = new ParameterValueIndex()
            };

            foreach (string name in plans
                         .Select(x => x.Records.FirstOrDefault()?.NormalizedName ?? x.Name.Trim())
                         .Distinct(StringComparer.Ordinal))
            {
                snapshot.SelectedNormalizedNames.Add(name);
            }

            if (plans.Count == 0) return snapshot;

            var timer = Stopwatch.StartNew();
            CaptureCollector(plans.Where(x => x.BindingKind == BindingKind.Instance), false, snapshot, log);
            CaptureCollector(plans.Where(x => x.BindingKind == BindingKind.Type), true, snapshot, log);

            log?.Invoke(
                $"清理前参数值快照完成：{snapshot.ElementValues.Count} 个元素-参数组，" +
                $"{snapshot.DistinctStoredValueCount} 个不同非空内部值，读取错误 {snapshot.CaptureReadErrorCount}；耗时 {timer.Elapsed.TotalSeconds:0.0} 秒。");
            return snapshot;
        }

        public ParameterCleanupVerificationResult VerifyAfter(
            ParameterCleanupSnapshot snapshot,
            ScanResult before,
            ScanResult after,
            Action<string>? log)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));

            var timer = Stopwatch.StartNew();
            var result = new ParameterCleanupVerificationResult
            {
                SnapshotElementCount = snapshot.ElementValues.Count,
                SnapshotValueCount = snapshot.DistinctStoredValueCount,
                SnapshotReadErrorCount = snapshot.CaptureReadErrorCount,
                SelectedGroupCount = snapshot.SelectedGroupCount
            };

            Dictionary<string, HashSet<long>> afterIdsByName = after.AllParameters
                .GroupBy(x => x.NormalizedName, StringComparer.Ordinal)
                .ToDictionary(
                    x => x.Key,
                    x => new HashSet<long>(x.Select(y => y.Id)),
                    StringComparer.Ordinal);

            VerifyValues(snapshot, afterIdsByName, result, log);
            VerifyUniqueness(before, after, result);

            log?.Invoke(BuildSummary(result));
            log?.Invoke($"清理后完整性复核耗时 {timer.Elapsed.TotalSeconds:0.0} 秒。");
            return result;
        }

        public static string BuildSummary(ParameterCleanupVerificationResult result)
        {
            if (result == null) return string.Empty;

            string valueStatus = result.ValueIntegrityPassed ? "通过" : "失败";
            string uniqueStatus = result.FullyCleanedGroupsUniquePassed ? "通过" : "失败";
            string allSelectedStatus = result.RemainingDuplicateGroupCount == 0 ? "全部唯一" : "仍有未完全清理组";

            return
                "──────── 数据完整性复核 ────────\n" +
                $"元素参数值：{valueStatus}；快照 {result.SnapshotValueCount} 个不同非空内部值，" +
                $"验证通过 {result.VerifiedValueCount}，缺失 {result.MissingValueCount}，快照读取错误 {result.SnapshotReadErrorCount}，" +
                $"元素缺失 {result.MissingElementCount}，读取错误 {result.ReadErrorCount}。\n" +
                $"已完成删除组唯一性：{uniqueStatus}；完全清理 {result.FullyCleanedGroupCount} 组，" +
                $"异常 {result.FullyCleanedUniquenessFailureCount} 组。\n" +
                $"选中组最终状态：{allSelectedStatus}；唯一 {result.UniqueGroupCount} 组，" +
                $"仍重复 {result.RemainingDuplicateGroupCount} 组。";
        }

        private void CaptureCollector(
            IEnumerable<DuplicateGroupPlan> sourcePlans,
            bool elementTypes,
            ParameterCleanupSnapshot snapshot,
            Action<string>? log)
        {
            List<DuplicateGroupPlan> plans = sourcePlans.ToList();
            if (plans.Count == 0) return;

            var planByParameterId = new Dictionary<long, DuplicateGroupPlan>();
            var targetCategoryIds = new HashSet<long>();
            foreach (DuplicateGroupPlan plan in plans)
            {
                foreach (ParameterRecord record in plan.Records)
                {
                    planByParameterId[record.Id] = plan;
                    snapshot.ValueIndex?.GetElementIds(record.Id);
                    foreach (long categoryId in record.CategoryIds)
                        targetCategoryIds.Add(categoryId);
                }
            }

            FilteredElementCollector collector = new FilteredElementCollector(_document);
            collector = elementTypes
                ? collector.WhereElementIsElementType()
                : collector.WhereElementIsNotElementType();

            int visited = 0;
            ParameterValueIndex? valueIndex = snapshot.ValueIndex;
            var valuesByPlan = new Dictionary<DuplicateGroupPlan, List<ParameterStoredValue>>();

            foreach (Element element in collector)
            {
                visited++;
                if (visited % 5000 == 0)
                {
                    log?.Invoke(
                        $"快照/值索引扫描{(elementTypes ? "元素类型" : "实例元素")}：" +
                        $"已检查 {visited} 个元素，已记录 {snapshot.ElementValues.Count} 个元素-参数组……");
                }

                long categoryId = long.MinValue;
                Category? category;
                try
                {
                    category = element.Category;
                    if (category == null) continue;
                    categoryId = category.Id.GetIdValue();
                }
                catch { continue; }
                if (!targetCategoryIds.Contains(categoryId)) continue;

                valuesByPlan.Clear();
                try
                {
                    foreach (Parameter parameter in element.Parameters)
                    {
                        if (parameter == null) continue;
                        long parameterId = parameter.Id.GetIdValue();
                        if (!planByParameterId.TryGetValue(parameterId, out DuplicateGroupPlan? plan)) continue;

                        if (ParameterApiCompatibility.HasGlobalParameterAssociation(parameter))
                            valueIndex?.ParameterIdsWithGlobalAssociation.Add(parameterId);

                        if (!ParameterValueUtil.HasMeaningfulValue(parameter)) continue;

                        if (valueIndex != null && valueIndex.GetElementIds(parameterId).Add(element.Id.GetIdValue()))
                            valueIndex.RelevantValueCount++;

                        if (!TryCaptureValue(parameter, out ParameterStoredValue value))
                        {
                            snapshot.CaptureReadErrorCount++;
                            valueIndex?.ParameterIdsWithReadErrors.Add(parameterId);
                            continue;
                        }

                        if (!valuesByPlan.TryGetValue(plan, out List<ParameterStoredValue>? values))
                        {
                            values = new List<ParameterStoredValue>();
                            valuesByPlan[plan] = values;
                        }

                        if (!values.Any(x => StoredValuesEqual(x, value)))
                            values.Add(value);
                    }
                }
                catch
                {
                    snapshot.CaptureReadErrorCount++;
                    if (valueIndex != null)
                    {
                        foreach (KeyValuePair<long, DuplicateGroupPlan> pair in planByParameterId)
                        {
                            if (pair.Value.Records.Any(x => x.CategoryIds.Contains(categoryId)))
                                valueIndex.ParameterIdsWithReadErrors.Add(pair.Key);
                        }
                    }
                    continue;
                }

                foreach (KeyValuePair<DuplicateGroupPlan, List<ParameterStoredValue>> pair in valuesByPlan)
                {
                    if (pair.Value.Count == 0) continue;
                    ParameterRecord? first = pair.Key.Records.FirstOrDefault();
                    var entry = new ParameterElementValueSnapshot
                    {
                        ParameterName = pair.Key.Name,
                        NormalizedName = first?.NormalizedName ?? pair.Key.Name.Trim(),
                        ElementId = element.Id.GetIdValue(),
                        ElementUniqueId = SafeUniqueId(element),
                        CategoryName = SafeCategoryName(element)
                    };
                    entry.Values.AddRange(pair.Value);
                    snapshot.ElementValues.Add(entry);
                }
            }

            if (valueIndex != null)
            {
                if (elementTypes) valueIndex.ScannedTypeElementCount += visited;
                else valueIndex.ScannedInstanceElementCount += visited;
            }

            log?.Invoke(
                $"快照/值索引扫描{(elementTypes ? "元素类型" : "实例元素")}：检查 {visited} 个元素。" );
        }

        private void VerifyValues(
            ParameterCleanupSnapshot snapshot,
            IReadOnlyDictionary<string, HashSet<long>> afterIdsByName,
            ParameterCleanupVerificationResult result,
            Action<string>? log)
        {
            int checkedEntries = 0;
            foreach (ParameterElementValueSnapshot entry in snapshot.ElementValues)
            {
                checkedEntries++;
                if (checkedEntries % 5000 == 0)
                {
                    log?.Invoke(
                        $"参数值完整性复核：{checkedEntries}/{snapshot.ElementValues.Count} 个元素-参数组，" +
                        $"已验证 {result.VerifiedValueCount}，缺失 {result.MissingValueCount}……");
                }
                Element? element;
                try { element = _document.GetElement(ElementIdExtensions.CreateId(entry.ElementId)); }
                catch { element = null; }

                if (element == null)
                {
                    result.MissingElementCount++;
                    result.MissingValueCount += entry.Values.Count;
                    AddValueEntry(result, entry, string.Join(" | ", entry.Values.Select(x => x.ReportValue)), string.Empty, "元素不存在");
                    continue;
                }

                if (!afterIdsByName.TryGetValue(entry.NormalizedName, out HashSet<long>? currentParameterIds))
                {
                    result.MissingValueCount += entry.Values.Count;
                    AddValueEntry(result, entry, string.Join(" | ", entry.Values.Select(x => x.ReportValue)), string.Empty, "清理后找不到同名项目参数");
                    continue;
                }

                var currentValues = new List<ParameterStoredValue>();
                try
                {
                    foreach (Parameter parameter in element.Parameters)
                    {
                        if (parameter == null || !currentParameterIds.Contains(parameter.Id.GetIdValue())) continue;
                        if (!ParameterValueUtil.HasMeaningfulValue(parameter)) continue;
                        if (!TryCaptureValue(parameter, out ParameterStoredValue currentValue))
                        {
                            result.ReadErrorCount++;
                            continue;
                        }
                        if (!currentValues.Any(x => StoredValuesEqual(x, currentValue)))
                            currentValues.Add(currentValue);
                    }
                }
                catch (Exception ex)
                {
                    result.ReadErrorCount++;
                    result.MissingValueCount += entry.Values.Count;
                    AddValueEntry(result, entry, string.Join(" | ", entry.Values.Select(x => x.ReportValue)), string.Empty, "读取失败：" + ex.Message);
                    continue;
                }

                foreach (ParameterStoredValue expected in entry.Values)
                {
                    if (currentValues.Any(x => StoredValuesEqual(x, expected)))
                    {
                        result.VerifiedValueCount++;
                        continue;
                    }

                    result.MissingValueCount++;
                    AddValueEntry(
                        result,
                        entry,
                        expected.ReportValue,
                        string.Join(" | ", currentValues.Select(x => x.ReportValue)),
                        "原有非空内部值在清理后不存在");
                }
            }
        }

        private static void VerifyUniqueness(
            ScanResult before,
            ScanResult after,
            ParameterCleanupVerificationResult result)
        {
            Dictionary<string, int> afterCountByName = after.AllParameters
                .GroupBy(x => x.NormalizedName, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);

            foreach (DuplicateGroupPlan plan in before.Groups.Where(x => x.Selected && x.IsCompatible))
            {
                string normalizedName = plan.Records.FirstOrDefault()?.NormalizedName ?? plan.Name.Trim();
                int afterCount = afterCountByName.TryGetValue(normalizedName, out int count) ? count : 0;
                int deletedSources = plan.Sources.Count(x => x.Deleted);
                int protectedSources = plan.SourceCount - deletedSources;
                bool expectedFullyCleaned = plan.SourceCount > 0 && deletedSources == plan.SourceCount;
                bool unique = afterCount == 1;

                if (unique) result.UniqueGroupCount++;
                if (afterCount > 1) result.RemainingDuplicateGroupCount++;
                if (expectedFullyCleaned)
                {
                    result.FullyCleanedGroupCount++;
                    if (!unique) result.FullyCleanedUniquenessFailureCount++;
                }

                result.GroupEntries.Add(new ParameterGroupVerificationEntry
                {
                    ParameterName = plan.Name,
                    BeforeCount = plan.Records.Count,
                    AfterCount = afterCount,
                    DeletedSourceCount = deletedSources,
                    ProtectedSourceCount = protectedSources,
                    ExpectedFullyCleaned = expectedFullyCleaned,
                    IsUniqueAfterCleanup = unique,
                    Result = unique
                        ? "清理后唯一"
                        : expectedFullyCleaned
                            ? "异常：来源均报告已删除，但清理后不是唯一参数"
                            : "仍有受保护/失败来源参数，未完全清理"
                });
            }
        }

        private static bool TryCaptureValue(Parameter parameter, out ParameterStoredValue value)
        {
            value = new ParameterStoredValue { StorageType = parameter.StorageType };
            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String:
                        value.StringValue = parameter.AsString() ?? string.Empty;
                        break;
                    case StorageType.Integer:
                        value.IntegerValue = parameter.AsInteger();
                        break;
                    case StorageType.Double:
                        value.DoubleValue = parameter.AsDouble();
                        break;
                    case StorageType.ElementId:
                        ElementId id = parameter.AsElementId();
                        value.ElementIdValue = id == null ? long.MinValue : id.GetIdValue();
                        break;
                    default:
                        return false;
                }

                value.ReportValue = ParameterValueUtil.ToReportValue(parameter);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool StoredValuesEqual(ParameterStoredValue left, ParameterStoredValue right)
        {
            if (left.StorageType != right.StorageType) return false;
            switch (left.StorageType)
            {
                case StorageType.String:
                    return string.Equals(left.StringValue, right.StringValue, StringComparison.Ordinal);
                case StorageType.Integer:
                    return left.IntegerValue == right.IntegerValue;
                case StorageType.Double:
                    return Math.Abs(left.DoubleValue - right.DoubleValue) <= 1e-9;
                case StorageType.ElementId:
                    return left.ElementIdValue == right.ElementIdValue;
                default:
                    return false;
            }
        }

        private static void AddValueEntry(
            ParameterCleanupVerificationResult result,
            ParameterElementValueSnapshot snapshot,
            string expected,
            string current,
            string message)
        {
            if (result.ValueEntries.Count >= MaxDetailedEntries) return;
            result.ValueEntries.Add(new ParameterValueVerificationEntry
            {
                ParameterName = snapshot.ParameterName,
                ElementId = snapshot.ElementId,
                ElementUniqueId = snapshot.ElementUniqueId,
                CategoryName = snapshot.CategoryName,
                ExpectedValue = expected,
                CurrentValues = current,
                Result = message
            });
        }

        private static string SafeUniqueId(Element element)
        {
            try { return element.UniqueId ?? string.Empty; }
            catch { return string.Empty; }
        }

        private static string SafeCategoryName(Element element)
        {
            try { return element.Category?.Name ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
