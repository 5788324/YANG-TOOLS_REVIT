using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;
using YangTools.Revit.Models.BatchTask;

namespace YangTools.Revit.Core.BatchTasks
{
    public static class BatchTaskEngine
    {
        public static BatchTaskRunResult RunLinks(
            Document doc,
            IBatchTaskOptions viewModel,
            Action<string>? progressCallback)
        {
            var result = new BatchTaskRunResult();
            if (viewModel.Links.Count == 0)
                return result;

            if (doc.IsFamilyDocument)
            {
                result.Errors.Add("当前文档是族文档，不能创建 Revit/CAD 链接。");
                return result;
            }
            if (doc.IsModifiable)
            {
                result.Errors.Add("当前文档已有事务正在运行，不能开始批量链接。");
                return result;
            }

            int createdCount = 0;
            using (var group = new TransactionGroup(doc, "批量链接模型"))
            {
                try
                {
                    if (group.Start() != TransactionStatus.Started)
                    {
                        result.Errors.Add("无法启动批量链接事务组。");
                        return result;
                    }

                    foreach (LinkItem link in viewModel.Links.ToList())
                    {
                        progressCallback?.Invoke($"链接：{link.FileName}");
                        LinkProcessResult linkResult;
                        if (link.IsRevit)
                        {
                            linkResult = AccLinkService.Process(doc, new CloudLinkItem
                            {
                                Name = link.FileName,
                                Path = link.FilePath,
                                SkipIfExists = viewModel.LinkSkipIfExists,
                                Pinned = viewModel.LinkPinned
                            });
                        }
                        else if (link.IsCad)
                        {
                            linkResult = CadLinkService.Process(doc, new CadLinkItem
                            {
                                Name = link.FileName,
                                Path = link.FilePath,
                                SkipIfExists = viewModel.LinkSkipIfExists,
                                Pinned = viewModel.LinkPinned
                            });
                        }
                        else
                        {
                            linkResult = LinkProcessResult.Failed($"不支持的链接格式：{link.FileName}");
                        }

                        switch (linkResult.Status)
                        {
                            case LinkProcessStatus.Created:
                                createdCount++;
                                result.Successes.Add(linkResult.Message);
                                break;
                            case LinkProcessStatus.Skipped:
                                result.Warnings.Add(linkResult.Message);
                                break;
                            default:
                                result.Errors.Add(linkResult.Message);
                                break;
                        }
                    }

                    if (createdCount > 0)
                    {
                        TransactionStatus groupStatus = group.Assimilate();
                        if (groupStatus != TransactionStatus.Committed)
                        {
                            try
                            {
                                if (group.GetStatus() == TransactionStatus.Started)
                                    group.RollBack();
                            }
                            catch { }
                            result.Errors.Add(
                                $"批量链接事务组未能合并为一个撤销步骤（状态：{groupStatus}）。");
                        }
                    }
                    else if (group.GetStatus() == TransactionStatus.Started)
                    {
                        group.RollBack();
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        if (group.GetStatus() == TransactionStatus.Started)
                            group.RollBack();
                    }
                    catch { }

                    string logPath = DiagnosticLog.WriteError("批量链接模型", ex);
                    result.Errors.Add($"批量链接中断：{ex.Message}" +
                                      (string.IsNullOrEmpty(logPath) ? string.Empty : $"（日志：{logPath}）"));
                }
            }

            return result;
        }

