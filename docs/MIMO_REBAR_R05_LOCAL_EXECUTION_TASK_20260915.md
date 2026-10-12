# Mimo Local Execution Task — YangTools Rebar R05

Date: 2026-09-15
Role: Windows / Revit execution only
Final GO/NO-GO: ChatGPT

## 0. Input identity

Use only the R05 source ZIP included in this handoff.
Verify its SHA-256 against the sidecar before extracting.

Do not use Google Drive.
Do not modify unrelated YangTools functions.
Do not run Revit 2025; 2025 is compile-only.

## 1. Build/static gates

1. Fresh extract.
2. Revit 2024 build: 0 errors.
3. Revit 2025 compile-only build: 0 errors.
4. Run all `tools/Test-*.py`: zero suite failures.
5. Record R00/R01/R02/R03/R04/R05 counts and UI static result.
6. 2024 Build SHA = Deploy SHA = Loaded SHA, where Loaded path is obtained from the running assembly, not inferred from `.addin`.

## 2. R00.4 gate

Create/read a Shape Driven set having Edge/Start/End host-face constraints.

`inspect_rebar` must report:
- SchemaVersion = R00.4
- each host-face constraint has `TargetHostFaceStableReferences`
- references are non-empty and parseable
- transformed sample geometry is still readable

## 3. R05-A dryRun

Choose an existing R03/R04 varying set.

Choose a **different** exposed planar host face that is a valid ToCover candidate for one handle (prefer Edge).

Call `rebind_shape_rebar_constraints(..., dry_run=true)`.

PASS requires:
- Success=true
- ChangedModel=false
- model/rebar identity unchanged
- candidate preflight succeeds

## 4. R05-B exact face retarget (core)

Apply the same retarget with `dry_run=false`.

PASS requires:
- same ElementId / UniqueId / Host
- requested handle remains `ToCover`
- R00.4 `TargetHostFaceStableReferences` equals the newly requested stable face reference
- target face before != target face after
- Varying/Uniform mode preserved
- unretargeted constraint signature preserved
- transformed geometry changes consistently with the new face (record before/after sample coordinates)
- transaction committed

If the fixture cannot produce a valid alternate ToCover candidate, create a new simple valid Rebar host fixture. Do not fake PASS.

## 5. R05-C Undo

After R05-B, post Revit Undo and wait for idle.

PASS requires:
- same Rebar still exists
- target host face stable reference returns to the original face
- identity/host/varying remain valid

## 6. R05-D Start/End smoke

Attempt one StartOfBar or EndOfBar retarget to a different valid exposed planar face on the same host.

PASS if a valid candidate exists and exact-face readback succeeds.
If no suitable candidate exists in the fixture, record `NOT_TESTED` with candidate evidence. Do not invent PASS.

## 7. Error matrix

At minimum:
- invalid RebarId
- ExpectedUniqueId mismatch
- invalid stable reference
- face belonging to a different host
- same current target face (must reject no-op)
- non-exposed/invalid cover face if a sample can be built
- Free Form input: `NOT_TESTED` if no sample exists

After each failure, verify the target Rebar remains unchanged.

## 8. UI

`钢筋工具区 -> 约束重绑 R05` interactive click is useful but not a Core blocker.
If automation cannot complete PickObject/PickFace dialogs, mark `UI_INTERACTIVE_NOT_TESTED`.

## 9. Evidence package

Return one ZIP:

`RESULT_YANGTOOLS_REBAR_R05_MIMO_YYYYMMDD_HHMM.zip`

Include:
- RESULT.md
- source/input SHA
- build 2024/2025 logs
- static logs/reports
- Build/Deploy/Loaded SHA evidence
- raw MCP JSON for all gates
- R00.4 before/after readbacks
- stable face references before/after
- Undo evidence
- journal excerpt / journal file proving Revit 2024
- screenshots only if their title bar visibly matches the claimed Revit version
- `diffs/README.txt`; use `NO_SOURCE_DIFF` if source was not changed

Do not label an untested item PASS.
