[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$Project = Join-Path $ProjectRoot 'src\YangTools.Revit\YangTools.Revit.csproj'
$Commands = Join-Path $ProjectRoot 'src\YangTools.Revit\Commands'

$ExpectedVersion = '3.1.35'
$ExpectedRibbonCount = 30
$ObsoleteFiles = @(
    'src\YangTools.Revit\Commands\HelloWorldCommand.cs',
    'src\YangTools.Revit\Commands\McpStatusCommand.cs',
    'src\YangTools.Revit\Commands\RibbonSettingsCommand.cs',
    'src\YangTools.Revit\UI\RibbonSettingsWindow.xaml',
    'src\YangTools.Revit\UI\RibbonSettingsWindow.xaml.cs',
    'src\YangTools.Revit\Icons\hello_16.png',
    'src\YangTools.Revit\Icons\hello_32.png',
    'src\YangTools.Revit\Icons\mcp_server_16.png',
    'src\YangTools.Revit\Icons\mcp_server_32.png',
    'src\YangTools.Revit\Icons\ribbon_settings_16.png',
    'src\YangTools.Revit\Icons\ribbon_settings_32.png',
    'src\YangTools.Revit\Commands\ManholeElevationCommand.cs',
    'src\YangTools.Revit\Commands\PipeModifyTypeACommand.cs',
    'src\YangTools.Revit\Icons\manhole_elevation_16.png',
    'src\YangTools.Revit\Icons\manhole_elevation_32.png',
    'src\YangTools.Revit\Icons\pipe_modify_16.png',
    'src\YangTools.Revit\Icons\pipe_modify_32.png',
    'src\YangTools.Revit\Core\DeepSeekClient.cs',
    'src\YangTools.Revit\UI\CopilotWindow.xaml',
    'src\YangTools.Revit\UI\CopilotWindow.xaml.cs',
    'src\YangTools.Revit\Mcp\McpExternalEventHandler.cs',
    'src\YangTools.Revit\Mcp\McpHttpServer.cs',
    'src\YangTools.Revit\UI\AssistantWindow.xaml',
    'src\YangTools.Revit\UI\AssistantWindow.xaml.cs',
    'src\YangTools.Revit\Commands\CopilotCommand.cs',
    'src\YangTools.Revit\UI\AssistantModeView.xaml',
    'src\YangTools.Revit\UI\AssistantModeView.xaml.cs',
    'src\YangTools.Revit\UI\AssistantDockablePaneProvider.cs',
    'src\YangTools.Revit\UI\CopilotPanel.xaml',
    'src\YangTools.Revit\UI\CopilotPanel.xaml.cs',
    'src\YangTools.Revit\Icons\copilot_16.png',
    'src\YangTools.Revit\Icons\copilot_32.png'
)

if (-not (Test-Path -LiteralPath $Project)) {
    throw "Project file not found: $Project"
}

[xml]$ProjectXml = Get-Content -LiteralPath $Project -Raw -Encoding UTF8
$VersionNodes = @($ProjectXml.SelectNodes('/Project/PropertyGroup/Version'))
if ($VersionNodes.Count -ne 1) {
    throw "Expected exactly one Version node, found $($VersionNodes.Count)."
}
$ActualVersion = [string]$VersionNodes[0].InnerText
if ($ActualVersion -ne $ExpectedVersion) {
    throw "Source version mismatch. Actual: $ActualVersion; expected: $ExpectedVersion."
}

$FoundObsolete = @()
foreach ($relative in $ObsoleteFiles) {
    $fullPath = Join-Path $ProjectRoot $relative
    if (Test-Path -LiteralPath $fullPath) {
        $FoundObsolete += $relative
    }
}
if ($FoundObsolete.Count -gt 0) {
    throw ("Obsolete files found:`n- " + ($FoundObsolete -join "`n- ") + "`nDelete the whole source directory and extract the ZIP again. Do not overwrite an old directory.")
}

$RibbonCount = @(
    Get-ChildItem -LiteralPath $Commands -Filter '*.cs' -File |
        Select-String -Pattern '\[RibbonButton' -AllMatches |
        ForEach-Object { $_.Matches }
).Count

if ($RibbonCount -ne $ExpectedRibbonCount) {
    throw "RibbonButton count mismatch. Actual: $RibbonCount; expected: $ExpectedRibbonCount."
}

$RequiredSettingsContent = @(
    'UI\SystemSettingsWindow.xaml',
    'UI\SystemSettingsWindow.xaml.cs',
    'Commands\SampleWindowCommand.cs',
    'Commands\FamilyCategoryChangeCommand.cs',
    'Core\FamilyCategoryChangeService.cs',
    'UI\FamilyCategoryChangeWindow.xaml',
    'UI\FamilyCategoryChangeWindow.xaml.cs',
    'Commands\FamilyMaterialBindingCommand.cs',
    'Core\FamilyMaterialBindingService.cs',
    'UI\FamilyMaterialBindingWindow.xaml',
    'UI\FamilyMaterialBindingWindow.xaml.cs',
    'Commands\SimpleFamilyMaterialBindingCommand.cs',
    'Core\SimpleFamilyMaterialBindingService.cs',
    'UI\SimpleFamilyMaterialBindingWindow.xaml',
    'UI\SimpleFamilyMaterialBindingWindow.xaml.cs'
)
$ProjectSourceRoot = Join-Path $ProjectRoot 'src\YangTools.Revit'
foreach ($relative in $RequiredSettingsContent) {
    $fullPath = Join-Path $ProjectSourceRoot $relative
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Required System Settings file is missing: $relative"
    }
}


$GeneratorChecks = @(
    @{
        Name = 'EntityGenerator window has no modal Hide/Show selection flow'
        Path = 'src\YangTools.Revit\UI\EntityGeneratorWindow.xaml.cs'
        Forbidden = @('Hide()', '.Selection.PickObject', '.Selection.PickObjects')
    },
    @{
        Name = 'SolidGenerator window has no modal Hide/Show selection flow'
        Path = 'src\YangTools.Revit\UI\SolidGeneratorWindow.xaml.cs'
        Forbidden = @('Hide()', '.Selection.PickObject', '.Selection.PickObjects')
    }
)

foreach ($check in $GeneratorChecks) {
    $fullPath = Join-Path $ProjectRoot $check.Path
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Generator source file is missing: $($check.Path)"
    }
    $content = Get-Content -LiteralPath $fullPath -Raw -Encoding UTF8
    foreach ($token in $check.Forbidden) {
        if ($content.Contains($token)) {
            throw "Generator regression detected in $($check.Path): forbidden token '$token'."
        }
    }
}

