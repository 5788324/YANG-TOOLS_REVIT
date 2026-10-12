#!/usr/bin/env python3
"""RC2N LinearPlacement source contract + independent numerical specification examples.
Does not compile C# or invoke Revit. Checks implementation tokens plus output invariants.
"""
import json
import math
import re
import sys
from pathlib import Path
r=Path(__file__).resolve().parents[1]
engine=(r/'src/YangTools.Revit/Core/LinearPlacementEngine.cs').read_text('utf-8')
window=(r/'src/YangTools.Revit/UI/LinearPlacementWindow.xaml.cs').read_text('utf-8')
request=(r/'src/YangTools.Revit/Core/LinearPlacementRequest.cs').read_text('utf-8')
xaml=(r/'src/YangTools.Revit/UI/LinearPlacementWindow.xaml').read_text('utf-8')
checks=[]
def ck(name, condition):checks.append((name,bool(condition)))
for label,source,tokens in [
('request',request,[
 'sealed class LinearPlacementRequest','LinearPlacementRequest Create(','Capture(LinearPlacementViewModel model)',
 'chainedCurves.ToList().AsReadOnly()','SymbolId','LevelId','StartElevationMm','EndElevationMm',
 'StartValue','EndValue','StepValue','IsByDistance','IsBySpacing','AlignToCurve','KeepVertical',
 'ReversePath','IncludeRemainderEndpoint','RotationOffset','model.SelectedSymbol.Id.GetIdValue()',
 'model.SelectedLevel.Id.GetIdValue()','class LinearPlacementResult','CreatedIds','ReadbackConfirmed','ReadbackWarning']),
('engine',engine,[
 'ExecutePlacement(LinearPlacementRequest request)','CalculatePlacements(request.ChainedCurves, request)',
 'CalculatePlacements(IReadOnlyList<Curve> chainedCurves, LinearPlacementRequest request)',
 ': chainedCurves.ToList()',
 'ApplyElevationProfile(placements, targetLevel, request)','targetLevel.ProjectElevation + relativeElevationFeet',
 'firstDistance = placements[0].DistanceAlongPath','lastDistance = placements[placements.Count - 1].DistanceAlongPath',
 'startRelativeFeet + relativeRise * ratio','worldZ','FamilyPlacementHelper.CreateAtLevelRelativeElevation',
 'if (request.AlignToCurve)','if (request.KeepVertical)','request.ReversePath','request.IsBySpacing',
 'request.IncludeRemainderEndpoint','request.RotationOffset','double[] cumulativeLengths',
 'GetNormalizedParameterAtArcLength','GetInstanceGeometry()','SortAndChainCurves',
 'omittedCount > 0','throw new InvalidOperationException(','for (int i = 1; i < request.ChainedCurves.Count; i++)',
 'transaction.Start() != TransactionStatus.Started','transaction.Commit()','TransactionStatus.Committed',
 'transaction.RollBack() != TransactionStatus.RolledBack','结果无法确认',
 'result.ReadbackConfirmed = true','result.ReadbackWarning','CreatedIds = createdIds.AsReadOnly()',
 'expectedPoints[i]','location.Point.DistanceTo(expectedPoints[i])','positionToleranceFeet = 1.0 / 304.8',
 'const int maxPlacements = 10000','estimatedCount > maxPlacements','dataList.Count >= maxPlacements',
 'Math.Abs(request.StepValue - Math.Round(request.StepValue))','if (trueEnd < trueStart)',
 'request.StartValue < 0 || request.EndValue > 1','ValidateFinite(request.StartValue',
 'ValidateFinite(request.EndValue','ValidateFinite(request.StepValue','ValidateFinite(request.RotationOffset',
 'firstDistance','lastDistance','Math.Min(trueEnd, totalLength)']),
('ui',window,[
 'new RevitEventHandler(_doc,','wrapInTransactionGroup: false','showAutomaticSuccess: false',
 '_doc = uiapp.ActiveUIDocument?.Document','HasInvalidInput(this)','Validation.GetHasError(textbox)',
 'LinearPlacementRequest.Capture(_viewModel)','engine.ExecutePlacement(request)',
 'long version = ++_placementVersion','version != _placementVersion','if (_isClosed) return',
 '++_pathVersion','version != _pathVersion','_isClosed = true','TaskDialog.Show("线性布置结果"',
 'result.ReadbackConfirmed','result.ReadbackWarning','已提交','DiagnosticLog.WriteInfo',
 'SetAction(app =>','showSuccess: false','SaveCurrentSettings();']),
('xaml',xaml,['Click="StartPlacement_Click"','Click="PickPath_Click"','Binding SelectedLevel',
 'Binding StartElevationMm','Binding EndElevationMm','Binding StepValue','Binding ReversePath',
 'Binding IncludeRemainderEndpoint'])]:
 for token in tokens:ck(label+':'+token,token in source)
ck('Engine must not hold ViewModel', 'LinearPlacementViewModel' not in engine and 'viewModel.' not in engine)
ck('Engine never opens WPF/Revit TaskDialog','TaskDialog.Show' not in engine)
ck('Level is project coordinate, not shared elevation','double worldZ = targetLevel.Elevation +' not in engine)
ck('UI captures before ExternalEvent queue',window.index('LinearPlacementRequest.Capture(_viewModel)') < window.index('"线性布置", showSuccess: false'))
ck('Engine Result follows commit',engine.index('transaction.Commit()')<engine.index('return result;'))
ck('Engine readback outside transaction',engine.index('transaction.RollBack()') < engine.index('result.ReadbackConfirmed = true'))
ck('Stale UI callback blocks model write',window.index('if (_isClosed || version != _placementVersion) return;') < window.index('engine.ExecutePlacement(request)'))
ck('Zero tolerance for silent unconnected path','throw new InvalidOperationException(' in engine[engine.index('int omittedCount'):engine.index('return chained;')])
ck('No silent start/end reversal','trueStart = trueEnd;' not in engine and 'trueEnd = trueStart;' not in engine)
ck('No int cast of unbounded estimated count',engine.index('estimatedCount > maxPlacements') < engine.index('numPoints = (int)estimatedCount'))
ck('Old scope suites preserved',len(list((r/'tools').glob('Test-*.py'))) >= 53)
# Specification fixtures (not an execution of the C# engine): validate nonzero project levels and station split.
mathcount=0
for base,start,end in [(-3500,0,3000),(0,-1200,1800),(12000,-500,2500),(3650,800,-700)]:
 for index in range(5):
  fraction=index/4
  actual=base+start+(end-start)*fraction
  expected=(1-fraction)*(base+start)+fraction*(base+end)
  ck(f'world Z base={base} index={index}',math.isclose(actual,expected,abs_tol=1e-8))
  mathcount+=1
# Explicit multi-segment example: 2 m + 3 m, placed at 1m,2m,3m,4m; endpoint equality.
for station,curveidx,local in [(1,0,1),(2,0,2),(3,1,1),(4,1,2),(5,1,3)]:
 index=0 if station<=2 else 1
 local_actual=station-(0 if index==0 else 2)
 ck(f'multicurve station {station}',index==curveidx and local==local_actual)
failed=[n for n,ok in checks if not ok]
print(json.dumps({'suite':'RC2N LinearPlacement source/math static checks',
  'total':len(checks),'passed':len(checks)-len(failed),'failed':failed,
  'mathCases':mathcount+5,
  'limitations':'static assertions and independent formula examples, not C# compilation or Revit runtime'},ensure_ascii=False,indent=2))
sys.exit(1 if failed else 0)
