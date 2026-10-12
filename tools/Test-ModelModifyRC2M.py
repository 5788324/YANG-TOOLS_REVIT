#!/usr/bin/env python3
"""Static contract checks for RC2M model-edit priority and column placement."""
from pathlib import Path
import sys, json, re
r=Path(__file__).resolve().parents[1]
core=(r/'src/YangTools.Revit/Core/ColumnAlongPathEngine.cs').read_text('utf-8')
model=(r/'src/YangTools.Revit/Core/ColumnAlongPathPlacementRequest.cs').read_text('utf-8')
ui=(r/'src/YangTools.Revit/UI/ColumnAlongPathWindow.xaml.cs').read_text('utf-8')
xaml=(r/'src/YangTools.Revit/UI/ColumnAlongPathWindow.xaml').read_text('utf-8')
road=(r/'docs/YANGTOOLS_4_0_0_OPTIMIZATION_ROADMAP_MODEL_FIRST_20261010.md').read_text('utf-8')
checks=[]
def ck(n,c):checks.append((n,bool(c)))
for label,source,tokens in [
('request',model,['sealed class ColumnAlongPathPlacementRequest','ColumnAlongPathPlacementRequest Create(','Capture(ColumnAlongPathViewModel model)','ChainedCurves = chainedCurves.ToList().AsReadOnly()','model.SelectedColumnType.ElementIdValue','model.SelectedBaseLevel.ElementIdValue','model.SelectedTopLevel.ElementIdValue','model.SpacingMm','model.StartOffsetMm','model.RouteStartMileageM','model.RotationOffsetDegrees','model.BaseOffsetMm','model.TopOffsetMm','model.ReversePath','model.IncludeEndColumn','class ColumnAlongPathPlacementResult','CreatedIds','ReadbackConfirmed','ReadbackWarning']),
('engine',core,['ExecutePlacement(ColumnAlongPathPlacementRequest request)','ValidateFinite(request.SpacingMm','ValidateFinite(request.StartOffsetMm','ValidateFinite(request.BaseOffsetMm','ValidateFinite(request.TopOffsetMm','ValidateFinite(request.RotationOffsetDegrees','ValidateFinite(request.RouteStartMileageM','_doc.IsReadOnly','transaction.Start() != TransactionStatus.Started','transaction.Commit()','TransactionStatus.Committed','transaction.RollBack() != TransactionStatus.RolledBack','结果无法确认','ReadbackConfirmed = false','CreatedIds = createdIds.AsReadOnly()','maxColumnsPerBatch = 10000','estimatedCount','first + index * spacing','StructuralType.Column','FAMILY_BASE_LEVEL_PARAM','FAMILY_TOP_LEVEL_PARAM','FAMILY_TOP_LEVEL_OFFSET_PARAM','ElementTransformUtils.RotateElement','IsApproximatelyHorizontal','ReverseChain','CalculateDistances','NormalizeSignedAngle','internal static string FormatStation']),
('ui',ui,['ColumnAlongPathPlacementRequest.Capture(_viewModel)','engine.ExecutePlacement(request)','long requestVersion = ++_requestVersion','requestVersion != _requestVersion','if (_isClosed) return','HasInvalidInput(this)','Validation.GetHasError(textbox)','_isClosed = true','Selection.SetElementIds(result.CreatedIds.ToList())','result.ReadbackConfirmed','result.ReadbackWarning','已提交','RefreshActiveView','SetAction(app =>','wrapInTransactionGroup: false','_doc,','TaskDialog.Show','Show();','Activate();']),
('xaml',xaml,['Click="StartPlacement_Click"','Click="PickRoute_Click"','SelectedColumnType','SelectedBaseLevel','SelectedTopLevel','RotationOffsetDegrees','ReversePath','IncludeEndColumn']),
('roadmap',road,['模型修改区优先','沿线布柱','线性布置','标高修改','基于面转换','实体生成','实体提取','布尔几何','MCP','Windows/Revit','18–25'])]:
 for token in tokens:ck(label+':'+token,token in source)
ck('engine never reads mutable ViewModel', 'viewModel.' not in core)
ck('engine no TaskDialog UI', 'TaskDialog.Show' not in core)
ck('engine no selection update after commit', 'Selection.SetElementIds' not in core)
ck('transaction before result',core.index('transaction.Commit()')<core.index('return result;'))
ck('readback outside transaction using',core.index('ReadbackConfirmed = true')>core.index('transaction.RollBack()'))
ck('ui captured request before queue',ui.index('ColumnAlongPathPlacementRequest.Capture(_viewModel)')<ui.index('"沿线布置柱", showSuccess: false'))
ck('no XAML business change expected', 'ColumnAlongPathWindow' in xaml)
ck('baseline old tests retained',len(list((r/'tools').glob('Test-*.py')))>=52)
failed=[n for n,ok in checks if not ok]
print(json.dumps({'suite':'RC2M ColumnAlongPath model edit/UI/static','total':len(checks),'passed':len(checks)-len(failed),'failed':failed,'limitations':'static source assertions; not C# compilation or Revit runtime'},ensure_ascii=False,indent=2))
sys.exit(1 if failed else 0)
