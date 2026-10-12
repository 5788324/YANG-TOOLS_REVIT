#!/usr/bin/env python3
"""RC2E WPF cell edit + single-transaction source contracts; NOT Revit runtime."""
import json,re,sys,xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[1]
u=(root/'src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml.cs').read_text(encoding='utf-8-sig')
s=(root/'src/YangTools.Revit/Core/ProjectAssets/ProjectAssetCellEditService.cs').read_text(encoding='utf-8-sig')
x=(root/'src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml').read_text(encoding='utf-8-sig')
checks=[]
def ck(name,p):checks.append((name,bool(p)))
ui=u[u.index('private void AssetDataGrid_CellEditEnding('):u.index('private void AnalyzeFilter_Click(')]
for n in ['ProjectAssetCellEditService','ProjectAssetCellEditRequest','ProjectAssetCellEditResult','ProjectAssetCellEditKind']:
 ck('typed '+n, n in s)
for n in ['AssetName','ElementParameter','ProjectInformationParameter']:
 ck('mode '+n,n in s and n in ui)
for n in ['ElementId','ParameterId','ParameterName','NewValue']:
 ck('request prop '+n,re.search(r'public\s+\w+\s+'+n+r'\s*\{',s))
ck('No WPF dependency',not any(t in s for t in ['System.Windows','Dispatcher','TaskDialog','MessageBox']))
ck('No GUI state in Service',not any(t in s for t in ['AssetList','AssetDataGrid','StatusText']))
ck('Document-valid preflight','!document.IsValidObject' in s)
ck('Read-only document preflight','document.IsReadOnly' in s)
ck('Asset exists preflight','element == null || !element.IsValidObject' in s)
ck('ProjectInfo ID matches parameter, not element','p.Id.GetIdValue() == request.ParameterId.GetIdValue()' in s)
ck('Parameter identity name matches','p.Definition?.Name == request.ParameterName' in s)
ck('No ProjectInfo LookupParameter ambiguity','info.LookupParameter' not in s)
ck('Element param fallback','element.GetOrderedParameters()' in s)
ck('No silent missing parameter','没有参数' in s)
ck('No silent readonly','!ParameterValueHelper.CanEditAsText(parameter)' in s)
ck('No silent empty name','string.IsNullOrWhiteSpace(value)' in s)
ck('No transaction on no-op','if (string.Equals(result.DisplayValue, value, StringComparison.Ordinal))' in s and 'return result; // no transaction' in s)
ck('One transaction',s.count('new Transaction(')==1 and 'new TransactionGroup(' not in s)
ck('Check Start','transaction.Start() != TransactionStatus.Started' in s)
ck('Check Commit','transaction.Commit() != TransactionStatus.Committed' in s)
ck('Check Rollback','transaction.RollBack() != TransactionStatus.RolledBack' in s)
ck('Uncertain rollback reported','回滚结果无法确认' in s)
ck('Change reported after commit',s.index('result.Changed = true;') > s.index('transaction.Commit()'))
ck('Service used by Revit ExternalEvent','_handler.SetAction(app =>' in ui and 'ProjectAssetCellEditService.Execute(_doc, request)' in ui)
ck('Uses originating _doc, not active doc','app.ActiveUIDocument.Document' not in ui)
ck('UI uses captured text','NewValue = newValue' in ui)
ck('ProjectInfo uses ParameterId','ParameterId = vm.AssetId' in ui)
ck('One cell edit UI handler',u.count('private void AssetDataGrid_CellEditEnding(')==1)
ck('Close guard','_isClosed' in ui)
ck('Tab/version guard','_tabLoadVersion' in ui and '_currentTabName' in ui)
ck('Node identity guard','ReferenceEquals(_selectedNode, nodeAtRequest)' in ui)
ck('Existing grid row identity guard','AssetList.Contains(itemAtRequest)' in ui)
ck('No success ahead of Service',ui.index('ProjectAssetCellEditService.Execute(_doc, request)') < ui.index('applyValue(result.DisplayValue)'))
ck('Stale callback skips before writing',ui.index('if (_isClosed || versionAtRequest != _tabLoadVersion') < ui.index('ProjectAssetCellEditService.Execute(_doc, request)'))
ck('Distinguish committed UI refresh failure','资产编辑已提交，但表格刷新失败' in ui)
ck('No swallowed exceptions in event','Debug.WriteLine' not in ui)
ck('No direct transactions in event','new Transaction(' not in ui)
ck('No duplicate warning for success', 'showSuccess: false' in ui)
ck('No Cancel edit trap','e.Cancel = true' not in ui)
ck('Grid AssetName Explicit','AssetName, UpdateSourceTrigger=Explicit' in x)
ck('ProjectInfo parameter binding Explicit','Binding = new System.Windows.Data.Binding($"Parameters[{pName}].Value") { UpdateSourceTrigger = UpdateSourceTrigger.Explicit }' in u)
ck('Dynamic parameter binding Explicit','col.Binding = new System.Windows.Data.Binding($"Parameters[{pName}].Value") { UpdateSourceTrigger = UpdateSourceTrigger.Explicit }' in u)
ck('Dynamic parameter read only editor','IsReadOnlyProperty' in u)
ck('ProjectInfo read only UI feedback','TaskDialog.Show("不可编辑"' in ui)
ck('XAML event matches WPF method', 'CellEditEnding="AssetDataGrid_CellEditEnding"' in x)
ck('XAML structure',ET.fromstring(x).tag.endswith('Window'))
ck('43 UI XAML files still present',len(list((root/'src/YangTools.Revit').rglob('*.xaml')))==43)
try:
 for p in (root/'src/YangTools.Revit').rglob('*.xaml'):ET.parse(p)
 ck('43 XAML parse',True)
except ET.ParseError:
 ck('43 XAML parse',False)
ck('Rebar untouched in client contract','ProjectAsset' not in (root/'src/YangTools.Revit/Mcp/RevitExecutionBroker.cs').read_text(encoding='utf-8-sig'))
failed=[n for n,v in checks if not v]
print(json.dumps({'suite':'RC2E ProjectAsset single-cell edit/UI static contracts','total':len(checks),'passed':len(checks)-len(failed),'failed':failed,'limitations':'Source only; no Windows build or Revit UI/Undo evidence'},ensure_ascii=False,indent=2))
sys.exit(1 if failed else 0)
