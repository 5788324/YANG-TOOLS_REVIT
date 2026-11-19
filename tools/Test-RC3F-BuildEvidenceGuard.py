#!/usr/bin/env python3
"""RC3F source contracts derived from independently reviewed RC3E Phase A V4 logs.
These checks catch the 21 distinct previously diagnosed source errors; they do not compile C#.
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1] / 'src' / 'YangTools.Revit'
checks = []
def src(path): return (ROOT / path).read_text('utf-8-sig')
def ck(name, val): checks.append((name,bool(val)))

cleaner = src('UI/ViewGraphicCleanerWindow.xaml.cs')
ck('CS0103 DiagnosticLog namespace imported', 'using YangTools.Revit.Core;' in cleaner and 'DiagnosticLog.WriteError(' in cleaner)
types = src('Core/FamilyInstances/FamilyTypeExcelImportService.cs')
instances = src('Core/FamilyInstances/FamilyInstanceExcelImportService.cs')
ck('CS1061 family-type Parameter lacks IsValidObject usage', not re.search(r'\b(?:parameter|actual)\.IsValidObject\b|\bp\.Parameter\.IsValidObject\b',types))
ck('CS1061 family-instance Parameter lacks IsValidObject usage',not re.search(r'\b(?:parameter|actual)\.IsValidObject\b|\bp\.Parameter\.IsValidObject\b',instances))
ck('pre-commit type owner checked', 'type == null || !type.IsValidObject' in types)
ck('post-commit type parameter readback retained','StableParameterKey.ResolveByDisplayLabel(type, p.Label)' in types and 'InternalEqual(ReadInternalValue(actual), p.AppliedValue)' in types)
ck('post-commit instance parameter readback retained','StableParameterKey.ResolveByDisplayLabel(element, p.Label)' in instances and 'InternalValuesEqual(ReadInternalValue(actual), p.AppliedValue)' in instances)
dim = src('Commands/DimTextOverrideCommand.cs')
ck('CS0104 Dimension exception namespace deconflicted','using Autodesk.Revit.Exceptions;' not in dim and 'catch (Autodesk.Revit.Exceptions.OperationCanceledException)' in dim)
ck('dimension transaction and rollback retained','val2.Start()' in dim and 'val2.RollBack()' in dim)
for path in ['UI/ColumnAlongPathWindow.xaml.cs','UI/LinearPlacementWindow.xaml.cs','UI/FamilyInstanceManagerWindow.xaml.cs']:
    ck('CS0104 WPF TextBox explicit alias '+path, 'using TextBox = System.Windows.Controls.TextBox;' in src(path))
ck('CS0104 WPF Binding explicit alias', 'using Binding = System.Windows.Data.Binding;' in src('UI/FamilyInstanceManagerWindow.xaml.cs'))
color = src('Core/ProjectAssets/ProjectAssetLineColorService.cs')
ck('CS0104 line color Revit API type', 'new Autodesk.Revit.DB.Color(request.Red, request.Green, request.Blue)' in color and 'new Color(' not in color)
ck('line color transaction and rollback remain', 'transaction.Commit()' in color and 'transaction.RollBack()' in color)
face = src('UI/FaceBasedConverterWindow.xaml.cs')
request = src('Core/FaceBasedConversionRequest.cs')
ck('CS1061 face conversion captured suffix name matches', 'typeItem.Suffix,' in face and 'public string Suffix { get; }' in request and 'Suffix = row.ConvertedSuffix' in request)
ck('face source request frozen', 'new FaceBasedConversionRequest(' in request and 'InstanceIds = row.InstanceIds.ToArray()' in request)
asset = src('UI/ProjectAssetManagerWindow.xaml.cs')
ck('CS0176 no unqualified Visibility.Visible on line style', not re.search(r'LineStyleDataGrid\.Visibility\s*!=\s*Visibility\.Visible',asset))
ck('CS0176 fully qualified WPF Visibility exists twice',asset.count('LineStyleDataGrid.Visibility != System.Windows.Visibility.Visible')>=2)
project = src('YangTools.Revit.csproj')
ck('MSB3052 numeric configuration implicit defines suppressed', '<DisableImplicitConfigurationDefines>true</DisableImplicitConfigurationDefines>' in project)
ck('explicit 2024 symbols retained','<DefineConstants>REVIT2021_OR_GREATER;REVIT2022_OR_GREATER;REVIT2023_OR_GREATER;REVIT2024_OR_GREATER</DefineConstants>' in project)
ck('explicit 2025 symbols retained','REVIT2025_OR_GREATER</DefineConstants>' in project)
ck('net48 retained','<TargetFramework>net48</TargetFramework>' in project)
ck('net8.0-windows retained','<TargetFramework>net8.0-windows</TargetFramework>' in project)
ck('deployment opt-in remains',"'$(DeployAfterBuild)' == 'true'" in project)
for n,passed in checks:print(('PASS' if passed else 'FAIL')+': '+n)
print(f'RC3F Phase A V4 blocker guard: {sum(p for _,p in checks)}/{len(checks)} static contracts PASS; WINDOWS BUILD NOT_TESTED')
sys.exit(0 if all(p for _,p in checks) else 1)
