#!/usr/bin/env python3
"""RC2L batch type service/transaction/WPF source contracts. Static only, NOT a Revit GUI test."""
from pathlib import Path
from xml.etree import ElementTree as ET
import json, re, sys
r=Path(__file__).resolve().parents[1]
s=(r/'src/YangTools.Revit/Core/FamilyInstances/FamilyInstanceBatchTypeService.cs').read_text('utf-8')
u=(r/'src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml.cs').read_text('utf-8-sig')
x=(r/'src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml').read_text('utf-8-sig')
h=(r/'src/YangTools.Revit/Core/RevitEventHandler.cs').read_text('utf-8-sig')
checks=[]
def ck(k,v):checks.append((k,bool(v)))
for name in ['FamilyInstanceBatchTypeRequest','FamilyInstanceBatchTypeResult','FamilyInstanceTypeReplacement','FamilyInstanceBatchTypeService','IReadOnlyList<ElementId> InstanceIds','ElementId TargetTypeId','bool Committed','int ChangedCount','int SkippedCount','bool ReadbackConfirmed','string ReadbackWarning','IReadOnlyList<FamilyInstanceTypeReplacement> Replacements','ElementId OriginalId','ElementId EffectiveId']:
    ck('typed '+name,name in s)
for name in ['document == null || !document.IsValidObject','document.IsReadOnly','request == null','request.InstanceIds == null','request.InstanceIds.Count == 0','request.TargetTypeId == null','ElementType targetType = document.GetElement(request.TargetTypeId) as ElementType','!targetType.IsValidObject','new HashSet<long>()','!seen.Add(idValue)','document.GetElement(id)','!element.IsValidObject','element.GetTypeId()','InvalidElementId','skipped++','candidates.Add(id)','candidates.Count == 0','new Transaction(document, "批量更换实例类型")','transaction.Start() != TransactionStatus.Started','foreach (ElementId originalId in candidates)','element.ChangeTypeId(request.TargetTypeId)','replacementId != ElementId.InvalidElementId','? replacementId : originalId','document.GetElement(effectiveId)','effective.GetTypeId().GetIdValue() != targetId','OriginalId = originalId, EffectiveId = effectiveId','transaction.Commit() != TransactionStatus.Committed','transaction.GetStatus() == TransactionStatus.Started','transaction.RollBack() != TransactionStatus.RolledBack','回滚无法确认','事务结果无法确认','ReadbackConfirmed = false','result.ReadbackWarning','Replacements = replacements.AsReadOnly()','Committed = true','ChangedCount = replacements.Count','Replacements = replacements.AsReadOnly()']:
    ck('service '+name,name in s)
ck('preflight complete before transaction',s.index('foreach (ElementId id in request.InstanceIds)')<s.index('new Transaction('))
ck('zero-change before transaction',s.index('if (candidates.Count == 0)')<s.index('new Transaction('))
ck('one Revit transaction',s.count('new Transaction(')==1)
ck('no partial subtransactions', 'new SubTransaction(' not in s)
ck('no transaction group','new TransactionGroup(' not in s)
ck('single type change per candidate',s.count('element.ChangeTypeId(')==1)
ck('commit before readback',s.index('transaction.Commit()')<s.index('// The transaction is committed here'))
ck('replaced ID preserved',s.index('document.GetElement(effectiveId)')<s.index('transaction.Commit()'))
ck('readback effective new id',s.index('document.GetElement(replacement.EffectiveId)')>s.index('transaction.Commit()'))
ck('no UI types', not any(t in re.sub(r'(?m)^\s*//.*$','',s) for t in ['using System.Windows','Dispatcher.','TaskDialog.','DataGrid','InstanceItemViewModel','UIApplication','ActiveUIDocument']))
b=u[u.index('private void BatchChangeType_Click'):u.index('private void BatchSetParameter_Click')]
for name in ['GetSelectedRows()','TargetTypeComboBox.SelectedItem as TypeTargetItem','new FamilyInstanceBatchTypeRequest','InstanceIds = rows.Select(x => x.InstanceId).ToArray()','TargetTypeId = target.TypeId','int tabAtRequest','long readVersionAtRequest','++_batchTypeSequence','bool IsCurrentRequest()','_isWindowClosed','tabAtRequest != MainTabControl.SelectedIndex','readVersionAtRequest != _readRequestVersion','batchVersionAtRequest != _batchTypeSequence','ReferenceEquals(TargetTypeComboBox.SelectedItem, target)','familyIdAtRequest','categoryAtRequest','scopeAtRequest','typeFilterAtRequest','levelFilterAtRequest','activeRows.Contains(row)','grid.SelectedItems.Contains(row)','_handler.SetAction(app =>','if (!IsCurrentRequest())','SKIPPED_STALE_UI','FamilyInstanceBatchTypeService.Execute(_doc, request)','result.Committed ? "COMMITTED" : "NO_CHANGE"','Replacements=','OriginalId.GetIdValue()','EffectiveId.GetIdValue()','!result.ReadbackConfirmed','result.ReadbackWarning','批量换型已提交','RequestRead(ReloadCurrentTab','批量换型界面刷新失败','showSuccess: false','_externalEvent.Raise()']:
    ck('WPF '+name,name in b)
ck('UI not writing transactions', all(t not in b for t in ['new Transaction(', 'new SubTransaction(', 'ChangeTypeId(']))
ck('freshness before service execution',b.index('if (!IsCurrentRequest())')<b.index('FamilyInstanceBatchTypeService.Execute'))
ck('service before UI refresh',b.index('FamilyInstanceBatchTypeService.Execute')<b.index('RequestRead(ReloadCurrentTab'))
ck('service executes once',b.count('FamilyInstanceBatchTypeService.Execute')==1)
ck('UI no failure partial summary','failures.Add(' not in b)
ck('type generation field','private long _batchTypeSequence;' in u)
ck('Revit write handler bound doc','new RevitEventHandler(_doc, wrapInTransactionGroup: false, showAutomaticSuccess: false)' in u)
ck('write handler document guard','_expectedDocument.IsValidObject' in h and '_queue.TryDequeue' in h)
ck('old batch write still separate','FamilyInstanceBatchParameterService.Execute(_doc, request)' in u)
ck('XAML operation unchanged','Click="BatchChangeType_Click"' in x and 'x:Name="TargetTypeComboBox"' in x)
ck('XAML parses',ET.fromstring(x) is not None)
ck('43 XAML',len(list((r/'src/YangTools.Revit').rglob('*.xaml')))==43)
ck('old 50 suites remain',len(list((r/'tools').glob('Test-*.py')))>=51)
ck('MCP retained',(r/'McpServer/server.py').exists())
ck('Rebar retained',(r/'src/YangTools.Revit/Core/Rebar').is_dir())
fail=[k for k,v in checks if not v]
print(json.dumps({'name':'RC2L FamilyInstance BatchType atomic/UI source contract','total':len(checks),'passed':len(checks)-len(fail),'failed':fail,'runtime':'NOT_TESTED'},indent=2,ensure_ascii=False))
sys.exit(bool(fail))