$GeneratorCommandFiles = @(
    'src\YangTools.Revit\Commands\EntityGeneratorCommand.cs',
    'src\YangTools.Revit\Commands\SolidGeneratorCommand.cs'
)
foreach ($relative in $GeneratorCommandFiles) {
    $fullPath = Join-Path $ProjectRoot $relative
    $content = Get-Content -LiteralPath $fullPath -Raw -Encoding UTF8
    if (-not $content.Contains('GeneratorDialogAction.SelectInput')) {
        throw "Generator selection state machine is missing: $relative"
    }
    if (-not ($content.Contains('.Selection.PickObject') -or $content.Contains('.Selection.PickObjects'))) {
        throw "Revit selection is not owned by the external command: $relative"
    }
}

$GeneratorEngineFiles = @(
    'src\YangTools.Revit\Core\EntityGeneratorEngine.cs',
    'src\YangTools.Revit\Core\SolidGeneratorEngine.cs'
)
foreach ($relative in $GeneratorEngineFiles) {
    $fullPath = Join-Path $ProjectRoot $relative
    $content = Get-Content -LiteralPath $fullPath -Raw -Encoding UTF8
    $saveIndex = $content.IndexOf('familyDocument.SaveAs')
    $groupIndex = $content.IndexOf('new TransactionGroup')
    if ($saveIndex -lt 0 -or $groupIndex -lt 0 -or $saveIndex -ge $groupIndex) {
        throw "Family SaveAs must occur before the project TransactionGroup: $relative"
    }
}

$ActionFile = Join-Path $ProjectRoot 'src\YangTools.Revit\Models\GeneratorDialogAction.cs'
if (-not (Test-Path -LiteralPath $ActionFile)) {
    throw 'GeneratorDialogAction.cs is missing.'
}


$CategoryHelper = Join-Path $ProjectRoot 'src\YangTools.Revit\Core\BuiltInCategoryHelper.cs'
if (-not (Test-Path -LiteralPath $CategoryHelper)) {
    throw 'BuiltInCategoryHelper.cs is missing.'
}
foreach ($relative in @(
    'src\YangTools.Revit\UI\EntityGeneratorWindow.xaml.cs',
    'src\YangTools.Revit\UI\SolidGeneratorWindow.xaml.cs'
)) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $relative) -Raw -Encoding UTF8
    if ($content.Contains('Enum.IsDefined(typeof(BuiltInCategory)')) {
        throw "Legacy BuiltInCategory enum conversion remains: $relative"
    }
    if (-not $content.Contains('BuiltInCategoryHelper.TryFromCategory')) {
        throw "BuiltInCategory helper is not used: $relative"
    }
}

$UserSettingsFile = Join-Path $ProjectRoot 'src\YangTools.Revit\Core\UserSettings.cs'
$UserSettingsContent = Get-Content -LiteralPath $UserSettingsFile -Raw -Encoding UTF8
foreach ($token in @(
    'BooleanDeleteTargetInst',
    'LinearStartValue',
    'LinearReversePath',
    'LinearIncludeRemainderEndpoint',
    'ColumnAlongPathSpacingMm',
    'ColumnAlongPathBaseLevelName',
    'ColumnAlongPathIncludeEndColumn',
    'ColumnAlongPathRotationOffsetDegrees',
    'EntityGeneratorFamilyName',
    'SolidGeneratorFamilyName'
)) {
    if (-not $UserSettingsContent.Contains($token)) {
        throw "Persistent input setting is missing: $token"
    }
}

foreach ($relative in @(
    'src\YangTools.Revit\Core\EntityGeneratorEngine.cs',
    'src\YangTools.Revit\Core\SolidGeneratorEngine.cs'
)) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $relative) -Raw -Encoding UTF8
    if (-not $content.Contains('string.IsNullOrWhiteSpace(state.MaterialName)')) {
        throw "Optional material behavior is missing: $relative"
    }
}


$ManagementWindows = @(
    'src\YangTools.Revit\UI\FamilyManagerWindow.xaml.cs',
    'src\YangTools.Revit\UI\FamilyInstanceManagerWindow.xaml.cs',
    'src\YangTools.Revit\UI\ProjectAssetManagerWindow.xaml.cs',
    'src\YangTools.Revit\UI\SheetManagerWindow.xaml.cs'
)
foreach ($relative in $ManagementWindows) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $relative) -Raw -Encoding UTF8
    if (-not $content.Contains('new RevitEventHandler(_doc, wrapInTransactionGroup: false')) {
        throw "Document-bound write ExternalEvent is missing: $relative"
    }
    if (-not $content.Contains('new RevitReadEventHandler(_doc)')) {
        throw "Document-bound read ExternalEvent is missing: $relative"
    }
}

$ParameterHelper = Join-Path $ProjectRoot 'src\YangTools.Revit\Core\ParameterValueHelper.cs'
if (-not (Test-Path -LiteralPath $ParameterHelper)) {
    throw 'ParameterValueHelper.cs is missing.'
}
$ParameterHelperContent = Get-Content -LiteralPath $ParameterHelper -Raw -Encoding UTF8
if (-not $ParameterHelperContent.Contains('SetValueString')) {
    throw 'Display-unit parameter writing is missing.'
}

$SheetManagerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\SheetManagerWindow.xaml.cs') -Raw -Encoding UTF8
if ($SheetManagerSource.Contains('List<FamilySymbol> _availableTitleblocks')) {
    throw 'Sheet Manager still binds Revit FamilySymbol objects directly to modeless WPF.'
}
if (-not $SheetManagerSource.Contains('TitleblockTypeOption')) {
    throw 'Sheet Manager titleblock DTO is missing.'
}



$MicroToolRequired = @(
    'src\YangTools.Revit\Models\MicroToolModels.cs',
    'src\YangTools.Revit\UI\MicroCommandPickerWindow.xaml',
    'src\YangTools.Revit\UI\MicroCommandPickerWindow.xaml.cs'
)
foreach ($relative in $MicroToolRequired) {
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot $relative))) {
        throw "Project MicroTool file is missing: $relative"
    }
}

$MicroToolEngineSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Commands\MicroToolEngine.cs') -Raw -Encoding UTF8
foreach ($token in @('micro-projects.json', 'AssemblyResolve', 'Assembly.LoadFile', 'MicroCommandPickerWindow', 'MicroToolCache')) {
    if (-not $MicroToolEngineSource.Contains($token)) {
        throw "Project MicroTool regression detected: $token"
    }
}
if ($MicroToolEngineSource.Contains('FirstOrDefault(t => typeof(IExternalCommand)')) {
    throw 'Project MicroTool still executes an arbitrary first IExternalCommand.'
}
if ($MicroToolEngineSource.Contains('yield return (candidate, true)')) {
    throw 'Project MicroTool candidate iterator still contains the CS1626 yield/catch pattern.'
}
if (-not $MicroToolEngineSource.Contains('var results = new List<(string file, bool isPackage)>()')) {
    throw 'Project MicroTool candidate enumeration result buffer is missing.'
}

