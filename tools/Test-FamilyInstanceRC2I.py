#!/usr/bin/env python3
"""RC2I: read-queue freshness and batch type candidate service source contracts.
Static checks only; cannot establish compiled/Revit WPF runtime correctness.
"""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
ui = (root/'src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml.cs').read_text(encoding='utf-8-sig')
xaml_path=root/'src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml'
xaml=xaml_path.read_text(encoding='utf-8-sig')
service_path=root/'src/YangTools.Revit/Core/FamilyInstances/FamilyInstanceTypeOptionsService.cs'
service=service_path.read_text(encoding='utf-8-sig')
readhandler=(root/'src/YangTools.Revit/Core/RevitReadEventHandler.cs').read_text(encoding='utf-8-sig')
checks=[]
def test(name, ok):
    checks.append((name,bool(ok)))
def body(method, following):
    return ui[ui.index(method):ui.index(following, ui.index(method))]

for label, fragment in [
 ('request type','sealed class FamilyInstanceTypeOptionsRequest'),
 ('result type','sealed class FamilyInstanceTypeOptionsResult'),
 ('option type','sealed class FamilyInstanceTypeOption'),
 ('typed read','FamilyInstanceTypeOptionsResult Read('),
 ('doc snapshot','public Document Document { get; }'),
 ('mode snapshot','public bool IsSystemTab { get; }'),
 ('family id snapshot','public ElementId FamilyId { get; }'),
 ('category snapshot','public BuiltInCategory SystemCategory { get; }'),
 ('family symbols','family.GetFamilySymbolIds()'),
 ('system category collector','.OfCategory(request.SystemCategory)'),
 ('system types','.WhereElementIsElementType()'),
 ('sort','StringComparer.CurrentCultureIgnoreCase'),
 ('invalid system category guard','request.SystemCategory != BuiltInCategory.INVALID'),
 ('invalid family id guard','request.FamilyId != ElementId.InvalidElementId'),
 ('closed document guard','!doc.IsValidObject'),
 ('family cast','doc.GetElement(request.FamilyId) as Family'),
 ('symbol type cast','doc.GetElement(id) as FamilySymbol'),
 ('no transaction in service','return new FamilyInstanceTypeOptionsResult('),
]: test(label,fragment in service)
for forbidden in ['System.Windows','TaskDialog','Transaction(', 'SubTransaction(', 'ExternalEvent', 'Dispatcher.', 'ViewModel', 'ActiveUIDocument']:
    # Comments mention ExternalEvent and WPF/ViewModel, skip // lines for dependency check.
    bare='\n'.join(z for z in service.splitlines() if not z.strip().startswith('//'))
    test('no service dependency: '+forbidden, forbidden not in bare)
refresh=body('private void RefreshBatchOptions()', 'private List<InstanceItemViewModel> GetSelectedRows()')
for label, fragment in [
 ('service invocation','FamilyInstanceTypeOptionsService.Read(request)'),
 ('mode snapshot','MainTabControl.SelectedIndex == 1'),
 ('id snapshot','_selectedFamilyId'),
 ('category snapshot','_selectedSystemCategory'),
 ('view projection','new TypeTargetItem(x.TypeId, x.DisplayName)'),
 ('failure clears stale options','new List<TypeTargetItem>()'),
 ('reset selection','TargetTypeComboBox.SelectedIndex = -1;'),
 ('closed guard','_isWindowClosed'),
 ('status refresh','UpdateStatusText();'),
]:test(label,fragment in refresh)
for forbidden in ['new FilteredElementCollector(','.GetFamilySymbolIds()', 'new Transaction(']:
    test('no old query in WPF refresh: '+forbidden,forbidden not in refresh)
request=body('private void RequestRead(Action action, string operationName)','private void RestoreAfterSelection()')
for label, frag in [
 ('request callback checks closed','if (_isWindowClosed || requestVersion != _readRequestVersion) return;'),
 ('request captures generation','long requestVersion = ++_readRequestVersion;'),
 ('request queue uses read handler','_readHandler.SetAction'),
 ('request raise event','_readExternalEvent.Raise();'),
 ('request guard disposed read handler','_readExternalEvent == null'),
 ('request guard disposed window','_isWindowClosed'),
]:test(label,frag in request)
closed=body('protected override void OnClosed(EventArgs e)','private void TitleBar_MouseLeftButtonDown')
test('closed marks dead before disposal',closed.index('_isWindowClosed = true;') < closed.index('_readExternalEvent?.Dispose();'))
test('closed invalidates reads', 'InvalidateReadRequests();' in closed)
test('closed preserves read and edit cleanup','_externalEvent?.Dispose();' in closed and '_readExternalEvent?.Dispose();' in closed)
tab=body('private void MainTabControl_SelectionChanged','private void FamilyFilterChanged')
test('tab switch invalidates even when no selection','InvalidateReadRequests();' in tab and tab.index('InvalidateReadRequests();') < tab.index('if (MainTabControl.SelectedIndex == 1)'))
family=body('private void FamilyFilterChanged', 'private void SystemCategoryChanged')
system=body('private void SystemCategoryChanged','private void LoadConfig()')
test('no family invalidates',re.search(r'if\s*\(selected == null\)\s*\{\s*InvalidateReadRequests\(\);',family) is not None)
test('no system category invalidates',re.search(r'if\s*\(selected == null\)\s*\{\s*InvalidateReadRequests\(\);',system) is not None)
for frag in ['_expectedDocument.IsValidObject','activeDocument.Equals(_expectedDocument)','_queue.TryDequeue']:
    test('read handler preserves document gate '+frag,frag in readhandler)
# Explicit compatibility guard: original functional UI events, tabs and labels are still present.
for fragment in ['MainTabControl_SelectionChanged','FamilyFilterChanged','SystemCategoryChanged','Refresh_Click','BatchChangeType_Click','BatchSetParameter_Click','ImportExcel_Click','Delete_Click','SystemDataGrid_CellEditEnding','InstanceDataGrid_CellEditEnding']:
    test('old handler intact '+fragment,fragment in ui and fragment in xaml)
for fragment in ['x:Name="TargetTypeComboBox"','x:Name="FamilyFilterComboBox"','x:Name="SystemCategoryComboBox"','x:Name="MainTabControl"','x:Name="InstanceDataGrid"','x:Name="SystemDataGrid"']:
    test('UI control retained '+fragment,fragment in xaml)
try:
    ET.fromstring(xaml)
    test('XAML XML parses',True)
except ET.ParseError:
    test('XAML XML parses',False)
test('service included by default SDK project', '<Project Sdk="Microsoft.NET.Sdk"' in (root/'src/YangTools.Revit/YangTools.Revit.csproj').read_text(encoding='utf-8-sig'))
failed=[name for name,ok in checks if not ok]
print(f'FamilyInstance RC2I static contracts {len(checks)-len(failed)}/{len(checks)} PASS' if not failed else f'{len(failed)} FAILURE')
for name in failed: print('FAIL',name)
raise SystemExit(bool(failed))
