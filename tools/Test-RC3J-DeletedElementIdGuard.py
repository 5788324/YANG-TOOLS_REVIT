#!/usr/bin/env python3
"""Static source regression for invalid Revit Element access after Document.Delete.
SOURCE-ONLY: not a Windows compile nor an Autodesk Revit runtime test.
"""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parents[1]
source = (root/'src/YangTools.Revit/Core/DetailLineCleanup.cs').read_text(encoding='utf-8-sig')
apply = source.split('internal static List<ElementId> Apply(', 1)[1]
checks = {
    'keeper ID captured before any modifications': apply.index('ElementId keeperId = keep.Element.Id;') < apply.index('keep.Element.SetGeometryCurve(target, false)'),
    'member IDs all captured before delete': apply.index('List<ElementId> redundantIds = plan.Members') < apply.index('doc.Delete(redundantId)'),
    'iterate detached ElementId values': 'foreach (ElementId redundantId in redundantIds)' in apply,
    'delete by detached ID': 'doc.Delete(redundantId)' in apply,
    'verify deletion by detached ID': 'removed.Contains(redundantId)' in apply,
    'reject unexpected cascaded deletions': 'removed.Count != 1' in apply,
    'retain original keeper ID': 'survivors.Add(keeperId)' in apply,
    'rollback on transaction errors': 'tx.RollBack()' in apply,
    'no document-invalidated Element.Id access in deletion loop': not re.search(r'foreach\s*\(Segment\s+source\s+in\s+plan\.Members\)', apply),
    'no legacy post-delete source.Element.Id': 'removed.Contains(source.Element.Id)' not in apply,
}
for k, v in checks.items():
    print(('PASS' if v else 'FAIL') + ': ' + k)
print(f'HF1_DELETE_ID_GUARD={sum(checks.values())}/{len(checks)}')
sys.exit(0 if all(checks.values()) else 1)
