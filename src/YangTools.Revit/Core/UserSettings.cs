using System;
using System.IO;
using Newtonsoft.Json;

namespace YangTools.Revit.Core
{
    public sealed class UserSettings
    {
        public int SettingsVersion { get; set; } = 7;

        // EntityGenerator (Loft)
        public string EntityGeneratorTemplatePath { get; set; } = string.Empty;
        public string EntityGeneratorFamilyName { get; set; } = "GeneratedLoftFamily";
        public string EntityGeneratorMaterialName { get; set; } = string.Empty;
        public long? EntityGeneratorCategoryValue { get; set; }
        public string EntityGeneratorLevelName { get; set; } = string.Empty;
        public bool EntityGeneratorIsVoid { get; set; }
        public bool EntityGeneratorCutWithVoids { get; set; }

        // SolidGenerator
        public string SolidGeneratorTemplatePath { get; set; } = string.Empty;
        public string SolidGeneratorFamilyName { get; set; } = "GeneratedSolidFamily";
        public string SolidGeneratorMaterialName { get; set; } = string.Empty;
        public long? SolidGeneratorCategoryValue { get; set; }
        public string SolidGeneratorLevelName { get; set; } = string.Empty;

        // BooleanGeometry: only safe scalar options are persisted. Element selections are never persisted.
        public bool BooleanDeleteTargetInst { get; set; }
        public bool BooleanDeleteTargetFam { get; set; }

        // LinearPlacement: path geometry is not persisted because it belongs to the current document session.
        public bool LinearIsByDistance { get; set; } = true;
        public bool LinearIsBySpacing { get; set; } = true;
        public double LinearStartValue { get; set; } = 0;
        public double LinearEndValue { get; set; } = 10000;
        public double LinearStepValue { get; set; } = 1000;
        public bool LinearAlignToCurve { get; set; } = true;
        public bool LinearKeepVertical { get; set; } = true;
        public double LinearRotationOffset { get; set; }
        public string LinearFamilyName { get; set; } = string.Empty;
        public string LinearTypeName { get; set; } = string.Empty;
        public bool LinearReversePath { get; set; }
        public bool LinearIncludeRemainderEndpoint { get; set; }
        public string LinearLevelName { get; set; } = string.Empty;
        public double LinearStartElevationMm { get; set; }
        public double LinearEndElevationMm { get; set; }

        // ColumnAlongPath: route geometry is session-only; scalar inputs and selected names are persisted.
        public string ColumnAlongPathFamilyName { get; set; } = string.Empty;
        public string ColumnAlongPathTypeName { get; set; } = string.Empty;
        public double ColumnAlongPathSpacingMm { get; set; } = 6000.0;
        public double ColumnAlongPathStartOffsetMm { get; set; }
        public double ColumnAlongPathStartMileageM { get; set; }
        public double ColumnAlongPathRotationOffsetDegrees { get; set; }
        public string ColumnAlongPathBaseLevelName { get; set; } = string.Empty;
        public double ColumnAlongPathBaseOffsetMm { get; set; }
        public string ColumnAlongPathTopLevelName { get; set; } = string.Empty;
        public double ColumnAlongPathTopOffsetMm { get; set; }
        public bool ColumnAlongPathReversePath { get; set; }
        public bool ColumnAlongPathIncludeEndColumn { get; set; } = true;


        // Project MicroTool: only paths are persisted. Runtime assemblies are always revalidated.
        public string MicroToolLastProjectPath { get; set; } = string.Empty;
        public string MicroToolLastToolPath { get; set; } = string.Empty;

        // Paste CAD
        public int PasteCadUnitIndex { get; set; } = 1;
        public int PasteCadColorIndex { get; set; }
        public int PasteCadPlacementIndex { get; set; }
        public bool PasteCadThisViewOnly { get; set; } = true;

