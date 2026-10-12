# Mimo Windows/Revit Execution Task — Rebar R07

You are the Windows/Revit execution agent. Do not declare final GO. Do not modify source unless compilation is impossible; if a minimal compile fix is unavoidable, record exact diff.

## Input
Use the bound R07 source ZIP and verify its sidecar SHA before build.

## Required
1. Fresh extract.
2. Run all tools/Test-*.py and UI static.
3. Revit 2024 build 0 error; Revit 2025 compile-only 0 error.
4. Deploy only Revit 2024. Prove Build=Deploy=Loaded DLL SHA from the running assembly path.
5. Use a fresh test project/fixture. Do not touch Revit 2022 user work.

## A — dryRun
Call create_free_form_rebar with one non-planar path containing at least 4 points. Confirm ChangedModel=false and Rebar count unchanged.

## B — single non-planar Free Form
Apply the same request. Required evidence:
- Created ElementId/UniqueId/Host.
- R00 inspect readback EngineMode=FreeForm.
- Centerline has the expected multiple segments and actual XYZ variation in more than one plane.
- FreeFormIsUnconstrained=true if the API exposes the property; otherwise mark FIELD_NOT_AVAILABLE, not PASS.
- Transaction Committed.

## C — explicit 3-bar Free Form set
Create one Free Form object from 3 explicit bar paths, each spatially offset. Required:
- one created Rebar ElementId;
- readback demonstrates multiple bar positions/set semantics if exposed;
- first/middle/last transformed geometry are spatially distinct;
- do not call it NumberWithSpacing unless Revit actually reports that.

## D — Undo
Post Revit Undo once after C. The whole C transaction must disappear. Record before/after Rebar count and inspect by Id.

## E — errors
At minimum: invalid host, invalid BarType, empty bars, one-point bar, duplicate consecutive points/zero segment, >500 bars, invalid style. All must fail without model residue.

## Screenshot
One screenshot must visibly show Autodesk Revit 2024.x title. Screenshot alone is not proof; journal/MCP/loaded SHA remain required.

## Return package
RESULT_YANGTOOLS_REBAR_R07_MIMO_YYYYMMDD_HHMM.zip containing RESULT.md, build logs, static, SHA triple, fixture, raw MCP JSON, before/after inspector JSON, undo evidence, screenshot, and source diff marker.
