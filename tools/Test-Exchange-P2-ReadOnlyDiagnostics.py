#!/usr/bin/env python3
"""Contract/preflight tests, NOT compiled Revit runtime acceptance."""
import importlib.util, json, subprocess, sys, tempfile
from pathlib import Path
root=Path(__file__).resolve().parents[1]
source=(root/'src/YangTools.Revit/Core/CadToRevit/CadRevitExchangeReader.cs').read_text(encoding='utf-8')
modpath=root/'tools/diagnose_exchange_map.py'
spec=importlib.util.spec_from_file_location('diagnose_exchange_map',modpath); mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
fixture=root/'tools/fixtures/RVT_EXCHANGE_V1_HATCH_POLYLINE_P2.json'
r=mod.analyze(fixture,0.7803686370624999)
items={x['handle']:x for x in r['items']}
assert r['source_entries']==3
assert r['hatch_count']==3
assert items['P2_POLY_STRAIGHT']['status']=='eligible_preflight'
assert items['P2_POLY_STRAIGHT']['total_edges']==4
assert items['P2_POLY_CURVED']['status']=='unsupported'
assert items['P2_POLY_CURVED']['curved_polyline_vertex_count']==1
assert items['P2_POLY_SHORT']['status']=='short_edge'
assert items['P2_POLY_SHORT']['short_edge_count']==1
assert 'polylineLoop["vertices"] is JArray vertices' in source
assert 'Math.Abs(bulge) > 1e-12' in source
assert 'item.Boundaries.Add(points)' in source
assert 'ReadPointList(loopToken)' in source  # legacy array loops remain supported
realfixture=root/'tools/fixtures/RVT_EXCHANGE_V1_READER_FIXTURE.json'
r=mod.analyze(realfixture,0.7803686370624999)
assert r['hatch_count']==2
with tempfile.TemporaryDirectory() as tmp:
 out=Path(tmp)/'out.json'
 p=subprocess.run([sys.executable,str(modpath),str(fixture),'--output',str(out)],capture_output=True,text=True)
 assert p.returncode==0,p.stderr
 assert json.loads(out.read_text(encoding='utf-8'))['no_coordinates_exported']
 original=fixture.read_bytes()
 p=subprocess.run([sys.executable,str(modpath),str(fixture),'--output',str(fixture)],capture_output=True,text=True)
 assert p.returncode!=0 and fixture.read_bytes()==original
 p=subprocess.run([sys.executable,str(modpath),str(fixture),'--output',str(out),'--expect-sha256','0'*64],capture_output=True,text=True)
 assert p.returncode!=0
print(json.dumps({'status':'STATIC_CONTRACT_PASS','checks':17,'runtime':'NOT_TESTED','actual_original_C4_map':'NOT_AVAILABLE'},ensure_ascii=False))
