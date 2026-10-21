"""RC3G targeted compile API contract gate. Static only; not a Windows build."""
from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[1]
R=ROOT/'src'/'YangTools.Revit'/'Core'/'Rebar'
def read(name):return (R/name).read_text(encoding='utf-8')
checks={}
grad=read('RebarGradientSetService.cs'); insp=read('RebarInspectionService.cs'); rel=read('RebarRelationshipService.cs')
checks['2021 explicitly rejects GR01 create before Transaction.Start']=grad.index('FREEFORM_STYLE_API_UNSUPPORTED_REVIT2021')<grad.index('tx.Start()') and '#if REVIT2021' in grad
checks['2021 GR01 accessor setter guarded']=re.search(r'#if !REVIT2021\s+using \(RebarFreeFormAccessor accessor = created.GetFreeFormAccessor\(\)\)\s+\{\s+accessor.RebarStyle = state.Style;',grad) is not None
checks['2021 GR01 accessor getter guarded']=re.search(r'#if REVIT2021\s+throw new GradientValidationException\("CONTROL_STYLE_UNREADABLE_REVIT2021"',grad) is not None
checks['2021 GR01 readback prevents unsupported style read']=grad.count('FREEFORM_STYLE_API_UNSUPPORTED_REVIT2021')>=2
checks['2021 inspection does not fabricate FreeForm style']=re.search(r'#if REVIT2021\s+//.*\n\s*//.*\n\s*return "UNAVAILABLE_REVIT2021_FREEFORM_STYLE";',insp) is not None
checks['2026+ relationship scopes candidate to target.Id']='GetConstraintCandidatesForHandle(handle, target.Id)' in rel and '#if REVIT2026_OR_GREATER' in rel
checks['<=2025 relationship still uses original overload']='GetConstraintCandidatesForHandle(handle);' in rel and '#else' in rel
for name in ['RebarSurfaceVaryingCreateService.cs','RebarInspectionService.cs','RebarFaceDrivenCreateService.cs','RebarGradientSetService.cs']:
 s=read(name)
 checks[name+' no legacy suppressHooks named argument']='suppressHooks: false' not in s
 checks[name+' preserves 5-argument GetCenterlineCurves shape']='false, // Revit 2026+ renamed this argument suppressHooksAndCranks.' in s
checks['project remains multi-config 2021-2027']='2021;2022;2023;2024;2025;2026;2027' in (ROOT/'src'/'YangTools.Revit'/'YangTools.Revit.csproj').read_text(encoding='utf-8')
for name,ok in checks.items():
 print(('PASS' if ok else 'FAIL')+' '+name)
if not all(checks.values()):raise SystemExit(1)
print('RC3G MultiVersion API guard PASS',len(checks),'checks')
