import json, math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
checks=[]
def add(name,ok): checks.append({'name':name,'ok':bool(ok)})
def text(rel): return (ROOT/rel).read_text(encoding='utf-8-sig',errors='replace')

models=text('src/YangTools.Revit/Core/Rebar/RebarGradientSetModels.cs')
service=text('src/YangTools.Revit/Core/Rebar/RebarGradientSetService.cs')
p0models=text('src/YangTools.Revit/Core/Rebar/RebarGradientModels.cs')
planner=text('src/YangTools.Revit/Core/Rebar/RebarGradientSpacingPlanner.cs')
broker = (ROOT / "src/YangTools.Revit/Mcp/Handlers/RevitToolRouter.cs").read_text(encoding="utf-8-sig") + "\n" + (ROOT / "src/YangTools.Revit/Mcp/Handlers/RebarToolHandler.cs").read_text(encoding="utf-8-sig")
server=text('McpServer/server.py')
ribbon=text('src/YangTools.Revit/Core/RibbonBuilder.cs')
gui_command=text('src/YangTools.Revit/Commands/RebarGradientSetCommand.cs')
gui_window=text('src/YangTools.Revit/UI/RebarGradientSetWindow.xaml.cs')

# Models / scope
add('GR01 V1 path model','class RebarGradientDistributionPathInput' in models)
add('GR01 V1 request model','class RebarGradientSetRequest' in models)
add('GR01 V1 result model','class RebarGradientSetResult' in models)
add('GR01 V1 curve summary','class RebarGradientCurveSummary' in models)
add('GR01 V1 bar sample','class RebarGradientBarSample' in models)
add('GR01 V1 schema','"GR01.V1.1"' in models)
add('GR01 V1 engine explicit','EngineUsed { get; set; } = "ExplicitManagedFreeForm"' in models)
add('GR01 V1 managed constraint','ConstraintMode { get; set; } = "ManagedGeometry"' in models)
add('GR01 V1 native cover false','NativeToCover { get; set; } = false' in models)
add('GR01 V1 expected fingerprint','ExpectedPlanFingerprint' in models)
add('GR01 V1 dryrun default','DryRun { get; set; } = true' in models)
add('GR01 V1 max bars 1000','MaxPhysicalBars { get; set; } = 1000' in models)
add('GR01 V1 max curves 100','MaxCurvesPerBar { get; set; } = 100' in models)

# Service / controls
add('GR01 V1 service class','static class RebarGradientSetService' in service)
add('GR01 V1 control same element reject','SAME_CONTROL_REBAR' in service)
add('GR01 V1 single position guard','CONTROL_NOT_SINGLE_POSITION' in service and 'NumberOfBarPositions != 1' in service)
add('GR01 V1 same host guard','CONTROL_HOST_MISMATCH' in service)
add('GR01 V1 same bar type guard','CONTROL_BAR_TYPE_MISMATCH' in service)
add('GR01 V1 same style guard','CONTROL_STYLE_MISMATCH' in service)
add('GR01 V1 hook reject','CONTROL_HOOK_UNSUPPORTED' in service)
add('GR01 V1 termination reject','CONTROL_TERMINATION_UNSUPPORTED' in service)
add('GR01 V1 coupler reject','CONTROL_COUPLER_UNSUPPORTED' in service)
add('GR01 V1 transformed centerline','GetTransformedCenterlineCurves' in service and 'RawCenterline' not in service)
add('GR01 V1 line arc only','UNSUPPORTED_CONTROL_CURVE' in service and 'curve is Line' in service and 'curve is Arc' in service)
add('GR01 V1 continuity guard','CONTROL_CURVES_NOT_CONTINUOUS' in service)
add('GR01 V1 reverse matching','EndControlReversed' in service and 'ReverseSequence' in service and 'TryMatchScore' in service)

# Path / Station
add('GR01 V1 ModelLine path','kind.Equals("ModelLine"' in service and 'CurveElement' in service)
add('GR01 V1 Edge path','kind.Equals("Edge"' in service and 'ParseFromStableRepresentation' in service and 'GetGeometryObjectFromReference' in service)
add('GR01 V1 straight path guard','PATH_NOT_STRAIGHT' in service and 'is not Line' in service)
add('GR01 V1 station projection','DotProduct(path.UnitDirection)' in service)
add('GR01 V1 control path extent guard','CONTROL_OUTSIDE_PATH_EXTENT' in service)
add('GR01 V1 path reversed semantic','PathReversed = endProjection < startProjection' in service)
add('GR01 V1 zones reuse planner','RebarGradientSpacingPlanner.Calculate' in service)

# Geometry interpolation
add('GR01 V1 t by station','spacing.StationsMm[i] / spacing.DistributionLengthMm' in service)
add('GR01 V1 XYZ lerp','private static XYZ Lerp' in service)
add('GR01 V1 line interpolation','INTERPOLATED_LINE_TOO_SHORT' in service and 'Line.CreateBound' in service)
add('GR01 V1 arc interpolation','Arc.Create(p0, p1, pm)' in service and 'INTERPOLATED_ARC_DEGENERATE' in service)
add('GR01 V1 no silent arc line fallback','INTERPOLATED_ARC_INVALID' in service and '静默' not in service)
add('GR01 V1 interpolated continuity','INTERPOLATED_CURVES_NOT_CONTINUOUS' in service)
add('GR01 V1 first middle last sample','FirstBarSample' in service and 'MiddleBarSample' in service and 'LastBarSample' in service)

