#!/usr/bin/env python3
"""RC3H structural regression for Document wrapper identity, 12 removed Ribbon tests and pinned API. Static only."""
from pathlib import Path
import json,re,sys
R=Path(__file__).resolve().parents[1]
S=R/'src/YangTools.Revit'
read=lambda p:p.read_text(encoding='utf-8-sig')
checks={}
def chk(n,condition):checks[n]=bool(condition)
def cmd(name):return read(S/'Commands'/f'{name}.cs')
helper=read(S/'Core/DocumentIdentityGuard.cs')
chk('Document.Equals semantics without wrapper ReferenceEquals', 'expected.Equals(current)' in helper and 'ReferenceEquals' not in helper.replace('never ReferenceEquals',''))
chk('identity guard validates live active doc','application.ActiveUIDocument?.Document' in helper and 'IsValidObject' in helper)
for name in ('PasteCadCommand','CadToRevitCommand'):
 s=cmd(name)
 chk(f'{name} preserves existing dialog and view check', 'ShowDialog()' in s and 'ActiveView.Id.GetIdValue()' in s)
 chk(f'{name} uses value-semantic document guard', 'DocumentIdentityGuard.IsSameActiveDocument' in s and 'ReferenceEquals(' not in s)
chk('PasteCad context guard before file copy',cmd('PasteCadCommand').index('IsSameActiveDocument') < cmd('PasteCadCommand').index('CreateWorkingCopy(sourcePath)'))
chk('CAD conversion checks twice',cmd('CadToRevitCommand').count('DocumentIdentityGuard.IsSameActiveDocument')==2)
for name in ('BooleanGeometryCommand','SolidGeneratorCommand'):
 s=cmd(name)
 chk(f'{name} document guard fixed', 'DocumentIdentityGuard.IsSameActiveDocument' in s and 'ReferenceEquals(' not in s)
s=cmd('BooleanGeometryCommand')
chk('Boolean selected elements use document equality','doc.Equals(instance.Document)' in s)
s=read(S/'Models/BatchTask/BatchTaskViewModel.cs')
chk('Batch open/current use semantic equality','document.Equals(uiApp.ActiveUIDocument.Document)' in s)
chk('Batch compare opened models use semantic equality','left.OpenDocument.Equals(right.OpenDocument)' in s)
removed=['RebarRelationship','RebarClearance','RebarFreeFormModify','RebarFreeFormCreate','RebarCoupler','RebarConstraintRebind','RebarShapeDrivenCreate','RebarHookModify','RebarInspector','RebarSurfaceVaryingCreate','RebarFaceDrivenCreate','RebarShapeDrivenModify']
for n in removed: chk('removed command '+n,not (S/'Commands'/f'{n}Command.cs').exists())
remaining=list((S/'Commands').glob('Rebar*Command.cs'))
chk('only GR01 Ribbon command remains',len(remaining)==1 and remaining[0].name=='RebarGradientSetCommand.cs')
chk('GR01 command grouped as production', 'groupName: "创建钢筋"' in cmd('RebarGradientSetCommand'))
chk('no rebar test Ribbon group', not any('groupName: "钢筋测试"' in read(p) for p in (S/'Commands').glob('*.cs')))
icons={'rebar_test','rebar_face','rebar_clearance','rebar_constraint','rebar_coupler','rebar_freeform_create','rebar_freeform_modify','rebar_hook','rebar_inspector','rebar_relationship','rebar_shape_create','rebar_shape_modify','rebar_surface_varying'}
chk('removed all 26 test icons',all(not (S/'Icons'/f'{n}_{sz}.png').exists() for n in icons for sz in (16,32)))
chk('retain both GR01 icons',all((S/'Icons'/f'rebar_gradient_set_{sz}.png').exists() for sz in (16,32)))
chk('retain backend for GR01/MCP', (S/'Core/Rebar/RebarInspectionService.cs').exists() and (S/'Core/Rebar/RebarGradientSetService.cs').exists())
prj=read(S/'YangTools.Revit.csproj')
chk('stale command build hard stop','RebarInspectorCommand.cs' in prj and 'RebarShapeDrivenModifyCommand.cs' in prj)
chk('2025 pinned','<PinnedRevitApiVersion>2025.4.20</PinnedRevitApiVersion>' in prj)
chk('2026 pinned','<PinnedRevitApiVersion>2026.4.0</PinnedRevitApiVersion>' in prj)
chk('both API packages pinned via property',prj.count('Version="$(PinnedRevitApiVersion)"')==2)
chk('no automatic deployment by default',"'$(DeployAfterBuild)' == 'true'" in prj)
s=read(R/'tools/Test-UiPanelsStatic.py')
chk('Ribbon count 36 includes RC3J tools', 'ribbon_count != 36' in s and '预期 36' in s)
chk('no legacy UI checks for test group','expected_rebar_groups = {"创建钢筋"}' in s)
failed=[n for n,ok in checks.items() if not ok]
report={'suite':'RC3H Cad/Document identity + 12 test Ribbon removals + minimum API pins','total':len(checks),'passed':len(checks)-len(failed),'failed':failed,'runtime':'NOT_TESTED'}
(R/'tools/rc3h-cad-rebar-static-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
sys.exit(bool(failed))
