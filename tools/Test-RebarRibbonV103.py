import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / 'src' / 'YangTools.Revit'
COMMANDS = SRC / 'Commands'
ICONS = SRC / 'Icons'
checks=[]
def add(name, ok): checks.append({'name':name,'ok':bool(ok)})
def text(path): return Path(path).read_text(encoding='utf-8-sig', errors='replace')

files=sorted(COMMANDS.glob('Rebar*Command.cs'))
attrs={}
for p in files:
    s=text(p)
    m=re.search(r'\[RibbonButton\((.*?)\)\]',s,re.S)
    if m: attrs[p.name]=m.group(1)

add('V103 exactly 13 rebar commands', len(attrs)==13)
grad=attrs.get('RebarGradientSetCommand.cs','')
add('V103 gradient is only production create', 'groupName: "创建钢筋"' in grad)
for name,block in attrs.items():
    if name=='RebarGradientSetCommand.cs': continue
    add(f'V103 {name} in rebar test', 'groupName: "钢筋测试"' in block)

add('V103 no legacy rebar groups', all(x not in '\n'.join(attrs.values()) for x in ['groupName: "修改钢筋"','groupName: "端部与连接"','groupName: "检查钢筋"']))

expected_icons={
'RebarGradientSetCommand.cs':'rebar_gradient_set',
'RebarFaceDrivenCreateCommand.cs':'rebar_face',
'RebarSurfaceVaryingCreateCommand.cs':'rebar_surface_varying',
'RebarShapeDrivenCreateCommand.cs':'rebar_shape_create',
'RebarFreeFormCreateCommand.cs':'rebar_freeform_create',
'RebarShapeDrivenModifyCommand.cs':'rebar_shape_modify',
'RebarConstraintRebindCommand.cs':'rebar_constraint',
'RebarFreeFormModifyCommand.cs':'rebar_freeform_modify',
'RebarHookModifyCommand.cs':'rebar_hook',
'RebarRelationshipCommand.cs':'rebar_relationship',
'RebarCouplerCommand.cs':'rebar_coupler',
'RebarInspectorCommand.cs':'rebar_inspector',
'RebarClearanceCommand.cs':'rebar_clearance',
}
for name,icon in expected_icons.items():
    block=attrs.get(name,'')
    add(f'V103 {name} unique 32 icon', f'Icons/{icon}_32.png' in block)
    add(f'V103 {name} unique 16 icon', f'Icons/{icon}_16.png' in block)
    add(f'V103 {icon} 32 exists', (ICONS/f'{icon}_32.png').is_file())
    add(f'V103 {icon} 16 exists', (ICONS/f'{icon}_16.png').is_file())

all_blocks='\n'.join(attrs.values())
add('V103 rebar no project inspector placeholder', 'Icons/project_inspector_' not in all_blocks)
add('V103 create pulldown icon', 'groupLargeIcon: "Icons/rebar_gradient_set_32.png"' in grad and 'groupSmallIcon: "Icons/rebar_gradient_set_16.png"' in grad)
add('V103 test pulldown icon assets', (ICONS/'rebar_test_32.png').is_file() and (ICONS/'rebar_test_16.png').is_file())
add('V103 all test commands carry stable group icon', all('groupLargeIcon: "Icons/rebar_test_32.png"' in b and 'groupSmallIcon: "Icons/rebar_test_16.png"' in b for n,b in attrs.items() if n!='RebarGradientSetCommand.cs'))

attr=text(SRC/'Core'/'RibbonButtonAttribute.cs')
builder=text(SRC/'Core'/'RibbonBuilder.cs')
add('V103 group icon attribute supported','GroupLargeIcon' in attr and 'GroupSmallIcon' in attr)
add('V103 builder group icon fallback','x.Attr.GroupLargeIcon' in builder and 'x.Attr.GroupSmallIcon' in builder and '?? firstAttr.LargeIcon' in builder)

generator=text(ROOT/'tools'/'Generate-RibbonIcons.py')
for icon in list(expected_icons.values())+['rebar_test']:
    add(f'V103 generator owns {icon}', f"'{icon}':" in generator)

failed=[c for c in checks if not c['ok']]
report={'checks':checks,'summary':{'passed':len(checks)-len(failed),'failed':len(failed),'total':len(checks)}}
(ROOT/'tools/rebar-ribbon-v1-0-3-static-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
for c in checks: print(('PASS' if c['ok'] else 'FAIL')+' - '+c['name'])
print(f"SUMMARY {len(checks)-len(failed)}/{len(checks)} PASS")
raise SystemExit(1 if failed else 0)
