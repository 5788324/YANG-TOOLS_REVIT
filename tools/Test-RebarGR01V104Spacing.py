import json, math
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
checks=[]
def add(name,ok): checks.append({'name':name,'ok':bool(ok)})
def text(rel): return (ROOT/rel).read_text(encoding='utf-8-sig',errors='replace')

planner=text('src/YangTools.Revit/Core/Rebar/RebarGradientSpacingPlanner.cs')
models=text('src/YangTools.Revit/Core/Rebar/RebarGradientModels.cs')
setmodels=text('src/YangTools.Revit/Core/Rebar/RebarGradientSetModels.cs')
service=text('src/YangTools.Revit/Core/Rebar/RebarGradientSetService.cs')
xaml=text('src/YangTools.Revit/UI/RebarGradientSetWindow.xaml')
server=text('McpServer/server.py')

# Product strategy / compatibility
add('V104 uniform strategy marker','AutoSpacingStrategy = "UniformWithinMax"' in planner)
add('V104 ceil interval planner','Math.Ceiling((input.LengthMm - NumericToleranceMm) / input.MaxSpacingMm)' in planner)
add('V104 actual spacing length over count','double spacing = input.LengthMm / intervalCount' in planner)
add('V104 hard max guard','spacing > input.MaxSpacingMm + NumericToleranceMm' in planner)
add('V104 no legacy regular search','REGULAR_SPACING_NOT_FOUND' not in planner and 'MaxSearchIterations' not in planner)
add('V104 no active tail threshold','remainder / candidate' not in planner)
add('V104 old wire fields retained','SpacingStepMm { get; set; } = 5.0' in models and 'MinTailRatio { get; set; } = 0.75' in models)
add('V104 result strategy visible','SpacingStrategy { get; set; } = "UniformWithinMax"' in models)
add('V104 zone mode visible','SpacingMode { get; set; }' in models and 'SpacingMode = spacingMode' in planner)
add('V104 legacy fields not fingerprint determinants','F(request.SpacingStepMm)' not in service and 'F(request.MinTailRatio)' not in service)
add('V104 fingerprint strategy marker','GR01.V1.2|UNIFORM_WITHIN_MAX|' in service)
add('V104 GUI explains uniform policy','自动：均匀分配且实际间距 ≤ 最大间距' in xaml)
add('V104 MCP explains uniform policy','UniformWithinMax' in server and 'ceil(length/max_spacing)' in server)

# Manual spacing remains explicit and may leave tail.
add('V104 manual branch first','if (manual)' in planner and 'BuildWithCandidate' in planner)
add('V104 manual tail retained','spacings.Add(remainder)' in planner and 'ManualActualSpacing' in planner)
add('V104 manual max validation','INVALID_MANUAL_SPACING' in planner)

# Bar limit before station expansion.
add('V104 projected bar precheck','projectedBars' in planner and 'BAR_LIMIT_EXCEEDED' in planner)

# Independent math reference.
def uniform(L, max_spacing):
    n=max(1, math.ceil((L-1e-6)/max_spacing))
    return n, L/n

def manual(L, spacing):
    n=math.floor((L+1e-6)/spacing)
    r=L-n*spacing
    if abs(r)<=1e-6:
        return n, spacing, 0.0
    return n+1, spacing, r

n100,s100=uniform(29130.046,100)
add('V104 production case max100 intervals',n100==292)
add('V104 production case max100 actual',abs(s100-99.76043150684932)<1e-9)
add('V104 production case max100 bars',n100+1==293)

n200,s200=uniform(29130.046,200)
add('V104 production case max200 intervals',n200==146)
add('V104 production case max200 actual',abs(s200-199.52086301369863)<1e-9)
add('V104 production case max200 bars',n200+1==147)

nm,sm,tail=manual(29130.046,100)
add('V104 manual100 intervals',nm==292)
add('V104 manual100 tail',abs(tail-30.046)<1e-6)
add('V104 manual100 bars',nm+1==293)

zones=[(1000,100),(4250,200),(2000,100),(4300,200),(1000,100)]
counts=[]
sp=[]
for L,M in zones:
    n,s=uniform(L,M); counts.append(n); sp.append(s)
add('V104 five zone interval count',sum(counts)==84)
add('V104 five zone bar count',sum(counts)+1==85)
add('V104 five zone Z2 actual',counts[1]==22 and abs(sp[1]-4250/22)<1e-9)
add('V104 five zone Z4 actual',counts[3]==22 and abs(sp[3]-4300/22)<1e-9)
add('V104 all actual <= max',all(s <= M+1e-9 for s,(_,M) in zip(sp,zones)))

failed=[c for c in checks if not c['ok']]
report={'checks':checks,'summary':{'passed':len(checks)-len(failed),'failed':len(failed),'total':len(checks)}}
(ROOT/'tools/rebar-gr01-v1-0-4-spacing-static-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
for c in checks: print(('PASS' if c['ok'] else 'FAIL')+' - '+c['name'])
print(f"SUMMARY {len(checks)-len(failed)}/{len(checks)} PASS")
raise SystemExit(1 if failed else 0)
