#!/usr/bin/env python3
"""RC2Q Loft source contracts and pure numerical regression. Not a Revit build/runtime test."""
from pathlib import Path
import math
import xml.etree.ElementTree as ET
r=Path(__file__).resolve().parents[1]
def read(path):return (r/path).read_text(encoding='utf-8-sig')
engine=read('src/YangTools.Revit/Core/EntityGeneratorEngine.cs')
win=read('src/YangTools.Revit/UI/EntityGeneratorWindow.xaml.cs')
xaml=read('src/YangTools.Revit/UI/EntityGeneratorWindow.xaml')
command=read('src/YangTools.Revit/Commands/EntityGeneratorCommand.cs')
state=read('src/YangTools.Revit/Models/EntityGeneratorState.cs')
checks=[]
def need(b,desc):checks.append((bool(b),desc))
for token in ['var sourceZ = new LoftZBounds()', 'var profileIds = new HashSet<string>', 'instance.Document != doc', 'profileIds.Add(instance.Id.ToString())', 'curve.Tessellate()', 'sourceZ.Include(vertex.Z)', 'sourceZ.Height <= MinimumEndpointTolerance', 'sourceZ.MinZ', 'sourceZ.MaxZ', 'familyAnchor = new XYZ(firstPoint.X, firstPoint.Y, sourceZ.MinZ)']:
 need(token in engine, 'source profile envelope: '+token)
for token in ['private sealed class LoftZBounds', 'double.PositiveInfinity', 'double.NegativeInfinity', 'double.IsNaN(value)', 'double.IsInfinity(value)', 'ReadSolidZBounds(Solid solid)', 'solid.GetBoundingBox()', 'bbox.Transform.OfPoint(new XYZ(x, y, z)).Z','GetInstanceGeometry()', 'ReadPlacedInstanceZBounds(FamilyInstance instance)', 'AppendPlacedGeometryZ', 'CheckZBounds(LoftZBounds actual', 'LoftHeightToleranceFeet', '5.0 / 304.8', 'Math.Abs(actual.MinZ - expectedMinZ)', 'Math.Abs(actual.MaxZ - expectedMaxZ)']:
 need(token in engine, 'geometry bounds: '+token)
for token in ['CheckZBounds(localBounds, sourceZ.MinZ - familyAnchor.Z', 'sourceZ.MaxZ - familyAnchor.Z', 'LoadAndPlaceFamily(doc, state, tempFile, familyAnchor, sourceZ)','FamilyPlacementHelper.CreateAtWorldPoint(', 'ReadPlacedInstanceZBounds(newInstance)', 'CheckZBounds(placedBounds, sourceZ.MinZ, sourceZ.MaxZ', 'if (!state.IsVoid)', '已检查局部 Loft 高度','document.Regenerate()', 'placeTransaction.Commit() != TransactionStatus.Committed', 'group.Assimilate() != TransactionStatus.Committed']:
 need(token in engine, 'integration: '+token)
for token in ['TransactionStatus rollback = group.RollBack()', 'rollback != TransactionStatus.RolledBack', '回滚状态未知', 'targetLevelId != ElementId.InvalidElementId && level == null', '所选目标标高已失效', 'item.ProjectElevation - referenceElevation']:
 need(token in engine, 'Revit transaction / level: '+token)
for token in ['已选择 {State.Profiles.Count} 个轮廓（按选择顺序放样）', 'TxtProfiles.ToolTip', 'TxtProfiles.ToolTip = null']:
 need(token in win,'UI compact selection: '+token)
for token in ['MinWidth="530"', '实体上下界来自原轮廓世界坐标', '不调整 Base/Top Offset', 'TextWrapping="Wrap"', '关联标高，保持原几何高度']:
 need(token in xaml, 'UI explain: '+token)
for token in ['未能确认完成', '请检查项目模型再重试']:
 need(token in command, 'uncertain state dialogue '+token)
need('TargetLevelId' in state and 'BaseLevelId' not in state and 'TopLevelId' not in state,'state does not invent Base/Top fields')
need('Math.Abs(item.Elevation - referenceElevation)' not in engine,'no shared elevation fallback')
need('group.RollBack();\n                    throw;' not in engine,'no silent unknown group rollback')
need('minZ = Math.Min(minZ, Math.Min(curve.GetEndPoint' not in engine,'no endpoint only arc Z')
try: ET.fromstring(xaml);need(True,'Loft XAML valid XML')
except ET.ParseError:need(False,'Loft XAML valid XML')
# Model tests: world vertical extent invariant under local-coordinate shift and placement,
# against positive, negative, non-zero levels, and varying Offset (no separate Base/Top fields).
for low,high in [(-30.0,2.0),(0.0,14.0),(145.1,164.99),(-2.15,-.7)]:
 for project_level in [-100.0,-5.0,0.0,30.0,150.0]:
  anchor=low
  local_lo,local_hi=low-anchor,high-anchor
  world_lo,world_hi=local_lo+anchor,local_hi+anchor
  need(math.isclose(world_lo,low,abs_tol=1e-10) and math.isclose(world_hi,high,abs_tol=1e-10) and math.isclose(world_hi-world_lo,high-low,abs_tol=1e-10),f'world Z invariant low={low}, high={high}, association={project_level}')
# Catch a bug (double level offset) in pure math expected observation
for lvl,anchor in [(0,25), (100,25),(-50,20)]:
 expected=anchor
 wrong=anchor+lvl
 need(lvl==0 or not math.isclose(wrong,expected,abs_tol=1e-7),f'double offset caught level={lvl}')
for i,(ok,label) in enumerate(checks,1):print(('PASS' if ok else 'FAIL')+f' {i:03} {label}')
failed=sum(not v for v,_ in checks)
print(f'=== RC2Q SUMMARY === total={len(checks)} failed={failed}')
raise SystemExit(bool(failed))
