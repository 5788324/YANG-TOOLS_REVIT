using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.FamilyInstances
{
    // Immutable-in-practice request snapshot, constructed before queueing the ExternalEvent.
    internal sealed class FamilyInstanceBatchParameterRequest
    {
        public IReadOnlyList<ElementId> InstanceIds { get; set; }
        public string ParameterKey { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
        public bool OnlyEmpty { get; set; }
    }

    internal sealed class FamilyInstanceBatchParameterResult
    {
        public bool Committed { get; internal set; }
        public int ChangedCount { get; internal set; }
        public int SkippedCount { get; internal set; }
        public bool ReadbackConfirmed { get; internal set; }
        public string ReadbackWarning { get; internal set; } = string.Empty;
    }

    /// <summary>
    /// Atomic bulk parameter edit, called ONLY inside the document-bound Revit ExternalEvent.
    /// No WPF dependencies. Failure in one element means NO successful partial batch.
    /// </summary>
    internal static class FamilyInstanceBatchParameterService
    {
        internal static FamilyInstanceBatchParameterResult Execute(Document document, FamilyInstanceBatchParameterRequest request)
        {
            if (document == null || !document.IsValidObject)
                throw new InvalidOperationException("项目文档已关闭，批量参数未执行。");
            if (document.IsReadOnly)
                throw new InvalidOperationException("项目文档为只读，批量参数未执行。");
            if (request == null || request.InstanceIds == null || request.InstanceIds.Count == 0 ||
                string.IsNullOrWhiteSpace(request.ParameterKey))
                throw new InvalidOperationException("请选择实例和要写入的参数。");

            var candidates = new List<(ElementId id, Parameter parameter, string before)>();
            var seen = new HashSet<long>();
            int skipped = 0;
            string value = request.NewValue ?? string.Empty;
            // Preflight every target before creating a transaction, including those with read-only parameters.
            foreach (ElementId id in request.InstanceIds)
            {
                long idValue = id == null ? -1 : id.GetIdValue();
                if (id == null || idValue == ElementId.InvalidElementId.GetIdValue() || !seen.Add(idValue))
                    throw new InvalidOperationException($"批量请求含无效或重复的实例 ID：{idValue}。");
                Element element = document.GetElement(id);
                if (element == null || !element.IsValidObject)
                    throw new InvalidOperationException($"实例 {idValue} 不存在，整批未执行。");
                Parameter parameter = StableParameterKey.Resolve(element, request.ParameterKey);
                if (parameter == null)
                    throw new InvalidOperationException($"实例 {idValue} 的目标参数不存在或身份不唯一，整批未执行。");
                if (!ParameterValueHelper.CanEditAsText(parameter))
                    throw new InvalidOperationException($"实例 {idValue} 的参数只读或不支持文本写入，整批未执行。");
                if (request.OnlyEmpty && !IsParameterEmpty(parameter))
                {
                    skipped++;
                    continue;
                }
                if (string.Equals(ParameterValueHelper.GetDisplayValue(parameter), value, StringComparison.Ordinal))
                {
                    skipped++;
                    continue;
                }
                candidates.Add((id, parameter, ParameterValueHelper.GetDisplayValue(parameter)));
            }

            if (candidates.Count == 0)
                return new FamilyInstanceBatchParameterResult
                { Committed = false, ChangedCount = 0, SkippedCount = skipped, ReadbackConfirmed = true };

            using (var transaction = new Transaction(document, "批量写入实例参数"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动批量参数事务，模型未修改。");
                try
                {
                    foreach (var candidate in candidates)
                    {
                        if (!ParameterValueHelper.TrySetFromDisplayValue(candidate.parameter, value, out string setError))
                            throw new InvalidOperationException($"实例 {candidate.id.GetIdValue()} 写入失败：{setError}");
                    }
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("批量参数事务未确认提交，请核查模型状态。");
                }
                catch (Exception writeError)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("回滚状态不是 RolledBack。");
                        }
                        catch (Exception rollbackError)
                        {
                            throw new InvalidOperationException(
                                $"批量参数写入失败，且回滚无法确认：{rollbackError.Message}；原始错误：{writeError.Message}", writeError);
                        }
                        throw new InvalidOperationException("批量参数写入失败，整批已回滚：" + writeError.Message, writeError);
                    }
                    // This includes a commit with an unknown status: never claim a rollback.
                    throw new InvalidOperationException("批量参数事务结果无法确认，请检查模型后再重试：" + writeError.Message, writeError);
                }
            }

            // Transaction has committed. Readback failure is NOT a write/rollback failure.
            var result = new FamilyInstanceBatchParameterResult
            { Committed = true, ChangedCount = candidates.Count, SkippedCount = skipped, ReadbackConfirmed = true };
            try
            {
                foreach (var candidate in candidates)
                {
                    Element actual = document.GetElement(candidate.id);
                    if (actual == null || !actual.IsValidObject)
                        throw new InvalidOperationException($"实例 {candidate.id.GetIdValue()} 已不存在，无法回读。");
                    Parameter updated = StableParameterKey.Resolve(actual, request.ParameterKey);
                    if (updated == null)
                        throw new InvalidOperationException($"实例 {candidate.id.GetIdValue()} 的目标参数无法回读。");
                    string after = ParameterValueHelper.GetDisplayValue(updated);
                    if (string.Equals(after, candidate.before, StringComparison.Ordinal))
                        throw new InvalidOperationException($"实例 {candidate.id.GetIdValue()} 的提交前后参数显示值相同，无法确认写入效果。");
                }
            }
            catch (Exception readbackError)
            {
                result.ReadbackConfirmed = false;
                result.ReadbackWarning = "批量事务已提交，但参数回读无法确认：" + readbackError.Message +
                    "。请刷新界面核验，不要直接重复写入。";
            }
            return result;
        }

        private static bool IsParameterEmpty(Parameter parameter)
        {
            if (parameter == null || !parameter.HasValue) return true;
            return parameter.StorageType == StorageType.String && string.IsNullOrWhiteSpace(parameter.AsString());
        }
    }
}
