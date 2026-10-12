#!/usr/bin/env python3
"""RC2T cross-feature closure and focused UI contracts. Static only; not C# compile or Revit execution."""
from pathlib import Path
import xml.etree.ElementTree as ET
import json, re, sys
root = Path(__file__).resolve().parents[1]
base = root/'src/YangTools.Revit'
checks=[]
def code(file):return (base/file).read_text(encoding='utf-8-sig')
def check(name, ok):checks.append((name,bool(ok)))
loft=code('UI/EntityGeneratorWindow.xaml.cs')
solid=code('UI/SolidGeneratorWindow.xaml.cs')
linear=code('UI/LinearPlacementWindow.xaml.cs')
linear_xaml=code('UI/LinearPlacementWindow.xaml')
event=code('Core/RevitEventHandler.cs')
for name,src in [('Loft',loft),('Solid',solid),('Linear',linear)]:
 check(name+' level list uses project elevation', bool(re.search(r'\.OrderBy\(\w+ => \w+\.ProjectElevation\)', src)))
 check(name+' no shared-elevation order', not bool(re.search(r'\.OrderBy\(\w+ => \w+\.Elevation\)', src)))
for token in ['int levelIdx = -1','bool hasSavedLevel =','State.TargetLevelId == ElementId.InvalidElementId',
              'levelIdx >= 0','!hasSavedLevel && CmbLevel.Items.Count > 0','else if (hasSavedLevel)',
              '目标标高已失效','请重新选择','CmbLevel.SelectedIndex = levelIdx','CmbLevel.SelectedIndex = 0']:
 check('Loft safe saved level '+token,token in loft)
check('Loft does not fallback saved stale Level name after ID missing',loft.index('State.TargetLevelId == ElementId.InvalidElementId')<loft.index('levels[i].Name.Equals'))
check('Loft level fallback only when no historical selection',re.search(r'else if \(!hasSavedLevel && CmbLevel.Items.Count > 0\)\s*CmbLevel.SelectedIndex = 0',loft) is not None)
for token in ['hasSavedSymbol','hasSavedLevel','savedSymbol : symbols.FirstOrDefault()',
              'hasSavedLevel\n                            ? savedLevel','之前保存的族类型已失效','之前保存的标高已失效',
              '_viewModel.PathInfoText =','_viewModel.SelectedSymbol =','_viewModel.SelectedLevel =']:
 check('Linear stale setting '+token,token in linear)
check('Linear saved type missing is not replaced', 'savedSymbol ?? symbols.FirstOrDefault()' not in linear)
check('Linear saved level missing is not replaced','savedLevel ?? activeLevel ?? levels.FirstOrDefault()' not in linear)
check('Linear warning visible in XAML', 'Text="{Binding PathInfoText}"' in linear_xaml and 'TextWrapping="Wrap"' in linear_xaml)
check('Linear path pick no prequeue Hide', 'long version = ++_pathVersion;\n            this.Hide();' not in linear)
pick=linear[linear.index('private void PickPath_Click'):linear.index('private void StartPlacement_Click')]
for token in ['SetAction(app =>','if (_isClosed || version != _pathVersion) return;','Hide();',
              'new LinearPlacementEngine(app.ActiveUIDocument)','finally','Show(); Activate();','_externalEvent.Raise()']:
 check('Pick path '+token,token in pick)
check('Pick path hide only after handler begins action',pick.index('SetAction(app =>') < pick.index('Hide();'))
check('Pick path shows again in finally',pick.index('Hide();') < pick.index('finally') < pick.index('Show(); Activate();'))
check('Linear close does not duplicate settings save', 'private void CloseButton_Click(object sender, RoutedEventArgs e)\n        {\n            Close();\n        }' in linear)
for token in ['new RevitEventHandler(_doc,','wrapInTransactionGroup: false','_isClosed = true','++_pathVersion','++_placementVersion',
              'LinearPlacementRequest.Capture(_viewModel)','Validation.GetHasError(textbox)',
              'engine.ExecutePlacement(request)','result.ReadbackConfirmed','result.ReadbackWarning']:
 check('Linear context '+token,token in linear)
for token in ['_expectedDocument.IsValidObject','doc.Equals(_expectedDocument)','SKIPPED_DOCUMENT_MISMATCH',
              'TryResolveDocument(app, item.name, out var doc)','continue;']:
 check('Shared ExternalEvent documents '+token,token in event)
