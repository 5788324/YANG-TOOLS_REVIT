using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.ProjectAssets
{
    /// <summary>Snapshot of selected Revit ids taken before queuing the ExternalEvent.</summary>
    internal sealed class ProjectAssetDeleteRequest
    {
        public IReadOnlyCollection<ElementId> ElementIds { get; set; } = Array.Empty<ElementId>();
    }

    internal sealed class ProjectAssetDeletePreview
    {
        public IReadOnlyList<ElementId> SelectedIds { get; internal set; } = Array.Empty<ElementId>();
        public IReadOnlyList<ElementId> DeletedIds { get; internal set; } = Array.Empty<ElementId>();
        public IReadOnlyList<ElementId> RelatedIds { get; internal set; } = Array.Empty<ElementId>();
    }

    internal sealed class ProjectAssetDeleteResult
    {
        public IReadOnlyList<ElementId> DeletedIds { get; internal set; } = Array.Empty<ElementId>();
        public int DeletedCount => DeletedIds.Count;
    }

    /// <summary>
    /// Revit-API-only deletion service. Both operations must run inside an ExternalEvent.
    /// The preview always rolls back, and the confirmed operation uses one Transaction/Undo.
    /// </summary>
    internal static class ProjectAssetDeleteService
    {
        internal static ProjectAssetDeletePreview Preview(Document document, ProjectAssetDeleteRequest request)
        {
            var ids = ValidateAndCapture(document, request);
            var preview = new ProjectAssetDeletePreview { SelectedIds = ids };
            if (ids.Count == 0) return preview;

            using (var transaction = new Transaction(document, "预演删除资产（自动回滚）"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动删除预演事务，未修改模型。");
                try
                {
                    var deleted = document.Delete(ids)?.ToList() ?? new List<ElementId>();
                    if (transaction.RollBack() != TransactionStatus.RolledBack)
                        throw new InvalidOperationException("预演删除事务回滚未能确认，请检查模型状态，不允许继续删除。");
                    var selected = new HashSet<long>(ids.Select(id => id.GetIdValue()));
                    preview.DeletedIds = deleted;
                    preview.RelatedIds = deleted.Where(id => !selected.Contains(id.GetIdValue())).ToList();
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("删除预演回滚未能确认，已阻止正式删除。", ex);
                        }
                        catch (Exception rollbackError)
                        {
                            throw new InvalidOperationException("删除预演失败且回滚未能确认：" + rollbackError.Message, ex);
                        }
                    }
                    throw;
                }
            }
            if (preview.DeletedIds.Count == 0)
                throw new InvalidOperationException("预演未发现可删除的图元，已取消操作。");
            return preview;
        }

        internal static ProjectAssetDeleteResult Execute(Document document, ProjectAssetDeletePreview preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            var ids = ValidateAndCapture(document, new ProjectAssetDeleteRequest { ElementIds = preview.SelectedIds });
            if (ids.Count == 0 || preview.DeletedIds == null || preview.DeletedIds.Count == 0)
                throw new InvalidOperationException("没有可确认删除的图元，不创建撤销记录。");

            using (var transaction = new Transaction(document, "批量删除资产(YANG TOOLS)"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动资产删除事务，项目未修改。");
                try
                {
                    var deleted = document.Delete(ids)?.ToList() ?? new List<ElementId>();
                    // An unexpected cascade must not silently delete more elements than confirmed.
                    var expected = new HashSet<long>(preview.DeletedIds.Select(id => id.GetIdValue()));
                    var actual = new HashSet<long>(deleted.Select(id => id.GetIdValue()));
                    if (!expected.SetEquals(actual))
                        throw new InvalidOperationException("实际删除集合与预演确认不一致，取消并回滚本次删除。");
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("资产删除事务未能提交，请核查模型状态。");
                    return new ProjectAssetDeleteResult { DeletedIds = deleted };
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("删除事务回滚未能确认，请检查模型状态。", ex);
                        }
                        catch (Exception rollbackError)
                        {
                            throw new InvalidOperationException("删除失败，回滚未能确认：" + rollbackError.Message, ex);
                        }
                    }
                    throw;
                }
            }
        }

        private static List<ElementId> ValidateAndCapture(Document document, ProjectAssetDeleteRequest request)
        {
            if (document == null || !document.IsValidObject)
                throw new InvalidOperationException("项目文档已经关闭，删除未执行。");
            if (document.IsReadOnly)
                throw new InvalidOperationException("项目处于只读状态，删除未执行。");
            if (request == null) throw new ArgumentNullException(nameof(request));
            var ids = (request.ElementIds ?? Array.Empty<ElementId>())
                .Where(id => id != null)
                .GroupBy(id => id.GetIdValue())
                .Select(g => g.First()).ToList();
            foreach (var id in ids)
            {
                if (id.GetIdValue() == ElementId.InvalidElementId.GetIdValue())
                    throw new InvalidOperationException("删除列表中存在无效图元 ID，请刷新列表。");
                // Some Revit line-style subcategories have nonpositive IDs and no Element
                // returned by GetElement. Allow their trial deletion to decide support.
                if (id.GetIdValue() > 0 && document.GetElement(id) == null)
                    throw new InvalidOperationException($"图元 ID {id.GetIdValue()} 已不存在，请刷新列表。");
            }
            return ids;
        }
    }
}
