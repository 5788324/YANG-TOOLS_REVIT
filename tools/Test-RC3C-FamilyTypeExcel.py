#!/usr/bin/env python3
"""Family type Excel atomic safety guards (static source contracts; not Revit runtime)."""
from pathlib import Path
import sys

root=Path(__file__).resolve().parents[1]
service=(root/'src/YangTools.Revit/Core/FamilyInstances/FamilyTypeExcelImportService.cs').read_text(encoding='utf-8')
ui=(root/'src/YangTools.Revit/UI/FamilyManagerWindow.xaml.cs').read_text(encoding='utf-8')
review=(root/'tools/Test-ReviewFixes.py').read_text(encoding='utf-8')
build=(root/'tools/Invoke-RC3C-WindowsCompileOnly.ps1').read_text(encoding='utf-8')
start=ui.index('private void ImportExcel_Click(')
end=ui.index('private void BatchRename_Click(',start)
import_ui=ui[start:end]
checks={
 'service validates document and transaction state':'doc.IsReadOnly || doc.IsModifiable' in service,
 'service validates selected family':'doc.GetElement(expectedFamilyId) as Family' in service,
 'service validates row count':'rows == null || rows.Count == 0' in service,
 'rejects invalid id':'long.TryParse' in service and 'id <= 0' in service,
 'rejects duplicate id':'!seenIds.Add(id)' in service,
 'rejects cross-family type':'!familyTypeIdSet.Contains(id)' in service,
 'requires actual FamilySymbol':'as FamilySymbol' in service,
 'rejects parameter identity ambiguity':'StableParameterKey.ResolveByDisplayLabel(type, cell.Key)' in service,
 'rejects readonly and unsupported parameter':'ParameterValueHelper.CanEditAsText(parameter)' in service,
 'rejects duplicate parameter headers':'!parameterKeys.Add(identity)' in service,
 'rejects family type name collisions':'!actualFinalNames.Add(finalName)' in service,
 'no-op prevents artificial undo':'if (plans.Count == 0) return result;' in service,
 'one transaction':'new Transaction(doc, "族类型 Excel 整批导入")' in service and service.count('new Transaction(')==1,
 'preflight precedes transaction':service.index('!actualFinalNames.Add(finalName)') < service.index('new Transaction(doc'),
 'transaction start checked':'transaction.Start() != TransactionStatus.Started' in service,
 'failed set rejects batch':'!ParameterValueHelper.TrySetFromDisplayValue' in service,
 'precommit readback':'Check before commit' in service and service.index('Check before commit') < service.index('transaction.Commit()'),
 'commit checked':'status != TransactionStatus.Committed' in service,
 'rollback checked':'transaction.RollBack() != TransactionStatus.RolledBack' in service,
 'explicit unknown rollback error':'回滚结果未知' in service,
 'post-commit readback':'result.ReadbackConfirmed = plans.All' in service,
 'readback uses internal numeric values':'parameter.AsDouble()' in service and 'parameter.AsInteger()' in service,
 'UI calls atomic service':'FamilyTypeExcelImportService.Execute(' in import_ui,
 'UI binds family at selection time':'ElementId expectedFamilyId = _selectedFamilyId' in import_ui,
 'UI maintains document bound to window':'_doc, expectedFamilyId, rows' in import_ui,
 'UI cancels Raise denial':'_handler.CancelPendingAction(operationId)' in import_ui,
 'UI distinguishes queue state':'ExternalEventRequest.Pending' in import_ui,
 'UI handles stale document skip callback':'reason => Dispatcher.BeginInvoke' in import_ui,
 'UI separates postcommit refresh':'数据已提交，但界面刷新失败' in import_ui,
 'UI no direct transaction group':'new TransactionGroup(' not in import_ui,
 'UI no per-row commit':'transaction.Commit()' not in import_ui,
 'review test updated':'FamilyTypeExcelImportService.Execute(' in review,
 'build script does not deploy':'-p:DeployAfterBuild=false' in build and '-p:RunPostBuildEvent=Never' in build,
 'build script hashes DLL':'Get-FileHash' in build,
 'build script emits JSON evidence':'build-results.json' in build,
 'build script does not run revit installer':'Build-Deploy-2024-2025.ps1' not in build and 'Remove-Item' not in build,
}
failures=[x for x,y in checks.items() if not y]
for item,yes in checks.items():print(('PASS' if yes else 'FAIL'),item)
print(f'Result: {len(checks)-len(failures)}/{len(checks)}')
sys.exit(1 if failures else 0)