        public static BatchTaskRunResult ValidateExports(
            Document doc,
            IBatchTaskOptions viewModel,
            bool multipleDocuments = false,
            ISet<string>? plannedOutputReservations = null)
        {
            var result = new BatchTaskRunResult();
            if (doc == null || !doc.IsValidObject)
            {
                result.Errors.Add("模型无效或已关闭。");
                return result;
            }
            if (doc.IsFamilyDocument)
            {
                result.Errors.Add("族文档不支持本批处理导出。");
                return result;
            }

            if (viewModel.EnableNwc)
            {
                if (!OptionalFunctionalityUtils.IsNavisworksExporterAvailable())
                {
                    result.Errors.Add("NWC：Navisworks Exporter 未安装。");
                }
                else if (!string.Equals(viewModel.SelectedNwc3DView, BatchTaskViewModel.WholeModelOption, StringComparison.Ordinal)
                         && FindView3D(doc, viewModel.SelectedNwc3DView, out string error) == null)
                {
                    result.Errors.Add("NWC：" + error);
                }
                else
                {
                    result.Successes.Add("NWC 配置可用");
                }
            }

            if (viewModel.EnableIfc)
            {
                if (!OptionalFunctionalityUtils.IsIFCAvailable())
                {
                    result.Errors.Add("IFC：当前 Revit 安装缺少 IFC 功能模块。");
                }
                else if (!string.Equals(viewModel.SelectedIfc3DView, BatchTaskViewModel.WholeModelOption, StringComparison.Ordinal)
                    && FindView3D(doc, viewModel.SelectedIfc3DView, out string error) == null)
                {
                    result.Errors.Add("IFC：" + error);
                }
                else
                {
                    result.Successes.Add("IFC 配置可用");
                }
            }

            if (viewModel.EnableDwg)
            {
                if (!OptionalFunctionalityUtils.IsDWGExportAvailable())
                {
                    result.Errors.Add("DWG：当前 Revit 安装缺少 DWG 导出模块。");
                }
                ViewSelectionResult selection = GetViewIds(doc, viewModel.SelectedDwgViewSheetSet);
                if (selection.Error != null)
                    result.Errors.Add("DWG：" + selection.Error);
                string? dwgError;
                using (DWGExportOptions options = CreateDwgOptions(doc, viewModel, out dwgError))
                {
                    if (dwgError != null)
                        result.Errors.Add("DWG：" + dwgError);
                }
                if (OptionalFunctionalityUtils.IsDWGExportAvailable()
                    && selection.Error == null
                    && dwgError == null)
                {
                    result.Successes.Add($"DWG：{selection.ViewIds.Count} 个视图/图纸可导出");
                }
            }

#if REVIT2022_OR_GREATER
            if (viewModel.EnablePdf)
            {
                if (!OptionalFunctionalityUtils.IsGraphicsAvailable())
                    result.Errors.Add("PDF：当前 Revit 安装缺少图形/打印导出功能。");
                ViewSelectionResult selection = GetViewIds(doc, viewModel.SelectedPdfViewSheetSet);
                if (selection.Error != null)
                    result.Errors.Add("PDF：" + selection.Error);
                string? pdfError;
                using (PDFExportOptions options = CreatePdfOptions(doc, viewModel, out pdfError))
                {
                    if (pdfError != null)
                        result.Errors.Add("PDF：" + pdfError);
                }
                if (OptionalFunctionalityUtils.IsGraphicsAvailable()
                    && selection.Error == null
                    && pdfError == null)
                {
                    result.Successes.Add($"PDF：{selection.ViewIds.Count} 个视图/图纸可导出");
                }
            }
#endif

            if (viewModel.EnablePrintPdf)
            {
                bool graphicsAvailable = OptionalFunctionalityUtils.IsGraphicsAvailable();
                if (!graphicsAvailable)
                    result.Errors.Add("打印 PDF：当前 Revit 安装缺少图形/打印功能。");
                ViewSelectionResult selection = GetViewIds(doc, viewModel.SelectedPrintPdfViewSheetSet);
                if (selection.Error != null)
                    result.Errors.Add("打印 PDF：" + selection.Error);

                string printer = viewModel.SelectedPrintPdfPrinterName?.Trim() ?? string.Empty;
                bool printConfigOk = true;
                if (string.IsNullOrWhiteSpace(printer) || printer.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    result.Errors.Add("打印 PDF：未选择有效 PDF 虚拟打印机。");
                    printConfigOk = false;
                }

                if (!string.IsNullOrWhiteSpace(viewModel.SelectedPrintPdfSetup)
                    && viewModel.SelectedPrintPdfSetup != BatchTaskViewModel.CurrentPrintSetupOption)
                {
                    bool exists = new FilteredElementCollector(doc)
                        .OfClass(typeof(PrintSetting))
                        .Cast<PrintSetting>()
                        .Any(value => string.Equals(value.Name, viewModel.SelectedPrintPdfSetup, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                    {
                        result.Errors.Add($"打印 PDF：当前模型不存在打印设置“{viewModel.SelectedPrintPdfSetup}”。");
                        printConfigOk = false;
                    }
                }

                string restoreWarning = string.Empty;
                if (printConfigOk)
                {
                    if (!ValidatePrintDriver(doc, viewModel, printer, out string driverError, out restoreWarning))
                    {
                        result.Errors.Add("打印 PDF：" + driverError);
                        printConfigOk = false;
                    }
                }
                if (!string.IsNullOrWhiteSpace(restoreWarning))
                    result.Warnings.Add("打印 PDF 预检：" + restoreWarning);

                if (string.Equals(printer, "Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add(
                        "打印 PDF：Microsoft Print to PDF 常见配置会弹出‘另存为’窗口；" +
                        "它只能在本机实测确认能否无人值守。若弹窗，请改用支持 PrintToFileName 的 PDF 驱动。");
                }

                if (graphicsAvailable && selection.Error == null && printConfigOk)
                    result.Successes.Add($"打印 PDF：{selection.ViewIds.Count} 个视图/图纸；打印机 {printer}");
            }

            if (result.ErrorCount == 0)
            {
                ISet<string> planReservations = plannedOutputReservations
                    ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                ValidateOutputPlan(doc, viewModel, multipleDocuments, planReservations, result);
            }

            return result;
        }

        public static BatchTaskRunResult RunExports(
            Document doc,
            IBatchTaskOptions viewModel,
            bool multipleDocuments,
            Action<string>? progressCallback,
            ISet<string>? batchOutputReservations = null)
        {
            var result = new BatchTaskRunResult();
            if (!viewModel.HasAnyExportEnabled)
                return result;
            if (doc.IsFamilyDocument)
            {
                result.Errors.Add("族文档不支持本批处理导出。");
                return result;
            }

            BatchTaskRunResult preflight = ValidateExports(doc, viewModel);
            if (preflight.ErrorCount > 0)
            {
                result.Errors.Add("执行前预检未通过，本模型未产生任何导出/打印文件。");
                foreach (string error in preflight.Errors)
                    result.Errors.Add(error);
                foreach (string warning in preflight.Warnings)
                    result.Warnings.Add(warning);
                return result;
            }
            foreach (string warning in preflight.Warnings)
                result.Warnings.Add(warning);

            string outputFolder;
            try
            {
                outputFolder = GetDocumentOutputFolder(doc, viewModel, multipleDocuments);
                Directory.CreateDirectory(outputFolder);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"无法创建输出目录：{ex.Message}");
                return result;
            }

            if (viewModel.EnableNwc)
                ExportNwc(doc, viewModel, outputFolder, result, progressCallback, batchOutputReservations);
            if (viewModel.EnableIfc)
                ExportIfc(doc, viewModel, outputFolder, result, progressCallback, batchOutputReservations);
            if (viewModel.EnableDwg)
                ExportDwg(doc, viewModel, outputFolder, result, progressCallback, batchOutputReservations);
            if (viewModel.EnablePdf)
                ExportPdf(doc, viewModel, outputFolder, result, progressCallback, batchOutputReservations);
            if (viewModel.EnablePrintPdf)
                PrintPdf(doc, viewModel, outputFolder, result, progressCallback, batchOutputReservations);

            return result;
        }

        private static void ExportNwc(
            Document doc,
            IBatchTaskOptions viewModel,
            string outputFolder,
            BatchTaskRunResult result,
            Action<string>? progressCallback,
            ISet<string>? batchOutputReservations)
        {
            progressCallback?.Invoke("导出 NWC...");
            try
            {
                if (!OptionalFunctionalityUtils.IsNavisworksExporterAvailable())
                {
                    result.Errors.Add("NWC 导出器未安装。请安装与当前 Revit 版本匹配的 Navisworks Exporter。");
                    return;
                }

                bool wholeModel = string.Equals(viewModel.SelectedNwc3DView, BatchTaskViewModel.WholeModelOption, StringComparison.Ordinal);
                View3D? view = null;
                if (!wholeModel)
                {
                    view = FindView3D(doc, viewModel.SelectedNwc3DView, out string viewError);
                    if (view == null)
                    {
                        result.Errors.Add("NWC：" + viewError);
                        return;
                    }
                }

                string fileName = BatchExportNaming.ResolveBaseName(viewModel.NwcFileName, doc, view, 0);
                string expectedPath = Path.Combine(outputFolder, fileName + ".nwc");
                if (File.Exists(expectedPath) && !viewModel.OverwriteExisting)
                {
                    result.Errors.Add($"NWC：目标文件已存在，且未勾选覆盖：{expectedPath}");
                    return;
                }

                string stagingFolder = CreateStagingFolder(outputFolder, "nwc");
                try
                {
                using (var options = new NavisworksExportOptions
                {
                    ExportScope = wholeModel ? NavisworksExportScope.Model : NavisworksExportScope.View,
                    ViewId = view?.Id ?? ElementId.InvalidElementId,
                    Coordinates = string.Equals(viewModel.SelectedNwcCoordinates, "内部坐标", StringComparison.Ordinal)
                        ? NavisworksCoordinates.Internal
                        : NavisworksCoordinates.Shared,
                    ExportLinks = viewModel.NwcExportLinks,
                    ConvertElementProperties = viewModel.NwcConvertElementProperties,
                    ExportElementIds = viewModel.NwcExportElementIds,
                    ExportRoomAsAttribute = viewModel.NwcExportRoomAsAttribute,
                    ExportRoomGeometry = viewModel.NwcExportRoomGeometry,
                    DivideFileIntoLevels = viewModel.NwcDivideFileIntoLevels
                })
                {
                    doc.Export(stagingFolder, fileName, options);
                }

                string stagedPath = Path.Combine(stagingFolder, fileName + ".nwc");
                if (!IsNonEmptyFile(stagedPath))
                {
                    result.Errors.Add($"NWC 导出未生成非空预期文件：{stagedPath}");
                    return;
                }
                if (!CommitStagedFiles(stagingFolder, outputFolder, viewModel.OverwriteExisting, batchOutputReservations, out List<string> committed, out string commitError))
                {
                    result.Errors.Add("NWC：" + commitError);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(commitError))
                    result.Warnings.Add("NWC：" + commitError);
                string nwcScope = wholeModel ? "整个模型" : $"视图：{view!.Name}";
                result.Successes.Add($"NWC：{expectedPath}（{nwcScope}；坐标：{viewModel.SelectedNwcCoordinates}）");
                result.OutputFiles.AddRange(committed);
                }
                finally
                {
                    DeleteDirectoryBestEffort(stagingFolder);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError($"NWC 导出：{doc.Title}", ex);
                result.Errors.Add($"NWC 导出失败：{ex.Message}");
            }
        }

        private static void ExportIfc(
            Document doc,
            IBatchTaskOptions viewModel,
            string outputFolder,
            BatchTaskRunResult result,
            Action<string>? progressCallback,
            ISet<string>? batchOutputReservations)
        {
            progressCallback?.Invoke("导出 IFC...");
            try
            {
                if (!OptionalFunctionalityUtils.IsIFCAvailable())
                {
                    result.Errors.Add("IFC：当前 Revit 安装缺少 IFC 功能模块。");
                    return;
                }

                bool wholeModel = string.Equals(viewModel.SelectedIfc3DView, BatchTaskViewModel.WholeModelOption, StringComparison.Ordinal);
                View3D? view = null;
                if (!wholeModel)
                {
                    view = FindView3D(doc, viewModel.SelectedIfc3DView, out string viewError);
                    if (view == null)
                    {
                        result.Errors.Add("IFC：" + viewError);
                        return;
                    }
                }

                string fileName = BatchExportNaming.ResolveBaseName(viewModel.IfcFileName, doc, view, 0);
                string expectedPath = Path.Combine(outputFolder, fileName + ".ifc");
                if (File.Exists(expectedPath) && !viewModel.OverwriteExisting)
                {
                    result.Errors.Add($"IFC：目标文件已存在，且未勾选覆盖：{expectedPath}");
                    return;
                }

                string stagingFolder = CreateStagingFolder(outputFolder, "ifc");
                try
                {
                bool exported;
                using (var options = new IFCExportOptions
                {
                    FileVersion = GetIfcVersion(viewModel.SelectedIfcVersion),
                    FilterViewId = view?.Id ?? ElementId.InvalidElementId,
                    ExportBaseQuantities = viewModel.IfcExportBaseQuantities,
                    WallAndColumnSplitting = viewModel.IfcSplitWallsColumns,
                    SpaceBoundaryLevel = viewModel.IfcSpaceBoundaryLevel
                })
                using (var transaction = new Transaction(doc, "导出 IFC（临时事务）"))
                {
                    if (transaction.Start() != TransactionStatus.Started)
                    {
                        result.Errors.Add("IFC：无法启动导出所需的临时事务。");
                        return;
                    }

                    try
                    {
                        exported = doc.Export(stagingFolder, fileName, options);
                    }
                    finally
                    {
                        if (transaction.GetStatus() == TransactionStatus.Started)
                            transaction.RollBack();
                    }
                }

                string stagedPath = Path.Combine(stagingFolder, fileName + ".ifc");
                if (!exported || !IsNonEmptyFile(stagedPath))
                {
                    result.Errors.Add($"IFC 导出返回失败或未生成非空预期文件：{stagedPath}");
                    return;
                }
                if (!CommitStagedFiles(stagingFolder, outputFolder, viewModel.OverwriteExisting, batchOutputReservations, out List<string> committed, out string commitError))
                {
                    result.Errors.Add("IFC：" + commitError);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(commitError))
                    result.Warnings.Add("IFC：" + commitError);
                string ifcScope = wholeModel ? "整个模型" : $"视图：{view!.Name}";
                result.Successes.Add($"IFC：{expectedPath}（{ifcScope}；版本：{viewModel.SelectedIfcVersion}）");
                result.OutputFiles.AddRange(committed);
                }
                finally
                {
                    DeleteDirectoryBestEffort(stagingFolder);
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError($"IFC 导出：{doc.Title}", ex);
                result.Errors.Add($"IFC 导出失败：{ex.Message}");
            }
        }

        private static void ExportDwg(
            Document doc,
            IBatchTaskOptions viewModel,
            string outputFolder,
            BatchTaskRunResult result,
            Action<string>? progressCallback,
            ISet<string>? batchOutputReservations)
        {
            progressCallback?.Invoke("导出 DWG...");
            try
            {
                if (!OptionalFunctionalityUtils.IsDWGExportAvailable())
                {
                    result.Errors.Add("DWG：当前 Revit 安装缺少 DWG 导出模块。");
                    return;
                }

                ViewSelectionResult selection = GetViewIds(doc, viewModel.SelectedDwgViewSheetSet);
                if (selection.Error != null)
                {
                    result.Errors.Add("DWG：" + selection.Error);
                    return;
                }

                using (DWGExportOptions options = CreateDwgOptions(doc, viewModel, out string? optionError))
                {
                    if (optionError != null)
                    {
                        result.Errors.Add("DWG：" + optionError);
                        return;
                    }

                    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int success = 0;
                    for (int index = 0; index < selection.ViewIds.Count; index++)
                    {
                        ElementId viewId = selection.ViewIds[index];
                        View? view = doc.GetElement(viewId) as View;
                        if (view == null)
                        {
                            result.Errors.Add($"DWG：视图已删除，ElementId={viewId}");
                            continue;
                        }

                        string baseName = BatchExportNaming.ResolveBaseName(viewModel.DwgFileName, doc, view, index);
                        baseName = BatchExportNaming.EnsureUniqueBaseName(baseName, usedNames);
                        string expectedMain = Path.Combine(outputFolder, baseName + ".dwg");
                        if (File.Exists(expectedMain) && !viewModel.OverwriteExisting)
                        {
                            result.Errors.Add($"DWG：目标文件已存在，且未勾选覆盖：{expectedMain}");
                            continue;
                        }

                        string stagingFolder = CreateStagingFolder(outputFolder, "dwg");
                        try
                        {
                            bool exported = doc.Export(stagingFolder, baseName, new List<ElementId> { viewId }, options);
                            if (!exported)
                            {
                                result.Errors.Add($"DWG：导出失败：{view.Name}");
                                continue;
                            }

                            List<string> created = Directory.EnumerateFiles(stagingFolder, "*", SearchOption.TopDirectoryOnly).ToList();
                            if (created.Count == 0)
                            {
                                result.Errors.Add($"DWG：导出返回成功，但暂存目录为空：{view.Name}");
                                continue;
                            }

                            string stagedMain = Path.Combine(stagingFolder, baseName + ".dwg");
                            if (!IsNonEmptyFile(stagedMain))
                            {
                                result.Errors.Add($"DWG：未生成预期主文件，不能确认命名正确：{stagedMain}");
                                continue;
                            }
                            if (!CommitStagedFiles(stagingFolder, outputFolder, viewModel.OverwriteExisting, batchOutputReservations, out List<string> committed, out string commitError))
                            {
                                result.Errors.Add($"DWG：{view.Name}：{commitError}");
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(commitError))
                                result.Warnings.Add($"DWG：{view.Name}：{commitError}");
                            success++;
                            foreach (string path in committed)
                                result.OutputFiles.Add(path);
                            result.Successes.Add($"DWG：{view.Name} → {Path.GetFileName(expectedMain)}（{committed.Count} 个文件）");
                        }
                        finally
                        {
                            DeleteDirectoryBestEffort(stagingFolder);
                        }
                    }

                    if (success == 0 && selection.ViewIds.Count > 0)
                        result.Errors.Add("DWG：没有任何视图/图纸成功导出。");
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError($"DWG 导出：{doc.Title}", ex);
                result.Errors.Add($"DWG 导出失败：{ex.Message}");
            }
        }

        private static DWGExportOptions CreateDwgOptions(
            Document doc,
            IBatchTaskOptions viewModel,
            out string? error)
        {
            error = null;
            DWGExportOptions options;
            if (!string.IsNullOrWhiteSpace(viewModel.SelectedDwgSetup)
                && viewModel.SelectedDwgSetup != BatchTaskViewModel.DefaultSetupOption)
            {
                ExportDWGSettings? settings = new FilteredElementCollector(doc)
                    .OfClass(typeof(ExportDWGSettings))
                    .Cast<ExportDWGSettings>()
                    .FirstOrDefault(value => string.Equals(
                        value.Name,
                        viewModel.SelectedDwgSetup,
                        StringComparison.OrdinalIgnoreCase));
                if (settings == null)
                {
                    error = $"当前模型不存在导出设置“{viewModel.SelectedDwgSetup}”。";
                    return new DWGExportOptions();
                }
                options = settings.GetDWGExportOptions();
            }
            else
            {
                options = new DWGExportOptions();
            }

            if (string.Equals(viewModel.SelectedDwgCoordinateMode, "共享坐标", StringComparison.Ordinal))
                options.SharedCoords = true;
            else if (string.Equals(viewModel.SelectedDwgCoordinateMode, "项目坐标", StringComparison.Ordinal))
                options.SharedCoords = false;

            options.MergedViews = viewModel.DwgMergedViews;
            return options;
        }

        private static void ExportPdf(
            Document doc,
            IBatchTaskOptions viewModel,
            string outputFolder,
            BatchTaskRunResult result,
            Action<string>? progressCallback,
            ISet<string>? batchOutputReservations)
        {
            progressCallback?.Invoke("导出 PDF...");
#if REVIT2022_OR_GREATER
            try
            {
                ViewSelectionResult selection = GetViewIds(doc, viewModel.SelectedPdfViewSheetSet);
                if (selection.Error != null)
                {
                    result.Errors.Add("PDF：" + selection.Error);
                    return;
                }

                using (PDFExportOptions options = CreatePdfOptions(doc, viewModel, out string? optionError))
                {
                    if (optionError != null)
                    {
                        result.Errors.Add("PDF：" + optionError);
                        return;
                    }

                    if (viewModel.PdfCombine)
                    {
                        string baseName = BatchExportNaming.ResolveBaseName(viewModel.PdfFileName, doc, null, 0);
                        string finalPath = Path.Combine(outputFolder, baseName + ".pdf");
                        if (!ExportPdfExact(doc, selection.ViewIds, options, outputFolder, finalPath, viewModel.OverwriteExisting, batchOutputReservations, out string error))
                        {
                            result.Errors.Add("PDF：" + error);
                            return;
                        }
                        if (!string.IsNullOrWhiteSpace(error)) result.Warnings.Add("PDF：" + error);
                        result.OutputFiles.Add(finalPath);
                        result.Successes.Add($"PDF：{finalPath}（合并 {selection.ViewIds.Count} 个视图/图纸）");
                    }
                    else
                    {
                        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        int success = 0;
                        for (int index = 0; index < selection.ViewIds.Count; index++)
                        {
                            ElementId id = selection.ViewIds[index];
                            View? view = doc.GetElement(id) as View;
                            if (view == null)
                            {
                                result.Errors.Add($"PDF：视图已删除，ElementId={id}");
                                continue;
                            }

                            string baseName = BatchExportNaming.ResolveBaseName(viewModel.PdfFileName, doc, view, index);
                            baseName = BatchExportNaming.EnsureUniqueBaseName(baseName, usedNames);
                            string finalPath = Path.Combine(outputFolder, baseName + ".pdf");
                            if (!ExportPdfExact(doc, new List<ElementId> { id }, options, outputFolder, finalPath, viewModel.OverwriteExisting, batchOutputReservations, out string error))
                            {
                                result.Errors.Add($"PDF：{view.Name}：{error}");
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(error)) result.Warnings.Add("PDF：" + error);
                            success++;
                            result.OutputFiles.Add(finalPath);
                            result.Successes.Add($"PDF：{view.Name} → {Path.GetFileName(finalPath)}");
                        }

                        if (success == 0)
                            result.Errors.Add("PDF：没有任何视图/图纸成功导出。");
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError($"PDF 导出：{doc.Title}", ex);
                result.Errors.Add($"PDF 导出失败：{ex.Message}");
            }
#else
            result.Errors.Add("PDF 导出需要 Revit 2022 或更高版本；Revit 2021 请启用“打印 PDF”。");
#endif
        }

#if REVIT2022_OR_GREATER
        private static PDFExportOptions CreatePdfOptions(
            Document doc,
            IBatchTaskOptions viewModel,
            out string? error)
        {
            error = null;
            if (!string.IsNullOrWhiteSpace(viewModel.SelectedPdfSetup)
                && viewModel.SelectedPdfSetup != BatchTaskViewModel.DefaultSetupOption)
            {
                ExportPDFSettings? settings = new FilteredElementCollector(doc)
                    .OfClass(typeof(ExportPDFSettings))
                    .Cast<ExportPDFSettings>()
                    .FirstOrDefault(value => string.Equals(
                        value.Name,
                        viewModel.SelectedPdfSetup,
                        StringComparison.OrdinalIgnoreCase));
                if (settings == null)
                {
                    error = $"当前模型不存在 PDF 导出设置“{viewModel.SelectedPdfSetup}”。";
                    return new PDFExportOptions();
                }
                return settings.GetOptions();
            }
            return new PDFExportOptions();
        }

        private static bool ExportPdfExact(
            Document doc,
            IList<ElementId> viewIds,
            PDFExportOptions options,
            string outputFolder,
            string finalPath,
            bool overwrite,
            ISet<string>? batchOutputReservations,
            out string error)
        {
            error = string.Empty;
            if (!CanUseBatchOutputPath(finalPath, batchOutputReservations, out error))
                return false;

            bool targetExists = File.Exists(finalPath);
            if (targetExists && !overwrite)
            {
                error = $"目标文件已存在，且未勾选覆盖：{finalPath}";
                return false;
            }

            string finalBaseName = Path.GetFileNameWithoutExtension(finalPath);
            string exportBaseName = targetExists
                ? CreateTemporaryExportBaseName(finalBaseName)
                : finalBaseName;
            string producedPath = Path.Combine(outputFolder, exportBaseName + ".pdf");
            DeleteFileBestEffort(producedPath);

            options.Combine = true;
            options.FileName = exportBaseName;
            options.StopOnError = true;
#if REVIT2025_OR_GREATER
            // Batch Publisher must be deterministic: a saved setup may have background export enabled.
            // Disable it so Document.Export only returns after the expected file is finalized.
            options.SetExportInBackground(false);
#endif

            bool exported;
            try
            {
                exported = doc.Export(outputFolder, viewIds, options);
            }
            catch
            {
                DeleteFileBestEffort(producedPath);
                throw;
            }

            if (!exported || !IsNonEmptyFile(producedPath))
            {
                DeleteFileBestEffort(producedPath);
                error = $"导出返回失败或未生成非空预期文件：{producedPath}";
                return false;
            }

            if (!string.Equals(producedPath, finalPath, StringComparison.OrdinalIgnoreCase)
                && !ReplaceOutputSafely(producedPath, finalPath, overwrite, out error))
                return false;

            AddBatchOutputReservation(finalPath, batchOutputReservations);
            return true;
        }
#endif

        private static bool ValidatePrintDriver(
            Document doc,
            IBatchTaskOptions viewModel,
            string printerName,
            out string error,
            out string restoreWarning)
        {
            error = string.Empty;
            restoreWarning = string.Empty;
            PrintManagerSnapshot? snapshot = null;
            try
            {
                snapshot = CapturePrintManagerState(doc);
                PrintManager manager = PreparePrintManager(doc, viewModel, printerName, out string? prepareError);
                if (prepareError != null)
                {
                    error = prepareError;
                    return false;
                }

                if (!string.Equals(manager.PrinterName, printerName, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"打印机切换失败。请求“{printerName}”，实际“{manager.PrinterName}”。";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = $"无法使用打印机“{printerName}”：{ex.Message}";
                return false;
            }
            finally
            {
                if (snapshot != null && !RestorePrintManagerState(doc, snapshot, out string restoreError))
                    restoreWarning = restoreError;
            }
        }

        private static void PrintPdf(
            Document doc,
            IBatchTaskOptions viewModel,
            string outputFolder,
            BatchTaskRunResult result,
            Action<string>? progressCallback,
            ISet<string>? batchOutputReservations)
        {
            progressCallback?.Invoke("打印 PDF...");

            ViewSelectionResult selection = GetViewIds(doc, viewModel.SelectedPrintPdfViewSheetSet);
            if (selection.Error != null)
            {
                result.Errors.Add("打印 PDF：" + selection.Error);
                return;
            }

            string printerName = viewModel.SelectedPrintPdfPrinterName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(printerName))
            {
                result.Errors.Add("打印 PDF：请选择或输入一个 PDF 虚拟打印机。");
                return;
            }
            if (printerName.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) < 0)
            {
                result.Errors.Add($"打印 PDF：为避免误打印到实体打印机，仅允许名称包含“PDF”的打印机。当前：{printerName}");
                return;
            }

            PrintManagerSnapshot? snapshot = null;
            try
            {
                snapshot = CapturePrintManagerState(doc);
                PrintManager printManager = PreparePrintManager(doc, viewModel, printerName, out string? prepareError);
                if (prepareError != null)
                {
                    result.Errors.Add("打印 PDF：" + prepareError);
                    return;
                }

                if (viewModel.PrintPdfCombine)
                {
                    string baseName = BatchExportNaming.ResolveBaseName(viewModel.PrintPdfFileName, doc, null, 0);
                    string finalPath = Path.Combine(outputFolder, baseName + ".pdf");
                    if (!PrintViewIdsToExactPdf(doc, printManager, selection.ViewIds, outputFolder, finalPath, viewModel.OverwriteExisting, batchOutputReservations, out string error))
                    {
                        result.Errors.Add("打印 PDF：" + error);
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(error)) result.Warnings.Add("打印 PDF：" + error);
                    result.Successes.Add($"打印 PDF：{finalPath}（打印机：{printerName}；合并 {selection.ViewIds.Count} 项）");
                    result.OutputFiles.Add(finalPath);
                }
                else
                {
                    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int success = 0;
                    for (int index = 0; index < selection.ViewIds.Count; index++)
                    {
                        ElementId id = selection.ViewIds[index];
                        View? view = doc.GetElement(id) as View;
                        if (view == null)
                        {
                            result.Errors.Add($"打印 PDF：视图已删除，ElementId={id}");
                            continue;
                        }

                        string baseName = BatchExportNaming.ResolveBaseName(viewModel.PrintPdfFileName, doc, view, index);
                        baseName = BatchExportNaming.EnsureUniqueBaseName(baseName, usedNames);
                        string finalPath = Path.Combine(outputFolder, baseName + ".pdf");
                        if (!PrintViewIdsToExactPdf(doc, printManager, new List<ElementId> { id }, outputFolder, finalPath, viewModel.OverwriteExisting, batchOutputReservations, out string error))
                        {
                            result.Errors.Add($"打印 PDF：{view.Name}：{error}");
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(error)) result.Warnings.Add("打印 PDF：" + error);
                        success++;
                        result.Successes.Add($"打印 PDF：{view.Name} → {Path.GetFileName(finalPath)}");
                        result.OutputFiles.Add(finalPath);
                    }

                    if (success == 0)
                        result.Errors.Add("打印 PDF：没有任何视图/图纸成功打印。");
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError($"打印 PDF：{doc.Title}", ex);
                result.Errors.Add($"打印 PDF 失败：{ex.Message}");
            }
            finally
            {
                if (snapshot != null
                    && !RestorePrintManagerState(doc, snapshot, out string restoreError))
                {
                    result.Warnings.Add("打印 PDF：任务已结束，但未能完全恢复原 Revit 打印状态。" + restoreError);
                }
            }
        }

        private static PrintManager PreparePrintManager(
            Document doc,
            IBatchTaskOptions viewModel,
            string printerName,
            out string? error)
        {
            error = null;
            PrintManager printManager = doc.PrintManager;
            printManager.SelectNewPrintDriver(printerName);
            printManager = doc.PrintManager;
            if (!string.Equals(printManager.PrinterName, printerName, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Revit 未切换到指定打印机。请求“{printerName}”，实际“{printManager.PrinterName}”。";
                return printManager;
            }

            if (!string.IsNullOrWhiteSpace(viewModel.SelectedPrintPdfSetup)
                && viewModel.SelectedPrintPdfSetup != BatchTaskViewModel.CurrentPrintSetupOption)
            {
                PrintSetting? printSetting = new FilteredElementCollector(doc)
                    .OfClass(typeof(PrintSetting))
                    .Cast<PrintSetting>()
                    .FirstOrDefault(value => string.Equals(
                        value.Name,
                        viewModel.SelectedPrintPdfSetup,
                        StringComparison.OrdinalIgnoreCase));
                if (printSetting == null)
                {
                    error = $"当前模型不存在打印设置“{viewModel.SelectedPrintPdfSetup}”。";
                    return printManager;
                }
                printManager.PrintSetup.CurrentPrintSetting = printSetting;
            }

            printManager.PrintRange = PrintRange.Select;
            printManager.PrintToFile = true;
            printManager.CombinedFile = true;
            printManager.Apply();
            return doc.PrintManager;
        }

        private static bool PrintViewIdsToExactPdf(
            Document doc,
            PrintManager printManager,
            IList<ElementId> viewIds,
            string outputFolder,
            string finalPath,
            bool overwrite,
            ISet<string>? batchOutputReservations,
            out string error)
        {
            error = string.Empty;
            if (!CanUseBatchOutputPath(finalPath, batchOutputReservations, out error))
                return false;
            if (File.Exists(finalPath) && !overwrite)
            {
                error = $"目标文件已存在，且未勾选覆盖：{finalPath}";
                return false;
            }

            string temporaryPath = Path.Combine(
                outputFolder,
                CreateTemporaryExportBaseName(Path.GetFileNameWithoutExtension(finalPath)) + ".pdf");
            DeleteFileBestEffort(temporaryPath);

            var viewSet = new ViewSet();
            foreach (ElementId viewId in viewIds)
            {
                View? view = doc.GetElement(viewId) as View;
                if (view == null || !view.CanBePrinted)
                {
                    error = $"视图不可打印或已删除，ElementId={viewId}";
                    return false;
                }
                viewSet.Insert(view);
            }

            printManager = doc.PrintManager;
            printManager.PrintRange = PrintRange.Select;
            printManager.Apply();
            printManager = doc.PrintManager;
            ViewSheetSetting viewSheetSetting = printManager.ViewSheetSetting;
            viewSheetSetting.CurrentViewSheetSet = viewSheetSetting.InSession;
            viewSheetSetting.CurrentViewSheetSet.Views = viewSet;
            printManager.PrintToFile = true;
            printManager.CombinedFile = true;
            printManager.PrintToFileName = temporaryPath;

            bool submitted = printManager.SubmitPrint();
            if (!submitted)
            {
                DeleteFileBestEffort(temporaryPath);
                error = "Revit SubmitPrint 返回失败。请检查打印机、打印设置、纸张和视图可打印状态。";
                return false;
            }

            if (!WaitForFileReady(temporaryPath, 60000))
            {
                DeleteFileBestEffort(temporaryPath);
                error = $"打印任务已提交，但 60 秒内未生成可读取 PDF。打印驱动可能弹出另存为窗口，或不支持 PrintToFileName：{temporaryPath}";
                return false;
            }

            if (!ReplaceOutputSafely(temporaryPath, finalPath, overwrite, out error))
                return false;
            AddBatchOutputReservation(finalPath, batchOutputReservations);
            return true;
        }

        private static PrintManagerSnapshot CapturePrintManagerState(Document doc)
        {
            PrintManager printManager = doc.PrintManager;
            var snapshot = new PrintManagerSnapshot
            {
                PrinterName = printManager.PrinterName ?? string.Empty,
                PrintRange = printManager.PrintRange
            };

            try
            {
                snapshot.PrintToFile = printManager.PrintToFile;
                snapshot.HasPrintToFile = true;
            }
            catch
            {
                // Some printer drivers expose only a subset of Revit global print properties.
            }

            try
            {
                snapshot.CombinedFile = printManager.CombinedFile;
                snapshot.HasCombinedFile = true;
            }
            catch
            {
                // CombinedFile is not valid for every physical/non-virtual printer.
            }

            try
            {
                snapshot.PrintToFileName = printManager.PrintToFileName ?? string.Empty;
                snapshot.HasPrintToFileName = true;
            }
            catch
            {
                snapshot.PrintToFileName = string.Empty;
            }

            try
            {
                IPrintSetting currentPrintSetting = printManager.PrintSetup.CurrentPrintSetting;
                if (currentPrintSetting is PrintSetting savedPrintSetting)
                    snapshot.PrintSettingName = savedPrintSetting.Name;
                else
                    snapshot.PrintSettingWasInSession = true;
            }
            catch
            {
                snapshot.PrintSettingWasInSession = true;
            }

            try
            {
                IViewSheetSet currentViewSheetSet = printManager.ViewSheetSetting.CurrentViewSheetSet;
                if (currentViewSheetSet is ViewSheetSet savedViewSheetSet)
                {
                    snapshot.ViewSheetSetName = savedViewSheetSet.Name;
                }
                else
                {
                    snapshot.ViewSheetSetWasInSession = true;
                    foreach (View view in currentViewSheetSet.Views)
                        snapshot.InSessionViewIds.Add(view.Id);
                }
            }
            catch
            {
                snapshot.ViewSheetSetWasInSession = true;
            }

            return snapshot;
        }

        private static bool RestorePrintManagerState(
            Document doc,
            PrintManagerSnapshot snapshot,
            out string error)
        {
            error = string.Empty;
            try
            {
                PrintManager printManager = doc.PrintManager;
                if (!string.IsNullOrWhiteSpace(snapshot.PrinterName)
                    && !string.Equals(printManager.PrinterName, snapshot.PrinterName, StringComparison.OrdinalIgnoreCase))
                {
                    printManager.SelectNewPrintDriver(snapshot.PrinterName);
                    printManager = doc.PrintManager;
                }

                if (snapshot.PrintSettingWasInSession)
                {
                    printManager.PrintSetup.CurrentPrintSetting = printManager.PrintSetup.InSession;
                }
                else if (!string.IsNullOrWhiteSpace(snapshot.PrintSettingName))
                {
                    PrintSetting? savedPrintSetting = new FilteredElementCollector(doc)
                        .OfClass(typeof(PrintSetting))
                        .Cast<PrintSetting>()
                        .FirstOrDefault(value => string.Equals(
                            value.Name,
                            snapshot.PrintSettingName,
                            StringComparison.OrdinalIgnoreCase));
                    if (savedPrintSetting != null)
                        printManager.PrintSetup.CurrentPrintSetting = savedPrintSetting;
                }

                // ViewSheetSetting.Views is only assignable while PrintRange is Select.
                printManager.PrintRange = PrintRange.Select;
                printManager.Apply();
                printManager = doc.PrintManager;

                ViewSheetSetting viewSheetSetting = printManager.ViewSheetSetting;
                if (snapshot.ViewSheetSetWasInSession)
                {
                    viewSheetSetting.CurrentViewSheetSet = viewSheetSetting.InSession;
                    var originalViews = new ViewSet();
                    foreach (ElementId id in snapshot.InSessionViewIds)
                    {
                        if (doc.GetElement(id) is View view)
                            originalViews.Insert(view);
                    }
                    if (originalViews.Size > 0)
                        viewSheetSetting.CurrentViewSheetSet.Views = originalViews;
                }
                else if (!string.IsNullOrWhiteSpace(snapshot.ViewSheetSetName))
                {
                    ViewSheetSet? savedViewSheetSet = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheetSet))
                        .Cast<ViewSheetSet>()
                        .FirstOrDefault(value => string.Equals(
                            value.Name,
                            snapshot.ViewSheetSetName,
                            StringComparison.OrdinalIgnoreCase));
                    if (savedViewSheetSet != null)
                        viewSheetSetting.CurrentViewSheetSet = savedViewSheetSet;
                }

                printManager.PrintRange = snapshot.PrintRange;

                var restoreWarnings = new List<string>();
                if (snapshot.HasPrintToFile)
                {
                    try
                    {
                        printManager.PrintToFile = snapshot.PrintToFile;
                    }
                    catch (Exception ex)
                    {
                        restoreWarnings.Add("PrintToFile：" + ex.Message);
                    }
                }

                if (snapshot.HasCombinedFile)
                {
                    try
                    {
                        printManager.CombinedFile = snapshot.CombinedFile;
                    }
                    catch (Exception ex)
                    {
                        // Revit can reject CombinedFile after restoring a physical printer.
                        // Keep restoring all other settings instead of aborting the whole cleanup.
                        restoreWarnings.Add("CombinedFile：" + ex.Message);
                    }
                }

                if (snapshot.HasPrintToFileName)
                {
                    try
                    {
                        printManager.PrintToFileName = snapshot.PrintToFileName ?? string.Empty;
                    }
                    catch (Exception ex)
                    {
                        restoreWarnings.Add("PrintToFileName：" + ex.Message);
                    }
                }

                printManager.Apply();
                if (restoreWarnings.Count > 0)
                {
                    error = "部分打印属性无法恢复：" + string.Join("；", restoreWarnings);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "恢复打印设置失败：" + ex.Message;
                return false;
            }
        }

        private static bool IsNonEmptyFile(string path)
        {
            try
            {
                return File.Exists(path) && new FileInfo(path).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool WaitForFileReady(string path, int timeoutMilliseconds)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.UtcNow <= deadline)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            if (stream.Length > 0)
                                return true;
                        }
                    }
                }
                catch (IOException)
                {
                    // The print driver may still be writing the file.
                }
                catch (UnauthorizedAccessException)
                {
                    // Treat a temporarily locked file the same as an in-progress spool write.
                }

                Thread.Sleep(250);
            }

            return false;
        }

        private static View3D? FindView3D(Document doc, string? viewName, out string error)
        {
            error = string.Empty;
            List<View3D> views = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .Where(value => !value.IsTemplate)
                .ToList();

            if (string.IsNullOrWhiteSpace(viewName)
                || viewName == BatchTaskViewModel.Auto3DViewOption)
            {
                string[] preferred = { "{3D}", "协调", "Coordination", "Navisworks", "NWC" };
                foreach (string name in preferred)
                {
                    View3D? matched = views.FirstOrDefault(value => string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (matched != null)
                        return matched;
                }
                View3D? first = views.OrderBy(value => value.Name, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault();
                if (first == null)
                    error = "当前模型没有可用的非模板三维视图。";
                return first;
            }

            if (viewName == BatchTaskViewModel.Current3DViewOption)
            {
                if (doc.ActiveView is View3D active && !active.IsTemplate)
                    return active;
                error = "当前活动视图不是三维视图。请选择“各模型自动三维视图”或明确的三维视图名称。";
                return null;
            }

            View3D? view = views.FirstOrDefault(value =>
                string.Equals(value.Name, viewName, StringComparison.OrdinalIgnoreCase));
            if (view == null)
                error = $"当前模型找不到三维视图“{viewName}”。可改用“各模型自动三维视图”。";
            return view;
        }

        private static IFCVersion GetIfcVersion(string? versionName)
        {
            switch (versionName)
            {
                case "IFC 2x2": return IFCVersion.IFC2x2;
                case "IFC 2x3": return IFCVersion.IFC2x3;
                case "IFC 2x3 CV 2.0": return IFCVersion.IFC2x3CV2;
                case "IFC BCA": return IFCVersion.IFCBCA;
                case "IFC COBie": return IFCVersion.IFCCOBIE;
                case "IFC 4 DTV": return IFCVersion.IFC4DTV;
                case "IFC 4 RV": return IFCVersion.IFC4RV;
                default: return IFCVersion.IFC4;
            }
        }

        private static ViewSelectionResult GetViewIds(Document doc, string? viewSetName, bool requirePrintable = true)
        {
            var result = new ViewSelectionResult();
            if (string.IsNullOrWhiteSpace(viewSetName)
                || viewSetName == BatchTaskViewModel.AllSheetsOption)
            {
                foreach (ViewSheet sheet in new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Where(sheet => !sheet.IsPlaceholder && sheet.CanBePrinted)
                    .OrderBy(sheet => sheet.SheetNumber, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(sheet => sheet.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    result.ViewIds.Add(sheet.Id);
                }
            }
            else if (viewSetName == BatchTaskViewModel.AllPrintableViewsOption)
            {
                foreach (View view in new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(view => !view.IsTemplate && view.CanBePrinted)
                    .OrderBy(view => view is ViewSheet ? 0 : 1)
                    .ThenBy(view => view.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    result.ViewIds.Add(view.Id);
                }
            }
            else if (viewSetName == BatchTaskViewModel.CurrentViewOption)
            {
                if (doc.ActiveView == null)
                {
                    result.Error = "当前模型没有活动视图。";
                    return result;
                }
                result.ViewIds.Add(doc.ActiveView.Id);
            }
            else
            {
                ViewSheetSet? viewSet = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheetSet))
                    .Cast<ViewSheetSet>()
                    .FirstOrDefault(set => string.Equals(
                        set.Name,
                        viewSetName,
                        StringComparison.OrdinalIgnoreCase));
                if (viewSet == null)
                {
                    result.Error = $"当前模型不存在视图/图纸集“{viewSetName}”。可改用“全部图纸”。";
                    return result;
                }

                foreach (View view in viewSet.Views.Cast<View>()
                    .OrderBy(view => view is ViewSheet sheet ? sheet.SheetNumber : view.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    if (!result.ViewIds.Contains(view.Id))
                        result.ViewIds.Add(view.Id);
                }
            }

            if (result.ViewIds.Count == 0)
            {
                result.Error = viewSetName == BatchTaskViewModel.AllSheetsOption
                    ? "当前模型没有可打印图纸。"
                    : "视图/图纸选择结果为空。";
                return result;
            }

            List<string> invalidViews = result.ViewIds
                .Select(id => doc.GetElement(id) as View)
                .Where(view => view == null || view.IsTemplate || (requirePrintable && !view.CanBePrinted))
                .Select(view => view?.Name ?? "<已删除视图>")
                .ToList();
            if (invalidViews.Count > 0)
            {
                result.Error = requirePrintable
                    ? "包含不可打印视图：" + string.Join("、", invalidViews.Take(10))
                    : "包含无效/模板视图：" + string.Join("、", invalidViews.Take(10));
            }

            return result;
        }

        private static void ValidateOutputPlan(
            Document doc,
            IBatchTaskOptions viewModel,
            bool multipleDocuments,
            ISet<string> plannedOutputReservations,
            BatchTaskRunResult result)
        {
            string outputFolder;
            try
            {
                outputFolder = GetDocumentOutputFolder(doc, viewModel, multipleDocuments);
            }
            catch (Exception ex)
            {
                result.Errors.Add("输出命名预检：无法计算输出目录：" + ex.Message);
                return;
            }

            var paths = new List<(string Label, string Path)>();

            if (viewModel.EnableNwc)
            {
                View3D? view = string.Equals(viewModel.SelectedNwc3DView, BatchTaskViewModel.WholeModelOption, StringComparison.Ordinal)
                    ? null
                    : FindView3D(doc, viewModel.SelectedNwc3DView, out _);
                string baseName = BatchExportNaming.ResolveBaseName(viewModel.NwcFileName, doc, view, 0);
                paths.Add(("NWC", Path.Combine(outputFolder, baseName + ".nwc")));
            }

            if (viewModel.EnableIfc)
            {
                View3D? view = string.Equals(viewModel.SelectedIfc3DView, BatchTaskViewModel.WholeModelOption, StringComparison.Ordinal)
                    ? null
                    : FindView3D(doc, viewModel.SelectedIfc3DView, out _);
                string baseName = BatchExportNaming.ResolveBaseName(viewModel.IfcFileName, doc, view, 0);
                paths.Add(("IFC", Path.Combine(outputFolder, baseName + ".ifc")));
            }

            if (viewModel.EnableDwg)
            {
                ViewSelectionResult selection = GetViewIds(doc, viewModel.SelectedDwgViewSheetSet);
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < selection.ViewIds.Count; index++)
                {
                    if (doc.GetElement(selection.ViewIds[index]) is not View view)
                        continue;
                    string baseName = BatchExportNaming.ResolveBaseName(viewModel.DwgFileName, doc, view, index);
                    baseName = BatchExportNaming.EnsureUniqueBaseName(baseName, usedNames);
                    paths.Add(($"DWG/{view.Name}", Path.Combine(outputFolder, baseName + ".dwg")));
                }
            }

#if REVIT2022_OR_GREATER
            if (viewModel.EnablePdf)
            {
                ViewSelectionResult selection = GetViewIds(doc, viewModel.SelectedPdfViewSheetSet);
                if (viewModel.PdfCombine)
                {
                    string baseName = BatchExportNaming.ResolveBaseName(viewModel.PdfFileName, doc, null, 0);
                    paths.Add(("PDF", Path.Combine(outputFolder, baseName + ".pdf")));
                }
                else
                {
                    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int index = 0; index < selection.ViewIds.Count; index++)
                    {
                        if (doc.GetElement(selection.ViewIds[index]) is not View view)
                            continue;
                        string baseName = BatchExportNaming.ResolveBaseName(viewModel.PdfFileName, doc, view, index);
                        baseName = BatchExportNaming.EnsureUniqueBaseName(baseName, usedNames);
                        paths.Add(($"PDF/{view.Name}", Path.Combine(outputFolder, baseName + ".pdf")));
                    }
                }
            }
#endif

            if (viewModel.EnablePrintPdf)
            {
                ViewSelectionResult selection = GetViewIds(doc, viewModel.SelectedPrintPdfViewSheetSet);
                if (viewModel.PrintPdfCombine)
                {
                    string baseName = BatchExportNaming.ResolveBaseName(viewModel.PrintPdfFileName, doc, null, 0);
                    paths.Add(("打印 PDF", Path.Combine(outputFolder, baseName + ".pdf")));
                }
                else
                {
                    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (int index = 0; index < selection.ViewIds.Count; index++)
                    {
                        if (doc.GetElement(selection.ViewIds[index]) is not View view)
                            continue;
                        string baseName = BatchExportNaming.ResolveBaseName(viewModel.PrintPdfFileName, doc, view, index);
                        baseName = BatchExportNaming.EnsureUniqueBaseName(baseName, usedNames);
                        paths.Add(($"打印 PDF/{view.Name}", Path.Combine(outputFolder, baseName + ".pdf")));
                    }
                }
            }

            int beforeErrors = result.ErrorCount;
            foreach ((string label, string path) in paths)
            {
                string normalized = NormalizeOutputPath(path);
                if (Directory.Exists(path))
                {
                    result.Errors.Add($"输出命名预检：{label} 目标与现有文件夹同名：{path}");
                    continue;
                }
                if (plannedOutputReservations.Contains(normalized))
                {
                    result.Errors.Add(
                        $"输出命名预检：{label} 与本批次其它输出重名：{path}。" +
                        "请在命名模板中加入 {模型名}/{图纸号}/{视图名}，或启用每模型独立子目录。");
                    continue;
                }
                if (File.Exists(path) && !viewModel.OverwriteExisting)
                {
                    result.Errors.Add($"输出命名预检：{label} 已存在且未允许覆盖：{path}");
                    continue;
                }
                plannedOutputReservations.Add(normalized);
            }

            if (result.ErrorCount == beforeErrors)
                result.Successes.Add($"输出命名预检：{paths.Count} 个主输出路径无冲突");
        }

        private static string GetDocumentOutputFolder(
            Document doc,
            IBatchTaskOptions viewModel,
            bool multipleDocuments)
        {
            string root = string.IsNullOrWhiteSpace(viewModel.OutputFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "YangTools_BatchExport")
                : viewModel.OutputFolder.Trim();

            if (viewModel.UseDocumentSubfolders)
                return Path.Combine(root, SanitizeFileName(doc.Title));
            return root;
        }

        private static string SanitizeFileName(string? value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "未命名模型" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                source = source.Replace(invalid, '_');
            return source.Trim().TrimEnd('.');
        }

        private static bool CanUseBatchOutputPath(
            string path,
            ISet<string>? batchOutputReservations,
            out string error)
        {
            error = string.Empty;
            if (!IsBatchOutputReserved(path, batchOutputReservations))
                return true;

            error = "同一批次存在重复输出路径。请在命名模板中加入 {模型名}/{图纸号}，或启用每模型独立子目录：" + path;
            return false;
        }

        private static bool IsBatchOutputReserved(string path, ISet<string>? batchOutputReservations)
        {
            if (batchOutputReservations == null || string.IsNullOrWhiteSpace(path))
                return false;
            return batchOutputReservations.Contains(NormalizeOutputPath(path));
        }

        private static void AddBatchOutputReservation(string path, ISet<string>? batchOutputReservations)
        {
            if (batchOutputReservations == null || string.IsNullOrWhiteSpace(path))
                return;
            batchOutputReservations.Add(NormalizeOutputPath(path));
        }

        private static string NormalizeOutputPath(string path)
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

        private static string CreateStagingFolder(string outputFolder, string tag)
        {
            string folder = Path.Combine(outputFolder, $".yangtools_{tag}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static bool CommitStagedFiles(
            string stagingFolder,
            string outputFolder,
            bool overwrite,
            ISet<string>? batchOutputReservations,
            out List<string> committedFiles,
            out string error)
        {
            BatchFileCommitResult commit = BatchFileCommitService.Commit(
                stagingFolder,
                outputFolder,
                overwrite,
                batchOutputReservations);
            committedFiles = commit.CommittedFiles;
            error = commit.Success && commit.RecoveryMessages.Count > 0
                ? string.Join("；", commit.RecoveryMessages)
                : commit.Error;
            return commit.Success;
        }

        private static void DeleteDirectoryBestEffort(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch { }
        }

        private static string CreateTemporaryExportBaseName(string finalBaseName)
        {
            return SanitizeFileName($"{finalBaseName}.yangtools_tmp_{Guid.NewGuid():N}");
        }

        private static bool ReplaceOutputSafely(string temporaryPath, string finalPath, bool overwrite, out string error)
        {
            PdfFileCommitResult commit = BatchFileCommitService.CommitPdf(
                temporaryPath, finalPath, overwrite, new PhysicalBatchFileSystem());
            error = commit.Success ? commit.Warning : commit.Error;
            return commit.Success;
        }

        private static void DeleteFileBestEffort(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Cleanup failure is reported by the primary operation when relevant.
            }
        }

        private static bool PrepareExactOutput(string path, bool overwrite, out string error)
        {
            error = string.Empty;
            if (!File.Exists(path))
                return true;
            if (!overwrite)
            {
                error = $"目标文件已存在，且未勾选覆盖：{path}";
                return false;
            }

            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                error = $"无法覆盖目标文件，文件可能正在使用：{path}；{ex.Message}";
                return false;
            }
        }

        private sealed class PrintManagerSnapshot
        {
            public string PrinterName { get; set; } = string.Empty;
            public PrintRange PrintRange { get; set; }
            public bool HasPrintToFile { get; set; }
            public bool PrintToFile { get; set; }
            public bool HasCombinedFile { get; set; }
            public bool CombinedFile { get; set; }
            public bool HasPrintToFileName { get; set; }
            public string PrintToFileName { get; set; } = string.Empty;
            public bool PrintSettingWasInSession { get; set; }
            public string? PrintSettingName { get; set; }
            public bool ViewSheetSetWasInSession { get; set; }
            public string? ViewSheetSetName { get; set; }
            public List<ElementId> InSessionViewIds { get; } = new List<ElementId>();
        }

        private sealed class ViewSelectionResult
        {
            public List<ElementId> ViewIds { get; } = new List<ElementId>();
            public string? Error { get; set; }
        }
    }
}
