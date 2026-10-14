#!/usr/bin/env python3
"""RC2Y source contract: targeted regression gates, NOT real Revit execution."""
import argparse
import json
from pathlib import Path

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--root',default='.')
    ap.add_argument('--output',default='artifacts/verification/rc2y-family-asset-batch.json')
    a=ap.parse_args()
    root=Path(a.root).resolve()
    def read(p):return (root/p).read_text(encoding='utf8')
    svc=read('src/YangTools.Revit/Core/FamilyInstances/FamilyInstanceExcelImportService.cs')
    ui=read('src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml.cs')
    visu=read('src/YangTools.Revit/Core/ProjectAssets/ProjectAssetFilledRegionVisualService.cs')
    asset=read('src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml.cs')
    batch=read('src/YangTools.Revit/Core/BatchTasks/BatchFileCommitService.cs')
    batchui=read('src/YangTools.Revit/Core/BatchTasks/BatchTaskEngine.cs')
    checks={}
    def ck(label,ok):checks[label]=bool(ok)
    def section(data,start,end):return data[data.index(start):data.index(end,data.index(start))]
    imported=section(ui,'        private void ImportExcel_Click(', '        private void SearchChanged(')
    pattern=section(asset,'        private void FillPattern_DropDownClosed(', '        private void DuplicateLineStyle_Click(')
    duplicate=section(asset,'        private void DuplicateFilledRegion_Click(', '        private void LineStyleColor_Click(')
    color=section(asset,'        private void FilledRegionColor_Click(', '        private void AssetDataGrid_CellEditEnding(')
    for label,frag in [
        ('Excel payload typed','IReadOnlyList<IDictionary<string, object>> Rows'),
        ('Excel no WPF dependencies','namespace YangTools.Revit.Core.FamilyInstances'),
        ('Excel documents validated','doc.IsReadOnly || doc.IsModifiable'),
        ('Excel empty rows skipped','result.SkippedBlankRows++'),
        ('Excel invalid ID rejected','图元ID无效'),
        ('Excel duplicate ID rejected','seenIds.Add(idValue)'),
        ('Excel missing target rejected','找不到图元'),
        ('Excel stable label resolution','StableParameterKey.ResolveByDisplayLabel'),
        ('Excel rejects read only','ParameterValueHelper.CanEditAsText'),
        ('Excel rejects aliasing keys','parameterIds.Add(stableId)'),
        ('Excel metadata-only tracked','result.SkippedMetadataOnlyRows++'),
        ('Excel preflight plans','plans.Add(plan);'),
        ('Excel writes once','TrySetFromDisplayValue'),
        ('Excel transaction begin checks','transaction.Start() != TransactionStatus.Started'),
        ('Excel transaction commit checks','transaction.Commit()'),
        ('Excel transaction confirms','status != TransactionStatus.Committed'),
        ('Excel rollback checks','transaction.RollBack() != TransactionStatus.RolledBack'),
        ('Excel rollback unknown reported','回滚结果未知'),
        ('Excel postcommit readback','result.ReadbackConfirmed = plans.All'),
        ('Excel postcommit failure distinguished','模型已提交，但提交后的读取失败'),
        ('Excel no op has no transaction','if (plans.Count == 0) return result;'),
        ('Excel one Undo action','实例参数 Excel 整批导入'),
    ]:ck(label,frag in svc)
    ck('Excel exactly one Transaction constructor',svc.count('new Transaction(')==1)
    ck('Excel not partial per row','new TransactionGroup(' not in svc and 'new Transaction(' not in imported)
    ck('Excel no dialogs inside service','TaskDialog' not in svc and 'Dispatcher' not in svc)
    ck('Excel UI uses new service','FamilyInstanceExcelImportService.Execute(doc' in imported)
    ck('Excel UI binds doc','!doc.Equals(_doc)' in imported)
    ck('Excel UI guard closure','_isWindowClosed' in imported)
    ck('Excel UI source path snapshot','string importPath = dialog.FileName' in imported)
    ck('Excel UI discloses partial readback','else if (!result.ReadbackConfirmed)' in imported)
    ck('Excel UI distinguishes refreshed UI failure','界面更新失败' in imported)
    ck('Excel UI explicit no-change','if (!result.Committed)' in imported)
    ck('Excel UI showSuccess false','showSuccess: false' in imported)
    ck('Excel UI no old row-by-row transactions','new Transaction(' not in imported and 'TransactionGroup' not in imported)
    for label,frag in [
        ('Visual request identity','public ElementId TypeId'),
        ('Visual pattern input','public ElementId PatternId'),
        ('Visual color input','public Autodesk.Revit.DB.Color Color'),
        ('Visual copy mode','Duplicate'),
        ('Visual foreground pattern','ForegroundPattern'),
        ('Visual background pattern','BackgroundPattern'),
        ('Visual foreground color','ForegroundColor'),
        ('Visual background color','BackgroundColor'),
        ('Visual doc preflight','doc.IsReadOnly || doc.IsModifiable'),
        ('Visual invalid type rejected','填充区域类型已不存在'),
        ('Visual missing pattern rejected','目标填充图案不存在'),
        ('Visual no op for pattern','oldPattern.GetIdValue() == expectedPattern.GetIdValue()'),
        ('Visual no op for color','SameColor(oldColor, expectedColor)'),
        ('Visual deterministic unique copy','StringComparer.OrdinalIgnoreCase'),
        ('Visual copy target id','targetId = duplicate.Id'),
        ('Visual precommit readback','if (!Matches(verify, request, newName))'),
        ('Visual commit verified','status != TransactionStatus.Committed'),
        ('Visual rollback verified','transaction.RollBack() != TransactionStatus.RolledBack'),
        ('Visual postcommit readback','ReadbackConfirmed = Matches(actual, request, newName)'),
        ('Visual no unhandled unsupported kind','不支持的填充区域编辑类型'),
    ]:ck(label,frag in visu)
    ck('Visual exactly one transaction',visu.count('new Transaction(')==1)
    ck('Visual no WPF','System.Windows' not in visu and 'Dispatcher' not in visu)
    ck('Pattern UI new service','ProjectAssetFilledRegionVisualService.Execute(_doc, request)' in pattern)
    ck('Pattern UI no transaction','new Transaction(' not in pattern)
    ck('Pattern UI masks stale tab', '_currentTabName != "FilledRegions"' in pattern)
    ck('Pattern UI no automatic success','showSuccess: false' in pattern)
    ck('Duplicate UI new service','ProjectAssetFilledRegionVisualService.Execute(_doc, request)' in duplicate)
    ck('Duplicate UI no transaction','new Transaction(' not in duplicate)
    ck('Duplicate UI refresh after model commit','LoadFilledRegions()' in duplicate)
    ck('Color UI new service','ProjectAssetFilledRegionVisualService.Execute(_doc, request)' in color)
    ck('Color UI no transaction','new Transaction(' not in color)
    ck('Color UI user cancellation','ItemSelectionDialogResult.Confirmed' in color)
    ck('Color UI readback needed','!result.ReadbackConfirmed' in color)
    ck('Color UI stale tab','tabVersion != _tabLoadVersion' in color)
    ck('Color UI refresh failure distinguished','界面刷新失败' in color)
    ck('Batch warnings retained path','result.BackupRetained = true' in batch and '备份未能清理' in batch)
    ck('Batch warnings only after successful output','result.Success = true;' in batch)
    ck('Batch warnings surfaced in caller','commit.RecoveryMessages.Count > 0' in batchui)
    ck('Batch warning surfaced for NWC','result.Warnings.Add("NWC：" + commitError)' in batchui)
    ck('Batch warning surfaced for IFC','result.Warnings.Add("IFC：" + commitError)' in batchui)
    ck('Batch warning surfaced for DWG','result.Warnings.Add($"DWG：{view.Name}：{commitError}")' in batchui)
    failed=[x for x,v in checks.items() if not v]
    out={'suite':'RC2Y Family Excel / Filled Region / Batch Backup static contracts','passed':len(checks)-len(failed),'total':len(checks),'failures':failed,'limitation':'SOURCE STATIC: no Revit, no C# compile, no UI/Undo assertions'}
    path=root/a.output;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps(out,ensure_ascii=False,indent=2))
    return 0 if not failed else 1
if __name__=='__main__':raise SystemExit(main())
