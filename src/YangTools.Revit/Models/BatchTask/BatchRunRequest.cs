using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace YangTools.Revit.Models.BatchTask
{
    /// <summary>
    /// 批处理引擎只依赖这一组只读业务选项，避免执行期间继续读取可变 WPF ViewModel。
    /// GUI 在点击预检/执行时创建 BatchRunRequest 快照；后续 MCP 也应复用同一 Request/Result 契约。
    /// </summary>
    public interface IBatchTaskOptions
    {
        ObservableCollection<LinkItem> Links { get; }
        bool LinkSkipIfExists { get; }
        bool LinkPinned { get; }
        string OutputFolder { get; }
        bool UseDocumentSubfolders { get; }
        bool OverwriteExisting { get; }
        bool OpenOutputFolderWhenDone { get; }

        bool EnableNwc { get; }
        string NwcFileName { get; }
        string? SelectedNwc3DView { get; }
        string SelectedNwcCoordinates { get; }
        bool NwcExportLinks { get; }
        bool NwcConvertElementProperties { get; }
        bool NwcExportElementIds { get; }
        bool NwcExportRoomAsAttribute { get; }
        bool NwcExportRoomGeometry { get; }
        bool NwcDivideFileIntoLevels { get; }

        bool EnableIfc { get; }
        string IfcFileName { get; }
        string? SelectedIfc3DView { get; }
        string SelectedIfcVersion { get; }
        bool IfcExportBaseQuantities { get; }
        bool IfcSplitWallsColumns { get; }
        int IfcSpaceBoundaryLevel { get; }

        bool EnableDwg { get; }
        string DwgFileName { get; }
        string? SelectedDwgViewSheetSet { get; }
        string? SelectedDwgSetup { get; }
        string SelectedDwgCoordinateMode { get; }
        bool DwgMergedViews { get; }

        bool EnablePdf { get; }
        string PdfFileName { get; }
        bool PdfCombine { get; }
        string? SelectedPdfViewSheetSet { get; }
        string? SelectedPdfSetup { get; }

        bool EnablePrintPdf { get; }
        string SelectedPrintPdfPrinterName { get; }
        string? SelectedPrintPdfSetup { get; }
        string? SelectedPrintPdfViewSheetSet { get; }
        string PrintPdfFileName { get; }
        bool PrintPdfCombine { get; }

        bool HasAnyExportEnabled { get; }
    }

    public sealed class BatchRunOptions : IBatchTaskOptions
    {
        public ObservableCollection<LinkItem> Links { get; } = new ObservableCollection<LinkItem>();
        public bool LinkSkipIfExists { get; set; }
        public bool LinkPinned { get; set; }
        public string OutputFolder { get; set; } = string.Empty;
        public bool UseDocumentSubfolders { get; set; }
        public bool OverwriteExisting { get; set; }
        public bool OpenOutputFolderWhenDone { get; set; }

        public bool EnableNwc { get; set; }
        public string NwcFileName { get; set; } = string.Empty;
        public string? SelectedNwc3DView { get; set; }
        public string SelectedNwcCoordinates { get; set; } = "共享坐标";
        public bool NwcExportLinks { get; set; }
        public bool NwcConvertElementProperties { get; set; }
        public bool NwcExportElementIds { get; set; }
        public bool NwcExportRoomAsAttribute { get; set; }
        public bool NwcExportRoomGeometry { get; set; }
        public bool NwcDivideFileIntoLevels { get; set; }

        public bool EnableIfc { get; set; }
        public string IfcFileName { get; set; } = string.Empty;
        public string? SelectedIfc3DView { get; set; }
        public string SelectedIfcVersion { get; set; } = "IFC 4";
        public bool IfcExportBaseQuantities { get; set; }
        public bool IfcSplitWallsColumns { get; set; }
        public int IfcSpaceBoundaryLevel { get; set; }

        public bool EnableDwg { get; set; }
        public string DwgFileName { get; set; } = string.Empty;
        public string? SelectedDwgViewSheetSet { get; set; }
        public string? SelectedDwgSetup { get; set; }
        public string SelectedDwgCoordinateMode { get; set; } = BatchTaskViewModel.KeepSetupOption;
        public bool DwgMergedViews { get; set; }

        public bool EnablePdf { get; set; }
        public string PdfFileName { get; set; } = string.Empty;
        public bool PdfCombine { get; set; }
        public string? SelectedPdfViewSheetSet { get; set; }
        public string? SelectedPdfSetup { get; set; }

        public bool EnablePrintPdf { get; set; }
        public string SelectedPrintPdfPrinterName { get; set; } = string.Empty;
        public string? SelectedPrintPdfSetup { get; set; }
        public string? SelectedPrintPdfViewSheetSet { get; set; }
        public string PrintPdfFileName { get; set; } = string.Empty;
        public bool PrintPdfCombine { get; set; }

        public bool HasAnyExportEnabled => EnableNwc || EnableIfc || EnableDwg || EnablePdf || EnablePrintPdf;
    }

    public sealed class BatchRunRequest
    {
        public BatchRunOptions Options { get; private set; } = new BatchRunOptions();
        public List<BatchDocumentInfo> Documents { get; private set; } = new List<BatchDocumentInfo>();

        public static BatchRunRequest Capture(BatchTaskViewModel source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var request = new BatchRunRequest
            {
                Options = new BatchRunOptions
                {
                    LinkSkipIfExists = source.LinkSkipIfExists,
                    LinkPinned = source.LinkPinned,
                    OutputFolder = source.OutputFolder ?? string.Empty,
                    UseDocumentSubfolders = source.UseDocumentSubfolders,
                    OverwriteExisting = source.OverwriteExisting,
                    OpenOutputFolderWhenDone = source.OpenOutputFolderWhenDone,

                    EnableNwc = source.EnableNwc,
                    NwcFileName = source.NwcFileName ?? string.Empty,
                    SelectedNwc3DView = source.SelectedNwc3DView,
                    SelectedNwcCoordinates = source.SelectedNwcCoordinates ?? "共享坐标",
                    NwcExportLinks = source.NwcExportLinks,
                    NwcConvertElementProperties = source.NwcConvertElementProperties,
                    NwcExportElementIds = source.NwcExportElementIds,
                    NwcExportRoomAsAttribute = source.NwcExportRoomAsAttribute,
                    NwcExportRoomGeometry = source.NwcExportRoomGeometry,
                    NwcDivideFileIntoLevels = source.NwcDivideFileIntoLevels,

                    EnableIfc = source.EnableIfc,
                    IfcFileName = source.IfcFileName ?? string.Empty,
                    SelectedIfc3DView = source.SelectedIfc3DView,
                    SelectedIfcVersion = source.SelectedIfcVersion ?? "IFC 4",
                    IfcExportBaseQuantities = source.IfcExportBaseQuantities,
                    IfcSplitWallsColumns = source.IfcSplitWallsColumns,
                    IfcSpaceBoundaryLevel = source.IfcSpaceBoundaryLevel,

                    EnableDwg = source.EnableDwg,
                    DwgFileName = source.DwgFileName ?? string.Empty,
                    SelectedDwgViewSheetSet = source.SelectedDwgViewSheetSet,
                    SelectedDwgSetup = source.SelectedDwgSetup,
                    SelectedDwgCoordinateMode = source.SelectedDwgCoordinateMode ?? BatchTaskViewModel.KeepSetupOption,
                    DwgMergedViews = source.DwgMergedViews,

                    EnablePdf = source.EnablePdf,
                    PdfFileName = source.PdfFileName ?? string.Empty,
                    PdfCombine = source.PdfCombine,
                    SelectedPdfViewSheetSet = source.SelectedPdfViewSheetSet,
                    SelectedPdfSetup = source.SelectedPdfSetup,

                    EnablePrintPdf = source.EnablePrintPdf,
                    SelectedPrintPdfPrinterName = source.SelectedPrintPdfPrinterName ?? string.Empty,
                    SelectedPrintPdfSetup = source.SelectedPrintPdfSetup,
                    SelectedPrintPdfViewSheetSet = source.SelectedPrintPdfViewSheetSet,
                    PrintPdfFileName = source.PrintPdfFileName ?? string.Empty,
                    PrintPdfCombine = source.PrintPdfCombine
                },
                Documents = source.Documents
                    .Where(document => document.Enabled)
                    .Select(CloneDocumentInfo)
                    .ToList()
            };

            foreach (LinkItem link in source.Links)
            {
                request.Options.Links.Add(new LinkItem { FilePath = link.FilePath });
            }

            return request;
        }

        private static BatchDocumentInfo CloneDocumentInfo(BatchDocumentInfo source)
        {
            return new BatchDocumentInfo
            {
                FilePath = source.FilePath,
                DisplayName = source.DisplayName,
                IsCurrentDocument = source.IsCurrentDocument,
                IsCloudModel = source.IsCloudModel,
                CloudRegion = source.CloudRegion,
                CloudProjectGuid = source.CloudProjectGuid,
                CloudModelGuid = source.CloudModelGuid,
                SourceKind = source.SourceKind,
                OpenDocument = source.OpenDocument,
                Enabled = source.Enabled
            };
        }
    }
}
