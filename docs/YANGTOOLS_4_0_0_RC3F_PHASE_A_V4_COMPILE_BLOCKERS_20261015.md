# YangTools 4.0.0 RC3F | Phase A V4 Build Failure Remediation

Status: SOURCE STATIC GO ONLY. Revit 2024/2025 Windows builds, UI, RVT and Ctrl+Z are NOT_TESTED on RC3F. Product release remains NO-GO.

## Canonical source
- Input: RC3E `YangTools_Project_4.0.0_Source_RC3E_COLOR_TYPE_FIX_STATIC_20261014.zip`, SHA-256 `8FCFFC4EF4A1A68A7BED7EB9024F264909A22FAAAC20497F1BC862E3DE3A6937`.
- Evidence: `YangTools_RC3E_PHASE_A_V4_20261015T013431Z_782dd019.zip`, SHA-256 `84F8EA149DA1CCF76A0145C04753FDBA11E17A6F5034F02ED7461AA2C579EB32`, CRC PASS, 11 records; source-files-2024/2025 manifests both match input ZIP 655/655.
- Actual Windows source compiler errors: 21 distinct positions in each 2024 and 2025 build, 0 DLLs, ExitCode=1. The log echoes errors twice, so 42 raw error rows per build. The build summary records 354 warnings (2024) and 392 (2025); many warning rows repeat in the full log.

## Source corrections
- Missing `DiagnosticLog` reference: import `YangTools.Revit.Core` in ViewGraphicCleanerWindow.
- `Autodesk.Revit.DB.Parameter` does not expose `IsValidObject`: remove invalid Parameter member checks; retain owner Element valid-object checks, nonnull guards, transaction checks and post-commit re-resolution/value readback.
- `InvalidOperationException`: remove unused `Autodesk.Revit.Exceptions` using from DimTextOverrideCommand; fully qualified cancellation exception handlers remain.
- WPF `TextBox`/`Binding` collisions: explicit aliases in ColumnAlongPathWindow, LinearPlacementWindow and FamilyInstanceManagerWindow.
- Line-color Revit API: explicitly `new Autodesk.Revit.DB.Color(...)`.
- FaceBasedConversionRequest.Item renamed suffix snapshot field: use `.Suffix` in FaceBasedConverterWindow while preserving immutable snapshot behavior.
- WPF `Visibility`: fully qualify `System.Windows.Visibility.Visible` in ProjectAssetManagerWindow two call sites.
- `MSB3052` numeric `/define:2024` or `/define:2025` warning: add `<DisableImplicitConfigurationDefines>true</DisableImplicitConfigurationDefines>` to the project. Explicit `REVIT20xx_OR_GREATER` symbols remain untouched. This follows Microsoft.NET.Sdk.CSharp.targets support for `DisableImplicitConfigurationDefines`; Windows build still required.

## Static test hygiene
- Updated old RC2G and RC2H static contract assertions to require compilable fully qualified names. Did not remove those tests.
- Added Test-RC3F-BuildEvidenceGuard.py (24 compile-blocker contract assertions).
- Comprehensive Python source contract scripts: 71/71 PASS in the working tree (including new RC3F guard). Fresh source ZIP test run and CRC/SHA integrity must be recorded in the independent external acceptance report.

## Next gate
Use packaged Gemini Phase A V5 isolated compilation kit only, collecting real `dotnet build` output and hashes for Revit 2024 net48 and 2025 net8.0-windows. No production addin deployment, no RVT edits, no Git push, no source fixes by Gemini. Only after ChatGPT reviews genuine Phase A BUILD PASS for both platforms can Phase B (isolated loaded DLL/WPF) be assigned, then Phase C (test RVT transaction, model readback, single Undo).
