#!/usr/bin/env python3
"""Read-only per-handle geometric triage for YangCAD RVT Exchange V1 maps.

Pure stdlib; never modifies CAD/Revit, the MAP source, or its coordinates.
This is a preflight approximation, NOT proof of Revit creation/compatibility.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from collections import Counter
from pathlib import Path

UNIT_MM = {"mm": 1., "millimeter":1.,"millimeters":1.,"cm":10.,"centimeter":10.,
           "m":1000.,"meter":1000.,"meters":1000.,"in":25.4,"inch":25.4,"inches":25.4,
           "ft":304.8,"feet":304.8}
C4_HANDLES = ('1D','1F','20','25','27','29','2A','B6','C3','148','152','1FB','1FD','1FE',
              '276','28C','29C','2AC','2B3')
C4_MAP_SHA256 = 'A0AB7DFEE336919EC5AAF49B4C7D56ACC708165ED525CD07230CC065397F5339'

def first(obj: object, *names: str):
    if not isinstance(obj,dict): return None
    for area in (obj,obj.get('Data'),obj.get('Traits')):
        if isinstance(area,dict):
            keys={str(k).lower():v for k,v in area.items()}
            for n in names:
                if n.lower() in keys:return keys[n.lower()]
    return None

def point(p):
    if isinstance(p,(list,tuple)) and len(p)>=2:
        try: x,y=float(p[0]),float(p[1])
        except (ValueError,TypeError):return None
    elif isinstance(p,dict):
        v=first(p,'point','vertex','position')
        if v is not None:return point(v)
        try:x,y=float(first(p,'x')),float(first(p,'y'))
        except (ValueError,TypeError):return None
    else:return None
    return (x,y) if math.isfinite(x) and math.isfinite(y) else None

def dist(a,b):return math.hypot(a[0]-b[0],a[1]-b[1])

def inspect_hatch(e,scale,threshold):
    loops=first(e,'boundary_loops','boundaries','loops','polygons')
    if not isinstance(loops,list):return {'status':'unsupported','reason':'missing_boundary_loops','loop_count':0,'loops':[]}
    reports=[];errors=[];min_mm=None;short_total=0;bulge_total=0;line_total=0
    for j,loop in enumerate(loops,1):
        loopspec=loop if isinstance(loop,dict) else {'kind':'points','vertices':loop}
        kind=str(loopspec.get('kind','')).lower()
        edges=[];issues=[];nonzero_bulges=0
        if isinstance(loopspec.get('curves'),list):
            curves=loopspec['curves']
            for i,c in enumerate(curves,1):
                if not isinstance(c,dict):issues.append('invalid_curve_record');continue
                t=str(first(c,'Type','DxfType','kind','curve_type') or '').upper()
                if t not in ('LINE','LINESEGMENT2D'):issues.append(f'edge_{i}_nonlinear_{t or "UNKNOWN"}')
                a,b=point(first(c,'StartPoint','start','p1')),point(first(c,'EndPoint','end','p2'))
                if a is None or b is None:issues.append(f'edge_{i}_missing_coordinate');continue
                edges.append((a,b))
            for i in range(len(edges)):
                if dist(edges[i][1],edges[(i+1)%len(edges)][0])>1e-8:
                    issues.append(f'edge_{i+1}_disconnected')
        elif isinstance(loopspec.get('vertices'),list) or isinstance(loopspec.get('points'),list):
            verts=loopspec.get('vertices',loopspec.get('points'))
            pts=[]
            for i,v in enumerate(verts,1):
                pp=point(v)
                if pp is None:issues.append(f'vertex_{i}_invalid');continue
                pts.append(pp)
                if isinstance(v,dict):
                    try:bulge=float(first(v,'bulge') or 0)
                    except (TypeError,ValueError):bulge=float('nan')
                    if not math.isfinite(bulge) or abs(bulge)>1e-12:
                        nonzero_bulges+=1;issues.append(f'vertex_{i}_nonzero_bulge')
            if len(pts)>3 and dist(pts[0],pts[-1])<=1e-8:pts=pts[:-1]
            if len(pts)<3:issues.append('fewer_than_3_vertices')
            edges=[(pts[i],pts[(i+1)%len(pts)]) for i in range(len(pts))]
        else:issues.append('unrecognized_loop_geometry')
        if len(edges)<3:issues.append('fewer_than_3_edges')
        shorts=[]
        for i,(a,b) in enumerate(edges,1):
            d=dist(a,b)*scale
            line_total+=1
            if min_mm is None or d<min_mm:min_mm=d
            if d<=threshold:
                short_total+=1;shorts.append({'edge':i,'length_mm':round(d,8)})
        bulge_total+=nonzero_bulges
        reports.append({'loop':j,'kind':kind or 'unspecified','edges':len(edges),
                        'short_edges':shorts,'invalidities':sorted(set(issues))})
        errors.extend(issues)
    if not loops:errors.append('no_loops')
    status='unsupported' if errors else ('short_edge' if short_total else 'eligible_preflight')
    return {'status':status,'loop_count':len(loops),'total_edges':line_total,
            'short_edge_count':short_total,'curved_polyline_vertex_count':bulge_total,
            'minimum_edge_mm':round(min_mm,8) if min_mm is not None else None,
            'loops':reports,'reasons':sorted(set(errors))}

def inspect_dimension(e,scale,threshold):
    kind=str(first(e,'dimension_kind','kind') or '').lower()
    refs=first(e,'reference_lines','extension_lines','witness_lines')
    defs=first(e,'definition_points','defpoints')
    lengths=[];basis='none';roles=[]
    if isinstance(refs,list):
        basis='explicit_reference_lines'
        for r in refs:
            a,b=point(first(r,'start','p1')),point(first(r,'end','p2'))
            if a and b:lengths.append(dist(a,b)*scale)
    if isinstance(defs,list):
        roles=[str(first(d,'role','name') or '') for d in defs]
        if not lengths:
            pts={r.lower():point(first(d,'point','position') or d) for r,d in zip(roles,defs)}
            def get(key):
                for k,p in pts.items():
                    if key in k:return p
                return None
            a,b,c=get('xline1'),get('xline2'),get('dimline')
            if a and b and c:
                vx,vy=b[0]-a[0],b[1]-a[1];ln=math.hypot(vx,vy)
                if ln>1e-12:
                    nx,ny=-vy/ln,vx/ln
                    h=(c[0]-a[0])*nx+(c[1]-a[1])*ny
                    lengths=[abs(h)*scale]*2;basis='derived_linear_witness_lines'
    shorts=[round(x,8) for x in lengths if x<=threshold]
    if kind and ('radius' in kind or 'radial' in kind or 'diameter' in kind):status='unsupported_dimension_kind'
    elif shorts:status='short_reference'
    elif len(lengths)<2:status='insufficient_reference_data'
    else:status='eligible_preflight'
    return {'status':status,'dimension_kind':kind,'reference_count':len(lengths),
            'minimum_reference_mm':round(min(lengths),8) if lengths else None,
            'short_reference_lengths_mm':shorts,'geometry_basis':basis,'definition_roles':roles}

def analyze(src:Path,tolerance_mm:float,historical:bool=False):
    raw=src.read_bytes();sha=hashlib.sha256(raw).hexdigest().upper()
    root=json.loads(raw.decode('utf-8-sig'))
    if not isinstance(root,dict):raise ValueError('MAP root must be object')
    units=str(first(root,'units') or 'mm').strip().lower()
    if units not in UNIT_MM:raise ValueError(f'Unknown source units {units!r}; fail closed, no lengths analyzed')
    scale=UNIT_MM[units]
    entries=[]
    for key in ('entities','objects','items','texts','curves','fills','dimensions'):
        v=root.get(key)
        if isinstance(v,list):entries.extend(v)
    bytype=Counter();out=[];seen=set()
    for idx,e in enumerate(entries,1):
        if not isinstance(e,dict):continue
        handle=str(first(e,'Handle','source_id','id') or f'INDEX_{idx}').upper()
        t=str(first(e,'DxfType','type','entity_type','kind') or '').upper()
        bytype[t]+=1
        if t in ('HATCH','FILL'):
            result=inspect_hatch(e,scale,tolerance_mm)
        elif t in ('DIMENSION','DIM'):
            result=inspect_dimension(e,scale,tolerance_mm)
        else:continue
        if handle in seen:result['duplicate_handle']=True
        seen.add(handle)
        out.append({'handle':handle,'dxf_type':t,**result})
    stats=Counter(x['status'] for x in out)
    report={'schema':'yangtools-map-diagnostic/1.0','mode':'read_only_preflight_not_revit_runtime',
            'source_file':src.name,'source_sha256':sha,'units':units,'tolerance_mm':tolerance_mm,
            'source_entries':len(entries),'by_dxf_type':dict(sorted(bytype.items())),
            'hatch_count':bytype['HATCH']+bytype['FILL'],'dimension_count':bytype['DIMENSION']+bytype['DIM'],
            'status_counts':dict(stats),'items':out,'no_coordinates_exported':True}
    if historical:
        ids={x['handle']:x for x in out}
        report['historical_C4_comparison']={
            'expected_original_map_sha256':C4_MAP_SHA256,'source_sha_matches_historical':sha==C4_MAP_SHA256,
            '19_failed_fills_found':[h for h in C4_HANDLES if h in ids],
            '19_failed_fills_missing':[h for h in C4_HANDLES if h not in ids],
            'dimension_1249_found':'1249' in ids,
            'warning':'Matching Handle alone does not prove original sample identity; SHA mismatch must not be treated as same original MAP'}
    return report

def main():
    p=argparse.ArgumentParser(description='只读分析 YangCAD MAP 的 HATCH/Dimension，默认不输出坐标')
    p.add_argument('map',type=Path)
    p.add_argument('--output',type=Path,required=True,help='报告保存路径；不能覆盖原 MAP')
    p.add_argument('--tolerance-mm',type=float,default=0.7803686370624999,help='使用目标 Revit 环境实测值；默认 QA 2025')
    p.add_argument('--expect-sha256',type=str,help='可选：源 MAP 期望 SHA，不一致直接停止')
    p.add_argument('--historical-c4',action='store_true',help='显示历史 19 HATCH 与 1249 是否存在')
    args=p.parse_args()
    if not (math.isfinite(args.tolerance_mm) and args.tolerance_mm>0):p.error('invalid tolerance')
    if args.map.resolve()==args.output.resolve():p.error('cannot overwrite source MAP')
    if args.expect_sha256:
        sha=hashlib.sha256(args.map.read_bytes()).hexdigest()
        if sha.lower()!=args.expect_sha256.lower():p.error(f'MAP SHA mismatch: {sha.upper()}')
    report=analyze(args.map,args.tolerance_mm,args.historical_c4)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'source_sha256':report['source_sha256'],'hatches':report['hatch_count'],
                      'dimensions':report['dimension_count'],'status_counts':report['status_counts'],
                      'report':str(args.output)},ensure_ascii=False))

if __name__=='__main__': main()
