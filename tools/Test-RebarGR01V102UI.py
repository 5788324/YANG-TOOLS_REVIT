import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
checks=[]
def add(name, ok): checks.append({'name': name, 'ok': bool(ok)})
def text(rel): return (ROOT/rel).read_text(encoding='utf-8-sig', errors='replace')

xaml=text('src/YangTools.Revit/UI/RebarGradientSetWindow.xaml')
code=text('src/YangTools.Revit/UI/RebarGradientSetWindow.xaml.cs')
vm=text('src/YangTools.Revit/Models/RebarGradientSetViewModel.cs')
service=text('src/YangTools.Revit/Core/Rebar/RebarGradientSetService.cs')

try:
    ET.fromstring(xaml)
    xaml_ok=True
except Exception:
    xaml_ok=False
add('GR01 V102 XAML parse', xaml_ok)

# Mouse wheel nested-scroll fix.
add('GR01 V102 named main scroll','x:Name="MainScrollViewer"' in xaml)
add('GR01 V102 zone wheel hook','PreviewMouseWheel="ZoneGrid_PreviewMouseWheel"' in xaml)
add('GR01 V102 wheel handler','ZoneGrid_PreviewMouseWheel' in code)
add('GR01 V102 inner scroll detection','DG_ScrollViewer' in code and 'ScrollableHeight' in code)
add('GR01 V102 outer scroll fallback','MainScrollViewer.ScrollToVerticalOffset' in code and 'e.Handled = true' in code)
add('GR01 V102 visual fallback','VisualTreeHelper.GetChildrenCount' in code)

# Direction semantics: path supplies axis/range, controls supply business direction.
add('GR01 V102 direction UI','布置方向' in xaml and 'DirectionText' in xaml)
add('GR01 V102 direction by controls','Rebar {_startControlId.Value} → Rebar {_endControlId.Value}' in code)
add('GR01 V102 zone zero from start','Zone[0] 从起始侧开始' in code)
add('GR01 V102 no path direction UI','+Z ↑' not in code and '-Z ↓' not in code)
add('GR01 V102 path semantics help','ModelLine / Edge 只提供分布轴线和可用范围' in xaml)
add('GR01 V102 core path reversed retained','PathReversed = endProjection < startProjection' in service)

# Path basis total vs effective distribution length.
add('GR01 V102 effective length UI','有效长度' in xaml and 'EffectiveLengthText' in xaml)
add('GR01 V102 basis total label','基准总长' in code)
add('GR01 V102 model line raw length cached','_pathReferenceLengthMm = lengthMm' in code)
add('GR01 V102 effective length from result','_lastKnownDistributionLengthMm = result.DistributionLengthMm' in code)
add('GR01 V102 effective display refresh','RefreshPathDisplay();' in code and 'EffectiveLengthText' in code)

# Auto remainder row readability.
add('GR01 V102 length mode header','Header="长度模式"' in xaml)
add('GR01 V102 length mode text','LengthModeText' in xaml and 'LengthModeText' in vm)
add('GR01 V102 pending auto length','LengthDisplayText' in vm and '待计算' in vm)
add('GR01 V102 auto length editing disabled','IsEnabled="{Binding IsFixedLength}"' in xaml)
add('GR01 V102 units visible','长度(mm)' in xaml and '最大间距(mm)' in xaml and '实际间距(mm)' in xaml)

# Immediate validation and safer action buttons.
add('GR01 V102 validation text','InputValidationText' in xaml and 'InputValidationText' in vm)
add('GR01 V102 validate zones','ValidateZoneInputs' in code)
add('GR01 V102 over allocation immediate','固定区段合计' in code and '已超过有效长度' in code)
add('GR01 V102 preview readiness','IsPreviewInputReady' in code)
add('GR01 V102 preview disabled until ready','PreviewButton.IsEnabled = previewReady' in code)
add('GR01 V102 preview tooltip reason','PreviewButton.ToolTip' in code)
add('GR01 V102 CS0165 reason initialized','string reason = string.Empty;' in code and 'IsPreviewInputReady(out reason)' in code)
add('GR01 V102 max spacing live guard','最大间距必须大于 0' in code and 'UpdateSourceTrigger=PropertyChanged' in xaml)
add('GR01 V102 swap ids and refresh', '_startControlId = _endControlId;' in code and '_endControlId = startId;' in code and 'RefreshPathDisplay();' in code)

# Small usability cleanup.
add('GR01 V102 insert after selected','InsertZoneAfterSelection' in code and 'selectedIndex + 1' in code)
add('GR01 V102 title no internal stage','GR01 V1 · Explicit ManagedFreeForm · Revit 2025' not in xaml)
add('GR01 V102 user subtitle','根据首末控制钢筋生成渐变钢筋集' in xaml)
add('GR01 V102 managed disclosure kept','ManagedGeometry' in xaml and 'Native ToCover' in xaml)

# Core geometry stays untouched by this polish.
add('GR01 V102 no GUI CreateFreeForm','CreateFreeForm' not in code)
add('GR01 V102 no GUI planner','RebarGradientSpacingPlanner.Calculate' not in code)
add('GR01 V102 no GUI transaction','new Transaction(' not in code)
add('GR01 V102 one core CreateFreeForm',service.count('RevitRebar.CreateFreeForm') == 1)

failed=[c for c in checks if not c['ok']]
report={'checks':checks,'summary':{'passed':len(checks)-len(failed),'failed':len(failed),'total':len(checks)}}
(ROOT/'tools/rebar-gr01-v1-0-2-ui-static-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
for c in checks: print(('PASS' if c['ok'] else 'FAIL')+' - '+c['name'])
print(f"SUMMARY {len(checks)-len(failed)}/{len(checks)} PASS")
raise SystemExit(1 if failed else 0)
