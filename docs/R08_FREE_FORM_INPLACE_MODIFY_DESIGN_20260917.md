# R08 — Free Form In-place Modify

## Goal
Prove that R07 raw-curve/unconstrained Free Form Rebar can be edited in place without delete/recreate, while keeping the constrained/server-driven lane explicitly separate.

## API model
R08 uses `RebarFreeFormAccessor.SetCurves(...)` only.

Supported lane:
`FreeForm + IsUnconstrained=true + HasValidServer=false -> SetCurves -> same Rebar identity`

Rejected lane:
`Shape Driven` or `server-driven/constrained Free Form -> fail before write`

R08 must never call `DisconnectFromServer` to force a constrained bar into the raw-curve lane.

## Request
`FreeFormRebarModifyRequest`
- RebarId
- ExpectedUniqueId
- Bars (explicit point paths in mm)
- DryRun=true by default

## Success invariants
After `SetCurves` and regeneration:
- ElementId is unchanged.
- UniqueId is unchanged.
- Host is unchanged.
- Engine remains FreeForm.
- FreeForm remains unconstrained and without a valid server.
- `NumberOfBarPositions == Bars.Count`.
- native `RebarFreeFormValidationResult == Success`.
- transaction commit must be `Committed`.

Any invariant failure before commit triggers rollback.

## Product API
MCP:
`modify_free_form_rebar(rebar_id, bars, expected_unique_id="", dry_run=True)`

GUI:
`钢筋工具区 -> Free Form修改 R08`
The minimal GUI only edits a single-bar Free Form with two newly picked endpoints. Multi-bar/non-planar production edits use the typed MCP service in R08.

## Explicit non-scope
- IRebarUpdateServer implementation
- native Aligned/Surface Free Form server creation
- constrained Free Form editing
- Shape Driven conversion
- Coupler/Lap
- collision resolution
- BS8666

## Why this stage comes before a custom server
R07 established the second engine can create real Free Form Rebar. The next risk is modification semantics. `SetCurves` gives a small, native, testable in-place update boundary before introducing the substantially larger `IRebarUpdateServer` lifecycle/handle/constraint architecture.
