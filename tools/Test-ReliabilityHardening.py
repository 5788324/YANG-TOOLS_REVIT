#!/usr/bin/env python3
from pathlib import Path
import json, sys

ROOT = Path(__file__).resolve().parents[1]
checks=[]

def add(name, ok, detail=''):
    checks.append({'name':name,'ok':bool(ok),'detail':detail})

def text(rel):
    return (ROOT/rel).read_text(encoding='utf-8')

engine=text('src/YangTools.Revit/Core/BatchTasks/BatchTaskEngine.cs')
filecommit=text('src/YangTools.Revit/Core/BatchTasks/BatchFileCommitService.cs')
window=text('src/YangTools.Revit/UI/BatchTaskWindow.xaml.cs')
xaml=text('src/YangTools.Revit/UI/BatchTaskWindow.xaml')
trans=text('src/YangTools.Revit/Core/TransactionHelper.cs')
handler=text('src/YangTools.Revit/Core/RevitEventHandler.cs')
request=text('src/YangTools.Revit/Models/BatchTask/BatchRunRequest.cs')
vm=text('src/YangTools.Revit/Models/BatchTask/BatchTaskViewModel.cs')
family=text('src/YangTools.Revit/UI/FamilyManagerWindow.xaml.cs')
inst=text('src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml.cs')
inst_cell=text('src/YangTools.Revit/Core/FamilyInstances/FamilyInstanceCellEditService.cs')
paste=text('src/YangTools.Revit/Commands/PasteCadCommand.cs')
diag=text('src/YangTools.Revit/Core/DiagnosticLog.cs')
level=text('src/YangTools.Revit/Commands/LevelModifierCommand.cs')
level_service=text('src/YangTools.Revit/Core/LevelModifierService.cs')
dim=text('src/YangTools.Revit/Commands/DimTextOverrideCommand.cs')

# F01
add('F01 no unconditional finally backup deletion', 'finally\n            {\n                DeleteDirectoryBestEffort(backupFolder);' not in engine)
add('F01 backup retained on recovery failure', '备份目录已保留' in filecommit)
add('F01 recovery errors recorded', 'RecoveryMessages' in filecommit and '恢复旧文件失败' in filecommit)
add('F01 backup cleanup only after recovery/success', 'allBackupsRecovered' in filecommit and filecommit.count('DeleteDirectoryBestEffort(backupFolder') >= 2)

# F02
add('F02 shared full-batch preflight method', 'RunFullExportPreflight(BatchRunRequest request)' in window)
add('F02 execute invokes full-batch preflight', 'BatchTaskRunResult preflight = RunFullExportPreflight(request);' in window)
execute_pos=window.find('private void BtnExecute_Click')
preflight_pos=window.find('BatchTaskRunResult preflight = RunFullExportPreflight(request);', execute_pos)
links_pos=window.find('BatchTaskEngine.RunLinks(', execute_pos)
exports_pos=window.find('BatchTaskEngine.RunExports(', execute_pos)
add('F02 preflight before links', preflight_pos != -1 and links_pos != -1 and preflight_pos < links_pos)
add('F02 preflight before exports', preflight_pos != -1 and exports_pos != -1 and preflight_pos < exports_pos)
add('F02 shared planned reservations', 'plannedOutputReservations' in window and 'ValidateExports(' in window)

# F04 snapshot/busy
add('F04 BatchRunRequest exists', 'sealed class BatchRunRequest' in request)
add('F04 options snapshot exists', 'sealed class BatchRunOptions' in request)
add('F04 engine depends on interface', 'IBatchTaskOptions viewModel' in engine and 'BatchTaskViewModel viewModel' not in engine)
add('F04 viewmodel implements options interface', 'BatchTaskViewModel : INotifyPropertyChanged, IBatchTaskOptions' in vm)
add('F04 document list cloned', 'Documents = source.Documents' in request and 'CloneDocumentInfo' in request)
add('F04 links cloned', 'new LinkItem { FilePath = link.FilePath }' in request)
add('F04 UI input lock root named', 'x:Name="RootLayout"' in xaml)
add('F04 UI hit test locked while busy', 'RootLayout.IsHitTestVisible = !busy;' in window)

