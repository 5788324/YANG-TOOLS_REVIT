#!/usr/bin/env python3
"""RC2J FamilyInstanceManager cell edit static contracts; NOT a Revit runtime test."""
from pathlib import Path
from xml.etree import ElementTree as ET
import re, json, sys
root=Path(__file__).resolve().parents[1]
ui=(root/'src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml.cs').read_text('utf-8-sig')
service=(root/'src/YangTools.Revit/Core/FamilyInstances/FamilyInstanceCellEditService.cs').read_text('utf-8')
xaml=(root/'src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml').read_text('utf-8-sig')
handler=(root/'src/YangTools.Revit/Core/RevitEventHandler.cs').read_text('utf-8-sig')
model=(root/'src/YangTools.Revit/Models/ParameterViewModel.cs').read_text('utf-8-sig')
body=ui.split('private void InstanceDataGrid_CellEditEnding(',1)[1].split('private void SystemDataGrid_CellEditEnding(',1)[0]
checks=[]
def ck(name,pred):checks.append((name,bool(pred)))
for name in ['FamilyInstanceCellEditRequest','FamilyInstanceCellEditResult','FamilyInstanceCellEditService']:
    ck('typed '+name,name in service)
for name in ['InstanceId','ParameterKey','NewValue']:
    ck('request '+name,re.search(r'public\s+(?:ElementId|string)\s+'+name+r'\s*\{',service))
for name in ['Changed','ReadbackConfirmed','DisplayValue','ReadbackWarning']:
    ck('result '+name,re.search(r'public\s+(?:bool|string)\s+'+name+r'\s*\{',service))
for term in ['document == null || !document.IsValidObject','document.IsReadOnly','request == null','request.InstanceId == null','request.ParameterKey','InvalidElementId.GetIdValue()',
             'document.GetElement(request.InstanceId)','!instance.IsValidObject','StableParameterKey.Resolve(instance, request.ParameterKey)',
             '!ParameterValueHelper.CanEditAsText(parameter)','ParameterValueHelper.GetDisplayValue(parameter)','StringComparison.Ordinal',
             'ParameterValueHelper.TrySetFromDisplayValue(parameter, value, out string error)',
             'transaction.Start() != TransactionStatus.Started','transaction.Commit() != TransactionStatus.Committed',
             'transaction.GetStatus() == TransactionStatus.Started','transaction.RollBack() != TransactionStatus.RolledBack',
             '回滚结果无法确认','ReadbackConfirmed = true','ReadbackConfirmed = false','ReadbackWarning =',
             'var refreshed = document.GetElement(request.InstanceId)','StableParameterKey.Resolve(refreshed, request.ParameterKey)']:
    ck('service: '+term, term in service)
ck('single transaction', service.count('new Transaction(')==1)
ck('no transaction group', 'new TransactionGroup(' not in service)
ck('no-op before transaction',service.index('string.Equals(before, value')<service.index('new Transaction('))
ck('readback only after commit',service.index('var refreshed = document.GetElement(request.InstanceId)')>service.index('transaction.Commit()'))
bare='\n'.join(x for x in service.splitlines() if not x.lstrip().startswith(('//','///')))
for forbidden in ['using System.Windows','Dispatcher.','TaskDialog.','TextBox','DataGrid','InstanceItemViewModel','UIApplication','ActiveUIDocument','ExternalEvent.Raise']:
    ck('service UI independent '+forbidden,forbidden not in bare)
for token in ['e.EditAction != DataGridEditAction.Commit','|| _isWindowClosed','grid != (isSystemTab ? SystemDataGrid : InstanceDataGrid)',
              'editor.GetBindingExpression(TextBox.TextProperty)','bindingPath.StartsWith("Parameters["','bindingPath.EndsWith("].Value"',
              'row.Parameters.TryGetValue(parameterKey, out var parameterVm)','string before = parameterVm.Value','string requestedValue = editor.Text',
              'if (parameterVm.IsReadOnly)','_readRequestVersion','familyAtRequest = _selectedFamilyId',
              'categoryAtRequest = _selectedSystemCategory','_pendingCellEdits[cellKey] = sequenceAtRequest',
              'bool IsRequestCurrent()','_isWindowClosed','tabAtRequest == MainTabControl.SelectedIndex',
              'readVersionAtRequest == _readRequestVersion','SystemAllItems.Contains(row)','AllItems.Contains(row)',
              'ReferenceEquals(currentVm, parameterVm)','latest == sequenceAtRequest',
              'InstanceId = row.InstanceId','ParameterKey = parameterKey','NewValue = requestedValue',
              'if (!IsRequestCurrent()) return','FamilyInstanceCellEditService.Execute(_doc, request)',
              'parameterVm.Value = before','if (!result.ReadbackConfirmed)','TaskDialog.Show("实例参数已提交", result.ReadbackWarning)',
              'parameterVm.Value = result.DisplayValue','模型修改已提交，但界面更新失败','showSuccess: false','_externalEvent.Raise()']:
    ck('UI: '+token,token in body)
ck('both grids reuse handler','InstanceDataGrid_CellEditEnding(sender, e);' in ui.split('private void SystemDataGrid_CellEditEnding(',1)[1].split('private void ResetColumns_Click',1)[0])
ck('UI no direct transaction','new Transaction(' not in body)
ck('UI service before VM update',body.index('FamilyInstanceCellEditService.Execute')<body.index('parameterVm.Value = result.DisplayValue'))
ck('UI only updates from service result',body.count('parameterVm.Value = result.DisplayValue')==1)
ck('write handler document-bound','new RevitEventHandler(_doc, wrapInTransactionGroup: false, showAutomaticSuccess: false)' in ui)
ck('Revit handler queue preserved','_queue.TryDequeue' in handler and '_expectedDocument' in handler)
ck('Explicit value binding', 'UpdateSourceTrigger = UpdateSourceTrigger.Explicit' in ui)
ck('single write route both tabs','CellEditEnding="InstanceDataGrid_CellEditEnding"' in xaml and 'CellEditEnding="SystemDataGrid_CellEditEnding"' in xaml)
ck('VM notifies changes','OnPropertyChanged(nameof(Value))' in model)
ck('XAML well-formed',ET.fromstring(xaml) is not None)
ck('XAML 43',len(list((root/'src/YangTools.Revit').rglob('*.xaml')))==43)
ck('MCP retained',(root/'McpServer/server.py').is_file())
ck('Rebar retained',(root/'src/YangTools.Revit/Core/Rebar').is_dir())
ck('original 48 test suites preserved',len(list((root/'tools').glob('Test-*.py')))>=49)
failed=[name for name,passed in checks if not passed]
print(json.dumps({'suite':'RC2J FamilyInstance cell edit UI/transaction static contracts','total':len(checks),'passed':len(checks)-len(failed),'failed':failed,'scope':'SOURCE STATIC ONLY; NOT Windows build, Revit edit or Undo'},ensure_ascii=False,indent=2))
sys.exit(bool(failed))
