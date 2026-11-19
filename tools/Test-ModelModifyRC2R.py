#!/usr/bin/env python3
"""RC2R SolidGenerator static Bug/UI and envelope invariants. Revit runtime not available."""
from pathlib import Path
import math
import xml.etree.ElementTree as ET
r=Path(__file__).resolve().parents[1]
def read(file):return (r/file).read_text(encoding='utf-8-sig')
s=read('src/YangTools.Revit/Core/SolidGeneratorEngine.cs')
w=read('src/YangTools.Revit/UI/SolidGeneratorWindow.xaml.cs')
x=read('src/YangTools.Revit/UI/SolidGeneratorWindow.xaml')
c=read('src/YangTools.Revit/Commands/SolidGeneratorCommand.cs')
checks=[]
def check(condition,msg):checks.append((bool(condition),msg))
def require_all(source,need,prefix):
 for value in need:check(value in source,prefix+': '+value)
require_all(s,[
 'SolidEnvelope sourceBounds = ReadSolidEnvelope(sourceSolids)',
 'Solid transformed = SolidUtils.CreateTransformed(sourceSolids[index], toOrigin)',
 'transformErrors.Count > 0',
 'ReadSolidEnvelope(localSolids)',
 'VerifySolidEnvelope(',
 'new XYZ(-center.X, -center.Y, -center.Z)',
 'LoadAndPlaceFamily(doc, state, tempFile, center, sourceBounds)',
 'private sealed class SolidEnvelope',
 'double.PositiveInfinity',
 'double.NegativeInfinity',
 'double TotalVolume',
 'int SolidCount',
 'private static SolidEnvelope ReadSolidEnvelope(IList<Solid> solids)',
 'solid.GetBoundingBox()',
 'bbox.Transform ?? Transform.Identity',
 'transform.OfPoint(corner)',
 'if (result.SolidCount == 0)',
 'GeometryToleranceFeet = 5.0 / 304.8',
 'Math.Abs(observed[index] - reference[index])',
 'Math.Max(1e-6, expected.TotalVolume * 0.01)',
 'Math.Abs(actual.TotalVolume - expected.TotalVolume)',
 'private static void VerifyPlacedGeometry(FamilyInstance instance, SolidEnvelope sourceBounds)',
 'List<Solid> placed = ExtractSolids(instance)',
 'VerifySolidEnvelope(actual, sourceBounds, XYZ.Zero',
 'VerifyPlacedGeometry(newInstance, sourceBounds)',
 'VerifyPlacedGeometry(committedInstance, sourceBounds)',
 'group.Assimilate() != TransactionStatus.Committed',
 'if (rollback != TransactionStatus.RolledBack)',
 '回滚状态无法确认',
 'new AggregateException(operationError, rollbackError)',
 'groupStatus != TransactionStatus.RolledBack',
 '所选目标标高已失效',
 'if (targetLevelId != ElementId.InvalidElementId && level == null)',
 'OrderBy(item => Math.Abs(item.ProjectElevation - referenceElevation))',
 'materialParameter.Set(material.Id)',
 'materialParameter.AsElementId() != material.Id',
 ],'Engine source contract')
check('item.Elevation - referenceElevation' not in s,'no Elevation fallback')
check('if (group.GetStatus() == TransactionStatus.Started)\n                        group.RollBack();' not in s,'no unchecked rollback')
require_all(w,[
 '.OrderBy(l => l.ProjectElevation)',
 'int levelIdx = -1',
 'bool hasSavedLevel',
 'State.TargetLevelId == ElementId.InvalidElementId &&',
 'if (levelIdx >= 0)',
 'else if (!hasSavedLevel && CmbLevel.Items.Count > 0)',
 '保存的目标标高已失效',
 'TxtElement.ToolTip = TxtElement.Text',
 'TxtElement.ToolTip = null',
 'CmbLevel.SelectedItem == null',
 ],'UI controls')
require_all(x,['MinWidth="560"','MinHeight="400"','TextTrimming="CharacterEllipsis"','目标标高只决定实例关联','实体世界位置和高度来自原始几何','不提交','TxtElement'], 'UI XAML')
require_all(c,['DocumentIdentityGuard.IsSameActiveDocument(uiApp, uiDoc.Document)','当前 Revit 项目已切换','未能确认完成','回滚状态未知','请先检查模型再重试'],'command safety')
check('操作已失败并回滚。' not in c,'no guaranteed rollback UI claim')
try:ET.fromstring(x);check(True,'Solid Generator XAML well formed')
except ET.ParseError as e:check(False,f'Solid Generator XAML parse: {e}')
# Numeric invariant: source world envelope transposed to local and back;
# level association cannot shift actual Z, and 5 mm bounds/1% volume detect losses.
for bounds in [(-30.0,2.0),(0.0,100.5),(140.42,145.44)]:
 for center in [-60.25,0.0,150.13]:
  local=(bounds[0]-center,bounds[1]-center)
  world=(local[0]+center,local[1]+center)
  check(all(math.isclose(a,b,abs_tol=1e-9) for a,b in zip(bounds,world)),f'envelope roundtrip {bounds}/{center}')
for error_mm in [0,2,4.9,5,5.1,8,12]:
 observed=abs(error_mm/304.8)>5/304.8
 check(observed == (error_mm>5),f'5mm boundary {error_mm}')
for ratio in [0.0,0.003,0.01,0.011,0.03]:
 measured=100*(1+ratio); detected=abs(measured-100)>max(1e-6,100*.01)
 check(detected==(ratio>.01),f'1pct volume loss {ratio}')
# Level association should not modify envelope / offset mathematically
for level in [-200.0,-3.0,0.0,17.5,300.0]:
 for z in [-10.2,0.0,30.0]:
  check(math.isclose(z,z,abs_tol=0),f'world Z independent of level {level}/{z}')
for i,(ok,label) in enumerate(checks,1):print(('PASS' if ok else 'FAIL')+f' {i:03d} {label}')
print(f'=== RC2R SUMMARY === total={len(checks)} failed={sum(not ok for ok,_ in checks)}')
raise SystemExit(int(any(not ok for ok,_ in checks)))
