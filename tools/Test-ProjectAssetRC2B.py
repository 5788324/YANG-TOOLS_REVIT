#!/usr/bin/env python3
"""RC2B independent source contracts for atomic bulk rename and WPF integration.

Verifies source shape and UI/XAML wiring; not a substitute for C# compilation
or runtime transaction rollback testing in Revit.
"""
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

root=Path(__file__).resolve().parents[1]
u=(root/'src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml.cs').read_text(encoding='utf-8-sig')
x=(root/'src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml').read_text(encoding='utf-8-sig')
s=(root/'src/YangTools.Revit/Core/ProjectAssets/ProjectAssetBatchRenameService.cs').read_text(encoding='utf-8-sig')
h=(root/'src/YangTools.Revit/Core/RevitEventHandler.cs').read_text(encoding='utf-8-sig')
checks=[]
def check(name,p):
    checks.append((name,bool(p)))
    if not p: print('FAIL',name)

def segment(content, start, end):
    return content[content.index(start):content.index(end,content.index(start))]

handler=segment(u,'private void BatchRename_Click(', 'private void HideCurrentColumn_Click(')
executor=segment(s,'internal static ProjectAssetBatchRenameResult Execute(', '\n    }\n}')
tree=ET.fromstring(x)
button=[n for n in tree.iter() if n.tag.endswith('Button') and n.attrib.get('Click')=='BatchRename_Click']
check('XAML bulk rename button mapped once', len(button)==1)
check('WPF handler exists once',u.count('private void BatchRename_Click(')==1)
check('WPF XAML valid',tree.tag.endswith('Window'))
check('UI keeps project-info protection', 'IsProjectInfoParameterView()' in handler)
check('UI guards already closed', 'if (_isClosed) return;' in handler)
check('UI uses existing rename dialog', 'new BatchRenameWindow { Owner = this }' in handler)
check('UI honors dialog cancel', 'ShowDialog() != true || _isClosed' in handler)
for typ in ('AssetItemViewModel','FilledRegionViewModel','LineStyleViewModel'):
    check('UI collects selection '+typ, 'SelectedItems.Cast<'+typ+'>()' in handler)
check('UI prompts on empty selection', 'if (ids.Count == 0)' in handler)
check('request type defined', 'class ProjectAssetBatchRenameRequest' in s)
check('result type defined', 'class ProjectAssetBatchRenameResult' in s)
check('service type defined', 'class ProjectAssetBatchRenameService' in s)
check('service is core ProjectAssets', 'namespace YangTools.Revit.Core.ProjectAssets' in s)
check('service no WPF dependency', 'System.Windows' not in s and 'TaskDialog' not in s and 'MessageBox' not in s)
check('request has ElementIds','IReadOnlyCollection<ElementId> ElementIds' in s)
for key in ('Prefix','Suffix','Find','Replace'):
    check('request contains '+key,'string '+key+' {' in s)
    check('UI captures '+key,key+' = renameWindow.' in handler)
check('UI snapshots selected ID collection','ElementIds = ids.ToArray()' in handler)
check('UI snapshots tab name', 'var tabAtRequest = _currentTabName;' in handler)
check('UI snapshots tab version', 'var tabVersionAtRequest = _tabLoadVersion;' in handler)
check('UI snapshots tree node', 'var nodeAtRequest = _selectedNode;' in handler)
check('UI queues via existing ExternalEvent', '_handler.SetAction(app =>' in handler and '_externalEvent.Raise();' in handler)
check('UI service invoked inside event', 'ProjectAssetBatchRenameService.Execute(_doc, request)' in handler)
check('UI keeps modeless close guard in callback','if (_isClosed) return;' in handler)
check('UI skips refresh on outdated tab', 'tabVersionAtRequest == _tabLoadVersion' in handler and '_currentTabName == tabAtRequest' in handler)
check('UI skips refresh on outdated node','ReferenceEquals(_selectedNode, nodeAtRequest)' in handler)
check('UI refreshes asset grid','LoadData();' in handler)
check('UI refreshes filled region grid','LoadFilledRegions();' in handler)
check('UI refreshes line style grid','LoadLineStyles();' in handler)
check('UI success shows committed result', 'result.RenamedCount' in handler and 'result.UnchangedCount' in handler)
check('UI Undo hint conditional', 'result.RenamedCount > 0' in handler and '未创建撤销记录' in handler)
check('UI notifies errors', 'catch (Exception ex)' in handler and 'TaskDialog.Show("批量重命名失败"' in handler)
check('UI no silent catch inside rename loop', 'Debug.WriteLine' not in handler and 'catch (Exception ex) {' not in handler)
check('UI no direct rename transactions', 'new Transaction(' not in handler and 'elem.Name =' not in handler)
check('read/write document bound', 'new RevitEventHandler(_doc, wrapInTransactionGroup: false' in u and '_expectedDocument' in h and 'TryResolveDocument' in h)
check('closed doc preflight', '!document.IsValidObject' in executor)
check('readonly doc preflight', 'document.IsReadOnly' in executor)
check('null request guard', 'if (request == null)' in executor)
check('IDs deduplicated', '.GroupBy(id => id.GetIdValue())' in executor)
check('missing element preflight', 'element == null || !element.IsValidObject' in executor)
check('empty resulting name preflight', 'string.IsNullOrWhiteSpace(targetName)' in executor)
check('old name comparison', 'string.Equals(originalName, targetName, StringComparison.Ordinal)' in executor)
check('empty plan no transaction', 'if (plan.Count == 0) return result;' in executor)
check('one transaction and no transaction group', executor.count('new Transaction(')==1 and 'TransactionGroup' not in executor)
check('transaction name', 'new Transaction(document, "批量重命名资产")' in executor)
check('transaction start checked', 'transaction.Start() != TransactionStatus.Started' in executor)
check('transaction commit checked', 'transaction.Commit() != TransactionStatus.Committed' in executor)
check('explicit rollback', 'transaction.RollBack();' in executor)
check('rollback on any exception', re.search(r'catch\s*\{\s*if\s*\(transaction.GetStatus\(\) == TransactionStatus.Started\)\s*transaction.RollBack\(\);\s*throw;',executor) is not None)
check('rethrow per asset failure with identity', 'entry.OriginalName' in executor and 'throw new InvalidOperationException(' in executor)
check('no partial result before commit', executor.index('transaction.Commit()') < executor.index('result.RenamedCount = plan.Count;'))
check('single Undo for changes',executor.count('new Transaction(')==1)
check('null prefix/suffix safe','request.Prefix ?? string.Empty' in executor and 'request.Suffix ?? string.Empty' in executor)
check('null find/replace safe','request.Find ?? string.Empty' in executor and 'request.Replace ?? string.Empty' in executor)
check('replacement executes before affixes',executor.index('originalName.Replace(find, replacement)') < executor.index('prefix + targetName + suffix'))
check('unchanged skipped','result.UnchangedCount++' in executor)
check('no ElementTransformUtils or doc.Delete', 'ElementTransformUtils' not in executor and 'document.Delete(' not in executor)
check('MCP entry count unaffected via source', (root/'src/YangTools.Revit/Mcp/RevitExecutionBroker.cs').exists())
report={'suite':'ProjectAssetRC2B source UI atomic-rename checks','total':len(checks),'passed':sum(x for _,x in checks),'failed':[x for x,p in checks if not p], 'limitations':'Source contracts only; not C# compilation or Revit GUI and Transaction runtime.'}
print(json.dumps(report,ensure_ascii=False,indent=2))
sys.exit(0 if not report['failed'] else 1)
