using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;
using YangTools.Revit.Models.ParameterCleanup;

namespace YangTools.Revit.Core.ParameterCleanup
{
    internal sealed class ParameterMergeService
    {
        private readonly Document _document;
        private readonly Autodesk.Revit.ApplicationServices.Application _application;

        public ParameterMergeService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _application = document.Application;
        }

        public void MergeSelected(ScanResult result, MergeOptions options, Action<string>? log, ParameterValueIndex? prebuiltIndex = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            List<DuplicateGroupPlan> plans = result.Groups
                .Where(x => x.Selected && x.IsCompatible)
                .ToList();

            if (plans.Count == 0)
            {
                log?.Invoke("没有选中可处理的重复参数组。");
                return;
            }

            var totalTimer = Stopwatch.StartNew();
            ResetOperationState(plans, result);
            MergeBindings(plans, result, log);

            List<DuplicateGroupPlan> boundPlans = plans.Where(x => x.BindingMerged).ToList();
            if (boundPlans.Count == 0)
            {
                log?.Invoke("没有成功合并绑定的参数组，参数值未处理。");
                return;
            }

            ParameterValueIndex valueIndex = prebuiltIndex ??
                new ParameterValueIndexService(_document).Build(boundPlans, log);
            ApplyIndexSafetyFlags(boundPlans, valueIndex);
            result.Diagnostics["值索引扫描实例元素"] = valueIndex.ScannedInstanceElementCount.ToString();
            result.Diagnostics["值索引扫描类型元素"] = valueIndex.ScannedTypeElementCount.ToString();
            result.Diagnostics["值索引相关非空值"] = valueIndex.RelevantValueCount.ToString();
            result.Diagnostics["值索引全局参数关联"] = valueIndex.ParameterIdsWithGlobalAssociation.Count.ToString();
            result.Diagnostics["值索引读取异常参数"] = valueIndex.ParameterIdsWithReadErrors.Count.ToString();

            bool valuesCommitted = MergeValuesIndexed(boundPlans, result, valueIndex, log);
            if (!valuesCommitted)
            {
                foreach (DuplicateGroupPlan plan in boundPlans)
                {
                    plan.ValuesMerged = false;
                    plan.Status = "参数值事务未提交，禁止删除来源参数";
                    foreach (ParameterRecord source in plan.Sources)
                    {
                        source.HasWriteError = true;
                        source.DeleteBlockReason = "参数值事务未成功提交";
                    }
                }

                log?.Invoke("参数值事务未成功提交，本轮不会开放删除。");
                return;
            }

            EvaluateDeletionSafety(boundPlans, options);

            foreach (DuplicateGroupPlan plan in boundPlans)
            {
                plan.ValuesMerged = true;
                plan.Status =
                    $"已合并：复制 {plan.CopiedValueCount}，相同 {plan.EqualValueCount}，" +
                    $"冲突 {plan.ConflictCount}，错误 {plan.ErrorCount}";
            }

            log?.Invoke(
                $"参数值合并完成：复制 {boundPlans.Sum(x => x.CopiedValueCount)}，" +
                $"冲突 {result.TotalConflictCount}，错误 {boundPlans.Sum(x => x.ErrorCount)}；" +
                $"总耗时 {totalTimer.Elapsed.TotalSeconds:0.0} 秒。");

            if (result.ConflictDetailsTruncated)
            {
                log?.Invoke(
                    $"冲突明细超过 {result.MaxDetailedConflicts} 条。为避免内存和 CSV 过大，" +
                    $"只保留前 {result.MaxDetailedConflicts} 条明细；冲突总数仍完整统计。");
            }
        }

        private static void ResetOperationState(
            IEnumerable<DuplicateGroupPlan> plans,
            ScanResult result)
        {
            result.Conflicts.Clear();
            result.TotalConflictCount = 0;
            result.ConflictDetailsTruncated = false;
            result.Deletions.Clear();
            result.Diagnostics.Clear();
            result.Errors.RemoveAll(x =>
                x.Stage == "合并参数绑定" ||
                x.Stage == "复制参数值" ||
                x.Stage == "删除重复参数" ||
                x.Stage == "事务提交");

            foreach (DuplicateGroupPlan plan in plans)
            {
                plan.BindingMerged = false;
                plan.ValuesMerged = false;
                plan.CopiedValueCount = 0;
                plan.EqualValueCount = 0;
                plan.ConflictCount = 0;
                plan.ErrorCount = 0;
                plan.VisitedElementCount = 0;
                plan.Status = "等待合并";

                foreach (ParameterRecord record in plan.Records)
                {
                    record.HasWriteError = false;
                    record.HasBlockingConflict = false;
                    record.HasGlobalParameterAssociation = false;
                    record.Deleted = false;
                    record.DeleteBlockReason = string.Empty;
                }
            }
        }

