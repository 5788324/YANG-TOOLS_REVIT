#!/usr/bin/env python3
from pathlib import Path
import json
import sys

root = Path(__file__).resolve().parents[1]
xaml = (root/'src/YangTools.Revit/UI/ChineseCheckWindow.xaml').read_text(encoding='utf-8')
cs = (root/'src/YangTools.Revit/UI/ChineseCheckWindow.xaml.cs').read_text(encoding='utf-8')
cmd = (root/'src/YangTools.Revit/Commands/ChineseCheckCommand.cs').read_text(encoding='utf-8')

checks = {
    'window title upgraded': '项目中文检查与清理' in xaml,
    'category navigation': 'x:Name="GroupList"' in xaml,
    'single data grid': 'x:Name="ResultGrid"' in xaml,
    'checkbox select': 'IsChecked' in xaml,
    'select all': 'BtnSelectAll_Click' in xaml,
    'select none': 'BtnSelectNone_Click' in xaml,
    'invert selection': 'BtnInvertSelection_Click' in xaml,
    'search': 'SearchBox_TextChanged' in xaml,
    'double edit': 'ResultGrid_CellEditEnding' in xaml,
    'edit refresh deferred': 'Dispatcher.BeginInvoke' in cs and 'DispatcherPriority.Background' in cs,
    'rename capability column': '可改名' in xaml,
    'delete capability column': '可删除' in xaml,
    'danger delete button': 'DangerButton' in xaml and 'BtnDeleteSelected_Click' in xaml,
    'family group': 'GroupFamily' in cs,
    'family type group': 'GroupFamilyType' in cs,
    'system type group': 'GroupSystemType' in cs,
    'material group': 'GroupMaterial' in cs,
    'group type group': 'GroupGroupType' in cs,
    'assembly group': 'GroupAssembly' in cs,
    'view group': 'GroupView' in cs,
    'view template group': 'GroupViewTemplate' in cs,
    'sheet group': 'GroupSheet' in cs,
    'schedule group': 'GroupSchedule' in cs,
    'schedule heading group': 'GroupScheduleHeading' in cs,
    'revision group': 'GroupRevision' in cs and 'AddRevisionText' in cs,
    'level group': 'GroupLevel' in cs,
    'grid group': 'GroupGrid' in cs,
    'filter group': 'GroupViewFilter' in cs,
    'workset group': 'GroupWorkset' in cs,
    'design option group': 'GroupDesignOption' in cs,
    'scope box group': 'GroupScopeBox' in cs,
    'fill pattern group': 'GroupFillPattern' in cs,
    'line pattern group': 'GroupLinePattern' in cs,
    'subcategory group': 'GroupSubcategory' in cs and 'parentCategory.SubCategories' in cs,
    'phase group': 'GroupPhase' in cs,
    'location group': 'GroupProjectLocation' in cs,
    'project param group': 'GroupProjectParameter' in cs,
    'shared param group': 'GroupSharedParameter' in cs,
    'type param name group': 'GroupTypeParameterName' in cs,
    'type param value group': 'GroupTypeParameterValue' in cs,
    'instance param name group': 'GroupInstanceParameterName' in cs,
    'instance param value group': 'GroupInstanceParameterValue' in cs,
    'all instance elements scanned': '.WhereElementIsNotElementType()' in cs and '正在扫描实例参数' in cs,
    'builtin parameter names excluded': 'parameterId > 0 && ContainsChinese(parameterName)' in cs,
    'text note group': 'GroupTextNote' in cs,
    'room group': 'GroupRoom' in cs,
    'space group': 'GroupSpace' in cs,
    'area group': 'GroupArea' in cs,
    'project info group': 'GroupProjectInfo' in cs,
    'revit link group': 'GroupRevitLink' in cs,
    'cad group': 'GroupCadLink' in cs,
    'print setting group': 'GroupPrintSetting' in cs,
    'view sheet set group': 'GroupViewSheetSet' in cs,
    'dwg group': 'GroupDwgExportSetting' in cs,
    'transaction rename': 'new Transaction(_doc, "YangTools - 中文检查重命名")' in cs,
    'workset rename api': 'WorksetTable.RenameWorkset' in cs,
    'schedule heading edit': '.ColumnHeading = newValue' in cs,
    'revision text edit': 'case EditKind.RevisionText' in cs and 'revision.Description = newValue' in cs and 'revision.IssuedTo = newValue' in cs,
    'sheet number edit': 'sheet.SheetNumber = newValue' in cs,
    'text note edit': 'note.Text = newValue' in cs,
    'parameter edit': 'parameter.Set(newValue)' in cs,
    'parameter clear': 'parameter.Set(string.Empty)' in cs,
    'delete preview rollback': '中文清理删除预检' in cs and 'sub.RollBack()' in cs,
    'batch subtransactions': 'new SubTransaction(_doc)' in cs,
    'one undo transaction': '中文检查批量清理' in cs,
    'command description updated': '双击重命名及安全删除/清空' in cmd,
    'old readonly claim removed': '只读检查族实例' not in cmd,
}

failed = [name for name, ok in checks.items() if not ok]
report_path = root / 'artifacts/verification/chinese-cleanup-report.json'
report_path.parent.mkdir(parents=True, exist_ok=True)
report_path.write_text(json.dumps({
    'checks': len(checks),
    'passed': len(checks) - len(failed),
    'failures': failed,
    'status': 'PASS' if not failed else 'FAIL',
}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'Chinese cleanup static contract: {len(checks)-len(failed)}/{len(checks)} passed')
print('report:', report_path)
for name in failed:
    print('FAIL:', name)
if failed:
    sys.exit(1)
