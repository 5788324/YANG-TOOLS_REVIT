using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Models.BatchTask
{
    public class LinkItem : INotifyPropertyChanged
    {
        private string _filePath = string.Empty;

        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(IsCad));
                OnPropertyChanged(nameof(IsRevit));
            }
        }

        public string FileName => Path.GetFileName(FilePath);
        public bool IsCad => FilePath.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)
                             || FilePath.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase);
        public bool IsRevit => FilePath.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BatchDocumentInfo : INotifyPropertyChanged
    {
        private bool _enabled = true;

        public string FilePath { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsCurrentDocument { get; set; }
        public bool IsCloudModel { get; set; }
        public string CloudRegion { get; set; } = string.Empty;
        public string CloudProjectGuid { get; set; } = string.Empty;
        public string CloudModelGuid { get; set; } = string.Empty;
        public string SourceKind { get; set; } = "本地";
        public Document? OpenDocument { get; set; }

        public string LocationDescription
        {
            get
            {
                if (IsCloudModel)
                {
                    string region = string.IsNullOrWhiteSpace(CloudRegion) ? "?" : CloudRegion;
                    string project = ShortGuid(CloudProjectGuid);
                    string model = ShortGuid(CloudModelGuid);
                    return $"Cloud {region} / P:{project} / M:{model}";
                }
                return FilePath ?? string.Empty;
            }
        }

        private static string ShortGuid(string? value)
        {
            return Guid.TryParse(value, out Guid parsed)
                ? parsed.ToString("D").Substring(0, 8)
                : "?";
        }

        public string SourceDescription
        {
            get
            {
                if (IsCurrentDocument)
                    return "当前模型";
                if (IsCloudModel)
                    return "云模型";
                if (OpenDocument != null && OpenDocument.IsValidObject)
                    return "已打开模型";
                return "本地/同步文件";
            }
        }

        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class BatchTaskViewModel : INotifyPropertyChanged, IBatchTaskOptions
    {
        public const string WholeModelOption = "<整个模型>";
        public const string Current3DViewOption = "<当前三维视图>";
        public const string Auto3DViewOption = "<各模型自动三维视图>";
        public const string CurrentViewOption = "<当前视图>";
        public const string AllSheetsOption = "<全部图纸>";
        public const string AllPrintableViewsOption = "<全部可打印视图>";
        public const string DefaultSetupOption = "<默认内置设置>";
        public const string CurrentPrintSetupOption = "<当前打印设置>";
        public const string KeepSetupOption = "<按所选导出设置>";

        private readonly Document _doc;
        private readonly UserSettings _settings;

        public BatchTaskViewModel(UIApplication uiApp)
        {
            if (uiApp == null)
                throw new ArgumentNullException(nameof(uiApp));

            _doc = uiApp.ActiveUIDocument.Document;
            _settings = UserSettings.Load();

            LoadDocumentSettings();
            LoadUserSettings();

            Documents.Add(CreateDocumentInfo(_doc, true));
        }

        private void LoadDocumentSettings()
        {
            Available3DViews = new List<string> { WholeModelOption, Auto3DViewOption, Current3DViewOption };
            Available3DViews.AddRange(new FilteredElementCollector(_doc)
                .OfClass(typeof(View3D))
                .Cast<View3D>()
                .Where(view => !view.IsTemplate)
                .Select(view => view.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase));

            AvailableViewSheetSets = new List<string> { AllSheetsOption, CurrentViewOption, AllPrintableViewsOption };
            AvailableViewSheetSets.AddRange(new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheetSet))
                .Cast<ViewSheetSet>()
                .Select(set => set.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase));

            AvailableDwgSetups = new List<string> { DefaultSetupOption };
            AvailableDwgSetups.AddRange(new FilteredElementCollector(_doc)
                .OfClass(typeof(ExportDWGSettings))
                .Cast<ExportDWGSettings>()
                .Select(settings => settings.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase));

#if REVIT2022_OR_GREATER
            AvailablePdfSetups = new List<string> { DefaultSetupOption };
            AvailablePdfSetups.AddRange(new FilteredElementCollector(_doc)
                .OfClass(typeof(ExportPDFSettings))
                .Cast<ExportPDFSettings>()
                .Select(settings => settings.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase));
#else
            AvailablePdfSetups = new List<string> { "Revit 2021 不支持 PDF 导出" };
#endif

            AvailablePrintPdfSetups = new List<string> { CurrentPrintSetupOption };
            AvailablePrintPdfSetups.AddRange(new FilteredElementCollector(_doc)
                .OfClass(typeof(PrintSetting))
                .Cast<PrintSetting>()
                .Select(settings => settings.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase));

            AvailablePrintPdfPrinters = LoadPdfPrinterNames(_doc);
        }

        private void LoadUserSettings()
        {
            string defaultOutput = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "YangTools_BatchExport");

            OutputFolder = !string.IsNullOrWhiteSpace(_settings.BatchOutputFolder)
                ? _settings.BatchOutputFolder
                : defaultOutput;
            UseDocumentSubfolders = _settings.BatchUseDocumentSubfolders;
            OverwriteExisting = _settings.BatchOverwriteExisting;
            OpenOutputFolderWhenDone = _settings.BatchOpenOutputFolderWhenDone;
            LinkSkipIfExists = _settings.BatchLinkSkipIfExists;
            LinkPinned = _settings.BatchLinkPinned;

            EnableNwc = _settings.BatchEnableNwc;
            EnableIfc = _settings.BatchEnableIfc;
            EnableDwg = _settings.BatchEnableDwg;
#if REVIT2022_OR_GREATER
            EnablePdf = _settings.BatchEnablePdf;
#else
            EnablePdf = false;
#endif
            EnablePrintPdf = _settings.BatchEnablePrintPdf;

            NwcFileName = NormalizeTemplate(_settings.BatchNwcFileName);
            IfcFileName = NormalizeTemplate(_settings.BatchIfcFileName);
            DwgFileName = NormalizeTemplate(_settings.BatchDwgFileName);
            PdfFileName = NormalizeTemplate(_settings.BatchPdfFileName);
            PdfCombine = _settings.BatchPdfCombine;
            PrintPdfFileName = NormalizeTemplate(_settings.BatchPrintPdfFileName);

            SelectedNwc3DView = PickAvailable(Available3DViews, _settings.BatchNwcViewName, Auto3DViewOption);
            SelectedIfc3DView = PickAvailable(Available3DViews, _settings.BatchIfcViewName, Auto3DViewOption);
            SelectedDwgViewSheetSet = PickAvailable(AvailableViewSheetSets, _settings.BatchDwgViewSetName, AllSheetsOption);
            SelectedPdfViewSheetSet = PickAvailable(AvailableViewSheetSets, _settings.BatchPdfViewSetName, AllSheetsOption);
            SelectedPrintPdfViewSheetSet = PickAvailable(AvailableViewSheetSets, _settings.BatchPrintPdfViewSetName, AllSheetsOption);
            SelectedDwgSetup = PickAvailable(AvailableDwgSetups, _settings.BatchDwgSetupName, DefaultSetupOption);
            SelectedPdfSetup = PickAvailable(AvailablePdfSetups, _settings.BatchPdfSetupName, AvailablePdfSetups.FirstOrDefault());
            SelectedPrintPdfSetup = PickAvailable(AvailablePrintPdfSetups, _settings.BatchPrintPdfSetupName, CurrentPrintSetupOption);
            SelectedPrintPdfPrinterName = PickPrinterName(AvailablePrintPdfPrinters, _settings.BatchPrintPdfPrinterName);
            SelectedIfcVersion = AvailableIfcVersions.Contains(_settings.BatchIfcVersion)
                ? _settings.BatchIfcVersion
                : "IFC 4";
            SelectedNwcCoordinates = AvailableNwcCoordinates.Contains(_settings.BatchNwcCoordinates)
                ? _settings.BatchNwcCoordinates
                : "共享坐标";
            NwcExportLinks = _settings.BatchNwcExportLinks;
            NwcConvertElementProperties = _settings.BatchNwcConvertElementProperties;
            NwcExportElementIds = _settings.BatchNwcExportElementIds;
            NwcExportRoomAsAttribute = _settings.BatchNwcExportRoomAsAttribute;
            NwcExportRoomGeometry = _settings.BatchNwcExportRoomGeometry;
            NwcDivideFileIntoLevels = _settings.BatchNwcDivideFileIntoLevels;
            IfcExportBaseQuantities = _settings.BatchIfcExportBaseQuantities;
            IfcSplitWallsColumns = _settings.BatchIfcSplitWallsColumns;
            IfcSpaceBoundaryLevel = Math.Max(0, Math.Min(2, _settings.BatchIfcSpaceBoundaryLevel));
            SelectedDwgCoordinateMode = AvailableDwgCoordinateModes.Contains(_settings.BatchDwgCoordinateMode)
                ? _settings.BatchDwgCoordinateMode
                : KeepSetupOption;
            DwgMergedViews = _settings.BatchDwgMergedViews;
            PrintPdfCombine = _settings.BatchPrintPdfCombine;
        }

        public void SaveUserSettings()
        {
            _settings.BatchOutputFolder = OutputFolder ?? string.Empty;
            _settings.BatchUseDocumentSubfolders = UseDocumentSubfolders;
            _settings.BatchOverwriteExisting = OverwriteExisting;
            _settings.BatchOpenOutputFolderWhenDone = OpenOutputFolderWhenDone;
            _settings.BatchLinkSkipIfExists = LinkSkipIfExists;
            _settings.BatchLinkPinned = LinkPinned;

            _settings.BatchEnableNwc = EnableNwc;
            _settings.BatchEnableIfc = EnableIfc;
            _settings.BatchEnableDwg = EnableDwg;
#if REVIT2022_OR_GREATER
            _settings.BatchEnablePdf = EnablePdf;
#endif
            _settings.BatchEnablePrintPdf = EnablePrintPdf;

            _settings.BatchNwcFileName = NormalizeTemplate(NwcFileName);
            _settings.BatchIfcFileName = NormalizeTemplate(IfcFileName);
            _settings.BatchDwgFileName = NormalizeTemplate(DwgFileName);
            _settings.BatchPdfFileName = NormalizeTemplate(PdfFileName);
            _settings.BatchPdfCombine = PdfCombine;
            _settings.BatchPrintPdfFileName = NormalizeTemplate(PrintPdfFileName);

            _settings.BatchNwcViewName = SelectedNwc3DView ?? string.Empty;
            _settings.BatchIfcViewName = SelectedIfc3DView ?? string.Empty;
            _settings.BatchDwgViewSetName = SelectedDwgViewSheetSet ?? string.Empty;
            _settings.BatchPdfViewSetName = SelectedPdfViewSheetSet ?? string.Empty;
            _settings.BatchPrintPdfViewSetName = SelectedPrintPdfViewSheetSet ?? string.Empty;
            _settings.BatchDwgSetupName = SelectedDwgSetup ?? string.Empty;
            _settings.BatchPdfSetupName = SelectedPdfSetup ?? string.Empty;
            _settings.BatchPrintPdfSetupName = SelectedPrintPdfSetup ?? string.Empty;
            _settings.BatchPrintPdfPrinterName = SelectedPrintPdfPrinterName?.Trim() ?? string.Empty;
            _settings.BatchIfcVersion = SelectedIfcVersion ?? "IFC 4";
            _settings.BatchNwcCoordinates = SelectedNwcCoordinates ?? "共享坐标";
            _settings.BatchNwcExportLinks = NwcExportLinks;
            _settings.BatchNwcConvertElementProperties = NwcConvertElementProperties;
            _settings.BatchNwcExportElementIds = NwcExportElementIds;
            _settings.BatchNwcExportRoomAsAttribute = NwcExportRoomAsAttribute;
            _settings.BatchNwcExportRoomGeometry = NwcExportRoomGeometry;
            _settings.BatchNwcDivideFileIntoLevels = NwcDivideFileIntoLevels;
            _settings.BatchIfcExportBaseQuantities = IfcExportBaseQuantities;
            _settings.BatchIfcSplitWallsColumns = IfcSplitWallsColumns;
            _settings.BatchIfcSpaceBoundaryLevel = IfcSpaceBoundaryLevel;
            _settings.BatchDwgCoordinateMode = SelectedDwgCoordinateMode ?? KeepSetupOption;
            _settings.BatchDwgMergedViews = DwgMergedViews;
            _settings.BatchPrintPdfCombine = PrintPdfCombine;
            _settings.Save();
        }

        public ObservableCollection<BatchDocumentInfo> Documents { get; } = new ObservableCollection<BatchDocumentInfo>();
        public ObservableCollection<LinkItem> Links { get; } = new ObservableCollection<LinkItem>();

        public void AddDocument(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            string normalized = NormalizePath(filePath);
            if (Documents.Any(document => !document.IsCloudModel && string.Equals(
                    NormalizePath(document.FilePath), normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            Documents.Add(new BatchDocumentInfo
            {
                FilePath = filePath,
                DisplayName = Path.GetFileNameWithoutExtension(filePath),
                IsCurrentDocument = false,
                IsCloudModel = false,
                SourceKind = "本地/同步文件",
                Enabled = true
            });
        }

        public int AddOpenDocuments(UIApplication uiApp)
        {
            int added = 0;
            foreach (Document document in uiApp.Application.Documents.Cast<Document>())
            {
                if (document == null || !document.IsValidObject || document.IsFamilyDocument)
                    continue;

                BatchDocumentInfo info = CreateDocumentInfo(
                    document,
                    uiApp.ActiveUIDocument != null && document.Equals(uiApp.ActiveUIDocument.Document));

                if (Documents.Any(existing => SameDocument(existing, info)))
                    continue;

                Documents.Add(info);
                added++;
            }
            return added;
        }

        private static BatchDocumentInfo CreateDocumentInfo(Document document, bool isCurrent)
        {
            var info = new BatchDocumentInfo
            {
                FilePath = document.PathName ?? string.Empty,
                DisplayName = isCurrent ? $"当前：{document.Title}" : document.Title,
                IsCurrentDocument = isCurrent,
                OpenDocument = document,
                Enabled = true,
                IsCloudModel = document.IsModelInCloud,
                SourceKind = document.IsModelInCloud ? "云模型" : "已打开模型"
            };

            if (document.IsModelInCloud)
            {
                try
                {
                    ModelPath cloudPath = document.GetCloudModelPath();
                    info.CloudRegion = cloudPath.Region ?? string.Empty;
                    info.CloudProjectGuid = cloudPath.GetProjectGUID().ToString("D");
                    info.CloudModelGuid = cloudPath.GetModelGUID().ToString("D");
                    info.FilePath = ModelPathUtils.ConvertModelPathToUserVisiblePath(cloudPath);
                }
                catch
                {
                    // Live document is still usable even if cloud metadata cannot be extracted.
                }
            }

            return info;
        }

        private static bool SameDocument(BatchDocumentInfo left, BatchDocumentInfo right)
        {
            if (left.IsCloudModel && right.IsCloudModel
                && Guid.TryParse(left.CloudProjectGuid, out Guid leftProject)
                && Guid.TryParse(left.CloudModelGuid, out Guid leftModel)
                && Guid.TryParse(right.CloudProjectGuid, out Guid rightProject)
                && Guid.TryParse(right.CloudModelGuid, out Guid rightModel))
            {
                return leftProject == rightProject && leftModel == rightModel;
            }

            if (!string.IsNullOrWhiteSpace(left.FilePath) && !string.IsNullOrWhiteSpace(right.FilePath))
            {
                return string.Equals(
                    NormalizePath(left.FilePath),
                    NormalizePath(right.FilePath),
                    StringComparison.OrdinalIgnoreCase);
            }

            return left.OpenDocument != null && right.OpenDocument != null
                && left.OpenDocument.Equals(right.OpenDocument);
        }

        public void RemoveDocument(BatchDocumentInfo document)
        {
            if (document == null || document.IsCurrentDocument)
                return;
            Documents.Remove(document);
        }

        public void AddLink(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            string normalized = NormalizePath(filePath);
            if (Links.Any(link => string.Equals(
                    NormalizePath(link.FilePath), normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            Links.Add(new LinkItem { FilePath = filePath });
        }

        private string _outputFolder = string.Empty;
        public string OutputFolder { get => _outputFolder; set { _outputFolder = value ?? string.Empty; OnPropertyChanged(); } }

        private bool _useDocumentSubfolders = true;
        public bool UseDocumentSubfolders { get => _useDocumentSubfolders; set { _useDocumentSubfolders = value; OnPropertyChanged(); } }

        private bool _overwriteExisting;
        public bool OverwriteExisting { get => _overwriteExisting; set { _overwriteExisting = value; OnPropertyChanged(); } }

        private bool _openOutputFolderWhenDone = true;
        public bool OpenOutputFolderWhenDone { get => _openOutputFolderWhenDone; set { _openOutputFolderWhenDone = value; OnPropertyChanged(); } }

        private bool _linkSkipIfExists = true;
        public bool LinkSkipIfExists { get => _linkSkipIfExists; set { _linkSkipIfExists = value; OnPropertyChanged(); } }

        private bool _linkPinned = true;
        public bool LinkPinned { get => _linkPinned; set { _linkPinned = value; OnPropertyChanged(); } }

        private bool _enableNwc;
        public bool EnableNwc
        {
            get => _enableNwc;
            set
            {
                _enableNwc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnyExportEnabled));
            }
        }

        private string _nwcFileName = "{模型名}";
        public string NwcFileName { get => _nwcFileName; set { _nwcFileName = value ?? string.Empty; OnPropertyChanged(); } }

        public List<string> Available3DViews { get; private set; } = new List<string>();
        private string? _selectedNwc3DView;
        public string? SelectedNwc3DView { get => _selectedNwc3DView; set { _selectedNwc3DView = value; OnPropertyChanged(); } }

        private bool _enableIfc;
        public bool EnableIfc
        {
            get => _enableIfc;
            set
            {
                _enableIfc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnyExportEnabled));
            }
        }

        private string _ifcFileName = "{模型名}";
        public string IfcFileName { get => _ifcFileName; set { _ifcFileName = value ?? string.Empty; OnPropertyChanged(); } }

        private string? _selectedIfc3DView;
        public string? SelectedIfc3DView { get => _selectedIfc3DView; set { _selectedIfc3DView = value; OnPropertyChanged(); } }

        public List<string> AvailableIfcVersions { get; } = new List<string>
        {
            "IFC 4", "IFC 4 DTV", "IFC 4 RV", "IFC 2x3", "IFC 2x3 CV 2.0", "IFC 2x2", "IFC BCA", "IFC COBie"
        };
        private string _selectedIfcVersion = "IFC 4";
        public string SelectedIfcVersion { get => _selectedIfcVersion; set { _selectedIfcVersion = value ?? "IFC 4"; OnPropertyChanged(); } }

        private bool _enableDwg;
        public bool EnableDwg
        {
            get => _enableDwg;
            set
            {
                _enableDwg = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnyExportEnabled));
            }
        }

        private string _dwgFileName = "{模型名}-{图纸号}-{图纸名}";
        public string DwgFileName { get => _dwgFileName; set { _dwgFileName = value ?? string.Empty; OnPropertyChanged(); } }

        public List<string> AvailableViewSheetSets { get; private set; } = new List<string>();
        private string? _selectedDwgViewSheetSet;
        public string? SelectedDwgViewSheetSet { get => _selectedDwgViewSheetSet; set { _selectedDwgViewSheetSet = value; OnPropertyChanged(); } }

        public List<string> AvailableDwgSetups { get; private set; } = new List<string>();
        private string? _selectedDwgSetup;
        public string? SelectedDwgSetup { get => _selectedDwgSetup; set { _selectedDwgSetup = value; OnPropertyChanged(); } }

        public bool CanExportPdf
        {
            get
            {
#if REVIT2022_OR_GREATER
                return true;
#else
                return false;
#endif
            }
        }

        private bool _enablePdf;
        public bool EnablePdf
        {
            get => _enablePdf;
            set
            {
                _enablePdf = CanExportPdf && value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnyExportEnabled));
            }
        }

        private string _pdfFileName = "{模型名}-{图纸号}-{图纸名}";
        public string PdfFileName { get => _pdfFileName; set { _pdfFileName = value ?? string.Empty; OnPropertyChanged(); } }

        private bool _pdfCombine = true;
        public bool PdfCombine { get => _pdfCombine; set { _pdfCombine = value; OnPropertyChanged(); } }

        private string? _selectedPdfViewSheetSet;
        public string? SelectedPdfViewSheetSet { get => _selectedPdfViewSheetSet; set { _selectedPdfViewSheetSet = value; OnPropertyChanged(); } }

        public List<string> AvailablePdfSetups { get; private set; } = new List<string>();
        private string? _selectedPdfSetup;
        public string? SelectedPdfSetup { get => _selectedPdfSetup; set { _selectedPdfSetup = value; OnPropertyChanged(); } }


        private bool _enablePrintPdf;
        public bool EnablePrintPdf
        {
            get => _enablePrintPdf;
            set
            {
                _enablePrintPdf = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnyExportEnabled));
            }
        }

        public List<string> AvailablePrintPdfPrinters { get; private set; } = new List<string>();
        private string _selectedPrintPdfPrinterName = "Microsoft Print to PDF";
        public string SelectedPrintPdfPrinterName
        {
            get => _selectedPrintPdfPrinterName;
            set { _selectedPrintPdfPrinterName = value ?? string.Empty; OnPropertyChanged(); }
        }

        public List<string> AvailablePrintPdfSetups { get; private set; } = new List<string>();
        private string? _selectedPrintPdfSetup;
        public string? SelectedPrintPdfSetup
        {
            get => _selectedPrintPdfSetup;
            set { _selectedPrintPdfSetup = value; OnPropertyChanged(); }
        }

        private string? _selectedPrintPdfViewSheetSet;
        public string? SelectedPrintPdfViewSheetSet
        {
            get => _selectedPrintPdfViewSheetSet;
            set { _selectedPrintPdfViewSheetSet = value; OnPropertyChanged(); }
        }

        private string _printPdfFileName = "{模型名}-{图纸号}-{图纸名}";
        public string PrintPdfFileName
        {
            get => _printPdfFileName;
            set { _printPdfFileName = value ?? string.Empty; OnPropertyChanged(); }
        }

        public List<string> AvailableNwcCoordinates { get; } = new List<string> { "共享坐标", "内部坐标" };
        private string _selectedNwcCoordinates = "共享坐标";
        public string SelectedNwcCoordinates { get => _selectedNwcCoordinates; set { _selectedNwcCoordinates = value ?? "共享坐标"; OnPropertyChanged(); } }

        private bool _nwcExportLinks;
        public bool NwcExportLinks { get => _nwcExportLinks; set { _nwcExportLinks = value; OnPropertyChanged(); } }

        private bool _nwcConvertElementProperties = true;
        public bool NwcConvertElementProperties { get => _nwcConvertElementProperties; set { _nwcConvertElementProperties = value; OnPropertyChanged(); } }

        private bool _nwcExportElementIds = true;
        public bool NwcExportElementIds { get => _nwcExportElementIds; set { _nwcExportElementIds = value; OnPropertyChanged(); } }

        private bool _nwcExportRoomAsAttribute = true;
        public bool NwcExportRoomAsAttribute { get => _nwcExportRoomAsAttribute; set { _nwcExportRoomAsAttribute = value; OnPropertyChanged(); } }

        private bool _nwcExportRoomGeometry = true;
        public bool NwcExportRoomGeometry { get => _nwcExportRoomGeometry; set { _nwcExportRoomGeometry = value; OnPropertyChanged(); } }

        private bool _nwcDivideFileIntoLevels = true;
        public bool NwcDivideFileIntoLevels { get => _nwcDivideFileIntoLevels; set { _nwcDivideFileIntoLevels = value; OnPropertyChanged(); } }

        private bool _ifcExportBaseQuantities = true;
        public bool IfcExportBaseQuantities { get => _ifcExportBaseQuantities; set { _ifcExportBaseQuantities = value; OnPropertyChanged(); } }

        private bool _ifcSplitWallsColumns;
        public bool IfcSplitWallsColumns { get => _ifcSplitWallsColumns; set { _ifcSplitWallsColumns = value; OnPropertyChanged(); } }

        public List<int> AvailableIfcSpaceBoundaryLevels { get; } = new List<int> { 0, 1, 2 };
        private int _ifcSpaceBoundaryLevel = 1;
        public int IfcSpaceBoundaryLevel { get => _ifcSpaceBoundaryLevel; set { _ifcSpaceBoundaryLevel = Math.Max(0, Math.Min(2, value)); OnPropertyChanged(); } }

        public List<string> AvailableDwgCoordinateModes { get; } = new List<string> { KeepSetupOption, "共享坐标", "项目坐标" };
        private string _selectedDwgCoordinateMode = KeepSetupOption;
        public string SelectedDwgCoordinateMode { get => _selectedDwgCoordinateMode; set { _selectedDwgCoordinateMode = value ?? KeepSetupOption; OnPropertyChanged(); } }

        private bool _dwgMergedViews;
        public bool DwgMergedViews { get => _dwgMergedViews; set { _dwgMergedViews = value; OnPropertyChanged(); } }

        private bool _printPdfCombine = true;
        public bool PrintPdfCombine { get => _printPdfCombine; set { _printPdfCombine = value; OnPropertyChanged(); } }

        public bool HasAnyExportEnabled => EnableNwc || EnableIfc || EnableDwg || EnablePdf || EnablePrintPdf;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private static string NormalizeTemplate(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "{模型名}" : value.Trim();
        }


        private static string PickPrinterName(IEnumerable<string> values, string? preferred)
        {
            List<string> list = values.ToList();
            string? matched = list.FirstOrDefault(value => string.Equals(value, preferred, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(matched))
                return matched;
            if (!string.IsNullOrWhiteSpace(preferred))
                return preferred.Trim();
            return list.FirstOrDefault() ?? "Microsoft Print to PDF";
        }

        private static List<string> LoadPdfPrinterNames(Document doc)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string currentPdfPrinter = string.Empty;

            try
            {
                string current = doc.PrintManager.PrinterName;
                if (LooksLikePdfPrinter(current))
                {
                    currentPdfPrinter = current;
                    names.Add(current);
                }
            }
            catch
            {
                // Printer discovery is best effort; execution performs the authoritative validation.
            }

            AddPdfPrintersFromRegistry(names, Registry.CurrentUser, @"Software\Microsoft\Windows NT\CurrentVersion\Devices", valueNames: true);
            AddPdfPrintersFromRegistry(names, Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Print\Printers", valueNames: false);

            if (names.Count == 0)
                names.Add("Microsoft Print to PDF");

            return names
                .OrderBy(name => string.Equals(name, currentPdfPrinter, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(name => string.Equals(name, "Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void AddPdfPrintersFromRegistry(
            HashSet<string> names,
            RegistryKey root,
            string subKeyPath,
            bool valueNames)
        {
            try
            {
                using (RegistryKey? key = root.OpenSubKey(subKeyPath))
                {
                    if (key == null)
                        return;

                    IEnumerable<string> candidates = valueNames ? key.GetValueNames() : key.GetSubKeyNames();
                    foreach (string name in candidates)
                    {
                        if (LooksLikePdfPrinter(name))
                            names.Add(name);
                    }
                }
            }
            catch
            {
                // Registry access may be restricted. The printer name remains manually editable in the UI.
            }
        }

        private static bool LooksLikePdfPrinter(string? name)
        {
            return !string.IsNullOrWhiteSpace(name)
                   && name.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string? PickAvailable(IEnumerable<string> values, string? preferred, string? fallback)
        {
            List<string> list = values.ToList();
            string? matched = list.FirstOrDefault(value => string.Equals(value, preferred, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(matched))
                return matched;

            matched = list.FirstOrDefault(value => string.Equals(value, fallback, StringComparison.OrdinalIgnoreCase));
            return matched ?? list.FirstOrDefault();
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

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
