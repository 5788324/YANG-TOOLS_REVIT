using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Models.BatchTask;

namespace YangTools.Revit.Core.BatchTasks
{
    /// <summary>
    /// Tracks whether a batch document was opened by YangTools and therefore must be closed afterwards.
    /// Existing documents in the current Revit session are reused and never closed by this helper.
    /// </summary>
    public sealed class BatchDocumentHandle : IDisposable
    {
        public Document Document { get; }
        public bool CloseWhenDone { get; }
        public string Label { get; }

        internal BatchDocumentHandle(Document document, bool closeWhenDone, string label)
        {
            Document = document;
            CloseWhenDone = closeWhenDone;
            Label = label;
        }

        public void Dispose()
        {
            if (!CloseWhenDone || Document == null || !Document.IsValidObject)
                return;

            try
            {
                if (Document.IsModifiable)
                    throw new InvalidOperationException($"文档“{Label}”仍处于可修改状态，无法安全关闭。");

                Document.Close(false);
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError($"关闭批处理文档：{Label}", ex);
            }
        }
    }

    public static class BatchDocumentOpener
    {
        public static BatchDocumentHandle Open(UIApplication uiApp, BatchDocumentInfo info)
        {
            if (uiApp == null)
                throw new ArgumentNullException(nameof(uiApp));
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            Document activeDocument = uiApp.ActiveUIDocument.Document;
            if (info.IsCurrentDocument)
                return new BatchDocumentHandle(activeDocument, false, activeDocument.Title);

            if (info.OpenDocument != null && info.OpenDocument.IsValidObject)
                return new BatchDocumentHandle(info.OpenDocument, false, info.OpenDocument.Title);

            if (info.IsCloudModel)
            {
                Document? openCloud = FindOpenCloudDocument(uiApp, info);
                if (openCloud != null)
                    return new BatchDocumentHandle(openCloud, false, openCloud.Title);

                if (!Guid.TryParse(info.CloudProjectGuid, out Guid projectGuid)
                    || !Guid.TryParse(info.CloudModelGuid, out Guid modelGuid))
                {
                    throw new InvalidOperationException(
                        $"云模型“{info.DisplayName}”缺少有效 Project GUID / Model GUID。请先在当前 Revit 会话打开该云模型，再点击“添加已打开模型（含云）”。");
                }

                if (string.IsNullOrWhiteSpace(info.CloudRegion))
                    throw new InvalidOperationException($"云模型“{info.DisplayName}”缺少 Region 信息。");

                ModelPath cloudPath = ModelPathUtils.ConvertCloudGUIDsToCloudPath(
                    info.CloudRegion,
                    projectGuid,
                    modelGuid);
                var cloudOpenOptions = new OpenOptions
                {
                    DetachFromCentralOption = DetachFromCentralOption.DoNotDetach
                };
                Document cloudDocument = uiApp.Application.OpenDocumentFile(cloudPath, cloudOpenOptions);
                if (cloudDocument == null || !cloudDocument.IsValidObject)
                    throw new InvalidOperationException($"无法打开云模型：{info.DisplayName}");
                if (cloudDocument.IsFamilyDocument)
                {
                    try { cloudDocument.Close(false); } catch { }
                    throw new InvalidOperationException("批处理导出只支持 Revit 项目文档，不支持族文档。");
                }

                return new BatchDocumentHandle(cloudDocument, true, cloudDocument.Title);
            }

            string filePath = info.FilePath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filePath))
                throw new InvalidOperationException("批处理模型路径为空。");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("批处理模型不存在，或 Desktop Connector 尚未将文件同步到本机。", filePath);

            string normalizedPath = NormalizePath(filePath);
            Document? alreadyOpen = uiApp.Application.Documents
                .Cast<Document>()
                .FirstOrDefault(document =>
                    !document.IsModelInCloud
                    && !string.IsNullOrWhiteSpace(document.PathName)
                    && string.Equals(NormalizePath(document.PathName), normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (alreadyOpen != null)
                return new BatchDocumentHandle(alreadyOpen, false, alreadyOpen.Title);

            var openOptions = new OpenOptions();
            using (BasicFileInfo fileInfo = BasicFileInfo.Extract(filePath))
            {
                if (fileInfo.IsSavedInLaterVersion)
                {
                    throw new InvalidOperationException(
                        $"模型“{Path.GetFileName(filePath)}”由更高版本 Revit 保存，当前 Revit 无法打开。");
                }

                // External local workshared documents are opened detached for export only.
                openOptions.DetachFromCentralOption = fileInfo.IsWorkshared
                    ? DetachFromCentralOption.DetachAndPreserveWorksets
                    : DetachFromCentralOption.DoNotDetach;
            }

            ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);
            Document opened = uiApp.Application.OpenDocumentFile(modelPath, openOptions);
            if (opened == null || !opened.IsValidObject)
                throw new InvalidOperationException($"无法打开批处理模型：{filePath}");
            if (opened.IsFamilyDocument)
            {
                try { opened.Close(false); } catch { }
                throw new InvalidOperationException("批处理导出只支持 Revit 项目文档，不支持族文档。");
            }

            return new BatchDocumentHandle(opened, true, opened.Title);
        }


        private static Document? FindOpenCloudDocument(UIApplication uiApp, BatchDocumentInfo info)
        {
            if (!Guid.TryParse(info.CloudProjectGuid, out Guid projectGuid)
                || !Guid.TryParse(info.CloudModelGuid, out Guid modelGuid))
                return null;

            foreach (Document document in uiApp.Application.Documents.Cast<Document>())
            {
                if (!document.IsValidObject || !document.IsModelInCloud)
                    continue;

                try
                {
                    ModelPath path = document.GetCloudModelPath();
                    if (path.GetProjectGUID() == projectGuid && path.GetModelGUID() == modelGuid)
                        return document;
                }
                catch
                {
                    // Ignore one unreadable cloud document and continue scanning others.
                }
            }
            return null;
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