# Create / transaction / one set
add('GR01 V1 explicit single create','RevitRebar.CreateFreeForm' in service and service.count('RevitRebar.CreateFreeForm')==1)
add('GR01 V1 style writeback','accessor.RebarStyle = state.Style' in service)
add('GR01 V1 one transaction','new Transaction(doc, "YangTools GR01 创建渐变钢筋集")' in service)
add('GR01 V1 rollback','tx.RollBack()' in service and 'FAILED_ROLLEDBACK' in service)
add('GR01 V1 no control delete','doc.Delete' not in service)
add('GR01 V1 count readback','READBACK_BAR_COUNT_MISMATCH' in service and 'NumberOfBarPositions != state.PlannedBars.Count' in service)
add('GR01 V1 unconstrained readback','READBACK_CONSTRAINT_MODE_MISMATCH' in service and 'IsUnconstrained' in service)
add('GR01 V1 no server readback','READBACK_SERVER_UNEXPECTED' in service and 'HasValidServer' in service)
add('GR01 V1 host readback','READBACK_HOST_MISMATCH' in service)
add('GR01 V1 type readback','READBACK_BAR_TYPE_MISMATCH' in service)
add('GR01 V1 style readback','READBACK_STYLE_MISMATCH' in service)
add('GR01 V1 all physical bar geometry readback','for (int i = 0; i < state.PlannedBars.Count; i++)' in service and 'READBACK_GEOMETRY_MISMATCH' in service)
add('GR01 V1 control unchanged','VerifyControlsUnchanged' in service and 'CONTROL_CHANGED_DURING_CREATE' in service)
add('GR01 V1 max deviation','MaxReadbackDeviationMm' in service)

# Fingerprint / dryrun / logs
add('GR01 V1 sha fingerprint','SHA256.Create()' in service and 'PlanFingerprint' in service)
add('GR01 V1 stale preview reject','PLAN_FINGERPRINT_MISMATCH' in service)
add('GR01 V1 revalidate before create','PLAN_CHANGED_BEFORE_CREATE' in service and 'RebuildFingerprintOnly' in service)
add('GR01 V1 dryrun no transaction return','if (request.DryRun)' in service and 'DRYRUN_VALIDATED' in service)
add('GR01 V1 product logging','DiagnosticLog.WriteProductOperation' in service and 'COMMITTED' in service and 'REJECTED' in service)

# MCP bridge
add('GR01 V1 broker preview command','case "preview_gradient_rebar_set"' in broker)
add('GR01 V1 broker create command','case "create_gradient_rebar_set"' in broker)
add('GR01 V1 broker distribution path','RebarGradientDistributionPathInput' in broker and 'stableReference' in broker)
add('GR01 V1 broker zones','request.Zones.Add(new RebarGradientSpacingZoneInput' in broker)
add('GR01 V1 MCP preview tool','async def preview_gradient_rebar_set(' in server)
add('GR01 V1 MCP create tool','async def create_gradient_rebar_set(' in server)
add('GR01 V1 MCP default dryrun','dry_run: bool = True' in server)
add('GR01 V1 MCP fingerprint handoff','expected_plan_fingerprint' in server)
add('GR01 V1 MCP ModelLine Edge','ModelLine / Edge' in server)
add('GR01 V1 GUI ribbon grouped', '渐变钢筋集' in gui_command and 'groupName: "创建钢筋"' in gui_command and 'order: 100' in gui_command)
add('GR01 V1 GUI uses shared service', 'RebarGradientSetService.Execute' in gui_window and 'RevitRebar.CreateFreeForm' not in gui_window and 'new Transaction(' not in gui_window)

# V1.0.4 spacing planner contract
add('GR01 V1 legacy spacing fields retained','SpacingStepMm { get; set; } = 5.0' in p0models and 'MinTailRatio { get; set; } = 0.75' in p0models)
add('GR01 V1 uniform within max','AutoSpacingStrategy = "UniformWithinMax"' in planner and 'Math.Ceiling' in planner)
add('GR01 V1 no regular search failure','REGULAR_SPACING_NOT_FOUND' not in planner and 'MaxSearchIterations' not in planner)
add('GR01 V1 station dedupe','MergeStations' in planner)

# Independent V1.0.4 spacing math sanity.
def plan_zone(L,Smax):
    n=max(1, math.ceil((L-1e-6)/Smax))
    return [L/n]*n
z=[plan_zone(1000,100),plan_zone(4250,200),plan_zone(2000,100),plan_zone(4300,200),plan_zone(1000,100)]
add('GR01 V1 uniform 85 bars',sum(len(x) for x in z)+1==85)
add('GR01 V1 Z2 uniform',len(z[1])==22 and abs(z[1][0]-4250/22)<1e-9)
add('GR01 V1 Z4 uniform',len(z[3])==22 and abs(z[3][0]-4300/22)<1e-9)
add('GR01 V1 all spacing <= max',max(z[0])<=100+1e-9 and max(z[1])<=200+1e-9 and max(z[2])<=100+1e-9 and max(z[3])<=200+1e-9 and max(z[4])<=100+1e-9)

# Independent interpolation sanity: start/end exact and midpoint XYZ linear.
a=(0.,0.,0.); b=(100.,200.,300.)
def lerp(a,b,t): return tuple(a[i]+(b[i]-a[i])*t for i in range(3))
add('GR01 V1 interpolation t0 exact',lerp(a,b,0)==a)
add('GR01 V1 interpolation t1 exact',lerp(a,b,1)==b)
add('GR01 V1 interpolation t05 XYZ',lerp(a,b,.5)==(50.,100.,150.))

failed=[c for c in checks if not c['ok']]
report={'checks':checks,'summary':{'passed':len(checks)-len(failed),'failed':len(failed),'total':len(checks)}}
(ROOT/'tools/rebar-gr01-v1-static-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
for c in checks: print(('PASS' if c['ok'] else 'FAIL')+' - '+c['name'])
print(f"SUMMARY {len(checks)-len(failed)}/{len(checks)} PASS")
raise SystemExit(1 if failed else 0)
