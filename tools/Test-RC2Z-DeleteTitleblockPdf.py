#!/usr/bin/env python3
"""RC2Z security/transaction contracts; static checks, NOT Revit runtime tests."""
import argparse
import json
from pathlib import Path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--root', default='.')
    ap.add_argument('--output', default='artifacts/verification/rc2z-static-contracts.json')
    a = ap.parse_args()
    root = Path(a.root).resolve()
    def read(rel):
        return (root/rel).read_text(encoding='utf-8-sig')

    fam = read('src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml.cs')
    sheets = read('src/YangTools.Revit/UI/SheetManagerWindow.xaml.cs')
    excel = read('src/YangTools.Revit/Core/Sheets/TitleblockExcelImportService.cs')
    family_excel = read('src/YangTools.Revit/Core/FamilyInstances/FamilyInstanceExcelImportService.cs')
    batch = read('src/YangTools.Revit/Core/BatchTasks/BatchFileCommitService.cs')
    engine = read('src/YangTools.Revit/Core/BatchTasks/BatchTaskEngine.cs')
    harness = read('tools/BatchFileCommitFaultHarness/Program.cs')
    checks = {}
    def ck(label, ok): checks[label] = bool(ok)
    def section(text, begin, end): return text[text.index(begin):text.index(end, text.index(begin))]

    imp = section(sheets,'        private void ImportTitleblockExcel_Click(', '        private void SelectTitleblockInView_Click(')
    fd = section(fam,'        private void ExecuteDelete(', '    public enum InstanceScopeMode')
    sd = section(sheets,'        private void DeleteSheets_Click(', '        private void EditRevisions_Click(')
    edit = section(sheets,'                vm.OnSheetInfoChanged = (prop, newVal) =>','                foreach (var paramName in _addedSheetParameters)')
    rev = section(sheets,'        private void EditRevisions_Click(', '        private void ExportSheetExcel_Click(')
    pdfsvc = section(batch,'        internal static PdfFileCommitResult CommitPdf(', '        private static bool IsReserved(')
    pdfeng = section(engine,'        private static bool ReplaceOutputSafely(', '        private static void DeleteFileBestEffort(')

    for label, frag in [
        ('Titleblock constrained to family instance','element as FamilyInstance'),
        ('Titleblock category bound','BuiltInCategory.OST_TitleBlocks'),
        ('Titleblock metadata ignored','"图纸号", "图纸名", "图框类型"'),
        ('Titleblock missing id rejects','图框图元ID无效'),
        ('Titleblock wrong class rejects','不是当前项目中的图框实例'),
        ('Titleblock one atomic backend','FamilyInstanceExcelImportService.Execute(doc'),
        ('Titleblock documents validated','doc.IsReadOnly || doc.IsModifiable'),
    ]: ck(label,frag in excel)
    ck('Titleblock service no WPF',not any(k in excel for k in ('System.Windows','TaskDialog','Dispatcher')))
    ck('Titleblock service no separate transaction', 'new Transaction(' not in excel)
    ck('Atomic shared backend', family_excel.count('new Transaction(')==1 and 'StableParameterKey.ResolveByDisplayLabel' in family_excel)
    ck('UI uses titleblock service','TitleblockExcelImportService.Execute(doc, rows)' in imp)
    ck('UI no per-row write','new Transaction(' not in imp and 'TrySetFromDisplayValue(' not in imp)
    ck('UI stale project guard','doc.Equals(_doc)' in imp)
    ck('UI readback warning','!outcome.ReadbackConfirmed' in imp)
    ck('UI refresh not mistaken for failure','刷新表格失败' in imp)
    ck('UI one Undo message','一次 Ctrl+Z' in imp)
    ck('UI no auto success','showSuccess: false' in imp)
    ck('UI snapshots Excel input','new Dictionary<string, object>(row)' in imp)

    for name, part in [('family',fd),('sheet',sd)]:
        ck(f'{name} stale project guard','doc.Equals(_doc)' in part)
        ck(f'{name} duplicate/expired IDs guard','ids.Distinct().Count() != ids.Count' in part)
        ck(f'{name} trial set captured','previewDeletedIds.UnionWith(deleted)' in part)
        ck(f'{name} trial covers requested','ids.All(previewDeletedIds.Contains)' in part)
        ck(f'{name} compare real cascade','previewDeletedIds.SetEquals(actuallyDeleted)' in part)
        ck(f'{name} real count verified','actuallyDeleted.Count != actualDeleteCount' in part)
        ck(f'{name} all requested gone readback','ids.Any(id => doc.GetElement(id) != null)' in part)
        ck(f'{name} rollback before mismatch commit','t2.Commit()' in part and part.index('SetEquals(actuallyDeleted)') < part.index('t2.Commit()'))
        ck(f'{name} one undo group','tg.Assimilate()' in part)
    ck('sheet delete no deceptive auto success','operationName: "删除图纸", showSuccess: false' in sd)

    ck('Sheet info transaction start verified','t.Start() != TransactionStatus.Started' in edit)
    ck('Sheet info commit verified','t.Commit() != TransactionStatus.Committed' in edit)
    ck('Sheet info readback','string actualValue =' in edit)
    ck('Sheet revisions start verified','transaction.Start() != TransactionStatus.Started' in rev)
    ck('Sheet revisions commit verified','transaction.Commit() != TransactionStatus.Committed' in rev)
    ck('Sheet revisions readback','GetAdditionalRevisionIds()).SetEquals(selectedRevisionIds)' in rev)
    ck('Sheet edited params check commit',sheets.count('t.Commit() != TransactionStatus.Committed') >=2)
    ck('Titleblock inline params check commit','图框参数修改未能提交' in sheets)

    ck('PDF result status','class PdfFileCommitResult' in batch)
    ck('PDF validates temp existence','if (!fileSystem.FileExists(source))' in pdfsvc)
    ck('PDF forbids unexpected overwrite','if (fileSystem.FileExists(target) && !overwrite)' in pdfsvc)
    ck('PDF retains old backup after restore fail','result.BackupRetained = true' in pdfsvc)
    ck('PDF keeps temporary on failure','暂存文件保留供检查' in pdfsvc)
    ck('PDF verifies output exists','if (!fileSystem.FileExists(target))' in pdfsvc)
    ck('PDF backup cleanup failure warns','result.Warning = "PDF 已替换成功' in pdfsvc)
    ck('PDF no direct old delete','DeleteFile(target)' not in pdfsvc and 'DeleteFile(source)' not in pdfsvc)
    ck('PDF caller delegates to service','BatchFileCommitService.CommitPdf(' in pdfeng)
    ck('PDF overwrite flag passed','finalPath, overwrite, new PhysicalBatchFileSystem()' in pdfeng)
    ck('PDF success warning propagated','error = commit.Success ? commit.Warning : commit.Error;' in pdfeng)
    ck('PDF engine combined warning','result.Warnings.Add("PDF：" + error)' in engine)
    ck('PDF engine print warning','result.Warnings.Add("打印 PDF：" + error)' in engine)
    for test in ['RunPdfReplaceSuccessCase','RunPdfMoveFailureRestoresOldCase',
                 'RunPdfRestoreFailureRetainsBackupCase','RunPdfCleanupFailureWarningCase',
                 'RunPdfOverwriteRefusedCase']:
        ck('Harness '+test, test+'();' in harness and 'void '+test+'(' in harness)

    failed=[k for k,v in checks.items() if not v]
    out={'checks':len(checks),'passed':len(checks)-len(failed),'failed':failed,
        'status':'PASS' if not failed else 'FAIL',
        'limitations':'Source contract only. C# harness included but cannot be considered executed on Linux without dotnet.'}
    path=root/a.output
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
    print('RC2Z static contracts',out['passed'],'/',out['checks'])
    for x in failed:print('FAILED:',x)
    return 1 if failed else 0


if __name__=='__main__':raise SystemExit(main())
