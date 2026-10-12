#!/usr/bin/env python3
"""RC2A: static contracts for ProjectAssetManager WPF and read-only FilterUsage service.

This catches missing XAML handlers, broken tab/data-grid wiring and source-boundary
regressions. It is not a Windows compilation or Revit GUI/runtime test.
"""
from __future__ import annotations
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
UI = ROOT / 'src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml.cs'
XAML = ROOT / 'src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml'
CORE = ROOT / 'src/YangTools.Revit/Core/ProjectAssets/ProjectAssetFilterUsageService.cs'
VM = ROOT / 'src/YangTools.Revit/Models/ProjectAssetManagerViewModels.cs'

checks: list[tuple[str, bool]] = []
def ok(label: str, passed: bool) -> None:
    checks.append((label, bool(passed)))
    if not passed:
        print('FAIL:', label)

ui = UI.read_text(encoding='utf-8-sig')
xaml = XAML.read_text(encoding='utf-8-sig')
service = CORE.read_text(encoding='utf-8-sig')
vm = VM.read_text(encoding='utf-8-sig')
root = ET.fromstring(xaml)
all_nodes = list(root.iter())
xns = '{http://schemas.microsoft.com/winfx/2006/xaml}'

ok('valid XAML XML', root.tag.endswith('Window'))
ok('WPF class maps to code-behind', root.attrib.get(xns + 'Class') == 'YangTools.Revit.UI.ProjectAssetManagerWindow')
ok('WPF UI has exactly five tabs', len([n for n in all_nodes if n.tag.endswith('TabItem')]) == 5)
for tab in ('Views', 'Filters', 'FilledRegions', 'LineStyles', 'System'):
    ok('tab '+tab, len([n for n in all_nodes if n.tag.endswith('TabItem') and n.attrib.get('Tag') == tab]) == 1)

names = [n.attrib.get(xns+'Name') for n in all_nodes if n.attrib.get(xns+'Name')]
ok('no duplicate x:Name in UI', len([n for n in names if n != 'bg']) == len(set(n for n in names if n != 'bg')))
for name in ('MainTabControl', 'AssetTreeView', 'AssetDataGrid', 'FilledRegionDataGrid',
             'LineStyleDataGrid', 'DefaultActionPanel', 'DuplicateActionPanel',
             'AnalyzeFilterBtn', 'ImportBtn', 'ExportBtn', 'SearchBox', 'StatusText'):
    ok('named control '+name, name in names)

callbacks = {'Click','SelectionChanged','SelectedItemChanged','CellEditEnding',
             'DropDownClosed','PreviewMouseWheel','TextChanged','MouseLeftButtonDown'}
handlers = [(n.tag.split('}')[-1],k,v) for n in all_nodes for k,v in n.attrib.items() if k in callbacks]
ok('registered UI event handlers', len(handlers) >= 15)
for tag,evt,handler in handlers:
    ok('event handler exists '+handler, re.search(r'\b'+re.escape(handler)+r'\s*\(',ui) is not None)

ok('read queued through ExternalEvent', 'new RevitReadEventHandler(_doc)' in ui and '_readExternalEvent.Raise()' in ui)
ok('closed window disallows queued UI work', 'if (_isClosed || _readHandler == null' in ui and '_isClosed = true;' in ui)
ok('tab requests versioned', 'var requestVersion = ++_tabLoadVersion;' in ui)
ok('stale tab read guarded', 'requestVersion != _tabLoadVersion || _currentTabName != requestedTab' in ui)
ok('node requests guarded', '_selectedNode != requestedNode || _currentTabName != requestedTab' in ui)
ok('columns created before node rows', 'CreateDynamicColumns();\n                    LoadData();' in ui)
ok('columns created before tab rows', 'LoadConfig();\n                    CreateDynamicColumns();\n                    LoadData();' in ui)
ok('loaded-count text not overwritten by ready', 'StatusText.Text = "就绪";' not in ui)
ok('count preserved on asset data', 'StatusText.Text = $"加载了 {AssetList.Count} 个对象"' in ui)
ok('project info read-only guard retained', 'IsProjectInfoParameterView()' in ui)
ok('Excel export/import event handlers retained', 'ExportExcel_Click' in ui and 'ImportExcel_Click' in ui)
ok('pattern dropdown commit retained', 'FillPattern_DropDownClosed' in ui and 'LinePattern_DropDownClosed' in ui)
ok('mouse wheel pattern guard retained', 'PatternComboBox_PreviewMouseWheel' in ui)
ok('filter columns retained', all(n in ui for n in ('是否控制实例元素','是否在视图中使用','在视图样板中是否使用')))
ok('filter usage Service called by GUI twice', ui.count('ProjectAssetFilterUsageService.Execute(') == 2)
ok('no raw view cache in UI', '_cachedViews' not in ui)
ok('no UI filter usage implementations', all(x not in ui for x in ('CheckFilterControlsElements(', 'CheckFilterUsedInViews(', 'CheckFilterUsedInTemplates(')))
ok('service request class', 'class ProjectAssetFilterUsageRequest' in service)
ok('service result class', 'class ProjectAssetFilterUsageResult' in service)
ok('service result DTO', 'class ProjectAssetFilterUsageItem' in service)
ok('service is read-only', all(x not in service for x in ('new Transaction(', 'TransactionGroup(', 'StartTransaction(', '.Delete(')))
ok('service no WPF dependency', 'System.Windows' not in service and 'TaskDialog' not in service)
ok('service guards closed document', '!document.IsValidObject' in service)
ok('service gets filtered views', 'new FilteredElementCollector(document)' in service and '.OfClass(typeof(View))' in service)
ok('service separates template usage', 'view.IsTemplate ? usedInTemplates : usedInViews' in service)
ok('service reuses per-query index', 'var usedInViews = new HashSet<long>()' in service)
ok('service deduplicates FilterIds', '.GroupBy(id => id.GetIdValue())' in service)
ok('service preserves category/element match', 'ElementMulticategoryFilter(categoryIds)' in service and 'WherePasses(elementFilter)' in service)
ok('service preserves true null filter semantics', 'if (elementFilter != null)' in service)
ok('service fallback for deleted filters', 'if (filter == null) continue;' in service)
ok('model still implements INotifyPropertyChanged', 'class AssetItemViewModel : INotifyPropertyChanged' in vm)
ok('filter source ID uses long for 2024+', '.GetIdValue()' in service)

report={'suite':'ProjectAssetRC2A static UI/service contract','total':len(checks), 'passed':sum(v for _,v in checks),'failed':[k for k,v in checks if not v], 'limitations':'No Windows C# compile or Revit UI runtime.'}
print(json.dumps(report,ensure_ascii=False,indent=2))
sys.exit(0 if not report['failed'] else 1)
