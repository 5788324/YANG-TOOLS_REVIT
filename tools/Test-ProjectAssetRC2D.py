#!/usr/bin/env python3
"""RC2D ProjectAsset deletion + WPF contract checks (SOURCE ONLY)."""
from pathlib import Path
import re, sys, json, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[1]
ui=(root/'src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml.cs').read_text(encoding='utf-8-sig')
xaml=(root/'src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml').read_text(encoding='utf-8-sig')
service_path=root/'src/YangTools.Revit/Core/ProjectAssets/ProjectAssetDeleteService.cs'
service=service_path.read_text(encoding='utf-8-sig')
project=(root/'src/YangTools.Revit/YangTools.Revit.csproj').read_text(encoding='utf-8-sig')
checks=[]
def ck(name,val):checks.append((name,bool(val)))
for cls in ['ProjectAssetDeleteRequest','ProjectAssetDeletePreview','ProjectAssetDeleteResult','ProjectAssetDeleteService']:
 ck('Typed '+cls, re.search(r'\bclass\s+'+cls+r'\b',service))
for prop in ['ElementIds','SelectedIds','DeletedIds','RelatedIds','DeletedCount']:
 ck('Contract '+prop, re.search(r'\b'+prop+r'\b',service))
ck('Service free of UI dependency', 'System.Windows' not in service and 'TaskDialog' not in service and 'Dispatcher' not in service)
ck('Service only accepts captured document', 'Execute(Document document, ProjectAssetDeletePreview preview)' in service and 'Preview(Document document, ProjectAssetDeleteRequest request)' in service)
ck('Valid document required', 'document == null || !document.IsValidObject' in service)
ck('Read-only documents denied','document.IsReadOnly' in service)
ck('Preflight stale IDs', 'id.GetIdValue() > 0 && document.GetElement(id) == null' in service)
ck('Nonpositive category ids allowed to trial delete', 'Some Revit line-style subcategories' in service)
ck('Invalid ElementId rejected', 'ElementId.InvalidElementId.GetIdValue()' in service)
ck('ID dedup', '.GroupBy(id => id.GetIdValue())' in service)
ck('No empty preview transaction','if (ids.Count == 0) return preview;' in service)
ck('Trial deleted refs collection','var deleted = document.Delete(ids)?.ToList()' in service)
ck('Preview rollback before return','transaction.RollBack() != TransactionStatus.RolledBack' in service)
ck('Cascade IDs measured','preview.RelatedIds = deleted.Where(' in service)
ck('Preview catches rollback failures','删除预演失败且回滚未能确认' in service)
ck('No empty confirmed delete','没有可确认删除的图元，不创建撤销记录' in service)
ck('One confirmed transaction','new Transaction(document, "批量删除资产(YANG TOOLS)")' in service)
ck('No TransactionGroup','new TransactionGroup(' not in service)
ck('Set-equality cascade guard','expected.SetEquals(actual)' in service)
ck('Commit status explicitly checked','transaction.Commit() != TransactionStatus.Committed' in service)
ck('Commit failures propagated','删除事务未能提交' in service)
ck('Rollback failures distinguished','删除失败，回滚未能确认' in service)
ck('No WPF mutation by service','AssetList' not in service and 'StatusText' not in service)
execute=ui[ui.index('private void ExecuteDelete('):ui.index('private void BatchRename_Click(')]
checked=ui[ui.index('private void DeleteCheckedNodes_Click('):ui.index('private void DeleteSelected_Click(')]
selected=ui[ui.index('private void DeleteSelected_Click('):ui.index('private void ExecuteDelete(')]
ck('DeleteChecked handler preserved', 'private void DeleteCheckedNodes_Click' in ui)
ck('DeleteSelected handler preserved', 'private void DeleteSelected_Click' in ui)
ck('Both handlers delegate to one delete path', 'ExecuteDelete(ids, "勾选的节点")' in checked and 'ExecuteDelete(ids, "高亮选中的资产")' in selected)
ck('Checked requests use valid ExternalEvent','RequestRead(() =>' in checked)
ck('Checked state frozen before read','var versionAtRequest = _tabLoadVersion;' in checked and 'versionAtRequest != _tabLoadVersion' in checked)
ck('Checked node frozen before read','nodeAtRequest = _selectedNode' in checked and '_selectedNode != nodeAtRequest' in checked)
ck('Protected project-info edit-only','IsProjectInfoParameterView()' in checked and 'IsProjectInfoParameterView()' in selected)
ck('Deletion request snapshot', 'ElementIds = ids.ToList()' in execute)
ck('Delete event queued', '_handler.SetAction(app =>' in execute and '_externalEvent.Raise();' in execute)
ck('Document anchored', 'ProjectAssetDeleteService.Preview(_doc, request)' in execute and 'ProjectAssetDeleteService.Execute(_doc, preview)' in execute)
ck('UI checks closed window before preview','if (_isClosed || tabVersionAtRequest != _tabLoadVersion' in execute)
ck('UI uses version/node/tab checks','_currentTabName != tabAtRequest || _selectedNode != nodeAtRequest' in execute)
ck('Confirmation is default No','confirmDialog.DefaultButton = TaskDialogResult.No;' in execute)
ck('Confirmation has Yes and No','TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No' in execute)
ck('Confirmation before real deletion',execute.index('confirmDialog.Show()') < execute.index('ProjectAssetDeleteService.Execute('))
ck('Includes cascade warning','extraIds.Count > 0' in execute and '连带删除' in execute)
ck('Postcommit UI refresh exception distinguished', '删除已提交，界面刷新失败' in execute)
ck('Success only after Execute',execute.index('ProjectAssetDeleteService.Execute(') < execute.index('成功删除'))
ck('No in-window destructive delete','doc.Delete(ids)' not in execute)
ck('No transaction for dialog in UI','new TransactionGroup(' not in execute and 'new Transaction(' not in execute)
ck('No modal after window closed', 'if (_isClosed) return;' in execute and 'if (!_isClosed)' in execute)
ck('Status updated to actual count','result.DeletedCount' in execute)
ck('Removed items match actual cascades','result.DeletedIds.Select' in execute and 'deletedSet.Contains' in execute)
ck('Tree cleanup recursively handles nodes','RemoveDeletedIdsFromAssetTree(child, deletedIds)' in execute)
ck('No stale tab refresh','tabVersionAtRequest != _tabLoadVersion' in execute and 'StatusText.Text' in execute)
ck('Revit Undo feedback','Ctrl+Z' in execute)
ck('Chinese UI preserved','AssetDataGrid' in xaml and 'AssetTreeView' in xaml)
ck('Standard SDK project auto-includes new C#','<Project Sdk="Microsoft.NET.Sdk">' in project and 'EnableDefaultCompileItems>false' not in project)
ck('Existing parameter edit wired to typed service', 'AssetDataGrid_CellEditEnding' in ui and 'ProjectAssetCellEditService.Execute(_doc, request)' in ui and 'TrySetFromDisplayValue' in (root/'src/YangTools.Revit/Core/ProjectAssets/ProjectAssetCellEditService.cs').read_text(encoding='utf-8-sig'))
ck('Original Excel service left','ProjectAssetExcelImportService.Execute' in ui)
ck('Existing batch rename service left','ProjectAssetBatchRenameService.Execute' in ui)
ck('UI XML well formed',True)
try: ET.fromstring(xaml)
except Exception as e:
 checks[-1]=('UI XML well formed',False)
# All 43 XAML files should parse; original templates use no new dependencies.
xaml_files=list((root/'src/YangTools.Revit').rglob('*.xaml'))
ck('All 43 XAML source files present',len(xaml_files)==43)
try:
 for x in xaml_files:ET.parse(x)
except ET.ParseError:
 ck('All XAML XML parse',False)
else: ck('All XAML XML parse',True)
for handler in ['DeleteCheckedNodes_Click','DeleteSelected_Click','AssetDataGrid_CellEditEnding']:
 ck('XAML event '+handler,handler in xaml and re.search(r'\b'+handler+r'\s*\(',ui))
failed=[k for k,v in checks if not v]
print(json.dumps({'suite':'RC2D ProjectAsset delete/GUI contracts','total':len(checks),'passed':len(checks)-len(failed),'failed':failed,'limitation':'Static source / XAML XML checks only; not Windows C# build or Revit Delete/Undo/UI runtime'},ensure_ascii=False,indent=2))
sys.exit(1 if failed else 0)