$CadHelper = Join-Path $ProjectRoot 'src\YangTools.Revit\Core\CadClipboardFileHelper.cs'
if (-not (Test-Path -LiteralPath $CadHelper)) {
    throw 'CadClipboardFileHelper.cs is missing.'
}
$PasteCadSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Commands\PasteCadCommand.cs') -Raw -Encoding UTF8
foreach ($token in @('CadClipboardFileHelper.CreateWorkingCopy', 'TransactionStatus.Committed', 'Selection.SetElementIds', 'MainWindowHandle')) {
    if (-not $PasteCadSource.Contains($token)) {
        throw "Paste CAD regression detected: $token"
    }
}
if ($PasteCadSource.Contains('YangTools_Temp_')) {
    throw 'Paste CAD still uses a shared fixed temporary filename.'
}

foreach ($token in @('MicroToolLastProjectPath', 'MicroToolLastToolPath', 'PasteCadUnitIndex', 'PasteCadThisViewOnly')) {
    if (-not $UserSettingsContent.Contains($token)) {
        throw "Persistent MicroTool/PasteCAD setting is missing: $token"
    }
}



$ModelessRegistry = Join-Path $ProjectRoot 'src\YangTools.Revit\Core\ModelessWindowRegistry.cs'
if (-not (Test-Path -LiteralPath $ModelessRegistry)) {
    throw 'ModelessWindowRegistry.cs is missing.'
}
foreach ($relative in @(
    'src\YangTools.Revit\Commands\FamilyManagerCommand.cs',
    'src\YangTools.Revit\Commands\FamilyInstanceManagerCommand.cs',
    'src\YangTools.Revit\Commands\ProjectAssetManagerCommand.cs',
    'src\YangTools.Revit\Commands\SheetManagerCommand.cs'
)) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $relative) -Raw -Encoding UTF8
    if (-not $content.Contains('ModelessWindowRegistry.ShowOrActivate')) {
        throw "Modeless manager singleton is missing: $relative"
    }
}

$MicroToolGuide = Join-Path $ProjectRoot 'MicroTool_Developer_Guide V3.md'
if (-not (Test-Path -LiteralPath $MicroToolGuide)) {
    throw 'MicroTool_Developer_Guide V3.md is missing.'
}
foreach ($token in @(
    'CollectToolPackage',
    'microtool.package.json',
    'IsCollectedPackage',
    '.collect-',
    'IsProcessRunning'
)) {
    if (-not $MicroToolEngineSource.Contains($token)) {
        throw "Temporary plugin box regression detected: $token"
    }
}
$MicroToolWindowSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\MicroToolWindow.xaml.cs') -Raw -Encoding UTF8
foreach ($token in @('CollectToolButton_Click', 'CleanupCacheButton_Click', 'DeleteCollectedPackage')) {
    if (-not $MicroToolWindowSource.Contains($token)) {
        throw "Temporary plugin box UI regression detected: $token"
    }
}

$CadHelperSource = Get-Content -LiteralPath $CadHelper -Raw -Encoding UTF8
foreach ($token in @(
    'Process.GetCurrentProcess().Id',
    'CleanupYangToolsCache',
    'CleanupOldClipboardFiles',
    'CleanupWorkingCopy'
)) {
    if (-not $CadHelperSource.Contains($token)) {
        throw "Paste CAD cache cleanup regression detected: $token"
    }
}
$PasteCadWindowSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\PasteCadWindow.xaml.cs') -Raw -Encoding UTF8
if (-not $PasteCadWindowSource.Contains('BtnCleanupCache_Click')) {
    throw 'Paste CAD cache cleanup button handler is missing.'
}

$ProjectAssetSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\ProjectAssetManagerWindow.xaml.cs') -Raw -Encoding UTF8
foreach ($token in @(
    '!(v is ViewSheet)',
    '!CheckFilterUsedInViews(filter) && !CheckFilterUsedInTemplates(filter)',
    'tg.Assimilate()',
    'Dispatcher.Invoke(() => vm.Name = oldName)',
    'Dispatcher.Invoke(() => vm.LineWeight = oldWeight)'
)) {
    if (-not $ProjectAssetSource.Contains($token)) {
        throw "Project Asset Manager regression detected: $token"
    }
}
if ($ProjectAssetSource.Contains('Dispatcher.InvokeAsync')) {
    throw 'Project Asset Manager still delays Revit API work through Dispatcher.InvokeAsync.'
}

foreach ($token in @(
    '_revisionCache.Clear()',
    'SetSheetInfoSilently',
    'SetValueSilently',
    'UpdateSourceTrigger = UpdateSourceTrigger.LostFocus'
)) {
    if (-not $SheetManagerSource.Contains($token)) {
        throw "Sheet Manager regression detected: $token"
    }
}



$ColumnAlongPathRequired = @(
    'src\YangTools.Revit\Commands\ColumnAlongPathCommand.cs',
    'src\YangTools.Revit\Core\ColumnAlongPathEngine.cs',
    'src\YangTools.Revit\Models\ColumnAlongPathModels.cs',
    'src\YangTools.Revit\UI\ColumnAlongPathWindow.xaml',
    'src\YangTools.Revit\UI\ColumnAlongPathWindow.xaml.cs',
    'src\YangTools.Revit\Icons\column_along_path_16.png',
    'src\YangTools.Revit\Icons\column_along_path_32.png'
)
foreach ($relative in $ColumnAlongPathRequired) {
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot $relative))) {
        throw "Column along path file is missing: $relative"
    }
}

$ColumnAlongPathEngineSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\ColumnAlongPathEngine.cs') -Raw -Encoding UTF8
foreach ($token in @(
    'PickContinuousModelLines',
    'StructuralType.Column',
    'FAMILY_BASE_LEVEL_PARAM',
    'FAMILY_BASE_LEVEL_OFFSET_PARAM',
    'FAMILY_TOP_LEVEL_PARAM',
    'FAMILY_TOP_LEVEL_OFFSET_PARAM',
    'IncludeEndColumn',
    'ReversePath',
    'RotationOffsetDegrees',
    'EvaluateFrameAtDistance',
    'ElementTransformUtils.RotateElement',
    'NormalizeSignedAngle'
)) {
    if (-not $ColumnAlongPathEngineSource.Contains($token)) {
        throw "Column along path regression detected: $token"
    }
}

$LinearPlacementEngineSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\LinearPlacementEngine.cs') -Raw -Encoding UTF8
foreach ($token in @('vm.ReversePath', 'vm.IncludeRemainderEndpoint')) {
    if (-not $LinearPlacementEngineSource.Contains($token)) {
        throw "Linear placement route option regression detected: $token"
    }
}



