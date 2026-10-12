using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Models.ParameterCleanup;

namespace YangTools.Revit.Core.ParameterCleanup
{
    internal sealed class ParameterDeleteService
    {
        // 实机诊断：10 个来源参数直接 Delete 约 100ms。
        // 旧版把 ParameterElement 的正常依赖子对象误判为危险级联，导致批次不断二分。
        // 新版先读取 GetDependentElements 作为允许删除集合，再使用较大批次直接删除。
        private const int DefaultBatchSize = 250;

        private readonly Document _document;

        public ParameterDeleteService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public int DeleteSafeSources(ScanResult result, Action<string>? log)
        {
            List<ParameterRecord> candidates = result.Groups
                .Where(x => x.Selected && x.IsCompatible && x.ValuesMerged)
                .SelectMany(x => x.Sources)
                .Where(x => string.IsNullOrEmpty(x.DeleteBlockReason) && !x.Deleted)
                .ToList();

            if (candidates.Count == 0)
            {
                log?.Invoke("没有可删除的重复参数。");
                return 0;
            }

            var totalTimer = Stopwatch.StartNew();
            var provisionalDeleted = new List<ParameterRecord>(candidates.Count);
            var metrics = new DeleteMetrics(candidates.Count);

            log?.Invoke(
                $"快速删除模式：{candidates.Count} 个候选参数，初始每批 {DefaultBatchSize} 个。" +
                "ParameterElement 自身的 Revit 依赖子对象允许随参数删除；只有出现未预期对象或缺少目标参数时才回滚并二分。" );

            using (var transaction = new Transaction(_document, "删除重复项目参数"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动删除事务。");

                for (int offset = 0; offset < candidates.Count; offset += DefaultBatchSize)
                {
                    int count = Math.Min(DefaultBatchSize, candidates.Count - offset);
                    DeleteBatchAdaptive(
                        candidates.GetRange(offset, count),
                        result,
                        provisionalDeleted,
                        metrics,
                        log);
                }

                var commitTimer = Stopwatch.StartNew();
                TransactionStatus commitStatus = transaction.Commit();
                metrics.TransactionCommitMilliseconds += commitTimer.Elapsed.TotalMilliseconds;
                if (commitStatus != TransactionStatus.Committed)
                {
                    foreach (ParameterRecord rec in provisionalDeleted)
                    {
                        rec.Deleted = false;
                        rec.DeleteBlockReason = "删除事务整体未成功提交：" + commitStatus;
                        result.Deletions.Add(new DeletionEntry
                        {
                            ParameterName = rec.Name,
                            ParameterId = rec.Id,
                            Deleted = false,
                            Reason = rec.DeleteBlockReason
                        });
                    }

                    result.Errors.Add(new ErrorEntry
                    {
                        Stage = "事务提交",
                        Message = "删除事务状态：" + commitStatus
                    });
                    return 0;
                }
            }

            foreach (ParameterRecord rec in provisionalDeleted)
            {
                rec.Deleted = true;
                result.Deletions.Add(new DeletionEntry
                {
                    ParameterName = rec.Name,
                    ParameterId = rec.Id,
                    Deleted = true,
                    Reason = "已删除；随参数删除的对象均属于 Revit 预先报告的依赖子对象"
                });
            }

            foreach (DuplicateGroupPlan plan in result.Groups.Where(x => x.ValuesMerged))
            {
                int deleted = plan.Sources.Count(x => x.Deleted);
                int blocked = plan.Sources.Count(x => !x.Deleted && !string.IsNullOrEmpty(x.DeleteBlockReason));
                plan.Status += $"；已删 {deleted}，保留/失败 {blocked}";
            }

            log?.Invoke(
                $"删除完成：已删除 {provisionalDeleted.Count} 个，回滚或保留 {candidates.Count - provisionalDeleted.Count} 个；" +
                $"总耗时 {totalTimer.Elapsed.TotalSeconds:0.00} 秒。" );
            log?.Invoke(
                $"删除性能：Delete API {metrics.DeleteApiMilliseconds:0.0} ms，子事务提交 {metrics.SubTransactionCommitMilliseconds:0.0} ms，" +
                $"外层事务提交 {metrics.TransactionCommitMilliseconds:0.0} ms；批量尝试 {metrics.BatchAttempts}，" +
                $"成功批次 {metrics.BatchCommits}，二分 {metrics.SplitCount}，单项定位 {metrics.SingleAttempts}，回滚 {metrics.RollbackCount}。" );
            result.Diagnostics["删除候选数"] = candidates.Count.ToString();
            result.Diagnostics["删除成功数"] = provisionalDeleted.Count.ToString();
            result.Diagnostics["Delete API耗时毫秒"] = metrics.DeleteApiMilliseconds.ToString("0.0");
            result.Diagnostics["删除子事务提交耗时毫秒"] = metrics.SubTransactionCommitMilliseconds.ToString("0.0");
            result.Diagnostics["删除外层事务提交耗时毫秒"] = metrics.TransactionCommitMilliseconds.ToString("0.0");
            result.Diagnostics["删除批量尝试"] = metrics.BatchAttempts.ToString();
            result.Diagnostics["删除成功批次"] = metrics.BatchCommits.ToString();
            result.Diagnostics["删除二分次数"] = metrics.SplitCount.ToString();
            result.Diagnostics["删除单项定位次数"] = metrics.SingleAttempts.ToString();
            result.Diagnostics["删除回滚次数"] = metrics.RollbackCount.ToString();
            result.Diagnostics["允许伴随对象数"] = metrics.AllowedDependentCount.ToString();
            result.Diagnostics["未预期级联对象数"] = metrics.UnexpectedDependentCount.ToString();

            log?.Invoke(
                $"依赖检查：允许的 Revit 子对象 {metrics.AllowedDependentCount} 个，未预期对象 {metrics.UnexpectedDependentCount} 个，" +
                $"依赖读取错误 {metrics.DependencyReadErrors} 个。" );
            return provisionalDeleted.Count;
        }

        private void DeleteBatchAdaptive(
            List<ParameterRecord> batch,
            ScanResult result,
            List<ParameterRecord> provisionalDeleted,
            DeleteMetrics metrics,
            Action<string>? log)
        {
            if (batch.Count == 0) return;

            metrics.BatchAttempts++;
            if (batch.Count == 1) metrics.SingleAttempts++;

            var requestedIds = new List<ElementId>(batch.Count);
            var requestedValues = new HashSet<long>();
            var allowedDeletedValues = new HashSet<long>();
            foreach (ParameterRecord rec in batch)
            {
                requestedIds.Add(rec.Element.Id);
                requestedValues.Add(rec.Id);
                allowedDeletedValues.Add(rec.Id);

                try
                {
                    ICollection<ElementId> dependentIds = rec.Element.GetDependentElements(null);
                    foreach (ElementId dependentId in dependentIds)
                    {
                        if (dependentId == null) continue;
                        long value = dependentId.GetIdValue();
                        if (allowedDeletedValues.Add(value) && value != rec.Id)
                            metrics.AllowedDependentCount++;
                    }
                }
                catch
                {
                    // 读取失败时不扩大允许集合；实际 Delete 若返回额外对象会回滚并二分。
                    metrics.DependencyReadErrors++;
                }
            }

            ICollection<ElementId>? deletedIds = null;
            List<long> unexpectedIds = new List<long>();
            Exception? failure = null;

            using (var subTransaction = new SubTransaction(_document))
            {
                try
                {
                    if (subTransaction.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动删除子事务。");

                    var deleteTimer = Stopwatch.StartNew();
                    deletedIds = _document.Delete(requestedIds);
                    metrics.DeleteApiMilliseconds += deleteTimer.Elapsed.TotalMilliseconds;

                    var deletedValues = new HashSet<long>(
                        deletedIds.Where(x => x != null).Select(x => x.GetIdValue()));

                    unexpectedIds = deletedValues
                        .Where(x => !allowedDeletedValues.Contains(x))
                        .Distinct()
                        .ToList();
                    bool missingRequested = requestedValues.Any(x => !deletedValues.Contains(x));

                    if (unexpectedIds.Count > 0 || missingRequested)
                    {
                        metrics.UnexpectedDependentCount += unexpectedIds.Count;
                        subTransaction.RollBack();
                        metrics.RollbackCount++;
                    }
                    else
                    {
                        var commitTimer = Stopwatch.StartNew();
                        TransactionStatus status = subTransaction.Commit();
                        metrics.SubTransactionCommitMilliseconds += commitTimer.Elapsed.TotalMilliseconds;
                        if (status != TransactionStatus.Committed)
                            throw new InvalidOperationException("删除子事务未提交：" + status);

                        provisionalDeleted.AddRange(batch);
                        metrics.BatchCommits++;
                        metrics.Processed += batch.Count;
                        ReportProgress(metrics, provisionalDeleted.Count, log);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    failure = ex;
                    if (subTransaction.GetStatus() == TransactionStatus.Started)
                    {
                        subTransaction.RollBack();
                        metrics.RollbackCount++;
                    }
                }
            }

            if (batch.Count > 1)
            {
                metrics.SplitCount++;
                int middle = batch.Count / 2;
                DeleteBatchAdaptive(batch.GetRange(0, middle), result, provisionalDeleted, metrics, log);
                DeleteBatchAdaptive(batch.GetRange(middle, batch.Count - middle), result, provisionalDeleted, metrics, log);
                return;
            }

            ParameterRecord singleRecord = batch[0];
            string reason;
            if (failure != null)
            {
                reason = "Revit 删除失败：" + failure.Message;
                result.Errors.Add(new ErrorEntry
                {
                    Stage = "删除重复参数",
                    ParameterName = singleRecord.Name,
                    ParameterId = singleRecord.Id,
                    Message = failure.Message
                });
            }
            else if (unexpectedIds.Count > 0)
            {
                reason = "删除出现未预期级联对象，已回滚：" + DescribeElements(unexpectedIds);
            }
            else
            {
                reason = "Revit 未确认删除当前参数，已回滚。";
            }

            singleRecord.DeleteBlockReason = reason;
            result.Deletions.Add(new DeletionEntry
            {
                ParameterName = singleRecord.Name,
                ParameterId = singleRecord.Id,
                Deleted = false,
                Reason = reason
            });
            metrics.Processed++;
            ReportProgress(metrics, provisionalDeleted.Count, log);
        }

        private static void ReportProgress(DeleteMetrics metrics, int provisionalDeleted, Action<string>? log)
        {
            if (metrics.Processed < metrics.NextProgressLog && metrics.Processed < metrics.Total)
                return;

            log?.Invoke(
                $"删除进度：{metrics.Processed}/{metrics.Total}，暂定成功 {provisionalDeleted}；" +
                $"成功批次 {metrics.BatchCommits}，二分 {metrics.SplitCount}，单项定位 {metrics.SingleAttempts}……" );
            while (metrics.NextProgressLog <= metrics.Processed)
                metrics.NextProgressLog += 250;
        }

        private string DescribeElements(IEnumerable<long> ids)
        {
            List<long> values = ids.Distinct().ToList();
            var descriptions = new List<string>();
            foreach (long idValue in values.Take(12))
            {
                try
                {
                    Element? element = _document.GetElement(ElementIdExtensions.CreateId(idValue));
                    string name = element == null ? "未知对象" : element.Name;
                    descriptions.Add($"{idValue}({name})");
                }
                catch
                {
                    descriptions.Add(idValue.ToString());
                }
            }

            if (values.Count > descriptions.Count)
                descriptions.Add("另有 " + (values.Count - descriptions.Count) + " 个对象");

            return string.Join(", ", descriptions);
        }

        private sealed class DeleteMetrics
        {
            public DeleteMetrics(int total)
            {
                Total = total;
            }

            public int Total { get; }
            public int Processed { get; set; }
            public int NextProgressLog { get; set; } = 250;
            public int BatchAttempts { get; set; }
            public int BatchCommits { get; set; }
            public int SplitCount { get; set; }
            public int SingleAttempts { get; set; }
            public int RollbackCount { get; set; }
            public int AllowedDependentCount { get; set; }
            public int UnexpectedDependentCount { get; set; }
            public int DependencyReadErrors { get; set; }
            public double DeleteApiMilliseconds { get; set; }
            public double SubTransactionCommitMilliseconds { get; set; }
            public double TransactionCommitMilliseconds { get; set; }
        }
    }
}