        private void MergeBindings(
            IList<DuplicateGroupPlan> plans,
            ScanResult result,
            Action<string>? log)
        {
            log?.Invoke("正在合并保留参数的绑定类别……");
            var timer = Stopwatch.StartNew();
            var provisionalSuccess = new List<DuplicateGroupPlan>();
            int alreadyMatched = 0;
            int reinsertCount = 0;

            using (var transaction = new Transaction(_document, "合并重复项目参数绑定"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动参数绑定事务。");

                int processedPlans = 0;
                foreach (DuplicateGroupPlan plan in plans)
                {
                    processedPlans++;
                    if (processedPlans % 100 == 0)
                    {
                        log?.Invoke(
                            $"绑定处理进度：{processedPlans}/{plans.Count} 组，" +
                            $"无需 ReInsert {alreadyMatched}，实际 ReInsert {reinsertCount}……");
                    }

                    try
                    {
                        ParameterRecord keeper = plan.Keeper ??
                            throw new InvalidOperationException("当前参数组没有保留参数。");

                        CategorySet categorySet = _application.Create.NewCategorySet();
                        var mergedCategoryIds = new HashSet<long>();
                        foreach (ParameterRecord record in plan.Records)
                        {
                            foreach (Category category in record.Binding.Categories)
                            {
                                if (category == null || !mergedCategoryIds.Add(category.Id.GetIdValue()))
                                    continue;
                                categorySet.Insert(category);
                            }
                        }

                        // 已绑定类别完全一致时不调用 ReInsert。这是 600+ 重复组场景最有效的优化。
                        if (keeper.CategoryIds.SetEquals(mergedCategoryIds))
                        {
                            provisionalSuccess.Add(plan);
                            alreadyMatched++;
                            plan.Status = "绑定类别已一致，无需 ReInsert";
                            continue;
                        }

                        using (var subTransaction = new SubTransaction(_document))
                        {
                            try
                            {
                                if (subTransaction.Start() != TransactionStatus.Started)
                                    throw new InvalidOperationException("无法启动当前参数组的子事务。");

                                ElementBinding newBinding = plan.BindingKind == BindingKind.Instance
                                    ? (ElementBinding)_application.Create.NewInstanceBinding(categorySet)
                                    : _application.Create.NewTypeBinding(categorySet);

                                bool success = ParameterApiCompatibility.ReInsert(
                                    _document.ParameterBindings,
                                    keeper.Definition,
                                    newBinding);
                                if (!success)
                                    throw new InvalidOperationException("BindingMap.ReInsert 返回 False。");

                                if (subTransaction.Commit() != TransactionStatus.Committed)
                                    throw new InvalidOperationException("当前参数组的绑定子事务未提交。");

                                reinsertCount++;
                                provisionalSuccess.Add(plan);
                                plan.Status = "绑定已合并";
                            }
                            catch
                            {
                                if (subTransaction.GetStatus() == TransactionStatus.Started)
                                    subTransaction.RollBack();
                                throw;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        plan.BindingMerged = false;
                        plan.ErrorCount++;
                        plan.Status = "绑定合并失败";
                        foreach (ParameterRecord source in plan.Sources)
                        {
                            source.HasWriteError = true;
                            source.DeleteBlockReason = "参数绑定合并失败";
                        }

                        result.Errors.Add(new ErrorEntry
                        {
                            Stage = "合并参数绑定",
                            ParameterName = plan.Name,
                            ParameterId = plan.Keeper?.Id ?? 0,
                            Message = ex.Message
                        });
                    }
                }

                if (reinsertCount > 0)
                    _document.Regenerate();

                TransactionStatus commitStatus = transaction.Commit();
                if (commitStatus != TransactionStatus.Committed)
                {
                    foreach (DuplicateGroupPlan plan in provisionalSuccess)
                    {
                        plan.BindingMerged = false;
                        plan.Status = "绑定事务整体未提交";
                        foreach (ParameterRecord source in plan.Sources)
                        {
                            source.HasWriteError = true;
                            source.DeleteBlockReason = "绑定事务整体未成功提交";
                        }
                    }

                    result.Errors.Add(new ErrorEntry
                    {
                        Stage = "事务提交",
                        Message = "合并参数绑定事务状态：" + commitStatus
                    });
                    return;
                }
            }

            foreach (DuplicateGroupPlan plan in provisionalSuccess)
                plan.BindingMerged = true;

            log?.Invoke(
                $"绑定处理完成：成功 {provisionalSuccess.Count} 组（{alreadyMatched} 组无需 ReInsert，" +
                $"实际 ReInsert {reinsertCount} 组），失败 {plans.Count - provisionalSuccess.Count} 组；" +
                $"耗时 {timer.Elapsed.TotalSeconds:0.0} 秒。");
        }

        private bool MergeValuesIndexed(
            IList<DuplicateGroupPlan> plans,
            ScanResult result,
            ParameterValueIndex index,
            Action<string>? log)
        {
            log?.Invoke(
                "正在使用快速值索引引擎：复用清理前单次扫描结果，只处理真正存在来源值的元素……");
            var timer = Stopwatch.StartNew();

            var operationsByElementId = new Dictionary<long, List<SourceValueOperation>>();
            int operationCount = 0;
            foreach (DuplicateGroupPlan plan in plans)
            {
                foreach (ParameterRecord source in plan.Sources)
                {
                    foreach (long elementId in index.GetElementIds(source.Id))
                    {
                        if (!operationsByElementId.TryGetValue(elementId, out List<SourceValueOperation>? operations))
                        {
                            operations = new List<SourceValueOperation>();
                            operationsByElementId.Add(elementId, operations);
                        }

                        operations.Add(new SourceValueOperation(plan, source));
                        operationCount++;
                    }
                }
            }

            const int IdentityFailureAbortThreshold = 2000;
            int processedElements = 0;
            int processedOperations = 0;
            int parameterMapMisses = 0;
            int fallbackLookupHits = 0;
            int missingElements = 0;
            var targetIds = new HashSet<long>();
            var parametersById = new Dictionary<long, Parameter>();
            var touchedPlans = new HashSet<DuplicateGroupPlan>();

            using (var transaction = new Transaction(_document, "合并重复项目参数值"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动参数值事务。");

                foreach (KeyValuePair<long, List<SourceValueOperation>> pair in operationsByElementId)
                {
                    processedElements++;
                    Element? element;
                    try
                    {
                        element = _document.GetElement(ElementIdExtensions.CreateId(pair.Key));
                    }
                    catch
                    {
                        element = null;
                    }

                    if (element == null)
                    {
                        missingElements++;
                        foreach (SourceValueOperation operation in pair.Value)
                        {
                            MarkWriteError(
                                operation.Plan,
                                operation.Source,
                                pair.Key,
                                string.Empty,
                                string.Empty,
                                "值索引中的元素已不存在。",
                                result);
                        }
                        continue;
                    }

                    targetIds.Clear();
                    parametersById.Clear();
                    touchedPlans.Clear();
                    foreach (SourceValueOperation operation in pair.Value)
                    {
                        targetIds.Add(operation.Source.Id);
                        if (operation.Plan.Keeper != null)
                            targetIds.Add(operation.Plan.Keeper.Id);
                        touchedPlans.Add(operation.Plan);
                    }

                    try
                    {
                        foreach (Parameter parameter in element.Parameters)
                        {
                            if (parameter == null) continue;
                            long parameterId = parameter.Id.GetIdValue();
                            if (targetIds.Contains(parameterId))
                                parametersById[parameterId] = parameter;
                        }
                    }
                    catch (Exception ex)
                    {
                        foreach (SourceValueOperation operation in pair.Value)
                        {
                            MarkWriteError(
                                operation.Plan,
                                operation.Source,
                                element,
                                "读取元素参数失败：" + ex.Message,
                                result);
                        }
                        continue;
                    }

                    foreach (DuplicateGroupPlan touchedPlan in touchedPlans)
                        touchedPlan.VisitedElementCount++;

                    foreach (SourceValueOperation operation in pair.Value)
                    {
                        processedOperations++;
                        DuplicateGroupPlan plan = operation.Plan;
                        ParameterRecord source = operation.Source;
                        ParameterRecord? keeper = plan.Keeper;
                        if (keeper == null) continue;

                        parametersById.TryGetValue(source.Id, out Parameter? sourceParameter);
                        parametersById.TryGetValue(keeper.Id, out Parameter? keeperParameter);

                        if (sourceParameter == null)
                        {
                            sourceParameter = TryGetParameterByIdentity(element, source);
                            if (sourceParameter != null) fallbackLookupHits++;
                        }
                        if (keeperParameter == null)
                        {
                            keeperParameter = TryGetParameterByIdentity(element, keeper);
                            if (keeperParameter != null) fallbackLookupHits++;
                        }

                        if (sourceParameter == null || !ParameterValueUtil.HasMeaningfulValue(sourceParameter))
                        {
                            parameterMapMisses++;
                            MarkWriteError(
                                plan,
                                source,
                                element,
                                "值索引显示来源参数有值，但元素上无法按 ParameterElement ID 重新取得该参数。",
                                result);
                            continue;
                        }

                        if (ParameterApiCompatibility.HasGlobalParameterAssociation(sourceParameter))
                            source.HasGlobalParameterAssociation = true;

                        if (keeperParameter == null)
                        {
                            parameterMapMisses++;
                            MarkWriteError(
                                plan,
                                source,
                                element,
                                "元素上找不到保留参数。请检查绑定类别是否合并成功。",
                                result);
                            continue;
                        }

                        if (!ParameterValueUtil.HasMeaningfulValue(keeperParameter))
                        {
                            if (ParameterValueUtil.TrySetFrom(keeperParameter, sourceParameter, out string error))
                            {
                                plan.CopiedValueCount++;
                            }
                            else
                            {
                                MarkWriteError(plan, source, element, error, result);
                            }
                            continue;
                        }

                        if (ParameterValueUtil.AreEqual(keeperParameter, sourceParameter))
                        {
                            plan.EqualValueCount++;
                            continue;
                        }

                        source.HasBlockingConflict = true;
                        plan.ConflictCount++;
                        result.AddConflict(new ConflictEntry
                        {
                            ParameterName = plan.Name,
                            KeeperParameterId = keeper.Id,
                            SourceParameterId = source.Id,
                            ElementId = element.Id.GetIdValue(),
                            ElementUniqueId = SafeUniqueId(element),
                            CategoryName = SafeCategoryName(element),
                            KeeperValue = ParameterValueUtil.ToReportValue(keeperParameter),
                            SourceValue = ParameterValueUtil.ToReportValue(sourceParameter),
                            Decision = "保留参数已有值优先；来源值不同，默认禁止删除该来源参数。"
                        });
                    }

                    if (processedElements % 1000 == 0)
                    {
                        log?.Invoke(
                            $"快速值迁移：{processedElements}/{operationsByElementId.Count} 个相关元素，" +
                            $"复制 {plans.Sum(x => x.CopiedValueCount)}，冲突 {result.TotalConflictCount}，" +
                            $"错误 {plans.Sum(x => x.ErrorCount)}……");
                    }

                    // 若参数身份重取大面积失败，继续遍历只会产生数万条相同错误并让用户误以为卡死。
                    // 提前回滚并给出诊断，让后续版本可以针对具体 Revit/API 差异修复。
                    if (parameterMapMisses >= IdentityFailureAbortThreshold &&
                        parameterMapMisses * 2 >= Math.Max(1, processedOperations))
                    {
                        TransactionStatus rollbackStatus = transaction.RollBack();
                        string message =
                            $"参数身份访问连续大面积失败：已处理操作 {processedOperations}，重取失败 {parameterMapMisses}。" +
                            $"为避免继续无效运行，参数值事务已提前停止并回滚（{rollbackStatus}）。";
                        result.Errors.Add(new ErrorEntry
                        {
                            Stage = "复制参数值",
                            Message = message
                        });
                        result.Diagnostics["值引擎提前停止"] = "是";
                        result.Diagnostics["提前停止时已处理操作"] = processedOperations.ToString();
                        result.Diagnostics["提前停止时参数重取失败"] = parameterMapMisses.ToString();
                        log?.Invoke(message);
                        return false;
                    }
                }

                TransactionStatus commitStatus = transaction.Commit();
                if (commitStatus != TransactionStatus.Committed)
                {
                    result.Errors.Add(new ErrorEntry
                    {
                        Stage = "事务提交",
                        Message = "合并参数值事务状态：" + commitStatus
                    });
                    return false;
                }
            }

            result.Diagnostics["值引擎"] = "快速值索引";
            result.Diagnostics["值索引操作数"] = operationCount.ToString();
            result.Diagnostics["值迁移实际处理操作"] = processedOperations.ToString();
            result.Diagnostics["值引擎提前停止"] = "否";
            result.Diagnostics["相关元素数"] = operationsByElementId.Count.ToString();
            result.Diagnostics["元素缺失数"] = missingElements.ToString();
            result.Diagnostics["身份回退命中数"] = fallbackLookupHits.ToString();
            result.Diagnostics["参数重取失败数"] = parameterMapMisses.ToString();
            result.Diagnostics["值迁移耗时毫秒"] = timer.Elapsed.TotalMilliseconds.ToString("0.0");

            log?.Invoke(
                $"快速值索引引擎完成：索引操作 {operationCount}，相关元素 {operationsByElementId.Count}，" +
                $"元素缺失 {missingElements}，身份回退命中 {fallbackLookupHits}，参数重取失败 {parameterMapMisses}；" +
                $"耗时 {timer.Elapsed.TotalSeconds:0.0} 秒。");
            return true;
        }

        private static Parameter? TryGetParameterByIdentity(Element element, ParameterRecord record)
        {
            try
            {
                Parameter? parameter = null;
                if (record.IsShared && record.SharedGuid.HasValue)
                    parameter = element.get_Parameter(record.SharedGuid.Value);
                else
                    parameter = element.get_Parameter(record.Definition);

                if (parameter != null && parameter.Id.GetIdValue() == record.Id)
                    return parameter;
            }
            catch
            {
                // 最终仍以 ParameterElement ID 映射为准，禁止按名称模糊匹配。
            }

            return null;
        }

        private static void ApplyIndexSafetyFlags(
            IEnumerable<DuplicateGroupPlan> plans,
            ParameterValueIndex index)
        {
            foreach (DuplicateGroupPlan plan in plans)
            {
                foreach (ParameterRecord source in plan.Sources)
                {
                    if (index.ParameterIdsWithGlobalAssociation.Contains(source.Id))
                        source.HasGlobalParameterAssociation = true;

                    if (index.ParameterIdsWithReadErrors.Contains(source.Id))
                    {
                        source.HasWriteError = true;
                        source.DeleteBlockReason = "参数值索引读取不完整，禁止自动删除";
                    }
                }
            }
        }

        private static void MarkWriteError(
            DuplicateGroupPlan plan,
            ParameterRecord source,
            long elementId,
            string elementUniqueId,
            string categoryName,
            string message,
            ScanResult result)
        {
            source.HasWriteError = true;
            source.DeleteBlockReason = "存在参数值写入错误";
            plan.ErrorCount++;
            result.Errors.Add(new ErrorEntry
            {
                Stage = "复制参数值",
                ParameterName = plan.Name,
                ParameterId = source.Id,
                ElementId = elementId,
                ElementUniqueId = elementUniqueId,
                Message = message
            });
        }

        private sealed class SourceValueOperation
        {
            public SourceValueOperation(DuplicateGroupPlan plan, ParameterRecord source)
            {
                Plan = plan;
                Source = source;
            }

            public DuplicateGroupPlan Plan { get; }
            public ParameterRecord Source { get; }
        }

        private static void MarkWriteError(
            DuplicateGroupPlan plan,
            ParameterRecord source,
            Element element,
            string message,
            ScanResult result)
        {
            source.HasWriteError = true;
            source.DeleteBlockReason = "存在参数值写入错误";
            plan.ErrorCount++;
            result.Errors.Add(new ErrorEntry
            {
                Stage = "复制参数值",
                ParameterName = plan.Name,
                ParameterId = source.Id,
                ElementId = element.Id.GetIdValue(),
                ElementUniqueId = SafeUniqueId(element),
                Message = message
            });
        }

        private static void EvaluateDeletionSafety(
            IEnumerable<DuplicateGroupPlan> plans,
            MergeOptions options)
        {
            foreach (DuplicateGroupPlan plan in plans)
            {
                foreach (ParameterRecord source in plan.Sources)
                {
                    if (source.IsReferenced)
                    {
                        source.DeleteBlockReason = "参数仍被明细表或视图过滤器引用";
                        continue;
                    }

                    if (source.HasGlobalParameterAssociation)
                    {
                        source.DeleteBlockReason = "参数值仍关联全局参数";
                        continue;
                    }

                    if (source.IsShared && !options.AllowDeleteSharedParameters)
                    {
                        source.DeleteBlockReason = "共享参数默认受保护";
                        continue;
                    }

                    if (source.HasWriteError)
                    {
                        source.DeleteBlockReason = "存在参数值写入错误";
                        continue;
                    }

                    if (source.HasBlockingConflict && !options.DeleteSourcesWithConflicts)
                    {
                        source.DeleteBlockReason = "存在不同的非空参数值冲突";
                        continue;
                    }

                    // 不因未知引用全局阻断；删除阶段以 Document.Delete 返回的级联对象为最终门禁。
                    source.DeleteBlockReason = string.Empty;
                }
            }
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