$DualDeployRequired = @(
    'tools\Build-Deploy-2024-2025.ps1',
    'Build-Deploy-2024-2025.cmd',
    '编译部署2024和2025.cmd'
)
foreach ($relative in $DualDeployRequired) {
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot $relative))) {
        throw "Dual 2024/2025 deploy entry is missing: $relative"
    }
}
$DualDeploySource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'tools\Build-Deploy-2024-2025.ps1') -Raw -Encoding UTF8
foreach ($token in @("@('2024', '2025')", '-p:DeployAfterBuild=true', 'Deployed ProductVersion mismatch')) {
    if (-not $DualDeploySource.Contains($token)) {
        throw "Dual deploy regression detected: $token"
    }
}


$BatchRequired = @(
    'src\YangTools.Revit\Core\BatchTasks\BatchDocumentOpener.cs',
    'src\YangTools.Revit\Models\BatchTask\BatchTaskResults.cs'
)
foreach ($relative in $BatchRequired) {
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot $relative))) {
        throw "Batch task file is missing: $relative"
    }
}
$BatchWindowSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\BatchTaskWindow.xaml.cs') -Raw -Encoding UTF8
$BatchEngineSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\BatchTasks\BatchTaskEngine.cs') -Raw -Encoding UTF8
$BatchOpenerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\BatchTasks\BatchDocumentOpener.cs') -Raw -Encoding UTF8
foreach ($token in @('BatchTaskEngine.RunLinks', 'BatchDocumentOpener.Open', 'BatchTaskEngine.RunExports', 'WriteReport')) {
    if (-not $BatchWindowSource.Contains($token)) { throw "Batch task window regression detected: $token" }
}
foreach ($token in @('OptionalFunctionalityUtils.IsNavisworksExporterAvailable', '#if REVIT2022_OR_GREATER', '不会自动退回当前视图', 'PrepareExactOutput', 'HasExistingDwgOutput', 'PrintManager', 'SelectNewPrintDriver', 'SubmitPrint', 'PrintRange.Select', 'PrintToFileName', 'RestorePrintManagerState')) {
    if (-not $BatchEngineSource.Contains($token)) { throw "Batch export regression detected: $token" }
}
foreach ($token in @('BasicFileInfo.Extract', 'IsWorkshared', 'DetachAndPreserveWorksets', 'Document.Close(false)')) {
    if (-not $BatchOpenerSource.Contains($token)) { throw "Batch document open/close regression detected: $token" }
}
foreach ($token in @('BatchOutputFolder', 'BatchUseDocumentSubfolders', 'BatchLinkSkipIfExists', 'BatchPdfCombine', 'BatchEnablePrintPdf', 'BatchPrintPdfPrinterName', 'BatchPrintPdfSetupName', 'BatchPrintPdfViewSetName', 'BatchPrintPdfFileName')) {
    if (-not $UserSettingsContent.Contains($token)) { throw "Persistent batch setting is missing: $token" }
}

$NamespaceCollisionChecks = @(
    @{
        Path = 'src\YangTools.Revit\UI\SheetManagerWindow.xaml.cs'
        Forbidden = @(
            'Binding = new Binding($"Parameters[{selectedName}].Value")',
            'sender as ComboBox'
        )
    },
    @{
        Path = 'src\YangTools.Revit\UI\FamilyManagerWindow.xaml.cs'
        Forbidden = @(
            'TypeDataGrid.Visibility = Visibility.Collapsed',
            'EmptyStatePanel.Visibility = Visibility.Visible'
        )
    },
    @{
        Path = 'src\YangTools.Revit\UI\ProjectAssetManagerWindow.xaml.cs'
        Forbidden = @(
            ' as SolidColorBrush;',
            'new SolidColorBrush('
        )
    }
)
foreach ($check in $NamespaceCollisionChecks) {
    $fullPath = Join-Path $ProjectRoot $check.Path
    $content = Get-Content -LiteralPath $fullPath -Raw -Encoding UTF8
    foreach ($token in $check.Forbidden) {
        if ($content.Contains($token)) {
            throw "WPF/Revit namespace collision regression detected in $($check.Path): $token"
        }
    }
}


$AppSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\App.cs') -Raw -Encoding UTF8
$ThemeManagerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\ThemeManager.cs') -Raw -Encoding UTF8
$ThemeHelperSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\ThemeHelper.cs') -Raw -Encoding UTF8
if ($AppSource.Contains('new System.Windows.Application()') -or $AppSource.Contains('new Application()')) {
    throw 'Revit host isolation regression: plugin still creates a second WPF Application.'
}
foreach ($token in @('XamlInitializationScope', 'BeginXamlInitialization', 'RegisterCompletedRoot', 'EnsureDictionaryAttached')) {
    if (-not $ThemeManagerSource.Contains($token)) {
        throw "ACC/WPF local resource isolation regression detected: $token"
    }
}
foreach ($token in @('Application.Current;', '_application.Resources', 'application.Resources.MergedDictionaries')) {
    if ($ThemeManagerSource.Contains($token)) {
        throw "ThemeManager must not depend on or mutate Revit Application resources: $token"
    }
}
# AI/摸鱼助手已整体删除；禁止旧 DockablePane、聊天命令或助手资源回流。
$RemovedAssistantRuntime = @(
    'src\YangTools.Revit\Commands\CopilotCommand.cs',
    'src\YangTools.Revit\Core\Assistant',
    'src\YangTools.Revit\Core\RevitApiSearcher.cs',
    'src\YangTools.Revit\UI\AssistantModeView.xaml',
    'src\YangTools.Revit\UI\AssistantModeView.xaml.cs',
    'src\YangTools.Revit\UI\AssistantDockablePaneProvider.cs',
    'src\YangTools.Revit\UI\CopilotPanel.xaml',
    'src\YangTools.Revit\UI\CopilotPanel.xaml.cs',
    'src\YangTools.Revit\Icons\copilot_16.png',
    'src\YangTools.Revit\Icons\copilot_32.png'
)
foreach ($relative in $RemovedAssistantRuntime) {
    if (Test-Path -LiteralPath (Join-Path $ProjectRoot $relative)) {
        throw "Removed AI/private assistant runtime remains: $relative"
    }
}
foreach ($token in @('RegisterDockablePane', 'AssistantDockablePaneController', 'AssistantDockablePaneProvider')) {
    if ($AppSource.Contains($token)) {
        throw "Removed assistant registration remains in App.cs: $token"
    }
}

if (-not $ThemeHelperSource.Contains('InitializeWithTheme')) {
    throw 'ThemeHelper.InitializeWithTheme is missing.'
}
$XamlCodeBehindFiles = Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI') -Filter '*.xaml.cs' -File
foreach ($file in $XamlCodeBehindFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    if ($content.Contains('InitializeComponent();')) {
        throw "Unisolated InitializeComponent remains: $($file.Name)"
    }
    if (-not $content.Contains('ThemeHelper.InitializeWithTheme(this, InitializeComponent)')) {
        throw "Themed XAML root is not isolated: $($file.Name)"
    }
}
$ParameterSelectSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\ParameterSelectWindow.cs') -Raw -Encoding UTF8
if ($ParameterSelectSource.Contains('Application.Current.TryFindResource')) {
    throw 'ParameterSelectWindow still reads Revit Application global resources.'
}

