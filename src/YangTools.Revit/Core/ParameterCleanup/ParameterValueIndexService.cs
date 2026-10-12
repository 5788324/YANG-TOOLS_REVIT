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
    /// 为选中的重复项目参数建立“参数 Id -> 有值元素 Id”索引。
    /// 该索引只扫描实例元素一次、元素类型一次，供快速值迁移复用，
    /// 避免每个参数组重复创建 FilteredElementCollector。
    /// </summary>
    internal sealed class ParameterValueIndexService
    {
        private readonly Document _document;

        public ParameterValueIndexService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public ParameterValueIndex Build(
            IEnumerable<DuplicateGroupPlan> sourcePlans,
            Action<string>? log)
        {
            List<DuplicateGroupPlan> plans = sourcePlans
                .Where(x => x.IsCompatible)
                .ToList();
            var index = new ParameterValueIndex();
            if (plans.Count == 0) return index;

            var timer = Stopwatch.StartNew();
            BuildCollector(
                plans.Where(x => x.BindingKind == BindingKind.Instance).ToList(),
                elementTypes: false,
                index,
                log);
            BuildCollector(
                plans.Where(x => x.BindingKind == BindingKind.Type).ToList(),
                elementTypes: true,
                index,
                log);

            log?.Invoke(
                $"参数值索引完成：实例 {index.ScannedInstanceElementCount}，类型 {index.ScannedTypeElementCount}，" +
                $"相关非空参数值 {index.RelevantValueCount}，读取异常参数 {index.ParameterIdsWithReadErrors.Count}；" +
                $"耗时 {timer.Elapsed.TotalSeconds:0.0} 秒。");
            return index;
        }

        private void BuildCollector(
            IList<DuplicateGroupPlan> plans,
            bool elementTypes,
            ParameterValueIndex index,
            Action<string>? log)
        {
            if (plans.Count == 0) return;

            var recordByParameterId = new Dictionary<long, ParameterRecord>();
            var targetCategoryIds = new HashSet<long>();
            foreach (DuplicateGroupPlan plan in plans)
            {
                foreach (ParameterRecord record in plan.Records)
                {
                    recordByParameterId[record.Id] = record;
                    index.GetElementIds(record.Id);
                    foreach (long categoryId in record.CategoryIds)
                        targetCategoryIds.Add(categoryId);
                }
            }

            FilteredElementCollector collector = new FilteredElementCollector(_document);
            collector = elementTypes
                ? collector.WhereElementIsElementType()
                : collector.WhereElementIsNotElementType();

            int visited = 0;
            int matchedCategories = 0;
            foreach (Element element in collector)
            {
                visited++;
                if (visited % 5000 == 0)
                {
                    log?.Invoke(
                        $"值索引{(elementTypes ? "元素类型" : "实例元素")}：已检查 {visited}，" +
                        $"相关类别 {matchedCategories}，相关非空值 {index.RelevantValueCount}……");
                }

                long categoryId = long.MinValue;
                try
                {
                    Category? category = element.Category;
                    if (category == null) continue;
                    categoryId = category.Id.GetIdValue();
                    if (!targetCategoryIds.Contains(categoryId))
                        continue;
                    matchedCategories++;

                    foreach (Parameter parameter in element.Parameters)
                    {
                        if (parameter == null) continue;
                        long parameterId = parameter.Id.GetIdValue();
                        if (!recordByParameterId.TryGetValue(parameterId, out ParameterRecord? record))
                            continue;

                        if (ParameterApiCompatibility.HasGlobalParameterAssociation(parameter))
                            index.ParameterIdsWithGlobalAssociation.Add(parameterId);

                        if (!ParameterValueUtil.HasMeaningfulValue(parameter))
                            continue;

                        if (index.GetElementIds(parameterId).Add(element.Id.GetIdValue()))
                            index.RelevantValueCount++;
                    }
                }
                catch
                {
                    // 只标记当前相关类别中的参数，后续将阻止它们自动删除。
                    foreach (ParameterRecord record in recordByParameterId.Values)
                    {
                        if (categoryId == long.MinValue || record.CategoryIds.Contains(categoryId))
                            index.ParameterIdsWithReadErrors.Add(record.Id);
                    }
                }

            }

            if (elementTypes) index.ScannedTypeElementCount += visited;
            else index.ScannedInstanceElementCount += visited;

            log?.Invoke(
                $"值索引{(elementTypes ? "元素类型" : "实例元素")}完成：检查 {visited}，" +
                $"相关类别 {matchedCategories}。");
        }
    }
}
