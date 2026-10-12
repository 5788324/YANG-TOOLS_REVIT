using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.FamilyInstances
{
    /// <summary>Captured before the WPF window queues its document-bound ExternalEvent.</summary>
    internal sealed class FamilyInstanceBatchTypeRequest
    {
        public IReadOnlyList<ElementId> InstanceIds { get; set; }
        public ElementId TargetTypeId { get; set; }
    }

    internal sealed class FamilyInstanceTypeReplacement
    {
        public ElementId OriginalId { get; internal set; }
        public ElementId EffectiveId { get; internal set; }
    }

    internal sealed class FamilyInstanceBatchTypeResult
    {
        public bool Committed { get; internal set; }
        public int ChangedCount { get; internal set; }
        public int SkippedCount { get; internal set; }
        public IReadOnlyList<FamilyInstanceTypeReplacement> Replacements { get; internal set; }
            = new List<FamilyInstanceTypeReplacement>();
        public bool ReadbackConfirmed { get; internal set; }
        public string ReadbackWarning { get; internal set; } = string.Empty;
    }

    /// <summary>
    /// All-or-nothing bulk instance type change; invoke only on the Revit API thread.
    /// Revit Element.ChangeTypeId may replace an element and return a different ElementId.
    /// WPF must not interpret a successful Commit followed by readback failure as rollback.
    /// </summary>
    internal static class FamilyInstanceBatchTypeService
    {
        internal static FamilyInstanceBatchTypeResult Execute(Document document, FamilyInstanceBatchTypeRequest request)
        {
            if (document == null || !document.IsValidObject)
                throw new InvalidOperationException("项目文档已关闭，批量换型未执行。");
            if (document.IsReadOnly)
                throw new InvalidOperationException("项目文档为只读，批量换型未执行。");
            if (request == null || request.InstanceIds == null || request.InstanceIds.Count == 0 ||
                request.TargetTypeId == null || request.TargetTypeId == ElementId.InvalidElementId)
                throw new InvalidOperationException("请选择实例和目标类型。");
            ElementType targetType = document.GetElement(request.TargetTypeId) as ElementType;
            if (targetType == null || !targetType.IsValidObject)
                throw new InvalidOperationException("目标类型不存在，整批未执行。");

            var candidates = new List<ElementId>();
            var seen = new HashSet<long>();
            int skipped = 0;
            long targetId = request.TargetTypeId.GetIdValue();
            foreach (ElementId id in request.InstanceIds)
            {
                long idValue = id == null ? -1 : id.GetIdValue();
                if (id == null || idValue == ElementId.InvalidElementId.GetIdValue() || !seen.Add(idValue))
                    throw new InvalidOperationException($"批量换型请求含无效或重复图元 ID：{idValue}。");
                Element element = document.GetElement(id);
                if (element == null || !element.IsValidObject)
                    throw new InvalidOperationException($"图元 {idValue} 不存在，整批未执行。");
                ElementId originalTypeId = element.GetTypeId();
                if (originalTypeId == null || originalTypeId == ElementId.InvalidElementId)
                    throw new InvalidOperationException($"图元 {idValue} 不支持类型修改，整批未执行。");
                if (originalTypeId.GetIdValue() == targetId)
                {
                    skipped++;
                    continue;
                }
                candidates.Add(id);
            }

            if (candidates.Count == 0)
                return new FamilyInstanceBatchTypeResult
                { Committed = false, ChangedCount = 0, SkippedCount = skipped, ReadbackConfirmed = true };

            var replacements = new List<FamilyInstanceTypeReplacement>();
            using (var transaction = new Transaction(document, "批量更换实例类型"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动批量换型事务，模型未修改。");
                try
                {
                    foreach (ElementId originalId in candidates)
                    {
                        Element element = document.GetElement(originalId);
                        if (element == null || !element.IsValidObject)
                            throw new InvalidOperationException($"图元 {originalId.GetIdValue()} 在写入时已失效。");
                        ElementId replacementId = element.ChangeTypeId(request.TargetTypeId);
                        ElementId effectiveId = replacementId != null && replacementId != ElementId.InvalidElementId
                            ? replacementId : originalId;
                        // ChangeTypeId may create an entirely new element. Never read only the old ID.
                        Element effective = document.GetElement(effectiveId);
                        if (effective == null || !effective.IsValidObject ||
                            effective.GetTypeId() == null || effective.GetTypeId().GetIdValue() != targetId)
                            throw new InvalidOperationException($"图元 {originalId.GetIdValue()} 换型后类型或替换图元 ID 校验不一致。");
                        replacements.Add(new FamilyInstanceTypeReplacement
                        { OriginalId = originalId, EffectiveId = effectiveId });
                    }
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("批量换型事务提交结果无法确认，请核查模型状态。");
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
                                $"批量换型失败且回滚无法确认：{rollbackError.Message}；原始错误：{writeError.Message}", writeError);
                        }
                        throw new InvalidOperationException("批量换型失败，整批已回滚：" + writeError.Message, writeError);
                    }
                    throw new InvalidOperationException(
                        "批量换型事务结果无法确认，请检查模型后再重试：" + writeError.Message, writeError);
                }
            }

            // The transaction is committed here, regardless of the outcome of readback.
            var result = new FamilyInstanceBatchTypeResult
            {
                Committed = true,
                ChangedCount = replacements.Count,
                SkippedCount = skipped,
                Replacements = replacements.AsReadOnly(),
                ReadbackConfirmed = true
            };
            try
            {
                foreach (FamilyInstanceTypeReplacement replacement in replacements)
                {
                    Element effective = document.GetElement(replacement.EffectiveId);
                    if (effective == null || !effective.IsValidObject ||
                        effective.GetTypeId() == null || effective.GetTypeId().GetIdValue() != targetId)
                        throw new InvalidOperationException($"图元 {replacement.OriginalId.GetIdValue()} " +
                            $"的实际替换 ID {replacement.EffectiveId.GetIdValue()} 无法确认目标类型。");
                }
            }
            catch (Exception readbackError)
            {
                result.ReadbackConfirmed = false;
                result.ReadbackWarning = "批量换型事务已提交，但模型回读无法确认：" + readbackError.Message +
                    "。请刷新界面核验，不要直接重复换型。";
            }
            return result;
        }
    }
}