        // Batch task and linking. Revit element ids and document selections are never persisted.
        public string BatchOutputFolder { get; set; } = string.Empty;
        public bool BatchUseDocumentSubfolders { get; set; } = true;
        public bool BatchOverwriteExisting { get; set; }
        public bool BatchOpenOutputFolderWhenDone { get; set; } = true;
        public bool BatchLinkSkipIfExists { get; set; } = true;
        public bool BatchLinkPinned { get; set; } = true;
        public bool BatchEnableNwc { get; set; }
        public bool BatchEnableIfc { get; set; }
        public bool BatchEnableDwg { get; set; }
        public bool BatchEnablePdf { get; set; }
        public bool BatchEnablePrintPdf { get; set; }
        public string BatchNwcFileName { get; set; } = "{模型名}";
        public string BatchIfcFileName { get; set; } = "{模型名}";
        public string BatchDwgFileName { get; set; } = "{模型名}-{图纸号}-{图纸名}";
        public string BatchPdfFileName { get; set; } = "{模型名}-{图纸号}-{图纸名}";
        public string BatchPrintPdfFileName { get; set; } = "{模型名}-{图纸号}-{图纸名}";
        public string BatchNwcViewName { get; set; } = string.Empty;
        public string BatchIfcViewName { get; set; } = string.Empty;
        public string BatchDwgViewSetName { get; set; } = string.Empty;
        public string BatchPdfViewSetName { get; set; } = string.Empty;
        public string BatchPrintPdfViewSetName { get; set; } = string.Empty;
        public string BatchDwgSetupName { get; set; } = string.Empty;
        public string BatchPdfSetupName { get; set; } = string.Empty;
        public string BatchPrintPdfSetupName { get; set; } = string.Empty;
        public string BatchPrintPdfPrinterName { get; set; } = string.Empty;
        public string BatchIfcVersion { get; set; } = "IFC 4";
        public bool BatchPdfCombine { get; set; } = true;
        public string BatchNwcCoordinates { get; set; } = "共享坐标";
        public bool BatchNwcExportLinks { get; set; }
        public bool BatchNwcConvertElementProperties { get; set; } = true;
        public bool BatchNwcExportElementIds { get; set; } = true;
        public bool BatchNwcExportRoomAsAttribute { get; set; } = true;
        public bool BatchNwcExportRoomGeometry { get; set; } = true;
        public bool BatchNwcDivideFileIntoLevels { get; set; } = true;
        public bool BatchIfcExportBaseQuantities { get; set; } = true;
        public bool BatchIfcSplitWallsColumns { get; set; }
        public int BatchIfcSpaceBoundaryLevel { get; set; } = 1;
        public string BatchDwgCoordinateMode { get; set; } = "<按所选导出设置>";
        public bool BatchDwgMergedViews { get; set; }
        public bool BatchPrintPdfCombine { get; set; } = true;

