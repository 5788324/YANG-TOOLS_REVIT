#!/usr/bin/env python3
"""RC2G ProjectAsset line-color service & WPF source contracts; NOT runtime/UI click tests."""
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parents[1]
ui = (root/'src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml.cs').read_text(encoding='utf-8-sig')
src = (root/'src/YangTools.Revit/Core/ProjectAssets/ProjectAssetLineColorService.cs').read_text(encoding='utf-8-sig')
xaml = (root/'src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml').read_text(encoding='utf-8-sig')
checks=[]
def ck(label, condition): checks.append((label,bool(condition)))

def position(text, fragment): return text.index(fragment) if fragment in text else 999999

body=ui[ui.index('        private void LineStyleColor_Click('):ui.index('        private void FilledRegionColor_Click(')]
untouched=ui[ui.index('        private void FilledRegionColor_Click('):ui.index('        private void AssetDataGrid_CellEditEnding(')]
for classname in ('ProjectAssetLineColorRequest','ProjectAssetLineColorResult','ProjectAssetLineColorService'):
    ck('Typed '+classname,classname in src)
for prop in ('CategoryId','Red','Green','Blue'):
    ck('Request member '+prop,re.search(r'public\s+\S+\s+'+prop+r'\s*\{',src))
for prop in ('Changed','Red','Green','Blue'):
    ck('Result member '+prop,re.search(r'public\s+\S+\s+'+prop+r'\s*\{',src))
for dep in ('using System.Windows','Dispatcher.','TaskDialog.Show','using YangTools.Revit.Models','DataGrid','ColorSelectionDialog'):
    ck('Service no WPF dependency '+dep,dep not in src)
ck('Checks document validity','document == null || !document.IsValidObject' in src)
ck('Rejects read-only document','document.IsReadOnly' in src)
ck('Rejects invalid requests','request == null || request.CategoryId == null' in src)
ck('Checks invalid ID','ElementId.InvalidElementId' in src)
ck('Line Category domain only','BuiltInCategory.OST_Lines' in src)
ck('Handles negative CategoryIds via lookup','lines?.SubCategories.Cast<Category>()' in src)
ck('Does not misuse doc.GetElement for built-in cats','GetElement(request.CategoryId)' not in src)
ck('Checks vanished category','if (category == null)' in src)
for c in ('Red','Green','Blue'):
    ck('No-op compares '+c,f'oldColor.{c} == request.{c}' in src)
    ck('Actual color readback '+c,f'{c} = actual.{c}' in src)
ck('Exactly one model transaction',src.count('new Transaction(')==1 and 'new TransactionGroup' not in src)
ck('Start result checked','transaction.Start() != TransactionStatus.Started' in src)
ck('Commit result checked','transaction.Commit() != TransactionStatus.Committed' in src)
ck('Only paints during transaction','category.LineColor = new Color(request.Red, request.Green, request.Blue)' in src)
ck('Rollback guarded for started only','transaction.GetStatus() == TransactionStatus.Started' in src)
ck('Rollback result checked','transaction.RollBack() != TransactionStatus.RolledBack' in src)
ck('Uncertain rollback reported','回滚结果无法确认' in src)
ck('No-op comes before transaction', position(src,'if (oldColor != null') < position(src,'new Transaction('))
ck('Success result after commit',position(src,'Changed = true') > position(src,'transaction.Commit()'))
ck('No Revit UIDocument drift','app.ActiveUIDocument' not in body)
ck('Service bound to original Document','ProjectAssetLineColorService.Execute(_doc, request)' in body)
ck('Dialog once','new ColorSelectionDialog' in body and body.count('new ColorSelectionDialog')==1)
ck('Dialog before queuing', position(body,'colorDialog.Show()') < position(body,'_handler.SetAction('))
ck('Cancellation no request','if (colorDialog.Show() != ItemSelectionDialogResult.Confirmed) return;' in body)
ck('No source UI Transaction','new Transaction(' not in body)
ck('Request freezes ID','CategoryId = viewModel.Id' in body)
for prop in ('Red','Green','Blue'):
    ck('Captures RGB '+prop,f'{prop} = chosenColor.{prop}' in body)
ck('Original UI close check','if (_isClosed) return;' in body)
ck('Uses _tabLoadVersion','versionAtRequest = _tabLoadVersion' in body)
ck('Checks stale version','versionAtRequest != _tabLoadVersion' in body)
ck('Checks exact tab','_currentTabName != "LineStyles"' in body)
ck('Checks visibility','LineStyleDataGrid.Visibility' in body)
ck('Checks row identity','LineStyleList.Contains(rowAtRequest)' in body)
ck('Suppresses generic success','showSuccess: false' in body)
ck('Reports failed transaction','TaskDialog.Show("线颜色修改失败", ex.Message)' in body)
ck('No pre-commit Brush update',position(body,'ProjectAssetLineColorService.Execute(_doc, request)') < position(body,'rowAtRequest.ColorBrush ='))
ck('Reports successful no-op','不生成撤销记录' in body)
ck('Reports commit vs UI refresh separately','已提交，但界面刷新失败' in body)
ck('Does not use swallowed catch','catch { }' not in body)
ck('Fill-region color is handled in Core service', 'ProjectAssetFilledRegionVisualService.Execute(_doc, request)' in untouched and 'new Transaction(' not in untouched)
ck('Existing XAML color button bound','Click="LineStyleColor_Click"' in xaml)
ck('Existing fill color button retained','Click="FilledRegionColor_Click"' in xaml)
ck('Existing line pattern retained','DropDownClosed="LinePattern_DropDownClosed"' in xaml)
ck('MCP preserved in source', (root/'McpServer/server.py').is_file())
ck('45 previous test suites retained',len([p for p in (root/'tools').glob('Test-*.py') if p.name != 'Test-ProjectAssetRC2G.py'])>=45)
ck('43 XAML files preserved',len(list((root/'src/YangTools.Revit').rglob('*.xaml')))==43)
try:
    for x in (root/'src/YangTools.Revit').rglob('*.xaml'): ET.parse(x)
    ck('All XAML XML parses',True)
except ET.ParseError as e:
    ck('All XAML XML parses',False)
ck('No unrelated MCP edits in Service','McpGateway' not in src and 'Rebar' not in src)

bad=[name for name, ok in checks if not ok]
print(json.dumps({'suite':'RC2G ProjectAsset line color & UI source contracts',
                  'total':len(checks), 'passed':len(checks)-len(bad),
                  'failed':bad, 'caveat':'Static source assertions only. Windows Build/Revit GUI/Undo not tested.'},
                 ensure_ascii=False,indent=2))
sys.exit(bool(bad))
