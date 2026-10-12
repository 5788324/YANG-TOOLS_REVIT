using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;
using YangTools.Revit.Models.BatchTask;

namespace YangTools.Revit.Core.BatchTasks
{
    public class CloudLinkItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool SkipIfExists { get; set; } = true;
        public bool Pinned { get; set; } = true;
    }

    /// <summary>
    /// Creates a Revit link from a local/network/Desktop Connector synchronized RVT path.
    /// Direct ACC cloud GUID selection is intentionally not claimed by this service.
    /// </summary>
    public static class AccLinkService
    {
        public static LinkProcessResult Process(Document doc, CloudLinkItem item)
        {
            if (doc == null)
                return LinkProcessResult.Failed("当前文档为空。");
            if (doc.IsFamilyDocument)
                return LinkProcessResult.Failed("族文档不支持创建 Revit 链接。");
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
                return LinkProcessResult.Failed("Revit 链接路径为空。");
            if (!File.Exists(item.Path))
            {
                return LinkProcessResult.Failed(
                    $"Revit 文件不存在，或 Desktop Connector 尚未同步到本机：{item.Path}");
            }

            try
            {
                if (item.SkipIfExists && HasExistingLink(doc, item.Path))
                    return LinkProcessResult.Skipped($"已存在，跳过 Revit 链接：{item.Name}");

                ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(item.Path);
                var options = new RevitLinkOptions(false);

                using (var transaction = new Transaction(doc, $"链接 Revit：{item.Name}"))
                {
                    if (transaction.Start() != TransactionStatus.Started)
                        return LinkProcessResult.Failed($"无法启动 Revit 链接事务：{item.Name}");

                    LinkLoadResult loadResult = RevitLinkType.Create(doc, modelPath, options);
                    if (loadResult == null || loadResult.ElementId == ElementId.InvalidElementId)
                    {
                        transaction.RollBack();
                        return LinkProcessResult.Failed($"RevitLinkType.Create 未返回有效链接类型：{item.Name}");
                    }

                    RevitLinkInstance instance = RevitLinkInstance.Create(doc, loadResult.ElementId);
                    if (instance == null)
                    {
                        transaction.RollBack();
                        return LinkProcessResult.Failed($"链接类型已创建，但无法创建链接实例：{item.Name}");
                    }

                    if (item.Pinned)
                        instance.Pinned = true;

                    TransactionStatus commitStatus = transaction.Commit();
                    if (commitStatus != TransactionStatus.Committed)
                    {
                        try
                        {
                            if (transaction.GetStatus() == TransactionStatus.Started)
                                transaction.RollBack();
                        }
                        catch { }
                        return LinkProcessResult.Failed(
                            $"Revit 链接事务未提交：{item.Name}（状态：{commitStatus}）");
                    }

                    return LinkProcessResult.Created($"已创建 Revit 链接：{item.Name}", instance.Id);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError($"创建 Revit 链接：{item.Name}", ex);
                return LinkProcessResult.Failed($"Revit 链接失败 [{item.Name}]：{ex.Message}");
            }
        }

        private static bool HasExistingLink(Document doc, string candidatePath)
        {
            foreach (RevitLinkType linkType in new FilteredElementCollector(doc)
                         .OfClass(typeof(RevitLinkType))
                         .Cast<RevitLinkType>())
            {
                try
                {
                    ExternalFileReference? reference = linkType.GetExternalFileReference();
                    if (reference == null)
                        continue;

                    string existingPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetPath());
                    if (ExternalLinkPathMatcher.AreEquivalent(existingPath, candidatePath))
                        return true;
                }
                catch (Exception ex)
                {
                    DiagnosticLog.WriteError("检查现有 Revit 链接", ex);
                }
            }

            return false;
        }

    }
}
