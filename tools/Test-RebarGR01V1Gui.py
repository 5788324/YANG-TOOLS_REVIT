import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
checks=[]
def add(name,ok): checks.append({'name':name,'ok':bool(ok)})
def text(rel): return (ROOT/rel).read_text(encoding='utf-8-sig',errors='replace')

cmd=text('src/YangTools.Revit/Commands/RebarGradientSetCommand.cs')
xaml=text('src/YangTools.Revit/UI/RebarGradientSetWindow.xaml')
code=text('src/YangTools.Revit/UI/RebarGradientSetWindow.xaml.cs')
vm=text('src/YangTools.Revit/Models/RebarGradientSetViewModel.cs')
service=text('src/YangTools.Revit/Core/Rebar/RebarGradientSetService.cs')

# XAML is structurally parseable.
try:
    ET.fromstring(xaml)
    xaml_ok=True
except Exception:
    xaml_ok=False
add('GR01 GUI XAML parse', xaml_ok)

# Ribbon: one new dropdown item, not a new top-level group.
add('GR01 GUI command exists','class RebarGradientSetCommand' in cmd)
add('GR01 GUI steel panel','"钢筋工具区"' in cmd)
add('GR01 GUI grouped create','groupName: "创建钢筋"' in cmd)
add('GR01 GUI order 100','order: 100' in cmd)
add('GR01 GUI user title no stage tag','"渐变钢筋集"' in cmd and 'GR01 V1' not in cmd.split('[RibbonButton',1)[1].split(')]',1)[0])
add('GR01 GUI modeless registry','ModelessWindowRegistry.ShowOrActivate' in cmd)

# Window keeps theme/document/external-event safety.
add('GR01 GUI theme init','ThemeHelper.InitializeWithTheme(this, InitializeComponent)' in code)
add('GR01 GUI document binding','_document = uiapp.ActiveUIDocument.Document' in code)
add('GR01 GUI external event','ExternalEvent.Create(_handler)' in code)
add('GR01 GUI no outer transaction group','wrapInTransactionGroup: false' in code)
add('GR01 GUI pick via external event','uidoc.Selection.PickObject' in code and '_externalEvent.Raise()' in code)
add('GR01 GUI rebar selection filter','new RebarSelectionFilter()' in code)
add('GR01 GUI model line filter','StraightModelCurveSelectionFilter' in code and 'ModelCurve' in code and 'GeometryCurve is Line' in code)
add('GR01 GUI edge filter','StraightEdgeSelectionFilter' in code and 'ObjectType.Edge' in code and 'geometry is Edge edge && edge.AsCurve() is Line' in code)
add('GR01 GUI stable edge reference','ConvertToStableRepresentation' in code)

# Scope and UI controls.
add('GR01 GUI control pick labels','起始控制筋' in xaml and '结束控制筋' in xaml)
add('GR01 GUI path kind choices','模型线' in xaml and '模型 Edge' in xaml)
add('GR01 GUI zone grid','x:Name="ZoneGrid"' in xaml and 'ItemsSource="{Binding Zones}"' in xaml)
add('GR01 GUI normal quick add','+ 普通区' in xaml and 'AddNormalZone_Click' in xaml)
add('GR01 GUI dense quick add','+ 加密区' in xaml and 'AddDenseZone_Click' in xaml)
add('GR01 GUI reorder zones','MoveZoneUp_Click' in xaml and 'MoveZoneDown_Click' in xaml)
add('GR01 GUI editable max spacing','MaxSpacingMm' in xaml)
add('GR01 GUI optional manual spacing','ManualActualSpacingMm' in xaml)
add('GR01 GUI preview columns','PreviewMainSpacingMm' in xaml and 'PreviewTailSpacingMm' in xaml and 'PreviewIntervalCount' in xaml)
add('GR01 GUI managed geometry disclosure','ManagedGeometry' in xaml and 'Native ToCover' in xaml)

# Shared Request/Service/Result: no duplicate geometry engine in UI.
add('GR01 GUI preview uses service','RebarGradientSetService.Execute(app, request)' in code)
add('GR01 GUI preview dryrun','TryBuildRequest(dryRun: true' in code)
add('GR01 GUI create service','TryBuildRequest(dryRun: false' in code)
add('GR01 GUI fingerprint handoff','_lastPlanFingerprint' in code and 'ExpectedPlanFingerprint = expectedFingerprint' in code)
add('GR01 GUI expected control UIDs','ExpectedStartControlUniqueId = _startControlUniqueId' in code and 'ExpectedEndControlUniqueId = _endControlUniqueId' in code)
add('GR01 GUI path request reuse','new RebarGradientDistributionPathInput' in code)
add('GR01 GUI zone request reuse','new RebarGradientSpacingZoneInput' in code)
add('GR01 GUI no CreateFreeForm duplication','CreateFreeForm' not in code)
add('GR01 GUI no planner duplication','RebarGradientSpacingPlanner.Calculate' not in code)
add('GR01 GUI no transaction duplication','new Transaction(' not in code)
add('GR01 GUI no update expansion','update_gradient_rebar_set' not in code.lower())
add('GR01 GUI no 2027 expansion','REVIT2027' not in code and 'Morphed' not in code and 'CustomSpacing' not in code)

# Preview invalidation and safe create UX.
add('GR01 GUI input invalidates preview','InvalidatePreview' in code and 'InputChanged' in vm)
add('GR01 GUI create requires preview','请先执行“计算预览”' in code)
add('GR01 GUI service stale guard preserved','PLAN_FINGERPRINT_MISMATCH' in service and 'ExpectedPlanFingerprint' in code)
add('GR01 GUI create disables after commit','若要再次创建，请重新计算预览' in code)
add('GR01 GUI one set wording','1 个完整 Free Form Rebar Set' in code)
add('GR01 GUI undo wording','一次 Revit Undo' in code)

failed=[c for c in checks if not c['ok']]
report={'checks':checks,'summary':{'passed':len(checks)-len(failed),'failed':len(failed),'total':len(checks)}}
(ROOT/'tools/rebar-gr01-v1-gui-static-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
for c in checks: print(('PASS' if c['ok'] else 'FAIL')+' - '+c['name'])
print(f"SUMMARY {len(checks)-len(failed)}/{len(checks)} PASS")
raise SystemExit(1 if failed else 0)
