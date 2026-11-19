#!/usr/bin/env python3
"""Check compile-blocking Color binding regression and basic safety of RC3E.

Static checks only; actual dotnet/MSBuild verification must occur in Windows.
"""
from __future__ import annotations
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'src/YangTools.Revit/Core/ProjectAssets/ProjectAssetLineDuplicateService.cs'
PROJECT = ROOT / 'src/YangTools.Revit/YangTools.Revit.csproj'


def main():
    source = SOURCE.read_text('utf-8-sig')
    project = PROJECT.read_text('utf-8-sig')
    tests = {
        'fully qualified Revit Color allocation': 'new Autodesk.Revit.DB.Color(originalColor.Red, originalColor.Green, originalColor.Blue)' in source,
        'fully qualified Revit Color readback signature': 'string name, Autodesk.Revit.DB.Color color, int? weight' in source,
        'no unqualified new Color usage in duplicate service': not re.search(r'\bnew\s+Color\s*\(', source),
        'no unqualified Color declaration in duplicate service': not re.search(r'\bColor\s+color\s*,', source.replace('Autodesk.Revit.DB.Color', '')),
        'source line category persistence readback intact': 'ReadbackConfirmed = Matches(' in source,
        'Revit transaction rollback intact': 'tx.RollBack()' in source,
        'Revit 2024 framework maintained': "<TargetFramework>net48</TargetFramework>" in project,
        'Revit 2025 framework maintained': "<TargetFramework>net8.0-windows</TargetFramework>" in project,
        'WPF UI remains enabled': '<UseWPF>true</UseWPF>' in project,
        'deployment remains opt-in': "'$(DeployAfterBuild)' == 'true'" in project,
    }
    for name, passed in tests.items():
        print(f'{"PASS" if passed else "FAIL"}: {name}')
    print(f'RC3E compile guard: {sum(tests.values())}/{len(tests)} passed; Windows build NOT_TESTED')
    return 0 if all(tests.values()) else 1


if __name__ == '__main__':
    raise SystemExit(main())
