using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.Core.ProjectAssets
{
    /// <summary>Immutable inputs captured by the UI before scheduling an ExternalEvent.</summary>
    internal sealed class ProjectAssetBatchRenameRequest
    {
        public IReadOnlyCollection<ElementId> ElementIds { get; set; } = Array.Empty<ElementId>();
        public string Prefix { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public string Find { get; set; } = string.Empty;
        public string Replace { get; set; } = string.Empty;
    }

    internal sealed class ProjectAssetBatchRenameResult
    {
        public int RequestedCount { get; internal set; }
        public int RenamedCount { get; internal set; }
        public int UnchangedCount { get; internal set; }
    }

    /// <summary>
    /// No WPF dependency. Call only inside Revit's valid API context (ExternalEvent).
    /// Preflight first; one Transaction, one Undo. Any failed rename rolls back the whole batch.
    /// </summary>
    internal static class ProjectAssetBatchRenameService
    {
        private sealed class RenameEntry
        {
            internal Element Element { get; set; }
            internal string OriginalName { get; set; }
            internal string TargetName { get; set; }
        }

        internal static ProjectAssetBatchRenameResult Execute(Document document, ProjectAssetBatchRenameRequest request)
        {
            if (document == null || !document.IsValidObject)
                throw new InvalidOperationException("项目文档已关闭，无法批量重命名资产。");
            if (document.IsReadOnly)
                throw new InvalidOperationException("当前项目为只读，无法批量重命名资产。");
            if (request == null) throw new ArgumentNullException(nameof(request));

            var ids = (request.ElementIds ?? Array.Empty<ElementId>())
                .Where(id => id != null)
                .GroupBy(id => id.GetIdValue())
                .Select(group => group.First())
                .ToList();
            var result = new ProjectAssetBatchRenameResult { RequestedCount = ids.Count };
            if (ids.Count == 0) return result;

            var prefix = request.Prefix ?? string.Empty;
            var suffix = request.Suffix ?? string.Empty;
            var find = request.Find ?? string.Empty;
            var replacement = request.Replace ?? string.Empty;
            var plan = new List<RenameEntry>();
            foreach (var id in ids)
            {
                var element = document.GetElement(id);
                if (element == null || !element.IsValidObject)
                    throw new InvalidOperationException($"图元 ID {id.GetIdValue()} 已被删除，整批操作已取消。请刷新列表后重试。");
                string originalName = element.Name;
                string targetName = string.IsNullOrEmpty(find) ? originalName : originalName.Replace(find, replacement);
                targetName = prefix + targetName + suffix;
                if (string.IsNullOrWhiteSpace(targetName))
                    throw new InvalidOperationException($"图元 ID {id.GetIdValue()} 的目标名称为空，整批操作已取消。");
                if (string.Equals(originalName, targetName, StringComparison.Ordinal))
                {
                    result.UnchangedCount++;
                    continue;
                }
                plan.Add(new RenameEntry { Element = element, OriginalName = originalName, TargetName = targetName });
            }
            if (plan.Count == 0) return result;

            // Disposing an uncommitted Transaction rolls back even if Revit throws on Name assignment.
            using (var transaction = new Transaction(document, "批量重命名资产"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动批量重命名事务，项目未修改。");
                try
                {
                    foreach (var entry in plan)
                    {
                        try { entry.Element.Name = entry.TargetName; }
                        catch (Exception error)
                        {
                            throw new InvalidOperationException(
                                $"资产“{entry.OriginalName}”重命名失败：{error.Message}。本批所有修改将回滚。", error);
                        }
                    }
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("批量重命名提交失败，请检查 Revit 事务状态。");
                }
                catch
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                        transaction.RollBack();
                    throw;
                }
            }
            result.RenamedCount = plan.Count;
            return result;
        }
    }
}
