#!/usr/bin/env python3
"""RC3J geometry fix source-only invariants. NOT a Revit runtime or C# compilation test."""
from pathlib import Path
import json
root=Path(__file__).resolve().parents[1]
cmd=root/'src/YangTools.Revit/Commands'
core=(root/'src/YangTools.Revit/Core/DetailLineCleanup.cs').read_text(encoding='utf8')
merge=(cmd/'MergeCollinearDetailLinesCommand.cs').read_text(encoding='utf8')
overlap=(cmd/'RemoveOverlappingDetailLinesCommand.cs').read_text(encoding='utf8')
region=(cmd/'SetSelectedFilledRegionInvisibleBoundaryCommand.cs').read_text(encoding='utf8')
checks={
 'shared conservative geometry core': 'internal static class DetailLineCleanup' in core,
 'tolerance 0.1mm': '0.1 / 304.8' in core,
 'cross-checked perpendicular entire endpoints': all(s in core for s in ('DistanceToAxis(b.A, a)', 'DistanceToAxis(b.B, a)', 'DistanceToAxis(a.A, b)', 'DistanceToAxis(a.B, b)')),
 'reject nontransitive drift': 'members.Any(s => !Aligned(keeper, s))' in core,
 'strict positive overlap': 'Math.Min(a1, b1) - Math.Max(a0, b0) > Tolerance' in core,
 'reject branches and loops': 'degrees.Count(x => x == 1) != 2' in core and 'degrees.Any(x => x > 2)' in core,
 'reject overlap in endpoint-only chains': 'PositiveOverlap(group[i], group[j])' in core,
 'overlap connected components': 'Components(lines, PositiveOverlap)' in core,
 'continue reject ambiguous geometry': 'unsafeCount += group.Count' in core,
 'retain original curve id': 'keep.Element.SetGeometryCurve(target, false)' in core,
 'do not recreate detail curve': 'NewDetailCurve(' not in core+merge+overlap,
 'guard dimension reference across doc': 'new FilteredElementCollector(doc).OfClass(typeof(Dimension))' in core and 'dimension.References' in core,
 'guard dependents': 'GetDependentElements(null)' in core,
 'delete unintended dependent rollback': 'removed.Count != 1' in core and 'tx.RollBack()' in core,
 'post edit geometry readback': 'doc.Regenerate()' in core and 'Revit 调整了目标线段' in core,
 'single transaction': 'using (var tx = new Transaction(doc, txName))' in core,
 'merge fallback picking': 'Selection.PickObjects(ObjectType.Element' in merge,
 'overlap fallback picking': 'Selection.PickObjects(ObjectType.Element' in overlap,
 'filled fallback picking': 'Selection.PickObjects(ObjectType.Element' in region,
 'revit built-in invisible category': 'BuiltInCategory.OST_InvisibleLines' in region,
 'valid filled line style only': 'GetValidLineStyleIdsForFilledRegion(doc)' in region and 'IsValidLineStyleIdForFilledRegion' in region,
 'filled existing object mutation': 'filled.SetLineStyleId(possible[0])' in region,
 'filled transaction rollback': 'tx.RollBack()' in region,
 'UI names retained': all('buttonText: "'+name+'"' in v for name,v in [('直线合并',merge),('重叠清理',overlap),('隐藏边界',region)]),
 '36 ribbon count': sum(x.read_text(encoding='utf8').count('[RibbonButton') for x in cmd.glob('*.cs'))==36,
}
failed=[n for n,ok in checks.items() if not ok]
print(json.dumps({'suite':'RC3J geometry fix SOURCE ONLY','total':len(checks),'passed':len(checks)-len(failed),'failed':failed,'build':'NOT_TESTED','runtime':'NOT_TESTED'},ensure_ascii=False,indent=2))
raise SystemExit(bool(failed))
