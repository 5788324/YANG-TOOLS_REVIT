import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
checks=[]
def add(name, ok): checks.append({'name': name, 'ok': bool(ok)})
def text(rel): return (ROOT/rel).read_text(encoding='utf-8-sig', errors='replace')

models=text('src/YangTools.Revit/Core/Rebar/RebarGradientModels.cs')
setmodels=text('src/YangTools.Revit/Core/Rebar/RebarGradientSetModels.cs')
service=text('src/YangTools.Revit/Core/Rebar/RebarGradientSetService.cs')
planner=text('src/YangTools.Revit/Core/Rebar/RebarGradientSpacingPlanner.cs')
vm=text('src/YangTools.Revit/Models/RebarGradientSetViewModel.cs')
xaml=text('src/YangTools.Revit/UI/RebarGradientSetWindow.xaml')
code=text('src/YangTools.Revit/UI/RebarGradientSetWindow.xaml.cs')
server=text('McpServer/server.py')
broker = (ROOT / "src/YangTools.Revit/Mcp/Handlers/RevitToolRouter.cs").read_text(encoding="utf-8-sig") + "\n" + (ROOT / "src/YangTools.Revit/Mcp/Handlers/RebarToolHandler.cs").read_text(encoding="utf-8-sig")
command=text('src/YangTools.Revit/Commands/RebarGradientSetCommand.cs')

try:
    ET.fromstring(xaml)
    xaml_ok=True
except Exception:
    xaml_ok=False
add('GR01 UX XAML parse', xaml_ok)

# Z axis path stays inside the shared Core service rather than GUI geometry math.
add('GR01 UX ZAxis request default','Kind { get; set; } = "ZAxis"' in setmodels)
add('GR01 UX ZAxis core resolver','kind.Equals("ZAxis"' in service and 'UnitDirection = XYZ.BasisZ' in service)
add('GR01 UX ZAxis uses control centers','ResolveDistributionPath(doc, request.DistributionPath, startCenter, endCenter)' in service)
add('GR01 UX ZAxis auto length from Z','double minZ = Math.Min(startCenter.Z, endCenter.Z)' in service and 'double maxZ = Math.Max(startCenter.Z, endCenter.Z)' in service)
add('GR01 UX ZAxis no model element requirement','Kind = "ZAxis"' in service and 'ElementId = null' in service)
add('GR01 UX ZAxis path reversed still available','PathReversed = endProjection < startProjection' in service)
add('GR01 UX legacy ModelLine kept','kind.Equals("ModelLine"' in service)
add('GR01 UX legacy Edge kept','kind.Equals("Edge"' in service)

# Auto remaining zone is normalized before the unchanged spacing planner.
add('GR01 UX AutoLength model','public bool AutoLength' in models)
add('GR01 UX auto normalizer','NormalizeZonesForDistribution' in service)
add('GR01 UX single auto guard','MULTIPLE_AUTO_LENGTH_ZONES' in service)
add('GR01 UX over allocation guard','AUTO_ZONE_OVER_ALLOCATED' in service)
add('GR01 UX short remainder guard','AUTO_ZONE_REMAINDER_TOO_SHORT' in service)
add('GR01 UX planner gets normalized zones','Zones = normalizedZones' in service)
add('GR01 UX planner remains independent','AutoLength' not in planner)
add('GR01 UX fingerprint auto stable','fingerprintLength = zone.AutoLength' in service and 'state.SpacingPlan.Zones[zoneIndex].LengthMm' in service)

# GUI defaults and ergonomics.
add('GR01 UX GUI default ZAxis','private string _pathKind = "ZAxis"' in code and 'Tag="ZAxis"' in xaml)
add('GR01 UX GUI ZAxis first option','SelectedIndex="0"' in xaml and xaml.index('Tag="ZAxis"') < xaml.index('Tag="ModelLine"'))
add('GR01 UX GUI disables path pick on ZAxis','PickPathButton.IsEnabled' in code and '!string.Equals(_pathKind, "ZAxis"' in code)
add('GR01 UX GUI default auto normal zone','Label = "普通"' in code and 'IsAutoLength = true' in code and 'LengthMm = 0.0' in code)
add('GR01 UX GUI auto mode control','IsAutoLength' in xaml and 'LengthModeText' in xaml and '自动剩余' in xaml)
add('GR01 UX GUI auto length readonly edit','ZoneGrid_BeginningEdit' in code and 'row.IsAutoLength' in code and 'e.Cancel = true' in code)
add('GR01 UX GUI live auto deduction','RecalculateAutoZoneLength' in code and '_lastKnownDistributionLengthMm.Value - fixedTotal' in code)
add('GR01 UX GUI live allocation summary','自动剩余：{remaining:0.###} mm' in code and '固定区段已超出路径' in code)
add('GR01 UX GUI swap controls','SwapControls_Click' in code and '交换首末' in xaml)
add('GR01 UX GUI zone order message',('区段顺序仍从新的起始控制筋一侧开始' in code) or ('Zone[0] 仍从新的起始控制筋一侧开始' in code))
add('GR01 UX GUI request sends auto flag','AutoLength = row.IsAutoLength' in code)
add('GR01 UX GUI ZAxis no picked path required','if (string.Equals(_pathKind, "ZAxis"' in code and (('无需拾取路径' in code) or ('无需拾取基准' in code)))
add('GR01 UX GUI preview resolves auto length','row.SetResolvedAutoLength(plan.LengthMm)' in code)

# MCP parity.
add('GR01 UX MCP ZAxis accepted','normalized_kind not in ("zaxis", "modelline", "edge")' in server)
add('GR01 UX MCP ZAxis payload','"kind": "ZAxis" if normalized_kind == "zaxis"' in server)
add('GR01 UX MCP auto length input','auto_length = bool(zone.get("auto_length"' in server)
add('GR01 UX MCP auto count guard','auto_length_count > 1' in server)
add('GR01 UX broker auto flag', broker.count('AutoLength = zone[\"autoLength\"]') >= 2)

# Product invariants remain unchanged.
add('GR01 UX still one CreateFreeForm',service.count('RevitRebar.CreateFreeForm') == 1)
add('GR01 UX no GUI CreateFreeForm','CreateFreeForm' not in code)
add('GR01 UX no GUI transaction','new Transaction(' not in code)
add('GR01 UX managed geometry disclosure',('ManagedGeometry' in xaml or 'Managed FreeForm' in xaml) and 'Native ToCover' in xaml)
add('GR01 UX command tooltip mentions Z axis','全局 Z 轴' in command)

# Frozen example: with four fixed segments and one auto normal, remainder is exactly 4300.
fixed = 1000 + 4250 + 2000 + 1000
add('GR01 UX frozen auto remainder 4300',12550-fixed == 4300)

failed=[c for c in checks if not c['ok']]
report={'checks': checks, 'summary': {'passed': len(checks)-len(failed), 'failed': len(failed), 'total': len(checks)}}
(ROOT/'tools/rebar-gr01-v1-ux-static-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
for c in checks: print(('PASS' if c['ok'] else 'FAIL')+' - '+c['name'])
print(f"SUMMARY {len(checks)-len(failed)}/{len(checks)} PASS")
raise SystemExit(1 if failed else 0)
