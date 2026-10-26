#!/usr/bin/env python3
"""Ribbon UI static contract: full command inventory, visible labels, long help, unique icon families."""
from collections import Counter, defaultdict
from pathlib import Path
import hashlib
import re
import sys
import struct

ROOT = Path(__file__).resolve().parents[1]
COMMAND_ROOT = ROOT / 'src/YangTools.Revit/Commands'
ICON_ROOT = ROOT / 'src/YangTools.Revit/Icons'
BUILDER = ROOT / 'src/YangTools.Revit/Core/RibbonBuilder.cs'
ATTRIBUTE = ROOT / 'src/YangTools.Revit/Core/RibbonButtonAttribute.cs'
RULES = ROOT / 'docs/RIBBON_FEATURE_DEVELOPMENT_RULES.md'
CATALOG = ROOT / 'docs/RIBBON_FEATURE_CATALOG_RC3J.md'
GENERATOR = ROOT / 'tools/Generate-RibbonIcons.py'
errors = []
checks = 0

def ensure(condition, message):
    global checks
    checks += 1
    if not condition: errors.append(message)

records=[]
pat=re.compile(r'\[RibbonButton\((.*?)\)\]\s*public\s+(?:sealed\s+)?class\s+(\w+)',re.S)
for path in sorted(COMMAND_ROOT.glob('*.cs')):
    source=path.read_text(encoding='utf-8-sig')
    for m in pat.finditer(source):
        raw,klass=m.groups()
        params=dict(re.findall(r'\b(\w+):\s*"((?:\\.|[^"\\])*)"',raw))
        ensure(all(x in params for x in ('panelName','buttonText','tooltip','usage','largeIcon','smallIcon')), f'{klass}: missing metadata')
        if not all(x in params for x in ('panelName','buttonText','tooltip','usage','largeIcon','smallIcon')): continue
        panel,label=params['panelName'],params['buttonText']
        ensure(2 <= len(label) <= 6 and '\\n' not in label, f'{klass}: Ribbon label too long or short: {label}')
        ensure(bool(re.search('[\u4e00-\u9fff]',label)),'{0}: non-Chinese label'.format(klass))
        ensure(params['tooltip'].strip() and len(params['tooltip'])>=9, f'{klass}: insufficient tooltip')
        ensure('使用：' in params['usage'] and ('注意：' in params['usage'] or '限制：' in params['usage'] or '建议：' in params['usage']),f'{klass}: Usage lacks steps and boundaries')
        ensure(params['largeIcon'].startswith('Icons/') and params['smallIcon'].startswith('Icons/'),f'{klass}: icon path not relative')
        ensure(params['largeIcon'].replace('_32.png','') == params['smallIcon'].replace('_16.png',''), f'{klass}: icon 16/32 family mismatch')
        records.append((klass,panel,label,params))
ensure(len(records)==36, f'expected 36 buttons; got {len(records)}')
ensure(len({r[0] for r in records})==len(records), 'duplicate command class')
ensure(len({r[2] for r in records})==len(records), 'duplicate short label')
ensure(len({r[3]['largeIcon'] for r in records})==len(records), 'large icon path reused')
ensure(len({r[3]['smallIcon'] for r in records})==len(records), 'small icon path reused')
ensure(Counter(r[1] for r in records)['项目管理区']==8,'项目管理区 should have 8 buttons')
ensure(not any(r[1]=='检查工具区' for r in records), 'orphan 检查工具区')
ensure({r[0] for r in records if r[1]=='项目管理区'}>= {'ChineseCheckCommand','DuplicateProjectParameterCleanupCommand'},'cleanup features were not moved')
ensure(len(set(r[1] for r in records))==10, 'expected 10 panels after consolidation')

hashes=defaultdict(list)
referenced=set()
for klass,panel,label,params in records:
    for sz,field in ((16,'smallIcon'),(32,'largeIcon')):
        icon_path=ICON_ROOT / Path(params[field]).name
        referenced.add(icon_path.name)
        ensure(icon_path.exists(), f'{klass}: {sz}px icon missing: {icon_path.name}')
        if not icon_path.exists(): continue
        png = icon_path.read_bytes()
        ensure(png.startswith(b'\x89PNG\r\n\x1a\n') and png[12:16] == b'IHDR', f'{klass}: invalid PNG header {sz}')
        if len(png) >= 26 and png[12:16] == b'IHDR':
            width, height = struct.unpack('>II',png[16:24])
            ensure((width,height)==(sz,sz), f'{klass}: icon bad size {sz}')
            ensure(png[25]==6, f'{klass}: icon must be RGBA PNG {sz}')
        hashes[(sz,hashlib.sha256(icon_path.read_bytes()).hexdigest())].append(klass)
for (sz,digest),klasses in hashes.items():
    ensure(len(klasses)==1,f'{sz}px actual image bytes duplicated by: {klasses}')
ensure({p.name for p in ICON_ROOT.glob('*.png')}==referenced,
       'unused/unmanaged PNG assets: '+str({p.name for p in ICON_ROOT.glob('*.png')}^referenced))
source=BUILDER.read_text(encoding='utf-8-sig')
attr=ATTRIBUTE.read_text(encoding='utf-8-sig')
ensure('LongDescription = ' in source and 'attr.Usage' in source,'RibbonBuilder LongDescription not wired')
ensure('public string Usage { get; }' in attr and 'string usage = ""' in attr,'RibbonButtonAttribute missing Usage')
cat=CATALOG.read_text(encoding='utf-8-sig') if CATALOG.exists() else ''
for klass,panel,label,params in records:
    ensure('`'+klass+'`' in cat and '| '+label+' |' in cat, f'{klass}: catalog not updated')
for path in (RULES,GENERATOR): ensure(path.exists(),f'missing {path.name}')
gen=GENERATOR.read_text(encoding='utf-8-sig')
for _,_,_,params in records:
    fam=Path(params['largeIcon']).stem[:-3]
    ensure("'"+fam+"':" in gen,f'icon family {fam} absent in generator; regeneration could delete it')
print(f'RIBBON_UX_CHECKS={checks}; errors={len(errors)}; commands={len(records)}; panels={len(set(r[1] for r in records))}; icon_pairs={len(referenced)//2}')
for err in errors: print('ERROR:',err)
sys.exit(1 if errors else 0)