# Family category change baseline: batch-select loadable families, edit FamilyCategory in family docs, reload, verify.
$FamilyCategoryCommand = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Commands\FamilyCategoryChangeCommand.cs') -Raw -Encoding UTF8
$FamilyCategoryService = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\FamilyCategoryChangeService.cs') -Raw -Encoding UTF8
$FamilyCategoryWindowXaml = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\FamilyCategoryChangeWindow.xaml') -Raw -Encoding UTF8
$FamilyCategoryWindowCode = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\FamilyCategoryChangeWindow.xaml.cs') -Raw -Encoding UTF8
foreach ($required in @('族类别修改', 'FamilyCategoryChangeService.Execute', 'TaskDialogCommonButtons.Yes')) {
    if (-not $FamilyCategoryCommand.Contains($required)) { throw "Family category command contract is incomplete: $required" }
}
foreach ($required in @('EditFamily', 'OwnerFamily.FamilyCategory = target', 'LoadFamily(projectDocument', 'TransactionGroup', '回载后类别校验失败', 'CaptureSafetySnapshot', 'VerifySafetySnapshot', '族类型 ElementId 集合发生变化', '自定义参数 ID', 'PostReloadValidationException')) {
    if (-not $FamilyCategoryService.Contains($required)) { throw "Family category service contract is incomplete: $required" }
}
foreach ($required in @('全选', '反选', '全不选', '目标类别', '执行修改')) {
    if (-not $FamilyCategoryWindowXaml.Contains($required)) { throw "Family category window contract is incomplete: $required" }
}
foreach ($required in @('OST_GenericModel', 'family.IsEditable', 'family.IsInPlace', 'FamilyCategoryChangeRequest')) {
    if (-not $FamilyCategoryWindowCode.Contains($required)) { throw "Family category selection contract is incomplete: $required" }
}

$BrokerContent = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Mcp\RevitExecutionBroker.cs') -Raw -Encoding UTF8
$GatewayContent = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Mcp\McpGateway.cs') -Raw -Encoding UTF8
$GatewaySettingsContent = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Mcp\McpGatewaySettings.cs') -Raw -Encoding UTF8
$McpServerContent = Get-Content -LiteralPath (Join-Path $ProjectRoot 'McpServer\server.py') -Raw -Encoding UTF8
$McpRequirementsContent = Get-Content -LiteralPath (Join-Path $ProjectRoot 'McpServer\requirements.txt') -Raw -Encoding UTF8
$SystemSettingsXaml = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\SystemSettingsWindow.xaml') -Raw -Encoding UTF8
foreach ($required in @('Task.WhenAny', '等待 Revit 主线程执行超时', 'IsCompleted', 'EnsurePythonEngine', 'get_selection', 'query_elements', 'execute_revit_python')) {
    if (-not $BrokerContent.Contains($required)) {
        throw "Revit execution broker is incomplete: $required"
    }
}
foreach ($required in @('IPAddress.Loopback', '8081', '8090', 'Authorization', 'Bearer', 'instances', 'GenerateToken', 'AllowPythonExecution')) {
    if (-not $GatewayContent.Contains($required)) {
        throw "Optional MCP gateway is incomplete: $required"
    }
}
if (-not $GatewaySettingsContent.Contains('AutoStart') -or -not $GatewaySettingsContent.Contains('false')) {
    throw 'MCP gateway default-off setting is missing.'
}
foreach ($forbidden in @('run_revit_command', 'run_pyrevit_script', 'http://127.0.0.1:8081/mcp/')) {
    if ($McpServerContent.Contains($forbidden)) {
        throw "Obsolete/fixed-port MCP bridge behavior remains: $forbidden"
    }
}
foreach ($required in @('FastMCP', '@mcp.tool()', 'mcp.run(transport="stdio")', 'YANGTOOLS_MCP_INSTANCE', 'REVIT_MCP_TOKEN', 'Authorization', 'Bearer', 'get_active_document', 'get_element_parameters', 'execute_revit_python')) {
    if (-not $McpServerContent.Contains($required)) {
        throw "MCP stdio bridge discovery/tool surface is incomplete: $required"
    }
}
if (-not $McpRequirementsContent.Contains('mcp>=1.2.0,<2')) { throw 'MCP Python SDK dependency is missing.' }
$PythonCache = @(Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'McpServer') -Recurse -Force | Where-Object { $_.Name -eq '__pycache__' -or $_.Extension -eq '.pyc' })
if ($PythonCache.Count -gt 0) { throw 'Python cache artifacts remain in McpServer.' }
foreach ($required in @('MCP与执行服务', 'StartMcp_Click', 'StopMcp_Click', 'RestartMcp_Click', 'CopyMcpConfig_Click', 'TestMcp_Click')) {
    if (-not $SystemSettingsXaml.Contains($required)) {
        throw "MCP status page is incomplete: $required"
    }
}
foreach ($obsolete in @('src\YangTools.Revit\Mcp\McpExternalEventHandler.cs', 'src\YangTools.Revit\Mcp\McpHttpServer.cs')) {
    if (Test-Path -LiteralPath (Join-Path $ProjectRoot $obsolete)) {
        throw "Obsolete MCP source remains: $obsolete"
    }
}

