import json, math
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
checks=[]
def add(name, ok): checks.append({'name':name,'ok':bool(ok)})
def text(rel): return (ROOT/rel).read_text(encoding='utf-8-sig', errors='replace')

models=text('src/YangTools.Revit/Core/Rebar/RebarGradientModels.cs')
planner=text('src/YangTools.Revit/Core/Rebar/RebarGradientSpacingPlanner.cs')
cap=text('src/YangTools.Revit/Core/Rebar/RebarGradientCapabilityService.cs')
server_probe=text('src/YangTools.Revit/Core/Rebar/RebarGradientServerCreateProbeService.cs')
broker = (ROOT / "src/YangTools.Revit/Mcp/Handlers/RevitToolRouter.cs").read_text(encoding="utf-8-sig") + "\n" + (ROOT / "src/YangTools.Revit/Mcp/Handlers/RebarToolHandler.cs").read_text(encoding="utf-8-sig")
server=text('McpServer/server.py')

add('GR01 zone input model','class RebarGradientSpacingZoneInput' in models)
add('GR01 spacing request model','class RebarGradientSpacingPlanRequest' in models)
add('GR01 spacing result model','class RebarGradientSpacingPlanResult' in models)
add('GR01 capability report model','class RebarGradientCapabilityReport' in models)
add('GR01 server probe model','class RebarGradientServerCreateProbeRequest' in models and 'class RebarGradientServerCreateProbeResult' in models)
add('GR01 schema','"GR01.P0.1"' in models)
add('GR01 planner pure class','static class RebarGradientSpacingPlanner' in planner)
add('GR01 legacy 5mm field retained','SpacingStepMm { get; set; } = 5.0' in models)
add('GR01 legacy tail ratio field retained','MinTailRatio { get; set; } = 0.75' in models)
add('GR01 station merge default','StationMergeToleranceMm { get; set; } = 0.5' in models)
add('GR01 max zones','MaxZones { get; set; } = 50' in models)
add('GR01 max bars','MaxPhysicalBars { get; set; } = 1000' in models)
add('GR01 uniform within max strategy','AutoSpacingStrategy = "UniformWithinMax"' in planner and 'Math.Ceiling' in planner)
add('GR01 no legacy regular failure','REGULAR_SPACING_NOT_FOUND' not in planner and 'MaxSearchIterations' not in planner)
add('GR01 auto hard max guarantee','spacing > input.MaxSpacingMm + NumericToleranceMm' in planner)
add('GR01 manual spacing validation','ManualActualSpacingMm' in planner and 'INVALID_MANUAL_SPACING' in planner)
add('GR01 zone length mismatch','ZONE_LENGTH_MISMATCH' in planner)
add('GR01 station merge','MergeStations' in planner and 'MergedStationCount' in planner)
add('GR01 bar hard limit','BAR_LIMIT_EXCEEDED' in planner)
add('GR01 changed false planner','ChangedModel = false' in planner)
add('GR01 capability reflection','BindingFlags.Public | BindingFlags.Static' in cap and 'CreateFreeFormOverloads' in cap)
add('GR01 aligned guid probe','FreeFormAlignedServerGuid' in cap)
add('GR01 surface guid probe','FreeFormSurfaceServerGuid' in cap)
add('GR01 custom spacing is probed not assumed','SetLayoutAsCustomSpacingPresent' in cap and 'SetLayoutAsCustomSpacing' in cap)
add('GR01 external service registration probe','RebarUpdateService' in cap and 'GetRegisteredServerIds' in cap)
add('GR01 capability read only','new Transaction(' not in cap and 'ChangedModel = false' in cap)
add('GR01 server skeleton create','CreateFreeForm' in server_probe and 'serverGuid' in server_probe)
add('GR01 server probe rollback','tx.RollBack()' in server_probe and 'ChangedModel = false' in server_probe)
add('GR01 server probe disclaimer','不能据此宣布 Native Surface/Aligned' in server_probe)
add('GR01 broker capability command','case "probe_gradient_rebar_capabilities"' in broker)
add('GR01 broker preview command','case "preview_gradient_spacing_plan"' in broker)
add('GR01 broker server probe command','case "probe_gradient_server_create"' in broker)
add('GR01 MCP capability tool','async def probe_gradient_rebar_capabilities(' in server)
add('GR01 MCP preview tool','async def preview_gradient_spacing_plan(' in server)
add('GR01 MCP server probe tool','async def probe_gradient_server_create(' in server)
add('GR01 no ribbon expansion','渐变钢筋集' not in text('src/YangTools.Revit/Core/RibbonBuilder.cs'))

# Independent reference math: V1.0.4 UniformWithinMax.
def plan_zone(L, Smax):
    n=max(1, math.ceil((L-1e-6)/Smax))
    s=L/n
    return [s]*n

z1=plan_zone(1000,100)
z2=plan_zone(4250,200)
z3=plan_zone(2000,100)
z4=plan_zone(4300,200)
z5=plan_zone(1000,100)
add('GR01 reference Z1 100x10',len(z1)==10 and all(abs(x-100)<1e-6 for x in z1))
add('GR01 reference Z2 uniform <=200',len(z2)==22 and abs(z2[0]-4250/22)<1e-6 and max(z2)<=200+1e-6)
add('GR01 reference Z3 100x20',len(z3)==20 and all(abs(x-100)<1e-6 for x in z3))
add('GR01 reference Z4 uniform <=200',len(z4)==22 and abs(z4[0]-4300/22)<1e-6 and max(z4)<=200+1e-6)
add('GR01 reference Z5 100x10',len(z5)==10 and all(abs(x-100)<1e-6 for x in z5))
intervals=sum(map(len,[z1,z2,z3,z4,z5]))
add('GR01 reference total 85 bars',intervals==84 and intervals+1==85)
add('GR01 reference all <= max',max(z1)<=100+1e-6 and max(z2)<=200+1e-6 and max(z3)<=100+1e-6 and max(z4)<=200+1e-6 and max(z5)<=100+1e-6)

failed=[c for c in checks if not c['ok']]
report={'checks':checks,'summary':{'passed':len(checks)-len(failed),'failed':len(failed),'total':len(checks)}}
(ROOT/'tools/rebar-gr01-p0-static-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
for c in checks: print(('PASS' if c['ok'] else 'FAIL')+' - '+c['name'])
print(f"SUMMARY {len(checks)-len(failed)}/{len(checks)} PASS")
raise SystemExit(1 if failed else 0)
