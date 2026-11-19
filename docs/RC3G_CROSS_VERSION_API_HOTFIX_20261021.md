# YangTools 4.0.0 RC3G — Cross-Version Rebar API Compile Hotfix Candidate (2026-10-21)

## Scope and evidence
- Input RC3F SHA-256: `A899CDBCC2342893ED39D6D11CFD637B58F75320FF589885CADB5E3C30C5DC43`; same source ZIP used by DeepSeek B4/C1.
- DeepSeek original B4/C1 evidence ZIP SHA-256 `F8D240BB487B32F99F342D7A42583BAE497F50E6C490CC6F612BC2CB9F3C3A0E`; 36 ZIP items, 36/36 CRC PASS.
- 2021 errors: 5 CS1061 `RebarFreeFormAccessor.RebarStyle`, not present in 2021 API.
- 2026 errors: 4 CS1739 `suppressHooks` renamed `suppressHooksAndCranks` in `Rebar.GetCenterlineCurves`, and 1 CS1501 `GetConstraintCandidatesForHandle(handle)` requires explicit target in 2026.
- 2022–2025 compiler logs report ExitCode=0 with source Modified=false; B4 GUI 2025.4 screenshots evidence visible windows for System Settings, MicroTool Box, Project Asset Manager, Family Instance Manager. Screenshots cannot establish actual save/modify/readback/Undo.
- This candidate has NOT been compiled on Windows; no formal Revit 2021/2026 Build GO, runtime GO or release GO.

## Changes, constrained
1. Rebar `GetCenterlineCurves` call sites: replace named arguments with equivalent positional arguments to bridge 2021–2027 second-parameter name change, preserving `false,false,false,IncludeAllMultiplanarCurves,position`.
2. RebarRelationshipService: `#if REVIT2026_OR_GREATER` use `GetConstraintCandidatesForHandle(handle,target.Id)` (target-scoped Autodesk API); <=2025 retain the historical one-argument call.
3. RebarGradientSetService: Revit 2021 FreeForm accessor has *no* `RebarStyle` property; there is no verified lossless alternative. Compile guards remove references to missing API in 2021, and GR01 creation rejects with explicit `FREEFORM_STYLE_API_UNSUPPORTED_REVIT2021` **before Transaction.Start**, instead of silently setting/reading unknown style. For 2021 free-form *control* style, preview read throws explicit `CONTROL_STYLE_UNREADABLE_REVIT2021`. Source 2022+ retains original accessor setter/readback. 2021 GR01 **functional limitation remains**, not a 2021 feature GO; later safe API-specific implementation can restore full functionality after tests.
4. RebarInspectionService: 2021 free-form style is displayed as `UNAVAILABLE_REVIT2021_FREEFORM_STYLE` rather than fabricated; shape-driven `RebarShape.RebarStyle` unchanged.
5. Old Test-RebarR01 static contract fixed to accept the new positional API call; new Test-RC3G-MultiVersionApiGuard.py (16 source assertions) added.

## API references
- Revit2021 FreeFormAccessor property list: https://www.revitapidocs.com/2021.1/ab6e8382-13a4-714b-cc51-615edc6da020.htm
- Revit2026 centerline signature: https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/7be7e413-bfac-bbd3-17b6-ff2008771afa.htm
- Revit2026 candidate target-element overload: https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/7fb6f4f8-a01f-b6c5-e553-08197ef55db6.htm

## Protection and GO/NO-GO
- Revit 2023 is in production use: no deployment, testing, closing, focusing or modifying Revit2023. Run only isolated `dotnet build` with versioned NuGet API packages and `DeployAfterBuild=false`.
- 2025.5+/2026.5+/2027 require .NET10-specific separate compatibility validation. 2027 not installed and SDK10 missing per B4/C1, so NOT_TESTED.
- Do not mistake build success for Runtime, Ribbon, WPF, model readback or Undo.
- Existing RC3F source is preserved; no Git push; DeepSeek is Windows evidence executor; ChatGPT reviews source and all raw evidence before gate changes.