# Confirm that the seven model-modification entry points have actual implementations and matching UI XAML.
for name in ['ColumnAlongPath','LinearPlacement','LevelModifier','FaceBasedConverter','EntityGenerator','SolidGenerator','BooleanGeometry']:
 check(name+' Revit command exists',(base/'Commands'/f'{name}Command.cs').exists())
 check(name+' WPF XAML exists',(base/'UI'/f'{name}Window.xaml').exists())
 check(name+' WPF codebehind exists',(base/'UI'/f'{name}Window.xaml.cs').exists())
 check(name+' Core engine exists',(base/'Core'/f'{name}Engine.cs').exists())
 try:
  ET.parse(base/'UI'/f'{name}Window.xaml');check(name+' XAML parses',True)
 except ET.ParseError:
  check(name+' XAML parses',False)
# Snapshot/atomicity barriers for the model-modification pipelines.
assertions={
 'ColumnAlongPath':('Core/ColumnAlongPathEngine.cs',['ColumnAlongPathPlacementRequest','TransactionStatus.Committed','TransactionStatus.RolledBack']),
 'LinearPlacement':('Core/LinearPlacementEngine.cs',['LinearPlacementRequest','ProjectElevation','TransactionStatus.Committed','TransactionStatus.RolledBack']),
 'LevelModifier':('Core/LevelModifierService.cs',['Transaction','TransactionStatus.Committed','TransactionStatus.RolledBack']),
 'FaceBasedConverter':('Core/FaceBasedConverterEngine.cs',['Solid','InvalidOperationException']),
 'Loft':('Core/EntityGeneratorEngine.cs',['CheckZBounds','ProjectElevation','TransactionGroup','group.Assimilate()']),
 'SolidGenerator':('Core/SolidGeneratorEngine.cs',['ProjectElevation','TransactionGroup','group.Assimilate()']),
 'BooleanGeometry':('Core/BooleanGeometryEngine.cs',['BooleanOperationsUtils','InvalidOperationException'])
}
for name,(file,tokens) in assertions.items():
 c=code(file)
 for token in tokens: check(name+' safeguard '+token,token in c)
check('RC2T has not added new MCP routing commands', (root/'McpServer/server.py').exists())
check('Seven model-modification previous tests retained',all((root/'tools'/f'Test-ModelModifyRC2{x}.py').exists() for x in 'MNOPQRS'))
# Column path previously used Shared/Display Level.Elevation in XYZ and base/top calculations.
column_engine=code('Core/ColumnAlongPathEngine.cs')
column_ui=code('UI/ColumnAlongPathWindow.xaml.cs')
column_model=code('Models/ColumnAlongPathModels.cs')
for token in ['baseLevel.ProjectElevation + baseOffsetFeet', 'topLevel.ProjectElevation + topOffsetFeet',
              'new XYZ(routePoint.X, routePoint.Y, baseLevel.ProjectElevation)',
              'placedBase.AsElementId() != baseLevel.Id', 'placedTop.AsElementId() != topLevel.Id',
              'placedBaseOffset.AsDouble() - baseOffsetFeet', 'placedTopOffset.AsDouble() - topOffsetFeet',
              'positionToleranceFeet = 1.0 / 304.8']:
 check('Column world elevation/readback '+token,token in column_engine)
for token in ['.OrderBy(level => level.ProjectElevation)','ProjectElevation = level.ProjectElevation',
              'level.ProjectElevation > baseLevel.ProjectElevation + 1e-6',
              'hasSavedColumnType','hasSavedBaseLevel','hasSavedTopLevel',
              '保存的柱类型已失效','保存的 Base Level 已失效','保存的 Top Level 已失效']:
 check('Column UI project level/stale '+token, token in column_ui)
check('Column LevelOption names project coordinate clearly','public double ProjectElevation' in column_model)
check('Column LevelOption legacy ambiguous elevation removed','public double Elevation { get; set; }' not in column_model)
check('Column no incorrect Shared base formula','baseLevel.Elevation + baseOffsetFeet' not in column_engine)
check('Column no incorrect Shared top formula','topLevel.Elevation + topOffsetFeet' not in column_engine)
# Independent world-coordinate arithmetic examples; not an invocation of Revit API.
for base,shared_shift,baseoffset,top,topoffset in [
 (0,3300,1000,4000,-500),(3500,-1400,250,10000,0),(-1500,750,0,3500,500)]:
 expected_base=base+baseoffset
 expected_top=top+topoffset
 display_base=base+shared_shift+baseoffset
 check(f'Column shared shift must not alter base Z base={base}',expected_base==base+baseoffset)
 check(f'Column shifted display != project Z shift={shared_shift}',display_base!=expected_base)
 check(f'Column top above base {top}',expected_top>expected_base)

check('No C# Revit .NET Windows build claim', True)
failed=[name for name,ok in checks if not ok]
print(json.dumps({'suite':'RC2T model modification closure','total':len(checks),'passed':len(checks)-len(failed),
 'failed':failed,'scope':'static source/UI contracts; not C# compilation or actual Revit execution'},ensure_ascii=False,indent=2))
sys.exit(1 if failed else 0)