# Runtime icon assets must be paired, referenced, actually 16/32 pixels and use alpha-capable PNGs.
$IconRoot = Join-Path $ProjectRoot 'src\YangTools.Revit\Icons'
$IconGuide = Join-Path $ProjectRoot 'docs\ICON_DESIGN_GUIDE.md'
$IconGenerator = Join-Path $ProjectRoot 'tools\Generate-RibbonIcons.py'
if (-not (Test-Path -LiteralPath $IconGuide) -or -not (Test-Path -LiteralPath $IconGenerator)) {
    throw 'Icon design guide or generator is missing.'
}
$CommandText = (Get-ChildItem -LiteralPath $Commands -Filter '*.cs' -File | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8
}) -join "`n"
$ReferencedIcons = @([regex]::Matches($CommandText, 'Icons/([A-Za-z0-9_\-]+\.png)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
$ActualIcons = @(Get-ChildItem -LiteralPath $IconRoot -Filter '*.png' -File | ForEach-Object { $_.Name } | Sort-Object -Unique)
$MissingIcons = @($ReferencedIcons | Where-Object { $_ -notin $ActualIcons })
$UnusedIcons = @($ActualIcons | Where-Object { $_ -notin $ReferencedIcons })
if ($MissingIcons.Count -gt 0) { throw ('Referenced icons are missing: ' + ($MissingIcons -join ', ')) }
if ($UnusedIcons.Count -gt 0) { throw ('Unused runtime icons remain: ' + ($UnusedIcons -join ', ')) }
foreach ($icon in $ActualIcons) {
    if ($icon -notmatch '^(?<name>.+)_(?<size>16|32)\.png$') {
        throw "Icon does not follow the name_16/name_32 convention: $icon"
    }
    $expectedSize = [int]$Matches.size
    $otherSize = if ($Matches.size -eq '16') { '32' } else { '16' }
    $pair = $Matches.name + '_' + $otherSize + '.png'
    if ($pair -notin $ActualIcons) { throw "Icon size pair is missing for $icon" }
    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::FromFile((Join-Path $IconRoot $icon))
    try {
        if ($bitmap.Width -ne $expectedSize -or $bitmap.Height -ne $expectedSize) {
            throw "Icon pixel dimensions are wrong: $icon is $($bitmap.Width)x$($bitmap.Height)"
        }
        if (($bitmap.PixelFormat.ToString() -notmatch 'Alpha') -and ($bitmap.PixelFormat.ToString() -notmatch 'Argb')) {
            throw "Icon does not use an alpha-capable pixel format: $icon"
        }
    }
    finally { $bitmap.Dispose() }
}
$NonPngIconAssets = @(Get-ChildItem -LiteralPath $IconRoot -File | Where-Object { $_.Extension -ne '.png' })
if ($NonPngIconAssets.Count -gt 0) {
    throw ('Non-PNG runtime icon assets remain: ' + (($NonPngIconAssets | ForEach-Object { $_.Name }) -join ', '))
}
$SolidGeneratorSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Commands\SolidGeneratorCommand.cs') -Raw -Encoding UTF8
if (-not $SolidGeneratorSource.Contains('Icons/solid_extract_32.png') -or $SolidGeneratorSource.Contains('Icons/entity_generator_32.png')) {
    throw 'Solid extraction still shares the Loft icon.'
}

# 3.1.20: WPF ComboBox must be fully qualified in files that also import Autodesk.Revit.UI.
$WpfComboBoxFiles = @(
    'src\YangTools.Revit\UI\DistributeTextWindow.xaml.cs',
    'src\YangTools.Revit\UI\AlignTextToLineWindow.xaml.cs'
)
foreach ($relative in $WpfComboBoxFiles) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $relative) -Raw -Encoding UTF8
    if ($content -match '(?m)\b(?:GetSelectedTag|SelectComboValue)\(ComboBox\s+') {
        throw "Unqualified WPF ComboBox causes Autodesk.Revit.UI ambiguity: $relative"
    }
    if (-not $content.Contains('System.Windows.Controls.ComboBox comboBox')) {
        throw "Qualified WPF ComboBox declaration is missing: $relative"
    }
}

# Text tools must not select Revit elements from inside a modal WPF window.
$TextWindowFiles = @(
    'src\YangTools.Revit\UI\DistributeTextWindow.xaml.cs',
    'src\YangTools.Revit\UI\AlignTextToLineWindow.xaml.cs',
    'src\YangTools.Revit\UI\AlignTextToTextWindow.xaml.cs',
    'src\YangTools.Revit\UI\MergeTextWindow.xaml.cs'
)
foreach ($relative in $TextWindowFiles) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $relative) -Raw -Encoding UTF8
    foreach ($forbidden in @('Hide()', '.Selection.PickObject', '.Selection.PickObjects', 'RowDefinitions.Insert')) {
        if ($content.Contains($forbidden)) {
            throw "Text tool modal-selection/layout regression in ${relative}: ${forbidden}"
        }
    }
    if (-not $content.Contains('TextToolDialogAction')) {
        throw "Text tool dialog state action is missing: $relative"
    }
}
foreach ($relative in @(
    'src\YangTools.Revit\Commands\DistributeTextCommand.cs',
    'src\YangTools.Revit\Commands\AlignTextToLineCommand.cs',
    'src\YangTools.Revit\Commands\AlignTextToTextCommand.cs',
    'src\YangTools.Revit\Commands\MergeTextCommand.cs'
)) {
    $content = Get-Content -LiteralPath (Join-Path $ProjectRoot $relative) -Raw -Encoding UTF8
    if (-not ($content.Contains('.Selection.PickObject') -or $content.Contains('.Selection.PickObjects') -or $content.Contains('.Selection.PickPoint'))) {
        throw "Text tool Revit selection is not owned by the external command: $relative"
    }
}


$ParameterCleanupRequired = @(
    'src\YangTools.Revit\Commands\DuplicateProjectParameterCleanupCommand.cs',
    'src\YangTools.Revit\UI\DuplicateProjectParameterCleanupWindow.xaml',
    'src\YangTools.Revit\UI\DuplicateProjectParameterCleanupWindow.xaml.cs',
    'src\YangTools.Revit\Core\ParameterCleanup\ParameterScanService.cs',
    'src\YangTools.Revit\Core\ParameterCleanup\ParameterMergeService.cs',
    'src\YangTools.Revit\Core\ParameterCleanup\ParameterValueIndexService.cs',
    'src\YangTools.Revit\Core\ParameterCleanup\ParameterDeleteService.cs',
    'src\YangTools.Revit\Core\ParameterCleanup\ParameterCleanupPreviewBuilder.cs',
    'src\YangTools.Revit\Core\ParameterCleanup\ParameterCleanupVerificationService.cs',
    'src\YangTools.Revit\Icons\parameter_cleanup_16.png',
    'src\YangTools.Revit\Icons\parameter_cleanup_32.png'
)
foreach ($relative in $ParameterCleanupRequired) {
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot $relative))) {
        throw "Duplicate project parameter cleanup file is missing: $relative"
    }
}
$ParameterCommandSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Commands\DuplicateProjectParameterCleanupCommand.cs') -Raw -Encoding UTF8
foreach ($required in @('检查工具区', '重复项目参数清理', 'parameter_cleanup_32.png')) {
    if (-not $ParameterCommandSource.Contains($required)) {
        throw "Duplicate project parameter Ribbon regression: $required"
    }
}
$ParameterMergeSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\ParameterCleanup\ParameterMergeService.cs') -Raw -Encoding UTF8
foreach ($required in @('MergeValuesIndexed', 'operationsByElementId', 'TryGetParameterByIdentity', 'ParameterValueUtil.TrySetFrom', 'ParameterApiCompatibility.ReInsert', 'IdentityFailureAbortThreshold', '值引擎提前停止')) {
    if (-not $ParameterMergeSource.Contains($required)) {
        throw "Duplicate project parameter algorithm regression: $required"
    }
}
if ($ParameterMergeSource.Contains('LookupParameter(')) {
    throw 'Duplicate project parameter cleanup must identify parameters by ID, not LookupParameter(name).'
}

$ParameterValueIndexSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\ParameterCleanup\ParameterValueIndexService.cs') -Raw -Encoding UTF8
foreach ($required in @('WhereElementIsNotElementType', 'WhereElementIsElementType', 'GetElementIds', 'HasGlobalParameterAssociation')) {
    if (-not $ParameterValueIndexSource.Contains($required)) {
        throw "Duplicate project parameter value-index regression: $required"
    }
}

$ParameterValueSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\ParameterCleanup\ParameterValueUtil.cs') -Raw -Encoding UTF8
foreach ($required in @('AsDouble()', 'AsInteger()', 'AsElementId()', 'target.Set(source.AsDouble())')) {
    if (-not $ParameterValueSource.Contains($required)) {
        throw "Duplicate project parameter raw-value transfer regression: $required"
    }
}
$ParameterDeleteSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\ParameterCleanup\ParameterDeleteService.cs') -Raw -Encoding UTF8
foreach ($required in @('_document.Delete(requestedIds)', 'DeleteBatchAdaptive', 'DefaultBatchSize = 250', 'GetDependentElements(null)', 'allowedDeletedValues', 'subTransaction.RollBack()', 'DescribeElements(unexpectedIds)', 'SplitCount')) {
    if (-not $ParameterDeleteSource.Contains($required)) {
        throw "Duplicate project parameter adaptive-delete regression: $required"
    }
}
$ParameterWindowXaml = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\DuplicateProjectParameterCleanupWindow.xaml') -Raw -Encoding UTF8
foreach ($required in @('全选可处理项', '全不选', '反选', '测试：最多 10 个来源参数', 'GroupSelectionCheckBox_Click')) {
    if (-not $ParameterWindowXaml.Contains($required)) {
        throw "Duplicate project parameter selection-toolbar regression: $required"
    }
}
$ParameterWindowCode = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\DuplicateProjectParameterCleanupWindow.xaml.cs') -Raw -Encoding UTF8
foreach ($required in @('ApplySelection', 'FirstTenSources', 'remainingSources = 10', 'UpdateSelectionSummary', 'CaptureBefore', 'VerifyAfter', 'WriteIntegrityVerification', 'snapshot.ValueIndex', 'DispatcherPriority.Render')) {
    if (-not $ParameterWindowCode.Contains($required)) {
        throw "Duplicate project parameter selection behavior regression: $required"
    }
}
if ($ParameterWindowCode.Contains('Task.Run(')) {
    throw 'Duplicate project parameter cleanup must not call Revit API from Task.Run.'
}


$ParameterVerificationSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\ParameterCleanup\ParameterCleanupVerificationService.cs') -Raw -Encoding UTF8
foreach ($required in @('WhereElementIsNotElementType', 'WhereElementIsElementType', 'ParameterValueUtil.HasMeaningfulValue', 'StoredValuesEqual', 'FullyCleanedGroupsUniquePassed', '原有非空内部值在清理后不存在')) {
    if (-not $ParameterVerificationSource.Contains($required)) {
        throw "Duplicate project parameter integrity verification regression: $required"
    }
}
if ($ParameterVerificationSource.Contains('AsValueString()')) {
    throw 'Integrity verification must compare raw internal values, not AsValueString().'
}
$ParameterReportSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\ParameterCleanup\CsvReportWriter.cs') -Raw -Encoding UTF8
foreach ($required in @('08_参数值完整性复核.csv', '09_已选参数唯一性复核.csv', '10_运行诊断.csv')) {
    if (-not $ParameterReportSource.Contains($required)) {
        throw "Duplicate project parameter verification CSV regression: $required"
    }
}

$InstallerRequiredFiles = @(
    'installer\YangTools安装器.ps1',
    'installer\YangTools安装器.cmd',
    'installer\Install.ps1',
    'installer\Uninstall.ps1',
    'tools\Test-UiPanelsStatic.py',
    'tools\Test-ParameterCleanupPipeline.py',
    'tools\Test-AlignTextWidth.py',
    '静态检查面板与编码.cmd'
)
foreach ($relative in $InstallerRequiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot $relative))) {
        throw "Installer or UI verification entry is missing: $relative"
    }
}
$GuiInstallerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'installer\YangTools安装器.ps1') -Raw -Encoding UTF8
foreach ($required in @('2021','2022','2023','2024','2025','2026','2027','YangTools.installing','YangTools.backup','Autodesk\Revit\Addins')) {
    if (-not $GuiInstallerSource.Contains($required)) {
        throw "GUI installer regression detected: $required"
    }
}

$UiStaticTest = Join-Path $ProjectRoot 'tools\Test-UiPanelsStatic.py'
if (-not (Test-Path -LiteralPath $UiStaticTest)) {
    throw 'UI static verification script is missing.'
}

foreach ($required in @('Get-VersionSource', 'BuildOutputRoot', '跳过 Revit', 'UI\SharedStyles.xaml', 'RevitParameterDeduplicator.addin')) {
    if (-not $GuiInstallerSource.Contains($required)) {
        throw "Installer source/build-output compatibility regression detected: $required"
    }
}

$BuildAllSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'tools\Build-All.ps1') -Raw -Encoding UTF8
foreach ($required in @('[switch]$Deploy', 'Deploy-Version', 'RevitParameterDeduplicator.addin')) {
    if (-not $BuildAllSource.Contains($required)) {
        throw "Build-All compile/deploy regression detected: $required"
    }
}

$PackageInstallerSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'tools\Package-Installer.ps1') -Raw -Encoding UTF8
foreach ($required in @('AvailableVersions', 'RequireAll', '已收集 Revit')) {
    if (-not $PackageInstallerSource.Contains($required)) {
        throw "Partial-version package regression detected: $required"
    }
}


