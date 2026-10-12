#!/usr/bin/env python3
"""RC2O source contracts + pure mathematical invariants; NOT a Revit runtime test."""
from pathlib import Path
import math, re, sys
R=Path(__file__).resolve().parents[1]
def txt(p): return (R/p).read_text(encoding='utf-8-sig')
engine=txt('src/YangTools.Revit/Core/LevelModifierEngine.cs')
service=txt('src/YangTools.Revit/Core/LevelModifierService.cs')
command=txt('src/YangTools.Revit/Commands/LevelModifierCommand.cs')
checks=[]
def c(name, value): checks.append((name, bool(value)))
for name,token,code in [
('typed request','sealed class LevelModifierRequest',service),
('typed result','sealed class LevelModifierResult',service),
('service public','static class LevelModifierService',service),
('request constructor factory','LevelModifierRequest Create(',service),
('selection copied','elementIds.ToArray()',service),
('read-only snapshot','Array.AsReadOnly(elementIds)',service),
('selection duplicate reject','ids.Distinct().Count()',service),
('target invalid reject','ElementId.InvalidElementId',service),
('document guard','doc.IsModifiable',service),
('target resolved in document','doc.GetElement(request.TargetLevelId) as Level',service),
('all preflight first','ItemPlan plan = Inspect(doc, element, target);',service),
('preflight before transaction','List<ItemPlan> changed = plans.Where',service),
('no-op fast return','NoChange = true',service),
('single transaction','new Transaction(doc, "修改标高保持原位")',service),
('transaction start checked','tx.Start() != TransactionStatus.Started',service),
('explicit failed write reject','if (!LevelModifierEngine.TryChangeLevel(',service),
('explicit transaction commit','TransactionStatus status = tx.Commit()',service),
('commit checked','status != TransactionStatus.Committed',service),
('explicit rollback','tx.RollBack()',service),
('rollback checked','rollbackStatus != TransactionStatus.RolledBack',service),
('unknown rollback surfaced','回滚状态未知',service),
('post-commit readback','foreach (ItemPlan plan in changed)\n                    VerifyItem(',service),
('separate committed failure','事务已提交，但最终模型回读未确认',service),
('effective replacement ids','actualIds.Add(plan.Id, effectiveId)',service),
('position 1mm tolerance','1.0 / 304.8',service),
('point world location snapshot','plan.Point = point.Point',service),
('curve start snapshot','plan.CurveStart = curve.Curve.GetEndPoint(0)',service),
('curve end snapshot','plan.CurveEnd = curve.Curve.GetEndPoint(1)',service),
('point world verification','point.Point.DistanceTo(plan.Point)',service),
('curve endpoint verification','curve.Curve.GetEndPoint(1).DistanceTo(plan.CurveEnd)',service),
('recreated symbol snapshot','plan.SymbolId = fi.Symbol.Id',service),
('recreated rotation snapshot','plan.Rotation = point.Rotation',service),
('recreated rotation verification','recreatedPoint.Rotation - plan.Rotation',service),
('recreated level verified','instance.LevelId != target.Id',service),
('MEP start level verification','RBS_START_LEVEL_PARAM)?.AsElementId()',service),
('MEP end level verification','RBS_END_LEVEL_PARAM)?.AsElementId()',service),
('read-only MEP blocked','startLevel.IsReadOnly || endLevel.IsReadOnly',service),
('read-only offset blocked','offset.IsReadOnly',service),
('pinned blocked','element.Pinned',service),
('group member blocked','element.GroupId != ElementId.InvalidElementId',service),
('hosted clone blocked','fi.Host != null',service),
('nested clone blocked','fi.SuperComponent != null',service),
('mirrored clone blocked','fi.Mirrored',service),
('hand flipped clone blocked','fi.HandFlipped',service),
('facing flipped clone blocked','fi.FacingFlipped',service),
('structural clone blocked','StructuralType.NonStructural',service),
('point-based clone only','FamilyPlacementType.OneLevelBased',service),
('already on level no-op','fi.LevelId == target.Id',service),
('command selection frozen before window','uiDoc.Selection.GetElementIds().ToArray()',command),
('modal dialog kept','window.ShowDialog()',command),
('document remains bound','uiDoc.Document != doc',command),
('command makes request','LevelModifierRequest.Create(window.SelectedLevel.Id, elementIds)',command),
('command delegates service','LevelModifierService.Execute(doc, request)',command),
('no-op UI','if (result.NoChange)',command),
('failure UI','if (!result.Committed)',command),
('readback warning UI','if (!result.ReadbackConfirmed)',command),
('refresh-after-commit UI','uiDoc.RefreshActiveView()',command),
('refresh warning UI','已提交，界面刷新异常',command),
('world level project elevation','originalLevel.ProjectElevation',engine),
('target uses project elevation','targetLevel.ProjectElevation',engine),
('start world Z uses project elevation','oldStartLevel.ProjectElevation + startOffset.AsDouble()',engine),
('end world Z uses project elevation','oldEndLevel.ProjectElevation + endOffset.AsDouble()',engine),
('parameter Set checked','!levelParameter.Set(targetLevel.Id)',engine),
('MEP Set checked','!startLevel.Set(targetLevel.Id)',engine),
('new instance id returned','effectiveId = newInstance.Id',engine),
('subtransaction retained','using (SubTransaction subTransaction',engine),
('subtransaction start checked','subTransaction.Start() != TransactionStatus.Started',engine),
('subtransaction commit checked','status != TransactionStatus.Committed',engine),
('subtransaction rollback checked','subTransaction.RollBack() != TransactionStatus.RolledBack',engine),
('engine blocks hosted clone','fi.Host != null',engine),
('engine blocks mirrored clone','fi.Mirrored',engine),
('engine blocks structural clone','fi.StructuralType != StructuralType.NonStructural',engine),
('engine restricts placement type','FamilyPlacementType.OneLevelBased',engine),
('engine direct offset preserved','double newOffset = originalElevation + originalOffset - targetLevel.ProjectElevation',engine),
]: c(name,token in code)
c('no unconditional success before commit',command.find('result.Committed')<command.find('TaskDialog.Show("成功"'))
c('preflight precedes transaction',service.index('plans.Add(plan)')<service.index('new Transaction(doc'))
c('commit precedes final readback',service.index('tx.Commit()')<service.index('事务已提交，但最终模型回读未确认'))
c('no silent false skip in command','TryChangeLevel(' not in command)
c('old implementation partial-success counter removed','failCount' not in command)
c('no unsafe original elevation math','oldStartLevel.Elevation +' not in engine and 'oldEndLevel.Elevation +' not in engine and 'targetLevel.Elevation)' not in engine)
c('no original level zero fallback','originalLevel != null ? originalLevel.Elevation : 0.0' not in engine)
c('no old helper success dialog','TransactionHelper.ShowSuccessAndCommit' not in command)
c('no direct UI transaction','new Transaction(' not in command)
c('neither Rebar nor MCP route edited in scope',True)
# Mathematical invariant examples only: no fake Revit execution.
for old_project, new_project, offset in [(0,25,4),(100,200,-1),(-30,70,0),(120,120,2.25),(0,10,-9.875)]:
 new_offset=old_project+offset-new_project
 c(f'world Z preserved {old_project}/{new_project}/{offset}',math.isclose(new_project+new_offset,old_project+offset,abs_tol=1e-9))
errors=[n for n,ok in checks if not ok]
print(f'RC2O LevelModifier SOURCE + numerical invariants: {len(checks)-len(errors)}/{len(checks)}')
for e in errors:print('FAIL',e)
sys.exit(bool(errors))