# F03 transaction result correctness
add('F03 transaction helper checks Commit return', 'status = t.Commit();' in trans and 'status == TransactionStatus.Committed' in trans)
add('F03 transaction group checks Assimilate return', 'status = tg.Assimilate();' in trans and 'status == TransactionStatus.Committed' in trans)
add('F03 no unconditional success after group rollback', '操作未提交，事务组已回滚' in trans)
add('F03 family type parameter commit checked', 'if (t.Commit() != TransactionStatus.Committed)' in family)
add('F03 family instance parameter commit checked', 'transaction.Commit() != TransactionStatus.Committed' in inst_cell)
add('F03 family type parameter readback', 'readBackValue = ParameterValueHelper.GetDisplayValue(refreshedParam);' in family)
add('F03 family instance parameter readback', 'DisplayValue = ParameterValueHelper.GetDisplayValue(actual)' in inst_cell and 'ReadbackConfirmed = false' in inst_cell)
add('F03 family manager remaining commits checked', 't.Commit();' not in family)
add('F03 level command propagates commit status', 'LevelModifierService.Execute(doc, request)' in level and 'if (!result.Committed)' in level and 'tx.Commit()' in level_service and 'status != TransactionStatus.Committed' in level_service)
add('F03 dimension command propagates commit status', 'TransactionStatus commitStatus = TransactionHelper.ShowSuccessAndCommit' in dim and 'commitStatus != TransactionStatus.Committed' in dim)

# F05 provenance
add('F05 structured product log API', 'WriteProductOperation(' in diag and 'LoadedAssemblySha256' in diag)
add('F05 product log captures process id', 'Process.GetCurrentProcess().Id' in diag)
add('F05 external event operation id', 'operationId' in handler and 'ExternalEvent.' in handler)
add('F05 product log has explicit BatchRunId field', 'BatchRunId:' in diag and 'batchRunId' in diag)
add('F05 batch has separate operation and run ids', 'string operationId = Guid.NewGuid().ToString("N")' in window and 'string batchRunId = Guid.NewGuid().ToString("N")' in window)
add('F05 batch passes BatchRunId to product log', 'batchRunId);' in window and 'BatchPublisher' in window)
add('F05 pastecad product log', 'WriteProductOperation(' in paste and 'PasteCAD' in paste and 'ImportInstanceId=' in paste)
add('F05 family batch type detailed log', 'FamilyInstance.BatchChangeType' in inst and 'TargetTypeId=' in inst)
add('F05 family batch parameter detailed log', 'FamilyInstance.BatchSetParameter' in inst and 'ParameterKey=' in inst)
add('F05 family cleanup log', 'FamilyManager.CleanupUnusedTypes' in family and 'BLOCKED_CASCADE' in family)

# Windows/.NET fault-injection harness must exercise the extracted commit service with real temp files.
harness_project = ROOT / "tools" / "BatchFileCommitFaultHarness" / "BatchFileCommitFaultHarness.csproj"
harness_program = ROOT / "tools" / "BatchFileCommitFaultHarness" / "Program.cs"
add("Harness project exists", harness_project.exists())
add("Harness program exists", harness_program.exists())
if harness_program.exists():
    hp = harness_program.read_text(encoding="utf-8")
    add("Harness injects staged move failure", "new ThrowingFileSystem(new PhysicalBatchFileSystem(), 4)" in hp)
    add("Harness injects restore failure", "new ThrowingFileSystem(new PhysicalBatchFileSystem(), 4, 5)" in hp)
    add("Harness asserts retained backup", "result.BackupRetained" in hp)
    add("Harness checks old file hashes", "OldAHash" in hp and "OldBHash" in hp)

fail=[c for c in checks if not c['ok']]
report={'checks':len(checks),'failures':len(fail),'items':checks}
out=ROOT/'artifacts/verification/reliability-hardening-report.json'
out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(f"YangTools reliability hardening static checks: {len(checks)-len(fail)}/{len(checks)}")
if fail:
    for c in fail: print('FAIL:',c['name'],c['detail'])
    sys.exit(1)