        private static string SettingsDirectory
        {
            get
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "YangTools");
            }
        }

        private static string JsonSettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");
        private static string LegacySettingsFilePath => Path.Combine(SettingsDirectory, "settings.txt");

        public static UserSettings Load()
        {
            try
            {
                if (File.Exists(JsonSettingsFilePath))
                {
                    string json = File.ReadAllText(JsonSettingsFilePath);
                    UserSettings? loaded = JsonConvert.DeserializeObject<UserSettings>(json);
                    if (loaded != null)
                    {
                        loaded.Normalize();
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("读取用户输入设置", ex);
            }

            var settings = new UserSettings();
            settings.LoadLegacyTemplatePath();
            settings.Normalize();
            return settings;
        }

        public void Save()
        {
            try
            {
                Normalize();
                Directory.CreateDirectory(SettingsDirectory);

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                string tempPath = JsonSettingsFilePath + ".tmp";
                File.WriteAllText(tempPath, json);

                if (File.Exists(JsonSettingsFilePath))
                    File.Delete(JsonSettingsFilePath);
                File.Move(tempPath, JsonSettingsFilePath);
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("保存用户输入设置", ex);
            }
        }

        private void LoadLegacyTemplatePath()
        {
            try
            {
                if (!File.Exists(LegacySettingsFilePath))
                    return;

                foreach (string line in File.ReadAllLines(LegacySettingsFilePath))
                {
                    const string prefix = "EntityGeneratorTemplatePath=";
                    if (line.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        EntityGeneratorTemplatePath = line.Substring(prefix.Length);
                        SolidGeneratorTemplatePath = EntityGeneratorTemplatePath;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("迁移旧用户设置", ex);
            }
        }

        private void Normalize()
        {
            SettingsVersion = 7;
            EntityGeneratorTemplatePath = EntityGeneratorTemplatePath ?? string.Empty;
            EntityGeneratorFamilyName = string.IsNullOrWhiteSpace(EntityGeneratorFamilyName)
                ? "GeneratedLoftFamily"
                : EntityGeneratorFamilyName;
            EntityGeneratorMaterialName = EntityGeneratorMaterialName ?? string.Empty;
            EntityGeneratorLevelName = EntityGeneratorLevelName ?? string.Empty;

            SolidGeneratorTemplatePath = SolidGeneratorTemplatePath ?? string.Empty;
            SolidGeneratorFamilyName = string.IsNullOrWhiteSpace(SolidGeneratorFamilyName)
                ? "GeneratedSolidFamily"
                : SolidGeneratorFamilyName;
            SolidGeneratorMaterialName = SolidGeneratorMaterialName ?? string.Empty;
            SolidGeneratorLevelName = SolidGeneratorLevelName ?? string.Empty;

            LinearFamilyName = LinearFamilyName ?? string.Empty;
            LinearTypeName = LinearTypeName ?? string.Empty;
            LinearLevelName = LinearLevelName ?? string.Empty;
            if (double.IsNaN(LinearStartElevationMm) || double.IsInfinity(LinearStartElevationMm)) LinearStartElevationMm = 0.0;
            if (double.IsNaN(LinearEndElevationMm) || double.IsInfinity(LinearEndElevationMm)) LinearEndElevationMm = 0.0;

            ColumnAlongPathFamilyName = ColumnAlongPathFamilyName ?? string.Empty;
            ColumnAlongPathTypeName = ColumnAlongPathTypeName ?? string.Empty;
            ColumnAlongPathBaseLevelName = ColumnAlongPathBaseLevelName ?? string.Empty;
            ColumnAlongPathTopLevelName = ColumnAlongPathTopLevelName ?? string.Empty;
            if (ColumnAlongPathSpacingMm <= 0) ColumnAlongPathSpacingMm = 6000.0;
            if (ColumnAlongPathStartOffsetMm < 0) ColumnAlongPathStartOffsetMm = 0;
            if (double.IsNaN(ColumnAlongPathRotationOffsetDegrees)
                || double.IsInfinity(ColumnAlongPathRotationOffsetDegrees))
            {
                ColumnAlongPathRotationOffsetDegrees = 0.0;
            }

            MicroToolLastProjectPath = MicroToolLastProjectPath ?? string.Empty;
            MicroToolLastToolPath = MicroToolLastToolPath ?? string.Empty;

            PasteCadUnitIndex = Clamp(PasteCadUnitIndex, 0, 6, 1);
            PasteCadColorIndex = Clamp(PasteCadColorIndex, 0, 2, 0);
            PasteCadPlacementIndex = Clamp(PasteCadPlacementIndex, 0, 2, 0);

            BatchOutputFolder = BatchOutputFolder ?? string.Empty;
            BatchNwcFileName = NormalizeBatchTemplate(BatchNwcFileName);
            BatchIfcFileName = NormalizeBatchTemplate(BatchIfcFileName);
            BatchDwgFileName = NormalizeBatchTemplate(BatchDwgFileName);
            BatchPdfFileName = NormalizeBatchTemplate(BatchPdfFileName);
            BatchPrintPdfFileName = NormalizeBatchTemplate(BatchPrintPdfFileName);
            BatchNwcViewName = BatchNwcViewName ?? string.Empty;
            BatchIfcViewName = BatchIfcViewName ?? string.Empty;
            BatchDwgViewSetName = BatchDwgViewSetName ?? string.Empty;
            BatchPdfViewSetName = BatchPdfViewSetName ?? string.Empty;
            BatchPrintPdfViewSetName = BatchPrintPdfViewSetName ?? string.Empty;
            BatchDwgSetupName = BatchDwgSetupName ?? string.Empty;
            BatchPdfSetupName = BatchPdfSetupName ?? string.Empty;
            BatchPrintPdfSetupName = BatchPrintPdfSetupName ?? string.Empty;
            BatchPrintPdfPrinterName = BatchPrintPdfPrinterName?.Trim() ?? string.Empty;
            BatchIfcVersion = string.IsNullOrWhiteSpace(BatchIfcVersion) ? "IFC 4" : BatchIfcVersion;
        }

        private static string NormalizeBatchTemplate(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "{模型名}" : value.Trim();
        }

        private static int Clamp(int value, int minimum, int maximum, int fallback)
        {
            return value >= minimum && value <= maximum ? value : fallback;
        }
    }
}