$FeatureHelpRequired = @(
    'src\YangTools.Revit\UI\FeatureHelpWindow.xaml',
    'src\YangTools.Revit\UI\FeatureHelpWindow.xaml.cs',
    'docs\templates\SampleTool.microtool.json'
)
foreach ($relative in $FeatureHelpRequired) {
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot $relative))) {
        throw "Feature help / temporary plugin documentation file is missing: $relative"
    }
}
foreach ($required in @('FunctionDescription', 'UsageInstructions', 'SupportedRevitVersions', 'CreateDocumentationTemplate', 'LoadToolDocumentation')) {
    if (-not $MicroToolEngineSource.Contains($required) -and -not (Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Models\MicroToolModels.cs') -Raw -Encoding UTF8).Contains($required)) {
        throw "Temporary plugin documentation regression detected: $required"
    }
}
foreach ($required in @('HasDocumentation', 'CreateDocumentationForTool', 'ToolHelpButton_Click', 'DeveloperGuideButton_Click')) {
    if (-not $MicroToolWindowSource.Contains($required)) {
        throw "Temporary plugin usage/function help regression detected: $required"
    }
}
$MicroToolGuideContent = Get-Content -LiteralPath $MicroToolGuide -Raw -Encoding UTF8
foreach ($required in @('V3.1', '<DLL名>.microtool.json', 'FunctionDescription', 'UsageInstructions', 'net10.0-windows', 'TransactionGroup.Assimilate')) {
    if (-not $MicroToolGuideContent.Contains($required)) {
        throw "MicroTool developer guide regression detected: $required"
    }
}

$DistributeWindowSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\DistributeTextWindow.xaml.cs') -Raw -Encoding UTF8
foreach ($required in @('VerticalAlignedX', 'HorizontalAlignedY', 'TextNote.Coord', 'HelpButton_Click')) {
    if (-not $DistributeWindowSource.Contains($required)) {
        throw "Text distribution explanation/mode regression detected: $required"
    }
}
$AlignTextSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Commands\AlignTextToTextCommand.cs') -Raw -Encoding UTF8
foreach ($required in @('SubTransaction', 'LastSucceededCount', 'LastSkippedCount', 'sourceText.Width', 'target.Width = assignableWidth', 'GetMinimumAllowedWidth', 'GetMaximumAllowedWidth', 'RecordSkipDetail')) {
    if (-not $AlignTextSource.Contains($required)) {
        throw "Text baseline alignment rollback/width/report regression detected: $required"
    }
}
$AlignTextStateSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Models\TextToolStates.cs') -Raw -Encoding UTF8
if (-not $AlignTextStateSource.Contains('MatchParagraphWidth')) {
    throw 'Text baseline alignment paragraph-width state is missing.'
}
$AlignTextWindowSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\AlignTextToTextWindow.xaml') -Raw -Encoding UTF8
if (-not $AlignTextWindowSource.Contains('ChkMatchParagraphWidth')) {
    throw 'Text baseline alignment paragraph-width checkbox is missing.'
}

$ChineseCheckSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\ChineseCheckWindow.xaml.cs') -Raw -Encoding UTF8
foreach ($required in @('DisplayLimitPerPanel', 'SharedParameterElement', 'ParameterFilterElement', 'Selection.SetElementIds', 'DispatcherPriority.ContextIdle', 'StorageType.String')) {
    if (-not $ChineseCheckSource.Contains($required)) {
        throw "Chinese check scope/read-only regression detected: $required"
    }
}
foreach ($forbidden in @('_doc.Delete(', 'AsValueString()', '.Take(5000)')) {
    if ($ChineseCheckSource.Contains($forbidden)) {
        throw "Chinese check unsafe/partial scan regression detected: $forbidden"
    }
}

$SystemSettingsSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\UI\SystemSettingsWindow.xaml.cs') -Raw -Encoding UTF8
foreach ($required in @('ValidatePanelSettings', 'ReplacePanelSettings', 'RestoreAllPanels_Click', 'CanToggle', '系统设置固定显示')) {
    if (-not $SystemSettingsSource.Contains($required)) {
        throw "System panel settings regression detected: $required"
    }
}
if ($SystemSettingsSource.Contains('OnClosed(EventArgs e)') -and $SystemSettingsSource.Contains("_mcpRefreshTimer.Stop();`n            SavePanelSettings();")) {
    throw 'System panel settings are still silently saved when the window closes.'
}
$RibbonBuilderSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\RibbonBuilder.cs') -Raw -Encoding UTF8
if (-not $RibbonBuilderSource.Contains('normalItems.Count == 0')) {
    throw 'Ribbon panel empty-main-area recovery is missing.'
}
$RibbonConfigSource = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src\YangTools.Revit\Core\RibbonConfigManager.cs') -Raw -Encoding UTF8
foreach ($required in @('ReplacePanelSettings', '.tmp', '.bak')) {
    if (-not $RibbonConfigSource.Contains($required)) {
        throw "Ribbon config atomic snapshot regression detected: $required"
    }
}

$PlacementHelperPath = Join-Path $ProjectRoot 'src\YangTools.Revit\Core\FamilyPlacementHelper.cs'
if (-not (Test-Path -LiteralPath $PlacementHelperPath)) {
    throw 'Shared level-aware family placement helper is missing.'
}
$PlacementHelperSource = Get-Content -LiteralPath $PlacementHelperPath -Raw -Encoding UTF8
foreach ($required in @('CreateAtWorldPoint', 'ElementTransformUtils.MoveElement', 'LocationPoint', 'VerifyLevelAssociation', 'PositionToleranceFeet')) {
    if (-not $PlacementHelperSource.Contains($required)) {
        throw "Generator world-coordinate placement regression detected: $required"
    }
}
foreach ($generatorFile in @('Core\EntityGeneratorEngine.cs', 'Core\SolidGeneratorEngine.cs')) {
    $generatorSource = Get-Content -LiteralPath (Join-Path $ProjectRoot ('src\YangTools.Revit\' + $generatorFile)) -Raw -Encoding UTF8
    if (-not $generatorSource.Contains('FamilyPlacementHelper.CreateAtWorldPoint')) {
        throw "Generator does not use the shared level-aware placement helper: $generatorFile"
    }
    if ($generatorSource.Contains('SetInstanceOffset(')) {
        throw "Generator still guesses a writable offset parameter instead of verifying world position: $generatorFile"
    }
}

Write-Host 'YangTools source baseline verification passed.' -ForegroundColor Green
Write-Host "Version: $ActualVersion"
Write-Host "RibbonButton count: $RibbonCount"
Write-Host 'Removed Ribbon entries: absent'
Write-Host 'System Settings entry and page files: present'
Write-Host 'Generator modal selection flow: verified'
Write-Host 'Generator family SaveAs transaction order: verified'
Write-Host 'Project management ExternalEvent binding: verified'
Write-Host 'Display-unit parameter editing: verified'
Write-Host 'Temporary plugin box collection, shadow cache, per-tool function/usage documentation and guide V3.1: verified'
Write-Host 'Paste CAD candidate, transaction and cache cleanup: verified'
Write-Host 'Modeless project managers and UI rollback safeguards: verified'
Write-Host 'WPF/Revit namespace collisions: verified'
Write-Host 'Column along path placement and constraints: verified'
Write-Host 'Linear placement reverse/end options: verified'
Write-Host 'Revit 2024/2025 dual build and deploy entry: verified'
Write-Host 'Batch processing, synchronized-link workflow and Revit 2021+ virtual-printer PDF printing: verified'
Write-Host 'ACC/Revit local WPF resources, removed AI assistant baseline, and family category change: verified'
Write-Host 'Compact AI display, resilient endpoints, execution broker, optional MCP gateway and FastMCP bridge: verified'
Write-Host "Ribbon icons: $($ActualIcons.Count) referenced PNG files, no unused assets"
Write-Host 'Icon design guide and reproducible generator: verified'
Write-Host 'Duplicate project parameter cleanup: Ribbon, theme UI, single-pass merge, raw-value copy, adaptive delete, value-integrity and uniqueness verification verified'
Write-Host 'Text baseline alignment: X/Y/rotation and exact TextNote.Width synchronization with range validation verified'


