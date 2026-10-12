# Mimo Windows/Revit Execution Task — Rebar R08

You are the Windows/Revit execution agent. Do not declare final GO. Do not modify source unless compilation is impossible; if a minimal compile fix is unavoidable, record the exact diff. Do not touch the user's Revit 2022 session.

## Input
Use the bound R08 source ZIP and verify its sidecar SHA before build.

## Required build gates
1. Fresh extract.
2. Run all `tools/Test-*.py` and UI static.
3. Revit 2024 build: 0 error.
4. Revit 2025 compile-only: 0 error.
5. Deploy only Revit 2024 and prove Build=Deploy=Loaded DLL SHA from the running assembly path/journal/MCP.

## Fixture
Create or reuse R07 raw-curve Free Form test bars. The target for B/C must satisfy:
- EngineMode=FreeForm
- FreeFormIsUnconstrained=true
- FreeFormHasValidServer=false

Do not use a Shape Driven bar as a successful R08 target.

## A — dryRun
Use `modify_free_form_rebar` on an R07 single Free Form with a clearly different non-planar path (>=4 points).
Required:
- Success=true
- ChangedModel=false
- same Rebar count
- target geometry/readback unchanged

## B — single-bar in-place geometry modify
Apply the A request.
Required before/after evidence:
- same ElementId
- same UniqueId
- same HostId
- EngineMode remains FreeForm
- FreeFormIsUnconstrained remains true
- HasValidServer remains false
- centerline geometry/length is demonstrably different
- native validation Success
- Transaction Committed

A delete/recreate with a new Id is FAIL.

## C — Free Form set count change
Create/reuse one explicit 3-bar R07 Free Form set, then use R08 to replace it with **4 explicit spatially separated paths**.
Required:
- same Rebar ElementId/UniqueId/Host
- NumberOfBarPositions 3 -> 4
- LayoutRule as actually reported by Revit (do not relabel it NumberWithSpacing)
- 4 transformed samples if exposed, with spatially distinct geometry
- remains unconstrained/no valid server

## D — Undo
After C, post one native Revit Undo.
Required:
- same Rebar Id still exists
- NumberOfBarPositions returns 4 -> 3
- pre-C geometry/sample state is restored

## E — safe rejection / errors
At minimum:
- Shape Driven target
- invalid RebarId
- wrong ExpectedUniqueId
- empty bars
- one-point bar
- duplicate consecutive/zero-length segment
- >500 bars
All must fail without residue or target corruption.

If you can obtain a genuine server-driven or constrained Free Form sample, verify R08 rejects it without calling DisconnectFromServer. If no such sample exists, mark `CONSTRAINED_FREEFORM_NOT_TESTED`; do not fabricate PASS.

## Screenshot
One screenshot should visibly show Autodesk Revit 2024.x. If automation captures the wrong window, mark `SCREENSHOT_INVALID`; journal/MCP/SHA remain the core runtime evidence.

## Return package
`RESULT_YANGTOOLS_REBAR_R08_MIMO_YYYYMMDD_HHMM.zip` containing RESULT.md, build logs, static logs, SHA triple, fixture notes, raw MCP JSON, before/after inspector JSON, Undo evidence, screenshot, and `NO_SOURCE_DIFF` or exact source diff.
