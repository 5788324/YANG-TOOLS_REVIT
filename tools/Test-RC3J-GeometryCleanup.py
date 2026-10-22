#!/usr/bin/env python3
"""Source-only checks, not Windows compilation or functional geometry testing."""
from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
s=root/'src/YangTools.Revit'
texts={name:(s/'Commands'/name).read_text(encoding='utf-8') for name in ('SetSelectedFilledRegionInvisibleBoundaryCommand.cs','RemoveOverlappingDetailLinesCommand.cs')}
a=texts['SetSelectedFilledRegionInvisibleBoundaryCommand.cs']; b=texts['RemoveOverlappingDetailLinesCommand.cs']; t=(s/'Core/TextLayoutMerger.cs').read_text(encoding='utf8')
checks={
 'text CS1628 out capture removed': 'double cutValue = cut;' in t and 'x.U < cutValue' in t and 'x.U >= cutValue' in t,
 'invisible region ribbon': '"填充隐形边界"' in a,
 'filled region API direct mutation': 'filled.SetLineStyleId(choices[0].Id)' in a,
 'filled valid style list': 'GetValidLineStyleIdsForFilledRegion(doc)' in a,
 'filled selection current view pinned group': all(x in a for x in ('GetElementIds()', 'OwnerViewId', 'x.Pinned', 'x.GroupId')),
 'filled fail closed style ambiguity': 'choices.Count != 1' in a,
 'filled preview transaction rollback': all(x in a for x in ('CommonButtons', 'new Transaction(', 'tx.RollBack()')),
 'overlap ribbon': '"清理重叠线段"' in b,
 'overlap selection only': 'uidoc.Selection.GetElementIds()' in b,
 'overlap DetailCurve Line only': 'DetailCurve element' in b and 'GeometryCurve is Line' in b,
 'overlap guard pinned group view style': all(x in b for x in ('element.Pinned','element.GroupId','element.OwnerViewId','candidate.StyleId.GetIdValue()')),
 'overlap real positive interval': 'segments[indices[next]].A < end - Tolerance' in b,
 'overlap preserve disjoint intervals': 'if (overlapping && component.Count > 1)' in b,
 'overlap prevent duplicates in groups': 'visited.Contains(j)' in b and '聚类不唯一' in b,
 'overlap short curve API': 'doc.Application.ShortCurveTolerance' in b,
 'overlap one transaction and rollback': all(x in b for x in ('new Transaction(doc', 'transaction.RollBack()','doc.Delete(source.Item.Id)')),
 'all 36 ribbon commands': sum(x.read_text(encoding='utf8').count('[RibbonButton') for x in (s/'Commands').glob('*.cs'))==36,
}
failed=[x for x,v in checks.items() if not v]
print(json.dumps({'checks':len(checks),'pass':len(checks)-len(failed),'failed':failed,'runtime':'NOT_TESTED'},indent=2,ensure_ascii=False))
raise SystemExit(bool(failed))
