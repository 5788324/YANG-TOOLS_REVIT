#!/usr/bin/env python3
from __future__ import annotations
import argparse, json
from pathlib import Path


def main() -> int:
    ap=argparse.ArgumentParser()
    ap.add_argument('--root',default='.')
    ap.add_argument('--output',default='artifacts/verification/batch-publisher-v2-report.json')
    args=ap.parse_args()
    root=Path(args.root).resolve()
    def read(rel): return (root/rel).read_text(encoding='utf-8-sig')
    engine=read('src/YangTools.Revit/Core/BatchTasks/BatchTaskEngine.cs')
    vm=read('src/YangTools.Revit/Models/BatchTask/BatchTaskViewModel.cs')
    opener=read('src/YangTools.Revit/Core/BatchTasks/BatchDocumentOpener.cs')
    naming=read('src/YangTools.Revit/Core/BatchTasks/BatchExportNaming.cs')
    xaml=read('src/YangTools.Revit/UI/BatchTaskWindow.xaml')
    code=read('src/YangTools.Revit/UI/BatchTaskWindow.xaml.cs')
    settings=read('src/YangTools.Revit/Core/UserSettings.cs')
    command=read('src/YangTools.Revit/Commands/BatchTaskCommand.cs')
    checks=[]
    def has(src,token,label): checks.append((label,token in src))
    def lacks(src,token,label): checks.append((label,token not in src))

    # Cloud / multi-model
    has(vm,'IsCloudModel','cloud model marker')
    has(vm,'CloudProjectGuid','cloud project guid')
    has(vm,'CloudModelGuid','cloud model guid')
    has(vm,'GetCloudModelPath()','capture cloud path')
    has(vm,'AddOpenDocuments','add opened models')
    has(opener,'ConvertCloudGUIDsToCloudPath','reopen native cloud model')
    has(opener,'OpenDocumentFile(cloudPath','open native cloud model')
    has(xaml,'添加已打开模型（含云）','cloud/opened model UI')
    has(code,'BtnAddOpenDocuments_Click','cloud/opened model handler')
    has(vm,'LocationDescription','cloud/local location display')

    # Preflight
    has(engine,'public static BatchTaskRunResult ValidateExports','preflight engine')
    has(code,'BtnPreflight_Click','preflight UI handler')
    has(xaml,'Content="预检"','preflight button')
    has(engine,'ValidateOutputPlan','preflight output naming plan')
    has(code,'plannedOutputReservations','cross-model preflight reservation set')
    has(engine,'输出命名预检：','preflight naming diagnostics')
    has(engine,'File.Exists(path) && !viewModel.OverwriteExisting','preflight existing-file guard')

    # Scope robustness
    for tok,label in [
        ('WholeModelOption','whole model scope'),('Auto3DViewOption','auto 3d scope'),('AllSheetsOption','all sheets scope'),
        ('AllPrintableViewsOption','all printable scope')]: has(vm,tok,label)
    has(engine,'"{3D}", "协调", "Coordination", "Navisworks", "NWC"','auto 3d preference')
    has(engine,'OfClass(typeof(ViewSheet))','all sheets collection')
    has(xaml,'IsEditable="True" Text="{Binding SelectedDwgSetup','editable DWG setup')
    has(xaml,'IsEditable="True" Text="{Binding SelectedPdfSetup','editable PDF setup')
    has(xaml,'IsEditable="True" Text="{Binding SelectedPrintPdfSetup','editable print setup')

    # Naming
    for tok in ['{模型名}','{模型文件名}','{图纸号}','{图纸名}','{视图名}','{序号}','{日期}']:
        has(naming,tok,f'naming token {tok}')
    has(engine,'BatchExportNaming.EnsureUniqueBaseName','unique per-view naming')
    has(settings,'BatchDwgFileName { get; set; } = "{模型名}-{图纸号}-{图纸名}"','DWG naming default')
    has(settings,'BatchPdfFileName { get; set; } = "{模型名}-{图纸号}-{图纸名}"','PDF naming default')
    has(settings,'BatchPrintPdfFileName { get; set; } = "{模型名}-{图纸号}-{图纸名}"','print naming default')

    # NWC settings
    for tok,label in [
        ('Coordinates =','NWC coordinates'),('ExportLinks = viewModel.NwcExportLinks','NWC links'),
        ('ConvertElementProperties = viewModel.NwcConvertElementProperties','NWC properties'),
        ('ExportElementIds = viewModel.NwcExportElementIds','NWC element ids'),
        ('ExportRoomAsAttribute = viewModel.NwcExportRoomAsAttribute','NWC room attributes'),
        ('ExportRoomGeometry = viewModel.NwcExportRoomGeometry','NWC rooms'),
        ('DivideFileIntoLevels = viewModel.NwcDivideFileIntoLevels','NWC levels')]: has(engine,tok,label)
    has(engine,'OptionalFunctionalityUtils.IsNavisworksExporterAvailable()','NWC exporter guard')
    has(engine,'NavisworksExportScope.Model','NWC whole model export')
    has(engine,'view?.Id ?? ElementId.InvalidElementId','view optional whole model scope')

    # IFC settings
    for tok,label in [
        ('ExportBaseQuantities = viewModel.IfcExportBaseQuantities','IFC quantities'),
        ('WallAndColumnSplitting = viewModel.IfcSplitWallsColumns','IFC split'),
        ('SpaceBoundaryLevel = viewModel.IfcSpaceBoundaryLevel','IFC boundary')]: has(engine,tok,label)
    has(engine,'OptionalFunctionalityUtils.IsIFCAvailable()','IFC module availability guard')

    # DWG exact per-view export
    has(engine,'OptionalFunctionalityUtils.IsDWGExportAvailable()','DWG module availability guard')
    has(engine,'new List<ElementId> { viewId }, options','DWG one view per export')
    has(engine,'GetViewIds(doc, viewModel.SelectedDwgViewSheetSet);','DWG requires printable/exportable views')
    has(engine,'options.SharedCoords = true','DWG shared coordinates')
    has(engine,'options.MergedViews = viewModel.DwgMergedViews','DWG merged setting')
    has(engine,'CreateStagingFolder(outputFolder, "dwg")','DWG staging')
    has(engine,'CommitStagedFiles(stagingFolder, outputFolder','staged commit')
    has(engine,'string stagedMain = Path.Combine(stagingFolder, baseName + ".dwg")','DWG exact main filename verification')
    has(engine,'IsNonEmptyFile(stagedMain)','DWG main file nonempty verification')

    # PDF exact naming / 2021 boundary
    has(engine,'options.Combine = true;','PDF exact one-file calls')
    has(engine,'ExportPdfExact(doc, new List<ElementId> { id }','PDF per-view export')
    has(engine,'PDF 导出需要 Revit 2022 或更高版本；Revit 2021 请启用“打印 PDF”','2021 native PDF boundary')
    has(engine,'options.StopOnError = true','PDF stop on error')
    has(engine,'options.SetExportInBackground(false)','2025+ PDF forced foreground for deterministic batch')
    has(engine,'IsNonEmptyFile(producedPath)','PDF nonempty output verification')

    # Print PDF
    has(vm,'PrintPdfCombine','print combine setting')
    has(engine,'PrintViewIdsToExactPdf','print exact filename helper')
    has(engine,'new List<ElementId> { id }, outputFolder, finalPath','print per-view mode')
    has(engine,'CapturePrintManagerState','print snapshot')
    has(engine,'RestorePrintManagerState','print restore')
    has(engine,'PrintRange = PrintRange.Select','print select range')
    has(engine,'WaitForFileReady(temporaryPath, 60000)','print spool wait')
    has(engine,'ValidatePrintDriver','preflight validates actual print driver')
    has(engine,'Microsoft Print to PDF 常见配置会弹出','interactive Microsoft PDF warning')
    has(engine,'printManager.PrintToFileName = snapshot.PrintToFileName ?? string.Empty','restore empty print-to-file name')
    has(engine,'if (originalViews.Size > 0)','avoid restoring invalid empty in-session view set')

    # Data protection / subfolder behavior
    has(engine,'CreateStagingFolder(outputFolder, "nwc")','NWC staging')
    has(engine,'CreateStagingFolder(outputFolder, "ifc")','IFC staging')
    has(engine,'IsNonEmptyFile(stagedPath)','NWC/IFC nonempty file verification')
    has(engine,'if (viewModel.UseDocumentSubfolders)','honor subfolder checkbox')
    lacks(engine,'if (viewModel.UseDocumentSubfolders || multipleDocuments)','no forced subfolders')
    lacks(engine,'File.Delete(finalPath)','do not pre-delete final PDF')
    has(engine,'执行前预检未通过，本模型未产生任何导出/打印文件','automatic fail-fast preflight')
    has(engine,'batchOutputReservations','batch output reservation set')
    has(engine,'同一批次存在重复输出路径','cross-model collision guard')

    # UI dimensions
    for tok,label in [
        ('Height="860" Width="1320" MinHeight="720" MinWidth="1120"','window dimensions'),
        ('RowHeight="30" ColumnHeaderHeight="34"','document grid dimensions'),
        ('<ColumnDefinition Width="360"/>','left panel width'),
        ('<ColumnDefinition Width="*" MinWidth="300"/>','output path min width'),
        ('<TabItem Header="NWC">','NWC tab'),('<TabItem Header="IFC">','IFC tab'),
        ('<TabItem Header="DWG">','DWG tab'),('<TabItem Header="PDF 导出">','PDF tab'),
        ('<TabItem Header="PDF 打印（2021+）">','print tab'),
        ('Binding="{Binding LocationDescription}"','location display'),
        ('Content="元素 ID"','NWC element ids UI'),
        ('Content="房间属性"','NWC room attributes UI')]: has(xaml,tok,label)

    has(command,'本地/已打开/云模型','ribbon tooltip updated')

    failures=[label for label,ok in checks if not ok]
    out=root/args.output; out.parent.mkdir(parents=True,exist_ok=True)
    payload={'ok':not failures,'checkCount':len(checks),'passedCount':sum(ok for _,ok in checks),'failures':failures}
    out.write_text(json.dumps(payload,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f"Batch Publisher V2 static checks: {payload['passedCount']}/{payload['checkCount']}")
    for f in failures: print('ERROR:',f)
    return 0 if not failures else 1

if __name__=='__main__': raise SystemExit(main())
