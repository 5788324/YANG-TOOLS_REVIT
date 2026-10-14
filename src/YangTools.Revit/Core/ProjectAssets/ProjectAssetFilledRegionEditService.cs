using System;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.ProjectAssets
{
    internal enum ProjectAssetFilledRegionEditKind { Name, Masking }

    internal sealed class ProjectAssetFilledRegionEditRequest
    {
        public ElementId TypeId { get; set; }
        public ProjectAssetFilledRegionEditKind Kind { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsMasking { get; set; }
    }

    internal sealed class ProjectAssetFilledRegionEditResult
    {
        public bool Changed { get; internal set; }
        public bool ReadbackConfirmed { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public bool IsMasking { get; internal set; }
    }

    /// <summary>Document-bound ExternalEvent service; no WPF ViewModels retained.</summary>
    internal static class ProjectAssetFilledRegionEditService
    {
        internal static ProjectAssetFilledRegionEditResult Execute(
            Document doc, ProjectAssetFilledRegionEditRequest request)
        {
            if (doc == null || !doc.IsValidObject || doc.IsReadOnly || doc.IsModifiable)
                throw new InvalidOperationException("项目不可编辑，填充区域未修改。");
            if (request?.TypeId == null ||
                request.TypeId.GetIdValue() == ElementId.InvalidElementId.GetIdValue())
                throw new InvalidOperationException("填充区域类型 ID 无效。");
            var region = doc.GetElement(request.TypeId) as FilledRegionType;
            if (region == null || !region.IsValidObject)
                throw new InvalidOperationException("填充区域类型已不存在，请刷新列表。");
            var result = new ProjectAssetFilledRegionEditResult
            {
                Name = region.Name,
                IsMasking = region.IsMasking,
                ReadbackConfirmed = true
            };
            if (request.Kind == ProjectAssetFilledRegionEditKind.Name)
            {
                request.Name = (request.Name ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(request.Name))
                    throw new InvalidOperationException("填充区域名称不能为空。");
                if (result.Name.StartsWith("<", StringComparison.Ordinal) ||
                    result.Name.EndsWith(">", StringComparison.Ordinal))
                    throw new InvalidOperationException("内置填充区域类型不能重命名。");
                if (result.Name == request.Name) return result;
            }
            else if (request.Kind == ProjectAssetFilledRegionEditKind.Masking)
            {
                if (result.IsMasking == request.IsMasking) return result;
            }
            else throw new InvalidOperationException("不支持的填充区域编辑类型。");

            using (var transaction = new Transaction(doc,
                request.Kind == ProjectAssetFilledRegionEditKind.Name ? "重命名填充区域" : "修改填充区域遮罩"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动填充区域事务，模型未修改。");
                try
                {
                    if (request.Kind == ProjectAssetFilledRegionEditKind.Name)
                        region.Name = request.Name;
                    else region.IsMasking = request.IsMasking;

                    bool verifiedBeforeCommit = request.Kind == ProjectAssetFilledRegionEditKind.Name
                        ? region.Name == request.Name : region.IsMasking == request.IsMasking;
                    if (!verifiedBeforeCommit)
                        throw new InvalidOperationException("填充区域写入结果不一致，取消修改。");
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("填充区域事务没有确认提交，状态可能未知。");
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("回滚状态不是 RolledBack。");
                        }
                        catch (Exception rollbackEx)
                        {
                            throw new InvalidOperationException(
                                $"填充区域编辑失败且回滚结果未知：{rollbackEx.Message}；原始错误：{ex.Message}", ex);
                        }
                    }
                    throw;
                }
            }
            result.Changed = true;
            var actual = doc.GetElement(request.TypeId) as FilledRegionType;
            result.ReadbackConfirmed = actual != null &&
                (request.Kind == ProjectAssetFilledRegionEditKind.Name ?
                    actual.Name == request.Name : actual.IsMasking == request.IsMasking);
            if (actual != null)
            {
                result.Name = actual.Name;
                result.IsMasking = actual.IsMasking;
            }
            return result;
        }
    }
}
