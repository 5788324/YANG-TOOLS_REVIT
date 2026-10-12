#!/usr/bin/env python3
"""RC2H static contracts: line-pattern ComboBox + Revit transaction, no runtime claim."""
import json
import re
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

root=Path(__file__).resolve().parents[1]
ui=(root/'src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml.cs').read_text('utf-8-sig')
xaml=(root/'src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml').read_text('utf-8-sig')
service=(root/'src/YangTools.Revit/Core/ProjectAssets/ProjectAssetLinePatternService.cs').read_text('utf-8-sig')
block=ui.split('private void LinePattern_DropDownClosed(',1)[1].split('private void FillPattern_DropDownClosed(',1)[0]
checks=[]
def check(name,passed):checks.append((name,bool(passed)))
def before(a,b):return a in block and b in block and block.index(a)<block.index(b)
for part in ('ProjectAssetLinePatternRequest','ProjectAssetLinePatternResult','ProjectAssetLinePatternService'):
    check('typed '+part,part in service)
for v in ('CategoryId','PatternId'):
    check('request member '+v,re.search(r'public\s+ElementId\s+'+v+r'\s*\{',service))
for term in ('Changed','PatternId'):
    check('result member '+term, term in service)
for dep in ('using System.Windows','Dispatcher.','TaskDialog.Show','ComboBox','DataGrid','ViewModel','ColorSelectionDialog'):
    check('service has no UI dependency '+dep,dep not in re.sub(r'///[^\n]*','',service))
for token in ('document == null || !document.IsValidObject','document.IsReadOnly',
              'request.CategoryId == null','request.PatternId == null','InvalidElementId.GetIdValue()',
              'BuiltInCategory.OST_Lines','lines?.SubCategories.Cast<Category>()','if (category == null)',
              'LinePatternElement.GetSolidPatternId()','document.GetElement(request.PatternId) is LinePatternElement',
              'category.GetLinePatternId(GraphicsStyleType.Projection)',
              'oldId.GetIdValue() == request.PatternId.GetIdValue()',
              'category.SetLinePatternId(request.PatternId, GraphicsStyleType.Projection)',
              'transaction.Start() != TransactionStatus.Started',
              'transaction.Commit() != TransactionStatus.Committed',
              'transaction.GetStatus() == TransactionStatus.Started',
              'transaction.RollBack() != TransactionStatus.RolledBack',
              '回滚结果无法确认','var actual = category.GetLinePatternId',
              'Changed = true, PatternId = actual, ReadbackConfirmed = true'):
    check('service contract: '+token,token in service)
check('service single transaction',service.count('new Transaction(')==1)
check('service no transaction group','TransactionGroup(' not in service)
check('service no accidental negative category rejection',not re.search(r'CategoryId\.GetIdValue\(\)\s*<\s*0',service))
check('service validates before transaction',service.index('document.GetElement(request.PatternId)')<service.index('new Transaction('))
check('service no-op before transaction',service.index('if (oldId.GetIdValue()')<service.index('new Transaction('))
check('service separates committed readback errors', 'ReadbackConfirmed = false' in service and 'ReadbackWarning' in service and '回读失败' in service)
check('service readback after commit',service.index('var actual = category.GetLinePatternId')>service.index('transaction.Commit()'))
check('UI opens normal handler', 'private void LinePattern_DropDownClosed(object sender, EventArgs e)' in ui)
check('XAML event unchanged','DropDownClosed="LinePattern_DropDownClosed"' in xaml)
check('XAML has explicit binding','SelectedValue="{Binding LinePatternId, Mode=TwoWay, UpdateSourceTrigger=Explicit}"' in xaml)
check('XAML not eager bound','SelectedValue="{Binding LinePatternId, UpdateSourceTrigger=PropertyChanged}"' not in xaml)
for tok in ('if (_isClosed) return','_currentTabName != "LineStyles"',
            'LineStyleDataGrid.Visibility != Visibility.Visible','LineStyleList.Contains(rowAtRequest)',
            'combo.SelectedValue as ElementId','combo.SetCurrentValue(',
            'System.Windows.Controls.Primitives.Selector.SelectedValueProperty',
            'rowAtRequest.LinePatternId','request = new ProjectAssetLinePatternRequest',
            'CategoryId = rowAtRequest.Id','PatternId = chosenId','versionAtRequest = _tabLoadVersion',
            'sequenceAtRequest = ++_linePatternRequestVersion',
            'sequenceAtRequest != _linePatternRequestVersion','ProjectAssetLinePatternService.Execute(_doc, request)',
            'rowAtRequest.LinePatternId = result.PatternId','StatusText.Text = "线图案修改未成功："',
            'TaskDialog.Show("线图案修改失败", ex.Message)',
            '线图案已提交，但界面刷新失败','showSuccess: false','_externalEvent.Raise()'):
    check('UI contract: '+tok,tok in block)
check('selection restored before queued request',before('combo.SetCurrentValue(', '_handler.SetAction('))
check('UI no inline transaction','new Transaction(' not in block)
check('UI distinguishes readback failure', 'if (!result.ReadbackConfirmed)' in block and 'TaskDialog.Show("线图案已提交", result.ReadbackWarning)' in block)
check('service before committed VM',before('ProjectAssetLinePatternService.Execute(_doc, request)','rowAtRequest.LinePatternId = result.PatternId'))
check('request ignored on closed dialog','_isClosed || versionAtRequest != _tabLoadVersion' in block)
check('window close invalidates pattern request','_linePatternRequestVersion++;' in ui.split('protected override void OnClosed',1)[1].split('private void TitleBar_',1)[0])
check('original fill code untouched','private void FillPattern_DropDownClosed' in ui)
check('other XAML fill bindings preserved','SelectedValue="{Binding ForePatternId, UpdateSourceTrigger=PropertyChanged}"' in xaml and 'SelectedValue="{Binding BackPatternId, UpdateSourceTrigger=PropertyChanged}"' in xaml)
check('XAML parse', ET.fromstring(xaml) is not None)
check('all 43 XAML files preserved',len(list((root/'src/YangTools.Revit').rglob('*.xaml')))==43)
check('MCP server retained',(root/'McpServer/server.py').is_file())
check('original 47 suites retained',len(list((root/'tools').glob('Test-*.py')))>=47)
failed=[name for name,pass_ in checks if not pass_]
print(json.dumps({'suite':'RC2H line pattern UI and transaction SOURCE contracts','total':len(checks),'passed':len(checks)-len(failed),'failed':failed,'scope':'static only; Windows Build/Revit GUI/Undo NOT_TESTED'},ensure_ascii=False,indent=2))
sys.exit(bool(failed))
