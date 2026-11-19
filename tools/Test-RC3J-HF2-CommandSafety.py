#!/usr/bin/env python3
"""HF2 SOURCE ONLY guard. This cannot establish Autodesk runtime correctness."""
from pathlib import Path
import re
r=Path(__file__).resolve().parents[1]
core=(r/'src/YangTools.Revit/Core/DetailLineCleanup.cs').read_text(encoding='utf-8-sig')
checks={
    'self ID excluded from dependents': 'id.GetIdValue() != selfId' in core and 'GetDependentElements(null).Any(' in core,
    'dimension reference scan retained': 'foreach (Dimension dimension' in core and 'dimension.References' in core,
    'T branch joint checks all selected valid segments': 'TouchesFiniteSegment(joint, s)' in core and 'lines.Any(s => !memberIds.Contains(' in core,
    'finite segment bounds checked': 't < -extension || t > 1 + extension' in core,
    'branch only at interior degree two joints': 'degrees[i] == 2' in core,
    'positive-length overlapping rejection retained': 'PositiveOverlap(group[i], group[j])' in core,
    'unexpected deletion cascade still rejected': 'removed.Count != 1' in core,
    'delete old element access regression avoided': 'removed.Contains(source.Element.Id)' not in core,
    'single undoable Revit transaction': 'new Transaction(doc, txName)' in core,
}
for short in ['MergeCollinearDetailLinesCommand','RemoveOverlappingDetailLinesCommand']:
    s=(r/f'src/YangTools.Revit/Commands/{short}.cs').read_text(encoding='utf-8-sig')
    checks[short+' clear before Apply']=s.index('uidoc.Selection.SetElementIds(new List<ElementId>());')<s.index('DetailLineCleanup.Apply(')
    checks[short+' remember and restore selection']='List<ElementId> selectionBefore =' in s and 'selectionBefore.Where(id => doc.GetElement(id) != null)' in s
    checks[short+' select retained IDs']='uidoc.Selection.SetElementIds(retained)' in s
    checks[short+' no backend error swallow']='catch (Exception ex)' in s and 'return Result.Failed;' in s
print('\n'.join(('PASS' if v else 'FAIL')+': '+k for k,v in checks.items()))
print(f'SOURCE_ONLY={sum(checks.values())}/{len(checks)}')
raise SystemExit(0 if all(checks.values()) else 1)
