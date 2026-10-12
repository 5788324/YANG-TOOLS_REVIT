using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;
using YangTools.Revit.Models.BatchTask;

namespace YangTools.Revit.Core.BatchTasks
{
    public class CadLinkItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool SkipIfExists { get; set; } = true;
        public bool Pinned { get; set; } = true;
    }

    public static class CadLinkService
    {
        public static LinkProcessResult Process(Document doc, CadLinkItem item)
        {
            if (doc == null)
                return LinkProcessResult.Failed("当前文档为空。");
            if (doc.IsFamilyDocument)
                return LinkProcessResult.Failed("族文档不支持创建 CAD 链接。");
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
                return LinkProcessResult.Failed("CAD 链接路径为空。");
            if (!File.Exists(item.Path))
                return LinkProcessResult.Failed($"CAD 文件不存在：{item.Path}");

            try
            {
                if (item.SkipIfExists && HasExistingLink(doc, item.Path))
                    return LinkProcessResult.Skipped($"已存在，跳过 CAD 链接：{item.Name}");

                var options = new DWGImportOptions
                {
                    ColorMode = ImportColorMode.Preserved,
                    CustomScale = 1.0,
                    OrientToView = false,
                    Placement = ImportPlacement.Origin,
                    ThisViewOnly = false,
                    Unit = ImportUnit.Default,
                    VisibleLayersOnly = false
                };

                using (var transaction = new Transaction(doc, $"链接 CAD：{item.Name}"))
                {
                    if (transaction.Start() != TransactionStatus.Started)
                        return LinkProcessResult.Failed($"无法启动 CAD 链接事务：{item.Name}");

                    bool success = doc.Link(item.Path, options, doc.ActiveView, out ElementId linkResultId);
                    if (!success || linkResultId == ElementId.InvalidElementId)
                    {
                        transaction.RollBack();
                        return LinkProcessResult.Failed($"Revit 未创建 CAD 链接：{item.Name}");
                    }

                    ImportInstance? instance = doc.GetElement(linkResultId) as ImportInstance;
                    if (item.Pinned && instance != null)
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
                            $"CAD 链接事务未提交：{item.Name}（状态：{commitStatus}）");
                    }

                    return LinkProcessResult.Created($"已创建 CAD 链接：{item.Name}", linkResultId);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError($"创建 CAD 链接：{item.Name}", ex);
                return LinkProcessResult.Failed($"CAD 链接失败 [{item.Name}]：{ex.Message}");
            }
        }

        private static bool HasExistingLink(Document doc, string candidatePath)
        {
            foreach (CADLinkType linkType in new FilteredElementCollector(doc)
                         .OfClass(typeof(CADLinkType))
                         .Cast<CADLinkType>())
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
                    DiagnosticLog.WriteError("检查现有 CAD 链接", ex);
                }
            }

            return false;
        }

    }
}
