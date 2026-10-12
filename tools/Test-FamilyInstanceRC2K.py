#!/usr/bin/env python3
"""RC2K family instance batch parameter atomicity/WPF source contract. NOT a Revit test."""
from pathlib import Path
from xml.etree import ElementTree as ET
import json, re, sys
r=Path(__file__).resolve().parents[1]
s=(r/'src/YangTools.Revit/Core/FamilyInstances/FamilyInstanceBatchParameterService.cs').read_text('utf-8')
u=(r/'src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml.cs').read_text('utf-8-sig')
x=(r/'src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml').read_text('utf-8-sig')
handler=(r/'src/YangTools.Revit/Core/RevitEventHandler.cs').read_text('utf-8-sig')
checks=[]
def ck(name, cond): checks.append((name, bool(cond)))
for token in ['FamilyInstanceBatchParameterRequest','FamilyInstanceBatchParameterResult','FamilyInstanceBatchParameterService','IReadOnlyList<ElementId> InstanceIds','string ParameterKey','string NewValue','bool OnlyEmpty','bool Committed','int ChangedCount','int SkippedCount','bool ReadbackConfirmed','string ReadbackWarning']:
    ck('typed: '+token,token in s)
for token in ['document == null || !document.IsValidObject','document.IsReadOnly','request == null','request.InstanceIds == null','request.InstanceIds.Count == 0','string.IsNullOrWhiteSpace(request.ParameterKey)','HashSet<long>','!seen.Add(idValue)','InvalidElementId.GetIdValue()', 'document.GetElement(id)','!element.IsValidObject','StableParameterKey.Resolve(element, request.ParameterKey)','!ParameterValueHelper.CanEditAsText(parameter)', 'request.OnlyEmpty && !IsParameterEmpty(parameter)','ParameterValueHelper.GetDisplayValue(parameter)','StringComparison.Ordinal','candidates.Add((id, parameter, ParameterValueHelper.GetDisplayValue(parameter)))','candidates.Count == 0','TransactionStatus.Started','TransactionStatus.Committed','TransactionStatus.RolledBack','ParameterValueHelper.TrySetFromDisplayValue(candidate.parameter, value, out string setError)','candidate.id.GetIdValue()','transaction.GetStatus() == TransactionStatus.Started','transaction.RollBack()','回滚无法确认','事务结果无法确认','ReadbackConfirmed = false','string after = ParameterValueHelper.GetDisplayValue(updated)','candidate.before','回读无法确认','Committed = true']:
    ck('service: '+token,token in s)
ck('exactly one transaction',s.count('new Transaction(')==1)
ck('no subtransaction', 'new SubTransaction(' not in s)
ck('no transaction group', 'new TransactionGroup(' not in s)
ck('zero-change before transaction',s.index('if (candidates.Count == 0)')<s.index('new Transaction('))
ck('preflight before transaction',s.index('foreach (ElementId id in request.InstanceIds)')<s.index('new Transaction('))
ck('batch failure raises', 'throw new InvalidOperationException($"实例 {candidate.id.GetIdValue()} 写入失败' in s)
ck('readback after commit',s.index('// Transaction has committed.')>s.index('transaction.Commit()'))
ck('no UI dependency',not any(term in re.sub(r'(?m)^\s*//.*$','',s) for term in ['using System.Windows','Dispatcher.','TaskDialog.','TextBox','DataGrid','InstanceItemViewModel','UIApplication','ActiveUIDocument']))
ck('is parameter empty old semantics','!parameter.HasValue' in s and 'StorageType.String' in s and 'AsString()' in s)
b=u[u.index('private void BatchSetParameter_Click'):u.index('private void SystemSearchChanged')]
for token in ['GetSelectedRows()','BatchParameterComboBox.SelectedItem as StableParameterDescriptor','BatchParameterValueBox.Text','OnlyEmptyCheckBox.IsChecked == true','new FamilyInstanceBatchParameterRequest','InstanceIds = rows.Select(x => x.InstanceId).ToArray()','ParameterKey = descriptor.Key','NewValue = newValue','OnlyEmpty = onlyEmpty','int tabAtRequest','long readVersionAtRequest','++_batchParameterSequence','bool IsCurrentRequest()','_isWindowClosed','tabAtRequest != MainTabControl.SelectedIndex','readVersionAtRequest != _readRequestVersion','batchVersionAtRequest != _batchParameterSequence','grid.SelectedItems.Contains(row)','SystemAllItems : AllItems','_handler.SetAction(app =>','if (!IsCurrentRequest())','SKIPPED_STALE_UI','FamilyInstanceBatchParameterService.Execute(_doc, request)','result.Committed ? "COMMITTED" : "NO_CHANGE"','ReadbackConfirmed=','!result.ReadbackConfirmed','result.ReadbackWarning','批量参数已提交','RequestRead(ReloadCurrentTab','批量参数界面刷新失败','showSuccess: false','_externalEvent.Raise()']:
    ck('UI: '+token,token in b)
ck('UI no direct transactions','new Transaction(' not in b and 'new SubTransaction(' not in b)
ck('UI service executes after freshness check',b.index('if (!IsCurrentRequest())')<b.index('FamilyInstanceBatchParameterService.Execute'))
ck('UI refresh after model operation',b.index('FamilyInstanceBatchParameterService.Execute')<b.index('RequestRead(ReloadCurrentTab'))
ck('UI write route only one callback',b.count('FamilyInstanceBatchParameterService.Execute')==1)
ck('UI no partial-success catch loop', 'failures.Add(' not in b)
ck('request generation field','private long _batchParameterSequence;' in u)
ck('handler doc bound','new RevitEventHandler(_doc, wrapInTransactionGroup: false, showAutomaticSuccess: false)' in u)
ck('handler protects document','_expectedDocument.IsValidObject' in handler and '_queue.TryDequeue' in handler)
ck('batch type routed to service after RC2L','FamilyInstanceBatchTypeService.Execute(_doc, request)' in u and 'new SubTransaction(' not in u[u.index('private void BatchChangeType_Click'):u.index('private void BatchSetParameter_Click')])
ck('XAML batch UI intact','Click="BatchSetParameter_Click"' in x and 'x:Name="OnlyEmptyCheckBox"' in x and 'x:Name="BatchParameterValueBox"' in x)
ck('XAML parses',ET.fromstring(x) is not None)
ck('43 XAML',len(list((r/'src/YangTools.Revit').rglob('*.xaml')))==43)
ck('MCP exists',(r/'McpServer/server.py').exists())
ck('Rebar core exists',(r/'src/YangTools.Revit/Core/Rebar').is_dir())
ck('original 49 suites still present',len(list((r/'tools').glob('Test-*.py')))>=50)
fail=[k for k,v in checks if not v]
print(json.dumps({'name':'RC2K FamilyInstance BatchParameter atomic/static UI','total':len(checks),'passed':len(checks)-len(fail),'failed':fail,'runtime':'NOT_TESTED'},indent=2,ensure_ascii=False))
sys.exit(bool(fail))
