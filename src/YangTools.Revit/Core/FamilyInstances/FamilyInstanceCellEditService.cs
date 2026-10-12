using System;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.FamilyInstances
{
    // Snapshot passed through the existing, document-bound Revit ExternalEvent.
    internal sealed class FamilyInstanceCellEditRequest
    {
        public ElementId InstanceId { get; set; }
        public string ParameterKey { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
    }

    internal sealed class FamilyInstanceCellEditResult
    {
        public bool Changed { get; internal set; }
        public bool ReadbackConfirmed { get; internal set; }
        public string DisplayValue { get; internal set; } = string.Empty;
        public string ReadbackWarning { get; internal set; } = string.Empty;
    }

    /// <summary>Revit API main-thread operation; no WPF or view-model dependencies.</summary>
    internal static class FamilyInstanceCellEditService
    {
        internal static FamilyInstanceCellEditResult Execute(Document document, FamilyInstanceCellEditRequest request)
        {
            if (document == null || !document.IsValidObject)
                throw new InvalidOperationException("项目文档已关闭，无法编辑实例参数。");
            if (document.IsReadOnly)
                throw new InvalidOperationException("项目为只读，无法编辑实例参数。");
            if (request == null || request.InstanceId == null ||
                request.InstanceId.GetIdValue() == ElementId.InvalidElementId.GetIdValue() ||
                string.IsNullOrWhiteSpace(request.ParameterKey))
                throw new InvalidOperationException("实例或参数标识无效，请刷新列表。");

            var instance = document.GetElement(request.InstanceId);
            if (instance == null || !instance.IsValidObject)
                throw new InvalidOperationException($"实例 {request.InstanceId.GetIdValue()} 已不存在，请刷新列表。");
            var parameter = StableParameterKey.Resolve(instance, request.ParameterKey);
            if (parameter == null)
                throw new InvalidOperationException("目标参数已不存在或参数身份不唯一，请刷新列表。");
            if (!ParameterValueHelper.CanEditAsText(parameter))
                throw new InvalidOperationException("目标参数为只读或不能安全地通过文本编辑。");

            string before = ParameterValueHelper.GetDisplayValue(parameter);
            string value = request.NewValue ?? string.Empty;
            if (string.Equals(before, value, StringComparison.Ordinal))
                return new FamilyInstanceCellEditResult
                { Changed = false, ReadbackConfirmed = true, DisplayValue = before };

            using (var transaction = new Transaction(document, "编辑族实例参数"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动实例参数事务，模型未修改。");
                try
                {
                    if (!ParameterValueHelper.TrySetFromDisplayValue(parameter, value, out string error))
                        throw new InvalidOperationException(error ?? "实例参数写入失败。");
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("事务提交未得到确认，请检查模型状态。");
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("事务回滚状态不是 RolledBack。");
                        }
                        catch (Exception rollbackEx)
                        {
                            throw new InvalidOperationException(
                                $"实例参数编辑失败，且回滚结果无法确认：{rollbackEx.Message}；原始错误：{ex.Message}", ex);
                        }
                    }
                    throw new InvalidOperationException("实例参数编辑未确认成功提交：" + ex.Message, ex);
                }
            }

            // The write was committed. Readback errors must not claim rollback or failure.
            try
            {
                var refreshed = document.GetElement(request.InstanceId);
                var actual = refreshed == null ? null : StableParameterKey.Resolve(refreshed, request.ParameterKey);
                if (actual == null)
                    throw new InvalidOperationException("提交后找不到目标参数。");
                return new FamilyInstanceCellEditResult
                {
                    Changed = true,
                    ReadbackConfirmed = true,
                    DisplayValue = ParameterValueHelper.GetDisplayValue(actual)
                };
            }
            catch (Exception readbackEx)
            {
                return new FamilyInstanceCellEditResult
                {
                    Changed = true,
                    ReadbackConfirmed = false,
                    DisplayValue = before,
                    ReadbackWarning = "修改事务已提交，但模型回读无法确认：" + readbackEx.Message +
                        "。请刷新界面确认，不要直接重复写入。"
                };
            }
        }
    }
}
